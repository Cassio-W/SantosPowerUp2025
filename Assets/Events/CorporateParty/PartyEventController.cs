using System;
using System.Collections;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Infrastructure;
using Mandato.Presentation;
using Mandato.Run;
using Mandato.UI;
using UnityEngine;

/// <summary>
/// Orquestra toda a gameplay do evento Festa Corporativa.
/// Implementa IPartyEventController para ser localizado pelo CorporatePartyLauncher
/// via interface, sem referência circular entre assemblies.
///
/// Estados internos do loop da festa:
///   Roaming      → aguardando o jogador clicar em um NPC ou abrir o celular
///   TalkingToNpc → NpcConversationFlow ativo
///   PhoneOpen    → FlipPhone aberto (interações com NPC pausadas)
///   Finished     → evento encerrado, resultado enviado
/// </summary>
public class PartyEventController : MonoBehaviour, IPartyEventController
{
    private enum PartyState { Roaming, TalkingToNpc, PhoneOpen, Finished }

    // ─── Referências de cena (arraste no Inspector) ───────────────────────────
    [Header("Componentes da Cena")]
    [SerializeField] private PartyHudPresenter hudPresenter;
    [SerializeField] private NpcConversationFlow conversationFlow;
    [SerializeField] private FlipPhonePresenter flipPhonePresenter;
    [SerializeField] private PartyGuestSlot[] guestSlots;

    [Header("Debug / Teste Isolado (Standalone)")]
    [Tooltip("Se verdadeiro, permite dar Play diretamente nesta cena no Editor sem precisar iniciar o jogo inteiro.")]
    [SerializeField] private bool autoInitializeInEditor = true;
    [Tooltip("Definição de evento usada ao rodar no modo de teste isolado.")]
    [SerializeField] private CorporatePartyEventDefinition debugEventDefinition;

    // ─── Estado da instância atual da festa ──────────────────────────────────
    private RunState runState;
    private CorporatePartyEventDefinition definition;
    private Action<InteractiveEventResult> onCompleted;

    private PartyState currentState = PartyState.Roaming;
    private int remainingInteractions;

    private void Start()
    {
        // Se a cena foi iniciada diretamente no Editor sem passar pelo CorporatePartyLauncher
        if (runState == null && autoInitializeInEditor && Application.isEditor)
        {
            if (debugEventDefinition != null)
            {
                Debug.Log("[PartyEventController] Modo Standalone ativado: Inicializando festa para teste isolado.");
                var mockState = new RunState();
                Initialize(mockState, debugEventDefinition, result =>
                {
                    Debug.Log($"[PartyEventController] [TESTE STANDALONE FINALIZADO] Evento concluído: {result.wasCompleted}");
                    if (result.npcRelationDeltas != null)
                    {
                        foreach (var kvp in result.npcRelationDeltas)
                            Debug.Log($"  • NPC '{kvp.Key}': Delta {kvp.Value}");
                    }
                });
            }
            else
            {
                Debug.LogWarning("[PartyEventController] Para testar esta cena diretamente no Editor, arraste um 'CorporatePartyEventDefinition' no campo 'Debug Event Definition' do Inspector.");
            }
        }
    }

    // npcId → conjunto de abordagens já usadas com esse NPC nesta instância
    private readonly Dictionary<string, HashSet<ApproachStyle>> usedApproaches
        = new Dictionary<string, HashSet<ApproachStyle>>(StringComparer.OrdinalIgnoreCase);

    // npcId → delta acumulado de relação nesta festa
    private readonly Dictionary<string, int> accumulatedRelationDeltas
        = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    // NPC em foco para ações do celular
    private NpcDefinition npcInPhoneFocus;

    // ─── IPartyEventController ────────────────────────────────────────────────

    /// <summary>
    /// Ponto de entrada chamado pelo CorporatePartyLauncher após carregar a cena.
    /// </summary>
    public void Initialize(
        RunState state,
        CorporatePartyEventDefinition def,
        Action<InteractiveEventResult> completionCallback)
    {
        runState    = state;
        definition  = def;
        onCompleted = completionCallback;

        remainingInteractions = def.GetInteractionLimit(state.activePerkIds);

        conversationFlow?.SetHud(hudPresenter);
        hudPresenter?.SetupEnergyBar(remainingInteractions);

        SpawnGuests();
        BindFlipPhone();

        if (hudPresenter != null)
        {
            hudPresenter.OnPartyEndRequested += EndParty;
            hudPresenter.OnConversationCancelled += CancelCurrentConversation;
        }

        StartCoroutine(PartyLoop());
    }

