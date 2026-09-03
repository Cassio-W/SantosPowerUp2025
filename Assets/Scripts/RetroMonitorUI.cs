using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Controlador da interface do Monitor Retrô em World Space via UI Toolkit (UXML + USS).
/// Conecta-se ao GameManager para atualizar os 4 Apps (Natureza, Economia, Relações, População),
/// o módulo de Corrupção, data e logs de terminal com animação suave e efeitos de glitch.
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

    private UIDocument _uiDocument;
    private VisualElement _root;

    // 4 Apps Quadrados
    private VisualElement _fillNature;
    private VisualElement _fillEconomy;
    private VisualElement _fillRelations;
    private VisualElement _fillPeople;

    private Label _valueNature;
    private Label _valueEconomy;
    private Label _valueRelations;
    private Label _valuePeople;

    private VisualElement _appNature;
    private VisualElement _appEconomy;
    private VisualElement _appRelations;
    private VisualElement _appPeople;

    // Módulo de Corrupção
    private VisualElement _fillCorruption;
    private Label _valueCorruption;
    private Label _corruptionStatusDesc;

    // Header & Logs
    private Label _dateLabel;
    private Label _dynamicLogEntry;
    private Label _tickerText;
    private ScrollView _logScrollView;

    // Valores atuais e alvos (0 - 100)
    private float _currentNature = 50f, _targetNature = 50f;
    private float _currentEconomy = 50f, _targetEconomy = 50f;
    private float _currentRelations = 50f, _targetRelations = 50f;
    private float _currentPeople = 50f, _targetPeople = 50f;
    private float _currentCorruption = 0f, _targetCorruption = 0f;

    private float _glitchBurst = 0f;
    private static readonly int GlitchBurstId = Shader.PropertyToID("_GlitchBurst");

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
        if (monitorMeshRenderer != null && crtMaterial == null)
        {
            crtMaterial = monitorMeshRenderer.material;
        }
    }

    private void OnEnable()
    {
        InitializeUI();

        GameManager.OnNewDeal += HandleNewDeal;
        GameManager.OnChangeAttributes += HandleAttributesChanged;
        GameManager.OnGameOver += HandleGameOver;
        GameManager.OnGameWin += HandleGameWin;
    }

    private void OnDisable()
    {
        GameManager.OnNewDeal -= HandleNewDeal;
        GameManager.OnChangeAttributes -= HandleAttributesChanged;
        GameManager.OnGameOver -= HandleGameOver;
        GameManager.OnGameWin -= HandleGameWin;
    }

    private void Start()
    {
        UpdateDateDisplay();

        if (GameManager.instance != null && GameManager.instance.gameAttributes != null)
        {
            SetAttributesImmediate(GameManager.instance.gameAttributes);
        }
    }

    private void UpdateDateDisplay()
    {
        if (GameManager.instance != null && _dateLabel != null)
        {
            _dateLabel.text = $"MANDATO: {GameManager.instance.month:00}/{GameManager.instance.year}";
        }
    }

    private void Update()
    {
        // Interpolação suave dos valores
        float dt = Time.deltaTime * animationSpeed;

        _currentNature = Mathf.MoveTowards(_currentNature, _targetNature, dt * 25f);
        _currentEconomy = Mathf.MoveTowards(_currentEconomy, _targetEconomy, dt * 25f);
        _currentRelations = Mathf.MoveTowards(_currentRelations, _targetRelations, dt * 25f);
        _currentPeople = Mathf.MoveTowards(_currentPeople, _targetPeople, dt * 25f);
        _currentCorruption = Mathf.MoveTowards(_currentCorruption, _targetCorruption, dt * 25f);

        ApplyVisualValues();

        // Decaimento suave do glitch dinâmico no material CRT
        if (_glitchBurst > 0.001f)
        {
            _glitchBurst = Mathf.MoveTowards(_glitchBurst, 0f, Time.deltaTime * 2.5f);
            if (crtMaterial != null)
            {
                crtMaterial.SetFloat(GlitchBurstId, _glitchBurst);
            }
        }
    }

    private void InitializeUI()
    {
        if (_uiDocument == null) return;
        _root = _uiDocument.rootVisualElement;
        if (_root == null) return;

        // 4 Apps
        _fillNature = _root.Q<VisualElement>("fill-nature");
        _fillEconomy = _root.Q<VisualElement>("fill-economy");
        _fillRelations = _root.Q<VisualElement>("fill-relations");
        _fillPeople = _root.Q<VisualElement>("fill-people");

        _valueNature = _root.Q<Label>("value-nature");
        _valueEconomy = _root.Q<Label>("value-economy");
        _valueRelations = _root.Q<Label>("value-relations");
        _valuePeople = _root.Q<Label>("value-people");

        _appNature = _root.Q<VisualElement>("app-nature");
        _appEconomy = _root.Q<VisualElement>("app-economy");
        _appRelations = _root.Q<VisualElement>("app-relations");
        _appPeople = _root.Q<VisualElement>("app-people");

        // Registrar cliques nos apps para feedback interativo
        RegisterAppClick(_appNature, "Módulo de Meio Ambiente e Clima selecionado.");
        RegisterAppClick(_appEconomy, "Módulo Econômico e Orçamento Municipal selecionado.");
        RegisterAppClick(_appRelations, "Módulo de Diplomacia e Relações selecionado.");
        RegisterAppClick(_appPeople, "Módulo de Opinião e Aprovação Popular selecionado.");

        // Corrupção
        _fillCorruption = _root.Q<VisualElement>("fill-corruption");
        _valueCorruption = _root.Q<Label>("value-corruption");
        _corruptionStatusDesc = _root.Q<Label>("corruption-status-desc");

        // Header & Logs
        _dateLabel = _root.Q<Label>("date-label");
        _dynamicLogEntry = _root.Q<Label>("dynamic-log-entry");
        _tickerText = _root.Q<Label>("ticker-text");
        _logScrollView = _root.Q<ScrollView>("log-scroll");
    }

    private void RegisterAppClick(VisualElement appElement, string message)
    {
        if (appElement == null) return;
        appElement.RegisterCallback<ClickEvent>(evt =>
        {
            AddLogEntry($"> [CLICK] {message}", "log-entry-highlight");
            TriggerGlitch(0.3f);
        });
    }

    public void SetAttributesImmediate(Attributes attributes)
    {
        if (attributes == null) return;

        _targetNature = _currentNature = attributes.climaticChanges;
        _targetEconomy = _currentEconomy = attributes.economy;
        _targetRelations = _currentRelations = attributes.internationalRelations;
        _targetPeople = _currentPeople = attributes.populationalApproval;
        _targetCorruption = _currentCorruption = attributes.corruption;

        ApplyVisualValues();
    }

    private void HandleAttributesChanged(Attributes attributes)
    {
        if (attributes == null) return;

        bool hasChange = (Mathf.Abs(_targetNature - attributes.climaticChanges) > 1 ||
                          Mathf.Abs(_targetEconomy - attributes.economy) > 1 ||
                          Mathf.Abs(_targetRelations - attributes.internationalRelations) > 1 ||
                          Mathf.Abs(_targetPeople - attributes.populationalApproval) > 1 ||
                          Mathf.Abs(_targetCorruption - attributes.corruption) > 1);

        _targetNature = attributes.climaticChanges;
        _targetEconomy = attributes.economy;
        _targetRelations = attributes.internationalRelations;
        _targetPeople = attributes.populationalApproval;
        _targetCorruption = attributes.corruption;

        if (hasChange && triggerGlitchOnChanges)
        {
            TriggerGlitch(0.5f);
            AddLogEntry($"> [SISTEMA] Sensores atualizaram estatísticas municipais.", "log-entry-warn");
        }
    }

    private void HandleNewDeal(Deal deal)
    {
        UpdateDateDisplay();

        if (deal != null)
        {
            string dealTitle = !string.IsNullOrEmpty(deal.tag) ? deal.tag : deal.name;
            AddLogEntry($"> [DESPACHO] Nova proposta sob análise: \"{dealTitle}\"", "log-entry-highlight");
            if (_tickerText != null)
            {
                _tickerText.text = $"> DECISÃO PENDENTE: {dealTitle.ToUpper()}";
            }
        }

        TriggerGlitch(0.4f);
    }

    private void HandleGameOver(string reason)
    {
        AddLogEntry($"> [ALERTA FATAL] FIM DE MANDATO: {reason}", "log-entry-danger");
        if (_tickerText != null)
        {
            _tickerText.text = $"> CRITICAL FAILURE: MANDATO ENCERRADO";
        }
        TriggerGlitch(1.2f);
    }

    private void HandleGameWin(string reason)
    {
        string winMsg = !string.IsNullOrEmpty(reason) ? reason : "Mandato concluído com êxito! Cidade estabilizada.";
        AddLogEntry($"> [SUCESSO] {winMsg}", "log-entry-highlight");
        if (_tickerText != null)
        {
            _tickerText.text = $"> VICTORY: MANDATO CUMPRIDO COM SUCESSO";
        }
        TriggerGlitch(0.3f);
    }

    private void ApplyVisualValues()
    {
        // 4 Apps Quadrados (Background Fill Vertical)
        SetElementHeightAndText(_fillNature, _valueNature, _currentNature);
        SetElementHeightAndText(_fillEconomy, _valueEconomy, _currentEconomy);
        SetElementHeightAndText(_fillRelations, _valueRelations, _currentRelations);
        SetElementHeightAndText(_fillPeople, _valuePeople, _currentPeople);

        // Barra de Corrupção (Pilar Vertical - Altura)
        SetElementHeightAndText(_fillCorruption, _valueCorruption, _currentCorruption);

        if (_corruptionStatusDesc != null)
        {
            int corr = Mathf.RoundToInt(_currentCorruption);
            if (corr < 25)
            {
                _corruptionStatusDesc.text = "ESTÁVEL";
            }
            else if (corr < 50)
            {
                _corruptionStatusDesc.text = "ATENÇÃO";
            }
            else if (corr < 75)
            {
                _corruptionStatusDesc.text = "ALERTA ELEVADO";
            }
            else
            {
                _corruptionStatusDesc.text = "PERIGO CRÍTICO";
            }
        }
    }

    private void SetElementWidthAndText(VisualElement fillElement, Label textLabel, float value)
    {
        float clamped = Mathf.Clamp(value, 0f, 100f);
        if (fillElement != null)
        {
            fillElement.style.width = new Length(clamped, LengthUnit.Percent);
        }
        if (textLabel != null)
        {
            textLabel.text = $"{Mathf.RoundToInt(clamped)}%";
        }
    }

    private void SetElementHeightAndText(VisualElement fillElement, Label textLabel, float value)
    {
        float clamped = Mathf.Clamp(value, 0f, 100f);
        if (fillElement != null)
        {
            fillElement.style.height = new Length(clamped, LengthUnit.Percent);
        }
        if (textLabel != null)
        {
            textLabel.text = $"{Mathf.RoundToInt(clamped)}%";
        }
    }

    public void AddLogEntry(string message, string ussClass = null)
    {
        if (_dynamicLogEntry != null)
        {
            _dynamicLogEntry.text = message;
        }

        if (_logScrollView != null)
        {
            var newLabel = new Label(message);
            newLabel.AddToClassList("log-entry");
            if (!string.IsNullOrEmpty(ussClass))
            {
                newLabel.AddToClassList(ussClass);
            }
            _logScrollView.Add(newLabel);

            // Manter no máximo 10 linhas para performance
            if (_logScrollView.childCount > 10)
            {
                _logScrollView.RemoveAt(0);
            }

            _logScrollView.ScrollTo(newLabel);
        }
    }

    public void TriggerGlitch(float intensity)
    {
        _glitchBurst = Mathf.Clamp(_glitchBurst + intensity, 0f, 2.0f);
        if (crtMaterial != null)
        {
            crtMaterial.SetFloat(GlitchBurstId, _glitchBurst);
        }
    }
}
