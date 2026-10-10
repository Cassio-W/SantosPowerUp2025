using System;
using UnityEngine;
using UnityEngine.UIElements;
using Mandato.Content;

namespace Mandato.UI
{
    /// <summary>
    /// Presenter do HUD da Festa Corporativa.
    /// Exibe a roda de abordagem cortada em X (4 fatias triangulares renderizadas
    /// via Painter2D / UI Toolkit Mesh API), a barra vertical de ânimo segmentada
    /// e o balão de fala do NPC. Não contém lógica de gameplay.
    ///
    /// Mapeamento da roda em X (sentido horário a partir de cima):
    ///   0 / Cima     = Arrogante  [1 / W]  (225° a 315°)
    ///   1 / Direita  = Brincalhão [2 / D]  (315° a 45°)
    ///   2 / Baixo    = Persuasivo [3 / S]  (45° a 135°)
    ///   3 / Esquerda = Romântico  [4 / A]  (135° a 225°)
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
        private const string CANVAS_ID          = "wheel-slice-canvas";
        private const string SLICE_UP_ID        = "slice-content-up";
        private const string SLICE_RIGHT_ID     = "slice-content-right";
        private const string SLICE_DOWN_ID      = "slice-content-down";
        private const string SLICE_LEFT_ID      = "slice-content-left";
        private const string ENERGY_BAR_ID      = "energy-bar";
        private const string END_BTN_ID         = "btn-end-party";
        private const string BTN_BACK_ID        = "btn-back-conversation";
        private const string FEEDBACK_LABEL_ID  = "approach-feedback-label";
        private const string FEEDBACK_CONT_ID   = "feedback-container";
        private const string ENERGY_COUNT_ID    = "energy-count-label";

        // ─── Referências UI ───────────────────────────────────────────────────────
        private VisualElement root;
        private VisualElement npcBubble;
        private Label npcNameLabel;
        private Label npcSpeechText;
        private VisualElement wheelRoot;
        private VisualElement sliceCanvas;
        private VisualElement sliceUp;
        private VisualElement sliceRight;
        private VisualElement sliceDown;
        private VisualElement sliceLeft;
        private VisualElement energyBar;
        private Label energyCountLabel;
        private Button endPartyBtn;
        private Button btnBack;
        private VisualElement feedbackContainer;
        private Label feedbackLabel;

        // ─── Estado da Roda ───────────────────────────────────────────────────────
        private readonly bool[] usedApproaches = new bool[4];
        private int hoveredSector = -1; // 0=Cima, 1=Direita, 2=Baixo, 3=Esquerda, -1=Nenhum
        private bool isSelectingApproach = false;