    // ─── Spawn dos convidados ─────────────────────────────────────────────────

    private void SpawnGuests()
    {
        if (definition == null || definition.guestPool == null || guestSlots == null) return;

        var pool = new List<NpcDefinition>(definition.guestPool);

        // Remove NPCs indisponíveis (mortos, presos, etc.)
        pool.RemoveAll(npc =>
        {
            if (npc == null) return true;
            string id = !string.IsNullOrEmpty(npc.id) ? npc.id : npc.name;
            return !runState.IsNpcAvailable(id);
        });

        int count = Mathf.Clamp(
            UnityEngine.Random.Range(definition.minGuests, definition.maxGuests + 1),
            0,
            Mathf.Min(pool.Count, guestSlots.Length));

        Shuffle(pool);

        for (int i = 0; i < count; i++)
        {
            var slot   = guestSlots[i];
            var npcDef = pool[i];

            GameObject instance = null;
            if (npcDef.prefab != null)
            {
                instance = Instantiate(npcDef.prefab, slot.transform.position, slot.transform.rotation);

                // Desativa comportamentos de proposta/gabinete dos prefabs de NPC
                var presentationNpc = instance.GetComponent<INpcController>();
                if (presentationNpc is MonoBehaviour mb)
                    mb.enabled = false;

                var npcPresentation = instance.GetComponent<NpcPresentation>();
                if (npcPresentation != null)
                    npcPresentation.enabled = false;

                var navAgent = instance.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (navAgent != null)
                    navAgent.enabled = false;

                // Garante que o NPC fique em Idle estático no slot
                SetNpcIdleAnimation(instance);

                // Garante um CapsuleCollider generoso cobrindo todo o corpo do NPC para clique fácil
                var col = instance.GetComponent<CapsuleCollider>();
                if (col == null)
                {
                    col = instance.AddComponent<CapsuleCollider>();
                }
                col.center = new Vector3(0f, 0.9f, 0f);
                col.radius = 0.5f;
                col.height = 1.9f;
                col.isTrigger = false;
            }

            slot.Assign(npcDef, instance);
        }
    }

