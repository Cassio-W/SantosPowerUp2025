using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Mandato.UI
{
    public class MainMenuPresenter : MonoBehaviour
    {
        [Header("Configuração de Cena")]
        [SerializeField] private string gameplaySceneName = "JogoV2";

        [Header("UI Toolkit")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset uxmlAsset;
        [SerializeField] private PanelSettings panelSettings;

        public event Action OnPlayClicked;
        public event Action OnCreditsOpened;
        public event Action OnCreditsClosed;
        public event Action OnQuitClicked;

        private VisualElement root;
        private Button btnPlay;
        private Button btnCredits;
        private Button btnCreditsClose;
        private Button btnQuit;
        private VisualElement creditsModal;

        public bool IsCreditsOpen { get; private set; } = false;

        private void Awake()
        {
            DisableLegacyCanvases();
            EnsureDocument();
            CacheElements();
        }

        private void DisableLegacyCanvases()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                if (canvas == null) continue;
                if (canvas.renderMode != RenderMode.WorldSpace)
                {
                    canvas.gameObject.SetActive(false);
                }
            }
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
            }

            if (btnCredits != null)
            {
                btnCredits.clicked -= HandleCreditsOpenClicked;
                btnCredits.clicked += HandleCreditsOpenClicked;
            }

            if (btnCreditsClose != null)
            {
                btnCreditsClose.clicked -= HandleCreditsCloseClicked;
                btnCreditsClose.clicked += HandleCreditsCloseClicked;
            }

            if (btnQuit != null)
            {
                btnQuit.clicked -= HandleQuitClicked;
                btnQuit.clicked += HandleQuitClicked;
            }
        }

        private void UnsubscribeEvents()
        {
            if (btnPlay != null) btnPlay.clicked -= HandlePlayClicked;
            if (btnCredits != null) btnCredits.clicked -= HandleCreditsOpenClicked;
            if (btnCreditsClose != null) btnCreditsClose.clicked -= HandleCreditsCloseClicked;
            if (btnQuit != null) btnQuit.clicked -= HandleQuitClicked;
        }

        public void HandlePlayClicked()
        {
            OnPlayClicked?.Invoke();
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
            SetCreditsVisible(true);
            OnCreditsOpened?.Invoke();
        }

        public void HandleCreditsCloseClicked()
        {
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

        public void HandleQuitClicked()
        {
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
