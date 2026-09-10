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

        public event Action OnRestartRequested;
        public event Action OnMainMenuRequested;

        private VisualElement root;
        private VisualElement endScreenContainer;
        private Label titleLabel;
        private Label reasonLabel;
        private Label statsSummaryLabel;
        private Button btnRestart;
        private Button btnMainMenu;

        private void Awake()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            CacheElements();
            Hide();
        }

        private void CacheElements()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            root = uiDocument.rootVisualElement;

            endScreenContainer = root.Q<VisualElement>("endscreen-container") ?? root.Q<VisualElement>("end-game-panel");
            titleLabel = root.Q<Label>("endscreen-title") ?? root.Q<Label>("title-gameover");
            reasonLabel = root.Q<Label>("endscreen-reason") ?? root.Q<Label>("reason-gameover");
            statsSummaryLabel = root.Q<Label>("endscreen-stats") ?? root.Q<Label>("stats-summary");

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
            CacheElements();

            if (endScreenContainer != null)
            {
                endScreenContainer.style.display = DisplayStyle.Flex;
            }

            if (titleLabel != null)
            {
                titleLabel.text = termination.IsVictory ? "MANDATO CUMPRIDO!" : "FIM DE GOVERNO";
                titleLabel.style.color = termination.IsVictory
                    ? new StyleColor(new Color(0.2f, 0.9f, 0.4f))
                    : new StyleColor(new Color(0.9f, 0.2f, 0.2f));
            }

            if (reasonLabel != null)
            {
                reasonLabel.text = !string.IsNullOrEmpty(termination.reason)
                    ? termination.reason
                    : (termination.IsVictory ? "Você governou o Brasil por 4 anos completos com sucesso!" : "O governo ruiu antes do término do mandato.");
            }

            if (statsSummaryLabel != null && finalSnapshot != null)
            {
                statsSummaryLabel.text = $"Duração: {finalSnapshot.DisplayDate} (Mês {finalSnapshot.MonthIndex})\n" +
                                         $"Posicionamento Final: {finalSnapshot.PoliticalAxis.Quadrant}\n" +
                                         $"Economia: {finalSnapshot.Stats.economy}% | Aprovação: {finalSnapshot.Stats.popularApproval}% | Clima: {finalSnapshot.Stats.climaticChanges}%\n" +
                                         $"Relações: {finalSnapshot.Stats.internationalRelations}% | Corrupção: {finalSnapshot.Stats.corruption}%";
            }
        }

        public void Hide()
        {
            CacheElements();
            if (endScreenContainer != null)
            {
                endScreenContainer.style.display = DisplayStyle.None;
            }
        }

        private void HandleRestart()
        {
            OnRestartRequested?.Invoke();
        }

        private void HandleMainMenu()
        {
            OnMainMenuRequested?.Invoke();
        }

        private void OnDestroy()
        {
            if (btnRestart != null) btnRestart.clicked -= HandleRestart;
            if (btnMainMenu != null) btnMainMenu.clicked -= HandleMainMenu;
        }
    }
}
