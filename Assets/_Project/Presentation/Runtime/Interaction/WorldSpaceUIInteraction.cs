using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mandato.Presentation
{
    /// <summary>
    /// Permite interação completa (clique, hover e eventos de ponteiro) com UIs do UI Toolkit
    /// renderizadas via RenderTexture no material de um objeto 3D (ex: Monitor Retrô, Flip-Phone, Papéis).
    /// Converte de forma precisa as coordenadas UV de impacto do Raycast para o espaço de coordenadas do painel UI Toolkit.
    /// </summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp", null)]
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
    private VisualElement _pointerDownTarget;
    private bool _isPointerDown = false;
    private Vector2 _lastHitUV = Vector2.zero;
    private Vector2 _lastPanelPosition = Vector2.zero;
    private bool _isCurrentlyHit = false;
    private Vector3 _lastHitWorldPoint = Vector3.zero;

    public bool IsCurrentlyHit => _isCurrentlyHit;
    public Vector2 LastHitUV => _lastHitUV;
    public Vector2 LastPanelPosition => _lastPanelPosition;
    public VisualElement LastHoveredElement => _lastHoveredElement;
    public Collider ScreenCollider => _collider;

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

    private void Start()
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
        }

        if (interactionCamera == null)
        {
            interactionCamera = Camera.main;
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
            interactionCamera = Camera.main;
            if (interactionCamera == null) return;
        }

        Ray ray = interactionCamera.ScreenPointToRay(Input.mousePosition);
        bool hitFound = false;
        RaycastHit validHit = default;

        // 1. Se CameraFocusManager estiver ativo, consome a resolução centralizada de raycast de primeiro plano
        if (CameraFocusManager.Instance != null)
        {
            if (CameraFocusManager.Instance.ActiveWorldSpaceUI == this ||
                (CameraFocusManager.Instance.ActiveWorldSpaceUI != null &&
                 CameraFocusManager.Instance.ActiveWorldSpaceUI.transform.IsChildOf(transform)))
            {
                hitFound = true;
                validHit = CameraFocusManager.Instance.ActiveRaycastHit;
            }
        }
        else
        {
            // Fallback autônomo (ex: cenas de teste unitário isoladas sem CameraFocusManager)
            RaycastHit[] hits = Physics.RaycastAll(ray, maxRaycastDistance, interactableLayers, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var h in hits)
            {
                if (h.collider == null) continue;

                if (h.collider == _collider || h.transform == transform || h.transform.IsChildOf(transform))
                {
                    validHit = h;
                    hitFound = true;
                    break;
                }
            }

            if (!hitFound && _collider != null && _collider.Raycast(ray, out RaycastHit directHit, maxRaycastDistance))
            {
                validHit = directHit;
                hitFound = true;
            }
        }

        _isCurrentlyHit = hitFound;

        if (hitFound)
        {
            _lastHitWorldPoint = validHit.point;
            Vector2 rawUV = CalculateUV(validHit, ray);
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

    private Vector2 CalculateUV(RaycastHit hit, Ray ray)
    {
        // 1. Se o colisor atingido for o MeshCollider deste componente, usa o textureCoord exato do PhysX
        if (hit.collider is MeshCollider && (hit.collider == _collider || hit.transform == transform))
        {
            return hit.textureCoord;
        }

        // 2. Se temos um MeshCollider configurado neste componente, faz raycast direto para obter textureCoord nativo
        if (_collider is MeshCollider meshCol && _collider.Raycast(ray, out RaycastHit screenHit, maxRaycastDistance))
        {
            return screenHit.textureCoord;
        }

        // 3. Se hit.collider for outro MeshCollider filho/válido:
        if (hit.collider is MeshCollider)
        {
            return hit.textureCoord;
        }

        // 4. Fallback para BoxCollider ou planos: calcula UV baseado nas dimensões locais da face atingida
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

    /// <summary>
    /// Retorna as dimensões lógicas do painel UI Toolkit no espaço de Pick.
    /// IMPORTANTE: panel.Pick() opera em coordenadas LÓGICAS do painel (espaço de layout),
    /// NÃO em pixels físicos da RenderTexture. Usar pixels da RT causaria todos os picks
    /// no canto superior esquerdo porque as coordenadas ficariam fora do espaço lógico.
    /// Prioridade: layout do visualTree > layout do rootVisualElement > referenceResolution.
    /// </summary>
    public Vector2 GetPanelResolution()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null)
            goto fallback;

        // Prioridade 1: Dimensões lógicas do visualTree (espaço real onde panel.Pick() opera).
        var panel = uiDocument.rootVisualElement.panel;
        if (panel?.visualTree != null)
        {
            var layout = panel.visualTree.layout;
            if (layout.width > 1f && layout.height > 1f)
                return new Vector2(layout.width, layout.height);
        }

        // Prioridade 2: Layout do próprio rootVisualElement.
        {
            var rootLayout = uiDocument.rootVisualElement.layout;
            if (rootLayout.width > 1f && rootLayout.height > 1f)
                return new Vector2(rootLayout.width, rootLayout.height);
        }

        fallback:
        // Prioridade 3: referenceResolution como fallback final.
        if (uiDocument?.panelSettings != null)
        {
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
                SetHoverClass(_lastHoveredElement, false);

                using var outEvent = MouseOutEvent.GetPooled();
                outEvent.target = _lastHoveredElement;
                _lastHoveredElement.SendEvent(outEvent);

                using var leaveEvent = PointerLeaveEvent.GetPooled();
                leaveEvent.target = _lastHoveredElement;
                _lastHoveredElement.SendEvent(leaveEvent);
            }

            _lastHoveredElement = target;

            if (target != null && target != uiDocument.rootVisualElement)
            {
                SetHoverClass(target, true);

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

        if (target != null && target != uiDocument.rootVisualElement)
        {
            using var moveEvent = PointerMoveEvent.GetPooled();
            moveEvent.target = target;
            target.SendEvent(moveEvent);

            using var mouseMove = MouseMoveEvent.GetPooled();
            mouseMove.target = target;
            target.SendEvent(mouseMove);
        }
    }

    private void SetHoverClass(VisualElement el, bool isHovered)
    {
        if (el == null || uiDocument == null || el == uiDocument.rootVisualElement) return;

        if (isHovered)
        {
            el.AddToClassList("hovered");
        }
        else
        {
            el.RemoveFromClassList("hovered");
        }

        // Também aplica/remove nos ancestrais diretos que possam conter regras de estilo (ex: .giant-card, .giant-corruption-pillar, .app-tile, Button)
        var ancestor = el.parent;
        while (ancestor != null && ancestor != uiDocument.rootVisualElement)
        {
            if (ancestor.ClassListContains("giant-card") ||
                ancestor.ClassListContains("giant-corruption-pillar") ||
                ancestor.ClassListContains("app-tile") ||
                ancestor is Button)
            {
                if (isHovered)
                    ancestor.AddToClassList("hovered");
                else
                    ancestor.RemoveFromClassList("hovered");
            }
            ancestor = ancestor.parent;
        }
    }

    private void ClearHover()
    {
        if (_lastHoveredElement != null)
        {
            SetHoverClass(_lastHoveredElement, false);

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
        _pointerDownTarget = target;

        if (target == null) return;

        if (showDebugLogs)
        {
            Debug.Log($"<color=#00e5ff>[WorldSpaceUI]</color> ⬇️ PointerDown em: <b>{target.name}</b> ({target.GetType().Name}) painel=({panelPosition.x:F0},{panelPosition.y:F0})");
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

        // Usa o target capturado no MouseDown (mesmo frame do hover) para garantir
        // consistência entre o elemento visualmente hoverado e o elemento clicado.
        var clickTarget = _pointerDownTarget ?? target;
        _pointerDownTarget = null;

        if (clickTarget == null) return;

        if (showDebugLogs)
        {
            Debug.Log($"<color=#00ffaa>[WorldSpaceUI]</color> ⬆️ PointerUp — downTarget: <b>{clickTarget.name}</b> | upTarget: <b>{target.name}</b> painel=({panelPosition.x:F0},{panelPosition.y:F0})");
        }

        // 1. Dispara PointerUp & MouseUp no target atual (para ScrollView e animações)
        using var pointerUp = PointerUpEvent.GetPooled();
        pointerUp.target = target;
        target.SendEvent(pointerUp);

        using var mouseUp = MouseUpEvent.GetPooled();
        mouseUp.target = target;
        target.SendEvent(mouseUp);

        // 2. Busca Action no userData subindo pela hierarquia a partir do downTarget.
        // Qualquer VisualElement pode armazenar um System.Action em userData para receber
        // cliques world-space de forma direta, sem depender da propagação do ClickEvent.
        // IMPORTANTE: usa clickTarget (downTarget) — garante que o elemento hoverado é o que
        // recebe o clique, mesmo que o mouse mova levemente entre MouseDown e MouseUp.
        var current = clickTarget;
        bool handledByAction = false;
        while (current != null)
        {
            if (current.userData is System.Action directAction)
            {
                directAction.Invoke();
                handledByAction = true;

                if (showDebugLogs)
                {
                    Debug.Log($"<color=#00ffaa>[WorldSpaceUI]</color> 🎯 userData Action invocada em: <b>'{current.name}'</b>");
                }
                break;
            }
            current = current.parent;
        }

        // 3. Se nenhuma Action foi encontrada, usa ClickEvent padrão do UI Toolkit
        if (!handledByAction)
        {
            using var clickEvent = ClickEvent.GetPooled();
            clickEvent.target = clickTarget;
            clickTarget.SendEvent(clickEvent);

            // Fallback para Botões do UI Toolkit (garante execução imediata de .clicked)
            Button button = clickTarget as Button ?? clickTarget.GetFirstAncestorOfType<Button>();
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
        _pointerDownTarget = null;
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
        var panelRes = GetPanelResolution();
        GUILayout.BeginArea(new Rect(10, 10, 380, 250), GUI.skin.box);
        GUILayout.Label("<b>🖥️ WorldSpace UI Inspector</b>");
        GUILayout.Label($"Objeto: {gameObject.name}");
        GUILayout.Label($"Colisor: {(_collider != null ? _collider.GetType().Name : "NENHUM")}");
        GUILayout.Label($"Hit Ativo: {(_isCurrentlyHit ? "<color=green>SIM</color>" : "<color=red>NÃO</color>")}");
        GUILayout.Label($"Resolução Painel: ({panelRes.x:F0} × {panelRes.y:F0})");
        GUILayout.Label($"UV Calibrado: ({_lastHitUV.x:F3}, {_lastHitUV.y:F3})");
        GUILayout.Label($"Painel Coord: ({_lastPanelPosition.x:F0}, {_lastPanelPosition.y:F0})");
        GUILayout.Label($"Elemento Hover: <b>{(_lastHoveredElement != null ? _lastHoveredElement.name : "NENHUM")}</b> ({(_lastHoveredElement != null ? _lastHoveredElement.GetType().Name : "")})");
        GUILayout.Label($"Pointer Down: {(_isPointerDown ? "SIM" : "NÃO")}");
        GUILayout.EndArea();
    }
}
}
