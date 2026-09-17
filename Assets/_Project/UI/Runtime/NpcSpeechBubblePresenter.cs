using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mandato.UI
{
    /// <summary>
    /// Presenter do balão de fala de NPCs em UI Toolkit posicionado como footer em tela cheia (30% da altura da tela),
    /// utilizando sprite invertido horizontalmente e largura de 100%.
    /// </summary>
    public class NpcSpeechBubblePresenter : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset uxmlAsset;
        [SerializeField] private PanelSettings panelSettings;

        [Header("Configurações Visuais")]
        [Tooltip("Quando ativado, o fundo do balão é invertido horizontalmente para apontar para a esquerda/NPC.")]
        [SerializeField] private bool flipHorizontal = true;

        [Tooltip("Exibir botão de avançar além do prompt de atalho.")]
        [SerializeField] private bool showAdvanceButton = true;

        [Header("Efeito de Digitação (Typewriter)")]
        [SerializeField] private bool enableTypewriter = false;
        [SerializeField] private float charactersPerSecond = 45f;

        [Header("Atalhos")]
        [SerializeField] private bool enableKeyboardShortcuts = true;
        [SerializeField] private KeyCode primaryAdvanceKey = KeyCode.Space;
        [SerializeField] private KeyCode secondaryAdvanceKey = KeyCode.Return;

        public event Action<string, string> OnSpeechShown;
        public event Action OnSpeechCompleted;
        public event Action OnSpeechHidden;
        public event Action OnBubbleClicked;

        private VisualElement root;
        private VisualElement footerContainer;
        private VisualElement speechBubbleBg;
        private Label speakerBadge;
        private Label speechTextLabel;
        private Label speechPromptLabel;
        private Button speechAdvanceBtn;

        private string currentSpeaker = string.Empty;
        private string currentText = string.Empty;
        private bool isVisible = false;
        private bool isTyping = false;
        private Coroutine typewriterCoroutine;
        private Action pendingOnCompleteCallback;

        public bool IsVisible => isVisible;
        public bool IsTyping => isTyping;
        public string CurrentSpeaker => currentSpeaker;
        public string CurrentText => currentText;

        private void Awake()
        {
            EnsureDocument();
            CacheElements();
        }

        private void OnEnable()
        {
            EnsureDocument();
            CacheElements();
            SubscribeEvents();
            ApplyOrientation();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
                typewriterCoroutine = null;
            }
            isTyping = false;
        }

        private void Update()
        {
            if (!enableKeyboardShortcuts || !isVisible) return;

            if (Input.GetKeyDown(primaryAdvanceKey) || Input.GetKeyDown(secondaryAdvanceKey))
            {
                HandleAdvanceAction();
            }
        }

        public void EnsureDocument()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>() ?? GetComponentInChildren<UIDocument>();
            }

            if (uiDocument == null)
            {
                uiDocument = gameObject.AddComponent<UIDocument>();
            }

            if (uiDocument.panelSettings == null)
            {
                if (panelSettings != null)
                {
                    uiDocument.panelSettings = panelSettings;
                }
                else
                {
#if UNITY_EDITOR
                    var screenPanel = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/Decision/DecisionPanelSettings.asset");
                    if (screenPanel != null) uiDocument.panelSettings = screenPanel;
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
                    var uxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_Project/UI/UXML/NpcSpeechBubble.uxml");
                    if (uxml != null) uiDocument.visualTreeAsset = uxml;
#endif
                }
            }
        }

        private void CacheElements()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            root = uiDocument.rootVisualElement;
            footerContainer = root.Q<VisualElement>("speech-footer-container");
            speechBubbleBg = root.Q<VisualElement>("speech-bubble-bg");
            speakerBadge = root.Q<Label>("speaker-badge");
            speechTextLabel = root.Q<Label>("speech-text-label");
            speechPromptLabel = root.Q<Label>("speech-prompt-label");
            speechAdvanceBtn = root.Q<Button>("speech-advance-btn");
        }

        private void SubscribeEvents()
        {
            if (speechAdvanceBtn != null)
            {
                speechAdvanceBtn.clicked -= HandleAdvanceAction;
                speechAdvanceBtn.clicked += HandleAdvanceAction;
            }

            if (footerContainer != null)
            {
                footerContainer.UnregisterCallback<ClickEvent>(HandleContainerClicked);
                footerContainer.RegisterCallback<ClickEvent>(HandleContainerClicked);
            }
        }

        private void UnsubscribeEvents()
        {
            if (speechAdvanceBtn != null)
            {
                speechAdvanceBtn.clicked -= HandleAdvanceAction;
            }

            if (footerContainer != null)
            {
                footerContainer.UnregisterCallback<ClickEvent>(HandleContainerClicked);
            }
        }

        private void HandleContainerClicked(ClickEvent evt)
        {
            OnBubbleClicked?.Invoke();
            HandleAdvanceAction();
        }

        /// <summary>
        /// Exibe o diálogo com o nome do interlocutor e texto via script.
        /// </summary>
        public void Show(string speakerName, string dialogueText, bool? useTypewriter = null, Action onComplete = null)
        {
            EnsureDocument();
            CacheElements();

            currentSpeaker = speakerName ?? string.Empty;
            currentText = dialogueText ?? string.Empty;
            pendingOnCompleteCallback = onComplete;

            UpdateSpeakerBadge(currentSpeaker);

            bool shouldType = useTypewriter ?? enableTypewriter;
            if (shouldType && Application.isPlaying && gameObject.activeInHierarchy)
            {
                StartTypewriter(currentText);
            }
            else
            {
                if (speechTextLabel != null) speechTextLabel.text = currentText;
                isTyping = false;
            }

            SetVisible(true);
            OnSpeechShown?.Invoke(currentSpeaker, currentText);
        }

        /// <summary>
        /// Sobrecarga para exibir apenas o texto sem alterar o interlocutor.
        /// </summary>
        public void Show(string dialogueText)
        {
            Show(currentSpeaker, dialogueText);
        }

        /// <summary>
        /// Atualiza o texto do diálogo diretamente sem disparar animação de entrada.
        /// </summary>
        public void SetSpeech(string dialogueText)
        {
            currentText = dialogueText ?? string.Empty;
            if (speechTextLabel != null) speechTextLabel.text = currentText;
        }

        /// <summary>
        /// Atualiza o interlocutor e o texto de diálogo.
        /// </summary>
        public void SetSpeech(string speakerName, string dialogueText)
        {
            currentSpeaker = speakerName ?? string.Empty;
            currentText = dialogueText ?? string.Empty;

            UpdateSpeakerBadge(currentSpeaker);
            if (speechTextLabel != null) speechTextLabel.text = currentText;
        }

        /// <summary>
        /// Define apenas o nome do interlocutor exibido no badge.
        /// </summary>
        public void SetSpeaker(string speakerName)
        {
            currentSpeaker = speakerName ?? string.Empty;
            UpdateSpeakerBadge(currentSpeaker);
        }

        /// <summary>
        /// Alterna a visibilidade do HUD do balão de fala.
        /// </summary>
        public void SetVisible(bool visible)
        {
            isVisible = visible;
            EnsureDocument();
            CacheElements();

            if (footerContainer != null)
            {
                if (visible)
                {
                    footerContainer.RemoveFromClassList("hidden");
                    footerContainer.style.display = DisplayStyle.Flex;
                }
                else
                {
                    footerContainer.AddToClassList("hidden");
                    footerContainer.style.display = DisplayStyle.None;
                }
            }

            if (!visible)
            {
                if (typewriterCoroutine != null)
                {
                    StopCoroutine(typewriterCoroutine);
                    typewriterCoroutine = null;
                }
                isTyping = false;
                OnSpeechHidden?.Invoke();
            }
        }

        /// <summary>
        /// Esconde o balão de fala.
        /// </summary>
        public void Hide()
        {
            SetVisible(false);
        }

        /// <summary>
        /// Inverte horizontalmente o fundo do balão (true: tail na esquerda, false: tail na direita).
        /// </summary>
        public void SetFlipped(bool flipped)
        {
            flipHorizontal = flipped;
            ApplyOrientation();
        }

        private void ApplyOrientation()
        {
            if (speechBubbleBg == null) return;

            if (flipHorizontal)
            {
                speechBubbleBg.RemoveFromClassList("flip-right");
                speechBubbleBg.style.scale = new StyleScale(new Scale(new Vector2(-1f, 1f)));
            }
            else
            {
                speechBubbleBg.AddToClassList("flip-right");
                speechBubbleBg.style.scale = new StyleScale(new Scale(new Vector2(1f, 1f)));
            }

            if (speechAdvanceBtn != null)
            {
                if (showAdvanceButton)
                    speechAdvanceBtn.RemoveFromClassList("hidden");
                else
                    speechAdvanceBtn.AddToClassList("hidden");
            }
        }

        private void UpdateSpeakerBadge(string speaker)
        {
            if (speakerBadge == null) return;

            if (string.IsNullOrWhiteSpace(speaker))
            {
                speakerBadge.text = string.Empty;
                speakerBadge.AddToClassList("hidden");
                speakerBadge.style.display = DisplayStyle.None;
            }
            else
            {
                speakerBadge.text = speaker;
                speakerBadge.RemoveFromClassList("hidden");
                speakerBadge.style.display = DisplayStyle.Flex;
            }
        }

        private void StartTypewriter(string fullText)
        {
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
            }
            typewriterCoroutine = StartCoroutine(TypewriterRoutine(fullText));
        }

        private IEnumerator TypewriterRoutine(string fullText)
        {
            isTyping = true;
            if (speechTextLabel != null) speechTextLabel.text = string.Empty;

            float delay = charactersPerSecond > 0 ? 1f / charactersPerSecond : 0.02f;
            int length = fullText.Length;

            for (int i = 1; i <= length; i++)
            {
                if (speechTextLabel != null)
                {
                    speechTextLabel.text = fullText.Substring(0, i);
                }
                yield return new WaitForSeconds(delay);
            }

            isTyping = false;
            typewriterCoroutine = null;
        }

        /// <summary>
        /// Pula a animação do typewriter imediatamente para o texto completo.
        /// </summary>
        public void SkipTypewriter()
        {
            if (!isTyping) return;

            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
                typewriterCoroutine = null;
            }

            if (speechTextLabel != null)
            {
                speechTextLabel.text = currentText;
            }
            isTyping = false;
        }

        /// <summary>
        /// Ação de avançar diálogo (se digitando, completa o texto; se completo, dispara callback de conclusão).
        /// </summary>
        public void HandleAdvanceAction()
        {
            if (isTyping)
            {
                SkipTypewriter();
                return;
            }

            var callback = pendingOnCompleteCallback;
            pendingOnCompleteCallback = null;

            callback?.Invoke();
            OnSpeechCompleted?.Invoke();
        }
    }
}
