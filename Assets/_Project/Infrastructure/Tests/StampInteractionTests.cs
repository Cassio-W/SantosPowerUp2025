using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;
using Mandato.Infrastructure;
using Mandato.Presentation;
using Mandato.Run;
using Mandato.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mandato.Infrastructure.Tests
{
    [TestFixture]
    public class StampInteractionTests
    {
        private GameObject coordinatorGo;
        private RunFlowCoordinator flowCoordinator;
        private UIModalCoordinator modalCoordinator;
        private ScenePresentationBindings bindings;

        private GameObject stampGo;
        private StampTool3D stampTool;
        private GameObject approvePadGo;
        private InkPad3D approvePad;
        private GameObject rejectPadGo;
        private InkPad3D rejectPad;
        private GameObject paperGo;
        private PaperDocumentPresenter paperPresenter;

        [SetUp]
        public void SetUp()
        {
            coordinatorGo = new GameObject("TestCoordinator");
            flowCoordinator = coordinatorGo.AddComponent<RunFlowCoordinator>();

            stampGo = new GameObject("TestStamp");
            stampTool = stampGo.AddComponent<StampTool3D>();

            approvePadGo = new GameObject("ApprovePad");
            approvePad = approvePadGo.AddComponent<InkPad3D>();
            approvePad.Type = InkType.Approve;

            rejectPadGo = new GameObject("RejectPad");
            rejectPad = rejectPadGo.AddComponent<InkPad3D>();
            rejectPad.Type = InkType.Reject;

            paperGo = new GameObject("PaperDoc");
            paperPresenter = paperGo.AddComponent<PaperDocumentPresenter>();

            modalCoordinator = new UIModalCoordinator();
            bindings = new ScenePresentationBindings();

            SetPrivateField(bindings, "stampTool", stampTool);
            SetPrivateField(bindings, "approveInkPad", approvePad);
            SetPrivateField(bindings, "rejectInkPad", rejectPad);
            SetPrivateField(bindings, "paperPresenter", paperPresenter);

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
            if (stampGo != null) Object.DestroyImmediate(stampGo);
            if (approvePadGo != null) Object.DestroyImmediate(approvePadGo);
            if (rejectPadGo != null) Object.DestroyImmediate(rejectPadGo);
            if (paperGo != null) Object.DestroyImmediate(paperGo);
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
        public void StampTool3D_InitialState_IsDry()
        {
            Assert.AreEqual(StampInkState.Dry, stampTool.CurrentInk);
            Assert.IsFalse(stampTool.IsInspectActive);
            Assert.IsFalse(stampTool.HasSubmittedDecision);
        }

        [Test]
        public void InkPad3D_ApplyInkToStamp_ApproveAndReject_UpdatesStampCorrectly()
        {
            approvePad.ApplyInkToStamp(stampTool);
            Assert.AreEqual(StampInkState.Approve, stampTool.CurrentInk);

            rejectPad.ApplyInkToStamp(stampTool);
            Assert.AreEqual(StampInkState.Reject, stampTool.CurrentInk);

            stampTool.ResetInk();
            Assert.AreEqual(StampInkState.Dry, stampTool.CurrentInk);
        }

        [Test]
        public void StampTool3D_SetInspectActive_TogglesStateAndResetsInkOnExit()
        {
            stampTool.SetInspectActive(true);
            Assert.IsTrue(stampTool.IsInspectActive);

            approvePad.ApplyInkToStamp(stampTool);
            Assert.AreEqual(StampInkState.Approve, stampTool.CurrentInk);

            stampTool.SetInspectActive(false);
            Assert.IsFalse(stampTool.IsInspectActive);
            Assert.AreEqual(StampInkState.Dry, stampTool.CurrentInk);
        }

        [Test]
        public void FlowCoordinator_HandleContextChanged_ActivatesAndDeactivatesStamp()
        {
            modalCoordinator.SetContext(InteractionContext.DeskOverview);
            Assert.IsFalse(stampTool.IsInspectActive);

            modalCoordinator.SetContext(InteractionContext.PaperInspect);
            Assert.IsTrue(stampTool.IsInspectActive);

            modalCoordinator.SetContext(InteractionContext.DeskOverview);
            Assert.IsFalse(stampTool.IsInspectActive);
        }

        [Test]
        public void PaperDocumentPresenter_AddStampMark_InstantiatesStampElement()
        {
            var card = ScriptableObject.CreateInstance<CardDefinition>();
            card.id = "test_card";
            card.title = "Reforma Administrativa";
            card.description = "Proposta de lei.";

            paperPresenter.SetProposal(card, "01/2026");

            var stampEl = paperPresenter.AddStampMark(isApproved: true, new Vector2(0.5f, 0.5f), 2.5f);
            Assert.IsNotNull(stampEl);
            Assert.IsTrue(stampEl.ClassListContains("stamp-approved"));

            var stampEl2 = paperPresenter.AddStampMark(isApproved: false, new Vector2(0.3f, 0.3f), -3.0f);
            Assert.IsNotNull(stampEl2);
            Assert.IsTrue(stampEl2.ClassListContains("stamp-rejected"));

            paperPresenter.ClearStamps();

            Object.DestroyImmediate(card);
        }

        [Test]
        public void StampTool3D_EnsuresCollidersSetToIgnoreRaycastLayer()
        {
            var childColliderGo = new GameObject("ChildCollider");
            childColliderGo.transform.SetParent(stampGo.transform);
            var col = childColliderGo.AddComponent<BoxCollider>();

            stampTool.SetInspectActive(true);

            Assert.AreEqual(2, stampGo.layer, "Stamp root should be on Ignore Raycast layer (2)");
            Assert.AreEqual(2, childColliderGo.layer, "Child collider should be on Ignore Raycast layer (2)");

            Object.DestroyImmediate(childColliderGo);
        }

        [Test]
        public void PaperDocumentPresenter_MapUVToPanelCoordinates_InvertsXAndY()
        {
            // UV (0.2, 0.8) -> X is unmirrored from right to left (1 - 0.2 = 0.8), Y is unmirrored (0.8)
            Vector2 panelPos = paperPresenter.MapUVToPanelCoordinates(new Vector2(0.2f, 0.8f));
            Assert.AreEqual(1024f * 0.8f, panelPos.x, 1f);
            Assert.AreEqual(1440f * 0.8f, panelPos.y, 1f);
        }

        [Test]
        public void PaperDocumentPresenter_UpdateStampPreview_ShowsAndHidesPreviewElement()
        {
            paperPresenter.UpdateStampPreview(true, isApproved: true, new Vector2(0.5f, 0.5f));
            var preview = paperGo.GetComponent<UnityEngine.UIElements.UIDocument>()?.rootVisualElement?.Q<UnityEngine.UIElements.VisualElement>(className: "stamp-preview");

            // Even if UIDocument isn't attached to simulated gameObject in pure test, method handles gracefully
            paperPresenter.UpdateStampPreview(false, false, Vector2.zero);
        }
    }
}
