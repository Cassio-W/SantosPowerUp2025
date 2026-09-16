using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Mandato.Presentation
{
    /// <summary>
    /// Gerenciador central de foco da camera e interacoes com objetos 3D.
    /// Move e rotaciona a camera suavemente entre o estado padrao e as posicoes de foco dos objetos.
    /// </summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp", null)]
    public class CameraFocusManager : MonoBehaviour
{
    public static CameraFocusManager Instance { get; private set; }

    [Header("--- Referencias de Camera ---")]
    [Tooltip("Camera a ser movimentada. Se deixada vazia, utiliza a Camera.main automaticamente.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Documentos de UI de tela monitorados para bloquear cliques 3D.")]
    [SerializeField] private List<UIDocument> screenUIDocuments = new List<UIDocument>();

    [Tooltip("Transform que define a posicao/rotacao inicial padrao da camera. Se vazio, captura a posicao inicial da camera na cena.")]
    [SerializeField] private Transform defaultCameraAnchor;

    [Tooltip("Se ativo, reduz o Near Clip Plane da camera para evitar que a mao ou objetos proximos sumam / sejam cortados.")]
    [SerializeField] private bool autoAdjustNearClipPlane = true;

    [Tooltip("Valor do Near Clip Plane aplicado automaticamente (ex: 0.02 = 2cm).")]
    [SerializeField] [Range(0.005f, 0.3f)] private float targetNearClipPlane = 0.02f;

    [Header("--- Animacao e Interpolacao ---")]
    [Tooltip("Duracao da transicao da camera em segundos.")]
    [SerializeField] private float transitionDuration = 0.65f;

    [Tooltip("Curva de interpolacao do movimento da camera.")]
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Se ativo, as animacoes de camera funcionam mesmo se Time.timeScale for 0 (jogo pausado).")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("--- Configuracoes de Raycast / Interacao ---")]
    [Tooltip("Habilita deteccao automatica de hover e clique do mouse em FocusableObjects.")]
    [SerializeField] private bool enableMouseInteraction = true;

    [Tooltip("LayerMask dos objetos interativos.")]
    [SerializeField] private LayerMask interactableLayers = ~0;

    [Tooltip("Distancia maxima do raio de colisao do mouse.")]
    [SerializeField] private float raycastDistance = 100f;

    [Tooltip("Se ativo, clicar em uma area vazia da cena desfaz o foco atual.")]
    [SerializeField] private bool unfocusOnEmptyClick = true;

    [Tooltip("Tecla para cancelar o foco e retornar a camera para a posicao padrao.")]
    [SerializeField] private KeyCode unfocusKey = KeyCode.Escape;

    [Header("--- Pos-Processamento / Volume ---")]
    [Tooltip("Volume responsavel pelo efeito de foco (desfoque, vignetting, etc.). Se vazio, tentara encontrar automaticamente.")]
    [SerializeField] private Volume focusVolume;

    [Header("--- Eventos Globais ---")]
    public UnityEvent<FocusableObject> onFocusChanged = new UnityEvent<FocusableObject>();

    public event Action<FocusableObject> OnObjectFocusChanged;
    public event Action<FocusableObject, bool> OnObjectHoverChanged;

    // Estados
    private Vector3 _defaultPosition;
    private Quaternion _defaultRotation;
    private float _defaultFov = 60f;

    private FocusableObject _currentFocusedObject;
    private FocusableObject _currentHoveredObject;
    private WorldSpaceUIInteraction _activeWorldSpaceUI;
    private FocusableObject _activeFocusable;
    private RaycastHit _activeRaycastHit;
    private bool _hasActiveInteractiveHit;
    private Coroutine _cameraMoveCoroutine;
    private Coroutine _effectCoroutine;

    public FocusableObject CurrentFocusedObject => _currentFocusedObject;
    public FocusableObject CurrentHoveredObject => _currentHoveredObject;
    public WorldSpaceUIInteraction ActiveWorldSpaceUI => _activeWorldSpaceUI;
    public FocusableObject ActiveFocusable => _activeFocusable;
    public RaycastHit ActiveRaycastHit => _activeRaycastHit;
    public bool HasActiveInteractiveHit => _hasActiveInteractiveHit;
    public bool HasActiveFocus => _currentFocusedObject != null;
    public Camera TargetCamera => targetCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>() ?? Camera.main;
        }

        if (targetCamera != null)
        {
            _defaultFov = targetCamera.fieldOfView;
            if (autoAdjustNearClipPlane && targetCamera.nearClipPlane > targetNearClipPlane)
            {
                targetCamera.nearClipPlane = targetNearClipPlane;
            }
        }
    }

    private void Start()
    {
        CaptureDefaultCameraTransform();
        EnsureScreenUIDocuments();
    }

    /// <summary>
    /// Garante que os UIDocuments de tela ativos sejam monitorados caso a lista não tenha sido preenchida no Inspector.
    /// </summary>
    private void EnsureScreenUIDocuments()
    {
        if (screenUIDocuments == null)
        {
            screenUIDocuments = new List<UIDocument>();
        }

        if (screenUIDocuments.Count == 0)
        {
            var allDocs = FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var doc in allDocs)
            {
                if (doc != null && (doc.panelSettings == null || doc.panelSettings.targetTexture == null))
                {
                    if (!screenUIDocuments.Contains(doc))
                    {
                        screenUIDocuments.Add(doc);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Garante que o CameraFocusManager exista na cena ativa.
    /// </summary>
    public static CameraFocusManager EnsureExists()
    {
        return Instance;
    }

    /// <summary>
    /// Captura a posicao e rotacao padrao da camera (ou usa o defaultCameraAnchor).
    /// </summary>
    public void CaptureDefaultCameraTransform()
    {
        if (defaultCameraAnchor != null)
        {
            _defaultPosition = defaultCameraAnchor.position;
            _defaultRotation = defaultCameraAnchor.rotation;
        }
        else if (targetCamera != null)
        {
            _defaultPosition = targetCamera.transform.position;
            _defaultRotation = targetCamera.transform.rotation;
            _defaultFov = targetCamera.fieldOfView;
        }
    }

    /// <summary>
    /// Define um novo ponto ancora padrao para a camera retornar.
    /// </summary>
    public void SetDefaultCameraAnchor(Transform anchor)
    {
        defaultCameraAnchor = anchor;
        if (anchor != null)
        {
            _defaultPosition = anchor.position;
            _defaultRotation = anchor.rotation;
        }
    }

    private void Update()
    {
        if (unfocusKey != KeyCode.None && Input.GetKeyDown(unfocusKey))
        {
            if (HasActiveFocus)
            {
                Unfocus();
            }
        }

        if (enableMouseInteraction)
        {
            HandleMouseRaycast();
        }
    }

    /// <summary>
    /// Processa o raio do mouse para deteccao centralizada de hover e clique nos objetos interativos.
    /// Respeita a oclusão física: o primeiro objeto sólido ou interativo encontrado ao longo do raio (ex: celular ligado em primeiro plano)
    /// impede que o raio perfure e atinja objetos interativos ao fundo (ex: PC).
    /// </summary>
    private void HandleMouseRaycast()
    {
        if (targetCamera == null) return;

        // Se o mouse estiver sobre um elemento de UI interativo real de tela, cancela o raio 3D
        if (IsPointerOverInteractiveUI())
        {
            ClearHover();
            return;
        }

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, raycastDistance, interactableLayers, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        FocusableObject hitFocusable = null;
        WorldSpaceUIInteraction hitWorldSpaceUI = null;
        RaycastHit selectedHit = default;
        RaycastHit uiHit = default;
        bool hasHitObstacle = false;

        foreach (var h in hits)
        {
            if (h.collider == null) continue;

            // Ignora colisores que são filhos diretos da câmera e NÃO possuem scripts interativos (ex: mãos/corpo do jogador)
            if (targetCamera != null && h.collider.transform.IsChildOf(targetCamera.transform))
            {
                var camWsUI = h.collider.GetComponentInParent<WorldSpaceUIInteraction>() ??
                              h.collider.GetComponentInChildren<WorldSpaceUIInteraction>();
                var camFo = h.collider.GetComponentInParent<FocusableObject>();
                if (camWsUI == null && camFo == null)
                {
                    continue;
                }
            }

            // Ignora triggers puros que não possuem scripts interativos (como volumes de som/área)
            bool isInteractiveTrigger = h.collider.isTrigger && (
                h.collider.GetComponentInParent<WorldSpaceUIInteraction>() != null ||
                h.collider.GetComponentInChildren<WorldSpaceUIInteraction>() != null ||
                h.collider.GetComponentInParent<FocusableObject>() != null
            );

            if (h.collider.isTrigger && !isInteractiveTrigger)
            {
                continue;
            }

            // O primeiro colisor sólido ou interativo encontrado ao longo do raio define o obstáculo frontal
            if (!hasHitObstacle)
            {
                hasHitObstacle = true;
                selectedHit = h;

                var wsUI = h.collider.GetComponentInParent<WorldSpaceUIInteraction>() ??
                           h.collider.GetComponentInChildren<WorldSpaceUIInteraction>();
                var fo = h.collider.GetComponentInParent<FocusableObject>();

                if (wsUI != null && wsUI.enabled && wsUI.gameObject.activeInHierarchy)
                {
                    hitWorldSpaceUI = wsUI;
                    uiHit = h;
                }

                if (fo != null && fo.enabled && fo.gameObject.activeInHierarchy)
                {
                    hitFocusable = fo;
                }
            }

            // Se a entidade possui WorldSpaceUIInteraction, busca entre os hits dessa mesma entidade o hit exato no colisor da tela
            if (hitWorldSpaceUI != null)
            {
                if (h.collider == hitWorldSpaceUI.ScreenCollider ||
                    h.transform == hitWorldSpaceUI.transform ||
                    h.transform.IsChildOf(hitWorldSpaceUI.transform))
                {
                    uiHit = h;
                    break;
                }
            }
            else
            {
                // Se não há WorldSpaceUIInteraction na entidade frontal, encerra no primeiro colisor
                break;
            }
        }

        _activeWorldSpaceUI = hitWorldSpaceUI;
        _activeFocusable = hitFocusable;
        _activeRaycastHit = (hitWorldSpaceUI != null && uiHit.collider != null) ? uiHit : selectedHit;
        _hasActiveInteractiveHit = (hitWorldSpaceUI != null || hitFocusable != null);

        // Atualizacao de Hover para FocusableObject
        if (hitFocusable != _currentHoveredObject)
        {
            if (_currentHoveredObject != null)
            {
                _currentHoveredObject.NotifyHoverExit();
                OnObjectHoverChanged?.Invoke(_currentHoveredObject, false);
                _currentHoveredObject = null;
            }

            if (hitFocusable != null && hitFocusable.enabled)
            {
                _currentHoveredObject = hitFocusable;
                _currentHoveredObject.NotifyHoverEnter();
                OnObjectHoverChanged?.Invoke(_currentHoveredObject, true);
            }
        }

        // Clique do Mouse
        if (Input.GetMouseButtonDown(0))
        {
            if (hitWorldSpaceUI != null)
            {
                // Se estamos focando um objeto (ex: computador) a partir da visão distante e clicamos nele:
                if (hitFocusable != null && hitFocusable.enabled && (!HasActiveFocus || CurrentFocusedObject != hitFocusable))
                {
                    if (hitFocusable.AllowClickToFocus)
                    {
                        hitFocusable.NotifyClicked();
                    }
                }
                // Se já estiver focado no objeto ou se for um objeto puramente de UI (ex: Flip Phone),
                // a interação ocorre diretamente no WorldSpaceUIInteraction sem interferir no foco da câmera.
            }
            else if (hitFocusable != null && hitFocusable.enabled)
            {
                hitFocusable.NotifyClicked();
            }
            else if (unfocusOnEmptyClick && HasActiveFocus && !hasHitObstacle)
            {
                Unfocus();
            }
        }
    }

    /// <summary>
    /// Limpa o estado atual de hover e notifica o objeto anterior.
    /// </summary>
    public void ClearHover()
    {
        _activeWorldSpaceUI = null;
        _activeFocusable = null;
        _hasActiveInteractiveHit = false;

        if (_currentHoveredObject != null)
        {
            _currentHoveredObject.NotifyHoverExit();
            OnObjectHoverChanged?.Invoke(_currentHoveredObject, false);
            _currentHoveredObject = null;
        }
    }

    public void RegisterUIDocument(UIDocument doc)
    {
        if (doc != null && !screenUIDocuments.Contains(doc))
        {
            screenUIDocuments.Add(doc);
        }
    }

    /// <summary>
    /// Verifica de forma precisa se o mouse está sobre um controle interativo de UI (Botão, Slider, etc.).
    /// Evita que telas vazias, containers transparentes ou painéis de tela cheia do UI Toolkit
    /// bloqueiem indevidamente o raio de interação 3D com objetos do cenário.
    /// </summary>
    public bool IsPointerOverInteractiveUI()
    {
        EnsureScreenUIDocuments();

        // 1. Verificação direta nos UIDocuments registrados de tela (UI Toolkit)
        for (int i = screenUIDocuments.Count - 1; i >= 0; i--)
        {
            var doc = screenUIDocuments[i];
            if (doc == null)
            {
                screenUIDocuments.RemoveAt(i);
                continue;
            }

            if (doc.isActiveAndEnabled && doc.rootVisualElement != null && doc.rootVisualElement.panel != null)
            {
                if (doc.panelSettings != null && doc.panelSettings.targetTexture != null)
                {
                    continue;
                }

                // RuntimePanelUtils.ScreenToPanel espera Input.mousePosition (origem no canto inferior esquerdo)
                Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(
                    doc.rootVisualElement.panel,
                    Input.mousePosition
                );
                var picked = doc.rootVisualElement.panel.Pick(panelPos);

                if (picked != null && picked != doc.rootVisualElement)
                {
                    if (picked is UnityEngine.UIElements.Button ||
                        picked.GetFirstAncestorOfType<UnityEngine.UIElements.Button>() != null ||
                        picked is UnityEngine.UIElements.TextField ||
                        picked.ClassListContains("decision-btn") ||
                        picked.ClassListContains("selectable") ||
                        picked.GetFirstAncestorOfType<VisualElement>()?.ClassListContains("decision-btn") == true ||
                        picked.GetFirstAncestorOfType<VisualElement>()?.ClassListContains("selectable") == true)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Move a camera suavemente para focar o objeto especificado.
    /// </summary>
    public void Focus(FocusableObject target)
    {
        if (target == null)
        {
            Unfocus();
            return;
        }

        if (_currentFocusedObject == target) return;

        // Desfoca o anterior se houver
        if (_currentFocusedObject != null)
        {
            _currentFocusedObject.SetFocused(false);
        }

        _currentFocusedObject = target;
        _currentFocusedObject.SetFocused(true);

        Vector3 targetPos = target.GetCameraTargetPosition();
        Quaternion targetRot = target.GetCameraTargetRotation();
        float targetFov = target.TargetCameraFov > 0f ? target.TargetCameraFov : _defaultFov;
        float duration = target.CustomTransitionDuration > 0f ? target.CustomTransitionDuration : transitionDuration;

        // Ativa ou desativa o efeito de camera / pos-processamento baseado na flag e peso do objeto focado
        float targetEffectWeight = target.EnableCameraEffectOnFocus ? target.CameraEffectWeight : 0f;
        SetFocusCameraEffect(targetEffectWeight, duration);

        MoveCameraTo(targetPos, targetRot, targetFov, duration);

        onFocusChanged?.Invoke(_currentFocusedObject);
        OnObjectFocusChanged?.Invoke(_currentFocusedObject);
    }

    /// <summary>
    /// Retorna a camera para a posicao e rotacao padrao da cena.
    /// </summary>
    public void Unfocus(float customDuration = -1f)
    {
        if (_currentFocusedObject == null) return;

        FocusableObject prev = _currentFocusedObject;
        _currentFocusedObject.SetFocused(false);
        _currentFocusedObject = null;

        float duration = customDuration > 0f ? customDuration : transitionDuration;

        // Desativa o efeito de camera / pos-processamento ao retornar para a visao geral
        SetFocusCameraEffect(0f, duration);

        Vector3 targetPos = defaultCameraAnchor != null ? defaultCameraAnchor.position : _defaultPosition;
        Quaternion targetRot = defaultCameraAnchor != null ? defaultCameraAnchor.rotation : _defaultRotation;

        MoveCameraTo(targetPos, targetRot, _defaultFov, duration);

        onFocusChanged?.Invoke(null);
        OnObjectFocusChanged?.Invoke(null);
    }

    /// <summary>
    /// Transiciona suavemente o efeito de camera / pos-processamento (Volume e Edge Blur no ToonOutlineFeature).
    /// </summary>
    public void SetFocusCameraEffect(float targetWeight, float duration = 0.65f)
    {
        if (_effectCoroutine != null)
        {
            StopCoroutine(_effectCoroutine);
            _effectCoroutine = null;
        }

        Volume vol = GetFocusVolume();
        float currentBlur = Shader.GetGlobalFloat("_EdgeBlurIntensity");
        float currentVol = vol != null ? vol.weight : currentBlur;
        float startVal = Mathf.Max(currentBlur, currentVol);

        float targetVal = Mathf.Clamp01(targetWeight);
        duration = Mathf.Max(duration, 0.05f);

        if (Mathf.Abs(startVal - targetVal) < 0.001f || !gameObject.activeInHierarchy)
        {
            if (vol != null) vol.weight = targetVal;
            Shader.SetGlobalFloat("_EdgeBlurIntensity", targetVal);
            return;
        }

        _effectCoroutine = StartCoroutine(AnimateFocusEffectRoutine(startVal, targetVal, duration));
    }

    private IEnumerator AnimateFocusEffectRoutine(float startVal, float targetVal, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += delta;
            float t = Mathf.Clamp01(elapsed / duration);
            float curveT = Mathf.SmoothStep(0f, 1f, t);
            float current = Mathf.Lerp(startVal, targetVal, curveT);

            Volume vol = GetFocusVolume();
            if (vol != null) vol.weight = current;
            Shader.SetGlobalFloat("_EdgeBlurIntensity", current);

            yield return null;
        }

        Volume finalVol = GetFocusVolume();
        if (finalVol != null) finalVol.weight = targetVal;
        Shader.SetGlobalFloat("_EdgeBlurIntensity", targetVal);
        _effectCoroutine = null;
    }

    /// <summary>
    /// Sobrecarga para ativar ou desativar o efeito usando booleano (0f ou 1f).
    /// </summary>
    public void SetFocusCameraEffect(bool enable, float duration = 0.65f)
    {
        SetFocusCameraEffect(enable ? 1f : 0f, duration);
    }

    private Volume GetFocusVolume()
    {
        if (focusVolume != null) return focusVolume;

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam != null)
        {
            Volume[] volumes = cam.GetComponents<Volume>();
            foreach (var v in volumes)
            {
                if (v != null && v.sharedProfile != null && v.sharedProfile.name.Contains("1"))
                {
                    focusVolume = v;
                    return focusVolume;
                }
            }
            var camVol = cam.GetComponent<Volume>();
            if (camVol != null)
            {
                focusVolume = camVol;
                return focusVolume;
            }
        }

        return focusVolume;
    }

    /// <summary>
    /// Alterna o foco do objeto (se ja estiver focado, desfoca; caso contrario, foca).
    /// </summary>
    public void ToggleFocus(FocusableObject target)
    {
        if (_currentFocusedObject == target)
        {
            Unfocus();
        }
        else
        {
            Focus(target);
        }
    }

    /// <summary>
    /// Inicia a animacao suave da camera para as coordenadas alvos.
    /// </summary>
    private void MoveCameraTo(Vector3 targetPos, Quaternion targetRot, float targetFov, float duration)
    {
        if (targetCamera == null) return;

        if (_cameraMoveCoroutine != null)
        {
            StopCoroutine(_cameraMoveCoroutine);
        }

        _cameraMoveCoroutine = StartCoroutine(CameraTransitionRoutine(targetPos, targetRot, targetFov, duration));
    }

    private IEnumerator CameraTransitionRoutine(Vector3 endPos, Quaternion endRot, float endFov, float duration)
    {
        Transform camTransform = targetCamera.transform;
        Vector3 startPos = camTransform.position;
        Quaternion startRot = camTransform.rotation;
        float startFov = targetCamera.fieldOfView;

        float elapsed = 0f;
        duration = Mathf.Max(duration, 0.01f);

        while (elapsed < duration)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += dt;

            float t = Mathf.Clamp01(elapsed / duration);
            float curveT = transitionCurve != null ? transitionCurve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);

            camTransform.position = Vector3.Lerp(startPos, endPos, curveT);
            camTransform.rotation = Quaternion.Slerp(startRot, endRot, curveT);
            targetCamera.fieldOfView = Mathf.Lerp(startFov, endFov, curveT);

            yield return null;
        }

        camTransform.position = endPos;
        camTransform.rotation = endRot;
        targetCamera.fieldOfView = endFov;
        _cameraMoveCoroutine = null;
    }

    private void OnDestroy()
    {
        if (_effectCoroutine != null)
        {
            StopCoroutine(_effectCoroutine);
            _effectCoroutine = null;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
}
