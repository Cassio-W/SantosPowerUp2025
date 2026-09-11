using System;
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

        private void EnsureReferences()
        {
            if (paperContainer == null) paperContainer = gameObject;
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

        public void SetPaperActive(bool active)
        {
            EnsureReferences();

            if (paperContainer != null)
            {
                paperContainer.SetActive(active);
            }

            // Sincroniza com UIManager / PhysicalPaperUI legado se presente via reflexão
            NotifyLegacyPaper(active);
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

            // 1. UI Toolkit
            if (descriptionLabel != null) descriptionLabel.text = desc;
            if (authorLabel != null) authorLabel.text = author;
            if (dateLabel != null) dateLabel.text = dateLoc;

            // 2. Fallback TextMeshPro no papel
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

            // 3. Sincroniza com PhysicalPaperUI se presente na cena via reflexão
            NotifyLegacyPaperUpdate(card, formattedDate);
        }

        private void NotifyLegacyPaper(bool active)
        {
            try
            {
                Type type = null;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType("PhysicalPaperUI");
                    if (type != null) break;
                }

                if (type != null)
                {
                    var instanceProp = type.GetField("instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var inst = instanceProp?.GetValue(null) as Component;
                    if (inst != null && inst.gameObject != paperContainer)
                    {
                        inst.gameObject.SetActive(active);
                    }
                }
            }
            catch { }
        }

        private void NotifyLegacyPaperUpdate(CardDefinition card, string displayDate)
        {
            try
            {
                Type type = null;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType("PhysicalPaperUI");
                    if (type != null) break;
                }

                if (type != null)
                {
                    var instanceProp = type.GetField("instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var inst = instanceProp?.GetValue(null) as Component ?? FindFirstObjectByType(type) as Component;
                    if (inst != null)
                    {
                        if (card != null && card.sourceLegacyAsset != null)
                        {
                            var updateContentMethod = type.GetMethod("UpdateContent");
                            updateContentMethod?.Invoke(inst, new object[] { card.sourceLegacyAsset });
                        }

                        var updateDateMethod = type.GetMethod("UpdateDateDisplay", new Type[] { typeof(string) });
                        updateDateMethod?.Invoke(inst, new object[] { displayDate });
                    }
                }
            }
            catch { }
        }

        public void Clear()
        {
            SetProposal(null, string.Empty);
            SetPaperActive(false);
            SetPaperInteractable(false);
        }
    }
}
