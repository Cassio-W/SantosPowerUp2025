using System;
using UnityEngine;
using UnityEngine.UIElements;
using Mandato.Content;

namespace Mandato.UI
{
    /// <summary>
    /// Presenter do HUD da Festa Corporativa.
    /// Exibe a roda de interação (4 abordagens), a barra de ânimo segmentada
    /// e o balão de fala do NPC. Não contém lógica de gameplay.
    ///
    /// Mapeamento da roda (sentido horário a partir de cima):
    ///   Cima    = Arrogante
    ///   Direita = Brincalhão
    ///   Baixo   = Persuasivo
    ///   Esquerda= Romântico
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PartyHudPresenter : MonoBehaviour
    {
        // ─── Eventos ─────────────────────────────────────────────────────────────
        public event Action<ApproachStyle> OnApproachSelected;
        public event Action OnPartyEndRequested;
        public event Action OnConversationCancelled;

        // ─── Nomes dos elementos UXML ─────────────────────────────────────────────
        private const string ROOT_ID            = "party-hud-root";
        private const string NPC_BUBBLE_ID      = "npc-speech-bubble";
        private const string NPC_NAME_ID        = "npc-name-label";
        private const string NPC_TEXT_ID        = "npc-speech-text";
        private const string WHEEL_ROOT_ID      = "approach-wheel";
        private const string BTN_UP_ID          = "btn-approach-up";
        private const string BTN_RIGHT_ID       = "btn-approach-right";
        private const string BTN_DOWN_ID        = "btn-approach-down";
        private const string BTN_LEFT_ID        = "btn-approach-left";
        private const string ENERGY_BAR_ID      = "energy-bar";
        private const string END_BTN_ID         = "btn-end-party";
        private const string BTN_BACK_ID        = "btn-back-conversation";
        private const string FEEDBACK_LABEL_ID  = "approach-feedback-label";

        // ─── Referências UI ───────────────────────────────────────────────────────
        private VisualElement root;
        private VisualElement npcBubble;
        private Label npcNameLabel;
        private Label npcSpeechText;
        private VisualElement wheelRoot;
        private Button btnUp;       // Arrogante
        private Button btnRight;    // Brincalhão
        private Button btnDown;     // Persuasivo
        private Button btnLeft;     // Romântico
        private VisualElement energyBar;
        private Button endPartyBtn;
        private Button btnBack;
        private Label feedbackLabel;

        // ─── Estado ───────────────────────────────────────────────────────────────
        private int totalInteractions;
        private int remainingInteractions;

        private void Awake()
        {
            var doc = GetComponent<UIDocument>();
            if (doc == null || doc.rootVisualElement == null) return;

            var uiRoot = doc.rootVisualElement;
            uiRoot.pickingMode = PickingMode.Ignore;
            root         = uiRoot.Q<VisualElement>(ROOT_ID) ?? uiRoot;
            root.pickingMode = PickingMode.Ignore;
            npcBubble    = root.Q<VisualElement>(NPC_BUBBLE_ID);
            npcNameLabel = root.Q<Label>(NPC_NAME_ID);
            npcSpeechText= root.Q<Label>(NPC_TEXT_ID);
            wheelRoot    = root.Q<VisualElement>(WHEEL_ROOT_ID);
            btnUp        = root.Q<Button>(BTN_UP_ID);
            btnRight     = root.Q<Button>(BTN_RIGHT_ID);
            btnDown      = root.Q<Button>(BTN_DOWN_ID);
            btnLeft      = root.Q<Button>(BTN_LEFT_ID);
            energyBar    = root.Q<VisualElement>(ENERGY_BAR_ID);
            endPartyBtn  = root.Q<Button>(END_BTN_ID);
            btnBack      = root.Q<Button>(BTN_BACK_ID);
            feedbackLabel= root.Q<Label>(FEEDBACK_LABEL_ID);

            WireWheelButtons();

            if (endPartyBtn != null)
                endPartyBtn.clicked += () => OnPartyEndRequested?.Invoke();

            if (btnBack != null)
                btnBack.clicked += () => OnConversationCancelled?.Invoke();

            HideWheel();
            HideBubble();
            HideBackButton();
        }

        // ─── API pública ──────────────────────────────────────────────────────────

        /// <summary>Inicializa a barra de ânimo com o total de interações.</summary>
        public void SetupEnergyBar(int total)
        {
            totalInteractions     = total;
            remainingInteractions = total;
            RefreshEnergyBar();
        }

        /// <summary>Decrementa o ânimo e atualiza o visual.</summary>
        public void ConsumeInteraction()
        {
            remainingInteractions = Mathf.Max(0, remainingInteractions - 1);
            RefreshEnergyBar();
        }

        /// <summary>Exibe o balão de fala do NPC com nome e texto.</summary>
        public void ShowNpcSpeech(string npcDisplayName, string speech)
        {
            if (npcNameLabel != null) npcNameLabel.text  = npcDisplayName;
            if (npcSpeechText != null) npcSpeechText.text = speech;
            npcBubble?.RemoveFromClassList("hidden");
        }

        public void HideBubble()
        {
            npcBubble?.AddToClassList("hidden");
        }

        /// <summary>
        /// Exibe a roda de interação, marcando abordagens já usadas como indisponíveis.
        /// </summary>
        public void ShowWheel(bool[] usedApproaches)
        {
            // usedApproaches[i] → (int)ApproachStyle: 0=Arrogante,1=Brincalhao,2=Persuasivo,3=Romantico
            SetButtonAvailable(btnUp,    !usedApproaches[0]); // Arrogante
            SetButtonAvailable(btnRight, !usedApproaches[1]); // Brincalhão
            SetButtonAvailable(btnDown,  !usedApproaches[2]); // Persuasivo
            SetButtonAvailable(btnLeft,  !usedApproaches[3]); // Romântico

            wheelRoot?.RemoveFromClassList("hidden");
        }

        public void HideWheel()
        {
            wheelRoot?.AddToClassList("hidden");
        }

        /// <summary>
        /// Exibe feedback textual temporário após o jogador escolher uma abordagem.
        /// (ex: "+2 Relação!" ou "-1 Relação")
        /// </summary>
        public void ShowFeedback(string text)
        {
            if (feedbackLabel == null) return;
            feedbackLabel.text = text;
            feedbackLabel.RemoveFromClassList("hidden");
        }

        public void HideFeedback()
        {
            feedbackLabel?.AddToClassList("hidden");
        }

        /// <summary>Exibe o botão de voltar durante o diálogo.</summary>
        public void ShowBackButton()
        {
            btnBack?.RemoveFromClassList("hidden");
        }

        /// <summary>Oculta o botão de voltar do diálogo.</summary>
        public void HideBackButton()
        {
            btnBack?.AddToClassList("hidden");
        }

        // ─── Privado ──────────────────────────────────────────────────────────────

        private void WireWheelButtons()
        {
            if (btnUp    != null) btnUp.clicked    += () => { HideWheel(); OnApproachSelected?.Invoke(ApproachStyle.Arrogante); };
            if (btnRight != null) btnRight.clicked += () => { HideWheel(); OnApproachSelected?.Invoke(ApproachStyle.Brincalhao); };
            if (btnDown  != null) btnDown.clicked  += () => { HideWheel(); OnApproachSelected?.Invoke(ApproachStyle.Persuasivo); };
            if (btnLeft  != null) btnLeft.clicked  += () => { HideWheel(); OnApproachSelected?.Invoke(ApproachStyle.Romantico); };
        }

        private void SetButtonAvailable(Button btn, bool available)
        {
            if (btn == null) return;
            btn.SetEnabled(available);
            if (available) btn.RemoveFromClassList("approach-used");
            else           btn.AddToClassList("approach-used");
        }

        private void RefreshEnergyBar()
        {
            if (energyBar == null || totalInteractions <= 0) return;

            energyBar.Clear();
            for (int i = 0; i < totalInteractions; i++)
            {
                var segment = new VisualElement();
                segment.AddToClassList("energy-segment");
                if (i < remainingInteractions)
                    segment.AddToClassList("energy-segment--active");
                else
                    segment.AddToClassList("energy-segment--spent");
                energyBar.Add(segment);
            }
        }
    }
}
