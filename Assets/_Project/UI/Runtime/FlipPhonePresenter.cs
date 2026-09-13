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
        [SerializeField] private GameObject phoneGameObject;

        public KeyCode ToggleKey { get => toggleKey; set => toggleKey = value; }
        public bool AllowKeyboardToggle { get => allowKeyboardToggle; set => allowKeyboardToggle = value; }
        public GameObject PhoneGameObject { get => phoneGameObject != null ? phoneGameObject : gameObject; set => phoneGameObject = value; }
        public GameObject GetPhoneGameObject() => PhoneGameObject;
        public void SetPhoneGameObject(GameObject go) => phoneGameObject = go;

        public event Action<string> OnActionRequested;
        public event Action OnPhoneOpened;
        public event Action OnPhoneClosed;

        public bool IsOpen { get; private set; } = false;
        public bool IsModalOpen => isModalOpen;

        public bool IsPhoneOpen()
        {
            if (screenRoot != null && screenRoot.style.display == DisplayStyle.Flex) return true;
            if (phoneGameObject != null && phoneGameObject.activeSelf) return true;
            return IsOpen;
        }

        private UIDocument uiDocument;
        private VisualElement root;
        private VisualElement screenRoot;
        private VisualElement appsContainer;
        private Label emptyLabel;
        private Button closeBtn;

        // Modal de Ação / Diálogo Inferior
        private VisualElement modalBackdrop;
        private VisualElement modalAppIcon;
        private Label modalTitle;
        private Label modalCategoryBadge;
        private Label modalDesc;
        private Button modalBackBtn;
        private Button modalExecBtn;

        private bool isModalOpen = false;
        private FlipPhoneActionViewModel selectedAction;
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
            if (!IsOpen)
            {
                Close();
            }
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                if (isModalOpen)
                {
                    CloseActionModal();
                }
                else
                {
                    Close();
                }
            }
        }

        public void EnsureDocument()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>() ?? GetComponentInChildren<UIDocument>() ?? GetComponentInParent<UIDocument>();
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
#if UNITY_EDITOR
                var loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_Project/UI/UXML/FlipPhone.uxml");
                if (loaded != null) uiDocument.visualTreeAsset = loaded;
#else
                var loaded = Resources.Load<VisualTreeAsset>("FlipPhone");
                if (loaded != null) uiDocument.visualTreeAsset = loaded;
