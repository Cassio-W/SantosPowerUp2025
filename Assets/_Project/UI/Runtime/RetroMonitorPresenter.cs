using System;
using System.Collections;
using System.Collections.Generic;
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
    /// animações de ghost fills, setas de variação, tremores, efeito typewriter e glitch no CRT.
    /// </summary>
    public class RetroMonitorPresenter : MonoBehaviour
    {
        [Header("Configurações do Monitor")]
        [SerializeField] private Material crtMaterial;
        [SerializeField] private float animationSpeed = 4f;
        [SerializeField] private float statsInterpolationDuration = 0.5f;
        [SerializeField] private float arrowDuration = 4.5f;
        [SerializeField] private bool triggerGlitchOnChanges = true;

        [Header("Animações de Texto (Typewriter)")]
        [SerializeField] private bool enableTypewriter = true;
        [SerializeField] private float logTypewriterSpeed = 45f;
        [SerializeField] private float situationTypewriterSpeed = 35f;
        [SerializeField] private float dateTypewriterSpeed = 30f;

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
        private Label situationLabel;
        private Label dynamicLogEntry;

        // Agendamentos UI Toolkit
        private readonly Dictionary<Label, IVisualElementScheduledItem> _activeTypewriters =
            new Dictionary<Label, IVisualElementScheduledItem>();
        private readonly Dictionary<Label, string> _displayedTexts =
            new Dictionary<Label, string>();
        private IVisualElementScheduledItem _statsInterpolationSchedule;

        // Cache de últimos textos definidos para evitar disparos redundantes
        private string _lastDateText;
        private string _lastSituationText;
        private string _lastLogText;

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

        private void Start()
        {
            if (dynamicLogEntry != null && enableTypewriter && gameObject.activeInHierarchy)
            {
                string initialMsg = "> [SISTEMA] Telemetria operacional. Aguardando despachos...";
                _lastLogText = SanitizeRetroText(initialMsg);
                PlayTypewriter(dynamicLogEntry, initialMsg, logTypewriterSpeed, showCursor: false);
            }
        }

        private void OnEnable()
        {
            EnsureReferences();
            CacheElements();
        }

        private void OnDisable()
        {
            StopAllTypewriters();
            StopStatsInterpolation();
        }

        private void OnDestroy()
        {
            StopAllTypewriters();
            StopStatsInterpolation();
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
            situationLabel = root.Q<Label>("situation-label") ?? root.Q<Label>("situation-display");
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

            // Dispara animação de interpolação sincronizada de preenchimento e números
            AnimateStatsInterpolation(targetNature, targetEconomy, targetRelations, targetPeople, targetCorruption, statsInterpolationDuration);

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
            if (dateLabel == null || string.IsNullOrEmpty(displayDate)) return;

            string fullText = $"DATA: {displayDate}";
            string sanitized = SanitizeRetroText(fullText);

            if (string.Equals(_lastDateText, sanitized, StringComparison.Ordinal) && dateLabel.text == sanitized)
            {
                return;
            }

            _lastDateText = sanitized;

            if (enableTypewriter && gameObject.activeInHierarchy && isActiveAndEnabled && dateLabel.panel != null)
            {
                PlayTypewriter(dateLabel, sanitized, dateTypewriterSpeed, showCursor: false);
            }
            else
            {
                dateLabel.text = sanitized;
            }
        }

        public void UpdateSituation(string situationText)
        {
            if (situationLabel == null || string.IsNullOrEmpty(situationText)) return;

            string formatted = situationText.StartsWith("SITUAÇÃO:", StringComparison.OrdinalIgnoreCase)
                ? situationText
                : $"SITUAÇÃO: {situationText}";
            string sanitized = SanitizeRetroText(formatted);

            if (string.Equals(_lastSituationText, sanitized, StringComparison.Ordinal) && situationLabel.text == sanitized)
            {
                return;
            }

            _lastSituationText = sanitized;

            if (enableTypewriter && gameObject.activeInHierarchy && isActiveAndEnabled && situationLabel.panel != null)
            {
                PlayTypewriter(situationLabel, sanitized, situationTypewriterSpeed, showCursor: false);
            }
            else
            {
                situationLabel.text = sanitized;
            }
        }

        public void NotifyNewProposal(CardDefinition card)
        {
            if (card == null) return;

            string title = !string.IsNullOrEmpty(card.categoryTag) ? card.categoryTag : card.title;
            string message = $"> [DESPACHO] Nova proposta: \"{title}\"";
            string sanitized = SanitizeRetroText(message);

            if (dynamicLogEntry != null)
            {
                SetLogVariantClass("log-dispatch");

                if (!string.Equals(_lastLogText, sanitized, StringComparison.Ordinal) || dynamicLogEntry.text != sanitized)
                {
                    _lastLogText = sanitized;
                    if (enableTypewriter && gameObject.activeInHierarchy && isActiveAndEnabled && dynamicLogEntry.panel != null)
                    {
                        PlayTypewriter(dynamicLogEntry, sanitized, logTypewriterSpeed, showCursor: true);
                    }
                    else
                    {
                        dynamicLogEntry.text = sanitized;
                    }
                }
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
            string message = $"> [FIM DE JOGO] {reason}";
            string sanitized = SanitizeRetroText(message);

            if (dynamicLogEntry != null)
            {
                SetLogVariantClass(termination.IsVictory ? "log-system" : "log-alert");

                if (!string.Equals(_lastLogText, sanitized, StringComparison.Ordinal) || dynamicLogEntry.text != sanitized)
                {
                    _lastLogText = sanitized;
                    if (enableTypewriter && gameObject.activeInHierarchy && isActiveAndEnabled && dynamicLogEntry.panel != null)
                    {
                        PlayTypewriter(dynamicLogEntry, sanitized, logTypewriterSpeed, showCursor: true);
                    }
                    else
                    {
                        dynamicLogEntry.text = sanitized;
                    }
                }
            }

            TriggerGlitch(0.8f);
        }

        private void Update()
        {
            // Fallback para edição/visualização no editor quando não estiver executando
            if (_statsInterpolationSchedule == null && !Application.isPlaying)
            {
                ApplyVisualValues();
            }
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

        // =========================================================================
        // TYPEWRITER & FORMATAÇÃO RETRÔ
        // =========================================================================

        /// <summary>
        /// Executa o efeito de digitação retrô (Typewriter) em um elemento de texto (Label).
        /// </summary>
        public void PlayTypewriter(Label label, string fullText, float charsPerSecond = 45f, bool showCursor = true, Action onComplete = null)
        {
            if (label == null) return;

            fullText = SanitizeRetroText(fullText);

            // Se o texto já está completamente exibido e idêntico, e não há animação rodando, não reinicia
            if (_displayedTexts.TryGetValue(label, out var lastText) && string.Equals(lastText, fullText, StringComparison.Ordinal))
            {
                if (!_activeTypewriters.ContainsKey(label))
                {
                    label.text = fullText;
                    onComplete?.Invoke();
                    return;
                }
            }

            if (_activeTypewriters.TryGetValue(label, out var prevSchedule) && prevSchedule != null)
            {
                prevSchedule.Pause();
                _activeTypewriters.Remove(label);
            }

            _displayedTexts[label] = fullText;

            if (string.IsNullOrEmpty(fullText))
            {
                label.text = string.Empty;
                onComplete?.Invoke();
                return;
            }

            if (!enableTypewriter || charsPerSecond <= 0f || label.panel == null)
            {
                label.text = fullText;
                onComplete?.Invoke();
                return;
            }

            label.text = showCursor ? "█" : string.Empty;

            float startTime = Time.unscaledTime;
            int totalChars = fullText.Length;
            float duration = totalChars / Mathf.Max(1f, charsPerSecond);

            var schedule = label.schedule.Execute(() =>
            {
                float elapsed = Time.unscaledTime - startTime;
                int currentLength = Mathf.Clamp(Mathf.RoundToInt((elapsed / Mathf.Max(0.01f, duration)) * totalChars), 0, totalChars);

                if (currentLength < totalChars && showCursor)
                {
                    label.text = fullText.Substring(0, currentLength) + "█";
                }
                else
                {
                    label.text = fullText.Substring(0, currentLength);
                }

                if (currentLength >= totalChars)
                {
                    if (_activeTypewriters.TryGetValue(label, out var s) && s != null)
                    {
                        s.Pause();
                        _activeTypewriters.Remove(label);
                    }
                    onComplete?.Invoke();
                }
            }).Every(0);

            _activeTypewriters[label] = schedule;
        }

        /// <summary>
        /// Interrompe todas as animações de digitação ativas.
        /// </summary>
        public void StopAllTypewriters()
        {
            foreach (var kvp in _activeTypewriters)
            {
                kvp.Value?.Pause();
            }
            _activeTypewriters.Clear();
        }

        /// <summary>
        /// Pula todas as animações de digitação ativas, interrompendo os agendamentos.
        /// </summary>
        public void SkipAllTypewriters()
        {
            StopAllTypewriters();
        }

        /// <summary>
        /// Altera dinamicamente a variante de estilo do log do terminal (ex: log-dispatch, log-alert, log-warning, log-system).
        /// </summary>
        public void SetLogVariantClass(string className)
        {
            if (dynamicLogEntry == null) return;

            dynamicLogEntry.RemoveFromClassList("log-system");
            dynamicLogEntry.RemoveFromClassList("log-dispatch");
            dynamicLogEntry.RemoveFromClassList("log-warning");
            dynamicLogEntry.RemoveFromClassList("log-alert");

            if (!string.IsNullOrEmpty(className))
            {
                dynamicLogEntry.AddToClassList(className);
            }
        }

        /// <summary>
        /// Registra uma mensagem personalizada no terminal inferior com efeito typewriter e estilo.
        /// </summary>
        public void LogMessage(string message, string logStyleClass = "log-system", float speed = -1f)
        {
            if (dynamicLogEntry == null || string.IsNullOrEmpty(message)) return;

            SetLogVariantClass(logStyleClass);
            string sanitized = SanitizeRetroText(message);

            if (string.Equals(_lastLogText, sanitized, StringComparison.Ordinal) && dynamicLogEntry.text == sanitized)
            {
                return;
            }

            _lastLogText = sanitized;
            float actualSpeed = speed > 0f ? speed : logTypewriterSpeed;

            if (enableTypewriter && gameObject.activeInHierarchy && isActiveAndEnabled && dynamicLogEntry.panel != null)
            {
                PlayTypewriter(dynamicLogEntry, sanitized, actualSpeed, showCursor: true);
            }
            else
            {
                dynamicLogEntry.text = sanitized;
            }
        }

        /// <summary>
        /// Animação de interpolação sincronizada entre o preenchimento de background e os valores numéricos (com curva EaseOutCubic idêntica à urna).
        /// </summary>
        public void AnimateStatsInterpolation(
            float targetClimate,
            float targetEconomy,
            float targetRelations,
            float targetPeople,
            float targetCorruption,
            float duration = -1f)
        {
            float actualDuration = duration > 0f ? duration : statsInterpolationDuration;
            float startClimate = curNature;
            float startEconomy = curEconomy;
            float startRelations = curRelations;
            float startPeople = curPeople;
            float startCorruption = curCorruption;

            // Se não houve alteração relevante entre os valores atuais e alvos, não precisa animar
            bool needsAnimation = Mathf.Abs(targetClimate - startClimate) >= 0.1f ||
                                  Mathf.Abs(targetEconomy - startEconomy) >= 0.1f ||
                                  Mathf.Abs(targetRelations - startRelations) >= 0.1f ||
                                  Mathf.Abs(targetPeople - startPeople) >= 0.1f ||
                                  Mathf.Abs(targetCorruption - startCorruption) >= 0.1f;

            if (!needsAnimation)
            {
                curNature = targetClimate;
                curEconomy = targetEconomy;
                curRelations = targetRelations;
                curPeople = targetPeople;
                curCorruption = targetCorruption;
                ApplyVisualValues();
                return;
            }

            StopStatsInterpolation();

            if (root == null || root.panel == null || !gameObject.activeInHierarchy || actualDuration <= 0.001f)
            {
                curNature = targetClimate;
                curEconomy = targetEconomy;
                curRelations = targetRelations;
                curPeople = targetPeople;
                curCorruption = targetCorruption;
                ApplyVisualValues();
                return;
            }

            float startTime = Time.unscaledTime;

            _statsInterpolationSchedule = root.schedule.Execute(() =>
            {
                float elapsed = Time.unscaledTime - startTime;
                float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, actualDuration));

                // Easing suave (EaseOutCubic) idêntico à UI da urna
                float t = 1f - Mathf.Pow(1f - normalized, 3f);

                curNature = Mathf.Lerp(startClimate, targetClimate, t);
                curEconomy = Mathf.Lerp(startEconomy, targetEconomy, t);
                curRelations = Mathf.Lerp(startRelations, targetRelations, t);
                curPeople = Mathf.Lerp(startPeople, targetPeople, t);
                curCorruption = Mathf.Lerp(startCorruption, targetCorruption, t);

                ApplyVisualValues();

                if (normalized >= 1f)
                {
                    StopStatsInterpolation();
                }
            }).Every(0);
        }

        /// <summary>
        /// Interrompe qualquer interpolação de atributos/stats em andamento.
        /// </summary>
        public void StopStatsInterpolation()
        {
            if (_statsInterpolationSchedule != null)
            {
                _statsInterpolationSchedule.Pause();
                _statsInterpolationSchedule = null;
            }
        }

        /// <summary>
        /// Normaliza e remove diacríticos/acentos e caracteres especiais não suportados nativamente
        /// pela fonte RETROTECH (8-bit ASCII), evitando acionamento de fonte de fallback do TextCore
        /// que causa deslocamento vertical / offsets de altura indesejados nas linhas de texto.
        /// </summary>
        public static string SanitizeRetroText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            text = text.Replace('•', '-');

            string normalized = text.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder(normalized.Length);

            for (int i = 0; i < normalized.Length; i++)
            {
                char c = normalized[i];
                var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }
    }
}
