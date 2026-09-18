using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Mandato.Presentation;
using Mandato.UI;

namespace Mandato.Infrastructure.Tests
{
    [TestFixture]
    public class PaperFocusableObjectTests
    {
        [Test]
        public void PaperFocusableObject_Initialization_SetsDefaults()
        {
            var go = new GameObject("PaperTestObject");
            var paperFocus = go.AddComponent<PaperFocusableObject>();

            // Verifica que o papel NÃO permite sair do foco ao clicar fora
            Assert.IsFalse(paperFocus.AllowUnfocusOnClickOutside, "PaperFocusableObject deve ter AllowUnfocusOnClickOutside = false por padrão.");
            Assert.IsFalse(paperFocus.UnfocusOnSecondClick, "PaperFocusableObject deve ter UnfocusOnSecondClick = false por padrão.");
            Assert.IsTrue(paperFocus.DetachFromParentOnFocus, "PaperFocusableObject deve ter DetachFromParentOnFocus = true por padrão.");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void PaperFocusableObject_GetTargetFocusedPosition_WithVector_ReturnsConfiguredPosition()
        {
            var go = new GameObject("PaperTestObject");
            var paperFocus = go.AddComponent<PaperFocusableObject>();

            Vector3 customPos = new Vector3(0.1f, 0.5f, -0.2f);
            Vector3 customRot = new Vector3(10f, 20f, 0f);

            paperFocus.FocusedPaperPosition = customPos;
            paperFocus.FocusedPaperRotation = customRot;
            paperFocus.FocusedPaperPoint = null;

            Assert.AreEqual(customPos, paperFocus.GetTargetFocusedPosition());
            Assert.AreEqual(Quaternion.Euler(customRot).eulerAngles.x, paperFocus.GetTargetFocusedRotation().eulerAngles.x, 0.01f);
            Assert.AreEqual(Quaternion.Euler(customRot).eulerAngles.y, paperFocus.GetTargetFocusedRotation().eulerAngles.y, 0.01f);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void PaperFocusableObject_GetTargetFocusedPosition_WithAnchorPoint_UsesAnchorTransform()
        {
            var parent = new GameObject("TableParent");
            var paperGo = new GameObject("Paper");
            paperGo.transform.SetParent(parent.transform);

            var anchorGo = new GameObject("FocusAnchor");
            anchorGo.transform.SetParent(parent.transform);
            anchorGo.transform.position = new Vector3(0.5f, 0.8f, -0.3f);
            anchorGo.transform.rotation = Quaternion.Euler(15f, 0f, 0f);

            var paperFocus = paperGo.AddComponent<PaperFocusableObject>();
            paperFocus.FocusedPaperPoint = anchorGo.transform;

            Assert.AreEqual(new Vector3(0.5f, 0.8f, -0.3f), paperFocus.GetTargetFocusedPosition());
            Assert.AreEqual(15f, paperFocus.GetTargetFocusedRotation().eulerAngles.x, 0.01f);

            Object.DestroyImmediate(parent);
        }

        [Test]
        public void PaperFocusableObject_SetFocused_DynamicUnparenting_DetachesAndRestores()
        {
            var handBone = new GameObject("PlayerHandBone");
            var paperGo = new GameObject("Paper");
            paperGo.transform.SetParent(handBone.transform);
            paperGo.transform.position = new Vector3(1f, 1f, 1f);

            var paperFocus = paperGo.AddComponent<PaperFocusableObject>();
            paperFocus.DetachFromParentOnFocus = true;
            paperFocus.ReparentOnUnfocus = true;

            // Focar deve desacoplar do osso do jogador
            paperFocus.SetFocused(true);
            Assert.IsTrue(paperFocus.IsFocused);
            Assert.IsNull(paperGo.transform.parent, "Papel deve ser desacoplado do osso da mão ao focar.");
            Assert.IsTrue(paperFocus.IsDetached);

            // Desfocar deve restaurar
            paperFocus.SetFocused(false);
            Assert.IsFalse(paperFocus.IsFocused);

            Object.DestroyImmediate(handBone);
            Object.DestroyImmediate(paperGo);
        }

        [Test]
        public void PaperFocusableObject_SetFocused_DeactivatesPlayerHandAnimation()
        {
            var go = new GameObject("PaperTestObject");
            var paperFocus = go.AddComponent<PaperFocusableObject>();

            var playerGo = new GameObject("PlayerHand");
            var animator = playerGo.AddComponent<Animator>();
            paperFocus.PlayerHandAnimator = animator;
            paperFocus.DisableHandAnimationOnFocus = true;

            Assert.DoesNotThrow(() => paperFocus.SetFocused(true));
            Assert.IsTrue(paperFocus.IsFocused);

            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void DecisionOverlayPresenter_BackButton_TriggersOnBackRequested()
        {
            var go = new GameObject("DecisionOverlayTest");
            var presenter = go.AddComponent<DecisionOverlayPresenter>();

            bool backRequested = false;
            presenter.OnBackRequested += () => backRequested = true;

            // Invoca método de controle de visibilidade
            presenter.SetBackVisible(true);
            Assert.DoesNotThrow(() => presenter.ClearChoices());

            Object.DestroyImmediate(go);
        }

        [Test]
        public void PaperFocusableObject_FocusedPosition_NotOverwrittenByHover()
        {
            var go = new GameObject("PaperTestObject");
            var paperFocus = go.AddComponent<PaperFocusableObject>();
            paperFocus.FocusedPaperPosition = new Vector3(0.167f, 1.111f, -3.86f);

            paperFocus.SetFocused(true);
            paperFocus.NotifyHoverEnter();

            // Simula tick de LateUpdate
            Assert.AreEqual(new Vector3(0.167f, 1.111f, -3.86f), paperFocus.GetTargetFocusedPosition());

            Object.DestroyImmediate(go);
        }

        [Test]
        public void CameraFocusManager_Focus_TracksMovingTarget()
        {
            var camGo = new GameObject("CameraGo");
            var cam = camGo.AddComponent<Camera>();
            var manager = camGo.AddComponent<CameraFocusManager>();

            var paperGo = new GameObject("Paper");
            var paperFocus = paperGo.AddComponent<PaperFocusableObject>();
            paperFocus.CustomTransitionDuration = 0.01f;

            manager.Focus(paperFocus);

            Assert.AreEqual(paperFocus, manager.CurrentFocusedObject);

            Object.DestroyImmediate(paperGo);
            Object.DestroyImmediate(camGo);
        }

        [Test]
        public void PaperFocusableObject_ReactivatePlayerHandAnimation_ExecutesState()
        {
            var go = new GameObject("PaperTestObject");
            var paperFocus = go.AddComponent<PaperFocusableObject>();

            var playerGo = new GameObject("PlayerHand");
            var animator = playerGo.AddComponent<Animator>();
            paperFocus.PlayerHandAnimator = animator;

            Assert.DoesNotThrow(() => paperFocus.ReactivatePlayerHandAnimation());

            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(go);
        }
    }
}
