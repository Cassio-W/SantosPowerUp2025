using System;
using System.Collections;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;
using Mandato.Presentation;
using Mandato.Run;
using Mandato.UI;
using UnityEngine;
using UnityEngine.AI;

namespace Mandato.Infrastructure
{
    /// <summary>
    /// Configuração individual de cada passo/texto do tutorial.
    /// Permite customizar o ScriptableObject da carta, a animação do NPC e o foco da câmera em cada diálogo.
    /// </summary>
    [Serializable]
    public class TutorialStepConfig
    {
        [Tooltip("ScriptableObject da Carta de Tutorial (CardDefinition) correspondente a este passo.")]
        public CardDefinition card;

        [Header("--- Animação do NPC ---")]
        [Tooltip("Nome do estado ou parâmetro no Animator do NPC (ex: 'Conversando', 'Apontando', 'Idle'). Deixe vazio para manter a animação atual.")]
        public string npcAnimationState = "";

        [Tooltip("Tempo de espera opcional após disparar a animação.")]
        public float animationDelay = 0f;

        [Header("--- Foco da Câmera ---")]
        [Tooltip("Ponto de foco da câmera para este texto (Transform com posição e rotação desejadas).")]
        public Transform cameraFocusPoint;

        [Tooltip("Objeto interativo da cena a ser focado pela câmera (opcional, estilo FocusableObject).")]
        public FocusableObject focusableTarget;

        [Tooltip("Campo de Visão (FOV) da câmera neste passo (-1 para usar o padrão).")]
        [Range(-1f, 120f)]
        public float targetCameraFov = -1f;

        [Tooltip("Duração da transição da câmera em segundos (-1 para usar o padrão).")]
        public float cameraTransitionDuration = -1f;

        [Tooltip("Se ativo, habilita o efeito de pós-processamento de foco / blur de borda da câmera.")]
        public bool enableCameraEffect = false;
    }

