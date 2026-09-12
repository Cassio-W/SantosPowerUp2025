using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mandato.Content;
using Mandato.Core;
using Mandato.Presentation;
using Mandato.Run;
using Mandato.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mandato.Infrastructure
{
    public class MandatoBootstrap : MonoBehaviour
    {
        public static MandatoBootstrap Instance { get; private set; }

        [Header("Tutorial e Conteúdo da Partida")]
        [Tooltip("Se verdadeiro, a partida inicia pelas propostas do tutorial.")]
        [SerializeField] private bool playTutorial = true;

        [Tooltip("Lista de propostas do tutorial (CardDefinition ou SO Deals).")]
        [SerializeField] private List<ScriptableObject> tutorialDealsOrCards = new List<ScriptableObject>();

        [Tooltip("Lista de CardDefinition ou SO Deals antigos a carregar no baralho principal.")]
        [SerializeField] private List<ScriptableObject> startingDealsOrCards = new List<ScriptableObject>();

        [Tooltip("Lista de Finais possíveis para avaliação no término do mandato.")]
        [SerializeField] private List<EndingDefinition> endingsCatalog = new List<EndingDefinition>();

        [Header("Catálogos de Modificadores & Narrativa")]
        [SerializeField] private List<PerkDefinition> perksCatalog = new List<PerkDefinition>();
        [SerializeField] private List<RunEventDefinition> eventsCatalog = new List<RunEventDefinition>();
        [SerializeField] private List<QuestDefinition> questsCatalog = new List<QuestDefinition>();

        [Header("Semente e Configurações")]
        [SerializeField] private int customSeed = 0;
        [SerializeField] private string mainMenuSceneName = "MenuV2";
        [Tooltip("Intervalo em segundos entre a saída do NPC e a entrada do próximo.")]
        [SerializeField] private float delayBetweenProposals = 1.5f;

        [Header("Controle de Fluxo entre Propostas")]
        [Tooltip("Se verdadeiro, o próximo visitante só é chamado após o jogador pressionar a tecla de chamada (Espaço).")]
        [SerializeField] private bool requireSpaceToCallNextNpc = true;
        [SerializeField] private KeyCode callNextNpcKey = KeyCode.Space;

        private bool isAwaitingSpaceForNextNpc = false;
        private float pendingProposalDelay = 1.5f;

        [Header("Animação do Jogador (Mão)")]
        [SerializeField] private Animator playerAnimator;
        [SerializeField] private string dealAnimationName = "LevantaMao";
        [SerializeField] private string dealAnimationReverseName = "";
        [SerializeField] private string defaultAnimationName = "None";

        [Header("Apresentadores (Opcionais / Auto-detectáveis)")]
        [SerializeField] private RunPresentationCoordinator presentationCoordinator;
        [SerializeField] private PaperDocumentPresenter paperPresenter;
        [SerializeField] private RetroMonitorPresenter retroMonitorPresenter;
        [SerializeField] private DecisionOverlayPresenter decisionOverlayPresenter;
        [SerializeField] private EndScreenPresenter endScreenPresenter;
        [SerializeField] private FlipPhonePresenter flipPhonePresenter;

        [Header("Flip-Phone & Ações")]
        [SerializeField] private List<FlipPhoneActionDefinition> startingActions = new List<FlipPhoneActionDefinition>();

        [Header("Configuração de UI")]
        [Tooltip("Se verdadeiro, desativa os elementos visuais do Canvas legado para rodar 100% em UI Toolkit.")]
        public bool disableLegacyCanvas = true;

        public RunStateMachine StateMachine { get; private set; }
        public RunCatalog Catalog => catalog;
        public IReadOnlyDictionary<string, CardDefinition> CardCatalog => catalog.Cards;
        public IReadOnlyDictionary<string, FlipPhoneActionDefinition> ActionCatalog => catalog.Actions;
        public IReadOnlyDictionary<string, PerkDefinition> PerkCatalog => catalog.Perks;
        public IReadOnlyDictionary<string, RunEventDefinition> EventCatalog => catalog.Events;
        public IReadOnlyDictionary<string, QuestDefinition> QuestCatalog => catalog.Quests;
        public ProfileState CurrentProfile => profileService.CurrentProfile;
        public RunProfileService ProfileService => profileService;

        private readonly RunCatalog catalog = new RunCatalog();
        private readonly RunProfileService profileService = new RunProfileService();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            AutoDetectPresenters();
            ApplyLegacyCanvasSuppression();
            profileService.InitializeProfile();
            BuildCatalog();
            InitializeStateMachine();
            BindPresenters();
        }

        private void Start()
        {
            // Inicializa a exibição visual dos indicadores no monitor
            if (retroMonitorPresenter != null && StateMachine != null)
            {
                retroMonitorPresenter.UpdateSnapshot(StateMachine.RunState.GetSnapshot());
            }

            if (decisionOverlayPresenter != null && StateMachine != null)
            {
                decisionOverlayPresenter.SetCorruptionLevel(StateMachine.RunState.stats.corruption);
            }

            SyncCanvasUI();
            SyncCameraEffects(instant: true);

            // Puxa a primeira proposta do baralho
            DrawFirstProposal();
        }

        private void Update()
        {
            if (isAwaitingSpaceForNextNpc)
            {
                bool isModalOpen = (flipPhonePresenter != null && flipPhonePresenter.IsOpen) ||
                                   (endScreenPresenter != null && endScreenPresenter.IsVisible);

                if (!isModalOpen)
                {
                    bool spaceOrEnter = Input.GetKeyDown(callNextNpcKey) ||
                                        Input.GetKeyDown(KeyCode.Space) ||
                                        Input.GetKeyDown(KeyCode.Return) ||
                                        Input.GetKeyDown(KeyCode.KeypadEnter);

                    if (spaceOrEnter)
                    {
                        AuthorizeNextVisitor();
                    }
                }
            }
        }

        /// <summary>
        /// Autoriza e inicia a chamada do próximo visitante quando o gabinete estiver livre.
        /// Pode ser acionado pela tecla configurada (Espaço/Enter) ou por botão de interface.
        /// </summary>
        public void AuthorizeNextVisitor()
        {
            if (!isAwaitingSpaceForNextNpc) return;
            if (flipPhonePresenter != null && flipPhonePresenter.IsOpen) return;
            if (endScreenPresenter != null && endScreenPresenter.IsVisible) return;

            isAwaitingSpaceForNextNpc = false;
            Debug.Log($"<color=#00ffaa>[MandatoBootstrap]</color> 🚪 Entrada autorizada. Chamando próximo visitante...");

            if (!gameObject.activeInHierarchy || !isActiveAndEnabled)
            {
                DrawNextProposalImmediate();
            }
            else
            {
                StartCoroutine(DrawNextProposalRoutine(0.05f));
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(mainMenuSceneName))
            {
                mainMenuSceneName = "MenuV2";
            }
        }
#endif

        private void ApplyLegacyCanvasSuppression()
        {
            if (!disableLegacyCanvas) return;

            // Oculta/desativa os painéis antigos do Canvas legado para não sobrepor o UI Toolkit
            var canvasObjects = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in canvasObjects)
            {
                if (c == null) continue;
                // Não desativa se for render texture de WorldSpace de papel/monitor
                if (c.renderMode == RenderMode.ScreenSpaceOverlay || c.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    // Oculta painéis filhos específicos (dealPanel, gameOverPanel, decisionButtonsPanel)
                    var transforms = c.GetComponentsInChildren<Transform>(true);
                    foreach (var t in transforms)
                    {
                        if (t == null || t == c.transform) continue;
                        string lower = t.name.ToLower();
                        if (lower.Contains("dealpanel") || lower.Contains("gameover") || lower.Contains("decisionbutton") || lower.Contains("corruptionpanel"))
                        {
                            t.gameObject.SetActive(false);
                        }
                    }
                }
            }

            // Desativa componentes UIManager legados na cena para evitar execução concorrente
            var legacyUIManagers = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var mb in legacyUIManagers)
            {
                if (mb != null && mb.GetType().Name == "UIManager")
                {
                    mb.enabled = false;
                }
            }
        }

        private void AutoDetectPresenters()
        {
            // 1. Apresentação 3D
            if (presentationCoordinator == null)
            {
                presentationCoordinator = FindFirstObjectByType<RunPresentationCoordinator>();
                if (presentationCoordinator == null)
                {
                    presentationCoordinator = gameObject.AddComponent<RunPresentationCoordinator>();
                }
            }

            // 2. Papel Físico 3D
            if (paperPresenter == null)
            {
                paperPresenter = FindFirstObjectByType<PaperDocumentPresenter>(FindObjectsInactive.Include);
                if (paperPresenter == null)
                {
                    var paperObj = GameObject.Find("Papel") ?? GameObject.Find("Paper");
                    if (paperObj != null)
                    {
                        paperPresenter = paperObj.GetComponent<PaperDocumentPresenter>() ?? paperObj.AddComponent<PaperDocumentPresenter>();
                    }
                    else
                    {
                        var go = new GameObject("PaperPresenter");
                        go.transform.SetParent(transform);
                        paperPresenter = go.AddComponent<PaperDocumentPresenter>();
                    }
                }
            }

            // 3. Monitor Retrô
            if (retroMonitorPresenter == null)
            {
                retroMonitorPresenter = FindFirstObjectByType<RetroMonitorPresenter>(FindObjectsInactive.Include);
                if (retroMonitorPresenter == null)
                {
                    var monitorObj = GameObject.Find("Monitor") ?? GameObject.Find("RetroMonitor");
                    if (monitorObj != null)
                    {
                        retroMonitorPresenter = monitorObj.GetComponent<RetroMonitorPresenter>() ?? monitorObj.AddComponent<RetroMonitorPresenter>();
                    }
                    else
                    {
                        var go = new GameObject("RetroMonitorPresenter");
                        go.transform.SetParent(transform);
                        retroMonitorPresenter = go.AddComponent<RetroMonitorPresenter>();
                    }
                }
            }

            // 4. Interface de Decisão
            if (decisionOverlayPresenter == null)
            {
                decisionOverlayPresenter = FindFirstObjectByType<DecisionOverlayPresenter>(FindObjectsInactive.Include);
                if (decisionOverlayPresenter == null)
                {
                    var decisionObj = GameObject.Find("DecisionOverlayUI") ?? GameObject.Find("DecisionUI") ?? GameObject.Find("Canvas");
                    if (decisionObj != null)
                    {
                        decisionOverlayPresenter = decisionObj.GetComponent<DecisionOverlayPresenter>() ?? decisionObj.AddComponent<DecisionOverlayPresenter>();
                    }
                    else
                    {
                        var go = new GameObject("DecisionOverlayUI");
                        decisionOverlayPresenter = go.AddComponent<DecisionOverlayPresenter>();
                    }
                }
            }

            // 5. Tela de Fim de Jogo
            if (endScreenPresenter == null)
            {
                endScreenPresenter = FindFirstObjectByType<EndScreenPresenter>(FindObjectsInactive.Include);
                if (endScreenPresenter == null)
                {
                    var endScreenObj = GameObject.Find("EndScreenUI") ?? GameObject.Find("EndScreen");
                    if (endScreenObj != null)
                    {
                        endScreenPresenter = endScreenObj.GetComponent<EndScreenPresenter>() ?? endScreenObj.AddComponent<EndScreenPresenter>();
                    }
                    else
                    {
                        var go = new GameObject("EndScreenUI");
                        endScreenPresenter = go.AddComponent<EndScreenPresenter>();
                    }
                }
            }

            // 6. Animator do Jogador
            if (playerAnimator == null)
            {
                var animators = FindObjectsByType<Animator>(FindObjectsSortMode.None);
                foreach (var a in animators)
                {
                    if (a != null && (a.HasState(0, Animator.StringToHash(dealAnimationName)) || a.HasState(0, Animator.StringToHash("Base Layer." + dealAnimationName))))
                    {
                        playerAnimator = a;
                        break;
                    }
                }
            }

            // 7. Flip-Phone
            if (flipPhonePresenter == null)
            {
                flipPhonePresenter = FindFirstObjectByType<FlipPhonePresenter>(FindObjectsInactive.Include);
                if (flipPhonePresenter == null)
                {
                    var phoneObj = GameObject.Find("Celular") ?? GameObject.Find("FlipPhone") ?? GameObject.Find("Phone");
                    if (phoneObj != null)
                    {
                        flipPhonePresenter = phoneObj.GetComponent<FlipPhonePresenter>() ?? phoneObj.AddComponent<FlipPhonePresenter>();
                    }
                    else
                    {
                        var go = new GameObject("FlipPhoneUI");
                        flipPhonePresenter = go.AddComponent<FlipPhonePresenter>();
                    }
                }
            }

            // Conecta FocusableObject do celular 3D se presente na mesa
            HookPhoneFocusableObject();
        }

        private void HookPhoneFocusableObject()
        {
            try
            {
                Type focusType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    focusType = asm.GetType("FocusableObject");
                    if (focusType != null) break;
                }

                if (focusType != null)
                {
                    var phoneObj = GameObject.Find("FlipPhone") ?? GameObject.Find("Phone") ?? GameObject.Find("Celular");
                    if (phoneObj != null)
                    {
                        var focusComp = phoneObj.GetComponent(focusType) as MonoBehaviour;
                        if (focusComp != null)
                        {
                            var onFocusedField = focusType.GetField("onFocused");
                            var onUnfocusedField = focusType.GetField("onUnfocused");

                            if (onFocusedField?.GetValue(focusComp) is UnityEngine.Events.UnityEvent onFocused)
                            {
                                onFocused.RemoveListener(OpenFlipPhone);
                                onFocused.AddListener(OpenFlipPhone);
                            }
                            if (onUnfocusedField?.GetValue(focusComp) is UnityEngine.Events.UnityEvent onUnfocused)
                            {
                                onUnfocused.RemoveListener(CloseFlipPhone);
                                onUnfocused.AddListener(CloseFlipPhone);
                            }

                            Debug.Log($"<color=#00e5ff>[MandatoBootstrap]</color> 📱 FocusableObject do Flip-Phone conectado com sucesso em '{phoneObj.name}'.");
                        }
                    }
                }
            }
            catch { }
        }

        private void BuildCatalog()
        {
            catalog.Build(
                tutorialDealsOrCards,
                startingDealsOrCards,
                perksCatalog,
                eventsCatalog,
                questsCatalog,
                endingsCatalog,
                startingActions,
                playTutorial
            );
        }

        private void InitializeStateMachine()
        {
            int seed = customSeed != 0 ? customSeed : UnityEngine.Random.Range(1, 100000);
            
            var priorityIds = (playTutorial && catalog.TutorialCardIds.Count > 0) ? catalog.TutorialCardIds : null;

            StateMachine = new RunStateMachine(seed: seed);
            StateMachine.StartRun(catalog.MainDeckCardIds, seed, priorityIds);
        }

        private void BindPresenters()
        {
            if (StateMachine == null) return;

            // 1. Apresentação 3D
            if (presentationCoordinator != null)
            {
                presentationCoordinator.Bind(StateMachine, CardCatalog);
                presentationCoordinator.OnProposalOnDesk += OnProposalReadyOnDesk;
                presentationCoordinator.OnConsequencesFinished += OnConsequencesFinishedAndAdvance;
            }
            else
            {
                // Se não houver coordenador 3D, apresenta direto na mesa
                StateMachine.OnProposalReady += OnProposalReadyOnDesk;
            }

            // 2. Consequências e Monitor CRT
            StateMachine.OnConsequencesReady += OnConsequencesResolved;

            // 3. Fim de Run
            StateMachine.OnRunTerminated += OnRunTerminated;

            // 4. Decisões do Player
            if (decisionOverlayPresenter != null)
            {
                decisionOverlayPresenter.OnChoiceSelected += OnPlayerChoiceSubmitted;
            }

            // 5. Botões de Fim de Jogo
            if (endScreenPresenter != null)
            {
                endScreenPresenter.OnRestartRequested += RestartRun;
                endScreenPresenter.OnMainMenuRequested += ReturnToMenu;
            }

            // 6. Flip-Phone
            if (flipPhonePresenter != null)
            {
                flipPhonePresenter.OnActionRequested += RequestUseFlipPhoneAction;
                flipPhonePresenter.OnPhoneOpened += OnFlipPhoneOpenedByPresenter;
                flipPhonePresenter.OnPhoneClosed += OnFlipPhoneClosedByPresenter;
            }
        }

        public bool IsTutorialCard(CardDefinition card)
        {
            if (card == null) return false;
            if (card.isTutorial) return true;
            return IsTutorialCard(card.id) ||
                   card.categoryTag.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   card.title.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public bool IsTutorialCard(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return false;
            if (catalog.IsTutorialCardId(cardId)) return true;
            if (catalog.Cards.TryGetValue(cardId, out var card) && card != null && card.isTutorial) return true;
            return cardId.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public bool HasRemainingTutorialCards()
        {
            if (StateMachine == null || StateMachine.DeckState == null) return false;

            if (StateMachine.DeckState.priorityDrawPile != null)
            {
                foreach (var cardId in StateMachine.DeckState.priorityDrawPile)
                {
                    if (IsTutorialCard(cardId)) return true;
                }
            }

            if (StateMachine.DeckState.drawPile != null)
            {
                foreach (var cardId in StateMachine.DeckState.drawPile)
                {
                    if (IsTutorialCard(cardId)) return true;
                }
            }

            return false;
        }

        public void DrawFirstProposal()
        {
            if (StateMachine != null && CardCatalog.Count > 0)
            {
                StateMachine.DrawAndPresentProposal(CardCatalog);
            }
        }

        private void OnProposalReadyOnDesk(CardDefinition card)
        {
            if (card == null || StateMachine == null) return;

            string displayDate = StateMachine.RunState.calendar.DisplayDate;

            // 1. Preenche e exibe o papel físico 3D
            if (paperPresenter != null)
            {
                paperPresenter.SetProposal(card, displayDate);
            }

            // 2. Toca a animação do jogador levantando a mão com o documento
            bool isTutorial = IsTutorialCard(card);
            if (!isPlayerHandRaised)
            {
                PlayPlayerDealAnimation();
                isPlayerHandRaised = true;
            }

            // 3. Exibe os botões de decisão na tela
            if (decisionOverlayPresenter != null)
            {
                decisionOverlayPresenter.PresentChoices(card);
            }

            // 4. Notifica o monitor retrô CRT da nova proposta e atualiza a data
            if (retroMonitorPresenter != null)
            {
                retroMonitorPresenter.NotifyNewProposal(card);
                retroMonitorPresenter.UpdateDateDisplay(displayDate);
            }

            // 5. Sincroniza Canvas UI (DateText e Sliders)
            SyncCanvasUI();
        }

        private bool isPlayerHandRaised = false;

        private void OnPlayerChoiceSubmitted(int choiceIndex)
        {
            if (StateMachine == null) return;

            bool isTutorial = IsTutorialCard(StateMachine.CurrentCard);
            bool hasMoreTutorial = isTutorial && HasRemainingTutorialCards();

            // Durante o tutorial, mantém a mão levantada e a câmera no documento
            if (!isTutorial || !hasMoreTutorial)
            {
                // 1. Toca animação reversa do jogador (abaixando a mão)
                PlayPlayerDealAnimationReverse();
                isPlayerHandRaised = false;

                // 2. Desabilita interação do papel e desfoque de câmera
                if (paperPresenter != null)
                {
                    paperPresenter.SetPaperInteractable(false);
                }

                NotifyCameraUnfocus();
            }
            else
            {
                // Mantém o papel ativo e interativo entre diálogos do tutorial (mão permanece levantada)
                if (paperPresenter != null)
                {
                    paperPresenter.SetPaperInteractable(true);
                }
            }

            // 3. Submete a escolha para resolução de estado (com catálogo de quests)
            StateMachine.SubmitChoice(choiceIndex, QuestCatalog);
        }

        private void NotifyCameraUnfocus()
        {
            try
            {
                Type focusType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    focusType = asm.GetType("CameraFocusManager");
                    if (focusType != null) break;
                }

                if (focusType != null)
                {
                    var instanceProp = focusType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var inst = instanceProp?.GetValue(null) ?? FindFirstObjectByType(focusType);
                    if (inst != null)
                    {
                        var hasFocusProp = focusType.GetProperty("HasActiveFocus");
                        bool hasFocus = (bool)(hasFocusProp?.GetValue(inst) ?? false);
                        if (hasFocus)
                        {
                            focusType.GetMethod("Unfocus")?.Invoke(inst, null);
                        }
                    }
                }
            }
            catch { }
        }

        private void OnConsequencesResolved(ResolutionReport report)
        {
            if (report == null || StateMachine == null) return;

            if (decisionOverlayPresenter != null)
            {
                decisionOverlayPresenter.ClearChoices();
                decisionOverlayPresenter.SetCorruptionLevel(StateMachine.RunState.stats.corruption);
            }

            if (retroMonitorPresenter != null)
            {
                retroMonitorPresenter.UpdateSnapshot(StateMachine.RunState.GetSnapshot(), report);
            }

            SyncCanvasUI();
            SyncCameraEffects(instant: false);
        }

        private void OnConsequencesFinishedAndAdvance(ResolutionReport report)
        {
            if (StateMachine == null) return;

            // Atualiza cooldowns visuais no celular
            RefreshFlipPhoneUI();

            // Verifica se a run terminou
            if (StateMachine.RunState.termination.IsDefeat || StateMachine.RunState.termination.IsVictory)
            {
                return;
            }

            SyncCanvasUI();

            // Tutorial contínuo avança direto sem delay e sem exigir tecla entre diálogos
            bool isTutorial = report != null ? IsTutorialCard(report.cardId) : IsTutorialCard(StateMachine.CurrentCard);
            bool hasMoreTutorial = isTutorial && HasRemainingTutorialCards();

            if (isTutorial)
            {
                if (hasMoreTutorial)
                {
                    // Diálogos de tutorial avançam instantaneamente para o próximo diálogo
                    StartCoroutine(DrawNextProposalRoutine(0.02f));
                }
                else
                {
                    // Concluiu todos os diálogos de tutorial: agora sim prepara o gabinete para receber o primeiro visitante
                    float endDelay = Mathf.Max(0.2f, delayBetweenProposals);
                    pendingProposalDelay = endDelay;

                    if (requireSpaceToCallNextNpc)
                    {
                        isAwaitingSpaceForNextNpc = true;
                        Debug.Log("<color=#00e5ff>[MandatoBootstrap]</color> ⏳ <b>Tutorial concluído! Gabinete livre.</b> Pressione <b>[ESPAÇO]</b> para autorizar a entrada do primeiro visitante.");
                    }
                    else
                    {
                        StartCoroutine(DrawNextProposalRoutine(endDelay));
                    }
                }
                return;
            }

            // Avança o turno e processa os modificadores mensais (Perks e Eventos)
            var monthlyReport = StateMachine.CompleteTurnAndAdvance(PerkCatalog, EventCatalog);
            if (monthlyReport != null && monthlyReport.HasAnyImpact)
            {
                if (retroMonitorPresenter != null)
                {
                    retroMonitorPresenter.UpdateSnapshot(StateMachine.RunState.GetSnapshot());
                    retroMonitorPresenter.TriggerGlitch();
                }
                SyncCanvasUI();
                SyncCameraEffects(instant: false);
            }

            float delay = Mathf.Max(0.2f, delayBetweenProposals);
            pendingProposalDelay = delay;

            if (requireSpaceToCallNextNpc)
            {
                isAwaitingSpaceForNextNpc = true;
                Debug.Log("<color=#00e5ff>[MandatoBootstrap]</color> ⏳ <b>Gabinete livre.</b> Pressione <b>[ESPAÇO]</b> para autorizar a entrada do próximo visitante.");
            }
            else
            {
                // Sempre via coroutine — nunca síncrono — para evitar race com PresentConsequencesRoutine
                StartCoroutine(DrawNextProposalRoutine(delay));
            }
        }

        private void SyncCameraEffects(bool instant = false)
        {
            if (StateMachine == null) return;
            var stats = StateMachine.RunState.stats;

            try
            {
                Type cameraEffectsType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    cameraEffectsType = asm.GetType("AttributeCameraEffects");
                    if (cameraEffectsType != null) break;
                }

                if (cameraEffectsType != null)
                {
                    var instanceProp = cameraEffectsType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var inst = instanceProp?.GetValue(null) ?? FindFirstObjectByType(cameraEffectsType);
                    if (inst != null)
                    {
                        var pollutionMethod = cameraEffectsType.GetMethod("UpdatePollutionEffect");
                        var corruptionMethod = cameraEffectsType.GetMethod("UpdateCorruptionEffect");

                        pollutionMethod?.Invoke(inst, new object[] { (float)stats.climaticChanges, instant });
                        corruptionMethod?.Invoke(inst, new object[] { (float)stats.corruption, instant });
                    }
                }
            }
            catch { }
        }

        private void SyncCanvasUI()
        {
            if (StateMachine == null) return;

            string displayDate = StateMachine.RunState.calendar.DisplayDate;
            var stats = StateMachine.RunState.stats;

            // 1. Atualiza Data no Canvas (TMPro ou Text via reflexão sem acoplamento de asmdef)
            var allComponents = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var comp in allComponents)
            {
                if (comp == null) continue;
                string typeName = comp.GetType().Name;
                string objName = comp.gameObject.name.ToLower();

                if (typeName.Contains("TextMeshPro") || typeName == "Text")
                {
                    if (objName.Contains("date") || objName.Contains("data"))
                    {
                        var textProp = comp.GetType().GetProperty("text", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        textProp?.SetValue(comp, displayDate);
                    }
                }
            }

            // 2. Atualiza Sliders do Canvas se existirem
            var allSliders = FindObjectsByType<UnityEngine.UI.Slider>(FindObjectsSortMode.None);
            foreach (var s in allSliders)
            {
                if (s == null) continue;
                string lower = s.gameObject.name.ToLower();
                if (lower.Contains("clima") || lower.Contains("nature"))
                    s.value = stats.climaticChanges / 100f;
                else if (lower.Contains("eco"))
                    s.value = stats.economy / 100f;
                else if (lower.Contains("relat") || lower.Contains("inter"))
                    s.value = stats.internationalRelations / 100f;
                else if (lower.Contains("approv") || lower.Contains("popul") || lower.Contains("povo"))
                    s.value = stats.popularApproval / 100f;
                else if (lower.Contains("corrup"))
                    s.value = stats.corruption / 100f;
            }

            // 3. Notifica UIManager legado se presente
            try
            {
                Type uiMgrType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    uiMgrType = asm.GetType("UIManager");
                    if (uiMgrType != null) break;
                }

                if (uiMgrType != null)
                {
                    var instanceProp = uiMgrType.GetField("instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var inst = instanceProp?.GetValue(null);
                    if (inst != null)
                    {
                        var dateField = uiMgrType.GetField("dateText");
                        var dateComp = dateField?.GetValue(inst);
                        if (dateComp != null)
                        {
                            var prop = dateComp.GetType().GetProperty("text");
                            prop?.SetValue(dateComp, displayDate);
                        }
                    }
                }
            }
            catch { }
        }

        private void DrawNextProposalImmediate()
        {
            if (StateMachine != null && CardCatalog.Count > 0)
            {
                bool drawn = StateMachine.DrawAndPresentProposal(CardCatalog);
                if (!drawn)
                {
                    Debug.LogWarning("<color=#ffaa00>[MandatoBootstrap]</color> ⚠️ Nenhuma nova proposta foi sorteada (fim de baralho ou partida encerrada).");
                }
            }
            else
            {
                Debug.LogWarning($"<color=#ff5566>[MandatoBootstrap]</color> ⚠️ Não foi possível puxar proposta: StateMachine={StateMachine != null}, Catálogo={CardCatalog?.Count} itens.");
            }
        }

        private IEnumerator DrawNextProposalRoutine(float delay = 0.2f)
        {
            yield return new WaitForSeconds(delay);
            DrawNextProposalImmediate();
        }

        private void OnRunTerminated(RunTermination termination)
        {
            if (StateMachine == null) return;

            isAwaitingSpaceForNextNpc = false;

            RunSnapshot finalSnapshot = StateMachine.RunState.GetSnapshot();
            EndingDefinition evaluatedEnding = null;

            try
            {
                var endingsList = endingsCatalog != null && endingsCatalog.Count > 0 ? (IEnumerable<EndingDefinition>)endingsCatalog : catalog.Endings.Values;
                evaluatedEnding = EndingEvaluator.EvaluateEnding(StateMachine.RunState, endingsList);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MandatoBootstrap] Erro ao avaliar final: {ex.Message}");
            }

            // 1. Registra e salva metaprogressão
            profileService.RecordRunCompleted(termination.IsVictory, evaluatedEnding?.id);

            // 2. Limpa papel, overlay de decisão e celular
            if (paperPresenter != null)
            {
                paperPresenter.Clear();
                paperPresenter.SetPaperInteractable(false);
            }

            if (decisionOverlayPresenter != null)
            {
                decisionOverlayPresenter.ClearChoices();
            }

            CloseFlipPhone();

            // 3. Unfocus da câmera
            NotifyCameraUnfocus();

            // 4. Notifica monitor retrô CRT (logs de falha crítica ou vitória)
            if (retroMonitorPresenter != null)
            {
                retroMonitorPresenter.NotifyTermination(termination);
            }

            // 5. Exibe tela de fim de jogo do UI Toolkit
            if (endScreenPresenter == null)
            {
                endScreenPresenter = FindFirstObjectByType<EndScreenPresenter>() ?? gameObject.AddComponent<EndScreenPresenter>();
            }

            if (endScreenPresenter != null)
            {
                endScreenPresenter.ShowEndScreen(termination, finalSnapshot);
            }

            // 6. Fallback Canvas legado
            ShowCanvasGameOver(termination);
        }

        private void ShowCanvasGameOver(RunTermination termination)
        {
            string reason = !string.IsNullOrEmpty(termination.reason)
                ? termination.reason
                : (termination.IsVictory ? "Você concluiu 4 anos de mandato com sucesso!" : "Seu mandato foi interrompido antes do término.");

            // 1. Tenta via UIManager.instance
            try
            {
                Type uiMgrType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    uiMgrType = asm.GetType("UIManager");
                    if (uiMgrType != null) break;
                }

                if (uiMgrType != null)
                {
                    var instanceProp = uiMgrType.GetField("instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var inst = instanceProp?.GetValue(null) ?? FindFirstObjectByType(uiMgrType);
                    if (inst != null)
                    {
                        string methodName = termination.IsVictory ? "ShowGameWin" : "ShowGameOver";
                        var method = uiMgrType.GetMethod(methodName);
                        if (method != null)
                        {
                            method.Invoke(inst, new object[] { reason });
                            return;
                        }
                    }
                }
            }
            catch { }

            // 2. Fallback direto: busca GameObject 'gameOverPanel' ou 'GameOverPanel'
            var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in allTransforms)
            {
                if (t == null) continue;
                string lower = t.name.ToLower();
                if (lower == "gameoverpanel" || lower == "gameover" || lower == "panelgameover" || lower == "painelgameover")
                {
                    t.gameObject.SetActive(true);
                    var rect = t.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        rect.anchoredPosition = new Vector2(0, -300);
                    }

                    // Preenche texto de GameOver se existir no painel
                    var texts = t.GetComponentsInChildren<MonoBehaviour>(true);
                    foreach (var comp in texts)
                    {
                        if (comp == null) continue;
                        string compName = comp.GetType().Name;
                        if (compName.Contains("TextMeshPro") || compName == "Text")
                        {
                            if (comp.gameObject.name.ToLower().Contains("reason") || comp.gameObject.name.ToLower().Contains("text") || comp.gameObject.name.ToLower().Contains("gameover"))
                            {
                                var textProp = comp.GetType().GetProperty("text");
                                textProp?.SetValue(comp, reason);
                            }
                        }
                    }
                    break;
                }
            }
        }

        private Coroutine playerAnimRoutine;

        private void EnsurePlayerAnimator()
        {
            if (playerAnimator != null && playerAnimator.runtimeAnimatorController != null) return;

            var animators = FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var a in animators)
            {
                if (a == null || a.runtimeAnimatorController == null) continue;

                int dealHash = Animator.StringToHash(dealAnimationName);
                int baseDealHash = Animator.StringToHash("Base Layer." + dealAnimationName);

                if (a.HasState(0, dealHash) || a.HasState(0, baseDealHash))
                {
                    playerAnimator = a;
                    Debug.Log($"<color=#00ffaa>[MandatoBootstrap]</color> ✋ Animator do jogador auto-detectado em '{a.gameObject.name}'.");
                    break;
                }
            }

            if (playerAnimator == null && animators.Length > 0)
            {
                // Fallback: se nenhum tem o nome exato do estado, busca um com nome Player / Mao / Hand / MaoJogador
                foreach (var a in animators)
                {
                    if (a == null || a.runtimeAnimatorController == null) continue;
                    string lower = a.gameObject.name.ToLower();
                    if (lower.Contains("player") || lower.Contains("mao") || lower.Contains("hand") || lower.Contains("braco") || lower.Contains("arm"))
                    {
                        playerAnimator = a;
                        Debug.Log($"<color=#00ffaa>[MandatoBootstrap]</color> ✋ Animator do jogador detectado por nome em '{a.gameObject.name}'.");
                        break;
                    }
                }
            }
        }

        private void PlayPlayerDealAnimation()
        {
            EnsurePlayerAnimator();
            if (playerAnimator == null || playerAnimator.runtimeAnimatorController == null)
            {
                Debug.LogWarning("<color=#ff5566>[MandatoBootstrap]</color> ⚠️ PlayerAnimator não encontrado na cena para tocar animação da mão!");
                return;
            }

            if (playerAnimRoutine != null)
            {
                StopCoroutine(playerAnimRoutine);
                playerAnimRoutine = null;
            }

            playerAnimator.speed = 1f;

            int stateHash = Animator.StringToHash(dealAnimationName);
            int baseStateHash = Animator.StringToHash("Base Layer." + dealAnimationName);

            if (playerAnimator.HasState(0, stateHash) || playerAnimator.HasState(0, baseStateHash))
            {
                Debug.Log($"<color=#00ffaa>[MandatoBootstrap]</color> ✋ Tocando animação '{dealAnimationName}' no Animator '{playerAnimator.gameObject.name}'.");
                playerAnimator.Play(dealAnimationName, 0, 0f);
            }
            else
            {
                Debug.LogWarning($"<color=#ff5566>[MandatoBootstrap]</color> ⚠️ Animator '{playerAnimator.gameObject.name}' não possui o estado '{dealAnimationName}'!");
            }
        }

        private void PlayPlayerDealAnimationReverse()
        {
            EnsurePlayerAnimator();
            if (playerAnimator == null || playerAnimator.runtimeAnimatorController == null) return;

            if (playerAnimRoutine != null)
            {
                try
                {
                    StopCoroutine(playerAnimRoutine);
                }
                catch { }
                playerAnimRoutine = null;
            }

            if (!gameObject.activeInHierarchy || !isActiveAndEnabled)
            {
                // Fallback caso o GameObject esteja inativo: toca o estado final diretamente
                if (!string.IsNullOrEmpty(dealAnimationReverseName) &&
                    (playerAnimator.HasState(0, Animator.StringToHash(dealAnimationReverseName)) ||
                     playerAnimator.HasState(0, Animator.StringToHash("Base Layer." + dealAnimationReverseName))))
                {
                    playerAnimator.speed = 1f;
                    playerAnimator.Play(dealAnimationReverseName, 0, 0f);
                }
                else if (!string.IsNullOrEmpty(defaultAnimationName) &&
                         (playerAnimator.HasState(0, Animator.StringToHash(defaultAnimationName)) ||
                          playerAnimator.HasState(0, Animator.StringToHash("Base Layer." + defaultAnimationName))))
                {
                    playerAnimator.Play(defaultAnimationName, 0, 0f);
                }
                return;
            }

            playerAnimRoutine = StartCoroutine(PlayDealAnimationReverseRoutine());
        }

        private IEnumerator PlayDealAnimationReverseRoutine()
        {
            EnsurePlayerAnimator();
            if (playerAnimator == null) yield break;

            Debug.Log($"<color=#00ffaa>[MandatoBootstrap]</color> ✋ Tocando animação reversa de '{dealAnimationName}' no Animator '{playerAnimator.gameObject.name}'.");

            // 1. Se houver estado dedicado reverso no Animator, toca ele
            if (!string.IsNullOrEmpty(dealAnimationReverseName) &&
                (playerAnimator.HasState(0, Animator.StringToHash(dealAnimationReverseName)) ||
                 playerAnimator.HasState(0, Animator.StringToHash("Base Layer." + dealAnimationReverseName))))
            {
                playerAnimator.speed = 1f;
                playerAnimator.Play(dealAnimationReverseName, 0, 0f);
                yield return new WaitForSeconds(0.4f);
            }
            // 2. Interpola o tempo de trás para frente suavemente
            else if (!string.IsNullOrEmpty(dealAnimationName) &&
                     (playerAnimator.HasState(0, Animator.StringToHash(dealAnimationName)) ||
                      playerAnimator.HasState(0, Animator.StringToHash("Base Layer." + dealAnimationName))))
            {
                float duration = 0.4f;
                float elapsed = 0f;
                playerAnimator.speed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Lerp(1f, 0f, elapsed / duration);
                    playerAnimator.Play(dealAnimationName, 0, t);
                    playerAnimator.Update(0f);
                    yield return null;
                }

                playerAnimator.speed = 1f;
            }

            if (!string.IsNullOrEmpty(defaultAnimationName) &&
                (playerAnimator.HasState(0, Animator.StringToHash(defaultAnimationName)) ||
                 playerAnimator.HasState(0, Animator.StringToHash("Base Layer." + defaultAnimationName))))
            {
                playerAnimator.Play(defaultAnimationName, 0, 0f);
            }

            playerAnimRoutine = null;
        }

        public void RestartRun()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ReturnToMenu()
        {
            if (!string.IsNullOrEmpty(mainMenuSceneName))
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
        }

        #region Flip-Phone Operations

        private void OnFlipPhoneOpenedByPresenter()
        {
            PlayPlayerDealAnimation();
            RefreshFlipPhoneUI();
            Debug.Log("<color=#00e5ff>[MandatoBootstrap]</color> 📱 Flip-Phone aberto via atalho/UI. Tocando animação da mão.");
        }

        private void OnFlipPhoneClosedByPresenter()
        {
            PlayPlayerDealAnimationReverse();
            Debug.Log("<color=#00e5ff>[MandatoBootstrap]</color> 📱 Flip-Phone fechado via atalho/UI. Tocando animação reversa da mão.");
        }

        public void OpenFlipPhone()
        {
            if (flipPhonePresenter != null)
            {
                if (!flipPhonePresenter.IsOpen)
                {
                    flipPhonePresenter.Open();
                }
                else
                {
                    RefreshFlipPhoneUI();
                }
            }
            else
            {
                PlayPlayerDealAnimation();
            }
        }

        public void CloseFlipPhone()
        {
            if (flipPhonePresenter != null)
            {
                if (flipPhonePresenter.IsOpen)
                {
                    flipPhonePresenter.Close();
                }
            }
            else
            {
                PlayPlayerDealAnimationReverse();
            }
        }

        public void ToggleFlipPhone()
        {
            bool isCurrentlyOpen = flipPhonePresenter != null && flipPhonePresenter.IsOpen;
            Debug.Log($"<color=#00e5ff>[MandatoBootstrap]</color> 📱 <b>Toggle Flip-Phone</b> (Estado atual: {(isCurrentlyOpen ? "Aberto -> Fechando" : "Fechado -> Abrindo")})");

            if (isCurrentlyOpen) CloseFlipPhone();
            else OpenFlipPhone();
        }

        public void RefreshFlipPhoneUI()
        {
            if (flipPhonePresenter == null || StateMachine == null) return;

            var viewModels = new List<FlipPhoneActionViewModel>();
            var runState = StateMachine.RunState;
            var currentCard = StateMachine.CurrentCard;
            string currentNpcId = currentCard != null ? currentCard.npcId : string.Empty;

            foreach (var kvp in ActionCatalog)
            {
                var action = kvp.Value;
                if (action == null) continue;

                bool isUnlocked = runState.IsActionUnlocked(action.id) || action.unlockByDefault;
                if (!isUnlocked) continue;

                bool isConsumed = action.cooldownType == FlipPhoneCooldownType.SingleUse && runState.IsActionConsumed(action.id);
                bool isOnCooldown = runState.IsActionOnCooldown(action.id);
                int cooldownTurns = runState.GetActionCooldown(action.id);
                bool conditionsMet = action.AreConditionsMet(runState.stats, runState.calendar.currentMonthIndex, runState.activePerkIds, runState.decisionHistory, currentNpcId);

                var vm = new FlipPhoneActionViewModel
                {
                    id = action.id,
                    displayName = action.displayName,
                    description = action.description,
                    categoryTag = !string.IsNullOrEmpty(action.categoryTag) ? action.categoryTag : "Ações",
                    icon = action.icon,
                    isConsumed = isConsumed,
                    isOnCooldown = isOnCooldown,
                    cooldownTurnsRemaining = cooldownTurns,
                    isAvailable = !isConsumed && !isOnCooldown && conditionsMet,
                    statusText = !conditionsMet ? "REQUISITOS NÃO ATENDIDOS" : string.Empty
                };

                // Monta resumo de tags de impacto
                if (action.effects != null)
                {
                    foreach (var eff in action.effects)
                    {
                        if (eff == null) continue;
                        if (eff.effectType == FlipPhoneEffectType.StatImpact && eff.statImpacts != null)
                        {
                            if (eff.statImpacts.climaticChanges != 0)
                                vm.impactTags.Add($"Clima {(eff.statImpacts.climaticChanges > 0 ? "+" : "")}{eff.statImpacts.climaticChanges}%");
                            if (eff.statImpacts.economy != 0)
                                vm.impactTags.Add($"Eco {(eff.statImpacts.economy > 0 ? "+" : "")}{eff.statImpacts.economy}%");
                            if (eff.statImpacts.internationalRelations != 0)
                                vm.impactTags.Add($"Rel {(eff.statImpacts.internationalRelations > 0 ? "+" : "")}{eff.statImpacts.internationalRelations}%");
                            if (eff.statImpacts.popularApproval != 0)
                                vm.impactTags.Add($"Pop {(eff.statImpacts.popularApproval > 0 ? "+" : "")}{eff.statImpacts.popularApproval}%");
                            if (eff.statImpacts.corruption != 0)
                                vm.impactTags.Add($"Corrupção {(eff.statImpacts.corruption > 0 ? "+" : "")}{eff.statImpacts.corruption}%");
                        }
                        else if (eff.effectType == FlipPhoneEffectType.DismissCurrentProposal)
                        {
                            vm.impactTags.Add("Descarta Proposta");
                        }
                        else if (eff.effectType == FlipPhoneEffectType.RemoveNpcFromGame)
                        {
                            vm.impactTags.Add($"Elimina NPC: {eff.targetId}");
                        }
                    }
                }

                viewModels.Add(vm);
            }

            Debug.Log($"<color=#00e5ff>[MandatoBootstrap]</color> 📱 RefreshFlipPhoneUI: {viewModels.Count} ações enviadas para a interface.");
            flipPhonePresenter.Refresh(viewModels);
        }

        public void RequestUseFlipPhoneAction(string actionId)
        {
            if (StateMachine == null || string.IsNullOrEmpty(actionId)) return;

            Debug.Log($"<color=#00e5ff>[MandatoBootstrap]</color> 📱 Solicitando execução da ação '<b>{actionId}</b>'...");

            if (!ActionCatalog.TryGetValue(actionId, out var action) || action == null)
            {
                Debug.LogWarning($"<color=#ff5566>[MandatoBootstrap]</color> 📱 Ação '{actionId}' não encontrada no catálogo!");
                if (flipPhonePresenter != null)
                    flipPhonePresenter.ShowMessage("Ação não encontrada no diretório.", isError: true);
                return;
            }

            var report = FlipPhoneResolver.ResolveUse(
                StateMachine.RunState,
                StateMachine.DeckState,
                action,
                CardCatalog,
                StateMachine.CurrentCard
            );

            if (report.success)
            {
                Debug.Log($"<color=#00ffaa>[MandatoBootstrap]</color> 📱 ✅ Ação '<b>{report.actionDisplayName}</b>' executada com sucesso!" +
                          $" [Impactos: Eco {report.impactsApplied.economy:+#;-#;0}, Pop {report.impactsApplied.popularApproval:+#;-#;0}, " +
                          $"Rel {report.impactsApplied.internationalRelations:+#;-#;0}, Clima {report.impactsApplied.climaticChanges:+#;-#;0}, " +
                          $"Corrupção {report.impactsApplied.corruption:+#;-#;0}]");

                // 1. Atualiza visual do monitor se houve variação
                if (retroMonitorPresenter != null)
                {
                    retroMonitorPresenter.UpdateSnapshot(StateMachine.RunState.GetSnapshot());
                    retroMonitorPresenter.TriggerGlitch();
                }

                if (decisionOverlayPresenter != null)
                {
                    decisionOverlayPresenter.SetCorruptionLevel(StateMachine.RunState.stats.corruption);
                }

                SyncCanvasUI();
                SyncCameraEffects(instant: false);

                // 2. Se a ação descartou a proposta atual
                if (report.dismissedCurrentProposal)
                {
                    Debug.Log("<color=#00ffaa>[MandatoBootstrap]</color> 📱 Ação descartou a proposta atual. Comandando reação e saída do visitante...");
                    if (paperPresenter != null)
                    {
                        paperPresenter.Clear();
                        paperPresenter.SetPaperInteractable(false);
                    }
                    if (decisionOverlayPresenter != null)
                    {
                        decisionOverlayPresenter.ClearChoices();
                    }

                    // Abaixa a mão do jogador e reseta a flag de animação para a próxima proposta
                    PlayPlayerDealAnimationReverse();
                    isPlayerHandRaised = false;

                    CloseFlipPhone();
                    NotifyCameraUnfocus();
                    RefreshFlipPhoneUI();

                    Action onNpcDismissed = () =>
                    {
                        Debug.Log("<color=#00ffaa>[MandatoBootstrap]</color> 🚪 Gabinete desocupado após saída do NPC.");
                        StateMachine.DismissCurrentProposal(advanceMonth: false);
                        RefreshFlipPhoneUI();

                        pendingProposalDelay = Mathf.Max(0.2f, delayBetweenProposals);
                        if (requireSpaceToCallNextNpc)
                        {
                            isAwaitingSpaceForNextNpc = true;
                            Debug.Log("<color=#00e5ff>[MandatoBootstrap]</color> ⏳ <b>Gabinete livre após descarte.</b> Pressione <b>[ESPAÇO]</b> ou <b>[ENTER]</b> para autorizar a entrada do próximo visitante.");
                        }
                        else if (!gameObject.activeInHierarchy || !isActiveAndEnabled)
                        {
                            DrawNextProposalImmediate();
                        }
                        else
                        {
                            StartCoroutine(DrawNextProposalRoutine(pendingProposalDelay));
                        }
                    };

                    if (presentationCoordinator == null)
                    {
                        presentationCoordinator = FindFirstObjectByType<RunPresentationCoordinator>() ?? gameObject.AddComponent<RunPresentationCoordinator>();
                        presentationCoordinator.Bind(StateMachine, CardCatalog);
                        presentationCoordinator.OnProposalOnDesk += OnProposalReadyOnDesk;
                        presentationCoordinator.OnConsequencesFinished += OnConsequencesFinishedAndAdvance;
                    }

                    if (presentationCoordinator != null)
                    {
                        presentationCoordinator.DismissCurrentProposal(isPositive: false, onDismissed: onNpcDismissed);
                    }
                    else
                    {
                        try
                        {
                            // Busca tipada via interface (sem reflection)
                            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                            {
                                if (mb is Mandato.Presentation.INpcController npcCtrl)
                                {
                                    npcCtrl.ReactAndExit(false);
                                    break;
                                }
                            }
                        }
                        catch { }

                        onNpcDismissed();
                    }

                    return;
                }

                // 3. Atualiza UI do celular com mensagem de sucesso
                RefreshFlipPhoneUI();
                if (flipPhonePresenter != null)
                {
                    flipPhonePresenter.ShowMessage($"> \"{report.actionDisplayName}\" executada com sucesso.");
                }
            }
            else
            {
                Debug.LogWarning($"<color=#ff5566>[MandatoBootstrap]</color> 📱 ❌ Falha ao executar ação '{actionId}': {report.failReason}");
                if (flipPhonePresenter != null)
                {
                    flipPhonePresenter.ShowMessage(report.failReason, isError: true);
                }
            }
        }

        #endregion

        private void OnDestroy()
        {
            if (StateMachine != null)
            {
                StateMachine.OnConsequencesReady -= OnConsequencesResolved;
                StateMachine.OnRunTerminated -= OnRunTerminated;
            }

            if (presentationCoordinator != null)
            {
                presentationCoordinator.OnProposalOnDesk -= OnProposalReadyOnDesk;
                presentationCoordinator.OnConsequencesFinished -= OnConsequencesFinishedAndAdvance;
            }

            if (decisionOverlayPresenter != null)
            {
                decisionOverlayPresenter.OnChoiceSelected -= OnPlayerChoiceSubmitted;
            }

            if (endScreenPresenter != null)
            {
                endScreenPresenter.OnRestartRequested -= RestartRun;
                endScreenPresenter.OnMainMenuRequested -= ReturnToMenu;
            }

            if (flipPhonePresenter != null)
            {
                flipPhonePresenter.OnActionRequested -= RequestUseFlipPhoneAction;
                flipPhonePresenter.OnPhoneOpened -= OnFlipPhoneOpenedByPresenter;
                flipPhonePresenter.OnPhoneClosed -= OnFlipPhoneClosedByPresenter;
            }
        }
    }
}
