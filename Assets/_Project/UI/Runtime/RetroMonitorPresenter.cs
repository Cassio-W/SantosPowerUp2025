using System;
using System.Collections;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mandato.UI
{
    /// <summary>
    /// Presenter do Monitor Retrô CRT 3D em UI Toolkit puro (renderizado diegeticamente no RenderTexture).
    /// Controla as barras dos 4 indicadores (Clima, Economia, Relações, População), módulo de Corrupção,
    /// animações de ghost fills, setas de variação, tremores e efeito de glitch no CRT.
    /// </summary>
    public class RetroMonitorPresenter : MonoBehaviour
    {
        [Header("Configurações do Monitor")]
        [SerializeField] private Material crtMaterial;
        [SerializeField] private float animationSpeed = 4f;
        [SerializeField] private float arrowDuration = 4.5f;
        [SerializeField] private bool triggerGlitchOnChanges = true;

        [Header("UI Document e Assets")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset uxmlAsset;
        [SerializeField] private PanelSettings panelSettings;

        private VisualElement root;

        // Fills e Labels dos 4 Indicadores
        private VisualElement fillNature, fillEconomy, fillRelations, fillPeople;
        private VisualElement ghostNature, ghostEconomy, ghostRelations, ghostPeople;
        private Label valueNature, valueEconomy, valueRelations, valuePeople;
        private Label arrowNature, arrowEconomy, arrowRelations, arrowPeople;
        private VisualElement deadOverlayNature, deadOverlayEconomy, deadOverlayRelations, deadOverlayPeople;
        private VisualElement appNature, appEconomy, appRelations, appPeople;

        // Corrupção
        private VisualElement fillCorruption;
        private VisualElement ghostCorruption;
        private Label valueCorruption;
        private Label arrowCorruption;

        // Header, Data & Logs
        private Label dateLabel;
        private Label dynamicLogEntry;

        // Valores interpolados
        private float curNature = 50f, targetNature = 50f;
        private float curEconomy = 50f, targetEconomy = 50f;
        private float curRelations = 50f, targetRelations = 50f;
        private float curPeople = 50f, targetPeople = 50f;
        private float curCorruption = 0f, targetCorruption = 0f;

        private bool isInitialSetupDone = false;

        private void Awake()
        {
            EnsureReferences();
            CacheElements();
            HideAllArrows();
        }

        private void OnEnable()
        {
            EnsureReferences();
            CacheElements();
        }

        public void EnsureReferences()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>() ?? GetComponentInChildren<UIDocument>();

            if (uiDocument != null)
            {
                if (uiDocument.panelSettings == null)
                {
                    if (panelSettings != null)
                    {
                        uiDocument.panelSettings = panelSettings;
                    }
                    else
                    {
#if UNITY_EDITOR
                        var pSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/PC/UI-TESTE.asset");
                        if (pSettings != null) uiDocument.panelSettings = pSettings;
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
                        var uxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/PC/RetroMonitor.uxml");
                        if (uxml != null) uiDocument.visualTreeAsset = uxml;
#endif
                    }
                }
            }
        }

        private void CacheElements()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            root = uiDocument.rootVisualElement;

            // Clima / Natureza
            fillNature = root.Q<VisualElement>("fill-nature") ?? root.Q<VisualElement>("app-nature-fill");
            ghostNature = root.Q<VisualElement>("ghost-fill-nature");
            valueNature = root.Q<Label>("value-nature");
            arrowNature = root.Q<Label>("arrow-nature");
            deadOverlayNature = root.Q<VisualElement>("dead-overlay-nature");
            appNature = root.Q<VisualElement>("app-nature");

            // Economia
            fillEconomy = root.Q<VisualElement>("fill-economy") ?? root.Q<VisualElement>("app-economy-fill");
            ghostEconomy = root.Q<VisualElement>("ghost-fill-economy");
            valueEconomy = root.Q<Label>("value-economy");
            arrowEconomy = root.Q<Label>("arrow-economy");
            deadOverlayEconomy = root.Q<VisualElement>("dead-overlay-economy");
            appEconomy = root.Q<VisualElement>("app-economy");

            // Relações Internacionais
            fillRelations = root.Q<VisualElement>("fill-relations") ?? root.Q<VisualElement>("app-relations-fill");
            ghostRelations = root.Q<VisualElement>("ghost-fill-relations");
            valueRelations = root.Q<Label>("value-relations");
            arrowRelations = root.Q<Label>("arrow-relations");
            deadOverlayRelations = root.Q<VisualElement>("dead-overlay-relations");
            appRelations = root.Q<VisualElement>("app-relations");

            // População
            fillPeople = root.Q<VisualElement>("fill-people") ?? root.Q<VisualElement>("app-people-fill");
            ghostPeople = root.Q<VisualElement>("ghost-fill-people");
            valuePeople = root.Q<Label>("value-people");
            arrowPeople = root.Q<Label>("arrow-people");
            deadOverlayPeople = root.Q<VisualElement>("dead-overlay-people");
            appPeople = root.Q<VisualElement>("app-people");

            // Corrupção
            fillCorruption = root.Q<VisualElement>("fill-corruption") ?? root.Q<VisualElement>("module-corruption-fill");
            ghostCorruption = root.Q<VisualElement>("ghost-fill-corruption");
            valueCorruption = root.Q<Label>("value-corruption");
            arrowCorruption = root.Q<Label>("arrow-corruption");

            // Header & Data
            dateLabel = root.Q<Label>("date-display") ?? root.Q<Label>("monitor-date") ?? root.Q<Label>("date-label");
            dynamicLogEntry = root.Q<Label>("dynamic-log-entry");
        }

        private Coroutine fadeRoutineNature;
        private Coroutine fadeRoutineEconomy;
        private Coroutine fadeRoutineRelations;
        private Coroutine fadeRoutinePeople;
        private Coroutine fadeRoutineCorruption;

        public void UpdateSnapshot(RunSnapshot snapshot, ResolutionReport lastReport = null)
        {
            if (snapshot == null) return;

            EnsureReferences();
            CacheElements();

            float prevNature = targetNature;
            float prevEconomy = targetEconomy;
            float prevRelations = targetRelations;
            float prevPeople = targetPeople;
            float prevCorruption = targetCorruption;

            targetNature = snapshot.Stats.climaticChanges;
            targetEconomy = snapshot.Stats.economy;
            targetRelations = snapshot.Stats.internationalRelations;
            targetPeople = snapshot.Stats.popularApproval;
            targetCorruption = snapshot.Stats.corruption;

            UpdateDateDisplay(snapshot.DisplayDate);

            if (!isInitialSetupDone)
            {
                // Primeira inicialização: valores imediatos
                curNature = targetNature;
                curEconomy = targetEconomy;
                curRelations = targetRelations;
                curPeople = targetPeople;
                curCorruption = targetCorruption;
                ApplyVisualValues();
                isInitialSetupDone = true;
                return;
            }

            // Variação de Atributos com Feedback Visual
            float dNature = targetNature - prevNature;
            float dEconomy = targetEconomy - prevEconomy;
            float dRelations = targetRelations - prevRelations;
            float dPeople = targetPeople - prevPeople;
            float dCorruption = targetCorruption - prevCorruption;

            if (lastReport != null && lastReport.impactsApplied != null)
            {
                dNature = lastReport.impactsApplied.climaticChanges;
                dEconomy = lastReport.impactsApplied.economy;
                dRelations = lastReport.impactsApplied.internationalRelations;
                dPeople = lastReport.impactsApplied.popularApproval;
                dCorruption = lastReport.impactsApplied.corruption;
            }

            // Exibe setas ▲ / ▼ apenas se houver variação real
            if (Mathf.Abs(dNature) >= 0.1f) ShowArrow(arrowNature, dNature, isCorruption: false, isPreview: false, ref fadeRoutineNature);
            if (Mathf.Abs(dEconomy) >= 0.1f) ShowArrow(arrowEconomy, dEconomy, isCorruption: false, isPreview: false, ref fadeRoutineEconomy);
            if (Mathf.Abs(dRelations) >= 0.1f) ShowArrow(arrowRelations, dRelations, isCorruption: false, isPreview: false, ref fadeRoutineRelations);
            if (Mathf.Abs(dPeople) >= 0.1f) ShowArrow(arrowPeople, dPeople, isCorruption: false, isPreview: false, ref fadeRoutinePeople);
            if (Mathf.Abs(dCorruption) >= 0.1f) ShowArrow(arrowCorruption, dCorruption, isCorruption: true, isPreview: false, ref fadeRoutineCorruption);

            // Ghost fills
            if (Mathf.Abs(dNature) >= 0.1f) TriggerGhost(ghostNature, prevNature, targetNature);
            if (Mathf.Abs(dEconomy) >= 0.1f) TriggerGhost(ghostEconomy, prevEconomy, targetEconomy);
            if (Mathf.Abs(dRelations) >= 0.1f) TriggerGhost(ghostRelations, prevRelations, targetRelations);
            if (Mathf.Abs(dPeople) >= 0.1f) TriggerGhost(ghostPeople, prevPeople, targetPeople);
            if (Mathf.Abs(dCorruption) >= 0.1f) TriggerGhost(ghostCorruption, prevCorruption, targetCorruption);

            bool hasChange = Mathf.Abs(dNature) > 0.1f || Mathf.Abs(dEconomy) > 0.1f ||
                             Mathf.Abs(dRelations) > 0.1f || Mathf.Abs(dPeople) > 0.1f ||
                             Mathf.Abs(dCorruption) > 0.1f;

            if (hasChange && triggerGlitchOnChanges)
            {
                TriggerGlitch();
            }
        }


        public void ShowPreviewImpacts(StatBlock impacts)
        {
            if (impacts == null)
            {
                ClearPreviewImpacts();
                return;
            }

            EnsureReferences();
            CacheElements();

            ShowArrow(arrowNature, impacts.climaticChanges, isCorruption: false, isPreview: true, ref fadeRoutineNature);
            ShowArrow(arrowEconomy, impacts.economy, isCorruption: false, isPreview: true, ref fadeRoutineEconomy);
            ShowArrow(arrowRelations, impacts.internationalRelations, isCorruption: false, isPreview: true, ref fadeRoutineRelations);
            ShowArrow(arrowPeople, impacts.popularApproval, isCorruption: false, isPreview: true, ref fadeRoutinePeople);
            ShowArrow(arrowCorruption, impacts.corruption, isCorruption: true, isPreview: true, ref fadeRoutineCorruption);
        }

        public void ClearPreviewImpacts()
        {
            EnsureReferences();
            CacheElements();

            HideArrowImmediate(arrowNature, ref fadeRoutineNature);
            HideArrowImmediate(arrowEconomy, ref fadeRoutineEconomy);
            HideArrowImmediate(arrowRelations, ref fadeRoutineRelations);
            HideArrowImmediate(arrowPeople, ref fadeRoutinePeople);
            HideArrowImmediate(arrowCorruption, ref fadeRoutineCorruption);
        }

        private void TriggerGhost(VisualElement ghostEl, float fromVal, float toVal)
        {
            if (ghostEl == null) return;
            float height = Mathf.Max(fromVal, toVal);
            ghostEl.style.height = Length.Percent(Mathf.Clamp(height, 0f, 100f));
            ghostEl.style.opacity = 0.8f;
            ghostEl.style.display = DisplayStyle.Flex;

            if (gameObject.activeInHierarchy && isActiveAndEnabled)
            {
                StartCoroutine(FadeOutGhostRoutine(ghostEl));
            }
        }

        private IEnumerator FadeOutGhostRoutine(VisualElement ghostEl)
        {
            yield return new WaitForSeconds(1.0f);
            if (ghostEl == null) yield break;

            float opacity = 0.8f;
            while (opacity > 0.05f)
            {
                opacity -= Time.deltaTime * 2.0f;
                ghostEl.style.opacity = opacity;
                yield return null;
            }
            ghostEl.style.display = DisplayStyle.None;
        }

        public void UpdateDateDisplay(string displayDate)
        {
            if (dateLabel != null && !string.IsNullOrEmpty(displayDate))
            {
                dateLabel.text = $"MANDATO: {displayDate}";
            }
        }

        public void NotifyNewProposal(CardDefinition card)
        {
            if (card == null) return;

            string title = !string.IsNullOrEmpty(card.categoryTag) ? card.categoryTag : card.title;
            if (dynamicLogEntry != null)
            {
                dynamicLogEntry.text = $"> [DESPACHO] Nova proposta: \"{title}\"";
            }

            if (triggerGlitchOnChanges)
            {
                TriggerGlitch(0.3f);
            }
        }

        public void NotifyTermination(RunTermination termination)
        {
            if (termination.IsOngoing) return;

            string reason = !string.IsNullOrEmpty(termination.reason) ? termination.reason : (termination.IsVictory ? "Mandato cumprido com êxito!" : "Mandato encerrado prematuramente.");
            if (dynamicLogEntry != null)
            {
                dynamicLogEntry.text = $"> [FIM DE JOGO] {reason}";
            }
            TriggerGlitch(0.8f);
        }

        private void Update()
        {
            float dt = Time.deltaTime * animationSpeed;

            curNature = Mathf.MoveTowards(curNature, targetNature, dt * 25f);
            curEconomy = Mathf.MoveTowards(curEconomy, targetEconomy, dt * 25f);
            curRelations = Mathf.MoveTowards(curRelations, targetRelations, dt * 25f);
            curPeople = Mathf.MoveTowards(curPeople, targetPeople, dt * 25f);
            curCorruption = Mathf.MoveTowards(curCorruption, targetCorruption, dt * 25f);

            ApplyVisualValues();
        }

        private void ApplyVisualValues()
        {
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

        private void HideAllArrows()
        {
            HideArrowImmediate(arrowNature);
            HideArrowImmediate(arrowEconomy);
            HideArrowImmediate(arrowRelations);
            HideArrowImmediate(arrowPeople);
            HideArrowImmediate(arrowCorruption);
        }

        private void ShowArrow(Label arrow, float delta, bool isCorruption, bool isPreview, ref Coroutine fadeRoutine)
        {
            if (arrow == null) return;

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }

            if (Mathf.Abs(delta) < 0.1f)
            {
                HideArrowImmediate(arrow, ref fadeRoutine);
                return;
            }

            bool isPositive = delta > 0;
            bool isGood = isCorruption ? !isPositive : isPositive;

            string symbol = isPositive ? "▲" : "▼";
            Color color = isGood
                ? new Color(0.133f, 0.773f, 0.369f)   // #22C55E verde
                : new Color(0.937f, 0.267f, 0.267f);  // #EF4444 vermelho

            // Só muda text, cor e opacity — sem tocar em display nem visibility
            arrow.text = symbol;
            arrow.style.color             = color;
            arrow.style.borderTopColor    = color;
            arrow.style.borderRightColor  = color;
            arrow.style.borderBottomColor = color;
            arrow.style.borderLeftColor   = color;
            arrow.style.opacity           = 1f;

            if (!isPreview && gameObject.activeInHierarchy && isActiveAndEnabled)
            {
                fadeRoutine = StartCoroutine(FadeOutArrowRoutine(arrow, arrowDuration));
            }
        }

        private IEnumerator FadeOutArrowRoutine(Label arrow, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (arrow == null) yield break;

            float opacity = 1f;
            while (opacity > 0.05f)
            {
                opacity -= Time.deltaTime * 3f;
                arrow.style.opacity = opacity;
                yield return null;
            }

            HideArrowImmediate(arrow);
        }

        private void HideArrowImmediate(Label arrow)
        {
            if (arrow == null) return;
            arrow.text             = string.Empty; // sem texto = invisível visualmente
            arrow.style.opacity    = 0f;           // completamente transparente
        }

        private void HideArrowImmediate(Label arrow, ref Coroutine fadeRoutine)
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }
            HideArrowImmediate(arrow);
        }

        public void TriggerGlitch(float strength = 0.4f)
        {
            if (crtMaterial != null && crtMaterial.HasProperty("_GlitchStrength") && gameObject.activeInHierarchy && isActiveAndEnabled)
            {
                StartCoroutine(GlitchPulseRoutine(strength));
            }
        }

        private IEnumerator GlitchPulseRoutine(float strength)
        {
            crtMaterial.SetFloat("_GlitchStrength", strength);
            yield return new WaitForSeconds(0.15f);
            crtMaterial.SetFloat("_GlitchStrength", 0.0f);
        }
    }
}
