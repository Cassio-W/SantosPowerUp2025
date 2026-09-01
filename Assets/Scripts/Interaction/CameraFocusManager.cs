using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// Gerenciador central de foco da camera e interacoes com objetos 3D.
/// Move e rotaciona a camera suavemente entre o estado padrao e as posicoes de foco dos objetos.
/// </summary>
[DisallowMultipleComponent]
public class CameraFocusManager : MonoBehaviour
{
    public static CameraFocusManager Instance { get; private set; }

    [Header("--- Referencias de Camera ---")]
    [Tooltip("Camera a ser movimentada. Se deixada vazia, utiliza a Camera.main automaticamente.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Transform que define a posicao/rotacao inicial padrao da camera. Se vazio, captura a posicao inicial da camera na cena.")]
    [SerializeField] private Transform defaultCameraAnchor;

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
    private Coroutine _cameraMoveCoroutine;

    public FocusableObject CurrentFocusedObject => _currentFocusedObject;
    public FocusableObject CurrentHoveredObject => _currentHoveredObject;
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
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }
            if (targetCamera == null)
            {
                targetCamera = FindFirstObjectByType<Camera>();
            }
        }

        if (targetCamera != null)
        {
            _defaultFov = targetCamera.fieldOfView;
        }
    }

    private void Start()
    {
        CaptureDefaultCameraTransform();
    }

    /// <summary>
    /// Garante que o CameraFocusManager exista na cena ativa.
    /// </summary>
    public static CameraFocusManager EnsureExists()
    {
        if (Instance != null) return Instance;

        Camera cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            Instance = cam.gameObject.AddComponent<CameraFocusManager>();
            Instance.targetCamera = cam;
            return Instance;
        }

        GameObject go = new GameObject("CameraFocusManager");
        Instance = go.AddComponent<CameraFocusManager>();
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
    /// Processa o raio do mouse para deteccao de hover e clique nos objetos interativos.
    /// </summary>
    private void HandleMouseRaycast()
    {
        if (targetCamera == null) return;

        // Se o mouse estiver sobre um elemento de UI, nao processa o raio na cena 3D
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            ClearHover();
            return;
        }

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, raycastDistance, interactableLayers);

        FocusableObject hitFocusable = null;
        if (hitSomething)
        {
            hitFocusable = hit.collider.GetComponentInParent<FocusableObject>();
        }

        // Atualizacao de Hover
        if (hitFocusable != _currentHoveredObject)
        {
            ClearHover();

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
            if (hitFocusable != null && hitFocusable.enabled)
            {
                hitFocusable.NotifyClicked();
            }
            else if (unfocusOnEmptyClick && HasActiveFocus)
            {
                Unfocus();
            }
        }
    }

    private void ClearHover()
    {
        if (_currentHoveredObject != null)
        {
            _currentHoveredObject.NotifyHoverExit();
            OnObjectHoverChanged?.Invoke(_currentHoveredObject, false);
            _currentHoveredObject = null;
        }
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
        float targetFov = target.OverrideCameraFov ? target.TargetCameraFov : _defaultFov;
        float duration = target.CustomTransitionDuration > 0f ? target.CustomTransitionDuration : transitionDuration;

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

        Vector3 targetPos = defaultCameraAnchor != null ? defaultCameraAnchor.position : _defaultPosition;
        Quaternion targetRot = defaultCameraAnchor != null ? defaultCameraAnchor.rotation : _defaultRotation;
        float duration = customDuration > 0f ? customDuration : transitionDuration;

        MoveCameraTo(targetPos, targetRot, _defaultFov, duration);

        onFocusChanged?.Invoke(null);
        OnObjectFocusChanged?.Invoke(null);
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
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
