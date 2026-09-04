using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Slider = UnityEngine.UI.Slider;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    [Header("UI Toolkit - Botões de Decisão (Tela)")]
    [Tooltip("UIDocument responsável pela interface de decisão na tela (Aprovar / Rejeitar).")]
    public UIDocument decisionUIDocument;
    [Tooltip("VisualTreeAsset da interface de decisão (DecisionUI.uxml).")]
    public VisualTreeAsset decisionUxmlAsset;
    [Tooltip("PanelSettings para o overlay de tela do UI Toolkit.")]
    public PanelSettings decisionPanelSettings;

    [Header("Referências de UI (Legado / Canvas)")]
    public GameObject dealPanel; // Painel principal da proposta legado (Description Panel)
    [Tooltip("Se falso, o Description Panel legado do Canvas não é exibido na tela.")]
    public bool showDescriptionPanel = false;

    [Header("Papel Físico 3D (Cena)")]
    [Tooltip("Referência ao script do papel físico na cena (PhysicalPaperUI).")]
    public PhysicalPaperUI physicalPaper;
    [Tooltip("Texto de descrição da proposta no papel 3D (fallback).")]
    public TextMeshProUGUI paperDescriptionText;
    [Tooltip("Texto do nome/autor da proposta no papel 3D (fallback).")]
    public TextMeshProUGUI paperNameText;

    [Header("Botões de Decisão na Tela (Legado / Canvas Fallback)")]
    public GameObject decisionButtonsPanel;
    public UnityEngine.UI.Button approveButton;
    public UnityEngine.UI.Button rejectButton;
    public TextMeshProUGUI approveButtonText;
    public TextMeshProUGUI rejectButtonText;

    [Header("Animação do Player (Deal)")]
    [Tooltip("Referência ao Animator do Player. Se vazio, busca automaticamente na cena.")]
    public Animator playerAnimator;
    [Tooltip("Nome do estado da animação no Animator (ex: 'Levantando papel' ou 'LevantaMao').")]
    public string dealAnimationName = "LevantaMao";
    [Tooltip("Nome do estado default/idle no Animator.")]
    public string defaultAnimationName = "None";
    [Tooltip("Nome do estado reverso se existir no Animator (ex: 'AbaixaMao'). Se vazio, inverte dealAnimationName via código.")]
    public string dealAnimationReverseName = "";

    [Header("HUD Geral")]
    public GameObject datePanel;
    public GameObject corruptionPanel;
    public GameObject gameOverPanel;
    public Text nameText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI leftAnswerText;
    public TextMeshProUGUI rightAnswerText;
    public TextMeshProUGUI dateText;
    public TextMeshProUGUI gameOverText;

    [Header("Barras de Atributos")]
    public Slider climateSlider;
    public Slider relationsSlider;
    public Slider approvalSlider;
    public Slider economySlider;
    public Slider corruptionSlider;

    [Header("Perks")]
    public UnityEngine.UI.Image[] perks = new UnityEngine.UI.Image[3];

    [Header("Configurações")]
    public float animationSpeed = 2f;
    public LeanTweenType easeType;

    // Elementos internos do UI Toolkit
    private VisualElement _decisionContainer;
    private UnityEngine.UIElements.Button _btnApprove;
    private UnityEngine.UIElements.Button _btnReject;
    private Label _lblApproveText;
    private Label _lblRejectText;
    private bool _isProcessingDecision = false;
    private Deal _currentDeal;
    private Coroutine _dealAnimReverseCoroutine;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    private void OnEnable()
    {
        GameManager.OnNewDeal += HandleNewDeal;
        GameManager.OnChangeAttributes += HandleUpdatedAttributes;
        GameManager.OnGameOver += HandleGameOver;
        GameManager.OnGameWin += ShowGameWin;

        if (CameraFocusManager.Instance != null)
        {
            CameraFocusManager.Instance.OnObjectFocusChanged += HandleCameraFocusChanged;
        }
    }

    private void OnDestroy()
    {
        GameManager.OnNewDeal -= HandleNewDeal;
        GameManager.OnChangeAttributes -= HandleUpdatedAttributes;
        GameManager.OnGameOver -= HandleGameOver;
        GameManager.OnGameWin -= ShowGameWin;

        if (CameraFocusManager.Instance != null)
        {
            CameraFocusManager.Instance.OnObjectFocusChanged -= HandleCameraFocusChanged;
        }
    }

    private void Start()
    {
        _isProcessingDecision = false;

        // Inicializa o UI Toolkit para a interface de decisão
        SetupDecisionUIToolkit();

        // Garante inscrição no CameraFocusManager caso tenha sido inicializado no Awake/Start
        if (CameraFocusManager.Instance != null)
        {
            CameraFocusManager.Instance.OnObjectFocusChanged -= HandleCameraFocusChanged;
            CameraFocusManager.Instance.OnObjectFocusChanged += HandleCameraFocusChanged;
        }

        // Se dealPanel legado do Canvas existir, desativa o fundo e textos para não interferir
        if (dealPanel != null)
        {
            if (descriptionText != null) descriptionText.gameObject.SetActive(false);
            if (nameText != null && nameText.transform.parent != null) nameText.transform.parent.gameObject.SetActive(showDescriptionPanel);

            var panelBg = dealPanel.GetComponent<UnityEngine.UI.Image>();
            if (panelBg != null)
            {
                panelBg.enabled = false;
                panelBg.raycastTarget = false;
            }

            if (!showDescriptionPanel && _decisionContainer != null)
            {
                dealPanel.SetActive(false);
            }
        }

        if (decisionButtonsPanel != null)
        {
            decisionButtonsPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (GameManager.instance != null)
        {
            UpdateAttributes(GameManager.instance.gameAttributes);
        }

        // Atalhos de Teclado [A] para Aprovar / [D] para Rejeitar
        if (_decisionContainer != null && !_decisionContainer.ClassListContains("hidden") && !_isProcessingDecision)
        {
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                ApproveDeal();
            }
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                RejectDeal();
            }
        }
    }

    private void SetupDecisionUIToolkit()
    {
        if (decisionUIDocument == null)
        {
            decisionUIDocument = GetComponent<UIDocument>();
            if (decisionUIDocument == null)
            {
                UIDocument[] docs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
                foreach (var doc in docs)
                {
                    if (doc != null && doc.visualTreeAsset != null && doc.visualTreeAsset.name.Contains("Decision"))
                    {
                        decisionUIDocument = doc;
                        break;
                    }
                }
            }
        }

        // Se ainda não existir UIDocument, configura automaticamente no próprio GameObject
        if (decisionUIDocument == null)
        {
            if (decisionUxmlAsset == null)
            {
                decisionUxmlAsset = Resources.Load<VisualTreeAsset>("DecisionUI");
            }
            if (decisionPanelSettings == null)
            {
                decisionPanelSettings = Resources.Load<PanelSettings>("DecisionPanelSettings");
            }

            if (decisionUxmlAsset != null)
            {
                decisionUIDocument = gameObject.AddComponent<UIDocument>();
                decisionUIDocument.visualTreeAsset = decisionUxmlAsset;
                if (decisionPanelSettings != null)
                {
                    decisionUIDocument.panelSettings = decisionPanelSettings;
                }
            }
        }

        if (decisionUIDocument != null && decisionUIDocument.rootVisualElement != null)
        {
            var root = decisionUIDocument.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;

            var screenElem = root.Q<VisualElement>("decision-screen");
            if (screenElem != null) screenElem.pickingMode = PickingMode.Ignore;

            _decisionContainer = root.Q<VisualElement>("decision-container");
            if (_decisionContainer != null) _decisionContainer.pickingMode = PickingMode.Ignore;

            _btnApprove = root.Q<UnityEngine.UIElements.Button>("btn-approve");
            _btnReject = root.Q<UnityEngine.UIElements.Button>("btn-reject");
            _lblApproveText = root.Q<Label>("lbl-approve-text");
            _lblRejectText = root.Q<Label>("lbl-reject-text");

            if (_btnApprove != null)
            {
                _btnApprove.clicked -= ApproveDeal;
                _btnApprove.clicked += ApproveDeal;
            }

            if (_btnReject != null)
            {
                _btnReject.clicked -= RejectDeal;
                _btnReject.clicked += RejectDeal;
            }

            // Inicia oculto
            _decisionContainer?.AddToClassList("hidden");
        }
    }

    /// <summary>
    /// Verifica de forma precisa se o mouse está sobre os botões de decisão (Aprovar / Rejeitar).
    /// Utilizado pelo CameraFocusManager para não disparar desfoque acidental ao clicar nos botões.
    /// </summary>
    public bool IsPointerOverDecisionButtons()
    {
        if (_decisionContainer == null || _decisionContainer.ClassListContains("hidden")) return false;
        if (decisionUIDocument == null || decisionUIDocument.rootVisualElement == null || decisionUIDocument.rootVisualElement.panel == null) return false;

        Vector2 screenPos = Input.mousePosition;
        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(
            decisionUIDocument.rootVisualElement.panel,
            new Vector2(screenPos.x, Screen.height - screenPos.y)
        );
        var picked = decisionUIDocument.rootVisualElement.panel.Pick(panelPos);
        if (picked == null || picked == decisionUIDocument.rootVisualElement) return false;

        if (picked is UnityEngine.UIElements.Button ||
            picked.GetFirstAncestorOfType<UnityEngine.UIElements.Button>() != null ||
            picked.ClassListContains("decision-btn") ||
            picked.GetFirstAncestorOfType<VisualElement>()?.ClassListContains("decision-btn") == true)
        {
            return true;
        }

        return false;
    }

    private void HandleNewDeal(Deal deal)
    {
        UpdatePerks();
        UpdateDate();
        ShowDeal(deal);
    }

    private void HandleUpdatedAttributes(Attributes attributes)
    {
        UpdateAttributes(attributes);
    }

    private void HandleGameOver(string reason)
    {
        ShowGameOver(reason);
    }

    /// <summary>
    /// Chamado automaticamente quando a câmera muda de foco para qualquer objeto ou retorna ao padrão.
    /// </summary>
    private void HandleCameraFocusChanged(FocusableObject focusedObject)
    {
        bool isPaper = IsPaperObject(focusedObject);

        if (isPaper && _currentDeal != null && !_isProcessingDecision)
        {
            ShowDecisionButtons(_currentDeal);
        }
        else
        {
            HideDecisionButtons();
        }
    }

    /// <summary>
    /// Verifica se o FocusableObject corresponde ao documento/papel da proposta.
    /// </summary>
    public bool IsPaperObject(FocusableObject obj)
    {
        if (obj == null) return false;

        if (physicalPaper != null)
        {
            if (obj.gameObject == physicalPaper.gameObject ||
                obj.transform.IsChildOf(physicalPaper.transform) ||
                physicalPaper.transform.IsChildOf(obj.transform))
            {
                return true;
            }
        }

        if (obj.GetComponentInParent<PhysicalPaperUI>() != null || obj.GetComponentInChildren<PhysicalPaperUI>() != null)
        {
            return true;
        }

        string objName = obj.gameObject.name.ToLower();
        if (objName.Contains("papel") || objName.Contains("paper"))
        {
            return true;
        }

        return false;
    }

    public void ShowDeal(Deal deal)
    {
        if (deal == null) return;

        _isProcessingDecision = false;
        _currentDeal = deal;

        // Atualiza o conteúdo no papel físico 3D via UI Toolkit
        UpdatePhysicalPaper(deal);

        // Toca a animação do player levantando o papel
        PlayPlayerDealAnimation();

        // A CÂMERA PERMANECE LIVRE:
        // Os botões só aparecem quando a câmera estiver ativamente focada no papel.
        if (CameraFocusManager.Instance != null && IsPaperObject(CameraFocusManager.Instance.CurrentFocusedObject))
        {
            ShowDecisionButtons(deal);
        }
        else
        {
            HideDecisionButtons();
        }

        // Atualizações dos painéis de data e corrupção (1s com easeType original)
        if (datePanel != null)
            LeanTween.move(datePanel.GetComponent<RectTransform>(), new Vector3(710, 451, 0), 1f).setEase(easeType);
        if (corruptionPanel != null)
            LeanTween.move(corruptionPanel.GetComponent<RectTransform>(), new Vector3(110, -120, 0), 1f).setEase(easeType);

        if (nameText != null && deal.NPC != null) nameText.text = deal.NPC.name;
        if (descriptionText != null) descriptionText.text = Deal.FormatSentenceBreaks(deal.Description);
        if (rightAnswerText != null) rightAnswerText.text = Deal.FormatSentenceBreaks(deal.rightAnswer);
        if (leftAnswerText != null) leftAnswerText.text = Deal.FormatSentenceBreaks(deal.leftAnswer);
    }

    public void UpdateAttributes(Attributes currentAttributes)
    {
        float climateValue = Mathf.Clamp01(currentAttributes.climaticChanges / 100f);
        float relationsValue = Mathf.Clamp01(currentAttributes.internationalRelations / 100f);
        float approvalValue = Mathf.Clamp01(currentAttributes.populationalApproval / 100f);
        float economyValue = Mathf.Clamp01(currentAttributes.economy / 100f);
        float corruptionValue = Mathf.Clamp01(currentAttributes.corruption / 100f);

        climateSlider.value = Mathf.Lerp(climateSlider.value, climateValue, animationSpeed * Time.deltaTime);
        relationsSlider.value = Mathf.Lerp(relationsSlider.value, relationsValue, animationSpeed * Time.deltaTime);
        approvalSlider.value = Mathf.Lerp(approvalSlider.value, approvalValue, animationSpeed * Time.deltaTime);
        economySlider.value = Mathf.Lerp(economySlider.value, economyValue, animationSpeed * Time.deltaTime);
        corruptionSlider.value = Mathf.Lerp(corruptionSlider.value, corruptionValue, animationSpeed * Time.deltaTime);
    }

    public void ShowGameOver(string reason)
    {
        _currentDeal = null;
        HideDecisionButtons();
        if (dealPanel != null) dealPanel.SetActive(false);
        LeanTween.move(gameOverPanel.GetComponent<RectTransform>(), new Vector3(0, -300, 0), 1f).setEase(easeType);
        gameOverText.text = reason;
    }

    public void ShowGameWin(string congratulations)
    {
        _currentDeal = null;
        HideDecisionButtons();
        if (dealPanel != null) dealPanel.SetActive(false);
        LeanTween.move(gameOverPanel.GetComponent<RectTransform>(), new Vector3(0, -300, 0), 1f).setEase(easeType);
        gameOverText.text = congratulations;
    }

    public void GoBack()
    {
        SceneManager.LoadScene(0);
    }

    /// <summary>
    /// Escolha de Aprovação (Opção da Esquerda).
    /// </summary>
    public void ApproveDeal()
    {
        if (_isProcessingDecision) return;
        _isProcessingDecision = true;
        _currentDeal = null;

        HideDecisionButtons();

        bool isTutorial = GameManager.instance != null && GameManager.instance.onTutorial;
        bool isLastTutorialDeal = isTutorial && (GameManager.instance.tutorialDeals == null || GameManager.instance.tutorialDeals.Count <= 1);

        // Durante o tutorial, não toca a animação reversa nem tira da animação de levantando o papel.
        // Só toca ao concluir o último deal do tutorial (transição para o jogo normal com NPCs) ou se não for tutorial.
        if (!isTutorial || isLastTutorialDeal)
        {
            PlayPlayerDealAnimationReverse();

            // Se a câmera estiver focada em algum objeto (computador ou papel), retorna suavemente para a visão geral
            if (CameraFocusManager.Instance != null && CameraFocusManager.Instance.HasActiveFocus)
            {
                CameraFocusManager.Instance.Unfocus();
            }
        }

        if (!isTutorial)
        {
            if (datePanel != null) LeanTween.move(datePanel.GetComponent<RectTransform>(), new Vector3(710, 651, 0), 1f).setEase(easeType);
            if (corruptionPanel != null) LeanTween.move(corruptionPanel.GetComponent<RectTransform>(), new Vector3(-120, -120, 0), 1f).setEase(easeType);
            StartCoroutine(GameManager.instance.ApplyDecision(GameManager.instance.actualDeck[0], GameManager.instance.actualDeck[0].impactsLeft));
        }
        else
        {
            StartCoroutine(GameManager.instance.ApplyDecision(GameManager.instance.tutorialDeals[0], GameManager.instance.tutorialDeals[0].impactsLeft));
            if (!GameManager.instance.tutorialDeals.Any())
            {
                if (datePanel != null) LeanTween.move(datePanel.GetComponent<RectTransform>(), new Vector3(710, 651, 0), 1f).setEase(easeType);
                if (corruptionPanel != null) LeanTween.move(corruptionPanel.GetComponent<RectTransform>(), new Vector3(-120, -120, 0), 1f).setEase(easeType);
            }
        }
    }

    /// <summary>
    /// Escolha de Rejeição (Opção da Direita).
    /// </summary>
    public void RejectDeal()
    {
        if (_isProcessingDecision) return;
        _isProcessingDecision = true;
        _currentDeal = null;

        HideDecisionButtons();

        bool isTutorial = GameManager.instance != null && GameManager.instance.onTutorial;
        bool isLastTutorialDeal = isTutorial && (GameManager.instance.tutorialDeals == null || GameManager.instance.tutorialDeals.Count <= 1);

        // Durante o tutorial, não toca a animação reversa nem tira da animação de levantando o papel.
        // Só toca ao concluir o último deal do tutorial (transição para o jogo normal com NPCs) ou se não for tutorial.
        if (!isTutorial || isLastTutorialDeal)
        {
            PlayPlayerDealAnimationReverse();

            // Se a câmera estiver focada em algum objeto (computador ou papel), retorna suavemente para a visão geral
            if (CameraFocusManager.Instance != null && CameraFocusManager.Instance.HasActiveFocus)
            {
                CameraFocusManager.Instance.Unfocus();
            }
        }

        if (!isTutorial)
        {
            if (datePanel != null) LeanTween.move(datePanel.GetComponent<RectTransform>(), new Vector3(710, 651, 0), 1f).setEase(easeType);
            if (corruptionPanel != null) LeanTween.move(corruptionPanel.GetComponent<RectTransform>(), new Vector3(-120, -120, 0), 1f).setEase(easeType);
            StartCoroutine(GameManager.instance.ApplyDecision(GameManager.instance.actualDeck[0], GameManager.instance.actualDeck[0].impactsRight));
        }
        else
        {
            StartCoroutine(GameManager.instance.ApplyDecision(GameManager.instance.tutorialDeals[0], GameManager.instance.tutorialDeals[0].impactsRight));
            if (!GameManager.instance.tutorialDeals.Any())
            {
                if (datePanel != null) LeanTween.move(datePanel.GetComponent<RectTransform>(), new Vector3(710, 651, 0), 1f).setEase(easeType);
                if (corruptionPanel != null) LeanTween.move(corruptionPanel.GetComponent<RectTransform>(), new Vector3(-120, -120, 0), 1f).setEase(easeType);
            }
        }
    }

    public void LeftAnswerButton()
    {
        ApproveDeal();
    }

    public void RightAnswerButton()
    {
        RejectDeal();
    }

    public void UpdatePerks()
    {
        if (perks == null || GameManager.instance == null || GameManager.instance.activePerks == null)
            return;

        for (int i = 0; i < perks.Length; i++)
        {
            if (perks[i] == null) continue;

            if (i < GameManager.instance.activePerks.Length && GameManager.instance.activePerks[i] != null)
            {
                perks[i].gameObject.SetActive(true);
                perks[i].sprite = GameManager.instance.activePerks[i].icon;
            }
            else
            {
                perks[i].gameObject.SetActive(false);
            }
        }
    }

    public void UpdateDate()
    {
        dateText.text = $"{GameManager.instance.month} / {GameManager.instance.year}";
    }

    void DeactivatePanel()
    {
        if (dealPanel != null) dealPanel.SetActive(false);
    }

    void ActivatePanel()
    {
        if (dealPanel != null) dealPanel.SetActive(true);
    }

    public Animator GetPlayerAnimator()
    {
        if (playerAnimator != null)
        {
            EnsurePlayerMeshRenderers(playerAnimator.gameObject);
            return playerAnimator;
        }

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null) playerObj = GameObject.Find("Player");
        if (playerObj != null)
        {
            playerAnimator = playerObj.GetComponent<Animator>();
            EnsurePlayerMeshRenderers(playerObj);
        }

        return playerAnimator;
    }

    private void EnsurePlayerMeshRenderers(GameObject playerObj)
    {
        if (playerObj == null) return;
        var smrs = playerObj.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in smrs)
        {
            if (smr != null && !smr.updateWhenOffscreen)
            {
                smr.updateWhenOffscreen = true;
            }
        }
    }

    public void PlayPlayerDealAnimation()
    {
        if (_dealAnimReverseCoroutine != null)
        {
            StopCoroutine(_dealAnimReverseCoroutine);
            _dealAnimReverseCoroutine = null;
        }

        Animator anim = GetPlayerAnimator();
        if (anim == null)
        {
            Debug.LogWarning("[UIManager] Animator do Player não encontrado para tocar a animação do Deal.");
            return;
        }

        if (!string.IsNullOrEmpty(dealAnimationName) && anim.HasState(0, Animator.StringToHash(dealAnimationName)))
        {
            // Se for tutorial e o player já estiver na animação de levantando o papel, mantém a pose sem reiniciar
            bool isTutorial = GameManager.instance != null && GameManager.instance.onTutorial;
            if (isTutorial)
            {
                var stateInfo = anim.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.IsName(dealAnimationName) || stateInfo.shortNameHash == Animator.StringToHash(dealAnimationName))
                {
                    return;
                }
            }

            anim.speed = 1f;
            anim.Play(dealAnimationName, 0, 0f);
        }
    }

    /// <summary>
    /// Toca a animação da mão levantada de forma invertida e retorna ao estado default.
    /// </summary>
    public void PlayPlayerDealAnimationReverse()
    {
        if (_dealAnimReverseCoroutine != null)
        {
            StopCoroutine(_dealAnimReverseCoroutine);
            _dealAnimReverseCoroutine = null;
        }
        _dealAnimReverseCoroutine = StartCoroutine(PlayDealAnimationReverseCoroutine());
    }

    private System.Collections.IEnumerator PlayDealAnimationReverseCoroutine()
    {
        Animator anim = GetPlayerAnimator();
        if (anim == null)
        {
            _dealAnimReverseCoroutine = null;
            yield break;
        }

        // 1. Se houver um estado dedicado reverso no Animator, toca ele
        if (!string.IsNullOrEmpty(dealAnimationReverseName) && anim.HasState(0, Animator.StringToHash(dealAnimationReverseName)))
        {
            anim.speed = 1f;
            anim.Play(dealAnimationReverseName, 0, 0f);
            float reverseDuration = GetClipDuration(anim, dealAnimationReverseName);
            yield return new WaitForSeconds(reverseDuration > 0 ? reverseDuration : 1f);
        }
        // 2. Caso contrário, toca a mesma animação de trás para frente interpolando o tempo suavemente (Unity não permite anim.speed negativo diretamente sem recorder)
        else if (!string.IsNullOrEmpty(dealAnimationName) && anim.HasState(0, Animator.StringToHash(dealAnimationName)))
        {
            float clipDuration = GetClipDuration(anim, dealAnimationName);
            float duration = clipDuration > 0 ? clipDuration : 0.5f;

            var stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            float startNormalizedTime = 1f;
            if (stateInfo.IsName(dealAnimationName) || stateInfo.shortNameHash == Animator.StringToHash(dealAnimationName))
            {
                startNormalizedTime = Mathf.Clamp01(stateInfo.normalizedTime % 1f);
                if (startNormalizedTime <= 0.01f && stateInfo.normalizedTime >= 0.99f) startNormalizedTime = 1f;
            }

            float currentDuration = Mathf.Max(duration * startNormalizedTime, 0.05f);
            float elapsed = 0f;

            anim.speed = 0f;
            while (elapsed < currentDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Lerp(startNormalizedTime, 0f, elapsed / currentDuration);
                anim.Play(dealAnimationName, 0, t);
                anim.Update(0f);
                yield return null;
            }

            anim.speed = 1f;
        }

        // 3. Retorna para o estado default configurado (ex: "None")
        if (!string.IsNullOrEmpty(defaultAnimationName) && anim.HasState(0, Animator.StringToHash(defaultAnimationName)))
        {
            anim.Play(defaultAnimationName, 0, 0f);
        }
        else
        {
            anim.Play("None", 0, 0f);
        }

        _dealAnimReverseCoroutine = null;
    }

    private float GetClipDuration(Animator anim, string stateOrClipName)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return 1f;
        foreach (AnimationClip clip in anim.runtimeAnimatorController.animationClips)
        {
            if (clip != null && (clip.name == stateOrClipName || stateOrClipName.Contains(clip.name) || clip.name.Contains(stateOrClipName)))
            {
                return clip.length;
            }
        }
        return 1f;
    }

    public void UpdatePhysicalPaper(Deal deal)
    {
        if (deal == null) return;

        if (physicalPaper == null)
        {
            physicalPaper = PhysicalPaperUI.instance ?? FindFirstObjectByType<PhysicalPaperUI>();
        }

        if (physicalPaper != null)
        {
            physicalPaper.UpdateContent(deal);
        }

        if (paperDescriptionText != null)
        {
            paperDescriptionText.text = Deal.FormatSentenceBreaks(deal.Description);
        }

        if (paperNameText != null && deal.NPC != null)
        {
            paperNameText.text = deal.NPC.name;
        }
    }

    public void ShowDecisionButtons(Deal deal)
    {
        if (deal == null) return;

        if (_decisionContainer == null)
        {
            SetupDecisionUIToolkit();
        }

        // 1. UI Toolkit (Moderno e recomendado)
        if (_decisionContainer != null)
        {
            string leftText = !string.IsNullOrEmpty(deal.leftAnswer) ? Deal.FormatSentenceBreaks(deal.leftAnswer) : "Aprovar";
            string rightText = !string.IsNullOrEmpty(deal.rightAnswer) ? Deal.FormatSentenceBreaks(deal.rightAnswer) : "Rejeitar";

            if (_lblApproveText != null) _lblApproveText.text = leftText;
            if (_lblRejectText != null) _lblRejectText.text = rightText;

            _decisionContainer.RemoveFromClassList("hidden");

            // Oculta o dealPanel clássico do Canvas para não bloquear cliques na cena
            if (dealPanel != null) dealPanel.SetActive(false);
            return;
        }

        // 2. Fallback Canvas (apenas se UI Toolkit não puder ser carregado)
        if (decisionButtonsPanel != null)
        {
            decisionButtonsPanel.SetActive(true);
            decisionButtonsPanel.transform.localScale = Vector3.zero;
            LeanTween.scale(decisionButtonsPanel, Vector3.one, 0.35f).setEase(easeType);

            if (approveButtonText != null) approveButtonText.text = !string.IsNullOrEmpty(deal.leftAnswer) ? Deal.FormatSentenceBreaks(deal.leftAnswer) : "Aprovar";
            if (rejectButtonText != null) rejectButtonText.text = !string.IsNullOrEmpty(deal.rightAnswer) ? Deal.FormatSentenceBreaks(deal.rightAnswer) : "Rejeitar";
            return;
        }

        if (dealPanel != null)
        {
            if (descriptionText != null) descriptionText.gameObject.SetActive(false);
            if (nameText != null && nameText.transform.parent != null) nameText.transform.parent.gameObject.SetActive(showDescriptionPanel);

            var panelBg = dealPanel.GetComponent<UnityEngine.UI.Image>();
            if (panelBg != null) { panelBg.enabled = false; panelBg.raycastTarget = false; }

            ActivatePanel();
            LeanTween.move(dealPanel.GetComponent<RectTransform>(), new Vector3(0, -300, 0), 0.5f).setEase(easeType);
        }

        if (leftAnswerText != null) leftAnswerText.text = !string.IsNullOrEmpty(deal.leftAnswer) ? Deal.FormatSentenceBreaks(deal.leftAnswer) : "Aprovar";
        if (rightAnswerText != null) rightAnswerText.text = !string.IsNullOrEmpty(deal.rightAnswer) ? Deal.FormatSentenceBreaks(deal.rightAnswer) : "Rejeitar";
    }

    public void HideDecisionButtons()
    {
        // 1. UI Toolkit
        if (_decisionContainer != null)
        {
            _decisionContainer.AddToClassList("hidden");
        }

        // 2. Fallback Canvas
        if (decisionButtonsPanel != null && decisionButtonsPanel.activeSelf)
        {
            LeanTween.scale(decisionButtonsPanel, Vector3.zero, 0.25f).setEase(easeType).setOnComplete(() =>
            {
                decisionButtonsPanel.SetActive(false);
            });
        }
        else if (dealPanel != null && dealPanel.activeSelf)
        {
            LeanTween.move(dealPanel.GetComponent<RectTransform>(), new Vector3(0, -900, 0), 0.5f).setEase(easeType).setOnComplete(DeactivatePanel);
        }
    }
}