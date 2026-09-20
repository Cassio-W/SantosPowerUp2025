using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Mandato.Presentation
{
    /// <summary>
    /// Botão físico 3D na cena do menu.
    /// Responde a cliques diretos do mouse através de raycast dedicado e colliders 3D.
    /// Possui suporte a animação procedural, efeitos sonoros e hover visual responsivo.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class MenuPhysicalButton3D : MonoBehaviour
    {
        [Header("--- Interação ---")]
        [Tooltip("Define se o botão está ativo para interação.")]
        [SerializeField] private bool interactable = true;

        [Tooltip("Se ativo, realiza raycast de mouse no Update para garantir 100% de consistência em qualquer câmera.")]
        [SerializeField] private bool useDirectRaycast = true;

        [Tooltip("LayerMask dos objetos clicáveis.")]
        [SerializeField] private LayerMask raycastLayerMask = ~0;

        [Tooltip("Se verdadeiro, ignora cliques se o cursor estiver sobre uma UI de tela UGUI/EventSystem ativa.")]
        [SerializeField] private bool checkEventSystemBlocking = false;

        [Header("--- Animação Procedural 3D (Clique) ---")]
        [Tooltip("Transform da parte móvel do botão. Se nulo, utiliza este próprio Transform.")]
        [SerializeField] private Transform movingPart;

        [Tooltip("Deslocamento local durante o clique (ex: Y = -0.015 ou Z = -0.015 conforme a orientação do botão).")]
        [SerializeField] private Vector3 pressOffset = new Vector3(0f, -0.015f, 0f);

        [Tooltip("Duração total da animação de pressionamento em segundos.")]
        [SerializeField] private float pressDuration = 0.12f;

        [Tooltip("Multiplicador de escala momentâneo durante o pressionamento.")]
        [SerializeField] private Vector3 punchScale = new Vector3(1.02f, 0.95f, 1.02f);

        [Header("--- Feedback de Hover (Ao passar o mouse) ---")]
        [Tooltip("Se ativo, destaca levemente o botão ao passar o mouse.")]
        [SerializeField] private bool enableHoverHighlight = true;
        [SerializeField] private Vector3 hoverOffset = new Vector3(0f, 0.005f, 0f);
        [SerializeField] private float hoverScaleMultiplier = 1.05f;

        [Header("--- Áudio / Sons ---")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip pressSound;
        [Range(0f, 1f)] [SerializeField] private float soundVolume = 1f;
        [SerializeField] private AudioClip hoverSound;
        [Range(0f, 1f)] [SerializeField] private float hoverSoundVolume = 0.5f;
        [SerializeField] private Vector2 pitchVariation = new Vector2(0.96f, 1.04f);

        [Header("--- Eventos Unity ---")]
        public UnityEvent onButtonPressed = new UnityEvent();
        public UnityEvent onButtonHoverEnter = new UnityEvent();
        public UnityEvent onButtonHoverExit = new UnityEvent();

        public event Action OnPressed;

        private Collider _collider;
        private Camera _mainCamera;
        private Vector3 _originalLocalPos;
        private Vector3 _originalLocalScale;
        private Coroutine _pressRoutine;
        private bool _isHovered;
        private bool _isPressed;

        public bool IsInteractable => interactable;
        public Transform TargetMovingPart => movingPart != null ? movingPart : transform;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _mainCamera = Camera.main;

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
        }

        private void Update()
        {
            if (!interactable || !useDirectRaycast) return;

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            // Raycast direto do ponteiro do mouse
            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            bool isHit = Physics.Raycast(ray, out RaycastHit hit, 100f, raycastLayerMask);
            bool isHittingThis = isHit && (hit.collider == _collider || hit.transform == transform || hit.transform.IsChildOf(transform));

            if (isHittingThis)
            {
                // Validação de bloqueio de UI (somente se configurado explicitamente)
                if (checkEventSystemBlocking && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    if (_isHovered) HandleHoverExit();
                    return;
                }

                if (!_isHovered)
                {
                    HandleHoverEnter();
                }

                if (Input.GetMouseButtonDown(0))
                {
                    Press();
                }
            }
            else
            {
                if (_isHovered)
                {
                    HandleHoverExit();
                }
            }
        }

        // Fallback legado para eventos tradicionais da Unity
        private void OnMouseDown()
        {
            if (useDirectRaycast || !interactable) return;
            if (checkEventSystemBlocking && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            Press();
        }

        private void OnMouseEnter()
        {
            if (useDirectRaycast || !interactable) return;
            HandleHoverEnter();
        }

        private void OnMouseExit()
        {
            if (useDirectRaycast || !interactable) return;
            HandleHoverExit();
        }

        private void HandleHoverEnter()
        {
            _isHovered = true;
            PlayHoverSound();

            if (enableHoverHighlight && !_isPressed && TargetMovingPart != null)
            {
                TargetMovingPart.localPosition = _originalLocalPos + hoverOffset;
                TargetMovingPart.localScale = _originalLocalScale * hoverScaleMultiplier;
            }

            onButtonHoverEnter?.Invoke();
        }

        private void HandleHoverExit()
        {
            _isHovered = false;

            if (enableHoverHighlight && !_isPressed && TargetMovingPart != null)
            {
                TargetMovingPart.localPosition = _originalLocalPos;
                TargetMovingPart.localScale = _originalLocalScale;
            }

            onButtonHoverExit?.Invoke();
        }

        /// <summary>
        /// Executa o clique do botão com animação e som.
        /// </summary>
        public void Press()
        {
            if (!interactable) return;

            PlayPressAudio();
            PlayPressAnimation();

            onButtonPressed?.Invoke();
            OnPressed?.Invoke();
        }

        /// <summary>
        /// Executa diretamente a animação e o som de clique (usado por exemplo quando acionado via atalho de teclado pelo coordenador).
        /// </summary>
        public void AnimateClick(bool playSound = true)
        {
            if (playSound)
            {
                PlayPressAudio();
            }
            PlayPressAnimation();
        }

        public void SetInteractable(bool value)
        {
            interactable = value;
            if (!value && _isHovered)
            {
                HandleHoverExit();
            }
        }

        private void PlayPressAudio()
        {
            if (pressSound == null) return;

            if (audioSource != null)
            {
                float originalPitch = audioSource.pitch;
                audioSource.pitch = UnityEngine.Random.Range(pitchVariation.x, pitchVariation.y);
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

        private void PlayPressAnimation()
        {
            if (TargetMovingPart == null || !gameObject.activeInHierarchy) return;

            if (_pressRoutine != null)
            {
                StopCoroutine(_pressRoutine);
            }

            _pressRoutine = StartCoroutine(PressAnimationRoutine());
        }

        private IEnumerator PressAnimationRoutine()
        {
            _isPressed = true;
            Transform target = TargetMovingPart;
            if (target == null) yield break;

            Vector3 downPos = _originalLocalPos + pressOffset;
            Vector3 downScale = Vector3.Scale(_originalLocalScale, punchScale);
            float halfDuration = Mathf.Max(0.01f, pressDuration * 0.5f);

            // 1. Desce
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                target.localPosition = Vector3.Lerp(_originalLocalPos, downPos, t);
                target.localScale = Vector3.Lerp(_originalLocalScale, downScale, t);
                yield return null;
            }

            // 2. Retorna
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                target.localPosition = Vector3.Lerp(downPos, _isHovered ? _originalLocalPos + hoverOffset : _originalLocalPos, smoothT);
                target.localScale = Vector3.Lerp(downScale, _isHovered ? _originalLocalScale * hoverScaleMultiplier : _originalLocalScale, smoothT);
                yield return null;
            }

            target.localPosition = _isHovered ? _originalLocalPos + hoverOffset : _originalLocalPos;
            target.localScale = _isHovered ? _originalLocalScale * hoverScaleMultiplier : _originalLocalScale;
            _isPressed = false;
            _pressRoutine = null;
        }

        private void OnDisable()
        {
            if (TargetMovingPart != null)
            {
                TargetMovingPart.localPosition = _originalLocalPos;
                TargetMovingPart.localScale = _originalLocalScale;
            }
            _isHovered = false;
            _isPressed = false;
            _pressRoutine = null;
        }

        private void OnDrawGizmosSelected()
        {
            // Desenha o colisor em amarelo para fácil ajuste visual na Scene View
            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.6f);
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
            }
        }
    }
}
