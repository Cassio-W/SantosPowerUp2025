using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;
using Mandato.Infrastructure;
using Mandato.Presentation;
using Mandato.Run;
using Mandato.UI;
using NUnit.Framework;
using UnityEngine;

namespace Mandato.Infrastructure.Tests
{
    [TestFixture]
    public class PcFocusCoordinationTests
    {
        private GameObject coordinatorGo;
        private RunFlowCoordinator flowCoordinator;
        private UIModalCoordinator modalCoordinator;
        private ScenePresentationBindings bindings;

        private GameObject phoneGo;
        private FlipPhonePresenter phonePresenter;
        private GameObject decisionGo;
        private DecisionOverlayPresenter decisionPresenter;
        private GameObject deskButtonGo;
        private DeskCallButton deskButton;
        private GameObject monitorGo;
        private RetroMonitorPresenter monitorPresenter;

        [SetUp]
        public void SetUp()
        {
            coordinatorGo = new GameObject("TestCoordinator");
            flowCoordinator = coordinatorGo.AddComponent<RunFlowCoordinator>();

            phoneGo = new GameObject("TestPhone");
            phonePresenter = phoneGo.AddComponent<FlipPhonePresenter>();

            decisionGo = new GameObject("TestDecisionOverlay");
            decisionPresenter = decisionGo.AddComponent<DecisionOverlayPresenter>();

            deskButtonGo = new GameObject("TestDeskButton");
            deskButton = deskButtonGo.AddComponent<DeskCallButton>();

            monitorGo = new GameObject("TestMonitor");
            monitorPresenter = monitorGo.AddComponent<RetroMonitorPresenter>();

            modalCoordinator = new UIModalCoordinator();
            bindings = new ScenePresentationBindings();

            // Atribui via reflexão nos campos privados serializados de ScenePresentationBindings
            SetPrivateField(bindings, "flipPhonePresenter", phonePresenter);
            SetPrivateField(bindings, "decisionOverlayPresenter", decisionPresenter);
            SetPrivateField(bindings, "deskCallButton", deskButton);
            SetPrivateField(bindings, "retroMonitorPresenter", monitorPresenter);
            SetPrivateField(bindings, "flipPhoneObject", phoneGo);

            var stateMachine = new RunStateMachine(new RunState(), new DeckState());
            var catalog = new RunCatalog();
            var profileService = new RunProfileService();

            flowCoordinator.Initialize(
                stateMachine,
                catalog,
                profileService,
                bindings,
                null,
                requireSpace: true,
                callKey: KeyCode.Space,
                delayBetween: 0.1f,
                menuScene: "MenuV2",
                modalCoordinator: modalCoordinator
            );
        }

        [TearDown]
        public void TearDown()
        {
            if (coordinatorGo != null) Object.DestroyImmediate(coordinatorGo);
            if (phoneGo != null) Object.DestroyImmediate(phoneGo);
            if (decisionGo != null) Object.DestroyImmediate(decisionGo);
            if (deskButtonGo != null) Object.DestroyImmediate(deskButtonGo);
            if (monitorGo != null) Object.DestroyImmediate(monitorGo);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }

        [Test]
        public void SetPcFocusState_True_DeactivatesPhoneDecisionUIAndDeskButton()
        {
            // Estado inicial: tudo ativo e visível, celular fechado
            Assert.IsFalse(flowCoordinator.IsPcFocused);
            Assert.IsTrue(phonePresenter.IsInteractable);
            Assert.IsFalse(phonePresenter.IsOpen);
            Assert.IsTrue(decisionPresenter.IsVisible);
            Assert.IsTrue(deskButton.IsInteractable);
            Assert.IsTrue(modalCoordinator.CanProcessDecisionShortcuts());
            Assert.IsTrue(modalCoordinator.CanCallNextVisitor());

            // Abre o celular para testar que o foco no PC o fecha
            phonePresenter.Open();
            Assert.IsTrue(phonePresenter.IsOpen);

            // Foca no PC
            flowCoordinator.SetPcFocusState(true);

            Assert.IsTrue(flowCoordinator.IsPcFocused);
            Assert.IsTrue(modalCoordinator.IsModalOpen(UIModalCoordinator.MODAL_PC_FOCUS));
            Assert.IsFalse(modalCoordinator.CanProcessDecisionShortcuts());
            Assert.IsFalse(modalCoordinator.CanCallNextVisitor());

            // 1. Celular desativado e fechado
            Assert.IsFalse(phonePresenter.IsInteractable);
            Assert.IsFalse(phonePresenter.IsOpen);

            // 2. DecisionUI desativada
            Assert.IsFalse(decisionPresenter.IsVisible);

            // 3. Botão de mesa desativado
            Assert.IsFalse(deskButton.IsInteractable);
        }

