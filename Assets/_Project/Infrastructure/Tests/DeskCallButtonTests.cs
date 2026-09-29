using System;
using Mandato.Presentation;
using Mandato.Infrastructure;
using NUnit.Framework;
using UnityEngine;

namespace Mandato.Infrastructure.Tests
{
    [TestFixture]
    public class DeskCallButtonTests
    {
        private GameObject buttonObject;
        private DeskCallButton deskButton;

        [SetUp]
        public void SetUp()
        {
            buttonObject = new GameObject("TestDeskCallButton");
            deskButton = buttonObject.AddComponent<DeskCallButton>();
        }

        [TearDown]
        public void TearDown()
        {
            if (buttonObject != null)
            {
                UnityEngine.Object.DestroyImmediate(buttonObject);
            }
        }

        [Test]
        public void Press_WhenInteractable_TriggersOnCallRequestedAndOnButtonPressed()
        {
            bool callRequested = false;
            bool buttonPressedEvent = false;

            deskButton.OnCallRequested += () => callRequested = true;
            deskButton.onButtonPressed.AddListener(() => buttonPressedEvent = true);

            deskButton.Press();

            Assert.IsTrue(callRequested, "OnCallRequested deveria ter sido disparado ao pressionar.");
            Assert.IsTrue(buttonPressedEvent, "UnityEvent onButtonPressed deveria ter sido disparado.");
        }

        [Test]
        public void Press_WhenNotInteractable_DoesNotTriggerEvents()
        {
            bool callRequested = false;
            bool buttonPressedEvent = false;

            deskButton.OnCallRequested += () => callRequested = true;
            deskButton.onButtonPressed.AddListener(() => buttonPressedEvent = true);

            deskButton.SetInteractable(false);
            Assert.IsFalse(deskButton.IsInteractable);

            deskButton.Press();

            Assert.IsFalse(callRequested, "OnCallRequested NÃO deveria disparar quando interactable for false.");
            Assert.IsFalse(buttonPressedEvent, "UnityEvent onButtonPressed NÃO deveria disparar quando interactable for false.");
        }

        [Test]
        public void PlayPressEffects_RunsSafelyWithoutNullReferenceExceptions()
        {
            Assert.DoesNotThrow(() => deskButton.PlayPressEffects(),
                "PlayPressEffects deve executar com segurança sem lançar exceções mesmo sem AudioSource ou Animator configurados.");

            deskButton.SetInteractable(false);
            Assert.DoesNotThrow(() => deskButton.PlayPressEffects(),
                "PlayPressEffects deve retornar com segurança sem executar efeitos quando inativo.");
        }

        [Test]
        public void StartPress_And_ReleasePress_TogglesHeldState_And_AudioLoop()
        {
            var audio = buttonObject.AddComponent<AudioSource>();
            deskButton.PressSound = AudioClip.Create("TestBuzz", 44100, 1, 44100, false);

            Assert.IsFalse(deskButton.IsHeldDown);

            deskButton.StartPress();
            Assert.IsTrue(deskButton.IsHeldDown, "IsHeldDown deve ser verdadeiro após StartPress.");
            Assert.IsTrue(audio.loop, "AudioSource deve estar com loop = true enquanto pressionado.");
            Assert.IsTrue(audio.isPlaying, "AudioSource deve estar tocando enquanto pressionado.");

            deskButton.ReleasePress();
            Assert.IsFalse(deskButton.IsHeldDown, "IsHeldDown deve ser falso após ReleasePress.");
            Assert.IsFalse(audio.loop, "AudioSource deve ter loop desativado após ReleasePress.");
            Assert.IsFalse(audio.isPlaying, "AudioSource deve parar de tocar após ReleasePress.");
        }

        [Test]
        public void ScenePresentationBindings_ExposesDeskCallButton()
        {
            var bindings = new ScenePresentationBindings();
            Assert.IsNull(bindings.DeskCallButton, "DeskCallButton deve ser nulo por padrão e opcional nos bindings.");
        }
    }
}