    /// <summary>
    /// Gerenciador dedicado do fluxo de Tutorial do MANDATO.
    /// Instancia um NPC específico, comanda a caminhada até a mesa, sincroniza animações e
    /// foco de câmera para cada texto do tutorial, e comanda a saída do NPC ao finalizar.
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialManager : MonoBehaviour
    {
        [Header("--- Ativação do Tutorial ---")]
        [Tooltip("Se verdadeiro, o tutorial será executado no início da partida.")]
        [SerializeField] private bool playTutorial = true;

        [Header("--- NPC 3D do Tutorial ---")]
        [Tooltip("Nome do NPC do tutorial exibido no balão de fala (badge do interlocutor).")]
        [SerializeField] private string tutorialNpcName = "ASSESSOR";

        [Tooltip("Prefab do NPC exclusivo que será instanciado no tutorial.")]
        [SerializeField] private GameObject tutorialNpcPrefab;

        [Tooltip("Ponto de spawn inicial do NPC do tutorial (ex: porta).")]
        [SerializeField] private Transform spawnPoint;

        [Tooltip("Ponto de destino do NPC na mesa.")]
        [SerializeField] private Transform tableTargetPoint;

        [Tooltip("Ponto de saída do NPC ao término do tutorial.")]
        [SerializeField] private Transform exitTargetPoint;

        [Header("--- Posições de Fallback ---")]
        [SerializeField] private Vector3 fallbackSpawnPosition = new Vector3(5.85f, 0f, 3.65f);
        [SerializeField] private Vector3 fallbackTablePosition = new Vector3(0f, 0f, -1.5f);
        [SerializeField] private Vector3 fallbackExitPosition = new Vector3(5.85f, 0f, 3.65f);

        [Header("--- Animações Globais do Tutorial ---")]
        [Tooltip("Animação opcional tocada assim que o NPC chega na mesa (deixe vazio se não houver).")]
        [SerializeField] private string arrivalAnimation = "";

        [Tooltip("Animação tocada durante a caminhada de saída.")]
        [SerializeField] private string exitAnimation = "Walk";

        [Tooltip("Tempo limite máximo de espera da caminhada até a mesa em segundos.")]
        [SerializeField] private float walkArrivalTimeout = 15f;

        [Tooltip("Tempo limite máximo de espera da caminhada até a saída em segundos.")]
        [SerializeField] private float exitTimeout = 12f;

        [Header("--- Passos e Diálogos do Tutorial ---")]
        [Tooltip("Lista de configurações de animação e câmera para cada texto/carta de tutorial.")]
        [SerializeField] private List<TutorialStepConfig> stepConfigurations = new List<TutorialStepConfig>();

        [Header("--- Apresentadores e UI ---")]
        [SerializeField] private NpcSpeechBubblePresenter speechBubble;
        [SerializeField] private CameraFocusManager cameraFocusManager;
        [SerializeField] private PaperDocumentPresenter paperPresenter;
        [SerializeField] private DecisionOverlayPresenter decisionOverlay;

        private RunStateMachine stateMachine;
        private RunCatalog catalog;

        private GameObject activeNpcGameObject;
        private INpcController activeNpcController;
        private Animator activeNpcAnimator;

        private int currentStepIndex = 0;
        private bool isTutorialActive = false;
        private bool isNpcAtTable = false;
        private string defaultTutorialSpeaker = "GUIA DO MANDATO";
        private Coroutine activeTutorialRoutine;

        public string TutorialNpcName
        {
            get => tutorialNpcName;
            set => tutorialNpcName = value;
        }

        public bool PlayTutorial
        {
            get => playTutorial;
            set => playTutorial = value;
        }

        public bool IsTutorialActive => isTutorialActive;
        public int CurrentStepIndex => currentStepIndex;
        public NpcSpeechBubblePresenter SpeechBubble => speechBubble;
        public GameObject ActiveNpcGameObject => activeNpcGameObject;
        public List<TutorialStepConfig> StepConfigurations => stepConfigurations;

        public event Action OnTutorialStarted;
        public event Action<CardDefinition, int> OnTutorialStepStarted;
        public event Action<int> OnTutorialStepCompleted;
        public event Action OnTutorialFinished;

        private void Awake()
        {
            if (speechBubble == null)
            {
                speechBubble = FindFirstObjectByType<NpcSpeechBubblePresenter>(FindObjectsInactive.Include);
            }
            if (speechBubble != null)
            {
                speechBubble.Hide();
            }
        }

        public void Initialize(
            RunStateMachine stateMachine,
            RunCatalog catalog,
            NpcSpeechBubblePresenter speechBubble = null,
            PaperDocumentPresenter paperPresenter = null,
            DecisionOverlayPresenter decisionOverlay = null,
            CameraFocusManager cameraFocus = null)
        {
            this.stateMachine = stateMachine;
            this.catalog = catalog;

            if (speechBubble != null) this.speechBubble = speechBubble;
            if (paperPresenter != null) this.paperPresenter = paperPresenter;
            if (decisionOverlay != null) this.decisionOverlay = decisionOverlay;
            if (cameraFocus != null) this.cameraFocusManager = cameraFocus;

            if (this.speechBubble == null)
            {
                this.speechBubble = FindFirstObjectByType<NpcSpeechBubblePresenter>(FindObjectsInactive.Include);
            }
            if (this.cameraFocusManager == null)
            {
                this.cameraFocusManager = CameraFocusManager.Instance ?? FindFirstObjectByType<CameraFocusManager>(FindObjectsInactive.Include);
            }

            this.currentStepIndex = 0;
            this.isTutorialActive = false;
            this.isNpcAtTable = false;

            if (this.speechBubble != null)
            {
                this.speechBubble.Hide();
                this.speechBubble.OnSpeechCompleted -= HandleSpeechCompleted;
                this.speechBubble.OnSpeechCompleted += HandleSpeechCompleted;
            }
        }

        public void SetDefaultTutorialSpeaker(string speakerName)
        {
            tutorialNpcName = speakerName;
            defaultTutorialSpeaker = speakerName;
        }

        /// <summary>
        /// Retorna a lista de CardDefinition configuradas nos passos do tutorial.
        /// </summary>
        public List<CardDefinition> GetConfiguredCards()
        {
            var list = new List<CardDefinition>();
            if (stepConfigurations != null)
            {
                foreach (var step in stepConfigurations)
                {
                    if (step != null && step.card != null && !list.Contains(step.card))
                    {
                        list.Add(step.card);
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Verifica se a proposta é uma carta de tutorial.
        /// </summary>
        public bool IsTutorialCard(CardDefinition card)
        {
            if (card == null) return false;
            if (card.isTutorial) return true;

            if (stepConfigurations != null)
            {
                foreach (var step in stepConfigurations)
                {
                    if (step != null && step.card != null)
                    {
                        if (step.card == card || (!string.IsNullOrEmpty(step.card.id) && string.Equals(step.card.id, card.id, StringComparison.OrdinalIgnoreCase)))
                        {
                            return true;
                        }
                    }
                }
            }

            if (catalog != null && catalog.IsTutorialCardId(card.id))
            {
                return true;
            }

            return card.categoryTag.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   card.title.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   card.id.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Verifica se ainda há cartas de tutorial na fila de compras prioritárias.
        /// </summary>
        public bool HasRemainingTutorialCards()
        {
            if (stateMachine == null || stateMachine.DeckState == null) return false;

            if (stateMachine.DeckState.priorityDrawPile != null)
            {
                foreach (var cardId in stateMachine.DeckState.priorityDrawPile)
                {
                    if (catalog != null && catalog.IsTutorialCardId(cardId)) return true;
                    if (catalog != null && catalog.Cards.TryGetValue(cardId, out var c) && IsTutorialCard(c)) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Apresenta o passo do tutorial. Se for o início, instancia o NPC 3D e comanda
        /// sua caminhada até a mesa antes de exibir a UI.
        /// </summary>
        public void PresentTutorialStep(CardDefinition card)
        {
            if (card == null) return;

            if (!isTutorialActive)
            {
                isTutorialActive = true;
                OnTutorialStarted?.Invoke();
            }

            // Suprime papel 3D e botões do DecisionUI
            if (paperPresenter != null)
            {
                paperPresenter.SetPaperActive(false);
                paperPresenter.SetPaperInteractable(false);
            }

            if (decisionOverlay != null)
            {
                decisionOverlay.ClearChoices();
                decisionOverlay.SetVisible(false);
            }

            // Se o NPC ainda não foi instanciado e há prefab atribuído, comanda spawn e caminhada
            if (activeNpcGameObject == null && tutorialNpcPrefab != null && gameObject.activeInHierarchy)
            {
                if (activeTutorialRoutine != null) StopCoroutine(activeTutorialRoutine);
                activeTutorialRoutine = StartCoroutine(SpawnAndWalkToTableRoutine(card));
            }
            else
            {
                DisplayStepDirectly(card, currentStepIndex);
            }
        }

        private IEnumerator SpawnAndWalkToTableRoutine(CardDefinition initialCard)
        {
            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : fallbackSpawnPosition;
            Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
            Vector3 tablePos = tableTargetPoint != null ? tableTargetPoint.position : fallbackTablePosition;

            activeNpcGameObject = Instantiate(tutorialNpcPrefab, spawnPos, spawnRot);

            var navAgent = activeNpcGameObject.GetComponent<NavMeshAgent>();
            if (navAgent != null)
            {
                navAgent.Warp(spawnPos);
            }

            activeNpcController = activeNpcGameObject.GetComponent<INpcController>();
            activeNpcAnimator = activeNpcGameObject.GetComponent<Animator>() ?? activeNpcGameObject.GetComponentInChildren<Animator>();

            if (activeNpcController != null)
            {
                activeNpcController.SetPositions(spawnPos, tablePos);
                activeNpcController.MoveToTable();

                float elapsed = 0f;
                while (activeNpcGameObject != null && elapsed < walkArrivalTimeout)
                {
                    elapsed += Time.deltaTime;
                    if (activeNpcController.IsReadyForDismissal()) break;
                    yield return null;
                }
            }
            else
            {
                // Fallback: se não tiver INpcController, aguarda delay curto
                yield return new WaitForSeconds(0.8f);
            }

            isNpcAtTable = true;

            if (!string.IsNullOrEmpty(arrivalAnimation))
            {
                PlayAnimationOnNpc(arrivalAnimation);
            }

            yield return new WaitForSeconds(0.1f);

            DisplayStepDirectly(initialCard, currentStepIndex);
            activeTutorialRoutine = null;
        }

        private void DisplayStepDirectly(CardDefinition card, int stepIndex)
        {
            TutorialStepConfig config = GetStepConfig(stepIndex, card);

            // 1. Aplica animação configurada no NPC
            if (config != null && !string.IsNullOrEmpty(config.npcAnimationState))
            {
                PlayAnimationOnNpc(config.npcAnimationState);
            }

            // 2. Aplica foco de câmera configurado
            ApplyCameraFocus(config);

            // 3. Formata e exibe o balão de fala (usa o nome do NPC configurado no TutorialManager)
            string speaker = !string.IsNullOrWhiteSpace(tutorialNpcName)
                ? tutorialNpcName
                : (tutorialNpcPrefab != null ? tutorialNpcPrefab.name : (!string.IsNullOrEmpty(card.title) ? card.title : defaultTutorialSpeaker));

            string dialogue = !string.IsNullOrEmpty(card.FormattedDescription) ? card.FormattedDescription : card.description;

            if (speechBubble != null)
            {
                speechBubble.Show(speaker, dialogue, useTypewriter: false);
            }

            OnTutorialStepStarted?.Invoke(card, stepIndex);
        }

        private void ApplyCameraFocus(TutorialStepConfig config)
        {
            var cam = cameraFocusManager ?? CameraFocusManager.Instance;
            if (cam == null) return;

            if (config != null)
            {
                if (config.focusableTarget != null)
                {
                    cam.Focus(config.focusableTarget);
                    return;
                }

                if (config.cameraFocusPoint != null)
                {
                    cam.FocusPoint(
                        config.cameraFocusPoint,
                        config.targetCameraFov,
                        config.cameraTransitionDuration,
                        config.enableCameraEffect
                    );
                    return;
                }
            }

            // Se não há foco específico configurado para este passo, mantém a visão geral
            cam.Unfocus();
        }

        private void PlayAnimationOnNpc(string animationState)
        {
            if (string.IsNullOrWhiteSpace(animationState) || activeNpcGameObject == null) return;

            if (activeNpcAnimator == null)
            {
                activeNpcAnimator = activeNpcGameObject.GetComponent<Animator>() ?? activeNpcGameObject.GetComponentInChildren<Animator>();
            }

            if (activeNpcAnimator == null || activeNpcAnimator.runtimeAnimatorController == null) return;

            int stateHash = Animator.StringToHash(animationState);

            // 1. Verifica se o estado existe na layer 0 ou em qualquer outra layer do Animator
            for (int layer = 0; layer < activeNpcAnimator.layerCount; layer++)
            {
                if (activeNpcAnimator.HasState(layer, stateHash))
                {
                    activeNpcAnimator.Play(stateHash, layer, 0f);
                    return;
                }
            }

            // 2. Se não encontrou o estado, verifica se é um Trigger ou Bool no Animator
            foreach (var param in activeNpcAnimator.parameters)
            {
                if (string.Equals(param.name, animationState, StringComparison.OrdinalIgnoreCase))
                {
                    if (param.type == AnimatorControllerParameterType.Trigger)
                    {
                        activeNpcAnimator.SetTrigger(param.nameHash);
                        return;
                    }
                    else if (param.type == AnimatorControllerParameterType.Bool)
                    {
                        activeNpcAnimator.SetBool(param.nameHash, true);
                        return;
                    }
                }
            }

            Debug.LogWarning($"[TutorialManager] Estado de animação ou parâmetro '{animationState}' não encontrado no Animator do NPC.");
        }

        private TutorialStepConfig GetStepConfig(int index, CardDefinition card = null)
        {
            if (stepConfigurations != null)
            {
                // 1. Prioridade: busca por referência direta de ScriptableObject ou ID
                if (card != null)
                {
                    var match = stepConfigurations.Find(s => s != null && (s.card == card || (!string.IsNullOrEmpty(s.card?.id) && string.Equals(s.card.id, card.id, StringComparison.OrdinalIgnoreCase))));
                    if (match != null) return match;
                }

                // 2. Fallback por índice do passo na lista
                if (index >= 0 && index < stepConfigurations.Count)
                {
                    return stepConfigurations[index];
                }
            }
            return null;
        }

        /// <summary>
        /// Trata a conclusão do balão de fala (clique em Avançar ou tecla de atalho).
        /// </summary>
        private void HandleSpeechCompleted()
        {
            if (!isTutorialActive) return;

            int completedStep = currentStepIndex;
            currentStepIndex++;
            OnTutorialStepCompleted?.Invoke(completedStep);

            if (HasRemainingTutorialCards())
            {
                // Avança para a próxima carta de tutorial
                stateMachine?.SubmitChoice(0, catalog?.Quests, catalog?.Perks);
            }
            else
            {
                // Último passo finalizado: inicia saída do NPC e retorno ao jogo
                if (gameObject.activeInHierarchy)
                {
                    if (activeTutorialRoutine != null) StopCoroutine(activeTutorialRoutine);
                    activeTutorialRoutine = StartCoroutine(ExitAndCompleteTutorialRoutine());
                }
                else
                {
                    CompleteTutorial();
                    stateMachine?.SubmitChoice(0, catalog?.Quests, catalog?.Perks);
                }
            }
        }

        private IEnumerator ExitAndCompleteTutorialRoutine()
        {
            // 1. Oculta a UI do balão de fala
            if (speechBubble != null)
            {
                speechBubble.Hide();
            }

            // 2. Retorna a câmera para a visão geral
            var cam = cameraFocusManager ?? CameraFocusManager.Instance;
            cam?.Unfocus();

            // 3. Comanda a saída do NPC
            if (activeNpcGameObject != null)
            {
                if (activeNpcController != null)
                {
                    activeNpcController.MoveToExit();

                    float elapsed = 0f;
                    while (activeNpcGameObject != null && elapsed < exitTimeout)
                    {
                        elapsed += Time.deltaTime;
                        yield return null;
                    }
                }
                else
                {
                    yield return new WaitForSeconds(0.5f);
                }

                if (activeNpcGameObject != null)
                {
                    Destroy(activeNpcGameObject);
                    activeNpcGameObject = null;
                }
            }

            activeNpcController = null;
            activeNpcAnimator = null;
            isNpcAtTable = false;

            CompleteTutorial();

            // Submete a última escolha para concluir o turno do tutorial na máquina de estados
            stateMachine?.SubmitChoice(0, catalog?.Quests, catalog?.Perks);
            activeTutorialRoutine = null;
        }

        /// <summary>
        /// Finaliza a sessão de tutorial e restaura a interface para as propostas regulares.
        /// </summary>
        public void CompleteTutorial()
        {
            if (!isTutorialActive) return;

            isTutorialActive = false;

            if (speechBubble != null)
            {
                speechBubble.Hide();
            }

            if (decisionOverlay != null)
            {
                decisionOverlay.SetVisible(true);
            }

            var cam = cameraFocusManager ?? CameraFocusManager.Instance;
            cam?.Unfocus();

            OnTutorialFinished?.Invoke();
        }

        /// <summary>
        /// Força o cancelamento ou pulo do tutorial.
        /// </summary>
        public void AbortTutorial()
        {
            if (activeTutorialRoutine != null)
            {
                StopCoroutine(activeTutorialRoutine);
                activeTutorialRoutine = null;
            }

            if (activeNpcGameObject != null)
            {
                Destroy(activeNpcGameObject);
                activeNpcGameObject = null;
            }

            CompleteTutorial();
        }

        private void OnDestroy()
        {
            if (speechBubble != null)
            {
                speechBubble.OnSpeechCompleted -= HandleSpeechCompleted;
            }

            if (activeNpcGameObject != null)
            {
                Destroy(activeNpcGameObject);
                activeNpcGameObject = null;
            }
        }
    }
}
