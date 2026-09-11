using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Permite interação completa (clique, hover e eventos de ponteiro) com UIs do UI Toolkit
/// renderizadas via RenderTexture no material de um objeto 3D (ex: Monitor Retrô, Flip-Phone, Papéis).
/// Converte de forma precisa as coordenadas UV de impacto do Raycast para o espaço de coordenadas do painel UI Toolkit.
/// </summary>
[DisallowMultipleComponent]
public class WorldSpaceUIInteraction : MonoBehaviour
{
    [Header("--- Referências ---")]
    [Tooltip("UIDocument que renderiza a interface na RenderTexture.")]
    [SerializeField] private UIDocument uiDocument;

    [Tooltip("Câmera usada para raycast da interação. Se vazia, busca Camera.main ou câmera ativa automaticamente.")]
    [SerializeField] private Camera interactionCamera;

    [Tooltip("LayerMask para os raycasts de interação (padrão Everything).")]
    [SerializeField] private LayerMask interactableLayers = ~0;

    [Tooltip("Distância máxima do raio de interação em unidades de mundo.")]
    [SerializeField] private float maxRaycastDistance = 50f;

    [Header("--- Calibração de Coordenadas UV ---")]
    [Tooltip("Inverte o eixo horizontal (X) das coordenadas UV.")]
    [SerializeField] private bool flipX = false;

    [Tooltip("Inverte o eixo vertical (Y) das coordenadas UV (padrão true pois UV 0 é embaixo e UI Toolkit 0 é no topo).")]
    [SerializeField] private bool flipY = true;

    [Tooltip("Troca os eixos X e Y (necessário se o mapeamento UV do modelo 3D estiver rotacionado em 90 graus).")]
    [SerializeField] private bool swapXY = false;

    [Tooltip("Offset adicional nas coordenadas UV (U, V).")]
    [SerializeField] private Vector2 uvOffset = Vector2.zero;

    [Tooltip("Multiplicador de escala nas coordenadas UV.")]
    [SerializeField] private Vector2 uvScale = Vector2.one;

    [Header("--- Debug & Visualização ---")]
    [Tooltip("Exibe logs detalhados no Console durante hover e cliques.")]
    [SerializeField] private bool showDebugLogs = false;

    [Tooltip("Desenha um overlay informativo na tela durante a execução do jogo para calibração rápida.")]
    [SerializeField] private bool showOnScreenOverlay = false;

    [Tooltip("Desenha gizmos na Scene View mostrando o raio e o ponto de impacto na malha 3D.")]
    [SerializeField] private bool drawGizmos = true;

    private Collider _collider;
    private VisualElement _lastHoveredElement;
    private bool _isPointerDown = false;
    private Vector2 _lastHitUV = Vector2.zero;
    private Vector2 _lastPanelPosition = Vector2.zero;
    private bool _isCurrentlyHit = false;
    private Vector3 _lastHitWorldPoint = Vector3.zero;

    public bool IsCurrentlyHit => _isCurrentlyHit;
    public Vector2 LastHitUV => _lastHitUV;
    public Vector2 LastPanelPosition => _lastPanelPosition;
    public VisualElement LastHoveredElement => _lastHoveredElement;

    private void Awake()
    {
        EnsureCollider();
        EnsureReferences();
    }

    private void OnEnable()
    {
        EnsureCollider();
        EnsureReferences();
    }

    private void EnsureReferences()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>() ?? GetComponentInChildren<UIDocument>();
            if (uiDocument == null)
            {
                uiDocument = GetComponentInParent<UIDocument>();
            }

