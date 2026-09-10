using System.Collections;
using Mandato.Core;
using Mandato.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mandato.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class RetroMonitorPresenter : MonoBehaviour
    {
        [Header("Configurações do Monitor")]
        [SerializeField] private Material crtMaterial;
        [SerializeField] private float animationSpeed = 5f;
        [SerializeField] private bool triggerGlitchOnChanges = true;

        private UIDocument uiDocument;
        private VisualElement root;

        // Fills e Labels dos 4 Indicadores
        private VisualElement fillNature, fillEconomy, fillRelations, fillPeople;
        private Label valueNature, valueEconomy, valueRelations, valuePeople;
        private Label arrowNature, arrowEconomy, arrowRelations, arrowPeople;
        private VisualElement deadOverlayNature, deadOverlayEconomy, deadOverlayRelations, deadOverlayPeople;

        // Corrupção
        private VisualElement fillCorruption;
        private Label valueCorruption;
        private Label arrowCorruption;

        // Header & Data
        private Label dateLabel;

        // Valores interpolados
        private float curNature = 50f, targetNature = 50f;
        private float curEconomy = 50f, targetEconomy = 50f;
        private float curRelations = 50f, targetRelations = 50f;
        private float curPeople = 50f, targetPeople = 50f;
        private float curCorruption = 0f, targetCorruption = 0f;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
            CacheElements();
        }

        private void OnEnable()
        {
            CacheElements();
        }

        private void CacheElements()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            root = uiDocument.rootVisualElement;

            // Clima / Natureza
            fillNature = root.Q<VisualElement>("fill-nature") ?? root.Q<VisualElement>("app-nature-fill");
            valueNature = root.Q<Label>("value-nature");
            arrowNature = root.Q<Label>("arrow-nature");
            deadOverlayNature = root.Q<VisualElement>("dead-overlay-nature");

            // Economia
            fillEconomy = root.Q<VisualElement>("fill-economy") ?? root.Q<VisualElement>("app-economy-fill");
            valueEconomy = root.Q<Label>("value-economy");
            arrowEconomy = root.Q<Label>("arrow-economy");
            deadOverlayEconomy = root.Q<VisualElement>("dead-overlay-economy");

            // Relações Internacionais
            fillRelations = root.Q<VisualElement>("fill-relations") ?? root.Q<VisualElement>("app-relations-fill");
            valueRelations = root.Q<Label>("value-relations");
            arrowRelations = root.Q<Label>("arrow-relations");
            deadOverlayRelations = root.Q<VisualElement>("dead-overlay-relations");

            // População
            fillPeople = root.Q<VisualElement>("fill-people") ?? root.Q<VisualElement>("app-people-fill");
            valuePeople = root.Q<Label>("value-people");
            arrowPeople = root.Q<Label>("arrow-people");
            deadOverlayPeople = root.Q<VisualElement>("dead-overlay-people");

            // Corrupção
            fillCorruption = root.Q<VisualElement>("fill-corruption") ?? root.Q<VisualElement>("module-corruption-fill");
            valueCorruption = root.Q<Label>("value-corruption");
            arrowCorruption = root.Q<Label>("arrow-corruption");

            // Data
            dateLabel = root.Q<Label>("date-display") ?? root.Q<Label>("monitor-date");
        }

        public void UpdateSnapshot(RunSnapshot snapshot, ResolutionReport lastReport = null)
        {
            if (snapshot == null) return;

            CacheElements();

            targetNature = snapshot.Stats.climaticChanges;
            targetEconomy = snapshot.Stats.economy;
            targetRelations = snapshot.Stats.internationalRelations;
            targetPeople = snapshot.Stats.popularApproval;
            targetCorruption = snapshot.Stats.corruption;

            if (dateLabel != null)
            {
                dateLabel.text = snapshot.DisplayDate;
            }

            if (lastReport != null)
            {
                ShowVariationArrows(lastReport);
                if (triggerGlitchOnChanges) TriggerGlitch();
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime * animationSpeed;

            curNature = Mathf.MoveTowards(curNature, targetNature, dt * 25f);
            curEconomy = Mathf.MoveTowards(curEconomy, targetEconomy, dt * 25f);
            curRelations = Mathf.MoveTowards(curRelations, targetRelations, dt * 25f);
            curPeople = Mathf.MoveTowards(curPeople, targetPeople, dt * 25f);
            curCorruption = Mathf.MoveTowards(curCorruption, targetCorruption, dt * 25f);

            ApplyBar(fillNature, valueNature, deadOverlayNature, curNature);
            ApplyBar(fillEconomy, valueEconomy, deadOverlayEconomy, curEconomy);
            ApplyBar(fillRelations, valueRelations, deadOverlayRelations, curRelations);
            ApplyBar(fillPeople, valuePeople, deadOverlayPeople, curPeople);
            ApplyBar(fillCorruption, valueCorruption, null, curCorruption);
        }

        private void ApplyBar(VisualElement fill, Label label, VisualElement deadOverlay, float val)
        {
            if (fill != null)
            {
                fill.style.height = Length.Percent(Mathf.Clamp(val, 0f, 100f));
            }

            if (label != null)
            {
                label.text = $"{Mathf.RoundToInt(val)}%";
            }

            if (deadOverlay != null)
            {
                deadOverlay.style.display = val <= 0.5f ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void ShowVariationArrows(ResolutionReport report)
        {
            if (report == null || report.impactsApplied == null) return;

            SetArrow(arrowNature, report.impactsApplied.climaticChanges);
            SetArrow(arrowEconomy, report.impactsApplied.economy);
            SetArrow(arrowRelations, report.impactsApplied.internationalRelations);
            SetArrow(arrowPeople, report.impactsApplied.popularApproval);
            SetArrow(arrowCorruption, report.impactsApplied.corruption, isCorruption: true);
        }

        private void SetArrow(Label arrow, int delta, bool isCorruption = false)
        {
            if (arrow == null) return;

            if (delta == 0)
            {
                arrow.text = string.Empty;
                return;
            }

            bool isGood = isCorruption ? delta < 0 : delta > 0;
            arrow.text = delta > 0 ? "▲" : "▼";
            arrow.style.color = isGood ? new StyleColor(new Color(0.2f, 0.9f, 0.3f)) : new StyleColor(new Color(0.9f, 0.2f, 0.2f));

            StartCoroutine(ClearArrowAfterDelay(arrow, 3f));
        }

        private IEnumerator ClearArrowAfterDelay(Label arrow, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (arrow != null) arrow.text = string.Empty;
        }

        public void TriggerGlitch()
        {
            if (crtMaterial != null && crtMaterial.HasProperty("_GlitchStrength"))
            {
                StartCoroutine(GlitchPulseRoutine());
            }
        }

        private IEnumerator GlitchPulseRoutine()
        {
            crtMaterial.SetFloat("_GlitchStrength", 0.4f);
            yield return new WaitForSeconds(0.15f);
            crtMaterial.SetFloat("_GlitchStrength", 0.0f);
        }
    }
}
