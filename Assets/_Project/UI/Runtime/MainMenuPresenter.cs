using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Mandato.UI
{
    public class MainMenuPresenter : MonoBehaviour
    {
        [Header("Configuração de Cena")]
        [SerializeField] private string gameplaySceneName = "JogoV2";

        [Header("Seleção de Personagem")]
        [Tooltip("Evento disparado ao clicar em Jogar. Conecte ao MenuCharacterSelectCoordinator.EnterCharacterSelect() no Inspector." +
                 " Se não houver listener, irá diretamente para a cena de gameplay.")]
        [SerializeField] private UnityEvent onPlayClickedUnity = new UnityEvent();

        [Header("UI Toolkit")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset uxmlAsset;
        [SerializeField] private PanelSettings panelSettings;

        [Header("Áudio / Sons")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip hoverSound;
        [SerializeField] private AudioClip clickSound;
        [Range(0f, 1f)] [SerializeField] private float hoverSoundVolume = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float clickSoundVolume = 1f;

        public event Action OnPlayClicked;
        public event Action OnCreditsOpened;
        public event Action OnCreditsClosed;
        public event Action OnQuitClicked;

        public AudioSource AudioSource => audioSource;
        public AudioClip HoverSound { get => hoverSound; set => hoverSound = value; }
        public AudioClip ClickSound { get => clickSound; set => clickSound = value; }

        private VisualElement root;
        private Button btnPlay;
        private Button btnCredits;
        private Button btnCreditsClose;
        private Button btnQuit;
        private VisualElement creditsModal;

        public bool IsCreditsOpen { get; private set; } = false;

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
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
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

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>() ?? GetComponentInChildren<AudioSource>();
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
                    var uxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_Project/UI/UXML/MainMenu.uxml");
                    if (uxml != null) uiDocument.visualTreeAsset = uxml;
#endif
                }
            }

#if UNITY_EDITOR
            if (hoverSound == null)
            {
                hoverSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audios/menu-select-sound-100466 (mp3cut.net).mp3");
            }
            if (clickSound == null)
            {
                clickSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audios/menu-click.mp3");
            }
#endif
        }

        private void CacheElements()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            root = uiDocument.rootVisualElement;

            btnPlay = root.Q<Button>("btn-play");
            btnCredits = root.Q<Button>("btn-credits");
            btnCreditsClose = root.Q<Button>("btn-credits-close");
            btnQuit = root.Q<Button>("btn-quit");
            creditsModal = root.Q<VisualElement>("credits-modal");
        }

        private void SubscribeEvents()
        {
            if (btnPlay != null)
            {
                btnPlay.clicked -= HandlePlayClicked;
                btnPlay.clicked += HandlePlayClicked;
                SubscribeButtonEvents(btnPlay);
            }

            if (btnCredits != null)
            {
                btnCredits.clicked -= HandleCreditsOpenClicked;
                btnCredits.clicked += HandleCreditsOpenClicked;
                SubscribeButtonEvents(btnCredits);
            }

            if (btnCreditsClose != null)
            {
                btnCreditsClose.clicked -= HandleCreditsCloseClicked;
                btnCreditsClose.clicked += HandleCreditsCloseClicked;
                SubscribeButtonEvents(btnCreditsClose);
            }

            if (btnQuit != null)
            {
                btnQuit.clicked -= HandleQuitClicked;
                btnQuit.clicked += HandleQuitClicked;
                SubscribeButtonEvents(btnQuit);
            }
        }

        private void UnsubscribeEvents()
        {
            if (btnPlay != null)
            {
                btnPlay.clicked -= HandlePlayClicked;
                UnsubscribeButtonEvents(btnPlay);
            }
            if (btnCredits != null)
            {
                btnCredits.clicked -= HandleCreditsOpenClicked;
                UnsubscribeButtonEvents(btnCredits);
            }
            if (btnCreditsClose != null)
            {
                btnCreditsClose.clicked -= HandleCreditsCloseClicked;
                UnsubscribeButtonEvents(btnCreditsClose);
            }
            if (btnQuit != null)
            {
                btnQuit.clicked -= HandleQuitClicked;
                UnsubscribeButtonEvents(btnQuit);
            }
        }

        private void SubscribeButtonEvents(Button btn)
        {
            if (btn == null) return;
            btn.UnregisterCallback<PointerEnterEvent>(HandleButtonHover);
            btn.RegisterCallback<PointerEnterEvent>(HandleButtonHover);
        }

        private void UnsubscribeButtonEvents(Button btn)
        {
            if (btn == null) return;
            btn.UnregisterCallback<PointerEnterEvent>(HandleButtonHover);
        }

        private void HandleButtonHover(PointerEnterEvent evt)
        {
            PlayHoverSound();
        }

        public void PlayHoverSound()
        {
            if (hoverSound == null) return;
            EnsureAudioSource();

            if (audioSource != null)
            {
                audioSource.PlayOneShot(hoverSound, hoverSoundVolume);
            }
        }

        public void PlayClickSound()
        {
            if (clickSound == null) return;
            EnsureAudioSource();

            if (audioSource != null)
            {
                audioSource.PlayOneShot(clickSound, clickSoundVolume);
            }
        }

        private void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>() ?? GetComponentInChildren<AudioSource>();
            }

            if (audioSource == null && gameObject != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }

        public void HandlePlayClicked()
        {
            PlayClickSound();
            OnPlayClicked?.Invoke();
            // Se houver listeners no UnityEvent do Inspector (ex: MenuCharacterSelectCoordinator),
            // eles têm prioridade. Caso contrário, vai direto para a cena de gameplay.
            if (onPlayClickedUnity != null && onPlayClickedUnity.GetPersistentEventCount() > 0)
                onPlayClickedUnity.Invoke();
            else
                StartGame();
        }

        public void StartGame()
        {
            if (!string.IsNullOrEmpty(gameplaySceneName) && Application.isPlaying)
            {
                SceneManager.LoadScene(gameplaySceneName);
            }
        }

        public void HandleCreditsOpenClicked()
        {
            PlayClickSound();
            SetCreditsVisible(true);
            OnCreditsOpened?.Invoke();
        }

        public void HandleCreditsCloseClicked()
        {
            PlayClickSound();
            SetCreditsVisible(false);
            OnCreditsClosed?.Invoke();
        }

        public void SetCreditsVisible(bool visible)
        {
            IsCreditsOpen = visible;
            if (creditsModal != null)
            {
                if (visible)
                {
                    creditsModal.AddToClassList("open");
                    creditsModal.style.display = DisplayStyle.Flex;
                }
                else
                {
                    creditsModal.RemoveFromClassList("open");
                    creditsModal.style.display = DisplayStyle.None;
                }
            }
        }

        public void SetMenuVisible(bool visible)
        {
            if (root != null)
            {
                root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
            else if (uiDocument != null && uiDocument.rootVisualElement != null)
            {
                uiDocument.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }

        public void HandleQuitClicked()
        {
            PlayClickSound();
            OnQuitClicked?.Invoke();
            QuitGame();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
