using System;
using Mandato.Presentation;
using Mandato.UI;
using UnityEngine;

namespace Mandato.Infrastructure
{
    [Serializable]
    public class ScenePresentationBindings
    {
        [Header("Apresentadores 3D e Visuais")]
        [SerializeField] private RunPresentationCoordinator presentationCoordinator;
        [SerializeField] private PaperDocumentPresenter paperPresenter;
        [SerializeField] private RetroMonitorPresenter retroMonitorPresenter;
        [SerializeField] private DecisionOverlayPresenter decisionOverlayPresenter;
        [SerializeField] private EndScreenPresenter endScreenPresenter;
        [SerializeField] private FlipPhonePresenter flipPhonePresenter;

        [Header("Animação do Jogador")]
        [SerializeField] private Animator playerAnimator;
        [SerializeField] private string dealAnimationName = "LevantaMao";
        [SerializeField] private string dealAnimationReverseName = "";
        [SerializeField] private string defaultAnimationName = "None";

        [Header("Objeto 3D do Celular")]
        [SerializeField] private GameObject flipPhoneObject;

        public RunPresentationCoordinator PresentationCoordinator => presentationCoordinator;
        public PaperDocumentPresenter PaperPresenter => paperPresenter;
        public RetroMonitorPresenter RetroMonitorPresenter => retroMonitorPresenter;
        public DecisionOverlayPresenter DecisionOverlayPresenter => decisionOverlayPresenter;
        public EndScreenPresenter EndScreenPresenter => endScreenPresenter;
        public FlipPhonePresenter FlipPhonePresenter => flipPhonePresenter;
        public GameObject FlipPhoneObject => flipPhoneObject;

        public void AutoDetectMissingReferences()
        {
            if (presentationCoordinator == null)
                presentationCoordinator = UnityEngine.Object.FindFirstObjectByType<RunPresentationCoordinator>(FindObjectsInactive.Include);

            if (paperPresenter == null)
                paperPresenter = UnityEngine.Object.FindFirstObjectByType<PaperDocumentPresenter>(FindObjectsInactive.Include);

            if (retroMonitorPresenter == null)
                retroMonitorPresenter = UnityEngine.Object.FindFirstObjectByType<RetroMonitorPresenter>(FindObjectsInactive.Include);

            if (decisionOverlayPresenter == null)
                decisionOverlayPresenter = UnityEngine.Object.FindFirstObjectByType<DecisionOverlayPresenter>(FindObjectsInactive.Include);

            if (endScreenPresenter == null)
                endScreenPresenter = UnityEngine.Object.FindFirstObjectByType<EndScreenPresenter>(FindObjectsInactive.Include);

            if (flipPhonePresenter == null)
                flipPhonePresenter = UnityEngine.Object.FindFirstObjectByType<FlipPhonePresenter>(FindObjectsInactive.Include);

            if (flipPhoneObject == null && flipPhonePresenter != null)
                flipPhoneObject = flipPhonePresenter.GetPhoneGameObject();

            if (flipPhonePresenter != null && flipPhoneObject != null)
                flipPhonePresenter.SetPhoneGameObject(flipPhoneObject);
        }

        public void PlayDealAnimation()
        {
            if (playerAnimator == null || string.IsNullOrEmpty(dealAnimationName)) return;
            try
            {
                playerAnimator.Play(dealAnimationName, 0, 0f);
            }
            catch { }
        }

        public void PlayDealAnimationReverse()
        {
            if (playerAnimator == null) return;
            try
            {
                if (!string.IsNullOrEmpty(dealAnimationReverseName))
                {
                    playerAnimator.Play(dealAnimationReverseName, 0, 0f);
                }
                else if (!string.IsNullOrEmpty(defaultAnimationName))
                {
                    playerAnimator.Play(defaultAnimationName, 0, 0f);
                }
            }
            catch { }
        }
    }
}
