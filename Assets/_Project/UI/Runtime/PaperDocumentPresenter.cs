using Mandato.Content;
using Mandato.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mandato.UI
{
    public class PaperDocumentPresenter : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private string descriptionLabelName = "deal-description";
        [SerializeField] private string authorLabelName = "deal-author";
        [SerializeField] private string dateLabelName = "deal-date";
        [SerializeField] private string defaultLocation = "Brasília - DF";

        [Header("Render Texture 3D (Opcional)")]
        [SerializeField] private Renderer paperRenderer;
        [SerializeField] private string texturePropertyName = "_BaseMap";
        [SerializeField] private RenderTexture paperRenderTexture;

        private Label descriptionLabel;
        private Label authorLabel;
        private Label dateLabel;

        private void Awake()
        {
            EnsureReferences();
            CacheUIElements();

            if (paperRenderer != null && paperRenderTexture != null)
            {
                paperRenderer.material.SetTexture(texturePropertyName, paperRenderTexture);
            }
        }

        private void OnEnable()
        {
            EnsureReferences();
            CacheUIElements();
        }

        private void EnsureReferences()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>() ?? GetComponentInChildren<UIDocument>();
            if (paperRenderer == null) paperRenderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();
        }

        private void CacheUIElements()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            VisualElement root = uiDocument.rootVisualElement;
            descriptionLabel = root.Q<Label>(descriptionLabelName);
            authorLabel = root.Q<Label>(authorLabelName);
            dateLabel = root.Q<Label>(dateLabelName);
        }

        public void SetProposal(CardDefinition card, string displayDate)
        {
            CacheUIElements();

            if (descriptionLabel != null)
            {
                descriptionLabel.text = card != null ? card.FormattedDescription : string.Empty;
            }

            if (authorLabel != null)
            {
                authorLabel.text = card != null ? (!string.IsNullOrEmpty(card.npcId) ? card.npcId : card.title) : string.Empty;
            }

            if (dateLabel != null)
            {
                string formattedDate = !string.IsNullOrEmpty(displayDate) ? displayDate : "Janeiro de 2026";
                dateLabel.text = $"{defaultLocation}, {formattedDate}";
            }
        }

        public void Clear()
        {
            SetProposal(null, string.Empty);
        }
    }
}
