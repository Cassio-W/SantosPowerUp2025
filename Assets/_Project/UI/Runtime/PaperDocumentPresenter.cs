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
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            VisualElement root = uiDocument.rootVisualElement;
            descriptionLabel = root.Q<Label>(descriptionLabelName) ?? root.Q<Label>("description-label") ?? root.Q<Label>("txt-description");
            authorLabel = root.Q<Label>(authorLabelName) ?? root.Q<Label>("author-label") ?? root.Q<Label>("txt-author");
            dateLabel = root.Q<Label>(dateLabelName) ?? root.Q<Label>("date-label") ?? root.Q<Label>("txt-date");
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

            var focusables = targetObj.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var comp in focusables)
            {
                if (comp != null && comp.GetType().Name == "FocusableObject")
                {
                    comp.enabled = interactable;
                }
            }
        }

        public void SetProposal(CardDefinition card, string displayDate)
        {
            EnsureReferences();
            SetPaperActive(true);
            SetPaperInteractable(true);
            CacheUIElements();

            string desc = card != null ? card.FormattedDescription : string.Empty;
            string author = card != null ? (!string.IsNullOrEmpty(card.npcId) ? card.npcId : card.title) : string.Empty;
            string formattedDate = !string.IsNullOrEmpty(displayDate) ? displayDate : "01/2026";
            string dateLoc = $"{defaultLocation}, {formattedDate}";

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

        public void Clear()
        {
            SetProposal(null, string.Empty);
            SetPaperActive(false);
            SetPaperInteractable(false);
        }
    }
}
