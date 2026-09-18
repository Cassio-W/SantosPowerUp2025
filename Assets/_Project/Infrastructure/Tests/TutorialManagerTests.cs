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
    public class TutorialManagerTests
    {
        private GameObject speechGo;
        private GameObject paperGo;
        private GameObject decisionGo;
        private GameObject tutorialGo;

        private NpcSpeechBubblePresenter speechBubble;
        private PaperDocumentPresenter paperPresenter;
        private DecisionOverlayPresenter decisionOverlay;
        private TutorialManager tutorialManager;

        private RunCatalog catalog;
        private RunStateMachine stateMachine;

        private CardDefinition tutCard1;
        private CardDefinition tutCard2;
        private CardDefinition mainCard;

        [SetUp]
        public void SetUp()
        {
            speechGo = new GameObject("TestSpeechBubble");
            speechBubble = speechGo.AddComponent<NpcSpeechBubblePresenter>();

            paperGo = new GameObject("TestPaper");
            paperPresenter = paperGo.AddComponent<PaperDocumentPresenter>();

            decisionGo = new GameObject("TestDecision");
            decisionOverlay = decisionGo.AddComponent<DecisionOverlayPresenter>();

            tutorialGo = new GameObject("TestTutorialManager");
            tutorialManager = tutorialGo.AddComponent<TutorialManager>();

            tutCard1 = CardDefinition.CreateRuntimeInstance("tut_1", "Bem-vindo ao Mandato", "Explicação 1", new ChoiceDefinition("Continuar"), new ChoiceDefinition("Continuar"), isTutorial: true);
            tutCard2 = CardDefinition.CreateRuntimeInstance("tut_2", "Gerenciando Ministérios", "Explicação 2", new ChoiceDefinition("Continuar"), new ChoiceDefinition("Continuar"), isTutorial: true);
            mainCard = CardDefinition.CreateRuntimeInstance("main_1", "Primeiro Decreto", "Proposta Real", new ChoiceDefinition("Aceitar"), new ChoiceDefinition("Recusar"), isTutorial: false);

            catalog = new RunCatalog();
            catalog.Build(
                tutorialCards: new[] { tutCard1, tutCard2 },
                startingCards: new[] { mainCard },
                catalogCards: null,
                perksList: null,
                eventsList: null,
                questsList: null,
                endingsList: null,
                startingActionsList: null,
                playTutorial: true
            );

            stateMachine = new RunStateMachine(seed: 42);
            stateMachine.StartRun(catalog.MainDeckCardIds, 42, catalog.TutorialCardIds);

            tutorialManager.Initialize(stateMachine, catalog, speechBubble, paperPresenter, decisionOverlay);
        }

        [TearDown]
        public void TearDown()
        {
            if (speechGo != null) Object.DestroyImmediate(speechGo);
            if (paperGo != null) Object.DestroyImmediate(paperGo);
            if (decisionGo != null) Object.DestroyImmediate(decisionGo);
            if (tutorialGo != null) Object.DestroyImmediate(tutorialGo);
        }

        [Test]
        public void IsTutorialCard_CorrectlyIdentifiesTutorialCards()
        {
            Assert.IsTrue(tutorialManager.IsTutorialCard(tutCard1));
            Assert.IsTrue(tutorialManager.IsTutorialCard(tutCard2));
            Assert.IsFalse(tutorialManager.IsTutorialCard(mainCard));
        }

        [Test]
        public void HasRemainingTutorialCards_ReturnsTrueWhenTutorialCardsInQueue()
        {
            Assert.IsTrue(tutorialManager.HasRemainingTutorialCards());
        }

        [Test]
        public void PresentTutorialStep_ShowsSpeechBubble_AndDisablesPaperAndDecisionUI()
        {
            bool stepStartedInvoked = false;
            tutorialManager.OnTutorialStepStarted += (card, step) => stepStartedInvoked = true;
            tutorialManager.TutorialNpcName = "ASSESSOR CHEFE";

            tutorialManager.PresentTutorialStep(tutCard1);

            Assert.IsTrue(tutorialManager.IsTutorialActive);
            Assert.IsTrue(speechBubble.IsVisible);
            Assert.AreEqual("ASSESSOR CHEFE", speechBubble.CurrentSpeaker);
            Assert.AreEqual("Explicação 1", speechBubble.CurrentText);
            Assert.IsFalse(decisionOverlay.IsVisible);
            Assert.IsTrue(stepStartedInvoked);
        }

        [Test]
        public void HandleSpeechCompleted_AdvancesStep_AndSubmitsChoice()
        {
            tutorialManager.PresentTutorialStep(tutCard1);
            Assert.AreEqual(0, tutorialManager.CurrentStepIndex);

            // Simula clique no botão de avançar do balão de fala
            speechBubble.HandleAdvanceAction();

            Assert.AreEqual(1, tutorialManager.CurrentStepIndex);
        }

        [Test]
        public void StepConfigurations_CanBeConfiguredAndQueried()
        {
            tutorialManager.PlayTutorial = true;
            Assert.IsTrue(tutorialManager.PlayTutorial);

            var step0 = new TutorialStepConfig
            {
                card = tutCard1,
                npcAnimationState = "Talking",
                targetCameraFov = 45f,
                enableCameraEffect = true
            };
            tutorialManager.StepConfigurations.Add(step0);

            Assert.AreEqual(1, tutorialManager.StepConfigurations.Count);
            Assert.AreEqual(tutCard1, tutorialManager.StepConfigurations[0].card);
            Assert.AreEqual("Talking", tutorialManager.StepConfigurations[0].npcAnimationState);
            Assert.AreEqual(45f, tutorialManager.StepConfigurations[0].targetCameraFov);
            Assert.IsTrue(tutorialManager.StepConfigurations[0].enableCameraEffect);

            var cards = tutorialManager.GetConfiguredCards();
            Assert.AreEqual(1, cards.Count);
            Assert.AreEqual(tutCard1, cards[0]);
        }

        [Test]
        public void CompleteTutorial_HidesSpeechBubble_AndRestoresDecisionOverlay()
        {
            bool finishedInvoked = false;
            tutorialManager.OnTutorialFinished += () => finishedInvoked = true;

            tutorialManager.PresentTutorialStep(tutCard1);
            Assert.IsTrue(tutorialManager.IsTutorialActive);

            tutorialManager.CompleteTutorial();

            Assert.IsFalse(tutorialManager.IsTutorialActive);
            Assert.IsFalse(speechBubble.IsVisible);
            Assert.IsTrue(decisionOverlay.IsVisible);
            Assert.IsTrue(finishedInvoked);
        }
    }
}