        [Test]
        public void SetPcFocusState_False_RestoresPhoneDecisionUIAndDeskButton_WithoutOpeningPhone()
        {
            // Foca e depois desfoca do PC
            flowCoordinator.SetPcFocusState(true);
            flowCoordinator.SetPcFocusState(false);

            Assert.IsFalse(flowCoordinator.IsPcFocused);
            Assert.IsFalse(modalCoordinator.IsModalOpen(UIModalCoordinator.MODAL_PC_FOCUS));
            Assert.IsTrue(modalCoordinator.CanProcessDecisionShortcuts());
            Assert.IsTrue(modalCoordinator.CanCallNextVisitor());

            // 1. Celular volta a ser interativo, mas PERMANECE FECHADO
            Assert.IsTrue(phonePresenter.IsInteractable);
            Assert.IsFalse(phonePresenter.IsOpen);

            // 2. DecisionUI restaurada
            Assert.IsTrue(decisionPresenter.IsVisible);

            // 3. Botão de mesa restaurado
            Assert.IsTrue(deskButton.IsInteractable);
        }

        [Test]
        public void FocusingNonPcObject_DoesNotTriggerPcFocusOrOpenPhone()
        {
            var paperGo = new GameObject("PaperProposal");
            var paperFocusable = paperGo.AddComponent<FocusableObject>();

            Assert.IsFalse(flowCoordinator.IsPcFocused);
            Assert.IsFalse(phonePresenter.IsOpen);

            // Simula troca de foco para papel
            flowCoordinator.SetPcFocusState(flowCoordinator.IsMonitorFocusable(paperFocusable));

            Assert.IsFalse(flowCoordinator.IsPcFocused);
            Assert.IsFalse(phonePresenter.IsOpen);
            Assert.IsTrue(phonePresenter.IsInteractable);
            Assert.IsTrue(decisionPresenter.IsVisible);

            Object.DestroyImmediate(paperGo);
        }

        [Test]
        public void AuthorizeNextVisitor_WhenPcFocused_IsBlocked()
        {
            flowCoordinator.SetPcFocusState(true);

            // Tentar autorizar próximo visitante deve ser ignorado
            flowCoordinator.AuthorizeNextVisitor();

            Assert.IsTrue(flowCoordinator.IsPcFocused);
        }

        [Test]
        public void IsMonitorFocusable_CorrectlyIdentifiesMonitorObjects()
        {
            var pcFocusable = monitorGo.AddComponent<FocusableObject>();
            Assert.IsTrue(flowCoordinator.IsMonitorFocusable(pcFocusable));

            var otherGo = new GameObject("MonitorScreen");
            var otherFocusable = otherGo.AddComponent<FocusableObject>();
            Assert.IsTrue(flowCoordinator.IsMonitorFocusable(otherFocusable));

            var randomGo = new GameObject("CupOfCoffee");
            var randomFocusable = randomGo.AddComponent<FocusableObject>();
            Assert.IsFalse(flowCoordinator.IsMonitorFocusable(randomFocusable));

            Object.DestroyImmediate(otherGo);
            Object.DestroyImmediate(randomGo);
        }
    }
}
