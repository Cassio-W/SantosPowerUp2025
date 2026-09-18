using System.Collections;
using UnityEngine;

namespace Mandato.Presentation
{
    public enum PaperPositionMode
    {
        [Tooltip("Utiliza o Transform âncora atribuído no campo 'Focused Paper Point' (Recomendado).")]
        UseTransformAnchor,

        [Tooltip("Utiliza as coordenadas mundiais absolutas configuradas em 'Focused Paper Position' e 'Focused Paper Rotation'.")]
        AbsoluteWorldPosition,

        [Tooltip("Aplica 'Focused Paper Position' como um deslocamento (offset) relativo à posição de repouso do papel.")]
        OffsetFromRestPosition
    }

    /// <summary>
    /// Componente especializado para o documento físico de papel da proposta.
    /// Além dos efeitos de foco da câmera herdados de FocusableObject, interpola suavemente
    /// a posição e a rotação do próprio GameObject do papel no espaço mundial (World Space).
    /// Desacopla temporariamente do osso do jogador ao focar para que o Animator do Mecanim
    /// não sobrescreva as coordenadas do papel, bloqueia cliques fora e reseta o Animator da mão.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class PaperFocusableObject : FocusableObject
    {
        [Header("--- Interpolação do Papel no Foco ---")]
        [Tooltip("Transform de referência que define a posição e rotação mundial para onde o papel deve interpolar ao entrar em foco (Recomendado).")]
        [SerializeField] private Transform focusedPaperPoint;

        [Tooltip("Modo de cálculo da posição de foco caso 'focusedPaperPoint' não seja utilizado.")]
        [SerializeField] private PaperPositionMode positionMode = PaperPositionMode.UseTransformAnchor;

        [Tooltip("Posição mundial ou offset para onde o papel interpola ao focar (usado caso 'focusedPaperPoint' seja nulo).")]
        [SerializeField] private Vector3 focusedPaperPosition = new Vector3(0.167f, 1.111f, -3.86f);

        [Tooltip("Rotação Euler mundial para onde o papel interpola ao focar (usado caso 'focusedPaperPoint' seja nulo).")]
        [SerializeField] private Vector3 focusedPaperRotation = new Vector3(0f, 180f, 0f);

        [Tooltip("Duração da animação de movimento/interpolação do papel em segundos.")]
        [SerializeField] private float paperMoveDuration = 0.5f;

        [Tooltip("Curva de interpolação do movimento do papel.")]
        [SerializeField] private AnimationCurve paperMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Se ativo, as animações do papel funcionam mesmo se Time.timeScale for 0.")]
        [SerializeField] private bool useUnscaledTime = true;

        [Header("--- Desacoplamento do Rig / Animator ---")]
        [Tooltip("Se ativo, desacopla temporariamente o papel do transform pai (osso da mão do jogador) ao entrar em foco para libertá-lo da avaliação do Animator.")]
        [SerializeField] private bool detachFromParentOnFocus = true;

        [Tooltip("Se ativo, restaura o transform pai original quando o papel sair do foco e retornar à posição de repouso.")]
        [SerializeField] private bool reparentOnUnfocus = true;

        [Header("--- Animação do Braço / Jogador ---")]
        [Tooltip("Animator do braço do jogador para desativar instantaneamente ao entrar em foco. Se nulo, busca automaticamente na cena.")]
        [SerializeField] private Animator playerHandAnimator;

        [Tooltip("Nome do estado padrão de repouso no Animator (padrão: 'None').")]
        [SerializeField] private string defaultHandStateName = "None";

        [Tooltip("Nome do estado de mão levantada no Animator (padrão: 'LevantaMao').")]
        [SerializeField] private string raisedHandStateName = "LevantaMao";

        [Tooltip("Se ativo, desativa a animação do braço instantaneamente ao entrar em foco.")]
        [SerializeField] private bool disableHandAnimationOnFocus = true;

        // Posições e hierarquia originais de repouso
        private Transform _originalParent;
        private Vector3 _restWorldPosition;
        private Quaternion _restWorldRotation;
        private Vector3 _restLocalPosition;
        private Quaternion _restLocalRotation;
        private bool _isDetached;

        private Coroutine _paperAnimationCoroutine;

        public Transform FocusedPaperPoint
        {
            get => focusedPaperPoint;
            set => focusedPaperPoint = value;
        }

        public PaperPositionMode PositionMode
        {
            get => positionMode;
            set => positionMode = value;
        }

        public Vector3 FocusedPaperPosition
        {
            get => focusedPaperPosition;
            set => focusedPaperPosition = value;
        }

        public Vector3 FocusedPaperRotation
        {
            get => focusedPaperRotation;
            set => focusedPaperRotation = value;
        }

        public float PaperMoveDuration
        {
            get => paperMoveDuration;
            set => paperMoveDuration = Mathf.Max(0.01f, value);
        }

        public bool DetachFromParentOnFocus
        {
            get => detachFromParentOnFocus;
            set => detachFromParentOnFocus = value;
        }

        public bool ReparentOnUnfocus
        {
            get => reparentOnUnfocus;
            set => reparentOnUnfocus = value;
        }

        public Animator PlayerHandAnimator
        {
            get => playerHandAnimator;
            set => playerHandAnimator = value;
        }

        public string DefaultHandStateName
        {
            get => defaultHandStateName;
            set => defaultHandStateName = value;
        }

        public string RaisedHandStateName
        {
            get => raisedHandStateName;
            set => raisedHandStateName = value;
        }

        public bool DisableHandAnimationOnFocus
        {
            get => disableHandAnimationOnFocus;
            set => disableHandAnimationOnFocus = value;
        }

        public override bool UnfocusOnSecondClick { get => false; set { } }
        public override bool AllowUnfocusOnClickOutside { get => false; set { } }

        public Vector3 RestWorldPosition => _restWorldPosition;
        public Quaternion RestWorldRotation => _restWorldRotation;
        public bool IsDetached => _isDetached;

        protected override void Awake()
        {
            base.Awake();

            // Por padrão, o papel não deve desfocar ao clicar fora nem ao segundo clique
            AllowUnfocusOnClickOutside = false;
            UnfocusOnSecondClick = false;

            CaptureRestTransform();
        }

        /// <summary>
        /// Captura a posição, rotação e pai inicial de repouso do papel na cena.
        /// </summary>
        public void CaptureRestTransform()
        {
            if (!_isDetached)
            {
                _originalParent = transform.parent;
                _restLocalPosition = transform.localPosition;
                _restLocalRotation = transform.localRotation;
            }
            _restWorldPosition = transform.position;
            _restWorldRotation = transform.rotation;
        }

        /// <summary>
        /// Atualiza o estado de foco, desacopla do osso pai, desativa a mão e interpola o papel no espaço mundial.
        /// </summary>
        public override void SetFocused(bool focused)
        {
            base.SetFocused(focused);

            if (_paperAnimationCoroutine != null)
            {
                StopCoroutine(_paperAnimationCoroutine);
                _paperAnimationCoroutine = null;
            }

            if (focused)
            {
                // Captura o estado de repouso no instante do foco antes de desacoplar
                if (!_isDetached && transform.parent != null)
                {
                    _originalParent = transform.parent;
                    _restWorldPosition = transform.position;
                    _restWorldRotation = transform.rotation;
                    _restLocalPosition = transform.localPosition;
                    _restLocalRotation = transform.localRotation;
                }

                // 1. Desacopla do osso animado para evitar que o Animator force a posição do papel
                if (detachFromParentOnFocus && transform.parent != null)
                {
                    transform.SetParent(null, true);
                    _isDetached = true;
                }

                // 2. Desativa instantaneamente a animação do braço do jogador
                if (disableHandAnimationOnFocus)
                {
                    DeactivatePlayerHandAnimation();
                }

                // 3. Inicia interpolação para a posição de foco
                if (gameObject.activeInHierarchy)
                {
                    _paperAnimationCoroutine = StartCoroutine(AnimatePaperWorldRoutine(true));
                }
                else
                {
                    ApplyImmediateWorldState(true);
                }
            }
            else
            {
                // Inicia retorno para a posição de repouso
                if (gameObject.activeInHierarchy)
                {
                    _paperAnimationCoroutine = StartCoroutine(AnimatePaperWorldRoutine(false));
                }
                else
                {
                    ApplyImmediateWorldState(false);
                    RestoreParentIfNeeded();
                }
            }
        }

        protected override void Update()
        {
            // Se estiver focado, desacoplado ou animando, não executa o hover de localPosition da classe base
            if (_isFocused || _isDetached || _paperAnimationCoroutine != null)
            {
                return;
            }

            base.Update();
        }

        protected override void UpdateHoverTransform(float dt)
        {
            // Bloqueia qualquer alteração de localPosition se estiver focado ou desacoplado
            if (_isFocused || _isDetached || _paperAnimationCoroutine != null)
            {
                return;
            }

            base.UpdateHoverTransform(dt);
        }

        protected virtual void LateUpdate()
        {
            // Garante que o papel permaneça cravado na posição e rotação mundiais de foco após a transição terminar
            if (_isFocused && _paperAnimationCoroutine == null)
            {
                transform.position = GetTargetFocusedPosition();
                transform.rotation = GetTargetFocusedRotation();
            }
        }

        /// <summary>
        /// Desativa instantaneamente a animação do braço levantado, retornando o Animator para o estado de repouso ('None').
        /// </summary>
        public void DeactivatePlayerHandAnimation()
        {
            if (playerHandAnimator == null)
            {
                var animators = FindObjectsByType<Animator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (var a in animators)
                {
                    if (a != null && (a.gameObject.name.IndexOf("player", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      a.gameObject.name.IndexOf("mao", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      a.gameObject.name.IndexOf("hand", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      a.gameObject.name.IndexOf("braco", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      a.HasState(0, Animator.StringToHash("LevantaMao"))))
                    {
                        playerHandAnimator = a;
                        break;
                    }
                }
            }

            if (playerHandAnimator != null)
            {
                try
                {
                    string state = !string.IsNullOrEmpty(defaultHandStateName) ? defaultHandStateName : "None";
                    playerHandAnimator.Play(state, 0, 0f);
                    playerHandAnimator.Update(0f);
                }
                catch { }
            }
        }

        /// <summary>
        /// Reativa a animação do braço levantado do jogador ('LevantaMao').
        /// </summary>
        public void ReactivatePlayerHandAnimation()
        {
            if (playerHandAnimator != null)
            {
                try
                {
                    string state = !string.IsNullOrEmpty(raisedHandStateName) ? raisedHandStateName : "LevantaMao";
                    playerHandAnimator.Play(state, 0, 0f);
                }
                catch { }
            }
        }

        /// <summary>
        /// Retorna a posição mundial alvo para quando o papel está focado.
        /// </summary>
        public Vector3 GetTargetFocusedPosition()
        {
            if (focusedPaperPoint != null)
            {
                return focusedPaperPoint.position;
            }

            switch (positionMode)
            {
                case PaperPositionMode.AbsoluteWorldPosition:
                    return focusedPaperPosition;

                case PaperPositionMode.OffsetFromRestPosition:
                    return _restWorldPosition + focusedPaperPosition;

                case PaperPositionMode.UseTransformAnchor:
                default:
                    // Se não há âncora, assume focusedPaperPosition como coordenada mundial absoluta
                    return focusedPaperPosition;
            }
        }

        /// <summary>
        /// Retorna a rotação mundial alvo para quando o papel está focado.
        /// </summary>
        public Quaternion GetTargetFocusedRotation()
        {
            if (focusedPaperPoint != null)
            {
                return focusedPaperPoint.rotation;
            }

            return Quaternion.Euler(focusedPaperRotation);
        }

        private void ApplyImmediateWorldState(bool focused)
        {
            transform.position = focused ? GetTargetFocusedPosition() : _restWorldPosition;
            transform.rotation = focused ? GetTargetFocusedRotation() : _restWorldRotation;
        }

        private void RestoreParentIfNeeded()
        {
            if (_isDetached && reparentOnUnfocus && _originalParent != null)
            {
                transform.SetParent(_originalParent, true);
                transform.localPosition = _restLocalPosition;
                transform.localRotation = _restLocalRotation;
                _isDetached = false;
            }
        }

        private IEnumerator AnimatePaperWorldRoutine(bool focused)
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;

            Vector3 endPos = focused ? GetTargetFocusedPosition() : _restWorldPosition;
            Quaternion endRot = focused ? GetTargetFocusedRotation() : _restWorldRotation;

            float duration = paperMoveDuration > 0.01f ? paperMoveDuration : 0.4f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                elapsed += dt;

                float t = Mathf.Clamp01(elapsed / duration);
                float curveT = paperMoveCurve != null ? paperMoveCurve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);

                transform.position = Vector3.Lerp(startPos, endPos, curveT);
                transform.rotation = Quaternion.Slerp(startRot, endRot, curveT);

                yield return null;
            }

            transform.position = endPos;
            transform.rotation = endRot;

            if (!focused)
            {
                RestoreParentIfNeeded();
            }

            _paperAnimationCoroutine = null;
        }

        protected override void OnDisable()
        {
            if (_paperAnimationCoroutine != null)
            {
                StopCoroutine(_paperAnimationCoroutine);
                _paperAnimationCoroutine = null;
            }

            RestoreParentIfNeeded();

            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            RestoreParentIfNeeded();
            base.OnDestroy();
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // Desenha gizmo da posição alvo do papel no espaço mundial
            Vector3 targetPos = GetTargetFocusedPosition();
            Quaternion targetRot = GetTargetFocusedRotation();

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            Gizmos.DrawLine(transform.position, targetPos);

            Gizmos.matrix = Matrix4x4.TRS(targetPos, targetRot, transform.lossyScale);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.3f, 0.01f, 0.4f));
        }
    }
}
