using Mandato.Content;
using UnityEngine;

/// <summary>
/// Marcador de posição de convidado na cena da Festa Corporativa.
/// Cada slot é um Transform filho de NpcSlots na hierarchy.
/// O PartyEventController atribui um NPC a cada slot disponível.
/// </summary>
public class PartyGuestSlot : MonoBehaviour
{
    public NpcDefinition AssignedNpc  { get; private set; }
    public GameObject   NpcInstance  { get; private set; }
    public bool IsOccupied => AssignedNpc != null;

    public void Assign(NpcDefinition def, GameObject instance)
    {
        AssignedNpc = def;
        NpcInstance = instance;

        if (instance != null)
        {
            // Redireciona cliques em colliders do prefab para este slot
            var colliders = instance.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                var relay = col.gameObject.GetComponent<PartyNpcClickRelay>() ?? col.gameObject.AddComponent<PartyNpcClickRelay>();
                relay.Setup(this);
            }
        }
    }

    public void Clear()
    {
        if (NpcInstance != null)
            Destroy(NpcInstance);
        AssignedNpc = null;
        NpcInstance = null;
    }

    private void OnMouseDown()
    {
        TriggerClick();
    }

    public void TriggerClick()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        if (IsOccupied)
        {
            var controller = FindFirstObjectByType<PartyEventController>();
            controller?.OnNpcClicked(this);
        }
    }

    private void OnDestroy()
    {
        // Garante limpeza do modelo 3D se o slot for destruído junto com a cena.
        if (NpcInstance != null)
            Destroy(NpcInstance);
    }
}

/// <summary>
/// Helper anexado aos colliders do modelo 3D para repassar o clique ao slot pai.
/// </summary>
public class PartyNpcClickRelay : MonoBehaviour
{
    private PartyGuestSlot targetSlot;
    public void Setup(PartyGuestSlot slot) => targetSlot = slot;

    private void OnMouseDown()
    {
        if (targetSlot != null)
            targetSlot.TriggerClick();
    }
}
