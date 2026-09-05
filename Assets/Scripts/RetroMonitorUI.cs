using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Controlador da interface do Monitor Retrô em World Space via UI Toolkit (UXML + USS).
/// Conecta-se ao GameManager para atualizar os 4 Apps (Natureza, Economia, Relações, População)
/// e o módulo de Corrupção com animação suave e efeitos de glitch/feedback.
///
/// Feedback visual de variação de atributos:
/// - PERDA: a barra desce enquanto um "ghost fill" permanece na altura antiga
///   mostrando visualmente quanto foi perdido, depois some gradualmente.
/// - GANHO: um "ghost fill" aparece imediatamente na altura futura antes da
///   barra subir — a barra sobe até alcançar o ghost, que então some.
/// - Seta ↑ (verde) / ↓ (vermelho) — grande, visível à distância.
/// - Tremor (shake) nos cards que perderam valor.
/// - Overlay ✕ quando atributo chega a 0.
/// - Brilho pulsante APENAS na borda quando atributo está em 100%.
/// Corrupção: lógica inversa — ↑ é ruim (vermelho + tremor), ↓ é bom (verde).
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class RetroMonitorUI : MonoBehaviour
{
    [Header("Configurações do Monitor")]
    [Tooltip("Material que usa o shader RetroCRTMonitor para receber pulsos dinâmicos de glitch.")]
    [SerializeField] private Material crtMaterial;
    [SerializeField] private Renderer monitorMeshRenderer;
    [SerializeField] private float animationSpeed = 4f;
    [SerializeField] private bool triggerGlitchOnChanges = true;

    [Header("Feedback Visual & Timings")]
    [Tooltip("Duração em segundos da seta de variação antes de desaparecer.")]
    [SerializeField] private float arrowDuration = 3.0f;
    [Tooltip("Tempo em segundos de delay com o ghost fill visível em preview ANTES da barra começar a subir (ganho).")]
    [SerializeField] private float ghostGainStartDelay = 0.75f;
    [Tooltip("Tempo em segundos que o ghost fill permanece visível APÓS a barra atingir o alvo (perda), antes de sumir.")]
    [SerializeField] private float ghostLossHoldDuration = 1.0f;
    [Tooltip("Velocidade de fade do ghost fill de perda após o término do hold.")]
    [SerializeField] private float ghostLossFadeSpeed = 1.8f;
    [Tooltip("Velocidade de fade do ghost fill de ganho após a barra alcançar o alvo.")]
    [SerializeField] private float ghostGainFadeSpeed = 2.0f;
    [Tooltip("Número de ciclos de tremor ao perder atributo.")]
    [SerializeField] private int shakeCycles = 5;
    [Tooltip("Duração de cada semi-ciclo do tremor em segundos.")]
    [SerializeField] private float shakeInterval = 0.06f;

    private UIDocument _uiDocument;
    private VisualElement _root;

    // ── 4 Apps ────────────────────────────────────────────
    private VisualElement _fillNature, _fillEconomy, _fillRelations, _fillPeople;
    private Label _valueNature, _valueEconomy, _valueRelations, _valuePeople;
    private VisualElement _appNature, _appEconomy, _appRelations, _appPeople;

    // Ghost fills (fantasma de variação)
    private VisualElement _ghostNature, _ghostEconomy, _ghostRelations, _ghostPeople;

    // Setas de variação (Labels grandes)
    private Label _arrowNature, _arrowEconomy, _arrowRelations, _arrowPeople;

    // Dead overlays (atributo zerado)
    private VisualElement _deadOverlayNature, _deadOverlayEconomy, _deadOverlayRelations, _deadOverlayPeople;

    // ── Corrupção ─────────────────────────────────────────
    private VisualElement _fillCorruption;
    private Label _valueCorruption, _corruptionStatusDesc;
    private VisualElement _ghostCorruption;
    private Label _arrowCorruption;

    // ── Header & Logs ──────────────────────────────────────
    private Label _dateLabel, _dynamicLogEntry, _tickerText;
    private ScrollView _logScrollView;

    // ── Valores interpolados ───────────────────────────────
    private float _currentNature    = 50f, _targetNature    = 50f;
    private float _currentEconomy   = 50f, _targetEconomy   = 50f;
    private float _currentRelations = 50f, _targetRelations = 50f;
    private float _currentPeople    = 50f, _targetPeople    = 50f;
    private float _currentCorruption = 0f, _targetCorruption = 0f;

    // ── Timers de delay de ganho e hold de perda ─────────────
    private float _gainDelayNature,     _lossHoldNature;
    private float _gainDelayEconomy,    _lossHoldEconomy;
    private float _gainDelayRelations,  _lossHoldRelations;
    private float _gainDelayPeople,     _lossHoldPeople;
    private float _gainDelayCorruption, _lossHoldCorruption;

    // ── Estado dos ghost fills ─────────────────────────────
    // GhostMode: 0=Inativo, 1=Perda(ghost acima da barra que caiu), 2=Ganho(ghost abaixo da barra que subirá)
    private enum GhostMode { Inactive, Loss, Gain }

    private float _ghostHeightNature,    _ghostOpacityNature;    private GhostMode _ghostModeNature;
    private float _ghostHeightEconomy,   _ghostOpacityEconomy;   private GhostMode _ghostModeEconomy;
    private float _ghostHeightRelations, _ghostOpacityRelations; private GhostMode _ghostModeRelations;
    private float _ghostHeightPeople,    _ghostOpacityPeople;    private GhostMode _ghostModePeople;
    private float _ghostHeightCorruption,_ghostOpacityCorruption;private GhostMode _ghostModeCorruption;

    // ── Shimmer (100%) ────────────────────────────────────
    private bool  _shimmerOn    = false;
    private float _shimmerTimer = 0f;
    private const float ShimmerPeriod = 0.7f;

    // ── Glitch CRT ────────────────────────────────────────
    private float _glitchBurst = 0f;
    private static readonly int GlitchBurstId = Shader.PropertyToID("_GlitchBurst");

    // ── Coroutines de shake ───────────────────────────────
    private Coroutine _shakeNature, _shakeEconomy, _shakeRelations, _shakePeople, _shakeCorruption;

    // ─────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
        if (monitorMeshRenderer == null)
            monitorMeshRenderer = GetComponent<Renderer>();
        if (monitorMeshRenderer != null && crtMaterial == null)
            crtMaterial = monitorMeshRenderer.material;
    }

    private void OnEnable()
    {
        InitializeUI();
        GameManager.OnNewDeal         += HandleNewDeal;
        GameManager.OnChangeAttributes += HandleAttributesChanged;
        GameManager.OnGameOver         += HandleGameOver;
        GameManager.OnGameWin          += HandleGameWin;

        if (GameManager.instance != null && GameManager.instance.gameAttributes != null)
            SetAttributesImmediate(GameManager.instance.gameAttributes);
    }

    private void OnDisable()
    {
        GameManager.OnNewDeal         -= HandleNewDeal;
        GameManager.OnChangeAttributes -= HandleAttributesChanged;
        GameManager.OnGameOver         -= HandleGameOver;
        GameManager.OnGameWin          -= HandleGameWin;
    }

    private void Start()
    {
        UpdateDateDisplay();
        if (GameManager.instance != null && GameManager.instance.gameAttributes != null)
            SetAttributesImmediate(GameManager.instance.gameAttributes);
    }

    private void Update()
    {
        float dt = Time.deltaTime * animationSpeed;

        // Animação da barra principal com delay inicial em caso de ganho
        _currentNature     = TickBarValue(_currentNature,     _targetNature,     ref _gainDelayNature,     dt);
        _currentEconomy    = TickBarValue(_currentEconomy,    _targetEconomy,    ref _gainDelayEconomy,    dt);
        _currentRelations  = TickBarValue(_currentRelations,  _targetRelations,  ref _gainDelayRelations,  dt);
        _currentPeople     = TickBarValue(_currentPeople,     _targetPeople,     ref _gainDelayPeople,     dt);
        _currentCorruption = TickBarValue(_currentCorruption, _targetCorruption, ref _gainDelayCorruption, dt);

        ApplyVisualValues();

        // Ghost fills
        TickGhost(_ghostNature,     ref _ghostHeightNature,     ref _ghostOpacityNature,     ref _ghostModeNature,     ref _lossHoldNature,     _currentNature,     _targetNature);
        TickGhost(_ghostEconomy,    ref _ghostHeightEconomy,    ref _ghostOpacityEconomy,    ref _ghostModeEconomy,    ref _lossHoldEconomy,    _currentEconomy,    _targetEconomy);
        TickGhost(_ghostRelations,  ref _ghostHeightRelations,  ref _ghostOpacityRelations,  ref _ghostModeRelations,  ref _lossHoldRelations,  _currentRelations,  _targetRelations);
        TickGhost(_ghostPeople,     ref _ghostHeightPeople,     ref _ghostOpacityPeople,     ref _ghostModePeople,     ref _lossHoldPeople,     _currentPeople,     _targetPeople);
        TickGhost(_ghostCorruption, ref _ghostHeightCorruption, ref _ghostOpacityCorruption, ref _ghostModeCorruption, ref _lossHoldCorruption, _currentCorruption, _targetCorruption);

        // Shimmer pulsante na borda dos cards em 100%
        _shimmerTimer += Time.deltaTime;
        if (_shimmerTimer >= ShimmerPeriod)
        {
            _shimmerTimer = 0f;
            _shimmerOn = !_shimmerOn;
            TickShimmer();
        }

        // Decaimento suave do glitch CRT
        if (_glitchBurst > 0.001f)
        {
            _glitchBurst = Mathf.MoveTowards(_glitchBurst, 0f, Time.deltaTime * 2.5f);
            if (crtMaterial != null)
                crtMaterial.SetFloat(GlitchBurstId, _glitchBurst);
        }
    }

    private float TickBarValue(float current, float target, ref float gainDelay, float dt)
    {
        if (gainDelay > 0f)
        {
            gainDelay -= Time.deltaTime;
            return current;
        }
        return Mathf.MoveTowards(current, target, dt * 25f);
    }

    // ─────────────────────────────────────────────────────
    //  INICIALIZAÇÃO
    // ─────────────────────────────────────────────────────

    private void InitializeUI()
    {
        if (_uiDocument == null) return;
        _root = _uiDocument.rootVisualElement;
        if (_root == null) return;

        // Fills principais
        _fillNature    = _root.Q<VisualElement>("fill-nature");
        _fillEconomy   = _root.Q<VisualElement>("fill-economy");
        _fillRelations = _root.Q<VisualElement>("fill-relations");
        _fillPeople    = _root.Q<VisualElement>("fill-people");

        // Values
        _valueNature    = _root.Q<Label>("value-nature");
        _valueEconomy   = _root.Q<Label>("value-economy");
        _valueRelations = _root.Q<Label>("value-relations");
        _valuePeople    = _root.Q<Label>("value-people");

        // Cards raiz
        _appNature    = _root.Q<VisualElement>("app-nature");
        _appEconomy   = _root.Q<VisualElement>("app-economy");
        _appRelations = _root.Q<VisualElement>("app-relations");
        _appPeople    = _root.Q<VisualElement>("app-people");

        // Ghost fills
        _ghostNature    = _root.Q<VisualElement>("ghost-fill-nature");
        _ghostEconomy   = _root.Q<VisualElement>("ghost-fill-economy");
        _ghostRelations = _root.Q<VisualElement>("ghost-fill-relations");
        _ghostPeople    = _root.Q<VisualElement>("ghost-fill-people");

        // Setas (Labels grandes)
        _arrowNature    = _root.Q<Label>("arrow-nature");
        _arrowEconomy   = _root.Q<Label>("arrow-economy");
        _arrowRelations = _root.Q<Label>("arrow-relations");
        _arrowPeople    = _root.Q<Label>("arrow-people");

        // Dead overlays
        _deadOverlayNature    = _root.Q<VisualElement>("dead-overlay-nature");
        _deadOverlayEconomy   = _root.Q<VisualElement>("dead-overlay-economy");
        _deadOverlayRelations = _root.Q<VisualElement>("dead-overlay-relations");
        _deadOverlayPeople    = _root.Q<VisualElement>("dead-overlay-people");

        // Cliques interativos
        RegisterAppClick(_appNature,    "Módulo de Meio Ambiente e Clima selecionado.");
        RegisterAppClick(_appEconomy,   "Módulo Econômico e Orçamento Municipal selecionado.");
        RegisterAppClick(_appRelations, "Módulo de Diplomacia e Relações selecionado.");
        RegisterAppClick(_appPeople,    "Módulo de Opinião e Aprovação Popular selecionado.");

        // Corrupção
        _fillCorruption       = _root.Q<VisualElement>("fill-corruption");
        _valueCorruption      = _root.Q<Label>("value-corruption");
        _corruptionStatusDesc = _root.Q<Label>("corruption-status-desc");
        _ghostCorruption      = _root.Q<VisualElement>("ghost-fill-corruption");
        _arrowCorruption      = _root.Q<Label>("arrow-corruption");

        // Header & Logs
        _dateLabel       = _root.Q<Label>("date-label");
        _dynamicLogEntry = _root.Q<Label>("dynamic-log-entry");
        _tickerText      = _root.Q<Label>("ticker-text");
        _logScrollView   = _root.Q<ScrollView>("log-scroll");
    }

    private void RegisterAppClick(VisualElement appElement, string message)
    {
        if (appElement == null) return;
        appElement.RegisterCallback<ClickEvent>(_ =>
        {
            AddLogEntry($"> [CLICK] {message}", "log-entry-highlight");
            TriggerGlitch(0.3f);
        });
    }

    // ─────────────────────────────────────────────────────
    //  ATRIBUTOS — Público
    // ─────────────────────────────────────────────────────

    public void SetAttributesImmediate(Attributes attributes)
    {
        if (attributes == null) return;

        _targetNature     = _currentNature     = attributes.climaticChanges;
        _targetEconomy    = _currentEconomy    = attributes.economy;
        _targetRelations  = _currentRelations  = attributes.internationalRelations;
        _targetPeople     = _currentPeople     = attributes.populationalApproval;
        _targetCorruption = _currentCorruption = attributes.corruption;

        _gainDelayNature     = _lossHoldNature     = 0f;
        _gainDelayEconomy    = _lossHoldEconomy    = 0f;
        _gainDelayRelations  = _lossHoldRelations  = 0f;
        _gainDelayPeople     = _lossHoldPeople     = 0f;
        _gainDelayCorruption = _lossHoldCorruption = 0f;

        // Ghost inativo no início
        _ghostModeNature = _ghostModeEconomy = _ghostModeRelations = _ghostModePeople = _ghostModeCorruption = GhostMode.Inactive;
        _ghostOpacityNature = _ghostOpacityEconomy = _ghostOpacityRelations = _ghostOpacityPeople = _ghostOpacityCorruption = 0f;

        ApplyVisualValues();
        UpdateBoundaryStates(attributes);
    }

    // ─────────────────────────────────────────────────────
    //  HANDLERS de evento
    // ─────────────────────────────────────────────────────

    private void HandleAttributesChanged(Attributes attributes)
    {
        if (attributes == null) return;

        // Delta em relação ao alvo anterior (inteiro, pois Attributes usa int)
        float dNature    = attributes.climaticChanges       - _targetNature;
        float dEconomy   = attributes.economy               - _targetEconomy;
        float dRelations = attributes.internationalRelations - _targetRelations;
        float dPeople    = attributes.populationalApproval  - _targetPeople;
        float dCorrupt   = attributes.corruption            - _targetCorruption;

        bool hasChange = Mathf.Abs(dNature)   > 0.5f || Mathf.Abs(dEconomy)  > 0.5f ||
                         Mathf.Abs(dRelations)> 0.5f || Mathf.Abs(dPeople)   > 0.5f ||
                         Mathf.Abs(dCorrupt)  > 0.5f;

        // Registra novos alvos nos ghosts ANTES de atualizar _target*
        TriggerGhostFeedback(_ghostNature,    ref _ghostHeightNature,    ref _ghostOpacityNature,    ref _ghostModeNature,    ref _gainDelayNature,    ref _lossHoldNature,    _targetNature,    attributes.climaticChanges,       dNature);
        TriggerGhostFeedback(_ghostEconomy,   ref _ghostHeightEconomy,   ref _ghostOpacityEconomy,   ref _ghostModeEconomy,   ref _gainDelayEconomy,   ref _lossHoldEconomy,   _targetEconomy,   attributes.economy,               dEconomy);
        TriggerGhostFeedback(_ghostRelations, ref _ghostHeightRelations, ref _ghostOpacityRelations, ref _ghostModeRelations, ref _gainDelayRelations, ref _lossHoldRelations, _targetRelations, attributes.internationalRelations, dRelations);
        TriggerGhostFeedback(_ghostPeople,    ref _ghostHeightPeople,    ref _ghostOpacityPeople,    ref _ghostModePeople,    ref _gainDelayPeople,    ref _lossHoldPeople,    _targetPeople,    attributes.populationalApproval,  dPeople);
        TriggerGhostFeedback(_ghostCorruption,ref _ghostHeightCorruption,ref _ghostOpacityCorruption,ref _ghostModeCorruption,ref _gainDelayCorruption,ref _lossHoldCorruption,_targetCorruption,attributes.corruption,            dCorrupt);

        // Atualiza alvos APÓS calcular ghosts
        _targetNature     = attributes.climaticChanges;
        _targetEconomy    = attributes.economy;
        _targetRelations  = attributes.internationalRelations;
        _targetPeople     = attributes.populationalApproval;
        _targetCorruption = attributes.corruption;

        // Setas de variação
        ShowArrow(_arrowNature,    dNature,    isCorruption: false);
        ShowArrow(_arrowEconomy,   dEconomy,   isCorruption: false);
        ShowArrow(_arrowRelations, dRelations, isCorruption: false);
        ShowArrow(_arrowPeople,    dPeople,    isCorruption: false);
        ShowArrow(_arrowCorruption,dCorrupt,   isCorruption: true);

        // Shake nas perdas (atributos positivos que caíram, ou corrupção que subiu)
        if (dNature    < -0.5f) TriggerShake(_appNature,    ref _shakeNature);
        if (dEconomy   < -0.5f) TriggerShake(_appEconomy,   ref _shakeEconomy);
        if (dRelations < -0.5f) TriggerShake(_appRelations, ref _shakeRelations);
        if (dPeople    < -0.5f) TriggerShake(_appPeople,    ref _shakePeople);
        if (dCorrupt   >  0.5f) TriggerShake(null,          ref _shakeCorruption); // pilar não tem card shake

        if (hasChange)
        {
            UpdateBoundaryStates(attributes);
            if (triggerGlitchOnChanges)
            {
                TriggerGlitch(0.5f);
                AddLogEntry("> [SISTEMA] Sensores atualizaram estatísticas municipais.", "log-entry-warn");
            }
        }
    }

    private void HandleNewDeal(Deal deal)
    {
        UpdateDateDisplay();
        if (deal != null)
        {
            string title = !string.IsNullOrEmpty(deal.tag) ? deal.tag : deal.name;
            AddLogEntry($"> [DESPACHO] Nova proposta sob análise: \"{title}\"", "log-entry-highlight");
            if (_tickerText != null) _tickerText.text = $"> DECISÃO PENDENTE: {title.ToUpper()}";
        }
        TriggerGlitch(0.4f);
    }

    private void HandleGameOver(string reason)
    {
        AddLogEntry($"> [ALERTA FATAL] FIM DE MANDATO: {reason}", "log-entry-danger");
        if (_tickerText != null) _tickerText.text = "> CRITICAL FAILURE: MANDATO ENCERRADO";
        TriggerGlitch(1.2f);
    }

    private void HandleGameWin(string reason)
    {
        string msg = !string.IsNullOrEmpty(reason) ? reason : "Mandato concluído com êxito!";
        AddLogEntry($"> [SUCESSO] {msg}", "log-entry-highlight");
        if (_tickerText != null) _tickerText.text = "> VICTORY: MANDATO CUMPRIDO COM SUCESSO";
        TriggerGlitch(0.3f);
    }

    // ─────────────────────────────────────────────────────
    //  GHOST FILL — lógica central
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// Define o estado inicial do ghost fill ao detectar uma variação.
    /// oldTarget é o _target* antes da mudança, newTarget é o novo valor.
    /// </summary>
    private void TriggerGhostFeedback(
        VisualElement ghost,
        ref float ghostHeight,
        ref float ghostOpacity,
        ref GhostMode ghostMode,
        ref float gainDelayTimer,
        ref float lossHoldTimer,
        float oldTarget,
        float newTarget,
        float delta)
    {
        if (ghost == null || Mathf.Abs(delta) < 0.5f) return;

        if (delta < 0f)
        {
            // PERDA (barra desce): ghost permanece na altura ANTIGA enquanto a barra desce.
            // Quando a barra alcança o novo alvo, o ghost segura por ghostLossHoldDuration antes de sumir.
            ghostHeight    = Mathf.Clamp(oldTarget, 0f, 100f);
            ghostOpacity   = 1.0f;
            ghostMode      = GhostMode.Loss;
            gainDelayTimer = 0f;
            lossHoldTimer  = ghostLossHoldDuration;
        }
        else
        {
            // GANHO (barra sobe): ghost vai imediatamente para a altura FUTURA como preview do ganho.
            // A barra principal aguarda ghostGainStartDelay para dar tempo ao jogador de ver o ghost antes de encher.
            ghostHeight    = Mathf.Clamp(newTarget, 0f, 100f);
            ghostOpacity   = 0.85f;
            ghostMode      = GhostMode.Gain;
            gainDelayTimer = ghostGainStartDelay;
            lossHoldTimer  = 0f;
        }
    }

    /// <summary>
    /// Atualizado no Update — gerencia o tempo de hold e o fade do ghost fill.
    /// </summary>
    private void TickGhost(
        VisualElement ghost,
        ref float ghostHeight,
        ref float ghostOpacity,
        ref GhostMode mode,
        ref float lossHoldTimer,
        float current,
        float target)
    {
        if (ghost == null) return;

        switch (mode)
        {
            case GhostMode.Loss:
                // Quando a barra já desceu e alcançou o novo alvo:
                if (Mathf.Abs(current - target) <= 0.5f)
                {
                    if (lossHoldTimer > 0f)
                    {
                        lossHoldTimer -= Time.deltaTime;
                    }
                    else
                    {
                        ghostOpacity = Mathf.MoveTowards(ghostOpacity, 0f, Time.deltaTime * ghostLossFadeSpeed);
                        if (ghostOpacity <= 0.01f)
                        {
                            ghostOpacity = 0f;
                            mode = GhostMode.Inactive;
                        }
                    }
                }
                break;

            case GhostMode.Gain:
                // Quando a barra subiu e alcançou o alvo (onde o preview do ghost estava):
                if (Mathf.Abs(current - target) <= 0.5f)
                {
                    ghostOpacity = Mathf.MoveTowards(ghostOpacity, 0f, Time.deltaTime * ghostGainFadeSpeed);
                    if (ghostOpacity <= 0.01f)
                    {
                        ghostOpacity = 0f;
                        mode = GhostMode.Inactive;
                    }
                }
                break;

            case GhostMode.Inactive:
            default:
                ghostOpacity = 0f;
                break;
        }

        ghost.style.height  = new Length(Mathf.Clamp(ghostHeight, 0f, 100f), LengthUnit.Percent);
        ghost.style.opacity = ghostOpacity;
    }

    // ─────────────────────────────────────────────────────
    //  SETAS DE VARIAÇÃO
    // ─────────────────────────────────────────────────────

    private void ShowArrow(Label arrow, float delta, bool isCorruption)
    {
        if (arrow == null || Mathf.Abs(delta) < 0.5f) return;

        bool isUp = delta > 0f;

        arrow.RemoveFromClassList("hidden");
        arrow.RemoveFromClassList("arrow-up");
        arrow.RemoveFromClassList("arrow-down");
        arrow.RemoveFromClassList("arrow-up-bad");
        arrow.RemoveFromClassList("arrow-down-good");

        if (isCorruption)
        {
            arrow.text = isUp ? "↑" : "↓";
            arrow.AddToClassList(isUp ? "arrow-up-bad" : "arrow-down-good");
        }
        else
        {
            arrow.text = isUp ? "↑" : "↓";
            arrow.AddToClassList(isUp ? "arrow-up" : "arrow-down");
        }

        // Fade-out automático após arrowDuration segundos
        arrow.schedule.Execute(() => arrow.AddToClassList("hidden")).StartingIn((long)(arrowDuration * 1000f));
    }

    // ─────────────────────────────────────────────────────
    //  SHAKE
    // ─────────────────────────────────────────────────────

    private void TriggerShake(VisualElement card, ref Coroutine shakeCoroutine)
    {
        if (card == null) return;
        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        shakeCoroutine = StartCoroutine(ShakeCard(card));
    }

    private IEnumerator ShakeCard(VisualElement card)
    {
        for (int i = 0; i < shakeCycles * 2; i++)
        {
            if (i % 2 == 0) card.AddToClassList("card-shaking");
            else             card.RemoveFromClassList("card-shaking");
            yield return new WaitForSeconds(shakeInterval);
        }
        card.RemoveFromClassList("card-shaking");

        if (card == _appNature)    _shakeNature    = null;
        if (card == _appEconomy)   _shakeEconomy   = null;
        if (card == _appRelations) _shakeRelations = null;
        if (card == _appPeople)    _shakePeople    = null;
    }

    // ─────────────────────────────────────────────────────
    //  ESTADOS LIMÍTROFES (0% e 100%)
    // ─────────────────────────────────────────────────────

    private void UpdateBoundaryStates(Attributes attr)
    {
        SetCardBoundary(_appNature,    _deadOverlayNature,    attr.climaticChanges);
        SetCardBoundary(_appEconomy,   _deadOverlayEconomy,   attr.economy);
        SetCardBoundary(_appRelations, _deadOverlayRelations, attr.internationalRelations);
        SetCardBoundary(_appPeople,    _deadOverlayPeople,    attr.populationalApproval);
    }

    private void SetCardBoundary(VisualElement card, VisualElement deadOverlay, int value)
    {
        if (card == null) return;

        if (value <= 0)
        {
            card.AddToClassList("card-dead");
            card.RemoveFromClassList("card-full");
            card.RemoveFromClassList("card-full-glow");
            if (deadOverlay != null) deadOverlay.RemoveFromClassList("hidden");
        }
        else if (value >= 100)
        {
            card.AddToClassList("card-full");
            card.RemoveFromClassList("card-dead");
            if (deadOverlay != null) deadOverlay.AddToClassList("hidden");
        }
        else
        {
            card.RemoveFromClassList("card-dead");
            card.RemoveFromClassList("card-full");
            card.RemoveFromClassList("card-full-glow");
            if (deadOverlay != null) deadOverlay.AddToClassList("hidden");
        }
    }

    // ─────────────────────────────────────────────────────
    //  SHIMMER (borda pulsante nos cards em 100%)
    // ─────────────────────────────────────────────────────

    private void TickShimmer()
    {
        ToggleShimmer(_appNature,    _targetNature    >= 100f);
        ToggleShimmer(_appEconomy,   _targetEconomy   >= 100f);
        ToggleShimmer(_appRelations, _targetRelations >= 100f);
        ToggleShimmer(_appPeople,    _targetPeople    >= 100f);
    }

    private void ToggleShimmer(VisualElement card, bool isFull)
    {
        if (card == null || !isFull) return;
        if (_shimmerOn) card.AddToClassList("card-full-glow");
        else            card.RemoveFromClassList("card-full-glow");
    }

    // ─────────────────────────────────────────────────────
    //  VISUALIZAÇÃO DE VALORES
    // ─────────────────────────────────────────────────────

    private void ApplyVisualValues()
    {
        SetHeightAndText(_fillNature,    _valueNature,    _currentNature);
        SetHeightAndText(_fillEconomy,   _valueEconomy,   _currentEconomy);
        SetHeightAndText(_fillRelations, _valueRelations, _currentRelations);
        SetHeightAndText(_fillPeople,    _valuePeople,    _currentPeople);
        SetHeightAndText(_fillCorruption,_valueCorruption,_currentCorruption);

        if (_corruptionStatusDesc != null)
        {
            int c = Mathf.RoundToInt(_currentCorruption);
            _corruptionStatusDesc.text = c < 25 ? "ESTÁVEL" :
                                         c < 50 ? "ATENÇÃO" :
                                         c < 75 ? "ALERTA ELEVADO" : "PERIGO CRÍTICO";
        }
    }

    private void SetHeightAndText(VisualElement fill, Label label, float value)
    {
        float clamped = Mathf.Clamp(value, 0f, 100f);
        if (fill  != null) fill.style.height = new Length(clamped, LengthUnit.Percent);
        if (label != null) label.text = $"{Mathf.RoundToInt(clamped)}%";
    }

    private void UpdateDateDisplay()
    {
        if (GameManager.instance != null && _dateLabel != null)
            _dateLabel.text = $"MANDATO: {GameManager.instance.month:00}/{GameManager.instance.year}";
    }

    // ─────────────────────────────────────────────────────
    //  LOG & GLITCH
    // ─────────────────────────────────────────────────────

    public void AddLogEntry(string message, string ussClass = null)
    {
        if (_dynamicLogEntry != null) _dynamicLogEntry.text = message;

        if (_logScrollView != null)
        {
            var lbl = new Label(message);
            lbl.AddToClassList("log-entry");
            if (!string.IsNullOrEmpty(ussClass)) lbl.AddToClassList(ussClass);
            _logScrollView.Add(lbl);
            if (_logScrollView.childCount > 10) _logScrollView.RemoveAt(0);
            _logScrollView.ScrollTo(lbl);
        }
    }

    public void TriggerGlitch(float intensity)
    {
        _glitchBurst = Mathf.Clamp(_glitchBurst + intensity, 0f, 2.0f);
        if (crtMaterial != null)
            crtMaterial.SetFloat(GlitchBurstId, _glitchBurst);
    }
}
