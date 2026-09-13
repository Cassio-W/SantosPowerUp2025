using System;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;
using Mandato.UI;
using UnityEngine;

namespace Mandato.Infrastructure
{
    /// <summary>
    /// Ponto de entrada leve da cena de gameplay (JogoV2).
    /// Conecta a inicialização de dados (RunBootstrap), a amarração de cena (ScenePresentationBindings),
    /// o fluxo de propostas e decisões (RunFlowCoordinator) e as ações do celular (FlipPhoneCoordinator).
    /// </summary>
    public class MandatoBootstrap : MonoBehaviour
    {
        public static MandatoBootstrap Instance { get; private set; }

        [Header("Tutorial e Conteúdo da Partida")]
        [Tooltip("Se verdadeiro, a partida inicia pelas propostas do tutorial.")]
        [SerializeField] private bool playTutorial = true;

        [Tooltip("Cartas de tutorial (CardDefinition). São exibidas com prioridade no início da run.")]
        [SerializeField] private List<CardDefinition> tutorialCards = new List<CardDefinition>();

        [Tooltip("Cartas iniciais do baralho principal (CardDefinition).")]
        [SerializeField] private List<CardDefinition> startingCards = new List<CardDefinition>();

        [Tooltip("Catálogo de cartas injetáveis (NonStarting). Registradas no catálogo mas NÃO entram no deck inicial.")]
        [SerializeField] private List<CardDefinition> catalogCards = new List<CardDefinition>();

        [Tooltip("Lista de Finais possíveis para avaliação no término do mandato.")]
        [SerializeField] private List<EndingDefinition> endingsCatalog = new List<EndingDefinition>();

        [Header("Catálogos de Modificadores & Narrativa")]
        [SerializeField] private List<PerkDefinition> perksCatalog = new List<PerkDefinition>();
        [SerializeField] private List<RunEventDefinition> eventsCatalog = new List<RunEventDefinition>();
        [SerializeField] private List<QuestDefinition> questsCatalog = new List<QuestDefinition>();

        [Header("Flip-Phone & Ações")]
        [SerializeField] private List<FlipPhoneActionDefinition> startingActions = new List<FlipPhoneActionDefinition>();

        [Header("Semente e Configurações de Partida")]
        [SerializeField] private int customSeed = 0;
        [SerializeField] private string mainMenuSceneName = "MenuV2";
        [SerializeField] private float delayBetweenProposals = 1.5f;

        [Header("Controle de Fluxo entre Propostas")]
        [Tooltip("Se verdadeiro, o próximo visitante só é chamado após o jogador pressionar a tecla de chamada (Espaço).")]
        [SerializeField] private bool requireSpaceToCallNextNpc = true;
        [SerializeField] private KeyCode callNextNpcKey = KeyCode.Space;

        [Header("Amarrações de Apresentação (Obrigatórias)")]
        [SerializeField] private ScenePresentationBindings presentationBindings = new ScenePresentationBindings();

        [Tooltip("RunFlowCoordinator já presente na cena como componente. Deve ser atribuído no Inspector.")]
        [SerializeField] private RunFlowCoordinator flowCoordinatorRef;

        [Header("Configuração de UI")]
        [Tooltip("Se verdadeiro, desativa os elementos visuais do Canvas legado para rodar em UI Toolkit.")]
        public bool disableLegacyCanvas = true;

        public RunStateMachine StateMachine => bootstrapResult?.StateMachine;
        public RunCatalog Catalog => bootstrapResult?.Catalog;
        public IReadOnlyDictionary<string, CardDefinition> CardCatalog => bootstrapResult?.Catalog.Cards;
        public IReadOnlyDictionary<string, FlipPhoneActionDefinition> ActionCatalog => bootstrapResult?.Catalog.Actions;
        public IReadOnlyDictionary<string, PerkDefinition> PerkCatalog => bootstrapResult?.Catalog.Perks;
        public IReadOnlyDictionary<string, RunEventDefinition> EventCatalog => bootstrapResult?.Catalog.Events;
        public IReadOnlyDictionary<string, QuestDefinition> QuestCatalog => bootstrapResult?.Catalog.Quests;
        public ProfileState CurrentProfile => bootstrapResult?.ProfileService.CurrentProfile;
        public RunProfileService ProfileService => bootstrapResult?.ProfileService;
        public RunFlowCoordinator FlowCoordinator => flowCoordinator;
        public FlipPhoneCoordinator FlipPhoneCoordinator => flipPhoneCoordinator;
        public ScenePresentationBindings PresentationBindings => presentationBindings;
        public UIModalCoordinator ModalCoordinator => flowCoordinator?.ModalCoordinator;

