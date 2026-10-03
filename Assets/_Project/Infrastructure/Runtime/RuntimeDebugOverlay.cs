using System;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;
using UnityEngine;

namespace Mandato.Infrastructure
{
    /// <summary>
    /// Painel de telemetria e debug em tempo real (IMGUI).
    /// Permite inspecionar diretamente na tela do jogo todas as variáveis internas da partida:
    /// Eixo Político (coordenadas e viés), Atributos (0..100), Relacionamentos com NPCs,
    /// Perks ativos, Mês/Calendário e Fases de fluxo.
    /// Pressione F1 para abrir ou fechar.
    /// </summary>
    public class RuntimeDebugOverlay : MonoBehaviour
    {
        [Header("Configurações do Overlay")]
        [Tooltip("Se verdadeiro, o painel inicia aberto.")]
        [SerializeField] private bool isVisible = true;

        [Tooltip("Tecla para alternar a exibição do painel.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;

        [Header("Posição e Dimensões da Janela")]
        [SerializeField] private Rect windowRect = new Rect(20, 20, 380, 580);

        public static RuntimeDebugOverlay Instance { get; private set; }

        private RunStateMachine stateMachine;
        private RunCatalog catalog;
        private RunFlowCoordinator flowCoordinator;
        private RunState directRunState;
        private Func<RunState> runStateProvider;

        private readonly List<Action> customSections = new List<Action>();
        private readonly Dictionary<string, NpcDefinition> additionalNpcs = new Dictionary<string, NpcDefinition>(StringComparer.OrdinalIgnoreCase);

        private Vector2 mainScrollPos;
        private Vector2 npcScrollPos;
        private GUIStyle headerStyle;
        private GUIStyle sectionStyle;
        private GUIStyle valueStyle;
        private GUIStyle badgeStyle;
        private bool stylesInitialized = false;

        public bool IsVisible
        {
            get => isVisible;
            set => isVisible = value;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Localiza ou cria em tempo de execução o painel de debug na cena atual.
        /// </summary>
        public static RuntimeDebugOverlay EnsureExists()
        {
            if (Instance != null) return Instance;

            var existing = FindFirstObjectByType<RuntimeDebugOverlay>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            var go = new GameObject("RuntimeDebugOverlay");
            Instance = go.AddComponent<RuntimeDebugOverlay>();
            return Instance;
        }

        public void Initialize(
            RunStateMachine stateMachine,
            RunCatalog catalog,
            RunFlowCoordinator flowCoordinator = null,
            KeyCode toggleKey = KeyCode.F1)
        {
            this.stateMachine = stateMachine;
            this.catalog = catalog;
            this.flowCoordinator = flowCoordinator;
            this.toggleKey = toggleKey;
        }

        public void SetDirectRunState(RunState state, RunCatalog catalog = null)
        {
            this.directRunState = state;
            if (catalog != null) this.catalog = catalog;
        }

        public void SetRunStateProvider(Func<RunState> provider)
        {
            this.runStateProvider = provider;
        }

        public void RegisterCustomSection(Action sectionDrawer)
        {
            if (sectionDrawer != null && !customSections.Contains(sectionDrawer))
            {
                customSections.Add(sectionDrawer);
            }
        }

        public void UnregisterCustomSection(Action sectionDrawer)
        {
            if (sectionDrawer != null)
            {
                customSections.Remove(sectionDrawer);
            }
        }

        public void RegisterNpc(NpcDefinition npc)
        {
            if (npc != null)
            {
                string id = !string.IsNullOrEmpty(npc.id) ? npc.id : npc.name;
                if (!string.IsNullOrEmpty(id))
                {
                    additionalNpcs[id] = npc;
                }
            }
        }

        public void RegisterNpcs(IEnumerable<NpcDefinition> npcs)
        {
            if (npcs == null) return;
            foreach (var npc in npcs)
            {
                RegisterNpc(npc);
            }
        }

        public RunState GetCurrentRunState()
        {
            if (runStateProvider != null) return runStateProvider();
            if (directRunState != null) return directRunState;
            if (stateMachine != null) return stateMachine.RunState;
            if (MandatoBootstrap.Instance != null && MandatoBootstrap.Instance.StateMachine != null)
            {
                return MandatoBootstrap.Instance.StateMachine.RunState;
            }
            return null;
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                isVisible = !isVisible;
            }

            // Auto-conexão de fallback se inicializado tardiamente
            if (stateMachine == null && MandatoBootstrap.Instance != null)
            {
                stateMachine = MandatoBootstrap.Instance.StateMachine;
                catalog = MandatoBootstrap.Instance.Catalog;
            }
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (!isVisible)
            {
                // Botão pill discreto no canto para reabrir
                if (GUI.Button(new Rect(16, 16, 120, 30), $"🐞 Debug ({toggleKey})"))
                {
                    isVisible = true;
                }
                return;
            }

            windowRect = GUI.Window(98765, windowRect, DrawWindowContent, $"🐞 MANDATO — TELEMETRIA DE DEBUG ({toggleKey})");
        }

        private void EnsureStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                normal = { textColor = new Color(0.4f, 0.9f, 1f) }
            };

            sectionStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(6, 6, 6, 6),
                margin = new RectOffset(0, 0, 4, 4)
            };

            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                richText = true
            };

            badgeStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold
            };

            stylesInitialized = true;
        }

        private void DrawWindowContent(int windowId)
        {
            // Barra de título arrastável e botão de fechar
            GUILayout.BeginHorizontal();
            GUILayout.Label("<color=#70d6ff><b>Painel de Inspeção de Estado</b></color>", valueStyle);
            if (GUILayout.Button("✕ Fechar", GUILayout.Width(70)))
            {
                isVisible = false;
            }
            GUILayout.EndHorizontal();

            var run = GetCurrentRunState();
            if (run == null)
            {
                GUILayout.Label("<color=#ffaa00>Aguardando inicialização da RunState...</color>", valueStyle);
                GUI.DragWindow(new Rect(0, 0, 10000, 25));
                return;
            }

            mainScrollPos = GUILayout.BeginScrollView(mainScrollPos, GUILayout.Height(510));

            // 1. SEÇÕES CUSTOMIZADAS REGISTRADAS (Ex: Jantar / Festa Corporativa)
            if (customSections.Count > 0)
            {
                for (int i = 0; i < customSections.Count; i++)
                {
                    try
                    {
                        customSections[i]?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        GUILayout.Label($"<color=red>Erro ao renderizar seção customizada: {ex.Message}</color>", valueStyle);
                    }
                }
            }

            // 2. MÊS, FASE E CONTEXTO
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("📅 MÊS & ESTADO DO FLUXO", headerStyle);
            string phaseName = stateMachine != null ? stateMachine.CurrentPhase.ToString() : "Standalone / Evento";
            string dateText = run.calendar != null ? $"{run.calendar.DisplayDate} (Mês {run.calendar.currentMonthIndex}/{RunCalendar.DefaultTotalMonths})" : "N/A";
            string cardTitle = stateMachine?.CurrentCard != null ? stateMachine.CurrentCard.title : "Nenhuma";
            string activeChar = !string.IsNullOrEmpty(run.activeCharacterId) ? run.activeCharacterId : "Padrão";

            GUILayout.Label($"<b>Data:</b> {dateText}  |  <b>Personagem:</b> {activeChar}", valueStyle);
            GUILayout.Label($"<b>Fase Lógica:</b> <color=#ffd166>{phaseName}</color>  |  <b>Proposta Atual:</b> {cardTitle}", valueStyle);

            string scheduledNext = !string.IsNullOrEmpty(run.scheduledEventId) ? $"<color=#06d6a0>{run.scheduledEventId}</color>" : "Nenhum";
            int curMonth = run.calendar != null ? run.calendar.currentMonthIndex : 1;
            string monthSched = run.scheduledEventsByMonth != null && run.scheduledEventsByMonth.TryGetValue(curMonth, out var ms) ? $"<color=#06d6a0>{ms}</color>" : "Nenhum";
            GUILayout.Label($"<b>Evento (Mês {curMonth}):</b> {monthSched}  |  <b>Próximo Turno:</b> {scheduledNext}", valueStyle);

            if (Application.isEditor && MandatoBootstrap.Instance != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("🍸 Agendar Festa (Próx. Turno)"))
                {
                    run.ScheduleEvent("FestaCorporativa");
                }
                if (GUILayout.Button($"📅 Agendar Festa (Mês {curMonth + 1})"))
                {
                    run.ScheduleEventForMonth("FestaCorporativa", curMonth + 1);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            // 3. EIXO POLÍTICO (POLITICAL COMPASS)
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("⚖️ EIXO POLÍTICO (COMPASS)", headerStyle);
            var pol = run.politicalAxis;
            if (pol != null)
            {
                string lockStr = pol.isLocked ? "<color=#ff5555>[TRAVADO]</color>" : "<color=#55ff55>[DESTRANCADO]</color>";
                string quadStr = $"<color=#06d6a0><b>{pol.Quadrant}</b></color>";

                GUILayout.Label($"<b>Coordenadas:</b> X: <b>{pol.x:+0;-0;0}</b> (Esq/Dir)  |  Y: <b>{pol.y:+0;-0;0}</b> (Lib/Aut)", valueStyle);
                GUILayout.Label($"<b>Quadrante:</b> {quadStr}  |  <b>Status:</b> {lockStr}", valueStyle);

                // Mini barra de progresso horizontal para X (-10..+10)
                float normX = Mathf.InverseLerp(PoliticalAxis.MinValue, PoliticalAxis.MaxValue, pol.x);
                GUILayout.HorizontalSlider(normX, 0f, 1f);
            }
            GUILayout.EndVertical();

            // 4. ATRIBUTOS GOVERNAMENTAIS (0..100)
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("📊 ATRIBUTOS (0..100)", headerStyle);
            var stats = run.stats;
            if (stats != null)
            {
                GUILayout.Label($"🌿 <b>Clima:</b> {FormatStat(stats.climaticChanges)}    |    💰 <b>Economia:</b> {FormatStat(stats.economy)}", valueStyle);
                GUILayout.Label($"🌐 <b>Relações Ext:</b> {FormatStat(stats.internationalRelations)}    |    👥 <b>Aprovação:</b> {FormatStat(stats.popularApproval)}", valueStyle);
                GUILayout.Label($"🚨 <b>Corrupção:</b> {FormatCorruption(stats.corruption)}", valueStyle);
            }
            GUILayout.EndVertical();

            // 5. RELACIONAMENTOS COM NPCs E VIÉS POLÍTICO
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("🤝 RELACIONAMENTOS COM NPCs & VIÉS POLÍTICO", headerStyle);

            npcScrollPos = GUILayout.BeginScrollView(npcScrollPos, GUILayout.Height(130));

            // Agrupa todos os NPCs registrados no catálogo, adicionais ou encontrados no RunState
            var allNpcKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (catalog?.Npcs != null)
            {
                foreach (var k in catalog.Npcs.Keys) allNpcKeys.Add(k);
            }
            foreach (var k in additionalNpcs.Keys)
            {
                allNpcKeys.Add(k);
            }
            if (run.npcStates != null)
            {
                foreach (var k in run.npcStates.Keys) allNpcKeys.Add(k);
            }

            if (allNpcKeys.Count == 0)
            {
                GUILayout.Label("<i>Nenhum NPC registrado no momento.</i>", valueStyle);
            }
            else
            {
                foreach (var npcKey in allNpcKeys)
                {
                    int relScore = run.GetNpcRelation(npcKey);
                    NpcDefinition npcDef = null;
                    if (catalog?.Npcs != null && catalog.Npcs.TryGetValue(npcKey, out var foundCat))
                    {
                        npcDef = foundCat;
                    }
                    else if (additionalNpcs.TryGetValue(npcKey, out var foundAdd))
                    {
                        npcDef = foundAdd;
                    }

                    string displayName = npcDef != null && !string.IsNullOrEmpty(npcDef.displayName)
                        ? npcDef.displayName
                        : npcKey;

                    string biasStr = npcDef != null
                        ? $"<color=#88ccff>[Viés: X:{npcDef.politicalBiasX:+0;-0;0}, Y:{npcDef.politicalBiasY:+0;-0;0}]</color>"
                        : "<color=#888888>[Sem Viés]</color>";

                    string relColor = relScore > 0 ? "#55ff55" : (relScore < 0 ? "#ff5555" : "#cccccc");
                    string relText = $"<color={relColor}><b>{relScore:+0;-0;0}</b></color>";

                    bool isMet = run.npcStates != null && run.npcStates.TryGetValue(npcKey, out var st) && st.isMet;
                    string metTag = isMet ? "✓" : "•";

                    GUILayout.Label($"{metTag} <b>{displayName}</b>: {relText}  {biasStr}", valueStyle);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            // 6. PERKS ATIVOS
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("🎖️ PERKS ATIVOS", headerStyle);
            if (run.activePerkIds == null || run.activePerkIds.Count == 0)
            {
                GUILayout.Label("<i>Nenhum perk ativo.</i>", valueStyle);
            }
            else
            {
                string perksList = string.Join(", ", run.activePerkIds);
                GUILayout.Label($"<b>Perks ({run.activePerkIds.Count}):</b> {perksList}", valueStyle);
            }
            GUILayout.EndVertical();

            GUILayout.EndScrollView();

            // Torna a janela arrastável pela barra de cabeçalho
            GUI.DragWindow(new Rect(0, 0, 10000, 25));
        }

        private string FormatStat(int val)
        {
            string color = val <= 20 ? "#ff4d4d" : (val <= 40 ? "#ffa64d" : "#5cd65c");
            return $"<color={color}><b>{val}</b></color>";
        }

        private string FormatCorruption(int val)
        {
            string color = val >= 75 ? "#ff4d4d" : (val >= 50 ? "#ffa64d" : "#5cd65c");
            return $"<color={color}><b>{val}</b>/100</color>";
        }
    }
}
