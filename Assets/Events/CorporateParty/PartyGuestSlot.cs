using Mandato.Content;
using Mandato.Presentation;
using UnityEngine;

/// <summary>
/// Marcador de posição de convidado na cena da Festa Corporativa.
/// Cada slot é um Transform filho de NpcSlots na hierarchy.
/// O PartyEventController atribui um NPC a cada slot disponível.
/// Integra-se diretamente com o CameraFocusManager através de FocusableObject.
/// </summary>
public class PartyGuestSlot : MonoBehaviour
{
    [Header("Câmera de Foco")]
    [Tooltip("Transform que define a posição/rotação da câmera ao focar este convidado. Se vazio, gera um ponto ideal automaticamente.")]
    [SerializeField] private Transform cameraFocusPoint;

    public NpcDefinition AssignedNpc  { get; private set; }
    public GameObject   NpcInstance  { get; private set; }
    public FocusableObject Focusable  { get; private set; }
    public bool IsOccupied => AssignedNpc != null;
    public Transform CameraFocusPoint => cameraFocusPoint != null ? cameraFocusPoint : EnsureDefaultFocusPoint();

    private void Awake()
    {
        EnsureFocusable();
    }

    public FocusableObject EnsureFocusable()
    {
        if (Focusable == null)
        {
            Focusable = GetComponent<FocusableObject>();
            if (Focusable == null)
            {
                Focusable = gameObject.AddComponent<FocusableObject>();
            }
        }

        Focusable.CameraFocusPoint = CameraFocusPoint;
        Focusable.TargetCameraFov = 45f;
        Focusable.EnableHoverScale = false;
        Focusable.EnableHoverHop = false;
        Focusable.EnableOutlineHighlight = true;
        Focusable.HighlightOutlineColor = new Color(1f, 0.85f, 0.25f, 1f);
        Focusable.AllowClickToFocus = true;
        Focusable.UnfocusOnSecondClick = false;
        Focusable.AllowUnfocusOnClickOutside = true;

        var col = GetComponent<CapsuleCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<CapsuleCollider>();
        }
        col.center = new Vector3(0f, 0.95f, 0f);
        col.radius = 0.55f;
        col.height = 2.0f;
        col.isTrigger = false;
        col.enabled = IsOccupied;

        return Focusable;
    }

    public void Assign(NpcDefinition def, GameObject instance)
    {
        AssignedNpc = def;
        NpcInstance = instance;

        EnsureFocusable();

        if (instance != null)
        {
            // Vincula o modelo instanciado como filho direto do slot
            instance.transform.SetParent(transform, true);

            // Desativa colliders internos do NPC instanciado para o collider do slot centralizar todo o raycast
            foreach (var c in instance.GetComponentsInChildren<Collider>(true))
            {
                if (c != null && c.gameObject != gameObject)
                {
                    c.enabled = false;
                }
            }

            // Alimenta os renderers do modelo para o highlight de ToonOutline
            Focusable.TargetRenderers.Clear();
            instance.GetComponentsInChildren(true, Focusable.TargetRenderers);

            Focusable.enabled = true;
            if (TryGetComponent<Collider>(out var col))
            {
                col.enabled = true;
            }
        }
        else
        {
            Focusable.enabled = false;
            if (TryGetComponent<Collider>(out var col))
            {
                col.enabled = false;
            }
        }
    }

    public Transform EnsureDefaultFocusPoint()
    {
        if (cameraFocusPoint != null) return cameraFocusPoint;

        // Procura filho existente com nome 'camera' ou 'focus'
        foreach (Transform child in transform)
        {
            if (child.name.IndexOf("camera", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name.IndexOf("focus", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                cameraFocusPoint = child;
                return cameraFocusPoint;
            }
        }

        // Cria ponto de foco procedural em frente ao convidado
        var go = new GameObject("FocusPoint");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 1.55f, 1.8f);
        go.transform.localRotation = Quaternion.Euler(4f, 180f, 0f);
        cameraFocusPoint = go.transform;
        return cameraFocusPoint;
    }

    public void Clear()
    {
        if (NpcInstance != null)
            Destroy(NpcInstance);
        AssignedNpc = null;
        NpcInstance = null;

        if (Focusable != null)
        {
            Focusable.TargetRenderers.Clear();
            Focusable.enabled = false;
        }

        if (TryGetComponent<Collider>(out var col))
        {
            col.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (NpcInstance != null)
            Destroy(NpcInstance);
    }
}
