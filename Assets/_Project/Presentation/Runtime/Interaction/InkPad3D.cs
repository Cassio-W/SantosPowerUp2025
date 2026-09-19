using System;
using UnityEngine;

namespace Mandato.Presentation
{
    public enum InkType
    {
        Approve = 0,
        Reject = 1
    }

    /// <summary>
    /// Componente 3D para as almofadas de tinta presentes na mesa presidencial.
    /// Herda de FocusableObject para suporte nativo a efeitos de hover (escala, elevação, áudio e outline no ToonOutlineFeature).
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class InkPad3D : FocusableObject
    {
        [Header("Configuração de Tinta")]
        [Tooltip("Tipo de decisão conferida por esta almofada (Approve = Aceitar, Reject = Recusar).")]
        [SerializeField] private InkType inkType = InkType.Approve;

        [Header("Efeitos e Áudio da Tinta")]
        [SerializeField] private AudioClip dipSound;
        [Tooltip("Transform opcional da esponja interna para micro-animação de pressão ao molhar o carimbo.")]
        [SerializeField] private Transform spongeTransform;
        [SerializeField] private float spongePressOffset = -0.005f;

        private Vector3 originalSpongePos;
        private Coroutine spongeAnimationCoroutine;

        public InkType Type
        {
            get => inkType;
            set
            {
                inkType = value;
                UpdateOutlineColor();
            }
        }

        public int ChoiceIndex => (int)inkType;
        public bool IsApprove => inkType == InkType.Approve;

        public event Action<InkPad3D> OnInkPadDipped;

        protected override void Awake()
        {
            base.Awake();

            AllowClickToFocus = false;
            UnfocusOnSecondClick = false;
            AllowUnfocusOnClickOutside = false;
            EnableHoverScale = false;
            HoverScaleMultiplier = 1.0f;
            HoverLiftOffset = Vector3.zero;
            EnableOutlineHighlight = true;

            UpdateOutlineColor();

            if (spongeTransform != null)
            {
                originalSpongePos = spongeTransform.localPosition;
            }
        }

        private void UpdateOutlineColor()
        {
            HighlightOutlineColor = (inkType == InkType.Approve)
                ? new Color(0.2f, 1.0f, 0.35f, 1f)
                : new Color(1.0f, 0.25f, 0.25f, 1f);
        }

        /// <summary>
        /// Molha o carimbo com a tinta correspondente e dispara o feedback audiovisual.
        /// </summary>
        public void ApplyInkToStamp(StampTool3D stamp)
        {
            if (stamp == null) return;

            PlayDipFeedback();
            stamp.SetInk(inkType);
            OnInkPadDipped?.Invoke(this);
        }

        public void PlayDipFeedback()
        {
            var audio = GetComponent<AudioSource>();
            if (audio != null && dipSound != null)
            {
                audio.PlayOneShot(dipSound);
            }

            if (spongeTransform != null && gameObject.activeInHierarchy)
            {
                if (spongeAnimationCoroutine != null)
                {
                    StopCoroutine(spongeAnimationCoroutine);
                }
                spongeAnimationCoroutine = StartCoroutine(AnimateSpongeRoutine());
            }
        }

        private System.Collections.IEnumerator AnimateSpongeRoutine()
        {
            float duration = 0.15f;
            float elapsed = 0f;
            Vector3 targetPos = originalSpongePos + new Vector3(0f, spongePressOffset, 0f);

            while (elapsed < duration * 0.5f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / (duration * 0.5f);
                spongeTransform.localPosition = Vector3.Lerp(originalSpongePos, targetPos, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < duration * 0.5f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / (duration * 0.5f);
                spongeTransform.localPosition = Vector3.Lerp(targetPos, originalSpongePos, t);
                yield return null;
            }

            spongeTransform.localPosition = originalSpongePos;
            spongeAnimationCoroutine = null;
        }
    }
}