#endif
            }
        }

        public void CacheVisualElements()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            root = uiDocument.rootVisualElement;
            screenRoot = root.Q<VisualElement>("phone-screen-root");
            appsContainer = root.Q<VisualElement>("apps-grid-container");
            emptyLabel = root.Q<Label>("empty-actions-label");
            closeBtn = root.Q<Button>("phone-close-btn");

            if (closeBtn != null)
            {
                closeBtn.clicked -= Close;
                closeBtn.clicked += Close;
            }

            // Modal de Diálogo Inferior
            modalBackdrop = root.Q<VisualElement>("action-modal-backdrop");
            modalAppIcon = root.Q<VisualElement>("modal-app-icon");
            modalTitle = root.Q<Label>("modal-action-title");
            modalCategoryBadge = root.Q<Label>("modal-category-badge");
            modalDesc = root.Q<Label>("modal-action-desc");
            modalBackBtn = root.Q<Button>("modal-back-btn");
            modalExecBtn = root.Q<Button>("modal-exec-btn");

            if (modalBackBtn != null)
            {
                modalBackBtn.clicked -= CloseActionModal;
                modalBackBtn.clicked += CloseActionModal;
            }

            if (modalExecBtn != null)
            {
                modalExecBtn.clicked -= HandleModalExecClicked;
                modalExecBtn.clicked += HandleModalExecClicked;
            }
        }

        public void SetCategoryFilter(string filterName)
        {
            // Abas foram removidas para simplificação e foco em grade de aplicativos.
            // Mantido para compatibilidade com assinaturas antigas.
        }

        public void Open()
        {
            IsOpen = true;

            if (phoneGameObject != null)
            {
                phoneGameObject.SetActive(true);
            }

            EnsureDocument();
            CacheVisualElements();
            CloseActionModal();

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
            CloseActionModal();

            if (screenRoot != null)
            {
                screenRoot.style.display = DisplayStyle.None;
                screenRoot.AddToClassList("hidden");
                screenRoot.style.opacity = 0f;
                screenRoot.Blur();
            }

            if (root != null)
            {
                root.Blur();
            }

            if (phoneGameObject != null)
            {
                phoneGameObject.SetActive(false);
            }

            Debug.Log("<color=#6a9fb5>[FlipPhonePresenter]</color> UI Toolkit: Tela do celular ocultada.");
            OnPhoneClosed?.Invoke();
        }

        public void Toggle()
        {
            if (IsPhoneOpen()) Close();
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
            RebuildAppTiles();

            Debug.Log($"<color=#6a9fb5>[FlipPhonePresenter]</color> Interface atualizada com {cachedViewModels.Count} aplicativos.");
        }

        public void OpenActionModal(FlipPhoneActionViewModel vm)
        {
            if (vm == null) return;
            selectedAction = vm;

            EnsureDocument();
            CacheVisualElements();

            if (modalTitle != null)
            {
                modalTitle.text = vm.displayName;
            }

            if (modalDesc != null)
            {
                modalDesc.text = !string.IsNullOrEmpty(vm.description) ? vm.description : "Sem descrição disponível.";
            }

            if (modalCategoryBadge != null)
            {
                modalCategoryBadge.text = !string.IsNullOrEmpty(vm.categoryTag) ? vm.categoryTag.ToUpper() : "GABINETE";
            }

            if (modalAppIcon != null)
            {
                if (vm.icon != null)
                {
                    modalAppIcon.style.backgroundImage = new StyleBackground(vm.icon);
                    modalAppIcon.style.display = DisplayStyle.Flex;
                }
                else
                {
                    modalAppIcon.style.backgroundImage = StyleKeyword.None;
                    modalAppIcon.style.display = DisplayStyle.None;
                }
            }

            if (modalExecBtn != null)
            {
                modalExecBtn.RemoveFromClassList("btn-disabled");

                if (vm.isConsumed)
                {
                    modalExecBtn.text = "UTILIZADO";
                    modalExecBtn.SetEnabled(false);
                    modalExecBtn.AddToClassList("btn-disabled");
                }
                else if (vm.isOnCooldown)
                {
                    modalExecBtn.text = $"RECARGA ({vm.cooldownTurnsRemaining}T)";
                    modalExecBtn.SetEnabled(false);
                    modalExecBtn.AddToClassList("btn-disabled");
                }
                else if (!vm.isAvailable)
                {
                    modalExecBtn.text = !string.IsNullOrEmpty(vm.statusText) ? vm.statusText.ToUpper() : "INDISPONIVEL";
                    modalExecBtn.SetEnabled(false);
                    modalExecBtn.AddToClassList("btn-disabled");
                }
                else
                {
                    bool isContact = vm.categoryTag != null && vm.categoryTag.IndexOf("Contato", StringComparison.OrdinalIgnoreCase) >= 0;
                    modalExecBtn.text = isContact ? "LIGAR" : "EXECUTAR";
                    modalExecBtn.SetEnabled(true);
                }
            }

            if (modalBackdrop != null)
            {
                modalBackdrop.RemoveFromClassList("hidden");
                modalBackdrop.style.display = DisplayStyle.Flex;
            }

            isModalOpen = true;
            Debug.Log($"<color=#6a9fb5>[FlipPhonePresenter]</color> Modal de detalhes aberto para o app: <b>{vm.displayName}</b>");
        }

        public void CloseActionModal()
        {
            if (modalBackdrop != null)
            {
                modalBackdrop.AddToClassList("hidden");
                modalBackdrop.style.display = DisplayStyle.None;
            }

            selectedAction = null;
            isModalOpen = false;
        }

        public void HandleModalExecClicked()
        {
            if (selectedAction == null) return;

            if (selectedAction.isAvailable && !selectedAction.isOnCooldown && !selectedAction.isConsumed)
            {
                string actionId = selectedAction.id;
                Debug.Log($"<color=#6a9fb5>[FlipPhonePresenter]</color> Executando ação confirmada no modal: '<b>{actionId}</b>'");
                CloseActionModal();
                OnActionRequested?.Invoke(actionId);
            }
        }

        private void RebuildAppTiles()
        {
            if (appsContainer == null) return;

            appsContainer.Clear();

            int visibleCount = 0;

            foreach (var vm in cachedViewModels)
            {
                if (vm == null) continue;

                visibleCount++;
                var tile = CreateAppTile(vm);
                appsContainer.Add(tile);
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

        private VisualElement CreateAppTile(FlipPhoneActionViewModel vm)
        {
            var tile = new VisualElement();
            tile.AddToClassList("app-tile");
            tile.pickingMode = PickingMode.Position;

            if (vm.isConsumed)
            {
                tile.AddToClassList("consumed");
            }
            else if (vm.isOnCooldown)
            {
                tile.AddToClassList("on-cooldown");

                var badge = new Label($"{vm.cooldownTurnsRemaining}T");
                badge.AddToClassList("app-badge");
                badge.AddToClassList("badge-cooldown");
                badge.pickingMode = PickingMode.Ignore;
                tile.Add(badge);
            }
            else if (!vm.isAvailable)
            {
                tile.AddToClassList("unavailable");
            }

            // Ícone do Aplicativo ou Fallback estilizado
            if (vm.icon != null)
            {
                var iconEl = new VisualElement();
                iconEl.AddToClassList("app-icon");
                iconEl.style.backgroundImage = new StyleBackground(vm.icon);
                iconEl.pickingMode = PickingMode.Ignore;
                tile.Add(iconEl);
            }
            else
            {
                var fallbackBox = new VisualElement();
                fallbackBox.AddToClassList("app-fallback-box");
                fallbackBox.pickingMode = PickingMode.Ignore;

                string initial = !string.IsNullOrEmpty(vm.displayName) ? vm.displayName.Substring(0, 1).ToUpper() : "?";
                var letter = new Label(initial);
                letter.AddToClassList("app-fallback-letter");
                letter.pickingMode = PickingMode.Ignore;

                fallbackBox.Add(letter);
                tile.Add(fallbackBox);
            }

            // Título do Aplicativo
            var titleLabel = new Label(vm.displayName);
            titleLabel.AddToClassList("app-title");
            titleLabel.pickingMode = PickingMode.Ignore;
            tile.Add(titleLabel);

            // Clique no tile abre o modal de diálogo na base
            tile.RegisterCallback<ClickEvent>(evt =>
            {
                OpenActionModal(vm);
            });

            return tile;
        }

        public void ShowMessage(string message, bool isError = false)
        {
            // Mantido para compatibilidade com chamadas de status
        }
    }
}
