using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Mandato.Presentation
{
    /// <summary>
    /// Componente modular que permite a qualquer objeto 3D receber efeitos de Hover (escala, elevacao, cor do outline no ToonOutlineFeature, som)
    /// e, ao ser clicado, comanda a Camera a se mover suavemente para uma posicao de foco pre-determinada.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    [ExecuteAlways]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp", null)]
    public class FocusableObject : MonoBehaviour
{
    public static FocusableObject ActiveHighlightedObject { get; private set; }
    public static readonly List<FocusableObject> ActiveHighlightedObjects = new List<FocusableObject>();

    [Header("--- Ponto de Foco da Camera ---")]
    [Tooltip("Transform que define a posicao e rotacao exatas para onde a camera deve ir ao focar este objeto. Se deixado vazio, calcula um offset automatico.")]
    [SerializeField] private Transform cameraFocusPoint;

    [Tooltip("Se nao houver 'cameraFocusPoint', este offset local sera usado para posicionar a camera em relacao ao objeto.")]
    [SerializeField] private Vector3 fallbackFocusOffset = new Vector3(0f, 0.5f, -2.5f);

    [Tooltip("Duracao da transicao da camera para este objeto em segundos (-1 para usar a duracao padrao do CameraFocusManager).")]
    [SerializeField] private float customTransitionDuration = -1f;

    [Tooltip("Valor do Campo de Visao (FOV) da camera durante o foco neste objeto (-1 para usar o FOV padrao da camera).")]
    [SerializeField] [Range(-1f, 120f)] private float targetCameraFov = 45f;

    [Tooltip("Se ativo, habilita o efeito de camera / pos-processamento (desfoco periferico e Volume de foco) quando este objeto entrar em foco.")]
    [SerializeField] private bool enableCameraEffectOnFocus = true;

    [Tooltip("Intensidade / peso do efeito de camera (Volume e Edge Blur) quando este objeto for focado (0 = desligado, 1 = intensidade maxima).")]
    [Range(0f, 1f)]
    [SerializeField] private float cameraEffectWeight = 1f;

    [Header("--- Configuracoes de Hover ---")]
    [Tooltip("Habilita ou desabilita animacoes de escala no hover.")]
    [SerializeField] private bool enableHoverScale = true;

    [Tooltip("Multiplicador de escala ao passar o mouse por cima (ex: 1.08 = 8% maior).")]
    [SerializeField] private float hoverScaleMultiplier = 1.08f;

    [Tooltip("Deslocamento de elevacao (lift) ao passar o mouse.")]
    [SerializeField] private Vector3 hoverLiftOffset = new Vector3(0f, 0.08f, 0f);

    [Tooltip("Velocidade de suavizacao do hover.")]
    [SerializeField] private float hoverTransitionSpeed = 12f;

    [Header("--- Efeito Pulinho no Hover (Hop) ---")]
    [Tooltip("Habilita ou desabilita a animação procedural de pulinho (hop) ao passar o mouse por cima do objeto.")]
    [SerializeField] private bool enableHoverHop = true;

    [Range(0f, 2f)]
    [Tooltip("Intensidade / multiplicador do pulinho no hover (slider de intensidade: 0 = desligado, 1 = normal, 2 = intenso).")]
    [SerializeField] private float hoverHopIntensity = 1f;

    [Tooltip("Deslocamento local do pulinho (ex: Y = 0.035). Multiplicado pela intensidade.")]
    [SerializeField] private Vector3 hoverHopOffset = new Vector3(0f, 0.035f, 0f);

    [Tooltip("Duração total da animação do pulinho em segundos.")]
    [SerializeField] private float hoverHopDuration = 0.15f;

    [Tooltip("Multiplicador de escala momentâneo durante o ápice do pulinho (squash & stretch).")]
    [SerializeField] private Vector3 hoverHopPunchScale = new Vector3(1.02f, 1.06f, 1.02f);

    [Header("--- Highlight via ToonOutline ---")]
    [Tooltip("Habilita a troca da cor do outline do ToonOutlineFeature para este objeto ao passar o mouse por cima.")]
    [SerializeField] private bool enableOutlineHighlight = true;

    [Tooltip("Cor do outline ao passar o mouse por cima (Highlight).")]
    [ColorUsage(true, true)]
    [SerializeField] private Color highlightOutlineColor = new Color(1f, 0.85f, 0.15f, 1f);

    [Tooltip("Renderers deste objeto que serao destacados pelo ToonOutlineFeature. Se vazio, busca automaticamente nos filhos.")]
    [SerializeField] private List<Renderer> targetRenderers = new List<Renderer>();

    [Header("--- Efeitos Sonoros ---")]
    [Tooltip("AudioSource para tocar os sons. Se vazio, cria/usa um AudioSource local automaticamente.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Som ao passar o mouse por cima do objeto.")]
    [SerializeField] private AudioClip hoverSound;
    [Range(0f, 1f)] [SerializeField] private float hoverSoundVolume = 0.8f;

    [Tooltip("Som ao clicar e focar o objeto.")]
    [SerializeField] private AudioClip focusSound;
    [Range(0f, 1f)] [SerializeField] private float focusSoundVolume = 1f;

    [Tooltip("Som ao desfocar o objeto.")]
    [SerializeField] private AudioClip unfocusSound;
    [Range(0f, 1f)] [SerializeField] private float unfocusSoundVolume = 0.8f;

    [Header("--- Comportamento de Clique ---")]
    [Tooltip("Permite clicar neste objeto para focar.")]
    [SerializeField] private bool allowClickToFocus = true;

    [Tooltip("Clicar novamente no objeto quando ele ja esta focado faz a camera desfocar (retornar).")]
    [SerializeField] private bool unfocusOnSecondClick = true;

    [Tooltip("Permite sair do foco ao clicar em áreas vazias ou em outros objetos do cenário.")]
    [SerializeField] private bool allowUnfocusOnClickOutside = true;

    [Header("--- Eventos Unity ---")]
    public UnityEvent onHoverEnter = new UnityEvent();
    public UnityEvent onHoverExit = new UnityEvent();
    public UnityEvent onFocused = new UnityEvent();
    public UnityEvent onUnfocused = new UnityEvent();
    public UnityEvent onClicked = new UnityEvent();

    // Estados internos
    protected bool _isHovered;
    protected bool _isFocused;
    protected Vector3 _originalLocalPos;
    protected Vector3 _originalLocalScale;
    protected Vector3 _currentHoverPosOffset;
    protected Vector3 _currentHoverScaleMultiplier = Vector3.one;
    protected float _currentHighlightWeight = 0f;
    protected bool _isHopping = false;
    protected float _hopStartTime;
    protected float _hopDuration;
    protected Vector3 _currentHopPosOffset = Vector3.zero;
    protected Vector3 _currentHopScaleMultiplier = Vector3.one;

    public bool IsHovered => _isHovered;
    public bool IsFocused => _isFocused;
    public float CurrentHighlightWeight => _currentHighlightWeight;
    public Vector3 FallbackFocusOffset => fallbackFocusOffset;
    public float CustomTransitionDuration { get => customTransitionDuration; set => customTransitionDuration = value; }
    public bool OverrideCameraFov => targetCameraFov > 0f;
    public float TargetCameraFov { get => targetCameraFov; set => targetCameraFov = value; }
    public bool EnableCameraEffectOnFocus { get => enableCameraEffectOnFocus; set => enableCameraEffectOnFocus = value; }
    public float CameraEffectWeight { get => cameraEffectWeight; set => cameraEffectWeight = Mathf.Clamp01(value); }
    public bool AllowClickToFocus { get => allowClickToFocus; set => allowClickToFocus = value; }
    public virtual bool UnfocusOnSecondClick { get => unfocusOnSecondClick; set => unfocusOnSecondClick = value; }
    public virtual bool AllowUnfocusOnClickOutside { get => allowUnfocusOnClickOutside; set => allowUnfocusOnClickOutside = value; }
    public bool EnableOutlineHighlight { get => enableOutlineHighlight; set => enableOutlineHighlight = value; }
    public Color HighlightOutlineColor { get => highlightOutlineColor; set => highlightOutlineColor = value; }
    public bool EnableHoverScale { get => enableHoverScale; set => enableHoverScale = value; }
    public float HoverScaleMultiplier { get => hoverScaleMultiplier; set => hoverScaleMultiplier = value; }
    public Vector3 HoverLiftOffset { get => hoverLiftOffset; set => hoverLiftOffset = value; }
    public bool EnableHoverHop { get => enableHoverHop; set => enableHoverHop = value; }
    public float HoverHopIntensity { get => hoverHopIntensity; set => hoverHopIntensity = Mathf.Max(0f, value); }
    public Vector3 HoverHopOffset { get => hoverHopOffset; set => hoverHopOffset = value; }
    public float HoverHopDuration { get => hoverHopDuration; set => hoverHopDuration = Mathf.Max(0.01f, value); }
    public Vector3 HoverHopPunchScale { get => hoverHopPunchScale; set => hoverHopPunchScale = value; }
    
    public bool IsHopping
    {
        get
        {
            if (!_isHopping || !gameObject.activeInHierarchy || !enabled)
            {
                _isHopping = false;
                _currentHopPosOffset = Vector3.zero;
                _currentHopScaleMultiplier = Vector3.one;
                return false;
            }

            if (Time.realtimeSinceStartup - _hopStartTime >= _hopDuration)
            {
                _isHopping = false;
                _currentHopPosOffset = Vector3.zero;
                _currentHopScaleMultiplier = Vector3.one;
                return false;
            }

            return true;
        }
    }

    public Vector3 CurrentHopPosOffset
    {
        get
        {
            if (!IsHopping) return Vector3.zero;
            CalculateHopTransform(out Vector3 posOffset, out _);
            return posOffset;
        }
    }

    public Vector3 CurrentHopScaleMultiplier
    {
        get
        {
            if (!IsHopping) return Vector3.one;
            CalculateHopTransform(out _, out Vector3 scaleMult);
            return scaleMult;
        }
    }

    public List<Renderer> TargetRenderers => targetRenderers;

    protected virtual void Awake()
    {
        _originalLocalPos = transform.localPosition;
        _originalLocalScale = transform.localScale;

        if (targetRenderers == null || targetRenderers.Count == 0)
        {
            GetComponentsInChildren(true, targetRenderers);
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && (hoverSound != null || focusSound != null || unfocusSound != null))
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
        }

        // Garante que o objeto tenha um Collider válido para raycasting
        EnsureCollider();
    }

    /// <summary>
    /// Garante a existência de um Collider válido e com dimensões positivas para recepção de cliques do mouse.
    /// </summary>
    public void EnsureCollider()
    {
        // Se já possui collider no próprio GameObject, garante que esteja ativo
        var existingCollider = GetComponent<Collider>();
        if (existingCollider != null)
        {
            existingCollider.enabled = true;
            return;
        }

        // Tenta usar MeshFilter do próprio GameObject se disponível
        var meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            var meshCollider = gameObject.AddComponent<MeshCollider>();
            meshCollider.convex = false;
            return;
        }

        // Caso possua malhas nele mesmo ou nos filhos, calcula o Bounding Box local preciso
        // transformando os 8 vértices locais de cada malha filha para o espaço local deste Transform.
        var meshFilters = GetComponentsInChildren<MeshFilter>(true);
        if (meshFilters.Length > 0)
        {
            Bounds localBounds = new Bounds();
            bool hasBounds = false;

            foreach (var mf in meshFilters)
            {
                if (mf == null || mf.sharedMesh == null) continue;

                Bounds b = mf.sharedMesh.bounds;
                Matrix4x4 childToLocal = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;

                Vector3[] corners = new Vector3[8]
                {
                    childToLocal.MultiplyPoint3x4(new Vector3(b.min.x, b.min.y, b.min.z)),
                    childToLocal.MultiplyPoint3x4(new Vector3(b.min.x, b.min.y, b.max.z)),
                    childToLocal.MultiplyPoint3x4(new Vector3(b.min.x, b.max.y, b.min.z)),
                    childToLocal.MultiplyPoint3x4(new Vector3(b.min.x, b.max.y, b.max.z)),
                    childToLocal.MultiplyPoint3x4(new Vector3(b.max.x, b.min.y, b.min.z)),
                    childToLocal.MultiplyPoint3x4(new Vector3(b.max.x, b.min.y, b.max.z)),
                    childToLocal.MultiplyPoint3x4(new Vector3(b.max.x, b.max.y, b.min.z)),
                    childToLocal.MultiplyPoint3x4(new Vector3(b.max.x, b.max.y, b.max.z))
                };

                foreach (var pt in corners)
                {
                    if (!hasBounds)
                    {
                        localBounds = new Bounds(pt, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(pt);
                    }
                }
            }

            if (hasBounds)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.center = localBounds.center;
                box.size = new Vector3(
                    Mathf.Max(localBounds.size.x, 0.1f),
                    Mathf.Max(localBounds.size.y, 0.1f),
                    Mathf.Max(localBounds.size.z, 0.1f)
                );
                return;
            }
        }

        // Fallback se não houver malhas: adiciona BoxCollider unitário
        if (GetComponentInChildren<Collider>() == null)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.size = Vector3.one;
        }
    }

    protected virtual void Start()
    {
        if (Application.isPlaying && CameraFocusManager.Instance == null)
        {
            CameraFocusManager.EnsureExists();
        }

        if (targetRenderers == null || targetRenderers.Count == 0 || targetRenderers.TrueForAll(r => r == null))
        {
            targetRenderers = new List<Renderer>();
            GetComponentsInChildren(true, targetRenderers);
        }
    }

    protected virtual void Update()
    {
        UpdateHoverTransform(Time.unscaledDeltaTime);
    }

    /// <summary>
    /// Interpola suavemente a posicao e escala do objeto para dar resposta tatil no hover.
    /// </summary>
    protected virtual void UpdateHoverTransform(float dt)
    {
        if (dt <= 0.0001f) return;

        Vector3 targetOffset = Vector3.zero;
        Vector3 targetScaleMult = Vector3.one;

        if (_isHovered && !_isFocused)
        {
            if (enableHoverScale)
                targetScaleMult = Vector3.one * hoverScaleMultiplier;

            targetOffset = hoverLiftOffset;
        }

        _currentHoverPosOffset = Vector3.Lerp(_currentHoverPosOffset, targetOffset, dt * hoverTransitionSpeed);
        _currentHoverScaleMultiplier = Vector3.Lerp(_currentHoverScaleMultiplier, targetScaleMult, dt * hoverTransitionSpeed);

        float targetHighlight = (_isHovered && !_isFocused && enableOutlineHighlight) ? 1f : 0f;
        _currentHighlightWeight = Mathf.Lerp(_currentHighlightWeight, targetHighlight, dt * hoverTransitionSpeed);
        if (Mathf.Abs(_currentHighlightWeight - targetHighlight) < 0.001f)
        {
            _currentHighlightWeight = targetHighlight;
        }

        if (_isHovered && !_isFocused && enableOutlineHighlight && _currentHighlightWeight > 0.001f)
        {
            if (!ActiveHighlightedObjects.Contains(this))
            {
                ActiveHighlightedObjects.Add(this);
            }
        }
        else
        {
            ActiveHighlightedObjects.Remove(this);
        }

        CalculateHopTransform(out _currentHopPosOffset, out _currentHopScaleMultiplier);

        if (!_isFocused && (enableHoverScale || hoverLiftOffset.sqrMagnitude > 0.0001f || enableHoverHop || _currentHopPosOffset.sqrMagnitude > 0.00001f || _currentHopScaleMultiplier != Vector3.one))
        {
            transform.localPosition = _originalLocalPos + _currentHoverPosOffset + _currentHopPosOffset;
            transform.localScale = Vector3.Scale(Vector3.Scale(_originalLocalScale, _currentHoverScaleMultiplier), _currentHopScaleMultiplier);
        }
    }

    /// <summary>
    /// Chamado quando o mouse entra no colisor do objeto.
    /// </summary>
    public void NotifyHoverEnter()
    {
        if (_isHovered) return;
        _isHovered = true;

        if (targetRenderers == null || targetRenderers.Count == 0 || targetRenderers.TrueForAll(r => r == null))
        {
            targetRenderers = new List<Renderer>();
            GetComponentsInChildren(true, targetRenderers);
        }

        // Limpa qualquer outro objeto que possa ter ficado no estado ativo de highlight
        for (int i = ActiveHighlightedObjects.Count - 1; i >= 0; i--)
        {
            var other = ActiveHighlightedObjects[i];
            if (other != null && other != this)
            {
                other._isHovered = false;
                other._currentHighlightWeight = 0f;
                ActiveHighlightedObjects.RemoveAt(i);
            }
        }

        if (!_isFocused)
        {
            PlaySound(hoverSound, hoverSoundVolume);
            if (enableOutlineHighlight)
            {
                ActiveHighlightedObject = this;
                if (!ActiveHighlightedObjects.Contains(this))
                {
                    ActiveHighlightedObjects.Add(this);
                }
            }

            if (enableHoverHop && hoverHopIntensity > 0.001f)
            {
                PlayHoverHop();
            }
        }

        onHoverEnter?.Invoke();
    }

    /// <summary>
    /// Dispara a animação procedural de pulinho (hop) ao passar o mouse por cima do objeto.
    /// </summary>
    public void PlayHoverHop()
    {
        if (!enableHoverHop || hoverHopIntensity <= 0.001f || !gameObject.activeInHierarchy || !enabled || _isFocused)
            return;

        _isHopping = true;
        _hopStartTime = Time.realtimeSinceStartup;
        _hopDuration = HoverHopDuration;
    }

    /// <summary>
    /// Interrompe a animação procedural de pulinho e reseta os offsets de transformação.
    /// </summary>
    public void StopHoverHop()
    {
        _isHopping = false;
        _currentHopPosOffset = Vector3.zero;
        _currentHopScaleMultiplier = Vector3.one;
    }

    /// <summary>
    /// Calcula os offsets de translação e escala instantâneos para a animação de pulinho no hover.
    /// </summary>
    private void CalculateHopTransform(out Vector3 posOffset, out Vector3 scaleMult)
    {
        if (!_isHopping || !gameObject.activeInHierarchy || !enabled)
        {
            _isHopping = false;
            posOffset = Vector3.zero;
            scaleMult = Vector3.one;
            return;
        }

        float elapsed = Time.realtimeSinceStartup - _hopStartTime;
        if (elapsed >= _hopDuration || _hopDuration <= 0.0001f)
        {
            _isHopping = false;
            posOffset = Vector3.zero;
            scaleMult = Vector3.one;
            return;
        }

        Vector3 peakHopOffset = hoverHopOffset * hoverHopIntensity;
        Vector3 peakHopScale = Vector3.Lerp(Vector3.one, hoverHopPunchScale, hoverHopIntensity);
        float halfDuration = Mathf.Max(0.005f, _hopDuration * 0.5f);

        if (elapsed < halfDuration)
        {
            float t = Mathf.Clamp01(elapsed / halfDuration);
            posOffset = Vector3.Lerp(Vector3.zero, peakHopOffset, t);
            scaleMult = Vector3.Lerp(Vector3.one, peakHopScale, t);
        }
        else
        {
            float t = Mathf.Clamp01((elapsed - halfDuration) / halfDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            posOffset = Vector3.Lerp(peakHopOffset, Vector3.zero, smoothT);
            scaleMult = Vector3.Lerp(peakHopScale, Vector3.one, smoothT);
        }
    }

    /// <summary>
    /// Chamado quando o mouse sai do colisor do objeto.
    /// </summary>
    public void NotifyHoverExit()
    {
        if (!_isHovered) return;
        _isHovered = false;
        _currentHighlightWeight = 0f;

        ActiveHighlightedObjects.Remove(this);

        if (ActiveHighlightedObject == this)
        {
            ActiveHighlightedObject = null;
        }

        onHoverExit?.Invoke();
    }

    /// <summary>
    /// Chamado quando o objeto e clicado.
    /// </summary>
    public void NotifyClicked()
    {
        onClicked?.Invoke();

        if (!allowClickToFocus) return;

        if (CameraFocusManager.Instance == null)
        {
            CameraFocusManager.EnsureExists();
        }

        if (CameraFocusManager.Instance != null)
        {
            if (_isFocused && unfocusOnSecondClick)
            {
                CameraFocusManager.Instance.Unfocus();
            }
            else
            {
                CameraFocusManager.Instance.Focus(this);
            }
        }
    }

    /// <summary>
    /// Define o estado de foco deste objeto.
    /// </summary>
    public virtual void SetFocused(bool focused)
    {
        if (_isFocused == focused) return;
        _isFocused = focused;

        if (focused)
        {
            StopHoverHop();

            PlaySound(focusSound, focusSoundVolume);
            _currentHighlightWeight = 0f;
            ActiveHighlightedObjects.Remove(this);
            if (ActiveHighlightedObject == this)
            {
                ActiveHighlightedObject = null;
            }
            onFocused?.Invoke();
        }
        else
        {
            PlaySound(unfocusSound, unfocusSoundVolume);
            if (_isHovered && enableOutlineHighlight)
            {
                ActiveHighlightedObject = this;
                if (!ActiveHighlightedObjects.Contains(this))
                {
                    ActiveHighlightedObjects.Add(this);
                }
            }
            onUnfocused?.Invoke();
        }
    }

    /// <summary>
    /// Retorna a posicao mundial onde a camera deve ficar ao focar este objeto.
    /// </summary>
    public Transform CameraFocusPoint
    {
        get
        {
            if (cameraFocusPoint == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.name.IndexOf("focus", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        child.name.IndexOf("camera", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        child.name.IndexOf("point", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        cameraFocusPoint = child;
                        break;
                    }
                }
            }
            return cameraFocusPoint;
        }
        set => cameraFocusPoint = value;
    }

    /// <summary>
    /// Retorna a posicao mundial onde a camera deve ficar ao focar este objeto.
    /// </summary>
    public Vector3 GetCameraTargetPosition()
    {
        var focusPt = CameraFocusPoint;
        if (focusPt != null)
        {
            return focusPt.position;
        }

        return transform.TransformPoint(fallbackFocusOffset);
    }

    /// <summary>
    /// Retorna a rotacao mundial que a camera deve adotar ao focar este objeto.
    /// </summary>
    public Quaternion GetCameraTargetRotation()
    {
        var focusPt = CameraFocusPoint;
        if (focusPt != null)
        {
            return focusPt.rotation;
        }

        Vector3 camPos = GetCameraTargetPosition();
        Vector3 targetCenter = transform.position;
        Vector3 dir = (targetCenter - camPos).normalized;

        if (dir != Vector3.zero)
            return Quaternion.LookRotation(dir, Vector3.up);

        return Quaternion.identity;
    }

    protected void PlaySound(AudioClip clip, float volume)
    {
        if (clip == null) return;

        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : transform.position, volume);
        }
    }

    protected virtual void OnDisable()
    {
        StopHoverHop();

        if (_isHovered)
        {
            _isHovered = false;
        }
        _currentHighlightWeight = 0f;
        ActiveHighlightedObjects.Remove(this);
        if (ActiveHighlightedObject == this)
        {
            ActiveHighlightedObject = null;
        }

        if (_isFocused && CameraFocusManager.Instance != null && CameraFocusManager.Instance.CurrentFocusedObject == this)
        {
            CameraFocusManager.Instance.Unfocus();
        }
    }

    protected virtual void OnDestroy()
    {
        StopHoverHop();

        _currentHighlightWeight = 0f;
        ActiveHighlightedObjects.Remove(this);
        if (ActiveHighlightedObject == this)
        {
            ActiveHighlightedObject = null;
        }

        if (_isFocused && CameraFocusManager.Instance != null && CameraFocusManager.Instance.CurrentFocusedObject == this)
        {
            CameraFocusManager.Instance.Unfocus();
        }
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Vector3 targetCamPos = GetCameraTargetPosition();
        Quaternion targetCamRot = GetCameraTargetRotation();

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.85f);
        Gizmos.DrawLine(transform.position, targetCamPos);

        Gizmos.matrix = Matrix4x4.TRS(targetCamPos, targetCamRot, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.4f, 0.3f, 0.5f));

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.DrawFrustum(Vector3.zero, targetCameraFov > 0f ? targetCameraFov : 50f, 3.5f, 0.1f, 1.777f);
    }
}
}
