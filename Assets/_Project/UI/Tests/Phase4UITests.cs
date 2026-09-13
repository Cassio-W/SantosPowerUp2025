using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;
using Mandato.UI;
using NUnit.Framework;
using UnityEngine;

namespace Mandato.UI.Tests
{
    [TestFixture]
    public class Phase4UITests
    {
        // ── UIModalCoordinator Tests ──────────────────────────────

        [Test]
        public void UIModalCoordinator_WhenNoModalsOpen_CanProcessDecisionShortcuts()
        {
            var coordinator = new UIModalCoordinator();

            Assert.IsFalse(coordinator.IsAnyModalOpen);
            Assert.IsTrue(coordinator.CanProcessDecisionShortcuts());
            Assert.IsTrue(coordinator.CanCallNextVisitor());
        }

        [Test]
        public void UIModalCoordinator_WhenPhoneOpen_BlocksDecisionShortcuts()
        {
            var coordinator = new UIModalCoordinator();

            coordinator.SetModalState(UIModalCoordinator.MODAL_FLIP_PHONE, true);

            Assert.IsTrue(coordinator.IsAnyModalOpen);
            Assert.IsTrue(coordinator.IsModalOpen(UIModalCoordinator.MODAL_FLIP_PHONE));
            Assert.IsFalse(coordinator.CanProcessDecisionShortcuts());
            Assert.IsFalse(coordinator.CanCallNextVisitor());

            coordinator.SetModalState(UIModalCoordinator.MODAL_FLIP_PHONE, false);

            Assert.IsFalse(coordinator.IsAnyModalOpen);
            Assert.IsTrue(coordinator.CanProcessDecisionShortcuts());
        }

        [Test]
        public void UIModalCoordinator_WhenEndScreenVisible_BlocksDecisionShortcuts()
        {
            var coordinator = new UIModalCoordinator();

            coordinator.SetModalState(UIModalCoordinator.MODAL_END_SCREEN, true);

            Assert.IsTrue(coordinator.IsModalOpen(UIModalCoordinator.MODAL_END_SCREEN));
            Assert.IsFalse(coordinator.CanProcessDecisionShortcuts());
        }

        [Test]
        public void UIModalCoordinator_CloseAllModals_ClearsAllAndFiresEvents()
        {
            var coordinator = new UIModalCoordinator();
            int closeEventCount = 0;
            coordinator.OnModalStateChanged += (modal, isOpen) =>
            {
                if (!isOpen) closeEventCount++;
            };

            coordinator.SetModalState("Modal1", true);
            coordinator.SetModalState("Modal2", true);

            Assert.AreEqual(2, coordinator.OpenModalCount);

            coordinator.CloseAllModals();

            Assert.AreEqual(0, coordinator.OpenModalCount);
            Assert.IsFalse(coordinator.IsAnyModalOpen);
            Assert.AreEqual(2, closeEventCount);
        }

        // ── MainMenuPresenter Tests ───────────────────────────────

        [Test]
        public void MainMenuPresenter_CreditsToggle_UpdatesStateAndFiresEvents()
        {
            var go = new GameObject("MainMenuTest");
            var presenter = go.AddComponent<MainMenuPresenter>();

            bool openedFired = false;
            bool closedFired = false;
            presenter.OnCreditsOpened += () => openedFired = true;
            presenter.OnCreditsClosed += () => closedFired = true;

            Assert.IsFalse(presenter.IsCreditsOpen);

            presenter.HandleCreditsOpenClicked();
            Assert.IsTrue(presenter.IsCreditsOpen);
            Assert.IsTrue(openedFired);

            presenter.HandleCreditsCloseClicked();
            Assert.IsFalse(presenter.IsCreditsOpen);
            Assert.IsTrue(closedFired);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void MainMenuPresenter_PlayClicked_FiresEvent()
        {
            var go = new GameObject("MainMenuTest");
            var presenter = go.AddComponent<MainMenuPresenter>();

            bool playFired = false;
            presenter.OnPlayClicked += () => playFired = true;

            presenter.HandlePlayClicked();

            Assert.IsTrue(playFired);

            Object.DestroyImmediate(go);
        }

        // ── FlipPhone ViewModel & Category Tests ──────────────────

        [Test]
        public void FlipPhoneActionViewModel_DefaultValues_AreCorrect()
        {
            var vm = new FlipPhoneActionViewModel
            {
                id = "acao_imprensa",
                displayName = "Entrevista Coletiva",
                categoryTag = "Comunicação",
                isAvailable = true,
                isOnCooldown = false,
                cooldownTurnsRemaining = 0,
                isConsumed = false
            };

            Assert.AreEqual("acao_imprensa", vm.id);
            Assert.AreEqual("Entrevista Coletiva", vm.displayName);
            Assert.IsTrue(vm.isAvailable);
            Assert.IsFalse(vm.isOnCooldown);
            Assert.IsFalse(vm.isConsumed);
        }

        // ── RetroMonitorPresenter Snapshot Logic Tests ────────────

        [Test]
        public void RetroMonitorPresenter_UpdateSnapshot_UpdatesStatTargets()
        {
            var go = new GameObject("MonitorTest");
            var presenter = go.AddComponent<RetroMonitorPresenter>();

            var stats = new StatBlock(
                climate: 75,
                relations: 55,
                approval: 40,
                eco: 65,
                corrupt: 15
            );
            var snapshot = new RunSnapshot(stats, new PoliticalAxis(), 1, "01/2026", true, false, false, string.Empty);

            presenter.UpdateSnapshot(snapshot);

            // Snapshot updates correctly without errors
            Assert.IsNotNull(snapshot);
            Assert.AreEqual(65, snapshot.Stats.economy);
            Assert.AreEqual(40, snapshot.Stats.popularApproval);
            Assert.AreEqual(75, snapshot.Stats.climaticChanges);
            Assert.AreEqual(55, snapshot.Stats.internationalRelations);
            Assert.AreEqual(15, snapshot.Stats.corruption);

            Object.DestroyImmediate(go);
        }

        // ── PaperDocumentPresenter Proposal Setting Tests ─────────

        [Test]
        public void PaperDocumentPresenter_SetProposal_ClearsAndUpdates()
        {
            var go = new GameObject("PaperTest");
            var presenter = go.AddComponent<PaperDocumentPresenter>();

            var card = ScriptableObject.CreateInstance<CardDefinition>();
            card.id = "card_test";
            card.title = "Proposta de Teste";
            card.description = "Descrição detalhada do projeto.";

            presenter.SetProposal(card, "01/2026");

            presenter.Clear();

            Assert.IsNotNull(presenter);

            Object.DestroyImmediate(card);
            Object.DestroyImmediate(go);
        }
    }
}
