using System;
using System.Collections.Generic;
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

        [Header("Animação do Jogador (Mão)")]
        [SerializeField] private Animator playerAnimator;
        [SerializeField] private string dealAnimationName = "LevantaMao";
        [SerializeField] private string dealAnimationReverseName = "";
        [SerializeField] private string defaultAnimationName = "None";

        [Header("Efeitos de Ambiente & Câmera")]
        [SerializeField] private AttributeCameraEffects cameraEffects;
        [SerializeField] private CameraFocusManager cameraFocus;

        [Header("Objeto 3D do Celular")]
        [SerializeField] private GameObject flipPhoneObject;

        public RunPresentationCoordinator PresentationCoordinator => presentationCoordinator;
        public PaperDocumentPresenter PaperPresenter => paperPresenter;
        public RetroMonitorPresenter RetroMonitorPresenter => retroMonitorPresenter;
        public DecisionOverlayPresenter DecisionOverlayPresenter => decisionOverlayPresenter;
        public EndScreenPresenter EndScreenPresenter => endScreenPresenter;
        public FlipPhonePresenter FlipPhonePresenter => flipPhonePresenter;
        public Animator PlayerAnimator => playerAnimator;
        public AttributeCameraEffects CameraEffects => cameraEffects != null ? cameraEffects : AttributeCameraEffects.Instance;
        public CameraFocusManager CameraFocus => cameraFocus != null ? cameraFocus : CameraFocusManager.Instance;
        public GameObject FlipPhoneObject => flipPhoneObject;

        public bool Validate(out List<string> missingErrors)
        {
            missingErrors = new List<string>();

            if (decisionOverlayPresenter == null)
                missingErrors.Add("DecisionOverlayPresenter não atribuído.");

            if (endScreenPresenter == null)
                missingErrors.Add("EndScreenPresenter não atribuído.");

            if (paperPresenter == null)
                missingErrors.Add("PaperDocumentPresenter não atribuído.");

            if (retroMonitorPresenter == null)
                missingErrors.Add("RetroMonitorPresenter não atribuído.");

            if (flipPhonePresenter == null)
                missingErrors.Add("FlipPhonePresenter não atribuído.");

            if (presentationCoordinator == null)
                missingErrors.Add("RunPresentationCoordinator não atribuído.");

            return missingErrors.Count == 0;
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
