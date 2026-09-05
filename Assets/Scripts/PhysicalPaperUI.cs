using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Componente responsável por exibir o conteúdo da proposta (Deal) diretamente
/// no papel físico da cena 3D, com suporte nativo a UI Toolkit (UXML + RenderTexture)
/// ou TextMeshPro/Canvas.
/// </summary>
public class PhysicalPaperUI : MonoBehaviour
{
    public static PhysicalPaperUI instance;

    [Header("--- UI Toolkit (Recomendado) ---")]
    [Tooltip("UIDocument que renderiza a interface da folha para a RenderTexture.")]
    [SerializeField] private UIDocument uiDocument;

    [Tooltip("Nome (Name) do elemento Label no UXML para a descrição da proposta.")]
    [SerializeField] private string descriptionLabelName = "deal-description";

    [Tooltip("Nome (Name) do elemento Label no UXML para o autor / NPC da proposta.")]
    [SerializeField] private string authorLabelName = "deal-author";

    [Tooltip("Nome (Name) do elemento Label no UXML para a data e local de assinatura.")]
    [SerializeField] private string dateLabelName = "deal-date";

    [Tooltip("Local padrão exibido no documento.")]
    [SerializeField] private string defaultLocation = "Brasília - DF";

    [Header("--- TextMeshPro / Canvas (Legado / Opcional) ---")]
    [Tooltip("Campo de texto TextMeshProUGUI caso prefira usar Canvas.")]
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI npcNameText;
    [SerializeField] private TextMeshPro descriptionText3D;
    [SerializeField] private TextMeshProUGUI dateLocationText;

    private static readonly string[] MonthNames = new string[]
    {
        "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho",
        "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro"
    };

    [Header("--- Material / RenderTexture (Opcional) ---")]
    [Tooltip("Renderer da malha do papel na cena para atribuição automática do material.")]
    [SerializeField] private Renderer paperRenderer;
    [Tooltip("Propriedade de textura no shader (ex: _BaseMap ou _MainTex).")]
    [SerializeField] private string texturePropertyName = "_BaseMap";
    [Tooltip("RenderTexture do papel (a mesma atribuída no PanelSettings do UI Toolkit).")]
    [SerializeField] private RenderTexture paperRenderTexture;

    private Deal _currentDeal;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }

        EnsureReferences();

        // Se uma RenderTexture e um Renderer foram informados, garante a amarração no Material
        if (paperRenderer != null && paperRenderTexture != null)
        {
            paperRenderer.material.SetTexture(texturePropertyName, paperRenderTexture);
        }

        UpdateDateDisplay();
    }

    private void OnEnable()
    {
        EnsureReferences();

        if (_currentDeal != null)
        {
            ApplyContentToUI(_currentDeal);
        }
        else
        {
            UpdateDateDisplay();
        }
    }

    private void EnsureReferences()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>() ?? GetComponentInChildren<UIDocument>();
        }

        if (paperRenderer == null)
        {
            paperRenderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();
        }
    }

    /// <summary>
    /// Retorna o texto formatado de local e data atual do mandato.
    /// Exemplo: "Brasília - DF, Janeiro de 2026"
    /// </summary>
    public string GetFormattedDocumentDate()
    {
        if (GameManager.instance != null)
        {
            int m = Mathf.Clamp(GameManager.instance.month, 1, 12);
            string monthName = MonthNames[m - 1];
            return $"{defaultLocation}, {monthName} de {GameManager.instance.year}";
        }
        return $"{defaultLocation}, 2026";
    }

    /// <summary>
    /// Atualiza apenas a data e local de assinatura no documento.
    /// </summary>
    public void UpdateDateDisplay()
    {
        EnsureReferences();
        string dateStr = GetFormattedDocumentDate();

        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            var dateLabel = uiDocument.rootVisualElement.Q<Label>(dateLabelName);
            if (dateLabel != null)
            {
                dateLabel.text = dateStr;
            }
        }

        if (dateLocationText != null)
        {
            dateLocationText.text = dateStr;
        }
    }

    /// <summary>
    /// Atualiza as informações exibidas no documento do papel (UI Toolkit e TextMeshPro).
    /// </summary>
    public void UpdateContent(Deal deal)
    {
        if (deal == null) return;

        _currentDeal = deal;
        EnsureReferences();
        ApplyContentToUI(deal);
    }

    private void ApplyContentToUI(Deal deal)
    {
        if (deal == null) return;

        string description = Deal.FormatSentenceBreaks(deal.Description);
        string npcName = deal.NPC != null ? deal.NPC.name : (!string.IsNullOrEmpty(deal.tag) ? deal.tag : "MINISTÉRIO DE ESTADO");
        string dateStr = GetFormattedDocumentDate();

        // 1. Atualização via UI Toolkit (UXML)
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            var descLabel = uiDocument.rootVisualElement.Q<Label>(descriptionLabelName);
            if (descLabel != null)
            {
                descLabel.text = description;
            }

            var authorLabel = uiDocument.rootVisualElement.Q<Label>(authorLabelName);
            if (authorLabel != null)
            {
                authorLabel.text = npcName;
            }

            var dateLabel = uiDocument.rootVisualElement.Q<Label>(dateLabelName);
            if (dateLabel != null)
            {
                dateLabel.text = dateStr;
            }
        }

        // 2. Atualização de fallback (TextMeshPro / Canvas)
        if (descriptionText != null) descriptionText.text = description;
        if (descriptionText3D != null) descriptionText3D.text = description;
        if (npcNameText != null) npcNameText.text = npcName;
        if (dateLocationText != null) dateLocationText.text = dateStr;
    }

    /// <summary>
    /// Limpa o conteúdo do documento.
    /// </summary>
    public void ClearContent()
    {
        _currentDeal = null;

        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            var descLabel = uiDocument.rootVisualElement.Q<Label>(descriptionLabelName);
            if (descLabel != null) descLabel.text = "";

            var authorLabel = uiDocument.rootVisualElement.Q<Label>(authorLabelName);
            if (authorLabel != null) authorLabel.text = "";
        }

        if (descriptionText != null) descriptionText.text = "";
        if (descriptionText3D != null) descriptionText3D.text = "";
        if (npcNameText != null) npcNameText.text = "";
    }
}