        // ─── Estado da Barra de Ânimo ─────────────────────────────────────────────
        private int totalInteractions;
        private int remainingInteractions;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
        }

        private void Start()
        {
            EnsureInitialized();
        }

        private void OnDisable()
        {
            UnwireWheelInteraction();
        }

        public bool EnsureInitialized()
        {
            if (root != null && energyBar != null && sliceCanvas != null && energyCountLabel != null)
                return true;

            var doc = GetComponent<UIDocument>();
            if (doc != null)
            {
                doc.sortingOrder = 500;
            }

            if (doc == null || doc.rootVisualElement == null) return false;

            var uiRoot = doc.rootVisualElement;
            uiRoot.pickingMode = PickingMode.Ignore;
            root              = uiRoot.Q<VisualElement>(ROOT_ID) ?? uiRoot;
            root.pickingMode  = PickingMode.Ignore;

            npcBubble         = root.Q<VisualElement>(NPC_BUBBLE_ID);
            npcNameLabel      = root.Q<Label>(NPC_NAME_ID);
            npcSpeechText     = root.Q<Label>(NPC_TEXT_ID);

            wheelRoot         = root.Q<VisualElement>(WHEEL_ROOT_ID);
            sliceCanvas       = root.Q<VisualElement>(CANVAS_ID);
            sliceUp           = root.Q<VisualElement>(SLICE_UP_ID);
            sliceRight        = root.Q<VisualElement>(SLICE_RIGHT_ID);
            sliceDown         = root.Q<VisualElement>(SLICE_DOWN_ID);
            sliceLeft         = root.Q<VisualElement>(SLICE_LEFT_ID);

            energyBar         = root.Q<VisualElement>(ENERGY_BAR_ID);
            energyCountLabel  = root.Q<Label>(ENERGY_COUNT_ID);
            endPartyBtn       = root.Q<Button>(END_BTN_ID);
            btnBack           = root.Q<Button>(BTN_BACK_ID);
            feedbackContainer = root.Q<VisualElement>(FEEDBACK_CONT_ID);
            feedbackLabel     = root.Q<Label>(FEEDBACK_LABEL_ID);

            WireWheelInteraction();

            if (endPartyBtn != null)
            {
                endPartyBtn.clicked -= OnEndPartyClicked;
                endPartyBtn.clicked += OnEndPartyClicked;
            }

            if (btnBack != null)
            {
                btnBack.clicked -= OnBackClicked;
                btnBack.clicked += OnBackClicked;
            }

            HideWheel();
            HideBubble();
            HideBackButton();
            HideFeedback();

            return true;
        }

        private void OnEndPartyClicked() => OnPartyEndRequested?.Invoke();
        private void OnBackClicked() => OnConversationCancelled?.Invoke();

        // ─── API pública ──────────────────────────────────────────────────────────

        /// <summary>Inicializa a barra de ânimo com o total de interações.</summary>
        public void SetupEnergyBar(int total)
        {
            totalInteractions     = total;
            remainingInteractions = total;
            EnsureInitialized();
            RefreshEnergyBar();
        }

        /// <summary>Decrementa o ânimo e atualiza o visual segmentado.</summary>
        public void ConsumeInteraction()
        {
            EnsureInitialized();
            remainingInteractions = Mathf.Max(0, remainingInteractions - 1);
            RefreshEnergyBar();
        }

        /// <summary>Exibe o balão de fala do NPC com nome e texto.</summary>
        public void ShowNpcSpeech(string npcDisplayName, string speech)
        {
            EnsureInitialized();
            if (npcNameLabel != null) npcNameLabel.text  = npcDisplayName;
            if (npcSpeechText != null) npcSpeechText.text = speech;
            npcBubble?.RemoveFromClassList("hidden");
        }

        public void HideBubble()
        {
            EnsureInitialized();
            npcBubble?.AddToClassList("hidden");
        }

        /// <summary>
        /// Exibe a roda de abordagem triangular em X, marcando abordagens já usadas como desabilitadas.
        /// </summary>
        public void ShowWheel(bool[] used)
        {
            EnsureInitialized();
            isSelectingApproach = false;
            hoveredSector = -1;

            if (used != null && used.Length >= 4)
            {
                for (int i = 0; i < 4; i++)
                {
                    usedApproaches[i] = used[i];
                }
            }

            SetSliceUsed(sliceUp,    usedApproaches[0]);
            SetSliceUsed(sliceRight, usedApproaches[1]);
            SetSliceUsed(sliceDown,  usedApproaches[2]);
            SetSliceUsed(sliceLeft,  usedApproaches[3]);

            UpdateSliceHoverClasses();
            sliceCanvas?.MarkDirtyRepaint();

            wheelRoot?.RemoveFromClassList("hidden");
        }

        public void HideWheel()
        {
            EnsureInitialized();
            hoveredSector = -1;
            UpdateSliceHoverClasses();
            wheelRoot?.AddToClassList("hidden");
        }

        /// <summary>
        /// Exibe feedback textual temporário após o jogador escolher uma postura (+2 Relação, etc.).
        /// </summary>
        public void ShowFeedback(string text)
        {
            if (feedbackLabel != null) feedbackLabel.text = text;
            feedbackContainer?.RemoveFromClassList("hidden");
            feedbackLabel?.RemoveFromClassList("hidden");
        }

        public void HideFeedback()
        {
            feedbackContainer?.AddToClassList("hidden");
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

        // ─── Renderização Vetorial da Roda em X (Painter2D) ──────────────────────

        private void WireWheelInteraction()
        {
            if (sliceCanvas != null)
            {
                sliceCanvas.generateVisualContent -= DrawWheelSlices;
                sliceCanvas.generateVisualContent += DrawWheelSlices;
            }

            if (wheelRoot != null)
            {
                wheelRoot.UnregisterCallback<PointerMoveEvent>(OnWheelPointerMove);
                wheelRoot.RegisterCallback<PointerMoveEvent>(OnWheelPointerMove);

                wheelRoot.UnregisterCallback<PointerLeaveEvent>(OnWheelPointerLeave);
                wheelRoot.RegisterCallback<PointerLeaveEvent>(OnWheelPointerLeave);

                wheelRoot.UnregisterCallback<PointerDownEvent>(OnWheelPointerDown);
                wheelRoot.RegisterCallback<PointerDownEvent>(OnWheelPointerDown);
            }
        }

        private void UnwireWheelInteraction()
        {
            if (sliceCanvas != null)
            {
                sliceCanvas.generateVisualContent -= DrawWheelSlices;
            }

            if (wheelRoot != null)
            {
                wheelRoot.UnregisterCallback<PointerMoveEvent>(OnWheelPointerMove);
                wheelRoot.UnregisterCallback<PointerLeaveEvent>(OnWheelPointerLeave);
                wheelRoot.UnregisterCallback<PointerDownEvent>(OnWheelPointerDown);
            }
        }

        /// <summary>
        /// Desenha as 4 fatias triangulares divididas pelo corte em X, aros e relevos rústicos.
        /// </summary>
        private void DrawWheelSlices(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float width = sliceCanvas.resolvedStyle.width > 0 ? sliceCanvas.resolvedStyle.width : 280f;
            float height = sliceCanvas.resolvedStyle.height > 0 ? sliceCanvas.resolvedStyle.height : 280f;
            Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
            float R = Mathf.Min(center.x, center.y) - 4f;

            // 1. Fundo circular base escuro
            p.BeginPath();
            p.Arc(center, R, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
            p.ClosePath();
            p.fillColor = new Color(0.11f, 0.09f, 0.08f, 0.98f);
            p.Fill();

            // 2. As 4 fatias triangulares em X
            // 0: Cima     (225° a 315°)
            // 1: Direita  (315° a 45°)
            // 2: Baixo    (45° a 135°)
            // 3: Esquerda (135° a 225°)
            float[] startAngles = { 225f, 315f, 45f, 135f };
            float[] endAngles   = { 315f,  45f, 135f, 225f };

            for (int i = 0; i < 4; i++)
            {
                bool isUsed = usedApproaches[i];
                bool isHovered = (hoveredSector == i) && !isUsed;

                Color fillColor;
                Color strokeColor;

                if (isUsed)
                {
                    fillColor   = new Color(0.08f, 0.07f, 0.06f, 0.70f);
                    strokeColor = new Color(0.26f, 0.22f, 0.20f, 0.25f);
                }
                else if (isHovered)
                {
                    // Madeira mogno aquecida iluminada por ouro polido
                    fillColor   = new Color(0.33f, 0.23f, 0.13f, 0.98f);
                    strokeColor = new Color(0.98f, 0.84f, 0.44f, 0.98f);
                }
                else
                {
                    // Madeira nobre escura com sutis nuances
                    fillColor   = new Color(0.17f, 0.13f, 0.11f, 0.94f);
                    strokeColor = new Color(0.77f, 0.63f, 0.35f, 0.35f);
                }

                p.BeginPath();
                p.MoveTo(center);
                p.Arc(center, R, Angle.Degrees(startAngles[i]), Angle.Degrees(endAngles[i]), ArcDirection.Clockwise);
                p.ClosePath();
                p.fillColor = fillColor;
                p.Fill();

                p.strokeColor = strokeColor;
                p.lineWidth = isHovered ? 2.5f : 1.2f;
                p.Stroke();
            }

            // 3. Linhas Diagonais do "X" (Divisórias a 45° e 135° cruzando no centro)
            float rad45 = 45f * Mathf.Deg2Rad;
            float rad135 = 135f * Mathf.Deg2Rad;
            Vector2 p45  = center + new Vector2(Mathf.Cos(rad45), Mathf.Sin(rad45)) * R;
            Vector2 p225 = center - new Vector2(Mathf.Cos(rad45), Mathf.Sin(rad45)) * R;
            Vector2 p135 = center + new Vector2(Mathf.Cos(rad135), Mathf.Sin(rad135)) * R;
            Vector2 p315 = center - new Vector2(Mathf.Cos(rad135), Mathf.Sin(rad135)) * R;

            p.strokeColor = new Color(0.83f, 0.69f, 0.34f, 0.55f);
            p.lineWidth = 1.6f;

            p.BeginPath();
            p.MoveTo(p225);
            p.LineTo(p45);
            p.Stroke();

            p.BeginPath();
            p.MoveTo(p315);
            p.LineTo(p135);
            p.Stroke();

            // 4. Aro externo e aro decorativo interno em latão envelhecido
            p.BeginPath();
            p.Arc(center, R, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
            p.strokeColor = new Color(0.83f, 0.69f, 0.34f, 0.85f);
            p.lineWidth = 2.5f;
            p.Stroke();

            p.BeginPath();
            p.Arc(center, 38f, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
            p.strokeColor = new Color(0.83f, 0.69f, 0.34f, 0.65f);
            p.lineWidth = 1.5f;
            p.Stroke();
        }

        // ─── Detecção Polar de Interação por Setor Triangular ────────────────────

        private int GetSectorFromLocalPos(Vector2 localPos)
        {
            if (wheelRoot == null) return -1;
            float width = wheelRoot.resolvedStyle.width > 0 ? wheelRoot.resolvedStyle.width : 280f;
            float height = wheelRoot.resolvedStyle.height > 0 ? wheelRoot.resolvedStyle.height : 280f;
            Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
            Vector2 offset = localPos - center;
            float dist = offset.magnitude;
            float maxR = (width * 0.5f) - 2f;
            float minR = 36f; // Limite do selo central

            if (dist < minR || dist > maxR)
                return -1;

            float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;

            // Mapeamento Polar em X:
            // Direita (Brincalhão = 1): [-45°, +45°)
            // Baixo (Persuasivo = 2):   [+45°, +135°)
            // Cima (Arrogante = 0):     [-135°, -45°)
            // Esquerda (Romântico = 3): [+135°, +180°] ou [-180°, -135°)
            if (angle >= -45f && angle < 45f) return 1;
            if (angle >= 45f && angle < 135f) return 2;
            if (angle >= -135f && angle < -45f) return 0;
            return 3;
        }

        private void OnWheelPointerMove(PointerMoveEvent evt)
        {
            int sector = GetSectorFromLocalPos(evt.localPosition);
            if (sector != hoveredSector)
            {
                hoveredSector = sector;
                UpdateSliceHoverClasses();
                sliceCanvas?.MarkDirtyRepaint();
            }
        }

        private void OnWheelPointerLeave(PointerLeaveEvent evt)
        {
            if (hoveredSector != -1)
            {
                hoveredSector = -1;
                UpdateSliceHoverClasses();
                sliceCanvas?.MarkDirtyRepaint();
            }
        }

        private void OnWheelPointerDown(PointerDownEvent evt)
        {
            int sector = GetSectorFromLocalPos(evt.localPosition);
            if (sector >= 0 && sector < 4 && !usedApproaches[sector])
            {
                SelectApproach((ApproachStyle)sector);
                evt.StopPropagation();
            }
        }

        private void SelectApproach(ApproachStyle style)
        {
            if (isSelectingApproach) return;
            isSelectingApproach = true;
            HideWheel();
            OnApproachSelected?.Invoke(style);
        }

        private void UpdateSliceHoverClasses()
        {
            SetSliceHovered(sliceUp,    hoveredSector == 0 && !usedApproaches[0]);
            SetSliceHovered(sliceRight, hoveredSector == 1 && !usedApproaches[1]);
            SetSliceHovered(sliceDown,  hoveredSector == 2 && !usedApproaches[2]);
            SetSliceHovered(sliceLeft,  hoveredSector == 3 && !usedApproaches[3]);
        }

        private void SetSliceHovered(VisualElement el, bool hovered)
        {
            if (el == null) return;
            if (hovered) el.AddToClassList("slice-hovered");
            else         el.RemoveFromClassList("slice-hovered");
        }

        private void SetSliceUsed(VisualElement el, bool isUsed)
        {
            if (el == null) return;
            if (isUsed) el.AddToClassList("approach-used");
            else        el.RemoveFromClassList("approach-used");
        }

        private void RefreshEnergyBar()
        {
            if (energyCountLabel != null)
            {
                energyCountLabel.text = $"{remainingInteractions}/{totalInteractions}";
            }

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
