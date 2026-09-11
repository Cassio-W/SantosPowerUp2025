using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mandato.UI
{
    [Serializable]
    public class FlipPhoneActionViewModel
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string description = string.Empty;
        public string categoryTag = "Ações";
        public Sprite icon;
        public bool isAvailable = true;
        public bool isOnCooldown = false;
        public int cooldownTurnsRemaining = 0;
        public bool isConsumed = false;
        public string statusText = string.Empty;
        public List<string> impactTags = new List<string>();
    }

    public class FlipPhonePresenter : MonoBehaviour
    {
        [Header("Configurações do Flip-Phone")]
        [SerializeField] private VisualTreeAsset uxmlAsset;
        [SerializeField] private PanelSettings panelSettings;
        [SerializeField] private KeyCode toggleKey = KeyCode.F;
        [SerializeField] private bool allowKeyboardToggle = true;

        public event Action<string> OnActionRequested;
        public event Action OnPhoneOpened;
        public event Action OnPhoneClosed;

        public bool IsOpen { get; private set; } = false;

        private UIDocument uiDocument;
        private VisualElement root;
        private VisualElement screenRoot;
        private VisualElement actionsContainer;
        private Label emptyLabel;
        private Label statusLabel;
        private Button closeBtn;

        // Categorias / Tabs
        private Button tabAll, tabActions, tabContacts, tabCabinet;
        private string activeCategoryFilter = "TODAS";

        private List<FlipPhoneActionViewModel> cachedViewModels = new List<FlipPhoneActionViewModel>();

        private void Awake()
        {
            EnsureDocument();
            CacheVisualElements();
        }

        private void OnEnable()
        {
            EnsureDocument();
            CacheVisualElements();
        }

        private void Start()
        {
            // Oculta inicialmente
            Close();
        }

        private void Update()
        {
            if (allowKeyboardToggle && Input.GetKeyDown(toggleKey))
            {
                Toggle();
            }
            else if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        private void EnsureDocument()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (uiDocument == null)
            {
                uiDocument = gameObject.AddComponent<UIDocument>();
            }

            if (panelSettings != null && uiDocument.panelSettings == null)
            {
                uiDocument.panelSettings = panelSettings;
            }

            if (uxmlAsset != null && uiDocument.visualTreeAsset == null)
            {
                uiDocument.visualTreeAsset = uxmlAsset;
            }
            else if (uiDocument.visualTreeAsset == null)
            {
                // Tenta carregar asset padrão
                var loaded = Resources.Load<VisualTreeAsset>("FlipPhone");
                if (loaded != null) uiDocument.visualTreeAsset = loaded;
            }
        }

        private void CacheVisualElements()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            root = uiDocument.rootVisualElement;
            screenRoot = root.Q<VisualElement>("phone-screen-root");
            actionsContainer = root.Q<VisualElement>("actions-list-container");
            emptyLabel = root.Q<Label>("empty-actions-label");
            statusLabel = root.Q<Label>("phone-status-message");
            closeBtn = root.Q<Button>("phone-close-btn");

            if (closeBtn != null)
            {
                closeBtn.clicked -= Close;
                closeBtn.clicked += Close;
            }

            // Tabs
            tabAll = root.Q<Button>("tab-all");
            tabActions = root.Q<Button>("tab-actions");
            tabContacts = root.Q<Button>("tab-contacts");
            tabCabinet = root.Q<Button>("tab-cabinet");

            SetupTab(tabAll, "TODAS");
            SetupTab(tabActions, "AÇÕES");
            SetupTab(tabContacts, "CONTATOS");
            SetupTab(tabCabinet, "GABINETE");
        }

        private void SetupTab(Button tabBtn, string filterName)
        {
            if (tabBtn == null) return;
            tabBtn.clicked -= () => SetCategoryFilter(filterName);
            tabBtn.clicked += () => SetCategoryFilter(filterName);
        }

        public void SetCategoryFilter(string filterName)
        {
            activeCategoryFilter = filterName;
            Debug.Log($"<color=#6a9fb5>[FlipPhonePresenter]</color> Filtro de categoria selecionado: <b>{filterName}</b>");

            UpdateTabState(tabAll, filterName == "TODAS");
            UpdateTabState(tabActions, filterName == "AÇÕES");
            UpdateTabState(tabContacts, filterName == "CONTATOS");
            UpdateTabState(tabCabinet, filterName == "GABINETE");

            RebuildActionCards();
        }

        private void UpdateTabState(Button tabBtn, bool isActive)
        {
            if (tabBtn == null) return;
            if (isActive) tabBtn.AddToClassList("active");
            else tabBtn.RemoveFromClassList("active");
        }

        public void Open()
        {
            IsOpen = true;
            EnsureDocument();
            CacheVisualElements();

            if (screenRoot != null)
            {
                screenRoot.style.display = DisplayStyle.Flex;
                screenRoot.RemoveFromClassList("hidden");
                screenRoot.style.opacity = 1f;
            }

            Debug.Log("<color=#6a9fb5>[FlipPhonePresenter]</color> UI Toolkit: Tela do celular exibida.");
            OnPhoneOpened?.Invoke();
        }

        public void Close()
        {
            IsOpen = false;

            if (screenRoot != null)
            {
                screenRoot.style.display = DisplayStyle.None;
                screenRoot.AddToClassList("hidden");
                screenRoot.style.opacity = 0f;
            }

            Debug.Log("<color=#6a9fb5>[FlipPhonePresenter]</color> UI Toolkit: Tela do celular ocultada.");
            OnPhoneClosed?.Invoke();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Refresh(IEnumerable<FlipPhoneActionViewModel> actions, string statusMessage = "")
        {
            cachedViewModels.Clear();
            if (actions != null)
            {
                cachedViewModels.AddRange(actions);
            }

            EnsureDocument();
            CacheVisualElements();
            RebuildActionCards();

            Debug.Log($"<color=#6a9fb5>[FlipPhonePresenter]</color> Interface atualizada com {cachedViewModels.Count} ações.");

            if (!string.IsNullOrEmpty(statusMessage))
            {
                ShowMessage(statusMessage);
            }
        }

        private void RebuildActionCards()
        {
            if (actionsContainer == null) return;

            actionsContainer.Clear();

            int visibleCount = 0;

            foreach (var vm in cachedViewModels)
            {
                if (vm == null) continue;

                // Aplica filtro de categoria
                if (!MatchesCategoryFilter(vm.categoryTag, activeCategoryFilter))
                {
                    continue;
                }

                visibleCount++;
                var card = CreateActionCard(vm);
                actionsContainer.Add(card);
            }

            if (emptyLabel != null)
            {
                if (visibleCount == 0)
                {
                    emptyLabel.style.display = DisplayStyle.Flex;
                    emptyLabel.RemoveFromClassList("hidden");
                }
                else
                {
                    emptyLabel.style.display = DisplayStyle.None;
                    emptyLabel.AddToClassList("hidden");
                }
            }
        }

        private bool MatchesCategoryFilter(string categoryTag, string filter)
        {
            if (string.IsNullOrEmpty(filter) || filter == "TODAS") return true;
            if (string.IsNullOrEmpty(categoryTag)) return false;

            return categoryTag.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private VisualElement CreateActionCard(FlipPhoneActionViewModel vm)
        {
            var card = new VisualElement();
            card.AddToClassList("action-item-card");

            if (vm.isConsumed) card.AddToClassList("consumed");
            else if (vm.isOnCooldown) card.AddToClassList("on-cooldown");

            // Coluna de Informações
            var infoCol = new VisualElement();
            infoCol.AddToClassList("action-info-col");

            // Linha do Nome + Badge de Categoria
            var nameRow = new VisualElement();
            nameRow.AddToClassList("action-name-row");

            if (!string.IsNullOrEmpty(vm.categoryTag))
            {
                var catBadge = new Label(vm.categoryTag.ToUpper());
                catBadge.AddToClassList("action-category-badge");
                nameRow.Add(catBadge);
            }

            var nameLabel = new Label(vm.displayName);
            nameLabel.AddToClassList("action-item-name");
            nameRow.Add(nameLabel);
            infoCol.Add(nameRow);

            // Descrição
            if (!string.IsNullOrEmpty(vm.description))
            {
                var descLabel = new Label(vm.description);
                descLabel.AddToClassList("action-item-desc");
                infoCol.Add(descLabel);
            }

            // Chips de Impactos / Tags
            if (vm.impactTags != null && vm.impactTags.Count > 0)
            {
                var tagsRow = new VisualElement();
                tagsRow.AddToClassList("action-tags-row");

                foreach (var tag in vm.impactTags)
                {
                    if (string.IsNullOrEmpty(tag)) continue;
                    var chip = new Label(tag);
                    chip.AddToClassList("action-impact-chip");
                    if (tag.StartsWith("-") || tag.IndexOf("perda", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        chip.AddToClassList("negative");
                    }
                    tagsRow.Add(chip);
                }
                infoCol.Add(tagsRow);
            }

            card.Add(infoCol);

            // Botão de Ação
            var btn = new Button();
            btn.AddToClassList("action-exec-btn");

            if (vm.isConsumed)
            {
                btn.text = "UTILIZADO";
                btn.SetEnabled(false);
                btn.AddToClassList("btn-consumed");
            }
            else if (vm.isOnCooldown)
            {
                btn.text = $"RECARGA ({vm.cooldownTurnsRemaining}t)";
                btn.SetEnabled(false);
                btn.AddToClassList("btn-cooldown");
            }
            else if (!vm.isAvailable)
            {
                btn.text = !string.IsNullOrEmpty(vm.statusText) ? vm.statusText : "INDISPONÍVEL";
                btn.SetEnabled(false);
                btn.AddToClassList("btn-cooldown");
            }
            else
            {
                bool isContact = vm.categoryTag != null && vm.categoryTag.IndexOf("Contato", StringComparison.OrdinalIgnoreCase) >= 0;
                btn.text = isContact ? "LIGAR" : "EXECUTAR";
                btn.SetEnabled(true);
                btn.clicked += () =>
                {
                    Debug.Log($"<color=#6a9fb5>[FlipPhonePresenter]</color> Botão clicado para a ação: '<b>{vm.id}</b>' ({vm.displayName})");
                    OnActionRequested?.Invoke(vm.id);
                };
            }

            card.Add(btn);

            return card;
        }

        public void ShowMessage(string message, bool isError = false)
        {
            if (statusLabel == null) return;

            statusLabel.text = message;
            statusLabel.RemoveFromClassList("success");
            statusLabel.RemoveFromClassList("error");

            if (isError)
            {
                statusLabel.AddToClassList("error");
            }
            else
            {
                statusLabel.AddToClassList("success");
            }
        }
    }
}
