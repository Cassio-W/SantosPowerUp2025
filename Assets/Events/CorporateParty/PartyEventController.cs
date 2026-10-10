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
        // Se a cena foi iniciada diretamente no Editor sem passar pelo CorporatePartyLauncher (modo Standalone)
        bool isStandalonePlay = UnityEngine.SceneManagement.SceneManager.sceneCount == 1
            && MandatoBootstrap.Instance == null;

        if (runState == null && autoInitializeInEditor && Application.isEditor && isStandalonePlay)
        {
            if (debugEventDefinition != null)
            {
                Debug.Log("[PartyEventController] Modo Standalone ativado: Inicializando festa para teste isolado.");
                var mockState = new RunState();
                mockState.stats.economy = 55;
                mockState.stats.climaticChanges = 48;
                mockState.stats.internationalRelations = 62;
                mockState.stats.popularApproval = 50;
                mockState.stats.corruption = 20;
                mockState.politicalAxis.x = 1;
                mockState.politicalAxis.y = -2;

                if (debugEventDefinition.guestPool != null)
                {
                    foreach (var npc in debugEventDefinition.guestPool)
                    {
                        if (npc != null)
                        {
                            string id = !string.IsNullOrEmpty(npc.id) ? npc.id : npc.name;
                            mockState.GetOrCreateNpcState(id);
                        }
                    }
                }

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

        remainingInteractions = def != null ? def.GetInteractionLimit(state?.activePerkIds) : 4;
        Debug.Log($"[PartyEventController] Inicializando festa corporativa. Limite de ânimo/interações: {remainingInteractions}");

        conversationFlow?.SetHud(hudPresenter);
        hudPresenter?.SetupEnergyBar(remainingInteractions);

        SpawnGuests();
        BindFlipPhone();
        BindCameraFocus();
        ConnectDebugOverlay();

        if (hudPresenter != null)
        {
            hudPresenter.OnPartyEndRequested -= EndParty;
            hudPresenter.OnPartyEndRequested += EndParty;
            hudPresenter.OnConversationCancelled -= CancelCurrentConversation;
            hudPresenter.OnConversationCancelled += CancelCurrentConversation;
        }

        StartCoroutine(PartyLoop());
    }

    // ─── Spawn dos convidados ─────────────────────────────────────────────────

    private void SpawnGuests()
    {
        if (definition == null || definition.guestPool == null || guestSlots == null) return;

        // Limpa slots anteriores para evitar duplicatas em reinicializações
        foreach (var s in guestSlots)
        {
            if (s != null)
                s.Clear();
        }

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
                instance.transform.SetParent(slot.transform, true);

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
            }

            slot.Assign(npcDef, instance);
        }
    }

    private void Update()
    {
        // Se estiver em conversa com um NPC, tecla ESC cancela o diálogo e desfaz o foco
        if (currentState == PartyState.TalkingToNpc)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelCurrentConversation(revertCamera: true);
                return;
            }
        }
        else if (currentState == PartyState.Roaming)
        {
            // Fallback de clique direto no NPC caso o jogador clique diretamente na mesa
            if (Input.GetMouseButtonDown(0))
            {
                HandleRoamingClickFallback();
            }
        }
    }

    private void HandleRoamingClickFallback()
    {
        var cam = Camera.main;
        if (cam == null) return;

        // Se o cursor estiver sobre elementos interativos de tela da UI, ignora
        if (CameraFocusManager.Instance != null && CameraFocusManager.Instance.IsPointerOverInteractiveUI())
            return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;

            PartyGuestSlot slot = hit.collider.GetComponentInParent<PartyGuestSlot>();
            if (slot == null && guestSlots != null)
            {
                foreach (var s in guestSlots)
                {
                    if (s != null && s.IsOccupied && (hit.collider.transform == s.transform || hit.collider.transform.IsChildOf(s.transform)))
                    {
                        slot = s;
                        break;
                    }
                }
            }

            if (slot != null && slot.IsOccupied)
            {
                OnNpcClicked(slot);
                break;
            }
        }
    }

    private void BindCameraFocus()
    {
        var focusMgr = CameraFocusManager.Instance;
        if (focusMgr != null)
        {
            focusMgr.OnObjectFocusChanged += HandleObjectFocusChanged;
        }
    }

    private void UnbindCameraFocus()
    {
        var focusMgr = CameraFocusManager.Instance;
        if (focusMgr != null)
        {
            focusMgr.OnObjectFocusChanged -= HandleObjectFocusChanged;
        }
    }

    private void HandleObjectFocusChanged(FocusableObject focusedObj)
    {
        if (currentState == PartyState.Finished) return;

        if (focusedObj != null)
        {
            PartyGuestSlot targetSlot = null;
            if (guestSlots != null)
            {
                foreach (var slot in guestSlots)
                {
                    if (slot != null && (slot.Focusable == focusedObj || (slot.NpcInstance != null && focusedObj.transform.IsChildOf(slot.transform))))
                    {
                        targetSlot = slot;
                        break;
                    }
                }
            }

            if (targetSlot != null && targetSlot.IsOccupied)
            {
                if (currentState == PartyState.Roaming)
                {
                    OnNpcFocused(targetSlot);
                }
            }
        }
        else
        {
            if (currentState == PartyState.TalkingToNpc)
            {
                CancelCurrentConversation(revertCamera: false);
            }
        }
    }

    /// <summary>
    /// Cancela a conversa ativa com o NPC e retorna a festa para o estado de Roaming.
    /// Não consome tentativas de conversa / ânimo.
    /// </summary>
    public void CancelCurrentConversation() => CancelCurrentConversation(revertCamera: true);

    public void CancelCurrentConversation(bool revertCamera)
    {
        if (currentState != PartyState.TalkingToNpc) return;

        conversationFlow?.Cancel();
        npcInPhoneFocus = null;
        currentState = PartyState.Roaming;

        if (revertCamera && CameraFocusManager.Instance != null && CameraFocusManager.Instance.HasActiveFocus)
        {
            CameraFocusManager.Instance.Unfocus();
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
    /// Chamado programaticamente ou por clique direto: foca o NPC via CameraFocusManager.
    /// </summary>
    public void OnNpcClicked(PartyGuestSlot slot)
    {
        if (slot != null && slot.Focusable != null)
        {
            CameraFocusManager.Instance?.Focus(slot.Focusable);
        }
        else
        {
            OnNpcFocused(slot);
        }
    }

    /// <summary>
    /// Chamado quando a câmera conclui ou estabelece foco no NPC do slot.
    /// Inicia o diálogo e atualiza o FlipPhone para esse NPC.
    /// </summary>
    public void OnNpcFocused(PartyGuestSlot slot)
    {
        if (currentState != PartyState.Roaming && currentState != PartyState.TalkingToNpc) return;
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
        // Retorna a câmera suavemente para a visão geral
        CameraFocusManager.Instance?.Unfocus();
        if (!usedApproaches.TryGetValue(npcId, out var used))
        {
            used = new HashSet<ApproachStyle>();
            usedApproaches[npcId] = used;
        }
        used.Add(approach);

        if (!accumulatedRelationDeltas.ContainsKey(npcId))
            accumulatedRelationDeltas[npcId] = 0;
        accumulatedRelationDeltas[npcId] += delta;

        // Em modo de teste isolado (sem RunFlowCoordinator gerenciando o retorno),
        // aplica a relação diretamente no mockState
        if (runState != null && MandatoBootstrap.Instance == null)
        {
            runState.ModifyNpcRelation(npcId, delta);
        }

        remainingInteractions--;
        Debug.Log($"[PartyEventController] Conversa concluída com '{npcId}' ({approach}). Ânimo consumido! Restante: {remainingInteractions}");
        hudPresenter?.ConsumeInteraction();

        npcInPhoneFocus = null;
        currentState    = PartyState.Roaming;

        if (remainingInteractions <= 0)
        {
            Debug.Log("[PartyEventController] Ânimo esgotado (0 interações restantes)! Encerrando festa corporativa...");
            EndParty();
        }
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

    [Header("Ações do Celular")]
    [SerializeField] private List<FlipPhoneActionDefinition> phoneActions = new List<FlipPhoneActionDefinition>();

    private void EnsurePhoneActionsLoaded()
    {
        if (phoneActions != null && phoneActions.Count > 0) return;

        phoneActions = new List<FlipPhoneActionDefinition>();

        if (MandatoBootstrap.Instance?.ActionCatalog != null)
        {
            phoneActions.AddRange(MandatoBootstrap.Instance.ActionCatalog.Values);
            return;
        }

        var loaded = Resources.LoadAll<FlipPhoneActionDefinition>("");
        if (loaded != null && loaded.Length > 0)
        {
            phoneActions.AddRange(loaded);
        }

#if UNITY_EDITOR
        if (phoneActions.Count == 0)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:FlipPhoneActionDefinition");
            foreach (var g in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                var act = UnityEditor.AssetDatabase.LoadAssetAtPath<FlipPhoneActionDefinition>(path);
                if (act != null && !phoneActions.Contains(act))
                {
                    phoneActions.Add(act);
                }
            }
        }
#endif
    }

    private void RefreshFlipPhoneForNpc(NpcDefinition npc)
    {
        if (flipPhonePresenter == null) return;
        EnsurePhoneActionsLoaded();

        var viewModels = new List<FlipPhoneActionViewModel>();
        string targetNpcId = npc != null ? (!string.IsNullOrEmpty(npc.id) ? npc.id : npc.name) : string.Empty;

        foreach (var action in phoneActions)
        {
            if (action == null) continue;

            string linkedNpc = action.GetLinkedNpcId();
            bool isNpcAvailable = string.IsNullOrEmpty(linkedNpc) || (runState != null && runState.IsNpcAvailable(linkedNpc));
            bool isConsumed = action.cooldownType == FlipPhoneCooldownType.SingleUse && (runState != null && runState.IsActionConsumed(action.id));
            bool onCooldown = runState != null && runState.IsActionOnCooldown(action.id);
            int cooldownTurns = runState != null ? runState.GetActionCooldown(action.id) : 0;
            bool conditionsMet = runState != null && action.AreConditionsMet(
                runState.stats,
                runState.calendar != null ? runState.calendar.currentMonthIndex : 1,
                runState.activePerkIds,
                runState.decisionHistory,
                targetNpcId,
                runState.GetNpcRelation
            );
            bool isUnlocked = runState != null && (runState.IsActionUnlocked(action.id) || action.unlockByDefault);

            bool isPartyApplicable = FlipPhoneResolver.IsActionApplicable(action, hasActiveCard: false, hasDeck: false, contextNpcId: targetNpcId);

            string statusText = string.Empty;
            if (!isNpcAvailable) statusText = "INDISPONÍVEL";
            else if (isConsumed) statusText = "USADO";
            else if (onCooldown) statusText = $"{cooldownTurns}T RECARGA";
            else if (!isPartyApplicable) statusText = "SÓ NO GABINETE";
            else if (!conditionsMet || !isUnlocked) statusText = "BLOQUEADO";

            var vm = new FlipPhoneActionViewModel
            {
                id = action.id,
                displayName = action.displayName,
                description = action.description,
                categoryTag = action.categoryTag,
                icon = action.icon,
                linkedNpcId = linkedNpc,
                isAvailable = isUnlocked && !isConsumed && !onCooldown && conditionsMet && isNpcAvailable && isPartyApplicable,
                isOnCooldown = onCooldown,
                cooldownTurnsRemaining = cooldownTurns,
                isConsumed = isConsumed,
                statusText = statusText
            };
            viewModels.Add(vm);
        }

        flipPhonePresenter.Refresh(viewModels);
    }

    private void OnPhoneActionRequested(string actionId)
    {
        Debug.Log($"[PartyEventController] Ação do celular solicitada: '{actionId}' (NPC em foco: {npcInPhoneFocus?.displayName ?? "nenhum"})");

        EnsurePhoneActionsLoaded();
        var actionDef = phoneActions.Find(a => string.Equals(a.id, actionId, StringComparison.OrdinalIgnoreCase));
        if (actionDef == null || runState == null) return;

        string targetNpcId = npcInPhoneFocus != null ? (!string.IsNullOrEmpty(npcInPhoneFocus.id) ? npcInPhoneFocus.id : npcInPhoneFocus.name) : null;

        var report = FlipPhoneResolver.ResolveUse(
            runState,
            deckState: null,
            actionDef,
            catalog: null,
            currentCard: null,
            perkCatalog: null,
            npcCatalog: MandatoBootstrap.Instance?.NpcCatalog,
            contextNpcId: targetNpcId
        );

        if (report != null && report.success)
        {
            Debug.Log($"[PartyEventController] Ação '{actionId}' executada com sucesso!");

            // Se a ação removeu um NPC (ex: assassinato/exoneração), limpa o slot do NPC da festa
            if (report.removedNpcIds != null && report.removedNpcIds.Count > 0)
            {
                foreach (var removedId in report.removedNpcIds)
                {
                    if (guestSlots != null)
                    {
                        foreach (var slot in guestSlots)
                        {
                            if (slot != null && slot.IsOccupied)
                            {
                                string sId = !string.IsNullOrEmpty(slot.AssignedNpc.id) ? slot.AssignedNpc.id : slot.AssignedNpc.name;
                                if (string.Equals(sId, removedId, StringComparison.OrdinalIgnoreCase))
                                {
                                    slot.Clear();
                                }
                            }
                        }
                    }
                }

                // Se o NPC focado foi removido, fecha a conversa e desfaz o foco
                if (!string.IsNullOrEmpty(targetNpcId) && report.removedNpcIds.Contains(targetNpcId))
                {
                    CancelCurrentConversation(revertCamera: true);
                }
            }

            RefreshFlipPhoneForNpc(npcInPhoneFocus);
        }
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
            eventTitle        = "Festa Corporativa",
            wasCompleted      = true,
            npcRelationDeltas = accumulatedRelationDeltas
        };

        onCompleted?.Invoke(result);
    }

    // ─── Telemetria e Debug Overlay ──────────────────────────────────────────

    private void ConnectDebugOverlay()
    {
        var overlay = RuntimeDebugOverlay.EnsureExists();
        overlay.SetDirectRunState(runState);
        if (definition != null && definition.guestPool != null)
        {
            overlay.RegisterNpcs(definition.guestPool);
        }
        overlay.RegisterCustomSection(DrawPartyDebugSection);
    }

    private void DrawPartyDebugSection()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        var headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 12,
            normal = { textColor = new Color(1f, 0.75f, 0.2f) }
        };
        GUILayout.Label("🍸 EVENTO: JANTAR / FESTA CORPORATIVA", headerStyle);

        string focusName = npcInPhoneFocus != null ? npcInPhoneFocus.displayName : "Nenhum";
        GUILayout.Label($"<b>Estado:</b> <color=#ffd166>{currentState}</color>  |  <b>Interações Restantes:</b> <color=#55ff55><b>{remainingInteractions}</b></color>");
        GUILayout.Label($"<b>Foco Atual:</b> {focusName}");

        if (accumulatedRelationDeltas != null && accumulatedRelationDeltas.Count > 0)
        {
            GUILayout.Label("<b>Deltas Acumulados Nesta Festa:</b>");
            foreach (var kvp in accumulatedRelationDeltas)
            {
                string deltaColor = kvp.Value > 0 ? "#55ff55" : (kvp.Value < 0 ? "#ff5555" : "#cccccc");
                GUILayout.Label($"  • <b>{kvp.Key}</b>: <color={deltaColor}><b>{kvp.Value:+0;-0;0}</b></color>");
            }
        }
        else
        {
            GUILayout.Label("<i>Nenhum delta acumulado ainda nesta festa.</i>");
        }

        // Ações de teste rápido no Editor
        if (Application.isEditor)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1 Interação"))
            {
                remainingInteractions++;
                hudPresenter?.SetupEnergyBar(remainingInteractions);
            }
            if (GUILayout.Button("Encerrar Festa"))
            {
                EndParty();
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
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
        if (RuntimeDebugOverlay.Instance != null)
        {
            RuntimeDebugOverlay.Instance.UnregisterCustomSection(DrawPartyDebugSection);
        }

        if (hudPresenter != null)
        {
            hudPresenter.OnPartyEndRequested -= EndParty;
            hudPresenter.OnConversationCancelled -= CancelCurrentConversation;
        }

        if (flipPhonePresenter != null)
            flipPhonePresenter.OnActionRequested -= OnPhoneActionRequested;

        UnbindCameraFocus();
    }
}
