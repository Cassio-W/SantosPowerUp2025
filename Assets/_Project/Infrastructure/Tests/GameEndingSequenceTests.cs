using System.Collections.Generic;
using Mandato.Core;
using Mandato.Presentation;
using Mandato.Run;
using Mandato.UI;
using NUnit.Framework;
using UnityEngine;

namespace Mandato.Infrastructure.Tests
{
    [TestFixture]
    public class GameEndingSequenceTests
    {
        private GameObject coordinatorGo;
        private RunFlowCoordinator coordinator;
        private GameObject cameraGo;
        private Camera targetCamera;
        private CameraFocusManager cameraFocus;
        private GameObject posCamGo;
        private GameObject posPalmasGo;
        private GameObject palmasPrefabGo;
        private GameObject speechBubbleGo;
        private NpcSpeechBubblePresenter speechBubble;
        private GameObject endScreenGo;
        private EndScreenPresenter endScreen;

        [SetUp]
        public void SetUp()
        {
            coordinatorGo = new GameObject("TestFlowCoordinator");
            coordinator = coordinatorGo.AddComponent<RunFlowCoordinator>();

            cameraGo = new GameObject("TestMainCamera");
            targetCamera = cameraGo.AddComponent<Camera>();
            cameraFocus = cameraGo.AddComponent<CameraFocusManager>();

            posCamGo = new GameObject("posCameraPalmas");
            posCamGo.transform.position = new Vector3(-2.68f, 2.28f, -2.64f);
            posCamGo.transform.rotation = Quaternion.Euler(0f, 25f, 0f);

            posPalmasGo = new GameObject("posPalmas");
            posPalmasGo.transform.position = new Vector3(-1.06f, 0f, 2.73f);
            posPalmasGo.transform.rotation = Quaternion.Euler(0f, 20f, 0f);

            palmasPrefabGo = new GameObject("TestPalmasPrefab");

            speechBubbleGo = new GameObject("TestSpeechBubble");
            speechBubble = speechBubbleGo.AddComponent<NpcSpeechBubblePresenter>();

            endScreenGo = new GameObject("TestEndScreen");
            endScreen = endScreenGo.AddComponent<EndScreenPresenter>();

            coordinator.PosCameraPalmas = posCamGo.transform;
            coordinator.PosPalmas = posPalmasGo.transform;
            coordinator.PalmasPrefab = palmasPrefabGo;
        }

        [TearDown]
        public void TearDown()
        {
            if (coordinator != null && coordinator.ActivePalmasInstance != null)
            {
                Object.DestroyImmediate(coordinator.ActivePalmasInstance);
            }

            if (coordinatorGo != null) Object.DestroyImmediate(coordinatorGo);
            if (cameraGo != null) Object.DestroyImmediate(cameraGo);
            if (posCamGo != null) Object.DestroyImmediate(posCamGo);
            if (posPalmasGo != null) Object.DestroyImmediate(posPalmasGo);
            if (palmasPrefabGo != null) Object.DestroyImmediate(palmasPrefabGo);
            if (speechBubbleGo != null) Object.DestroyImmediate(speechBubbleGo);
            if (endScreenGo != null) Object.DestroyImmediate(endScreenGo);
        }

        private RunSnapshot CreateTestSnapshot(bool isVictory, string reason = "")
        {
            return new RunSnapshot(
                stats: new StatBlock(),
                politicalAxis: new PoliticalAxis(),
                monthIndex: 48,
                displayDate: "12/2029",
                isOngoing: false,
                isDefeat: !isVictory,
                isVictory: isVictory,
                terminationReason: reason
            );
        }

        [Test]
        public void RunEnding_MovesCameraAndInstantiatesPalmasAtPosPalmas()
        {
            var termination = RunTermination.CreateVictory();
            var snapshot = CreateTestSnapshot(isVictory: true);

            coordinator.StartRunEndingSequence(termination, snapshot);

            Assert.IsNotNull(coordinator.ActivePalmasInstance, "Palmas prefab deve ser instanciado ao terminar o jogo.");
            Assert.AreEqual(posPalmasGo.transform.position, coordinator.ActivePalmasInstance.transform.position, "Palmas deve estar na posição de posPalmas.");
        }

        [Test]
        public void RunEnding_ShowsCongratulatoryDialogueBeforeEndScreenReport_AndOpensReportOnContinue()
        {
            var termination = RunTermination.CreateVictory();
            var snapshot = CreateTestSnapshot(isVictory: true);

            coordinator.StartRunEndingSequence(termination, snapshot);

            // 1. Diálogo de parabéns deve estar visível
            Assert.IsTrue(speechBubble.IsVisible, "O balão de diálogo deve ser exibido antes da UI de relatório.");
            StringAssert.Contains("Parabéns", speechBubble.CurrentText, "O texto do diálogo deve parabenizar o jogador.");

            // 2. O relatório final ainda NÃO deve estar visível enquanto o jogador não continuar
            Assert.IsFalse(endScreen.IsVisible, "O relatório final não deve aparecer antes de o jogador avançar o diálogo.");

            // 3. Jogador clica em continuar / avança
            speechBubble.HandleAdvanceAction();

            // 4. Balão de fala é ocultado e relatório final é aberto
            Assert.IsFalse(speechBubble.IsVisible, "O diálogo deve ser ocultado após continuar.");
            Assert.IsTrue(endScreen.IsVisible, "O relatório final deve abrir com as palmas no fundo após clicar em continuar.");
            Assert.IsNotNull(coordinator.ActivePalmasInstance, "Palmas continuam instanciadas no fundo.");
        }

        [Test]
        public void RunEnding_DefeatTermination_AlsoShowsCongratulationsForEffort()
        {
            var termination = RunTermination.CreateDefeat("Impeachment");
            var snapshot = CreateTestSnapshot(isVictory: false, reason: "Impeachment");

            coordinator.StartRunEndingSequence(termination, snapshot);

            Assert.IsTrue(speechBubble.IsVisible, "Diálogo deve ser exibido mesmo em caso de derrota.");
            StringAssert.Contains("Parabéns", speechBubble.CurrentText, "O diálogo deve parabenizar o empenho do jogador.");
        }
    }
}
