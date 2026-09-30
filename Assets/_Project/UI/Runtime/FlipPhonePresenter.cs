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
        public string categoryTag = "Contatos";
        public string linkedNpcId = string.Empty;
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
        [SerializeField] private bool isInteractable = true;
        [SerializeField] private GameObject phoneGameObject;

        [Header("Áudio / Sons do Celular")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip contactHoverSound;
        [SerializeField] private AudioClip buttonClickSound;
        [Range(0f, 1f)] [SerializeField] private float contactHoverSoundVolume = 0.7f;
        [Range(0f, 1f)] [SerializeField] private float buttonClickSoundVolume = 0.8f;

        public KeyCode ToggleKey { get => toggleKey; set => toggleKey = value; }
        public bool AllowKeyboardToggle { get => allowKeyboardToggle; set => allowKeyboardToggle = value; }
        public bool IsInteractable { get => isInteractable; set => SetInteractable(value); }
        public GameObject PhoneGameObject { get => phoneGameObject != null ? phoneGameObject : gameObject; set => phoneGameObject = value; }
        public GameObject GetPhoneGameObject() => PhoneGameObject;
        public void SetPhoneGameObject(GameObject go) => phoneGameObject = go;

        public AudioSource AudioSource { get => audioSource; set => audioSource = value; }
        public AudioClip ContactHoverSound { get => contactHoverSound; set => contactHoverSound = value; }
        public AudioClip ButtonClickSound { get => buttonClickSound; set => buttonClickSound = value; }
        public float ContactHoverSoundVolume { get => contactHoverSoundVolume; set => contactHoverSoundVolume = value; }
        public float ButtonClickSoundVolume { get => buttonClickSoundVolume; set => buttonClickSoundVolume = value; }

        public void SetInteractable(bool value)
        {
            isInteractable = value;
            if (!value && (IsOpen || IsPhoneOpen()))
            {
                Close();
            }
        }

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

        private UIModalCoordinator modalCoordinator;

        public void SetModalCoordinator(UIModalCoordinator coordinator)
        {
            modalCoordinator = coordinator;
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

        private VisualElement currentHoveredContactRow;
        private float lastContactHoverSoundTime = -1f;
        private float lastButtonClickSoundTime = -1f;
        private const float minContactHoverInterval = 0.05f;
        private const float minButtonClickInterval = 0.05f;

        private void Awake()
        {
            EnsureDocument();
            EnsureAudioAssets();
            EnsureAudioSource();
            CacheVisualElements();
        }

        private void OnEnable()
        {
            EnsureDocument();
            EnsureAudioAssets();
            EnsureAudioSource();
            CacheVisualElements();
        }

        private void OnValidate()
        {
            EnsureAudioAssets();
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
            if (!isInteractable) return;

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

        public void EnsureAudioAssets()
        {
#if UNITY_EDITOR
            if (contactHoverSound == null)
            {
                contactHoverSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audios/phone-type3.mp3");
            }
            if (buttonClickSound == null)
            {
                buttonClickSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audios/phone-type1.mp3");
            }
#endif
        }

        public void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>() ?? GetComponentInChildren<AudioSource>() ?? GetComponentInParent<AudioSource>();
            }

            if (audioSource == null && gameObject != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
        }

        public void PlayContactHoverSound()
        {
            if (contactHoverSound == null) return;
            if (Time.unscaledTime - lastContactHoverSoundTime < minContactHoverInterval) return;
            lastContactHoverSoundTime = Time.unscaledTime;

            EnsureAudioSource();
            if (audioSource != null)
            {
                audioSource.PlayOneShot(contactHoverSound, contactHoverSoundVolume);
            }
        }

        public void PlayButtonClickSound()
        {
            if (buttonClickSound == null) return;
            if (Time.unscaledTime - lastButtonClickSoundTime < minButtonClickInterval) return;
            lastButtonClickSoundTime = Time.unscaledTime;

            EnsureAudioSource();
            if (audioSource != null)
            {
                audioSource.PlayOneShot(buttonClickSound, buttonClickSoundVolume);
            }
        }

        private void HandleContactHover(VisualElement row)
        {
            if (currentHoveredContactRow == row) return;
            currentHoveredContactRow = row;
            PlayContactHoverSound();
        }

        private void HandleContactLeave(VisualElement row)
        {
            if (currentHoveredContactRow == row)
            {
                currentHoveredContactRow = null;
            }
        }

        private void HandleCloseClicked()
        {
            PlayButtonClickSound();
            Close();
        }

        private void HandleModalBackClicked()
        {
            PlayButtonClickSound();
            CloseActionModal();
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
            appsContainer = root.Q<VisualElement>("apps-grid-container") ?? root.Q<VisualElement>("contacts-list-container");
            emptyLabel = root.Q<Label>("empty-actions-label");
            closeBtn = root.Q<Button>("phone-close-btn");

            if (closeBtn != null)
            {
                closeBtn.userData = (Action)HandleCloseClicked;
                closeBtn.clicked -= HandleCloseClicked;
                closeBtn.clicked += HandleCloseClicked;
            }

            // Modal de Diálogo
            modalBackdrop = root.Q<VisualElement>("action-modal-backdrop");
            modalAppIcon = root.Q<VisualElement>("modal-app-icon");
            modalTitle = root.Q<Label>("modal-action-title");
            modalCategoryBadge = root.Q<Label>("modal-category-badge");
            modalDesc = root.Q<Label>("modal-action-desc");
            modalBackBtn = root.Q<Button>("modal-back-btn");
            modalExecBtn = root.Q<Button>("modal-exec-btn");

            if (modalBackBtn != null)
            {
                modalBackBtn.userData = (Action)HandleModalBackClicked;
                modalBackBtn.clicked -= HandleModalBackClicked;
                modalBackBtn.clicked += HandleModalBackClicked;
            }

            if (modalExecBtn != null)
            {
                modalExecBtn.userData = (Action)HandleModalExecClicked;
                modalExecBtn.clicked -= HandleModalExecClicked;
                modalExecBtn.clicked += HandleModalExecClicked;
            }
        }

        public void SetCategoryFilter(string filterName)
        {
            // Mantido para compatibilidade com assinaturas antigas.
        }

        public void Open()
        {
            if (!isInteractable) return;
            if (modalCoordinator != null && !modalCoordinator.CanOpenFlipPhone()) return;

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

            Debug.Log("<color=#6a9fb5>[FlipPhonePresenter]</color> UI Toolkit: Agenda de contatos exibida.");
            OnPhoneOpened?.Invoke();
        }

        public void Close()
        {
            IsOpen = false;
            CloseActionModal();
            currentHoveredContactRow = null;

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
            if (!isInteractable) return;
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

            Debug.Log($"<color=#6a9fb5>[FlipPhonePresenter]</color> Agenda atualizada com {cachedViewModels.Count} contatos.");
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
                modalCategoryBadge.text = !string.IsNullOrEmpty(vm.categoryTag) ? vm.categoryTag.ToUpper() : "CONTATO";
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
                    modalExecBtn.text = "LIGAR";
                    modalExecBtn.SetEnabled(true);
                }
            }

            if (modalBackdrop != null)
            {
                modalBackdrop.RemoveFromClassList("hidden");
                modalBackdrop.style.display = DisplayStyle.Flex;
            }

            isModalOpen = true;
            Debug.Log($"<color=#6a9fb5>[FlipPhonePresenter]</color> Modal de detalhes aberto para o contato: <b>{vm.displayName}</b>");
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

            PlayButtonClickSound();

            if (selectedAction.isAvailable && !selectedAction.isOnCooldown && !selectedAction.isConsumed)
            {
                string actionId = selectedAction.id;
                Debug.Log($"<color=#6a9fb5>[FlipPhonePresenter]</color> Executando ligação confirmada: '<b>{actionId}</b>'");
                CloseActionModal();
                OnActionRequested?.Invoke(actionId);
            }
        }

        private void RebuildAppTiles()
        {
            if (appsContainer == null) return;

            currentHoveredContactRow = null;
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

        public VisualElement CreateAppTile(FlipPhoneActionViewModel vm)
        {
            var row = new VisualElement();
            row.name = $"contact-row-{(vm != null && !string.IsNullOrEmpty(vm.id) ? vm.id : "unknown")}";
            row.AddToClassList("contact-row");
            row.AddToClassList("app-tile"); // Mantém compatibilidade com seletores e world-space interaction
            row.pickingMode = PickingMode.Position;

            if (vm.isConsumed)
            {
                row.AddToClassList("consumed");
            }
            else if (vm.isOnCooldown)
            {
                row.AddToClassList("on-cooldown");
            }
            else if (!vm.isAvailable)
            {
                row.AddToClassList("unavailable");
            }

            // 1. Ícone do Contato à esquerda
            var iconBox = new VisualElement();
            iconBox.AddToClassList("contact-icon-box");
            iconBox.pickingMode = PickingMode.Ignore;

            if (vm.icon != null)
            {
                var iconEl = new VisualElement();
                iconEl.AddToClassList("contact-icon");
                iconEl.AddToClassList("app-icon");
                iconEl.style.backgroundImage = new StyleBackground(vm.icon);
                iconEl.pickingMode = PickingMode.Ignore;
                iconBox.Add(iconEl);
            }
            else
            {
                var fallbackBox = new VisualElement();
                fallbackBox.AddToClassList("contact-fallback-box");
                fallbackBox.AddToClassList("app-fallback-box");
                fallbackBox.pickingMode = PickingMode.Ignore;

                var fallbackSymbol = new Label("📞");
                fallbackSymbol.AddToClassList("contact-fallback-symbol");
                fallbackSymbol.pickingMode = PickingMode.Ignore;
                fallbackBox.Add(fallbackSymbol);
                iconBox.Add(fallbackBox);
            }
            row.Add(iconBox);

            // 2. Informações do Contato (Centro)
            var infoBox = new VisualElement();
            infoBox.AddToClassList("contact-info");
            infoBox.pickingMode = PickingMode.Ignore;

            var titleLabel = new Label(vm.displayName);
            titleLabel.AddToClassList("contact-name");
            titleLabel.AddToClassList("app-title");
            titleLabel.pickingMode = PickingMode.Ignore;
            infoBox.Add(titleLabel);

            string tagText = !string.IsNullOrEmpty(vm.categoryTag) ? vm.categoryTag.ToUpper() : "CONTATO";
            var tagLabel = new Label(tagText);
            tagLabel.AddToClassList("contact-tag");
            tagLabel.pickingMode = PickingMode.Ignore;
            infoBox.Add(tagLabel);

            row.Add(infoBox);

            // 3. Indicador de Status / Ação à direita
            var callBadge = new Label();
            callBadge.AddToClassList("contact-call-badge");
            callBadge.pickingMode = PickingMode.Ignore;

            if (vm.isConsumed)
            {
                callBadge.text = "USADO";
                callBadge.AddToClassList("badge-consumed");
            }
            else if (vm.isOnCooldown)
            {
                callBadge.text = $"{vm.cooldownTurnsRemaining}T RECARGA";
                callBadge.AddToClassList("badge-cooldown");
            }
            else if (!vm.isAvailable)
            {
                callBadge.text = !string.IsNullOrEmpty(vm.statusText) ? vm.statusText.ToUpper() : "BLOQUEADO";
                callBadge.AddToClassList("badge-unavailable");
            }
            else
            {
                callBadge.text = "LIGAR 📞";
                callBadge.AddToClassList("badge-call");
            }
            row.Add(callBadge);

            // Hover no contato -> som phone-type3.mp3
            row.RegisterCallback<PointerEnterEvent>(evt => HandleContactHover(row));
            row.RegisterCallback<PointerLeaveEvent>(evt => HandleContactLeave(row));

            // Clique na linha inteira toca som de botão (phone-type1.mp3) e abre o modal de confirmação de ligação
            Action onContactClicked = () =>
            {
                PlayButtonClickSound();
                OpenActionModal(vm);
            };
            row.userData = onContactClicked;
            row.RegisterCallback<ClickEvent>(evt => onContactClicked());

            return row;
        }

        public void ShowMessage(string message, bool isError = false)
        {
            // Mantido para compatibilidade com chamadas de status
        }
    }
}
