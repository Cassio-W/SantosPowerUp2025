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
        [SerializeField] private bool flipHorizontal = false;

        [Tooltip("Exibir botão de avançar além do prompt de atalho.")]
        [SerializeField] private bool showAdvanceButton = true;

        [Header("Efeito de Digitação (Typewriter)")]
        [SerializeField] private bool enableTypewriter = true;
        [SerializeField] private float charactersPerSecond = 45f;

        [Header("Áudio de Digitação")]
        [SerializeField] private AudioClip typewriterSound;
        [Range(0f, 1f)] [SerializeField] private float typewriterSoundVolume = 0.35f;
        [SerializeField] private Vector2 typewriterPitchVariation = new Vector2(0.95f, 1.05f);
        [SerializeField] private float minTypewriterSoundInterval = 0.04f;
        [SerializeField] private AudioSource audioSource;

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
        private VisualElement speechTextArea;
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
        private float lastTypewriterSoundTime = -1f;

        public bool IsVisible => isVisible;
        public bool IsTyping => isTyping;
        public string CurrentSpeaker => currentSpeaker;
        public string CurrentText => currentText;

        public bool EnableTypewriter
        {
            get => enableTypewriter;
            set => enableTypewriter = value;
        }

        public float CharactersPerSecond
        {
            get => charactersPerSecond;
            set => charactersPerSecond = value;
        }

        public AudioClip TypewriterSound
        {
            get => typewriterSound;
            set => typewriterSound = value;
        }

        private void Awake()
        {
            EnsureDocument();
            CacheElements();
            SetVisible(false);
        }

        private void OnEnable()
        {
            EnsureDocument();
            CacheElements();
            SubscribeEvents();
            ApplyOrientation();
            if (!isVisible)
            {
                SetVisible(false);
            }
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
                typewriterCoroutine = null;
            }
            StopTypewriterSound();
            isTyping = false;
        }

        private void Update()
        {
            if (!isVisible) return;

            if (Input.GetMouseButtonDown(0) || 
                (enableKeyboardShortcuts && (Input.GetKeyDown(primaryAdvanceKey) || Input.GetKeyDown(secondaryAdvanceKey))))
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

            if (uiDocument.panelSettings != null && uiDocument.panelSettings.targetTexture == null)
            {
                uiDocument.panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                uiDocument.panelSettings.referenceResolution = new Vector2Int(1920, 1080);
                uiDocument.panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                uiDocument.panelSettings.match = 0.5f;
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

            EnsureAudioSource();
        }

        private void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>() ?? GetComponentInChildren<AudioSource>();
            }

            if (audioSource == null && Application.isPlaying)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // Garante som 2D para UI, sem perda de volume por distância
            }
        }

        private void CacheElements()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            root = uiDocument.rootVisualElement;
            footerContainer = root.Q<VisualElement>("speech-footer-container");
            speechBubbleBg = root.Q<VisualElement>("speech-bubble-bg");
            speechTextArea = root.Q<VisualElement>("speech-text-area");
            speakerBadge = root.Q<Label>("speaker-badge");
            speechTextLabel = root.Q<Label>("speech-text-label");
            speechPromptLabel = root.Q<Label>("speech-prompt-label");
            speechAdvanceBtn = root.Q<Button>("btn-continue") ?? root.Q<Button>("speech-advance-btn");
        }

        private void SubscribeEvents()
        {
            if (speechAdvanceBtn != null)
            {
                speechAdvanceBtn.clicked -= HandleAdvanceAction;
                speechAdvanceBtn.clicked += HandleAdvanceAction;
            }

            if (speechTextArea != null)
            {
                speechTextArea.UnregisterCallback<ClickEvent>(OnTextAreaClicked);
                speechTextArea.RegisterCallback<ClickEvent>(OnTextAreaClicked);
            }

            if (speechBubbleBg != null)
            {
                speechBubbleBg.UnregisterCallback<ClickEvent>(OnTextAreaClicked);
                speechBubbleBg.RegisterCallback<ClickEvent>(OnTextAreaClicked);
            }
        }

        private void UnsubscribeEvents()
        {
            if (speechAdvanceBtn != null)
            {
                speechAdvanceBtn.clicked -= HandleAdvanceAction;
            }

            if (speechTextArea != null)
            {
                speechTextArea.UnregisterCallback<ClickEvent>(OnTextAreaClicked);
            }

            if (speechBubbleBg != null)
            {
                speechBubbleBg.UnregisterCallback<ClickEvent>(OnTextAreaClicked);
            }
        }

        private void OnTextAreaClicked(ClickEvent evt)
        {
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
            SetVisible(true);

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
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
                typewriterCoroutine = null;
            }
            StopTypewriterSound();
            isTyping = false;
            currentText = dialogueText ?? string.Empty;
            if (speechTextLabel != null) speechTextLabel.text = currentText;
        }

        /// <summary>
        /// Atualiza o interlocutor e o texto de diálogo.
        /// </summary>
        public void SetSpeech(string speakerName, string dialogueText)
        {
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
                typewriterCoroutine = null;
            }
            StopTypewriterSound();
            isTyping = false;
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
                StopTypewriterSound();
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

        private AudioClip cachedSourceClip;
        private AudioClip trimmedTypewriterClip;

        private AudioClip GetTrimmedTypewriterClip(AudioClip sourceClip)
        {
            if (sourceClip == null) return null;
            if (sourceClip == cachedSourceClip && trimmedTypewriterClip != null)
            {
                return trimmedTypewriterClip;
            }

            cachedSourceClip = sourceClip;
            try
            {
                int channels = sourceClip.channels;
                int totalSamples = sourceClip.samples;
                float[] data = new float[totalSamples * channels];
                if (!sourceClip.GetData(data, 0))
                {
                    trimmedTypewriterClip = sourceClip;
                    return sourceClip;
                }

                // Remove o silêncio inicial do arquivo de áudio (ex: 70ms mudos no início do MP3)
                int startFrame = 0;
                for (int frame = 0; frame < totalSamples; frame++)
                {
                    bool audible = false;
                    for (int c = 0; c < channels; c++)
                    {
                        if (Mathf.Abs(data[frame * channels + c]) > 0.005f)
                        {
                            audible = true;
                            break;
                        }
                    }
                    if (audible)
                    {
                        startFrame = Mathf.Max(0, frame - 16);
                        break;
                    }
                }

                if (startFrame <= 0 || startFrame >= totalSamples - 64)
                {
                    trimmedTypewriterClip = sourceClip;
                    return sourceClip;
                }

                int newSampleCount = totalSamples - startFrame;
                float[] trimmedData = new float[newSampleCount * channels];
                Array.Copy(data, startFrame * channels, trimmedData, 0, trimmedData.Length);

                trimmedTypewriterClip = AudioClip.Create(
                    sourceClip.name + "_Trimmed",
                    newSampleCount,
                    channels,
                    sourceClip.frequency,
                    false
                );
                trimmedTypewriterClip.SetData(trimmedData, 0);
                return trimmedTypewriterClip;
            }
            catch
            {
                trimmedTypewriterClip = sourceClip;
                return sourceClip;
            }
        }

        public void PlayTypewriterSound()
        {
            if (typewriterSound == null) return;
            if (Time.unscaledTime - lastTypewriterSoundTime < minTypewriterSoundInterval) return;
            lastTypewriterSoundTime = Time.unscaledTime;

            EnsureAudioSource();
            if (audioSource == null) return;

            AudioClip clipToPlay = GetTrimmedTypewriterClip(typewriterSound);
            if (clipToPlay == null) return;

            if (!audioSource.enabled) audioSource.enabled = true;
            if (audioSource.mute) audioSource.mute = false;

            audioSource.pitch = UnityEngine.Random.Range(typewriterPitchVariation.x, typewriterPitchVariation.y);
            audioSource.volume = typewriterSoundVolume;
            audioSource.clip = clipToPlay;
            audioSource.timeSamples = 0;
            audioSource.Play();
        }

        public void StopTypewriterSound()
        {
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
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

            if (string.IsNullOrEmpty(fullText))
            {
                StopTypewriterSound();
                isTyping = false;
                typewriterCoroutine = null;
                yield break;
            }

            float delay = charactersPerSecond > 0 ? 1f / charactersPerSecond : 0.02f;
            int length = fullText.Length;

            for (int i = 1; i <= length; i++)
            {
                // Pula tags ricas se houver (ex: <b>, <color>)
                if (fullText[i - 1] == '<')
                {
                    int closeIdx = fullText.IndexOf('>', i - 1);
                    if (closeIdx != -1)
                    {
                        i = closeIdx + 1;
                    }
                }

                int displayLength = Mathf.Min(i, length);
                if (speechTextLabel != null)
                {
                    speechTextLabel.text = fullText.Substring(0, displayLength);
                }

                if (displayLength > 0 && !char.IsWhiteSpace(fullText[displayLength - 1]))
                {
                    PlayTypewriterSound();
                }

                if (i >= length) break;

                yield return new WaitForSecondsRealtime(delay);
            }

            if (speechTextLabel != null)
            {
                speechTextLabel.text = fullText;
            }

            StopTypewriterSound();

            isTyping = false;
            typewriterCoroutine = null;
        }

        /// <summary>
        /// Pula a animação do typewriter imediatamente para o texto completo.
        /// </summary>
        public void SkipTypewriter()
        {
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
                typewriterCoroutine = null;
            }

            StopTypewriterSound();

            if (speechTextLabel != null)
            {
                speechTextLabel.text = currentText;
            }
            isTyping = false;
        }

        private int lastActionFrame = -1;
        private float lastSkipTime = -1f;
        private float lastAdvanceTime = -1f;
        [SerializeField] private float skipToAdvanceCooldown = 0.25f;
        [SerializeField] private float minAdvanceCooldown = 0.25f;

        /// <summary>
        /// Ação de avançar diálogo (se digitando, completa o texto; se completo, dispara callback de conclusão).
        /// </summary>
        public void HandleAdvanceAction()
        {
            if (Time.frameCount == lastActionFrame) return;
            lastActionFrame = Time.frameCount;

            if (isTyping)
            {
                SkipTypewriter();
                lastSkipTime = Time.unscaledTime;
                return;
            }

            // Impede que o mesmo clique (ou o MouseUp subsequente do clique que pulou a digitação) avance o diálogo
            if (Time.unscaledTime - lastSkipTime < skipToAdvanceCooldown)
            {
                return;
            }

            // Cooldown de segurança para evitar avanços múltiplos acidentais em caso de múltiplos eventos no mesmo clique
            if (Time.unscaledTime - lastAdvanceTime < minAdvanceCooldown)
            {
                return;
            }
            lastAdvanceTime = Time.unscaledTime;

            var callback = pendingOnCompleteCallback;
            pendingOnCompleteCallback = null;

            callback?.Invoke();
            OnSpeechCompleted?.Invoke();
        }
    }
}