        private RunBootstrapResult bootstrapResult;
        private RunFlowCoordinator flowCoordinator;
        private FlipPhoneCoordinator flipPhoneCoordinator;
        private LegacyCompatibilityBridge legacyBridge;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            ValidateBindingsOnAwake();

            legacyBridge = new LegacyCompatibilityBridge();
            legacyBridge.SuppressLegacyCanvas(disableLegacyCanvas);

            // 1. Inicializa Catálogo, Perfil e Máquina de Estados
            bootstrapResult = RunBootstrap.CreateAndInitializeRun(
                tutorialCards,
                startingCards,
                catalogCards,
                perksCatalog,
                eventsCatalog,
                questsCatalog,
                endingsCatalog,
                startingActions,
                playTutorial,
                customSeed
            );

            // 2. Inicializa o Coordenador do Flip-Phone
            flipPhoneCoordinator = new FlipPhoneCoordinator(
                bootstrapResult.StateMachine,
                bootstrapResult.Catalog,
                presentationBindings.FlipPhonePresenter,
                presentationBindings.RetroMonitorPresenter,
                presentationBindings.DecisionOverlayPresenter
            );

            // 3. Inicializa o Coordenador de Fluxo da Run
            // flowCoordinatorRef deve ser um componente já presente na cena e atribuído no Inspector.
            // GetComponent é o único fallback aceito — AddComponent dinâmico foi removido para evitar
            // MissingReferenceException no Inspector ao entrar em Play Mode.
            flowCoordinator = flowCoordinatorRef != null
                ? flowCoordinatorRef
                : GetComponent<RunFlowCoordinator>();

            if (flowCoordinator == null)
            {
                Debug.LogError("[MandatoBootstrap] RunFlowCoordinator não encontrado! Adicione o componente ao GameObject na cena e atribua o campo 'Flow Coordinator Ref' no Inspector.", this);
                return;
            }
            flowCoordinator.Initialize(
                bootstrapResult.StateMachine,
                bootstrapResult.Catalog,
                bootstrapResult.ProfileService,
                presentationBindings,
                flipPhoneCoordinator,
                legacyBridge,
                requireSpaceToCallNextNpc,
                callNextNpcKey,
                delayBetweenProposals,
                mainMenuSceneName
            );

            HookPhoneFocusableObject();
        }

        private void Start()
        {
            legacyBridge?.SyncCameraEffects(StateMachine?.RunState?.stats, instant: true);
            flowCoordinator?.StartFlow();
        }

        private void ValidateBindingsOnAwake()
        {
            if (presentationBindings == null)
            {
                Debug.LogError("<color=#ff4444>[MandatoBootstrap]</color> ❌ ScenePresentationBindings não está instanciado.");
                return;
            }

            if (!presentationBindings.Validate(out var missingList))
            {
                foreach (var err in missingList)
                {
                    Debug.LogWarning($"<color=#ffaa00>[MandatoBootstrap]</color> ⚠️ {err}");
                }
            }
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

                if (focusType != null && presentationBindings.FlipPhoneObject != null)
                {
                    var focusComp = presentationBindings.FlipPhoneObject.GetComponent(focusType) as MonoBehaviour;
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
                    }
                }
            }
            catch { }
        }

        public void OpenFlipPhone() => flipPhoneCoordinator?.OpenPhone();
        public void CloseFlipPhone() => flipPhoneCoordinator?.ClosePhone();
        public void AuthorizeNextVisitor() => flowCoordinator?.AuthorizeNextVisitor();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(mainMenuSceneName))
                mainMenuSceneName = "MenuV2";

            // Auto-detecta RunFlowCoordinator no mesmo GameObject
            if (flowCoordinatorRef == null)
                flowCoordinatorRef = GetComponent<RunFlowCoordinator>();

            if (presentationBindings != null && !presentationBindings.Validate(out var missingList))
            {
                if (missingList.Count > 0)
                {
                    // Apenas aviso no console se chamado manualmente
                }
            }
        }
#endif
    }
}