    private void Update()
    {
        // Se estiver em conversa com um NPC, tecla ESC cancela o diálogo
        if (currentState == PartyState.TalkingToNpc)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelCurrentConversation();
                return;
            }
        }

        if (currentState != PartyState.Roaming) return;

        if (Input.GetMouseButtonDown(0))
        {
            HandleNpcClickRaycast();
        }
    }

    /// <summary>
    /// Cancela a conversa ativa com o NPC e retorna a festa para o estado de Roaming.
    /// Não consome tentativas de conversa / ânimo.
    /// </summary>
    public void CancelCurrentConversation()
    {
        if (currentState != PartyState.TalkingToNpc) return;

        conversationFlow?.Cancel();
        npcInPhoneFocus = null;
        currentState = PartyState.Roaming;
    }

    private void HandleNpcClickRaycast()
    {
        var cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);

        PartyGuestSlot closestSlot = null;
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            var slot = hit.collider.GetComponentInParent<PartyGuestSlot>();
            if (slot != null && slot.IsOccupied && hit.distance < closestDist)
            {
                closestDist = hit.distance;
                closestSlot = slot;
            }
        }

        if (closestSlot != null)
        {
            OnNpcClicked(closestSlot);
        }
    }

    private static readonly string[] IdleStateCandidates = { "Idle01", "Idle02", "Idle03", "IdleW", "Idle" };

    private void SetNpcIdleAnimation(GameObject instance)
    {
        if (instance == null) return;
        var animator = instance.GetComponent<Animator>() ?? instance.GetComponentInChildren<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null) return;

        var validIdles = new List<string>();
        foreach (var state in IdleStateCandidates)
        {
            if (animator.HasState(0, Animator.StringToHash(state)) ||
                animator.HasState(0, Animator.StringToHash("Base Layer." + state)))
            {
                validIdles.Add(state);
            }
        }

        string chosen = validIdles.Count > 0
            ? validIdles[UnityEngine.Random.Range(0, validIdles.Count)]
            : "Idle01";

        animator.Play(chosen, 0, UnityEngine.Random.Range(0f, 1f));
        animator.Update(0f);
    }

    // ─── Loop principal ───────────────────────────────────────────────────────

    private IEnumerator PartyLoop()
    {
        while (currentState != PartyState.Finished)
        {
            if (remainingInteractions <= 0)
            {
                EndParty();
                yield break;
            }
            yield return null;
        }
    }

    // ─── Interação com NPCs ───────────────────────────────────────────────────

    /// <summary>
    /// Chamado por clique no NPC (via raycast ou botão 3D na cena).
    /// Inicia o fluxo de conversa se o estado permitir.
    /// </summary>
    public void OnNpcClicked(PartyGuestSlot slot)
    {
        if (currentState != PartyState.Roaming) return;
        if (slot == null || !slot.IsOccupied) return;

        var npc   = slot.AssignedNpc;
        string npcId = !string.IsNullOrEmpty(npc.id) ? npc.id : npc.name;

        if (!usedApproaches.TryGetValue(npcId, out var used))
        {
            used = new HashSet<ApproachStyle>();
            usedApproaches[npcId] = used;
        }

        if (used.Count >= 4)
        {
            Debug.Log($"[PartyEventController] Todas as abordagens com {npc.displayName} já foram usadas.");
            return;
        }

        currentState    = PartyState.TalkingToNpc;
        npcInPhoneFocus = npc;
        RefreshFlipPhoneForNpc(npc);

        conversationFlow?.Begin(npc, used, OnConversationFinished);
    }

    private void OnConversationFinished(string npcId, ApproachStyle approach, int delta)
    {
        if (!usedApproaches.TryGetValue(npcId, out var used))
        {
            used = new HashSet<ApproachStyle>();
            usedApproaches[npcId] = used;
        }
        used.Add(approach);

        if (!accumulatedRelationDeltas.ContainsKey(npcId))
            accumulatedRelationDeltas[npcId] = 0;
        accumulatedRelationDeltas[npcId] += delta;

        remainingInteractions--;
        hudPresenter?.ConsumeInteraction();

        npcInPhoneFocus = null;
        currentState    = PartyState.Roaming;

        if (remainingInteractions <= 0)
            EndParty();
    }

    // ─── Integração com o Flip-Phone ─────────────────────────────────────────

    private void BindFlipPhone()
    {
        if (flipPhonePresenter == null) return;

        flipPhonePresenter.OnActionRequested += OnPhoneActionRequested;
        flipPhonePresenter.OnPhoneOpened     += () => currentState = PartyState.PhoneOpen;
        flipPhonePresenter.OnPhoneClosed     += () =>
        {
            if (currentState == PartyState.PhoneOpen)
                currentState = PartyState.Roaming;
        };
    }

    private void RefreshFlipPhoneForNpc(NpcDefinition npc)
    {
        if (flipPhonePresenter == null) return;

        // Constrói lista de ações do celular filtradas ao NPC em conversa.
        // Expansível: buscar FlipPhoneActionDefinitions do catálogo filtradas por linkedNpcId.
        var viewModels = new List<FlipPhoneActionViewModel>();
        flipPhonePresenter.Refresh(viewModels);
    }

    private void OnPhoneActionRequested(string actionId)
    {
        Debug.Log($"[PartyEventController] Ação do celular solicitada: '{actionId}' (NPC: {npcInPhoneFocus?.displayName ?? "nenhum"})");
        // Efeitos concretos (prisão, etc.) são registrados no resultado e aplicados pelo RunFlowCoordinator.
    }

    // ─── Encerramento da festa ────────────────────────────────────────────────

    private void EndParty()
    {
        if (currentState == PartyState.Finished) return;
        currentState = PartyState.Finished;

        if (hudPresenter != null)
            hudPresenter.OnPartyEndRequested -= EndParty;

        flipPhonePresenter?.Close();

        var result = new InteractiveEventResult
        {
            eventId           = definition != null ? definition.eventId : string.Empty,
            wasCompleted      = true,
            npcRelationDeltas = accumulatedRelationDeltas
        };

        onCompleted?.Invoke(result);
    }

    // ─── Utilitários ──────────────────────────────────────────────────────────

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void OnDestroy()
    {
        if (hudPresenter != null)
        {
            hudPresenter.OnPartyEndRequested -= EndParty;
            hudPresenter.OnConversationCancelled -= CancelCurrentConversation;
        }

        if (flipPhonePresenter != null)
            flipPhonePresenter.OnActionRequested -= OnPhoneActionRequested;
    }
}
