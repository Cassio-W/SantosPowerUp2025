using System;
using Mandato.Core;
using Mandato.Run;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Mandato.UI
{
    public class EndScreenPresenter : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset endScreenUxmlAsset;
        [SerializeField] private PanelSettings panelSettings;

        public event Action OnRestartRequested;
        public event Action OnMainMenuRequested;

        private VisualElement root;
        private VisualElement endScreenOverlay;
        private VisualElement endScreenContainer;
        private Label titleLabel;
        private Label stampLabel;
        private Label reasonLabel;
        private Label dateLabel;
        private Label durationLabel;

        // Barras e Valores de Estatísticas
        private VisualElement barEconomy;
        private Label valEconomy;
        private VisualElement barApproval;
        private Label valApproval;
        private VisualElement barClimate;
        private Label valClimate;
        private VisualElement barRelations;
        private Label valRelations;
        private VisualElement barCorruption;
        private Label valCorruption;
        private Label valPoliticalQuadrant;

        private Button btnRestart;
        private Button btnMainMenu;

        private void Awake()
        {
            EnsureDocumentSetup();
            CacheElements();
            Hide();
        }

        private void OnEnable()
        {
            EnsureDocumentSetup();
            CacheElements();
        }

        public void EnsureDocumentSetup()
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
                    if (uiDocument.panelSettings == null)
                    {
                        var existingDocs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
                        foreach (var d in existingDocs)
                        {
                            if (d != null && d != uiDocument && d.panelSettings != null && d.panelSettings.targetTexture == null)
                            {
                                uiDocument.panelSettings = d.panelSettings;
                                break;
                            }
                        }
                    }
                }
            }

            if (uiDocument.visualTreeAsset == null)
            {
                if (endScreenUxmlAsset != null)
                {
                    uiDocument.visualTreeAsset = endScreenUxmlAsset;
                }
                else
                {
#if UNITY_EDITOR
                    var uxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/EndScreen/EndScreenUI.uxml");
                    if (uxml != null) uiDocument.visualTreeAsset = uxml;
#endif
                }
            }

            uiDocument.sortingOrder = 100;

            if (uiDocument.rootVisualElement != null && uiDocument.visualTreeAsset != null && uiDocument.rootVisualElement.childCount == 0)
            {
                uiDocument.visualTreeAsset.CloneTree(uiDocument.rootVisualElement);
            }
        }

        private void CacheElements()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            root = uiDocument.rootVisualElement;

            if (root.childCount == 0 && uiDocument.visualTreeAsset != null)
            {
                uiDocument.visualTreeAsset.CloneTree(root);
            }

            endScreenOverlay = root.Q<VisualElement>("endscreen-overlay") ?? root;
            endScreenContainer = root.Q<VisualElement>("endscreen-container");
            titleLabel = root.Q<Label>("endscreen-title") ?? root.Q<Label>("title-gameover");
            stampLabel = root.Q<Label>("endscreen-stamp");
            reasonLabel = root.Q<Label>("endscreen-reason") ?? root.Q<Label>("reason-gameover");
            dateLabel = root.Q<Label>("endscreen-date");
            durationLabel = root.Q<Label>("endscreen-mandate-duration");

            barEconomy = root.Q<VisualElement>("bar-economy");
            valEconomy = root.Q<Label>("val-economy");
            barApproval = root.Q<VisualElement>("bar-approval");
            valApproval = root.Q<Label>("val-approval");
            barClimate = root.Q<VisualElement>("bar-climate");
            valClimate = root.Q<Label>("val-climate");
            barRelations = root.Q<VisualElement>("bar-relations");
            valRelations = root.Q<Label>("val-relations");
            barCorruption = root.Q<VisualElement>("bar-corruption");
            valCorruption = root.Q<Label>("val-corruption");
            valPoliticalQuadrant = root.Q<Label>("val-political-quadrant");

            btnRestart = root.Q<Button>("btn-restart") ?? root.Q<Button>("btn-jogar-novamente");
            btnMainMenu = root.Q<Button>("btn-main-menu") ?? root.Q<Button>("btn-menu");

            if (btnRestart != null)
            {
                btnRestart.clicked -= HandleRestart;
                btnRestart.clicked += HandleRestart;
            }

            if (btnMainMenu != null)
            {
                btnMainMenu.clicked -= HandleMainMenu;
                btnMainMenu.clicked += HandleMainMenu;
            }
        }

        public void ShowEndScreen(RunTermination termination, RunSnapshot finalSnapshot)
        {
            EnsureDocumentSetup();
            CacheElements();

            if (endScreenOverlay != null)
            {
                endScreenOverlay.RemoveFromClassList("hidden");
                endScreenOverlay.style.display = DisplayStyle.Flex;
                endScreenOverlay.style.opacity = 1f;
                endScreenOverlay.BringToFront();
            }

            bool isVictory = termination.IsVictory;

            // 1. Título & Carimbo
            if (titleLabel != null)
            {
                titleLabel.text = isVictory ? "MANDATO CUMPRIDO!" : "FIM DE GOVERNO";
                titleLabel.RemoveFromClassList("headline-victory");
                titleLabel.RemoveFromClassList("headline-defeat");
                titleLabel.AddToClassList(isVictory ? "headline-victory" : "headline-defeat");
            }

            if (stampLabel != null)
            {
                string stampText = isVictory ? "REELEITO" : DetermineDefeatStamp(termination.reason);
                stampLabel.text = stampText;
                stampLabel.RemoveFromClassList("stamp-victory");
                stampLabel.RemoveFromClassList("stamp-defeat");
                stampLabel.AddToClassList(isVictory ? "stamp-victory" : "stamp-defeat");
            }

            // 2. Causa / Motivo
            if (reasonLabel != null)
            {
                reasonLabel.text = !string.IsNullOrEmpty(termination.reason)
                    ? termination.reason
                    : (isVictory ? "Você governou o Brasil por 4 anos completos e conduziu a nação através de grandes reformas." : "O governo ruiu diante de fortes crises institucionais.");
            }

            // 3. Metadados e Datas
            if (dateLabel != null && finalSnapshot != null)
            {
                dateLabel.text = $"BRASÍLIA — {finalSnapshot.DisplayDate}";
            }

            if (durationLabel != null && finalSnapshot != null)
            {
                durationLabel.text = $"MÊS {Mathf.Clamp(finalSnapshot.MonthIndex, 1, 48)} / 48";
            }

            // 4. Barras de Atributos Finais
            if (finalSnapshot != null && finalSnapshot.Stats != null)
            {
                SetStatBar(barEconomy, valEconomy, finalSnapshot.Stats.economy);
                SetStatBar(barApproval, valApproval, finalSnapshot.Stats.popularApproval);
                SetStatBar(barClimate, valClimate, finalSnapshot.Stats.climaticChanges);
                SetStatBar(barRelations, valRelations, finalSnapshot.Stats.internationalRelations);
                SetStatBar(barCorruption, valCorruption, finalSnapshot.Stats.corruption);

                if (valPoliticalQuadrant != null)
                {
                    valPoliticalQuadrant.text = !string.IsNullOrEmpty(finalSnapshot.PoliticalAxis.Quadrant)
                        ? finalSnapshot.PoliticalAxis.Quadrant
                        : "Centro Moderado";
                }
            }
        }

        private void SetStatBar(VisualElement bar, Label label, int value)
        {
            int clamped = Mathf.Clamp(value, 0, 100);
            if (bar != null) bar.style.width = new Length(clamped, LengthUnit.Percent);
            if (label != null) label.text = $"{clamped}%";
        }

        private string DetermineDefeatStamp(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return "CASSADO";
            string lower = reason.ToLowerInvariant();
            if (lower.Contains("impeachment") || lower.Contains("revolta")) return "IMPEACHMENT";
            if (lower.Contains("falência") || lower.Contains("econôm")) return "COLAPSO";
            if (lower.Contains("corrupção") || lower.Contains("escândalo")) return "ESCÂNDALO";
            if (lower.Contains("isolamento") || lower.Contains("sanções")) return "ISOLADO";
            if (lower.Contains("clima") || lower.Contains("ambiental")) return "DESASTRE";
            return "RENÚNCIA";
        }

        public void Hide()
        {
            CacheElements();
            if (endScreenOverlay != null)
            {
                endScreenOverlay.AddToClassList("hidden");
                endScreenOverlay.style.display = DisplayStyle.None;
                endScreenOverlay.style.opacity = 0f;
            }
        }

        private void HandleRestart()
        {
            if (OnRestartRequested != null)
            {
                OnRestartRequested.Invoke();
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        private void HandleMainMenu()
        {
            if (OnMainMenuRequested != null)
            {
                OnMainMenuRequested.Invoke();
            }
            else
            {
                SceneManager.LoadScene(0);
            }
        }

        private void OnDestroy()
        {
            if (btnRestart != null) btnRestart.clicked -= HandleRestart;
            if (btnMainMenu != null) btnMainMenu.clicked -= HandleMainMenu;
        }
    }
}
