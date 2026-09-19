using System;
using Mandato.Content;
using Mandato.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mandato.UI
{
    /// <summary>
    /// Presenter do documento de papel 3D da proposta em UI Toolkit puro (renderizado diegeticamente no RenderTexture).
    /// </summary>
    public class PaperDocumentPresenter : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset uxmlAsset;
        [SerializeField] private PanelSettings panelSettings;
        [SerializeField] private string descriptionLabelName = "deal-description";
        [SerializeField] private string authorLabelName = "deal-author";
        [SerializeField] private string dateLabelName = "deal-date";
        [SerializeField] private string defaultLocation = "Brasília - DF";

        [Header("Objeto Físico 3D")]
        [SerializeField] private GameObject paperContainer;
        [SerializeField] private Renderer paperRenderer;
        [SerializeField] private string texturePropertyName = "_BaseMap";
        [SerializeField] private RenderTexture paperRenderTexture;

        private Label descriptionLabel;
        private Label authorLabel;
        private Label dateLabel;
        private VisualElement stampLayer;
        private VisualElement stampPreviewElement;
        private Label previewTextLabel;
        private Label previewSubLabel;
        private string currentFormattedDate = "01/2026";

        public RenderTexture PaperRenderTexture => paperRenderTexture;

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

        public void EnsureReferences()
        {
            if (paperContainer == null) paperContainer = gameObject;
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>() ?? GetComponentInChildren<UIDocument>();
            if (paperRenderer == null) paperRenderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();

            if (uiDocument != null)
            {
                if (uiDocument.panelSettings == null)
                {
                    if (panelSettings != null)
                    {
                        uiDocument.panelSettings = panelSettings;
                    }
                    else
                    {
#if UNITY_EDITOR
                        var pSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/PAPEL/Papel.asset");
                        if (pSettings != null) uiDocument.panelSettings = pSettings;
#endif
                    }
                }

                if (uiDocument.visualTreeAsset == null)
                {
                    if (uxmlAsset != null)
                    {
                        uiDocument.visualTreeAsset = uxmlAsset;
                    }
                    else
                    {
#if UNITY_EDITOR
                        var uxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/PAPEL/PAPEL.uxml");
                        if (uxml != null) uiDocument.visualTreeAsset = uxml;
#endif
                    }
                }
            }
        }

        private void CacheUIElements()
        {
            if (uiDocument?.rootVisualElement != null)
            {
                VisualElement root = uiDocument.rootVisualElement;
                descriptionLabel = root.Q<Label>(descriptionLabelName) ?? root.Q<Label>("description-label") ?? root.Q<Label>("txt-description");
                authorLabel = root.Q<Label>(authorLabelName) ?? root.Q<Label>("author-label") ?? root.Q<Label>("txt-author");
                dateLabel = root.Q<Label>(dateLabelName) ?? root.Q<Label>("date-label") ?? root.Q<Label>("txt-date");
                stampLayer = root.Q<VisualElement>("stamp-layer") ?? root.Q<VisualElement>("paper-container") ?? root;
            }
            else
            {
                stampLayer ??= new VisualElement { name = "stamp-layer" };
            }
        }

        public void SetPaperActive(bool active)
        {
            EnsureReferences();

            if (paperContainer != null)
            {
                paperContainer.SetActive(active);
            }
        }

        public void SetPaperInteractable(bool interactable)
        {
            EnsureReferences();

            GameObject targetObj = paperContainer != null ? paperContainer : gameObject;
            var colliders = targetObj.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                if (col != null) col.enabled = interactable;
            }
        }

        public void SetProposal(CardDefinition card, string displayDate)
        {
            EnsureReferences();
            SetPaperActive(true);
            SetPaperInteractable(true);
            CacheUIElements();
            ClearStamps();

            string desc = card != null ? card.FormattedDescription : string.Empty;
            string author = card != null ? (!string.IsNullOrEmpty(card.npcId) ? card.npcId : card.title) : string.Empty;
            currentFormattedDate = !string.IsNullOrEmpty(displayDate) ? displayDate : "01/2026";
            string dateLoc = $"{defaultLocation}, {currentFormattedDate}";

            // 1. UI Toolkit nativo
            if (descriptionLabel != null) descriptionLabel.text = desc;
            if (authorLabel != null) authorLabel.text = author;
            if (dateLabel != null) dateLabel.text = dateLoc;

            // 2. Fallback TextMeshPro no papel se presente
            GameObject targetObj = paperContainer != null ? paperContainer : gameObject;
            var allMono = targetObj.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var m in allMono)
            {
                if (m == null) continue;
                string typeName = m.GetType().Name;
                string objName = m.gameObject.name.ToLower();

                if (typeName.Contains("TextMeshPro") || typeName == "Text")
                {
                    var textProp = m.GetType().GetProperty("text");
                    if (objName.Contains("desc") || objName.Contains("corpo") || objName.Contains("texto"))
                    {
                        textProp?.SetValue(m, desc);
                    }
                    else if (objName.Contains("name") || objName.Contains("nome") || objName.Contains("author") || objName.Contains("autor"))
                    {
                        textProp?.SetValue(m, author);
                    }
                    else if (objName.Contains("date") || objName.Contains("data"))
                    {
                        textProp?.SetValue(m, dateLoc);
                    }
                }
            }
        }

        public Vector2 MapUVToPanelCoordinates(Vector2 uvCoord)
        {
            float panelWidth = 1024f;
            float panelHeight = 1440f;

            if (uiDocument?.rootVisualElement?.panel != null)
            {
                var visualWidth = uiDocument.rootVisualElement.resolvedStyle.width;
                var visualHeight = uiDocument.rootVisualElement.resolvedStyle.height;
                if (visualWidth > 0 && visualHeight > 0)
                {
                    panelWidth = visualWidth;
                    panelHeight = visualHeight;
                }
            }
            else if (paperRenderTexture != null)
            {
                panelWidth = paperRenderTexture.width;
                panelHeight = paperRenderTexture.height;
            }

            // Desespelha X e Y para corresponder exatamente à visão da câmera sobre o papel
            float panelX = Mathf.Clamp((1f - uvCoord.x) * panelWidth, 80f, panelWidth - 80f);
            float panelY = Mathf.Clamp(uvCoord.y * panelHeight, 80f, panelHeight - 80f);

            return new Vector2(panelX, panelY);
        }

        /// <summary>
        /// Atualiza ou oculta o preview translúcido do carimbo na folha enquanto o jogador mira.
        /// </summary>
        public void UpdateStampPreview(bool isVisible, bool isApproved, Vector2 uvCoord)
        {
            if (!isVisible)
            {
                if (stampPreviewElement != null)
                {
                    stampPreviewElement.style.display = DisplayStyle.None;
                }
                return;
            }

            EnsureReferences();
            CacheUIElements();

            if (stampLayer == null)
            {
                stampLayer = new VisualElement { name = "stamp-layer" };
                if (uiDocument?.rootVisualElement != null)
                {
                    uiDocument.rootVisualElement.Add(stampLayer);
                }
            }

            if (stampPreviewElement == null)
            {
                stampPreviewElement = new VisualElement();
                stampPreviewElement.AddToClassList("stamp-mark");
                stampPreviewElement.AddToClassList("stamp-preview");

                previewTextLabel = new Label();
                previewTextLabel.AddToClassList("stamp-mark-text");
                stampPreviewElement.Add(previewTextLabel);

                previewSubLabel = new Label();
                previewSubLabel.AddToClassList("stamp-mark-subtext");
                stampPreviewElement.Add(previewSubLabel);

                stampLayer.Add(stampPreviewElement);
            }

            Vector2 panelPos = MapUVToPanelCoordinates(uvCoord);

            stampPreviewElement.EnableInClassList("stamp-approved", isApproved);
            stampPreviewElement.EnableInClassList("stamp-rejected", !isApproved);

            if (previewTextLabel != null)
            {
                previewTextLabel.text = isApproved ? "APROVADO" : "REJEITADO";
            }
            if (previewSubLabel != null)
            {
                previewSubLabel.text = $"GABINETE PRESIDENCIAL • {currentFormattedDate}";
            }

            stampPreviewElement.style.display = DisplayStyle.Flex;
            stampPreviewElement.style.position = Position.Absolute;
            stampPreviewElement.style.left = panelPos.x - 160f;
            stampPreviewElement.style.top = panelPos.y - 50f;
            stampPreviewElement.style.rotate = new Rotate(Angle.Degrees(0f));
        }

        /// <summary>
        /// Adiciona uma marca visual definitiva de carimbo (Stamp Mark) na coordenada UV do papel.
        /// </summary>
        public VisualElement AddStampMark(bool isApproved, Vector2 uvCoord, float rotationAngle = 0f)
        {
            EnsureReferences();
            CacheUIElements();
            UpdateStampPreview(false, false, Vector2.zero);

            if (stampLayer == null)
            {
                stampLayer = new VisualElement { name = "stamp-layer" };
                if (uiDocument?.rootVisualElement != null)
                {
                    uiDocument.rootVisualElement.Add(stampLayer);
                }
            }

            Vector2 panelPos = MapUVToPanelCoordinates(uvCoord);

            var stampEl = new VisualElement();
            stampEl.AddToClassList("stamp-mark");
            stampEl.AddToClassList(isApproved ? "stamp-approved" : "stamp-rejected");

            var textLabel = new Label(isApproved ? "APROVADO" : "REJEITADO");
            textLabel.AddToClassList("stamp-mark-text");
            stampEl.Add(textLabel);

            var subLabel = new Label($"GABINETE PRESIDENCIAL • {currentFormattedDate}");
            subLabel.AddToClassList("stamp-mark-subtext");
            stampEl.Add(subLabel);

            stampEl.style.position = Position.Absolute;
            stampEl.style.left = panelPos.x - 160f;
            stampEl.style.top = panelPos.y - 50f;

            if (Mathf.Abs(rotationAngle) > 0.01f)
            {
                stampEl.style.rotate = new Rotate(Angle.Degrees(rotationAngle));
            }

            stampLayer.Add(stampEl);
            return stampEl;
        }

        public void ClearStamps()
        {
            UpdateStampPreview(false, false, Vector2.zero);

            if (stampLayer != null && stampLayer.name == "stamp-layer")
            {
                stampLayer.Clear();
                stampPreviewElement = null;
                previewTextLabel = null;
                previewSubLabel = null;
            }
            else if (stampLayer != null)
            {
                var marks = stampLayer.Query<VisualElement>(className: "stamp-mark").ToList();
                foreach (var m in marks)
                {
                    if (m != stampPreviewElement)
                    {
                        m.RemoveFromHierarchy();
                    }
                }
            }
        }

        public void Clear()
        {
            SetProposal(null, string.Empty);
            ClearStamps();
            SetPaperActive(false);
            SetPaperInteractable(false);
        }
    }
}
