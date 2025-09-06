using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    [Header("Referências de UI")]
    public GameObject dealPanel; // Painel principal da proposta
    public GameObject datePanel;
    public TextMeshProUGUI descriptionText; // Texto da proposta
    public TextMeshProUGUI leftAnswerText; // Texto do botão Aceitar
    public TextMeshProUGUI rightAnswerText; // Texto do botão Recusar
    public TextMeshProUGUI dateText;


    [Header("Barras de Atributos")]
    public Slider climateSlider;
    public Slider relationsSlider;
    public Slider approvalSlider;
    public Slider economySlider;

    [Header("Configurações")]
    public float animationSpeed = 2f; // Velocidade da animação das barras
    public LeanTweenType easeType;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        // inscrição dos eventos
        GameManager.OnNewDeal += HandleNewDeal;
        GameManager.OnChangeAttributes += HandleUpdatedAttributes;
        GameManager.OnGameOver += HandleGameOver;

        // Inicializando UI
        //dealPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        GameManager.OnNewDeal -= HandleNewDeal;
        GameManager.OnChangeAttributes -= HandleUpdatedAttributes;
        GameManager.OnGameOver -= HandleGameOver;
    }

    private void Start()
    {
        //LeanTween.move(dealPanel.GetComponent<RectTransform>(), new Vector3(0, -900, 0), 1f).setEase(easeType).setOnComplete(DeactivatePanel);
    }

    private void Update()
    {
        if (GameManager.instance != null)
        {
            UpdateAttributes(GameManager.instance.gameAttributes);
        }
    }

    private void HandleNewDeal(Deal deal)
    {
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

    public void ShowDeal(Deal deal)
    {
        if (deal == null) return;

        ActivatePanel();
        LeanTween.move(dealPanel.GetComponent<RectTransform>(), new Vector3(0, -300, 0), 1f).setEase(easeType);
        LeanTween.move(datePanel.GetComponent<RectTransform>(), new Vector3(710, 451, 0), 1f).setEase(easeType);
        if(!GameManager.instance.onTutorial) GameManager.instance.PPFocus();

        descriptionText.text = deal.Description;
        rightAnswerText.text = deal.rightAnswer;
        leftAnswerText.text = deal.leftAnswer;
    }

    public void UpdateAttributes(Attributes currentAttributes)
    {
        float climateValue = Mathf.Clamp01(currentAttributes.climaticChanges / 100f);
        float relationsValue = Mathf.Clamp01(currentAttributes.internationalRelations / 100f);
        float approvalValue = Mathf.Clamp01(currentAttributes.populationalApproval / 100f);
        float economyValue = Mathf.Clamp01(currentAttributes.economy / 100f);

        climateSlider.value = Mathf.Lerp(climateSlider.value, climateValue, animationSpeed * Time.deltaTime);
        relationsSlider.value = Mathf.Lerp(relationsSlider.value, relationsValue, animationSpeed * Time.deltaTime);
        approvalSlider.value = Mathf.Lerp(approvalSlider.value, approvalValue, animationSpeed * Time.deltaTime);
        economySlider.value = Mathf.Lerp(economySlider.value, economyValue, animationSpeed * Time.deltaTime);

    }

    public void ShowGameOver(string reason)
    {
        dealPanel.SetActive(false);
        Debug.Log($"{reason} Game Over");
        // Implemente sua lógica de tela de game over aqui
    }

    public void LeftAnswerButton()
    {
        if (!GameManager.instance.onTutorial)
        {
            LeanTween.move(dealPanel.GetComponent<RectTransform>(), new Vector3(0, -900, 0), 1f).setEase(easeType).setOnComplete(DeactivatePanel);
            LeanTween.move(datePanel.GetComponent<RectTransform>(), new Vector3(710, 651, 0), 1f).setEase(easeType);
            StartCoroutine(GameManager.instance.ApplyDecision(GameManager.instance.actualDeck[0], GameManager.instance.actualDeck[0].impactsLeft));
            GameManager.instance.PPUnfocus();
        }
        else
        {
            StartCoroutine(GameManager.instance.ApplyDecision(GameManager.instance.tutorialDeals[0], GameManager.instance.tutorialDeals[0].impactsLeft));
            if (!GameManager.instance.tutorialDeals.Any())
            {
                LeanTween.move(dealPanel.GetComponent<RectTransform>(), new Vector3(0, -900, 0), 1f).setEase(easeType).setOnComplete(DeactivatePanel);
                LeanTween.move(datePanel.GetComponent<RectTransform>(), new Vector3(710, 651, 0), 1f).setEase(easeType);
            }
        }
    }

    public void RightAnswerButton()
    {
        if (!GameManager.instance.onTutorial){
            LeanTween.move(dealPanel.GetComponent<RectTransform>(), new Vector3(0, -900, 0), 1f).setEase(easeType).setOnComplete(DeactivatePanel);
            LeanTween.move(datePanel.GetComponent<RectTransform>(), new Vector3(710, 651, 0), 1f).setEase(easeType);
            StartCoroutine(GameManager.instance.ApplyDecision(GameManager.instance.actualDeck[0], GameManager.instance.actualDeck[0].impactsRight));
            GameManager.instance.PPUnfocus();
        }
        else
        {
            StartCoroutine(GameManager.instance.ApplyDecision(GameManager.instance.tutorialDeals[0], GameManager.instance.tutorialDeals[0].impactsRight));
            if (!GameManager.instance.tutorialDeals.Any())
            {
                LeanTween.move(dealPanel.GetComponent<RectTransform>(), new Vector3(0, -900, 0), 1f).setEase(easeType).setOnComplete(DeactivatePanel);
                LeanTween.move(datePanel.GetComponent<RectTransform>(), new Vector3(710, 651, 0), 1f).setEase(easeType);
            }
        }
    }

    public void UpdateDate()
    {
        dateText.text = $"{GameManager.instance.month} / {GameManager.instance.year}";
    }

    void DeactivatePanel()
    {
        dealPanel.SetActive(false);
    }

    void ActivatePanel()
    {
        dealPanel.SetActive(true);
    }
}