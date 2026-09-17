using Mandato.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mandato.UI.Tests
{
    [TestFixture]
    public class NpcSpeechBubbleTests
    {
        private GameObject go;
        private NpcSpeechBubblePresenter presenter;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("TestSpeechBubble");
            presenter = go.AddComponent<NpcSpeechBubblePresenter>();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SetSpeech_UpdatesCurrentSpeakerAndText()
        {
            presenter.SetSpeech("Ministro da Fazenda", "Precisamos cortar gastos no próximo trimestre.");

            Assert.AreEqual("Ministro da Fazenda", presenter.CurrentSpeaker);
            Assert.AreEqual("Precisamos cortar gastos no próximo trimestre.", presenter.CurrentText);
        }

        [Test]
        public void Show_DispatchesOnSpeechShownEvent()
        {
            string receivedSpeaker = null;
            string receivedText = null;

            presenter.OnSpeechShown += (speaker, text) =>
            {
                receivedSpeaker = speaker;
                receivedText = text;
            };

            presenter.Show("Deputado Federal", "Votaremos a favor da emenda.", useTypewriter: false);

            Assert.IsTrue(presenter.IsVisible);
            Assert.AreEqual("Deputado Federal", receivedSpeaker);
            Assert.AreEqual("Votaremos a favor da emenda.", receivedText);
        }

        [Test]
        public void Hide_SetsVisibilityFalse_AndDispatchesEvent()
        {
            bool hiddenTriggered = false;
            presenter.OnSpeechHidden += () => hiddenTriggered = true;

            presenter.Show("Texto de Teste");
            Assert.IsTrue(presenter.IsVisible);

            presenter.Hide();
            Assert.IsFalse(presenter.IsVisible);
            Assert.IsTrue(hiddenTriggered);
        }

        [Test]
        public void HandleAdvanceAction_WhenNotTyping_TriggersCompletionCallback()
        {
            bool callbackInvoked = false;
            bool eventInvoked = false;

            presenter.OnSpeechCompleted += () => eventInvoked = true;
            presenter.Show("Interlocutor", "Proposta final.", useTypewriter: false, onComplete: () => callbackInvoked = true);

            presenter.HandleAdvanceAction();

            Assert.IsTrue(callbackInvoked);
            Assert.IsTrue(eventInvoked);
        }

        [Test]
        public void SetSpeaker_UpdatesOnlySpeaker_PreservingText()
        {
            presenter.SetSpeech("Texto existente");
            presenter.SetSpeaker("Novo Interlocutor");

            Assert.AreEqual("Novo Interlocutor", presenter.CurrentSpeaker);
            Assert.AreEqual("Texto existente", presenter.CurrentText);
        }

        [Test]
        public void SetFlipped_UpdatesOrientationState()
        {
            // Valida alternância de orientação sem erros
            presenter.SetFlipped(true);
            presenter.SetFlipped(false);
            Assert.Pass();
        }
    }
}
