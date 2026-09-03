using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Permite interação (clique e hover do mouse) com o monitor em World Space via Raycast no Collider 3D.
/// Converte a coordenada UV de impacto do raio para as coordenadas do painel UI Toolkit.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WorldSpaceUIInteraction : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private Camera interactionCamera;

    private Collider _collider;
    private RenderTexture _targetTexture;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (interactionCamera == null) interactionCamera = Camera.main;
    }

    private void Start()
    {
        if (uiDocument != null && uiDocument.panelSettings != null)
        {
            _targetTexture = uiDocument.panelSettings.targetTexture;
        }
    }

    private void Update()
    {
        if (_targetTexture == null || uiDocument == null || uiDocument.rootVisualElement == null) return;
        if (interactionCamera == null) interactionCamera = Camera.main;
        if (interactionCamera == null) return;

        Ray ray = interactionCamera.ScreenPointToRay(Input.mousePosition);

        if (_collider.Raycast(ray, out RaycastHit hit, 100f))
        {
            // Coordenadas UV do ponto de impacto
            Vector2 uv = hit.textureCoord;

            // Converter UV (0..1) para coordenadas do Painel UI Toolkit (invertendo o eixo Y)
            Vector2 panelPosition = new Vector2(
                uv.x * _targetTexture.width,
                (1.0f - uv.y) * _targetTexture.height
            );

            // Disparar evento de clique no elemento selecionado
            if (Input.GetMouseButtonDown(0))
            {
                var target = uiDocument.rootVisualElement.panel.Pick(panelPosition);
                if (target != null)
                {
                    using var clickEvent = ClickEvent.GetPooled();
                    clickEvent.target = target;
                    target.SendEvent(clickEvent);
                }
            }
        }
    }
}
