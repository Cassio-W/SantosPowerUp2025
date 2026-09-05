using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Componente modular que permite a qualquer objeto 3D receber efeitos de Hover (escala, elevacao, cor do outline no ToonOutlineFeature, som)
/// e, ao ser clicado, comanda a Camera a se mover suavemente para uma posicao de foco pre-determinada.
/// </summary>
[DisallowMultipleComponent]
[SelectionBase]
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

    [Header("--- Eventos Unity ---")]
    public UnityEvent onHoverEnter = new UnityEvent();
    public UnityEvent onHoverExit = new UnityEvent();
    public UnityEvent onFocused = new UnityEvent();
    public UnityEvent onUnfocused = new UnityEvent();
    public UnityEvent onClicked = new UnityEvent();

    // Estados internos
    private bool _isHovered;
    private bool _isFocused;
    private Vector3 _originalLocalPos;
    private Vector3 _originalLocalScale;
    private Vector3 _currentHoverPosOffset;
    private Vector3 _currentHoverScaleMultiplier = Vector3.one;
    private float _currentHighlightWeight = 0f;

    public bool IsHovered => _isHovered;
    public bool IsFocused => _isFocused;
    public float CurrentHighlightWeight => _currentHighlightWeight;
    public Transform CameraFocusPoint { get => cameraFocusPoint; set => cameraFocusPoint = value; }
    public Vector3 FallbackFocusOffset => fallbackFocusOffset;
    public float CustomTransitionDuration => customTransitionDuration;
    public bool OverrideCameraFov => targetCameraFov > 0f;
    public float TargetCameraFov => targetCameraFov;
    public bool EnableCameraEffectOnFocus { get => enableCameraEffectOnFocus; set => enableCameraEffectOnFocus = value; }
    public float CameraEffectWeight { get => cameraEffectWeight; set => cameraEffectWeight = Mathf.Clamp01(value); }
    public bool AllowClickToFocus => allowClickToFocus;
    public bool UnfocusOnSecondClick => unfocusOnSecondClick;
    public bool EnableOutlineHighlight => enableOutlineHighlight;
    public Color HighlightOutlineColor => highlightOutlineColor;
    public List<Renderer> TargetRenderers => targetRenderers;

    private void Awake()
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
        // Se já possui collider no próprio GameObject, garante dimensões estritamente positivas
        var existingCollider = GetComponent<Collider>();
        if (existingCollider != null)
        {
            if (existingCollider is BoxCollider boxExisting)
            {
                boxExisting.size = new Vector3(Mathf.Abs(boxExisting.size.x), Mathf.Abs(boxExisting.size.y), Mathf.Abs(boxExisting.size.z));
            }
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

        // Caso possua renderers nele mesmo ou nos filhos, gera um BoxCollider abrangente
        if (targetRenderers != null && targetRenderers.Count > 0)
        {
            var box = gameObject.AddComponent<BoxCollider>();

            Bounds localBounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;

            foreach (var rend in targetRenderers)
            {
                if (rend == null) continue;

                Bounds b = rend.bounds;
                Vector3 minLocal = transform.InverseTransformPoint(b.min);
                Vector3 maxLocal = transform.InverseTransformPoint(b.max);

                Vector3 center = (minLocal + maxLocal) * 0.5f;
                Vector3 size = new Vector3(
                    Mathf.Abs(maxLocal.x - minLocal.x),
                    Mathf.Abs(maxLocal.y - minLocal.y),
                    Mathf.Abs(maxLocal.z - minLocal.z)
                );

                Bounds rendLocalBounds = new Bounds(center, size);

                if (!hasBounds)
                {
                    localBounds = rendLocalBounds;
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(rendLocalBounds);
                }
            }

            if (hasBounds)
            {
                box.center = localBounds.center;
                box.size = new Vector3(
                    Mathf.Max(localBounds.size.x, 0.05f),
                    Mathf.Max(localBounds.size.y, 0.05f),
                    Mathf.Max(localBounds.size.z, 0.05f)
                );
            }
            else
            {
                box.size = Vector3.one;
            }
        }
        else if (GetComponentInChildren<Collider>() == null)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.size = Vector3.one;
        }
    }

    private void Start()
    {
        if (CameraFocusManager.Instance == null)
        {
            CameraFocusManager.EnsureExists();
        }
    }

    private void Update()
    {
        UpdateHoverTransform(Time.unscaledDeltaTime);
    }

    /// <summary>
    /// Interpola suavemente a posicao e escala do objeto para dar resposta tatil no hover.
    /// </summary>
    private void UpdateHoverTransform(float dt)
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

        transform.localPosition = _originalLocalPos + _currentHoverPosOffset;
        transform.localScale = Vector3.Scale(_originalLocalScale, _currentHoverScaleMultiplier);
    }

    /// <summary>
    /// Chamado quando o mouse entra no colisor do objeto.
    /// </summary>
    public void NotifyHoverEnter()
    {
        if (_isHovered) return;
        _isHovered = true;

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
        }

        onHoverEnter?.Invoke();
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
    public void SetFocused(bool focused)
    {
        if (_isFocused == focused) return;
        _isFocused = focused;

        if (focused)
        {
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
    public Vector3 GetCameraTargetPosition()
    {
        if (cameraFocusPoint != null)
        {
            return cameraFocusPoint.position;
        }

        return transform.TransformPoint(fallbackFocusOffset);
    }

    /// <summary>
    /// Retorna a rotacao mundial que a camera deve adotar ao focar este objeto.
    /// </summary>
    public Quaternion GetCameraTargetRotation()
    {
        if (cameraFocusPoint != null)
        {
            return cameraFocusPoint.rotation;
        }

        Vector3 camPos = GetCameraTargetPosition();
        Vector3 targetCenter = transform.position;
        Vector3 dir = (targetCenter - camPos).normalized;

        if (dir != Vector3.zero)
            return Quaternion.LookRotation(dir, Vector3.up);

        return Quaternion.identity;
    }

    private void PlaySound(AudioClip clip, float volume)
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

    private void OnDisable()
    {
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

    private void OnDestroy()
    {
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

    private void OnDrawGizmosSelected()
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
