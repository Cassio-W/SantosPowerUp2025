using System;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;
using Mandato.UI;
using UnityEngine;

namespace Mandato.Infrastructure
{
    public class FlipPhoneCoordinator
    {
        private readonly RunStateMachine stateMachine;
        private readonly RunCatalog catalog;
        private readonly FlipPhonePresenter phonePresenter;
        private readonly RetroMonitorPresenter monitorPresenter;
        private readonly DecisionOverlayPresenter overlayPresenter;

        public event Action<FlipPhoneUseReport> OnActionExecuted;

        public FlipPhoneCoordinator(
            RunStateMachine stateMachine,
            RunCatalog catalog,
            FlipPhonePresenter phonePresenter,
            RetroMonitorPresenter monitorPresenter = null,
            DecisionOverlayPresenter overlayPresenter = null)
        {
            this.stateMachine = stateMachine;
            this.catalog = catalog;
            this.phonePresenter = phonePresenter;
            this.monitorPresenter = monitorPresenter;
            this.overlayPresenter = overlayPresenter;

            Bind();
        }

        private void Bind()
        {
            if (phonePresenter == null) return;

            phonePresenter.OnActionRequested += HandleActionRequested;
            phonePresenter.OnPhoneOpened += HandlePhoneOpened;
            phonePresenter.OnPhoneClosed += HandlePhoneClosed;
        }

        public void Unbind()
        {
            if (phonePresenter == null) return;

            phonePresenter.OnActionRequested -= HandleActionRequested;
            phonePresenter.OnPhoneOpened -= HandlePhoneOpened;
            phonePresenter.OnPhoneClosed -= HandlePhoneClosed;
        }

        private void HandleActionRequested(string actionId)
        {
            RequestUseAction(actionId);
        }

        public void RefreshPhoneActions()
        {
            if (phonePresenter == null || stateMachine == null || catalog == null) return;

            var viewModels = new List<FlipPhoneActionViewModel>();
            string currentNpcId = stateMachine.CurrentCard != null ? stateMachine.CurrentCard.npcId : string.Empty;

            foreach (var action in catalog.Actions.Values)
            {
                if (action == null) continue;

                bool isConsumed = action.cooldownType == FlipPhoneCooldownType.SingleUse && stateMachine.RunState.IsActionConsumed(action.id);
                bool onCooldown = stateMachine.RunState.IsActionOnCooldown(action.id);
                int cooldownTurns = stateMachine.RunState.GetActionCooldown(action.id);
                bool conditionsMet = action.AreConditionsMet(stateMachine.RunState.stats, stateMachine.RunState.calendar.currentMonthIndex, stateMachine.RunState.activePerkIds, stateMachine.RunState.decisionHistory, currentNpcId);
                bool isUnlocked = stateMachine.RunState.IsActionUnlocked(action.id) || action.unlockByDefault;

                var vm = new FlipPhoneActionViewModel
                {
                    id = action.id,
                    displayName = action.displayName,
                    description = action.description,
                    categoryTag = action.categoryTag,
                    icon = action.icon,
                    isAvailable = isUnlocked && !isConsumed && !onCooldown && conditionsMet,
                    isOnCooldown = onCooldown,
                    cooldownTurnsRemaining = cooldownTurns,
                    isConsumed = isConsumed
                };

                viewModels.Add(vm);
            }

            phonePresenter.Refresh(viewModels);
        }

        public void OpenPhone()
        {
            if (phonePresenter != null && !phonePresenter.IsOpen)
            {
                RefreshPhoneActions();
                phonePresenter.Open();
            }
        }

        public void ClosePhone()
        {
            if (phonePresenter != null && phonePresenter.IsOpen)
            {
                phonePresenter.Close();
            }
        }

        private void HandlePhoneOpened()
        {
            RefreshPhoneActions();
        }

        private void HandlePhoneClosed()
        {
        }

        public FlipPhoneUseReport RequestUseAction(string actionId)
        {
            if (stateMachine == null || catalog == null)
                return new FlipPhoneUseReport { success = false, failReason = "Sistemas não inicializados." };

            if (!catalog.Actions.TryGetValue(actionId, out var actionDef) || actionDef == null)
            {
                return new FlipPhoneUseReport { success = false, failReason = $"Ação '{actionId}' não encontrada no catálogo." };
            }

            var report = FlipPhoneResolver.ResolveUse(
                stateMachine.RunState,
                stateMachine.DeckState,
                actionDef,
                catalog.Cards,
                stateMachine.CurrentCard,
                catalog.Perks
            );

            if (report.success)
            {
                // Atualiza monitor e overlay com novo snapshot
                if (monitorPresenter != null)
                {
                    monitorPresenter.UpdateSnapshot(stateMachine.RunState.GetSnapshot());
                }

                if (overlayPresenter != null)
                {
                    overlayPresenter.SetCorruptionLevel(stateMachine.RunState.stats.corruption);
                }

                RefreshPhoneActions();
                OnActionExecuted?.Invoke(report);
            }

            return report;
        }
    }
}
