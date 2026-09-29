using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Mandato.Presentation
{
    /// <summary>
    /// Componente que representa o botão físico / campainha na mesa do gabinete presidencial.
    /// Segue o clique contínuo do jogador: mantém o botão pressionado fisicamente e reproduz o som (buzz.mp3)
    /// em loop contínuo enquanto o jogador mantiver o clique ou tecla pressionada, retornando ao soltar.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class DeskCallButton : MonoBehaviour
    {
        [Header("--- Interação ---")]
        [Tooltip("Define se o botão responde a cliques do jogador.")]
        [SerializeField] private bool interactable = true;

        [Tooltip("Se ativo, realiza raycast de mouse no Update para garantir 100% de consistência em qualquer câmera.")]
        [SerializeField] private bool useDirectRaycast = true;

        [Tooltip("LayerMask dos objetos clicáveis.")]
        [SerializeField] private LayerMask raycastLayerMask = ~0;

        [Tooltip("Se verdadeiro, ignora cliques se o cursor estiver sobre uma UI de tela UGUI/EventSystem ativa.")]
        [SerializeField] private bool checkEventSystemBlocking = false;

        [Tooltip("Se verdadeiro, busca automaticamente o componente FocusableObject no mesmo GameObject ou pais para integração de clique.")]
        [SerializeField] private bool hookFocusableObject = true;

        [Tooltip("Se verdadeiro, monitora a tecla de chamada localmente caso o coordenador de fluxo não esteja presente.")]
        [SerializeField] private bool handleSpaceKeyLocally = false;

        [Tooltip("Tecla local para acionamento do botão.")]
        [SerializeField] private KeyCode localCallKey = KeyCode.Space;

        [Header("--- Áudio / Som (Contínuo / Loop no Hold) ---")]
        [Tooltip("AudioSource para tocar os sons do botão. Se nulo, busca automaticamente ou cria em runtime.")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("Som reproduzido continuamente enquanto o botão estiver pressionado (buzz.mp3).")]
        [SerializeField] private AudioClip pressSound;

        [Range(0f, 1f)]
        [Tooltip("Volume do som de clique/buzz.")]
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

        [Tooltip("Duração da descida do botão em segundos.")]
        [SerializeField] private float pressDownDuration = 0.04f;

        [Tooltip("Duração do retorno do botão ao soltar em segundos.")]
        [SerializeField] private float releaseDuration = 0.1f;

        [Tooltip("Multiplicador de escala momentâneo para efeito de punch.")]
        [SerializeField] private Vector3 punchScale = new Vector3(1.02f, 0.95f, 1.02f);

        [Header("--- Eventos Unity ---")]
        public UnityEvent onButtonPressed = new UnityEvent();
        public UnityEvent onButtonReleased = new UnityEvent();
        public UnityEvent onButtonHoverEnter = new UnityEvent();
        public UnityEvent onButtonHoverExit = new UnityEvent();

        /// <summary>
        /// Evento C# disparado quando o botão é pressionado para solicitar a chamada do visitante.
        /// </summary>
        public event Action OnCallRequested;

        // Estados internos
        private Collider _collider;
        private Camera _mainCamera;
        private Vector3 _originalLocalPos;
        private Vector3 _originalLocalScale;
        private Coroutine _proceduralRoutine;
        private Coroutine _autoReleaseRoutine;
        private bool _isHeldDown;
        private bool _isMouseHeld;
        private bool _isKeyHeld;
        private bool _isHovered;
        private FocusableObject _cachedFocusable;

        public bool IsInteractable => interactable;
        public bool IsHeldDown => _isHeldDown;
        public AudioSource AudioSource => audioSource;
        public Animator Animator => animator;
        public Transform TargetMovingPart => movingPart != null ? movingPart : transform;
        public AudioClip PressSound { get => pressSound; set => pressSound = value; }
        public AudioClip HoverSound { get => hoverSound; set => hoverSound = value; }

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

            EnsureAudioSource();

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

#if UNITY_EDITOR
            if (pressSound == null)
            {
                pressSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audios/buzz2.mp3");
            }
#endif
        }

        public void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>() ?? GetComponentInChildren<AudioSource>();
            }

            if (audioSource == null && gameObject != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
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
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }

            // 1. Raycast de mouse direto
            if (useDirectRaycast && interactable)
            {
                if (_mainCamera != null)
                {
                    Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
                    bool isHit = Physics.Raycast(ray, out RaycastHit hit, 100f, raycastLayerMask);
                    bool isHittingThis = isHit && (_collider != null ? (hit.collider == _collider || hit.transform == transform || hit.transform.IsChildOf(transform)) : (hit.transform == transform || hit.transform.IsChildOf(transform)));

                    if (isHittingThis)
                    {
                        if (!_isHovered)
                        {
                            HandleHoverEnter();
                        }

                        if (Input.GetMouseButtonDown(0))
                        {
                            if (!(checkEventSystemBlocking && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
                            {
                                _isMouseHeld = true;
                                StartPress();
                            }
                        }
                    }
                    else
                    {
                        if (_isHovered && !_isHeldDown)
                        {
                            HandleHoverExit();
                        }
                    }
                }
            }

            // 2. Liberação de clique do mouse
            if (_isMouseHeld)
            {
                if (!Input.GetMouseButton(0))
                {
                    _isMouseHeld = false;
                    ReleasePress();
                    if (!_isHovered)
                    {
                        HandleHoverExit();
                    }
                }
            }

            // 3. Monitoramento de tecla de espaço local
            if (handleSpaceKeyLocally && interactable)
            {
                bool keyStateDown = Input.GetKeyDown(localCallKey) ||
                                    Input.GetKeyDown(KeyCode.Space) ||
                                    Input.GetKeyDown(KeyCode.Return) ||
                                    Input.GetKeyDown(KeyCode.KeypadEnter);

                bool keyStateUp = Input.GetKeyUp(localCallKey) ||
                                  Input.GetKeyUp(KeyCode.Space) ||
                                  Input.GetKeyUp(KeyCode.Return) ||
                                  Input.GetKeyUp(KeyCode.KeypadEnter);

                if (keyStateDown)
                {
                    _isKeyHeld = true;
                    StartPress();
                }
                else if (_isKeyHeld && keyStateUp)
                {
                    _isKeyHeld = false;
                    ReleasePress();
                }
            }
        }

        // Fallbacks para eventos Unity tradicionais
        private void OnMouseDown()
        {
            if (useDirectRaycast || !interactable) return;
            if (checkEventSystemBlocking && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            _isMouseHeld = true;
            StartPress();
        }

        private void OnMouseUp()
        {
            if (_isMouseHeld)
            {
                _isMouseHeld = false;
                ReleasePress();
            }
        }

        private void OnMouseEnter()
        {
            if (useDirectRaycast || !interactable) return;
            HandleHoverEnter();
        }

        private void OnMouseExit()
        {
            if (useDirectRaycast || !interactable) return;
            if (!_isHeldDown)
            {
                HandleHoverExit();
            }
        }

        private void HandleHoverEnter()
        {
            _isHovered = true;
            PlayHoverSound();
            onButtonHoverEnter?.Invoke();
        }

        private void HandleHoverExit()
        {
            _isHovered = false;
            onButtonHoverExit?.Invoke();
        }

        /// <summary>
        /// Inicia o pressionamento contínuo do botão: move a cúpula para baixo, toca o som em loop
        /// e dispara a solicitação de chamada de visitante.
        /// </summary>
        public void StartPress()
        {
            if (!interactable) return;

            if (_autoReleaseRoutine != null)
            {
                StopCoroutine(_autoReleaseRoutine);
                _autoReleaseRoutine = null;
            }

            if (_isHeldDown) return;
            _isHeldDown = true;

            StartLoopingPressAudio();
            PlayAnimatorPress();
            PlayProceduralPressDown();

            onButtonPressed?.Invoke();
            OnCallRequested?.Invoke();
        }

        /// <summary>
        /// Libera o botão: encerra o som em loop e retorna a cúpula à posição original.
        /// </summary>
        public void ReleasePress()
        {
            if (!_isHeldDown) return;
            _isHeldDown = false;

            if (_autoReleaseRoutine != null)
            {
                StopCoroutine(_autoReleaseRoutine);
                _autoReleaseRoutine = null;
            }

            StopLoopingPressAudio();
            PlayProceduralRelease();

            onButtonReleased?.Invoke();
        }

        /// <summary>
        /// Aciona um clique único com auto-release para chamadas programáticas e testes.
        /// </summary>
        public void Press()
        {
            if (!interactable) return;

            StartPress();

            if (!_isMouseHeld && !_isKeyHeld)
            {
                if (_autoReleaseRoutine != null) StopCoroutine(_autoReleaseRoutine);
                _autoReleaseRoutine = StartCoroutine(AutoReleaseRoutine(0.12f));
            }
        }

        /// <summary>
        /// Executa apenas a parte audiovisual (som e animação) do botão.
        /// Mantido para compatibilidade com o fluxo da partida.
        /// </summary>
        public void PlayPressEffects()
        {
            if (!interactable) return;

            StartPress();

            if (!_isMouseHeld && !_isKeyHeld)
            {
                if (_autoReleaseRoutine != null) StopCoroutine(_autoReleaseRoutine);
                _autoReleaseRoutine = StartCoroutine(AutoReleaseRoutine(0.12f));
            }
        }

        private IEnumerator AutoReleaseRoutine(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (!_isMouseHeld && !_isKeyHeld && _isHeldDown)
            {
                ReleasePress();
            }
            _autoReleaseRoutine = null;
        }

        public void SetInteractable(bool value)
        {
            interactable = value;
            if (!value && _isHeldDown)
            {
                _isMouseHeld = false;
                _isKeyHeld = false;
                ReleasePress();
            }
        }

        private void StartLoopingPressAudio()
        {
            if (pressSound == null) return;
            EnsureAudioSource();

            if (audioSource != null)
            {
                audioSource.clip = pressSound;
                audioSource.loop = true;
                audioSource.volume = soundVolume;
                float randomPitch = UnityEngine.Random.Range(pitchVariation.x, pitchVariation.y);
                audioSource.pitch = randomPitch;
                audioSource.Play();
            }
        }

        private void StopLoopingPressAudio()
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
                audioSource.loop = false;
            }
        }

        private void PlayHoverSound()
        {
            if (hoverSound == null) return;
            EnsureAudioSource();

            if (audioSource != null)
            {
                audioSource.PlayOneShot(hoverSound, hoverSoundVolume);
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

        private void PlayProceduralPressDown()
        {
            if (!enableProceduralPress || TargetMovingPart == null || !gameObject.activeInHierarchy) return;

            if (_proceduralRoutine != null)
            {
                StopCoroutine(_proceduralRoutine);
            }

            _proceduralRoutine = StartCoroutine(ProceduralPressDownRoutine());
        }

        private void PlayProceduralRelease()
        {
            if (!enableProceduralPress || TargetMovingPart == null || !gameObject.activeInHierarchy) return;

            if (_proceduralRoutine != null)
            {
                StopCoroutine(_proceduralRoutine);
            }

            _proceduralRoutine = StartCoroutine(ProceduralReleaseRoutine());
        }

        private IEnumerator ProceduralPressDownRoutine()
        {
            Transform target = TargetMovingPart;
            if (target == null) yield break;

            Vector3 startPos = target.localPosition;
            Vector3 startScale = target.localScale;
            Vector3 downPos = _originalLocalPos + pressOffset;
            Vector3 downScale = Vector3.Scale(_originalLocalScale, punchScale);

            float duration = Mathf.Max(0.01f, pressDownDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                target.localPosition = Vector3.Lerp(startPos, downPos, t);
                target.localScale = Vector3.Lerp(startScale, downScale, t);
                yield return null;
            }

            target.localPosition = downPos;
            target.localScale = downScale;
            _proceduralRoutine = null;
        }

        private IEnumerator ProceduralReleaseRoutine()
        {
            Transform target = TargetMovingPart;
            if (target == null) yield break;

            Vector3 currentPos = target.localPosition;
            Vector3 currentScale = target.localScale;

            float duration = Mathf.Max(0.01f, releaseDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                target.localPosition = Vector3.Lerp(currentPos, _originalLocalPos, smoothT);
                target.localScale = Vector3.Lerp(currentScale, _originalLocalScale, smoothT);
                yield return null;
            }

            target.localPosition = _originalLocalPos;
            target.localScale = _originalLocalScale;
            _proceduralRoutine = null;
        }

        private void OnDisable()
        {
            if (TargetMovingPart != null && enableProceduralPress)
            {
                TargetMovingPart.localPosition = _originalLocalPos;
                TargetMovingPart.localScale = _originalLocalScale;
            }

            _isHeldDown = false;
            _isMouseHeld = false;
            _isKeyHeld = false;
            _isHovered = false;

            StopLoopingPressAudio();

            _proceduralRoutine = null;
            _autoReleaseRoutine = null;
        }
    }
}
