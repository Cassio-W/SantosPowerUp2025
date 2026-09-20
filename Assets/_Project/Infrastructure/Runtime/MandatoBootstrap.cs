using System;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;
using Mandato.Presentation;
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

        [Header("Conteúdo da Partida")]
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

        [Header("Personagens Jogáveis")]
        [Tooltip("Lista de todos os personagens disponíveis para seleção no menu. O id salvo é resolvido aqui.")]
        [SerializeField] private List<CharacterDefinition> availableCharacters = new List<CharacterDefinition>();

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

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            ValidateBindingsOnAwake();

            // Obtém as cartas de tutorial e estado de ativação exclusivamente do TutorialManager
            bool shouldPlayTutorial = presentationBindings?.TutorialManager != null && presentationBindings.TutorialManager.PlayTutorial;
            var activeTutorialCards = presentationBindings?.TutorialManager != null
                ? presentationBindings.TutorialManager.GetConfiguredCards()
                : new List<CardDefinition>();

            // 1. Resolve o personagem selecionado no menu (pode ser null se nenhum foi escolhido)
            var selectedCharacter = ResolveSelectedCharacter();

            // 2. Inicializa Catálogo, Perfil e Máquina de Estados (com personagem, se houver)
            bootstrapResult = RunBootstrap.CreateAndInitializeRun(
                activeTutorialCards,
                startingCards,
                catalogCards,
                perksCatalog,
                eventsCatalog,
                questsCatalog,
                endingsCatalog,
                startingActions,
                shouldPlayTutorial,
                customSeed,
                selectedCharacter
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
                requireSpaceToCallNextNpc,
                callNextNpcKey,
                delayBetweenProposals,
                mainMenuSceneName
            );

            HookPhoneFocusableObject();
            HookMonitorFocusableObject();
        }

        private void Start()
        {
            presentationBindings?.CameraEffects?.ApplyAttributeEffects(StateMachine?.RunState?.stats, instant: true);
            flowCoordinator?.StartFlow();
        }

        private void Update()
        {
            var phone = presentationBindings?.FlipPhonePresenter;
            if (phone != null && phone.AllowKeyboardToggle && phone.IsInteractable && Input.GetKeyDown(phone.ToggleKey))
            {
                ToggleFlipPhone();
            }
            else if (phone != null && phone.IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseFlipPhone();
            }
        }

        /// <summary>
        /// Resolve a CharacterDefinition com base no id salvo via CharacterSelectionPersistence.
        /// Se a lista availableCharacters estiver vazia no Inspector, tenta carregar automaticamente.
        /// Realiza busca resiliente por ID, nome de arquivo do ScriptableObject e displayName.
        /// </summary>
        private CharacterDefinition ResolveSelectedCharacter()
        {
            if (availableCharacters == null || availableCharacters.Count == 0)
            {
                availableCharacters = new List<CharacterDefinition>();

                // 1. Tenta carregar via Resources se houver
                var loaded = Resources.LoadAll<CharacterDefinition>("");
                if (loaded != null && loaded.Length > 0)
                {
                    availableCharacters.AddRange(loaded);
                }

#if UNITY_EDITOR
                // 2. No editor, busca todos os assets CharacterDefinition caso a lista não tenha sido serializada
                if (availableCharacters.Count == 0)
                {
                    string[] guids = UnityEditor.AssetDatabase.FindAssets("t:CharacterDefinition");
                    foreach (var g in guids)
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                        var charDef = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
                        if (charDef != null && !availableCharacters.Contains(charDef))
                        {
                            availableCharacters.Add(charDef);
                        }
                    }
                }
#endif
            }

            if (availableCharacters == null || availableCharacters.Count == 0)
            {
                Debug.LogWarning("[MandatoBootstrap] ⚠️ Nenhuma CharacterDefinition encontrada ou configurada. Partida iniciará com atributos padrão.");
                return null;
            }

            string savedId = CharacterSelectionPersistence.Load();
            CharacterDefinition resolved = null;

            if (!string.IsNullOrEmpty(savedId))
            {
                // Busca por ID exato ou case-insensitive
                foreach (var c in availableCharacters)
                {
                    if (c != null && !string.IsNullOrEmpty(c.id) && string.Equals(c.id, savedId, StringComparison.OrdinalIgnoreCase))
                    {
                        resolved = c;
                        break;
                    }
                }

                // Fallback 1: Busca por nome do asset (ScriptableObject name)
                if (resolved == null)
                {
                    foreach (var c in availableCharacters)
                    {
                        if (c != null && string.Equals(c.name, savedId, StringComparison.OrdinalIgnoreCase))
                        {
                            resolved = c;
                            break;
                        }
                    }
                }

                // Fallback 2: Busca por displayName
                if (resolved == null)
                {
                    foreach (var c in availableCharacters)
                    {
                        if (c != null && !string.IsNullOrEmpty(c.displayName) && string.Equals(c.displayName, savedId, StringComparison.OrdinalIgnoreCase))
                        {
                            resolved = c;
                            break;
                        }
                    }
                }

                if (resolved == null)
                {
                    Debug.LogWarning($"[MandatoBootstrap] ⚠️ Personagem salvo '{savedId}' não encontrado em availableCharacters ({availableCharacters.Count} disponíveis). Usando o primeiro.");
                    resolved = availableCharacters[0];
                }
            }
            else
            {
                resolved = availableCharacters[0];
            }

            if (resolved != null)
            {
                var s = resolved.initialStats;
                string statsInfo = s != null ? $"Clima={s.climaticChanges}%, Economia={s.economy}%, Relações={s.internationalRelations}%, Povo={s.popularApproval}%" : "default";
                Debug.Log($"[MandatoBootstrap] ✅ Personagem inicial aplicado com sucesso: {resolved.displayName} (ID: '{resolved.id}') | Stats: {statsInfo}");
            }

            return resolved;
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
            if (presentationBindings?.FlipPhoneObject == null) return;

            var focusComp = presentationBindings.FlipPhoneObject.GetComponent<FocusableObject>();
            if (focusComp != null)
            {
                focusComp.onFocused.RemoveListener(OpenFlipPhone);
                focusComp.onFocused.AddListener(OpenFlipPhone);
                focusComp.onUnfocused.RemoveListener(CloseFlipPhone);
                focusComp.onUnfocused.AddListener(CloseFlipPhone);
            }
        }

        private void HookMonitorFocusableObject()
        {
            if (presentationBindings?.RetroMonitorPresenter == null) return;

            var focusComp = presentationBindings.RetroMonitorPresenter.GetComponent<FocusableObject>() ??
                            presentationBindings.RetroMonitorPresenter.GetComponentInChildren<FocusableObject>() ??
                            presentationBindings.RetroMonitorPresenter.GetComponentInParent<FocusableObject>();

            if (focusComp != null)
            {
                focusComp.onFocused.RemoveListener(OnMonitorFocused);
                focusComp.onFocused.AddListener(OnMonitorFocused);
                focusComp.onUnfocused.RemoveListener(OnMonitorUnfocused);
                focusComp.onUnfocused.AddListener(OnMonitorUnfocused);
            }
        }

        private void OnMonitorFocused() => flowCoordinator?.SetPcFocusState(true);
        private void OnMonitorUnfocused() => flowCoordinator?.SetPcFocusState(false);

        public void OpenFlipPhone() => flipPhoneCoordinator?.OpenPhone();
        public void CloseFlipPhone() => flipPhoneCoordinator?.ClosePhone();
        public void ToggleFlipPhone() => flipPhoneCoordinator?.TogglePhone();
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
