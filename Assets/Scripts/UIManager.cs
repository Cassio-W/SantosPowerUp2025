using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    [Header("Referências de UI")]
    public GameObject dealPanel; // Painel principal da proposta
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
        dealPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        GameManager.OnNewDeal -= HandleNewDeal;
        GameManager.OnChangeAttributes -= HandleUpdatedAttributes;
        GameManager.OnGameOver -= HandleGameOver;
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

        if (!dealPanel.activeSelf)
            dealPanel.SetActive(true);

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
        GameManager.instance.ApplyDecision(GameManager.instance.actualDeck[0], GameManager.instance.actualDeck[0].impactsLeft);
    }

    public void RightAnswerButton()
    {
        GameManager.instance.ApplyDecision(GameManager.instance.actualDeck[0], GameManager.instance.actualDeck[0].impactsRight);
    }

    public void UpdateDate()
    {
        dateText.text = $"{GameManager.instance.month} / {GameManager.instance.year}";
    }
}