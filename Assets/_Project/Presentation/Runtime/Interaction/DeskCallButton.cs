using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Mandato.Presentation
{
    /// <summary>
    /// Componente que representa o botão físico / campainha na mesa do gabinete.
    /// Ao ser clicado ou ao acionar a tecla de chamada (Espaço), reproduz som e animações
    /// e solicita a chamada do próximo visitante ao fluxo da partida.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class DeskCallButton : MonoBehaviour
    {
        [Header("--- Interação ---")]
        [Tooltip("Define se o botão responde a cliques do jogador.")]
        [SerializeField] private bool interactable = true;

        [Tooltip("Se verdadeiro, busca automaticamente o componente FocusableObject no mesmo GameObject ou pais para integração de clique.")]
        [SerializeField] private bool hookFocusableObject = true;

        [Tooltip("Se verdadeiro, monitora a tecla de chamada localmente caso o coordenador de fluxo não esteja presente.")]
        [SerializeField] private bool handleSpaceKeyLocally = false;

        [Tooltip("Tecla local para acionamento do botão.")]
        [SerializeField] private KeyCode localCallKey = KeyCode.Space;

        [Header("--- Áudio / Som ---")]
        [Tooltip("AudioSource para tocar os sons do botão. Se nulo, busca automaticamente ou cria em runtime.")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("Som reproduzido ao pressionar o botão.")]
        [SerializeField] private AudioClip pressSound;

        [Range(0f, 1f)]
        [Tooltip("Volume do som de clique.")]
        [SerializeField] private float soundVolume = 1f;

        [Tooltip("Som opcional ao passar o mouse por cima do botão.")]
        [SerializeField] private AudioClip hoverSound;

        [Range(0f, 1f)]
        [Tooltip("Volume do som de hover.")]
        [SerializeField] private float hoverSoundVolume = 0.5f;

        [Tooltip("Intervalo de variação aleatória de pitch para dar sensação tátil natural.")]
        [SerializeField] private Vector2 pitchVariation = new Vector2(0.96f, 1.04f);

        [Header("--- Animação (Animator) ---")]
        [Tooltip("Animator do botão (opcional).")]
        [SerializeField] private Animator animator;

        [Tooltip("Nome do Trigger do Animator acionado no clique.")]
        [SerializeField] private string pressTrigger = "Press";

        [Tooltip("Nome do estado de animação acionado no clique (se não usar trigger).")]
        [SerializeField] private string pressAnimationState = "";

        [Header("--- Animação Procedural 3D ---")]
        [Tooltip("Se verdadeiro, anima fisicamente a descida e retorno da malha do botão mesmo sem Animator.")]
        [SerializeField] private bool enableProceduralPress = true;

        [Tooltip("Transform da parte móvel / cúpula do botão. Se nulo, utiliza este próprio Transform.")]
        [SerializeField] private Transform movingPart;

        [Tooltip("Deslocamento local para baixo durante o pressionamento (ex: Y = -0.015).")]
        [SerializeField] private Vector3 pressOffset = new Vector3(0f, -0.015f, 0f);

        [Tooltip("Duração total da animação de descida e subida em segundos.")]
        [SerializeField] private float pressDuration = 0.12f;

        [Tooltip("Multiplicador de escala momentâneo para efeito de punch.")]
        [SerializeField] private Vector3 punchScale = new Vector3(1.02f, 0.95f, 1.02f);

        [Header("--- Eventos Unity ---")]
        public UnityEvent onButtonPressed = new UnityEvent();
        public UnityEvent onButtonHoverEnter = new UnityEvent();
        public UnityEvent onButtonHoverExit = new UnityEvent();

        /// <summary>
        /// Evento C# disparado quando o botão é pressionado para solicitar a chamada do visitante.
        /// </summary>
        public event Action OnCallRequested;

        // Estados internos
        private Vector3 _originalLocalPos;
        private Vector3 _originalLocalScale;
        private Coroutine _proceduralPressRoutine;
        private bool _isHovered;
        private FocusableObject _cachedFocusable;

        public bool IsInteractable => interactable;
        public AudioSource AudioSource => audioSource;
        public Animator Animator => animator;
        public Transform TargetMovingPart => movingPart != null ? movingPart : transform;

        private void Awake()
        {
            if (movingPart == null)
            {
                movingPart = transform;
            }

            _originalLocalPos = movingPart.localPosition;
            _originalLocalScale = movingPart.localScale;

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
            }

            if (hookFocusableObject)
            {
                _cachedFocusable = GetComponent<FocusableObject>() ?? GetComponentInParent<FocusableObject>();
                if (_cachedFocusable != null)
                {
                    _cachedFocusable.onClicked.AddListener(Press);
                }
            }
        }

        private void OnDestroy()
        {
            if (_cachedFocusable != null)
            {
                _cachedFocusable.onClicked.RemoveListener(Press);
            }
        }

        private void Update()
        {
            if (handleSpaceKeyLocally && interactable)
            {
                bool keyPressed = Input.GetKeyDown(localCallKey) ||
                                  Input.GetKeyDown(KeyCode.Space) ||
                                  Input.GetKeyDown(KeyCode.Return) ||
                                  Input.GetKeyDown(KeyCode.KeypadEnter);

                if (keyPressed)
                {
                    Press();
                }
            }
        }

        /// <summary>
        /// Detecta clique direto do mouse via Collider na cena 3D.
        /// </summary>
        private void OnMouseDown()
        {
            if (!interactable) return;

            // Se o ponteiro estiver sobre UI tradicional, ignora o clique 3D
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Press();
        }

        private void OnMouseEnter()
        {
            if (!interactable) return;

            _isHovered = true;
            PlayHoverSound();
            onButtonHoverEnter?.Invoke();
        }

        private void OnMouseExit()
        {
            _isHovered = false;
            onButtonHoverExit?.Invoke();
        }

        /// <summary>
        /// Aciona o botão: dispara efeitos audiovisuais e emite a solicitação de chamada de visitante.
        /// </summary>
        public void Press()
        {
            if (!interactable) return;

            PlayPressEffects();
            onButtonPressed?.Invoke();
            OnCallRequested?.Invoke();
        }

        /// <summary>
        /// Executa apenas a parte audiovisual (som e animação) do botão.
        /// Chamado automaticamente quando a tecla Espaço é pressionada no coordenador de fluxo.
        /// </summary>
        public void PlayPressEffects()
        {
            PlayPressSound();
            PlayAnimatorPress();
            PlayProceduralPress();
        }

        public void SetInteractable(bool value)
        {
            interactable = value;
        }

        private void PlayPressSound()
        {
            if (pressSound == null) return;

            float originalPitch = 1f;
            if (audioSource != null)
            {
                originalPitch = audioSource.pitch;
                float randomPitch = UnityEngine.Random.Range(pitchVariation.x, pitchVariation.y);
                audioSource.pitch = randomPitch;
                audioSource.PlayOneShot(pressSound, soundVolume);
                audioSource.pitch = originalPitch;
            }
            else
            {
                AudioSource.PlayClipAtPoint(pressSound, transform.position, soundVolume);
            }
        }

        private void PlayHoverSound()
        {
            if (hoverSound == null) return;

            if (audioSource != null)
            {
                audioSource.PlayOneShot(hoverSound, hoverSoundVolume);
            }
            else
            {
                AudioSource.PlayClipAtPoint(hoverSound, transform.position, hoverSoundVolume);
            }
        }

        private void PlayAnimatorPress()
        {
            if (animator == null) return;

            try
            {
                if (!string.IsNullOrEmpty(pressTrigger))
                {
                    animator.SetTrigger(pressTrigger);
                }
                else if (!string.IsNullOrEmpty(pressAnimationState))
                {
                    animator.Play(pressAnimationState, 0, 0f);
                }
            }
            catch
            {
                // Ignora erros de parâmetros ausentes no controller
            }
        }

        private void PlayProceduralPress()
        {
            if (!enableProceduralPress || TargetMovingPart == null) return;

            if (!gameObject.activeInHierarchy) return;

            if (_proceduralPressRoutine != null)
            {
                StopCoroutine(_proceduralPressRoutine);
            }

            _proceduralPressRoutine = StartCoroutine(ProceduralPressRoutine());
        }

        private IEnumerator ProceduralPressRoutine()
        {
            Transform target = TargetMovingPart;
            if (target == null) yield break;

            Vector3 downPos = _originalLocalPos + pressOffset;
            Vector3 downScale = Vector3.Scale(_originalLocalScale, punchScale);

            float halfDuration = Mathf.Max(0.01f, pressDuration * 0.5f);
            float elapsed = 0f;

            // 1. Desce
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                target.localPosition = Vector3.Lerp(_originalLocalPos, downPos, t);
                target.localScale = Vector3.Lerp(_originalLocalScale, downScale, t);
                yield return null;
            }

            // 2. Retorna com suavidade
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                // Curva de mola suave
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                target.localPosition = Vector3.Lerp(downPos, _originalLocalPos, smoothT);
                target.localScale = Vector3.Lerp(downScale, _originalLocalScale, smoothT);
                yield return null;
            }

            target.localPosition = _originalLocalPos;
            target.localScale = _originalLocalScale;
            _proceduralPressRoutine = null;
        }

        private void OnDisable()
        {
            if (TargetMovingPart != null && enableProceduralPress)
            {
                TargetMovingPart.localPosition = _originalLocalPos;
                TargetMovingPart.localScale = _originalLocalScale;
            }
            _proceduralPressRoutine = null;
        }
    }
}
