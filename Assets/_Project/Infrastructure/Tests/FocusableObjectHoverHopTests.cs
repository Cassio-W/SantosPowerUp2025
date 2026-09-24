using System.Collections;
using Mandato.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mandato.Infrastructure.Tests
{
    [TestFixture]
    public class FocusableObjectHoverHopTests
    {
        private GameObject _testObject;
        private FocusableObject _focusable;

        [SetUp]
        public void SetUp()
        {
            _testObject = new GameObject("TestFocusableObject");
            _testObject.transform.position = new Vector3(0f, 1f, 0f);
            _focusable = _testObject.AddComponent<FocusableObject>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_testObject != null)
            {
                Object.DestroyImmediate(_testObject);
            }
        }

        [Test]
        public void HoverHop_DefaultValues_AreCorrectlyConfigured()
        {
            Assert.IsTrue(_focusable.EnableHoverHop, "EnableHoverHop deve ser true por padrão.");
            Assert.AreEqual(1f, _focusable.HoverHopIntensity, 0.001f, "HoverHopIntensity deve ser 1.0 por padrão.");
            Assert.AreEqual(new Vector3(0f, 0.035f, 0f), _focusable.HoverHopOffset, "HoverHopOffset padrão deve ser (0, 0.035, 0).");
            Assert.AreEqual(0.15f, _focusable.HoverHopDuration, 0.001f, "HoverHopDuration padrão deve ser 0.15s.");
            Assert.AreEqual(new Vector3(1.02f, 1.06f, 1.02f), _focusable.HoverHopPunchScale, "HoverHopPunchScale padrão deve ser (1.02, 1.06, 1.02).");
            Assert.IsFalse(_focusable.IsHopping, "Não deve estar em animação de pulinho inicialmente.");
        }

        [Test]
        public void HoverHop_Properties_CanBeModified()
        {
            _focusable.EnableHoverHop = false;
            Assert.IsFalse(_focusable.EnableHoverHop);

            _focusable.HoverHopIntensity = 1.5f;
            Assert.AreEqual(1.5f, _focusable.HoverHopIntensity, 0.001f);

            _focusable.HoverHopIntensity = -0.5f;
            Assert.AreEqual(0f, _focusable.HoverHopIntensity, 0.001f, "HoverHopIntensity não deve ser negativo.");

            _focusable.HoverHopOffset = new Vector3(0f, 0.08f, 0f);
            Assert.AreEqual(new Vector3(0f, 0.08f, 0f), _focusable.HoverHopOffset);

            _focusable.HoverHopDuration = 0.2f;
            Assert.AreEqual(0.2f, _focusable.HoverHopDuration, 0.001f);

            _focusable.HoverHopPunchScale = new Vector3(1.1f, 1.2f, 1.1f);
            Assert.AreEqual(new Vector3(1.1f, 1.2f, 1.1f), _focusable.HoverHopPunchScale);
        }

        [UnityTest]
        public IEnumerator HoverHop_NotifyHoverEnter_TriggersHopAnimation()
        {
            _focusable.EnableHoverHop = true;
            _focusable.HoverHopIntensity = 1f;
            _focusable.HoverHopDuration = 0.08f;

            _focusable.NotifyHoverEnter();

            Assert.IsTrue(_focusable.IsHovered, "Objeto deve estar em estado hovered.");
            Assert.IsTrue(_focusable.IsHopping, "A animação de pulinho (hop) deve ser iniciada.");

            // Aguarda o término da animação de pulinho
            yield return new WaitForSecondsRealtime(0.12f);

            Assert.IsFalse(_focusable.IsHopping, "A animação de pulinho deve finalizar.");
            Assert.AreEqual(Vector3.zero, _focusable.CurrentHopPosOffset, "CurrentHopPosOffset deve retornar a zero.");
            Assert.AreEqual(Vector3.one, _focusable.CurrentHopScaleMultiplier, "CurrentHopScaleMultiplier deve retornar a Vector3.one.");
        }

        [Test]
        public void HoverHop_WhenDisabled_DoesNotStartHopping()
        {
            _focusable.EnableHoverHop = false;
            _focusable.NotifyHoverEnter();

            Assert.IsFalse(_focusable.IsHopping, "Não deve iniciar pulinho se EnableHoverHop estiver false.");
        }

        [Test]
        public void HoverHop_WhenIntensityIsZero_DoesNotStartHopping()
        {
            _focusable.EnableHoverHop = true;
            _focusable.HoverHopIntensity = 0f;
            _focusable.NotifyHoverEnter();

            Assert.IsFalse(_focusable.IsHopping, "Não deve iniciar pulinho se HoverHopIntensity for zero.");
        }

        [Test]
        public void HoverHop_SetFocused_CancelsHopAndResetsTransformOffsets()
        {
            _focusable.EnableHoverHop = true;
            _focusable.HoverHopIntensity = 1f;
            _focusable.HoverHopDuration = 1.0f;

            _focusable.NotifyHoverEnter();
            Assert.IsTrue(_focusable.IsHopping);

            // Focar o objeto deve cancelar o pulinho imediatamente
            _focusable.SetFocused(true);

            Assert.IsFalse(_focusable.IsHopping, "Pulinho deve ser interrompido ao focar o objeto.");
            Assert.AreEqual(Vector3.zero, _focusable.CurrentHopPosOffset);
            Assert.AreEqual(Vector3.one, _focusable.CurrentHopScaleMultiplier);
        }

        [Test]
        public void HoverHop_OnDisable_CancelsHopAndResetsTransformOffsets()
        {
            _focusable.EnableHoverHop = true;
            _focusable.HoverHopIntensity = 1f;
            _focusable.HoverHopDuration = 1.0f;

            _focusable.NotifyHoverEnter();
            Assert.IsTrue(_focusable.IsHopping);

            _testObject.SetActive(false);

            Assert.IsFalse(_focusable.IsHopping, "Pulinho deve ser interrompido ao desativar o objeto.");
            Assert.AreEqual(Vector3.zero, _focusable.CurrentHopPosOffset);
            Assert.AreEqual(Vector3.one, _focusable.CurrentHopScaleMultiplier);
        }
    }
}