            if (uiDocument == null)
            {
                uiDocument = FindFirstObjectByType<UIDocument>();
            }
        }

        if (interactionCamera == null)
        {
            interactionCamera = Camera.main;
            if (interactionCamera == null)
            {
                interactionCamera = FindFirstObjectByType<Camera>();
            }
        }
    }

    /// <summary>
    /// Garante que o objeto possua um colisor adequado para interações UV.
    /// Dá preferência a MeshCollider com a malha do MeshFilter para garantir cálculo de UV nativo e exato.
    /// </summary>
    public void EnsureCollider()
    {
        var meshFilter = GetComponent<MeshFilter>();

        // Se tiver MeshFilter, garante que tenhamos um MeshCollider com a mesma malha
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            var meshCollider = GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                var oldBox = GetComponent<BoxCollider>();
                if (oldBox != null)
                {
                    oldBox.enabled = false;
                }

                meshCollider = gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                meshCollider.convex = false;
            }
            else if (meshCollider.sharedMesh == null)
            {
                meshCollider.sharedMesh = meshFilter.sharedMesh;
            }

            _collider = meshCollider;
            return;
        }

        if (_collider == null)
        {
            _collider = GetComponent<Collider>();
            if (_collider == null)
            {
                _collider = gameObject.AddComponent<BoxCollider>();
            }
        }
    }

    private void Update()
    {
        EnsureReferences();

        if (uiDocument == null || uiDocument.rootVisualElement == null || uiDocument.rootVisualElement.panel == null)
            return;

        if (interactionCamera == null)
        {
            interactionCamera = Camera.main ?? FindFirstObjectByType<Camera>();
            if (interactionCamera == null) return;
        }

        Ray ray = interactionCamera.ScreenPointToRay(Input.mousePosition);
        bool hitFound = false;
        RaycastHit validHit = default;

        // 1. Raycast de cena com Physics.Raycast (obtém hit.textureCoord nativo exato do PhysX)
        if (Physics.Raycast(ray, out RaycastHit sceneHit, maxRaycastDistance, interactableLayers))
        {
            if (sceneHit.collider == _collider || sceneHit.transform == transform || sceneHit.transform.IsChildOf(transform))
            {
                validHit = sceneHit;
                hitFound = true;
            }
        }

        // 2. Fallback de colisor direto caso haja layers ou triggers no caminho
        if (!hitFound && _collider != null && _collider.Raycast(ray, out RaycastHit directHit, maxRaycastDistance))
        {
            validHit = directHit;
            hitFound = true;
        }

        _isCurrentlyHit = hitFound;

        if (hitFound)
        {
            _lastHitWorldPoint = validHit.point;
            Vector2 rawUV = CalculateUV(validHit);
            Vector2 calibratedUV = ApplyUVCalibration(rawUV);

            _lastHitUV = calibratedUV;

            // Determina as dimensões do painel UI Toolkit
            Vector2 panelSize = GetPanelResolution();
            Vector2 panelPosition = new Vector2(
                Mathf.Clamp(calibratedUV.x * panelSize.x, 0f, panelSize.x),
                Mathf.Clamp(calibratedUV.y * panelSize.y, 0f, panelSize.y)
            );

            _lastPanelPosition = panelPosition;

            // Identifica o elemento sob o ponteiro
            var target = uiDocument.rootVisualElement.panel.Pick(panelPosition) ?? uiDocument.rootVisualElement;

            // 1. Processa Hover / Pointer Move
            ProcessHover(target, panelPosition);

            // 2. Processa Clique (Mouse Down)
            if (Input.GetMouseButtonDown(0))
            {
                ProcessPointerDown(target, panelPosition);
            }

            // 3. Processa Soltura do Clique (Mouse Up & Click)
            if (Input.GetMouseButtonUp(0) && _isPointerDown)
            {
                ProcessPointerUpAndClick(target, panelPosition);
            }

            // 4. Processa Scrollwheel
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                ProcessScroll(target);
            }
        }
        else
        {
            // Mouse saiu da área da UI 3D
            ClearHover();

            if (Input.GetMouseButtonUp(0) && _isPointerDown)
            {
                _isPointerDown = false;
            }
        }
    }

    private Vector2 CalculateUV(RaycastHit hit)
    {
        // 1. Se o colisor for MeshCollider, PhysX calcula a interpolação exata dos vértices da malha
        if (hit.collider is MeshCollider)
        {
            return hit.textureCoord;
        }

        // 2. Fallback para BoxCollider ou outros: calcula UV baseado nas dimensões locais da face atingida
        Vector3 localPoint = transform.InverseTransformPoint(hit.point);
        Vector3 localNormal = transform.InverseTransformDirection(hit.normal);

        if (_collider is BoxCollider box)
        {
            Vector3 size = box.size;
            Vector3 center = box.center;

            if (Mathf.Abs(localNormal.z) >= Mathf.Abs(localNormal.x) && Mathf.Abs(localNormal.z) >= Mathf.Abs(localNormal.y))
            {
                float u = Mathf.InverseLerp(center.x - size.x * 0.5f, center.x + size.x * 0.5f, localPoint.x);
                float v = Mathf.InverseLerp(center.y - size.y * 0.5f, center.y + size.y * 0.5f, localPoint.y);
                return new Vector2(u, v);
            }
            else if (Mathf.Abs(localNormal.x) >= Mathf.Abs(localNormal.y))
            {
                float u = Mathf.InverseLerp(center.z - size.z * 0.5f, center.z + size.z * 0.5f, localPoint.z);
                float v = Mathf.InverseLerp(center.y - size.y * 0.5f, center.y + size.y * 0.5f, localPoint.y);
                return new Vector2(u, v);
            }
            else
            {
                float u = Mathf.InverseLerp(center.x - size.x * 0.5f, center.x + size.x * 0.5f, localPoint.x);
                float v = Mathf.InverseLerp(center.z - size.z * 0.5f, center.z + size.z * 0.5f, localPoint.z);
                return new Vector2(u, v);
            }
        }

        return hit.textureCoord;
    }

    private Vector2 ApplyUVCalibration(Vector2 uv)
    {
        if (swapXY)
        {
            float temp = uv.x;
            uv.x = uv.y;
            uv.y = temp;
        }

        if (flipX) uv.x = 1.0f - uv.x;
        if (flipY) uv.y = 1.0f - uv.y;

        uv.x = (uv.x + uvOffset.x) * uvScale.x;
        uv.y = (uv.y + uvOffset.y) * uvScale.y;

        return uv;
    }

    public Vector2 GetPanelResolution()
    {
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            var panel = uiDocument.rootVisualElement.panel;
            if (panel != null && panel.visualTree != null)
            {
                var layout = panel.visualTree.layout;
                if (layout.width > 0 && layout.height > 0)
                {
                    return new Vector2(layout.width, layout.height);
                }
            }

            var rootLayout = uiDocument.rootVisualElement.layout;
            if (rootLayout.width > 0 && rootLayout.height > 0)
            {
                return new Vector2(rootLayout.width, rootLayout.height);
            }
        }

        if (uiDocument != null && uiDocument.panelSettings != null)
        {
            if (uiDocument.panelSettings.targetTexture != null)
            {
                var tex = uiDocument.panelSettings.targetTexture;
                return new Vector2(tex.width, tex.height);
            }

            var res = uiDocument.panelSettings.referenceResolution;
            if (res.x > 0 && res.y > 0) return res;
        }

        return new Vector2(1920, 1080);
    }

    private void ProcessHover(VisualElement target, Vector2 panelPosition)
    {
        if (target != _lastHoveredElement)
        {
            if (_lastHoveredElement != null)
            {
                using var outEvent = MouseOutEvent.GetPooled();
                outEvent.target = _lastHoveredElement;
                _lastHoveredElement.SendEvent(outEvent);

                using var leaveEvent = PointerLeaveEvent.GetPooled();
                leaveEvent.target = _lastHoveredElement;
                _lastHoveredElement.SendEvent(leaveEvent);
            }

            _lastHoveredElement = target;

            if (target != null)
            {
                using var overEvent = MouseOverEvent.GetPooled();
                overEvent.target = target;
                target.SendEvent(overEvent);

                using var enterEvent = PointerEnterEvent.GetPooled();
                enterEvent.target = target;
                target.SendEvent(enterEvent);

                if (showDebugLogs)
                {
                    Debug.Log($"<color=#6a9fb5>[WorldSpaceUI]</color> 🔍 Hover: <b>{target.name}</b> ({target.GetType().Name}) em ({panelPosition.x:F0}, {panelPosition.y:F0})");
                }
            }
        }

        if (target != null)
        {
            using var moveEvent = PointerMoveEvent.GetPooled();
            moveEvent.target = target;
            target.SendEvent(moveEvent);

            using var mouseMove = MouseMoveEvent.GetPooled();
            mouseMove.target = target;
            target.SendEvent(mouseMove);
        }
    }

    private void ClearHover()
    {
        if (_lastHoveredElement != null)
        {
            using var outEvent = MouseOutEvent.GetPooled();
            outEvent.target = _lastHoveredElement;
            _lastHoveredElement.SendEvent(outEvent);

            using var leaveEvent = PointerLeaveEvent.GetPooled();
            leaveEvent.target = _lastHoveredElement;
            _lastHoveredElement.SendEvent(leaveEvent);

            _lastHoveredElement = null;
        }
    }

    private void ProcessPointerDown(VisualElement target, Vector2 panelPosition)
    {
        _isPointerDown = true;

        if (target == null) return;

        if (showDebugLogs)
        {
            Debug.Log($"<color=#00e5ff>[WorldSpaceUI]</color> ⬇️ PointerDown em: <b>{target.name}</b> ({target.GetType().Name})");
        }

        using var pointerDown = PointerDownEvent.GetPooled();
        pointerDown.target = target;
        target.SendEvent(pointerDown);

        using var mouseDown = MouseDownEvent.GetPooled();
        mouseDown.target = target;
        target.SendEvent(mouseDown);
    }

    private void ProcessPointerUpAndClick(VisualElement target, Vector2 panelPosition)
    {
        _isPointerDown = false;

        if (target == null) return;

        if (showDebugLogs)
        {
            Debug.Log($"<color=#00ffaa>[WorldSpaceUI]</color> ⬆️ PointerUp / Click em: <b>{target.name}</b> ({target.GetType().Name})");
        }

        // 1. Dispara PointerUp & MouseUp
        using var pointerUp = PointerUpEvent.GetPooled();
        pointerUp.target = target;
        target.SendEvent(pointerUp);

        using var mouseUp = MouseUpEvent.GetPooled();
        mouseUp.target = target;
        target.SendEvent(mouseUp);

        // 2. Dispara ClickEvent (garante que callbacks registrados com RegisterCallback<ClickEvent> executem no elemento ou ancestrais)
        using var clickEvent = ClickEvent.GetPooled();
        clickEvent.target = target;
        target.SendEvent(clickEvent);

        // 3. Fallback para Botões do UI Toolkit (garante execução imediata de .clicked)
        Button button = target as Button ?? target.GetFirstAncestorOfType<Button>();
        if (button != null && button.enabledSelf && button.enabledInHierarchy)
        {
            using var submitEvent = NavigationSubmitEvent.GetPooled();
            submitEvent.target = button;
            button.SendEvent(submitEvent);

            if (showDebugLogs)
            {
                Debug.Log($"<color=#00ffaa>[WorldSpaceUI]</color> 🎯 Botão <b>'{button.name}'</b> acionado com sucesso.");
            }
        }
    }

    private void ProcessScroll(VisualElement target)
    {
        if (target == null) return;

        using var wheelEvent = WheelEvent.GetPooled();
        wheelEvent.target = target;
        target.SendEvent(wheelEvent);
    }

    private void OnDisable()
    {
        ClearHover();
        _isPointerDown = false;
        _isCurrentlyHit = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        if (_isCurrentlyHit)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(_lastHitWorldPoint, 0.02f);
            if (interactionCamera != null)
            {
                Gizmos.DrawLine(interactionCamera.transform.position, _lastHitWorldPoint);
            }
        }
    }

    private void OnGUI()
    {
        if (!showOnScreenOverlay) return;

        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(10, 10, 360, 220), GUI.skin.box);
        GUILayout.Label("<b>🖥️ WorldSpace UI Inspector</b>");
        GUILayout.Label($"Objeto: {gameObject.name}");
        GUILayout.Label($"Colisor: {(_collider != null ? _collider.GetType().Name : "NENHUM")}");
        GUILayout.Label($"Hit Ativo: {(_isCurrentlyHit ? "<color=green>SIM</color>" : "<color=red>NÃO</color>")}");
        GUILayout.Label($"UV Calibrado: ({_lastHitUV.x:F3}, {_lastHitUV.y:F3})");
        GUILayout.Label($"Painel Coord: ({_lastPanelPosition.x:F0}, {_lastPanelPosition.y:F0})");
        GUILayout.Label($"Elemento Hover: <b>{(_lastHoveredElement != null ? _lastHoveredElement.name : "NENHUM")}</b> ({(_lastHoveredElement != null ? _lastHoveredElement.GetType().Name : "")})");
        GUILayout.Label($"Pointer Down: {(_isPointerDown ? "SIM" : "NÃO")}");
        GUILayout.EndArea();
    }
}
