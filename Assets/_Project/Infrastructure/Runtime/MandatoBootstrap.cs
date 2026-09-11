using System;
using System.Collections;
using System.Collections.Generic;
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

        [Header("Semente e Configurações")]
        [SerializeField] private int customSeed = 0;
        [SerializeField] private string mainMenuSceneName = "MenuV2";
        [Tooltip("Intervalo em segundos entre a saída do NPC e a entrada do próximo.")]
        [SerializeField] private float delayBetweenProposals = 1.5f;

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

        [Header("Configuração de UI")]
        [Tooltip("Se verdadeiro, desativa os elementos visuais do Canvas legado para rodar 100% em UI Toolkit.")]
        public bool disableLegacyCanvas = true;

        public RunStateMachine StateMachine { get; private set; }
        public Dictionary<string, CardDefinition> CardCatalog { get; private set; } = new Dictionary<string, CardDefinition>();
        public ProfileState CurrentProfile { get; private set; }

        private List<string> tutorialCardIds = new List<string>();
        private List<string> mainDeckCardIds = new List<string>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            AutoDetectPresenters();
            ApplyLegacyCanvasSuppression();
            CurrentProfile = SaveSystem.LoadProfile();
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
                paperPresenter = FindFirstObjectByType<PaperDocumentPresenter>();
                if (paperPresenter == null)
                {
                    var paperObj = GameObject.Find("Papel") ?? GameObject.Find("Paper");
                    if (paperObj != null)
                    {
                        paperPresenter = paperObj.AddComponent<PaperDocumentPresenter>();
                    }
                    else
                    {
                        paperPresenter = gameObject.AddComponent<PaperDocumentPresenter>();
                    }
                }
            }

            // 3. Monitor Retrô
            if (retroMonitorPresenter == null)
            {
                retroMonitorPresenter = FindFirstObjectByType<RetroMonitorPresenter>();
                if (retroMonitorPresenter == null)
                {
                    var monitorObj = GameObject.Find("Monitor") ?? GameObject.Find("RetroMonitor");
                    if (monitorObj != null)
                    {
                        retroMonitorPresenter = monitorObj.AddComponent<RetroMonitorPresenter>();
                    }
                    else
                    {
                        retroMonitorPresenter = gameObject.AddComponent<RetroMonitorPresenter>();
                    }
                }
            }

            // 4. Interface de Decisão
            if (decisionOverlayPresenter == null)
            {
                decisionOverlayPresenter = FindFirstObjectByType<DecisionOverlayPresenter>();
                if (decisionOverlayPresenter == null)
                {
                    var canvasObj = GameObject.Find("Canvas") ?? GameObject.Find("UIManager");
                    if (canvasObj != null)
                    {
                        decisionOverlayPresenter = canvasObj.AddComponent<DecisionOverlayPresenter>();
                    }
                    else
                    {
                        decisionOverlayPresenter = gameObject.AddComponent<DecisionOverlayPresenter>();
                    }
                }
            }

            // 5. Tela de Fim de Jogo
            if (endScreenPresenter == null)
            {
                endScreenPresenter = FindFirstObjectByType<EndScreenPresenter>();
                if (endScreenPresenter == null)
                {
                    endScreenPresenter = gameObject.AddComponent<EndScreenPresenter>();
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
        }

        private void BuildCatalog()
        {
            CardCatalog.Clear();
            tutorialCardIds.Clear();
            mainDeckCardIds.Clear();

            // 1. Processa cartas de tutorial
            if (playTutorial && tutorialDealsOrCards != null)
            {
                foreach (var asset in tutorialDealsOrCards)
                {
                    if (asset == null) continue;
                    var card = RegisterAssetInCatalog(asset);
                    if (card != null && !tutorialCardIds.Contains(card.id))
                    {
                        tutorialCardIds.Add(card.id);
                    }
                }
            }

            // 2. Processa cartas do baralho principal
            if (startingDealsOrCards != null && startingDealsOrCards.Count > 0)
            {
                foreach (var asset in startingDealsOrCards)
                {
                    if (asset == null) continue;
                    var card = RegisterAssetInCatalog(asset);
                    if (card != null && !mainDeckCardIds.Contains(card.id))
                    {
                        mainDeckCardIds.Add(card.id);
                    }
                }
            }
        }

        private CardDefinition RegisterAssetInCatalog(ScriptableObject asset)
        {
            if (asset == null) return null;

            CardDefinition card = null;
            if (asset is CardDefinition cardDef)
            {
                card = cardDef;
            }
            else
            {
                card = LegacyDealAdapter.ConvertToCardDefinition(asset);
            }

            if (card != null && !string.IsNullOrEmpty(card.id))
            {
                CardCatalog[card.id] = card;
            }

            return card;
        }

        private void InitializeStateMachine()
        {
            int seed = customSeed != 0 ? customSeed : UnityEngine.Random.Range(1, 100000);
            
            var priorityIds = (playTutorial && tutorialCardIds.Count > 0) ? tutorialCardIds : null;

            StateMachine = new RunStateMachine(seed: seed);
            StateMachine.StartRun(mainDeckCardIds, seed, priorityIds);
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
        }

        public bool IsTutorialCard(CardDefinition card)
        {
            if (card == null) return false;
            return IsTutorialCard(card.id) ||
                   card.categoryTag.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   card.title.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public bool IsTutorialCard(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return false;
            if (tutorialCardIds != null && tutorialCardIds.Contains(cardId)) return true;
            return cardId.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public bool HasRemainingTutorialCards()
        {
            if (StateMachine == null || StateMachine.DeckState == null || StateMachine.DeckState.drawPile == null) return false;
            foreach (var cardId in StateMachine.DeckState.drawPile)
            {
                if (IsTutorialCard(cardId)) return true;
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

            // 3. Submete a escolha para resolução de estado
            StateMachine.SubmitChoice(choiceIndex);
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

            // Verifica se a run terminou
            if (StateMachine.RunState.termination.IsDefeat || StateMachine.RunState.termination.IsVictory)
            {
                return;
            }

            SyncCanvasUI();

            // Tutorial avança imediatamente sem delay de saída de NPC
            bool isTutorial = report != null ? IsTutorialCard(report.cardId) : IsTutorialCard(StateMachine.CurrentCard);
            float delay = isTutorial ? 0.05f : Mathf.Max(0.2f, delayBetweenProposals);

            // Puxa a próxima proposta do mandato
            StartCoroutine(DrawNextProposalRoutine(delay));
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

        private IEnumerator DrawNextProposalRoutine(float delay = 0.2f)
        {
            yield return new WaitForSeconds(delay);

            if (StateMachine != null && CardCatalog.Count > 0)
            {
                StateMachine.DrawAndPresentProposal(CardCatalog);
            }
        }

        private void OnRunTerminated(RunTermination termination)
        {
            if (StateMachine == null) return;

            RunSnapshot finalSnapshot = StateMachine.RunState.GetSnapshot();
            EndingDefinition evaluatedEnding = null;

            try
            {
                evaluatedEnding = EndingEvaluator.EvaluateEnding(StateMachine.RunState, endingsCatalog);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MandatoBootstrap] Erro ao avaliar final: {ex.Message}");
            }

            // 1. Registra e salva metaprogressão
            if (CurrentProfile != null)
            {
                try
                {
                    CurrentProfile.RecordRunCompleted(termination.IsVictory, evaluatedEnding?.id);
                    SaveSystem.SaveProfile(CurrentProfile);
                }
                catch { }
            }

            // 2. Limpa papel e overlay de decisão
            if (paperPresenter != null)
            {
                paperPresenter.Clear();
                paperPresenter.SetPaperInteractable(false);
            }

            if (decisionOverlayPresenter != null)
            {
                decisionOverlayPresenter.ClearChoices();
            }

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

        private void PlayPlayerDealAnimation()
        {
            if (playerAnimator == null || playerAnimator.runtimeAnimatorController == null) return;

            if (playerAnimRoutine != null)
            {
                StopCoroutine(playerAnimRoutine);
                playerAnimRoutine = null;
            }

            playerAnimator.speed = 1f;

            if (!string.IsNullOrEmpty(dealAnimationName) && (playerAnimator.HasState(0, Animator.StringToHash(dealAnimationName)) || playerAnimator.HasState(0, Animator.StringToHash("Base Layer." + dealAnimationName))))
            {
                playerAnimator.Play(dealAnimationName, 0, 0f);
            }
        }

        private void PlayPlayerDealAnimationReverse()
        {
            if (playerAnimator == null || playerAnimator.runtimeAnimatorController == null) return;

            if (playerAnimRoutine != null)
            {
                StopCoroutine(playerAnimRoutine);
                playerAnimRoutine = null;
            }

            playerAnimRoutine = StartCoroutine(PlayDealAnimationReverseRoutine());
        }

        private IEnumerator PlayDealAnimationReverseRoutine()
        {
            if (playerAnimator == null) yield break;

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
        }
    }
}
