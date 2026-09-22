using System;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mandato.UI
{
/// <summary>
/// Presenter da tela de seleção de personagem em UI Toolkit.
///
/// Responsabilidades:
/// - Exibir o personagem atualmente selecionado.
/// - Controlar o carrossel vertical anterior/atual/próximo.
/// - Exibir os detalhes do personagem selecionado.
/// - Animar a troca do personagem através do movimento vertical dos slots.
/// - Emitir OnCharacterChanged quando a seleção é efetivamente alterada.
///
/// A UI não altera estado de domínio diretamente.
/// </summary>
public class CharacterSelectionUIPresenter : MonoBehaviour
{
[Header("UI Document e Configurações")]
[SerializeField] private UIDocument uiDocument;
[SerializeField] private VisualTreeAsset uxmlAsset;
[SerializeField] private PanelSettings panelSettings;

    [Header("RenderTexture e Material da Telinha")]
    [SerializeField] private Renderer screenRenderer;
    [SerializeField] private RenderTexture screenRenderTexture;
    [SerializeField] private string texturePropertyName = "_BaseMap";

    [Header("Navegação")]
    [SerializeField] private bool allowKeyboardNavigation = false;

    [Header("Animação da Roleta")]
    [SerializeField] private float carouselAnimationDuration = 0.18f;
    [SerializeField] private float carouselAnimationOvershoot = 1.05f;

    private VisualElement _root;

    // ---------------------------------------------------------------------
    // Carrossel
    // ---------------------------------------------------------------------

    private VisualElement _itemPrev;
    private VisualElement _itemCurr;
    private VisualElement _itemNext;

    private VisualElement _portraitPrev;
    private VisualElement _portraitCurr;
    private VisualElement _portraitNext;

    private Label _namePrev;
    private Label _nameCurr;
    private Label _nameNext;

    private Label _counterLabel;

    // ---------------------------------------------------------------------
    // Painel de detalhes
    // ---------------------------------------------------------------------

    private Label _detailName;
    private Label _detailTitle;
    private Label _detailBio;
    private VisualElement _detailPortrait;
    private Label _detailAbility;

    private VisualElement _statsContainer;
    private Label _statClimate;
    private Label _statEconomy;
    private Label _statRelations;
    private Label _statPeople;
    private Label _statCorruption;
    private VisualElement _fillClimate;
    private VisualElement _fillEconomy;
    private VisualElement _fillRelations;
    private VisualElement _fillPeople;
    private VisualElement _fillCorruption;

    // ---------------------------------------------------------------------
    // Animações de Texto (Typewriter) e Interpolação de Parâmetros
    // ---------------------------------------------------------------------

    [Header("Animações de Texto e Parâmetros")]
    [SerializeField] private float nameTypewriterSpeed = 65f;
    [SerializeField] private float titleTypewriterSpeed = 55f;
    [SerializeField] private float bioTypewriterSpeed = 70f;
    [SerializeField] private float abilityTypewriterSpeed = 60f;
    [SerializeField] private float statsInterpolationDuration = 0.45f;

    private readonly Dictionary<Label, IVisualElementScheduledItem> _activeTypewriters =
        new Dictionary<Label, IVisualElementScheduledItem>();

    private float _displayedClimate = 0f;
    private float _displayedEconomy = 0f;
    private float _displayedRelations = 0f;
    private float _displayedPeople = 0f;
    private float _displayedCorruption = 0f;
    private bool _hasInitializedStats = false;
    private IVisualElementScheduledItem _statsInterpolationSchedule;

    // ---------------------------------------------------------------------
    // Estado
    // ---------------------------------------------------------------------

    private List<CharacterDefinition> _characters = new List<CharacterDefinition>();
    private int _currentIndex;

    private bool _isAnimating;
    private IVisualElementScheduledItem _animationSchedule;

    public CharacterDefinition CurrentCharacter =>
        (_characters != null &&
         _characters.Count > 0 &&
         _currentIndex >= 0 &&
         _currentIndex < _characters.Count)
            ? _characters[_currentIndex]
            : null;

    public int CurrentIndex => _currentIndex;

    public int CharacterCount =>
        _characters != null ? _characters.Count : 0;

    public bool IsAnimating => _isAnimating;

    public event Action<CharacterDefinition> OnCharacterChanged;

    // ---------------------------------------------------------------------
    // Unity lifecycle
    // ---------------------------------------------------------------------

    private void Awake()
    {
        EnsureReferences();
        CacheElements();
        ApplyRenderTextureToMaterial();
    }

    private void OnEnable()
    {
        EnsureReferences();
        CacheElements();
        ResetCarouselTransforms();
        RefreshDisplay();
    }

    private void OnDisable()
    {
        StopCarouselAnimation();
        StopAllTypewriters();
        StopStatsInterpolation();
    }

    private void Update()
    {
        if (!allowKeyboardNavigation)
            return;

        if (_isAnimating)
            return;

        if (_characters == null || _characters.Count <= 1)
            return;

        if (Input.GetKeyDown(KeyCode.UpArrow) ||
            Input.GetKeyDown(KeyCode.W))
        {
            NavigateUp();
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) ||
                 Input.GetKeyDown(KeyCode.S))
        {
            NavigateDown();
        }
    }

    // ---------------------------------------------------------------------
    // Setup
    // ---------------------------------------------------------------------

    public void EnsureReferences()
    {
        if (uiDocument == null)
        {
            uiDocument =
                GetComponent<UIDocument>() ??
                GetComponentInChildren<UIDocument>();
        }

        if (screenRenderer == null)
        {
            screenRenderer =
                GetComponent<Renderer>() ??
                GetComponentInChildren<Renderer>();
        }

        if (uiDocument == null)
            return;

        if (uiDocument.panelSettings == null &&
            panelSettings != null)
        {
            uiDocument.panelSettings = panelSettings;
        }

        if (uiDocument.visualTreeAsset == null &&
            uxmlAsset != null)
        {
            uiDocument.visualTreeAsset = uxmlAsset;
        }
    }

    private void ApplyRenderTextureToMaterial()
    {
        if (screenRenderer == null ||
            screenRenderTexture == null)
        {
            return;
        }

        Material material = screenRenderer.material;

        if (material == null)
            return;

        if (material.HasProperty(texturePropertyName))
        {
            material.SetTexture(
                texturePropertyName,
                screenRenderTexture);
        }
        else if (material.HasProperty("_MainTex"))
        {
            material.SetTexture(
                "_MainTex",
                screenRenderTexture);
        }
    }

    private void CacheElements()
    {
        if (uiDocument == null ||
            uiDocument.rootVisualElement == null)
        {
            return;
        }

        _root = uiDocument.rootVisualElement;

        // Carrossel
        _itemPrev =
            _root.Q<VisualElement>("carousel-item-prev") ??
            _root.Q<VisualElement>("item-prev");

        _itemCurr =
            _root.Q<VisualElement>("carousel-item-curr") ??
            _root.Q<VisualElement>("item-curr");

        _itemNext =
            _root.Q<VisualElement>("carousel-item-next") ??
            _root.Q<VisualElement>("item-next");

        _portraitPrev =
            _root.Q<VisualElement>("portrait-prev");

        _portraitCurr =
            _root.Q<VisualElement>("portrait-curr");

        _portraitNext =
            _root.Q<VisualElement>("portrait-next");

        _namePrev =
            _root.Q<Label>("name-prev");

        _nameCurr =
            _root.Q<Label>("name-curr");

        _nameNext =
            _root.Q<Label>("name-next");

        _counterLabel =
            _root.Q<Label>("carousel-counter") ??
            _root.Q<Label>("txt-counter");

        // Detalhes
        _detailName =
            _root.Q<Label>("detail-name") ??
            _root.Q<Label>("txt-character-name");

        _detailTitle =
            _root.Q<Label>("detail-title") ??
            _root.Q<Label>("txt-character-title");

        _detailBio =
            _root.Q<Label>("detail-bio") ??
            _root.Q<Label>("txt-character-bio");

        _detailPortrait =
            _root.Q<VisualElement>("detail-portrait-img") ??
            _root.Q<VisualElement>("detail-portrait");

        _detailAbility =
            _root.Q<Label>("detail-ability") ??
            _root.Q<Label>("txt-character-ability");

        // Stats
        _statsContainer =
            _root.Q<VisualElement>("stats-container");

        _statClimate =
            _root.Q<Label>("stat-climate-val");

        _statEconomy =
            _root.Q<Label>("stat-economy-val");

        _statRelations =
            _root.Q<Label>("stat-relations-val");

        _statPeople =
            _root.Q<Label>("stat-people-val");

        _statCorruption =
            _root.Q<Label>("stat-corruption-val") ??
            _root.Q<Label>("value-corruption");

        _fillClimate =
            _root.Q<VisualElement>("fill-climate");

        _fillEconomy =
            _root.Q<VisualElement>("fill-economy");

        _fillRelations =
            _root.Q<VisualElement>("fill-relations");

        _fillPeople =
            _root.Q<VisualElement>("fill-people");

        _fillCorruption =
            _root.Q<VisualElement>("fill-corruption");
    }

    // ---------------------------------------------------------------------
    // Inicialização
    // ---------------------------------------------------------------------

    public void Initialize(List<CharacterDefinition> characters)
    {
        StopCarouselAnimation();

        _characters =
            characters ?? new List<CharacterDefinition>();

        _currentIndex = 0;

        EnsureReferences();
        CacheElements();
        ResetCarouselTransforms();
        RefreshDisplay();
    }

    // ---------------------------------------------------------------------
    // Navegação
    // ---------------------------------------------------------------------

    public void NavigateUp()
    {
        if (!CanNavigate())
            return;

        int nextIndex =
            (_currentIndex - 1 + _characters.Count) %
            _characters.Count;

        PlayCarouselAnimation(
            nextIndex,
            -1);
    }

    public void NavigateDown()
    {
        if (!CanNavigate())
            return;

        int nextIndex =
            (_currentIndex + 1) %
            _characters.Count;

        PlayCarouselAnimation(
            nextIndex,
            1);
    }

    /// <summary>
    /// Seleciona diretamente um personagem.
    /// Não utiliza a animação da roleta.
    /// </summary>
    public void SetIndex(int index)
    {
        if (_characters == null ||
            _characters.Count == 0)
        {
            return;
        }

        StopCarouselAnimation();

        _currentIndex =
            Mathf.Clamp(
                index,
                0,
                _characters.Count - 1);

        ResetCarouselTransforms();
        RefreshDisplay();

        OnCharacterChanged?.Invoke(CurrentCharacter);
    }

    private bool CanNavigate()
    {
        return !_isAnimating &&
               _characters != null &&
               _characters.Count > 1;
    }

    // ---------------------------------------------------------------------
    // Animação da roleta
    // ---------------------------------------------------------------------

    private void PlayCarouselAnimation(
        int nextIndex,
        int direction)
    {
        if (_isAnimating)
            return;

        if (_characters == null ||
            _characters.Count <= 1)
        {
            return;
        }

        if (_itemPrev == null ||
            _itemCurr == null ||
            _itemNext == null)
        {
            _currentIndex = nextIndex;
            RefreshDisplay();
            OnCharacterChanged?.Invoke(CurrentCharacter);
            return;
        }

        _isAnimating = true;

        float slotHeight =
            GetCarouselSlotHeight();

        if (slotHeight <= 0f)
            slotHeight = 160f;

        float distance =
            slotHeight * carouselAnimationOvershoot;

        float startTime = Time.unscaledTime;

        StopScheduledAnimationOnly();

        _animationSchedule =
            _root.schedule.Execute(() =>
            {
                float elapsed =
                    Time.unscaledTime - startTime;

                float normalized =
                    Mathf.Clamp01(
                        elapsed /
                        Mathf.Max(
                            0.01f,
                            carouselAnimationDuration));

                float progress =
                    EvaluateCarouselProgress(
                        normalized);

                float offset =
                    Mathf.Lerp(
                        0f,
                        -direction * distance,
                        progress);

                ApplyCarouselOffset(offset);

                if (normalized >= 1f)
                {
                    FinishCarouselAnimation(
                        nextIndex);
                }
            })
            .Every(0);
    }

    /// <summary>
    /// Movimento deliberadamente mais mecânico que um carousel moderno.
    ///
    /// O início é rápido, há uma pequena desaceleração no final
    /// e um pequeno overshoot já configurado no deslocamento.
    /// </summary>
    private float EvaluateCarouselProgress(
        float normalized)
    {
        if (normalized < 0.72f)
        {
            float phase =
                normalized / 0.72f;

            return Mathf.Lerp(
                0f,
                0.88f,
                phase);
        }

        float finalPhase =
            (normalized - 0.72f) / 0.28f;

        return Mathf.Lerp(
            0.88f,
            1f,
            finalPhase);
    }

    private float GetCarouselSlotHeight()
    {
        if (_itemCurr != null &&
            _itemCurr.resolvedStyle.height > 0f)
        {
            return _itemCurr.resolvedStyle.height;
        }

        if (_itemPrev != null &&
            _itemPrev.resolvedStyle.height > 0f)
        {
            return _itemPrev.resolvedStyle.height;
        }

        if (_itemNext != null &&
            _itemNext.resolvedStyle.height > 0f)
        {
            return _itemNext.resolvedStyle.height;
        }

        return 0f;
    }

    private void ApplyCarouselOffset(
        float offset)
    {
        SetElementVerticalOffset(
            _itemPrev,
            offset);

        SetElementVerticalOffset(
            _itemCurr,
            offset);

        SetElementVerticalOffset(
            _itemNext,
            offset);
    }

    private void SetElementVerticalOffset(
        VisualElement element,
        float offset)
    {
        if (element == null)
            return;

        element.style.translate =
            new Translate(
                0f,
                offset,
                0f);
    }

    private void FinishCarouselAnimation(
        int nextIndex)
    {
        StopScheduledAnimationOnly();

        _currentIndex = nextIndex;

        ResetCarouselTransforms();
        RefreshDisplay();

        _isAnimating = false;

        OnCharacterChanged?.Invoke(
            CurrentCharacter);
    }

    private void ResetCarouselTransforms()
    {
        SetElementVerticalOffset(
            _itemPrev,
            0f);

        SetElementVerticalOffset(
            _itemCurr,
            0f);

        SetElementVerticalOffset(
            _itemNext,
            0f);
    }

    private void StopCarouselAnimation()
    {
        StopScheduledAnimationOnly();
        StopAllTypewriters();
        StopStatsInterpolation();

        _isAnimating = false;

        ResetCarouselTransforms();
    }

    private void StopScheduledAnimationOnly()
    {
        if (_animationSchedule != null)
        {
            _animationSchedule.Pause();
            _animationSchedule = null;
        }
    }

    // ---------------------------------------------------------------------
    // Refresh
    // ---------------------------------------------------------------------

    private void RefreshDisplay()
    {
        if (_characters == null ||
            _characters.Count == 0)
        {
            return;
        }

        EnsureReferences();
        CacheElements();

        int count = _characters.Count;

        int prevIndex =
            (_currentIndex - 1 + count) %
            count;

        int nextIndex =
            (_currentIndex + 1) %
            count;

        CharacterDefinition prevCharacter =
            _characters[prevIndex];

        CharacterDefinition currentCharacter =
            _characters[_currentIndex];

        CharacterDefinition nextCharacter =
            _characters[nextIndex];

        // Carrossel
        UpdateCarouselSlot(
            _itemPrev,
            _portraitPrev,
            _namePrev,
            prevCharacter);

        UpdateCarouselSlot(
            _itemCurr,
            _portraitCurr,
            _nameCurr,
            currentCharacter);

        UpdateCarouselSlot(
            _itemNext,
            _portraitNext,
            _nameNext,
            nextCharacter);

        if (_counterLabel != null)
        {
            _counterLabel.text =
                $"{_currentIndex + 1:D2} / {count:D2}";
        }

        // Detalhes
        UpdateDetails(currentCharacter);
    }

    private void UpdateDetails(
        CharacterDefinition character)
    {
        if (character == null)
            return;

        if (_detailName != null)
        {
            string nameText = SanitizeRetroText(character.displayName);
            PlayTypewriter(_detailName, nameText, nameTypewriterSpeed);
        }

        if (_detailTitle != null)
        {
            string titleText = !string.IsNullOrEmpty(character.title)
                ? SanitizeRetroText(character.title).ToUpperInvariant()
                : "CANDIDATO(A)";
            PlayTypewriter(_detailTitle, titleText, titleTypewriterSpeed);
        }

        if (_detailBio != null)
        {
            string bioText = SanitizeRetroText(character.biography);
            PlayTypewriter(_detailBio, bioText, bioTypewriterSpeed);
        }

        UpdateDetailPortrait(character);
        UpdateAbility(character);
        UpdateStats(character);
    }

    private void UpdateDetailPortrait(
        CharacterDefinition character)
    {
        if (_detailPortrait == null)
            return;

        if (character.portrait != null)
        {
            _detailPortrait.style.backgroundImage =
                new StyleBackground(
                    character.portrait);

            _detailPortrait.style.display =
                DisplayStyle.Flex;
        }
        else
        {
            _detailPortrait.style.backgroundImage =
                StyleKeyword.None;
        }
    }

    private void UpdateAbility(
        CharacterDefinition character)
    {
        if (_detailAbility == null)
            return;

        if (!string.IsNullOrEmpty(
                character.uniqueAbilityDescription))
        {
            _detailAbility.style.display =
                DisplayStyle.Flex;

            string abilityText = $"- HABILIDADE ESPECIAL:\n{SanitizeRetroText(character.uniqueAbilityDescription)}";
            PlayTypewriter(
                _detailAbility,
                abilityText,
                abilityTypewriterSpeed);
        }
        else
        {
            _detailAbility.style.display =
                DisplayStyle.None;
            _detailAbility.text = string.Empty;
        }
    }

    private void UpdateStats(
        CharacterDefinition character)
    {
        bool hasStats =
            character.overrideInitialStats &&
            character.initialStats != null;

        if (_statsContainer != null)
        {
            _statsContainer.style.display =
                hasStats
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
        }

        if (!hasStats)
            return;

        float targetClimate = Mathf.Clamp(character.initialStats.climaticChanges, 0, 100);
        float targetEconomy = Mathf.Clamp(character.initialStats.economy, 0, 100);
        float targetRelations = Mathf.Clamp(character.initialStats.internationalRelations, 0, 100);
        float targetPeople = Mathf.Clamp(character.initialStats.popularApproval, 0, 100);
        float targetCorruption = Mathf.Clamp(character.initialStats.corruption, 0, 100);

        AnimateStatsInterpolation(targetClimate, targetEconomy, targetRelations, targetPeople, targetCorruption, statsInterpolationDuration);
    }

    private void UpdateCarouselSlot(
        VisualElement slotContainer,
        VisualElement portraitElement,
        Label nameLabel,
        CharacterDefinition character)
    {
        if (character == null)
            return;

        if (nameLabel != null)
        {
            nameLabel.text =
                SanitizeRetroText(character.displayName);
        }

        if (portraitElement == null)
            return;

        if (character.portrait != null)
        {
            portraitElement.style.backgroundImage =
                new StyleBackground(
                    character.portrait);
        }
        else
        {
            portraitElement.style.backgroundImage =
                StyleKeyword.None;
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

        // Substitui caracteres especiais comuns não presentes no conjunto RETROTECH
        text = text.Replace('•', '-');

        // Decompõe caracteres acentuados (ex: 'ã' -> 'a' + '~') e filtra marcas sem espaçamento
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

    // ---------------------------------------------------------------------
    // Efeito Typewriter e Interpolação de Atributos
    // ---------------------------------------------------------------------

    private void PlayTypewriter(Label label, string fullText, float charsPerSecond = 60f)
    {
        if (label == null) return;

        if (_activeTypewriters.TryGetValue(label, out var prevSchedule) && prevSchedule != null)
        {
            prevSchedule.Pause();
            _activeTypewriters.Remove(label);
        }

        if (string.IsNullOrEmpty(fullText))
        {
            label.text = string.Empty;
            return;
        }

        // Dá "clear" antes de iniciar o efeito de digitação
        label.text = string.Empty;

        float startTime = Time.unscaledTime;
        int totalChars = fullText.Length;
        float duration = totalChars / Mathf.Max(1f, charsPerSecond);

        var schedule = label.schedule.Execute(() =>
        {
            float elapsed = Time.unscaledTime - startTime;
            int currentLength = Mathf.Clamp(Mathf.RoundToInt((elapsed / Mathf.Max(0.01f, duration)) * totalChars), 0, totalChars);
            label.text = fullText.Substring(0, currentLength);

            if (currentLength >= totalChars)
            {
                if (_activeTypewriters.TryGetValue(label, out var s) && s != null)
                {
                    s.Pause();
                    _activeTypewriters.Remove(label);
                }
            }
        }).Every(0);

        _activeTypewriters[label] = schedule;
    }

    private void StopAllTypewriters()
    {
        foreach (var kvp in _activeTypewriters)
        {
            kvp.Value?.Pause();
        }
        _activeTypewriters.Clear();
    }

    private void AnimateStatsInterpolation(
        float targetClimate,
        float targetEconomy,
        float targetRelations,
        float targetPeople,
        float targetCorruption,
        float duration = 0.45f)
    {
        float startClimate = _displayedClimate;
        float startEconomy = _displayedEconomy;
        float startRelations = _displayedRelations;
        float startPeople = _displayedPeople;
        float startCorruption = _displayedCorruption;

        if (!_hasInitializedStats)
        {
            startClimate = 0f;
            startEconomy = 0f;
            startRelations = 0f;
            startPeople = 0f;
            startCorruption = 0f;
            _hasInitializedStats = true;
        }

        StopStatsInterpolation();

        float startTime = Time.unscaledTime;

        _statsInterpolationSchedule = _root.schedule.Execute(() =>
        {
            float elapsed = Time.unscaledTime - startTime;
            float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));

            // Easing suave (EaseOutCubic)
            float t = 1f - Mathf.Pow(1f - normalized, 3f);

            _displayedClimate = Mathf.Lerp(startClimate, targetClimate, t);
            _displayedEconomy = Mathf.Lerp(startEconomy, targetEconomy, t);
            _displayedRelations = Mathf.Lerp(startRelations, targetRelations, t);
            _displayedPeople = Mathf.Lerp(startPeople, targetPeople, t);
            _displayedCorruption = Mathf.Lerp(startCorruption, targetCorruption, t);

            // Atualiza alturas das barras de preenchimento
            if (_fillClimate != null) _fillClimate.style.height = Length.Percent(_displayedClimate);
            if (_fillEconomy != null) _fillEconomy.style.height = Length.Percent(_displayedEconomy);
            if (_fillRelations != null) _fillRelations.style.height = Length.Percent(_displayedRelations);
            if (_fillPeople != null) _fillPeople.style.height = Length.Percent(_displayedPeople);
            if (_fillCorruption != null) _fillCorruption.style.height = Length.Percent(_displayedCorruption);

            // Atualiza contagem dos valores numéricos
            if (_statClimate != null) _statClimate.text = $"{Mathf.RoundToInt(_displayedClimate)}%";
            if (_statEconomy != null) _statEconomy.text = $"{Mathf.RoundToInt(_displayedEconomy)}%";
            if (_statRelations != null) _statRelations.text = $"{Mathf.RoundToInt(_displayedRelations)}%";
            if (_statPeople != null) _statPeople.text = $"{Mathf.RoundToInt(_displayedPeople)}%";
            if (_statCorruption != null) _statCorruption.text = $"{Mathf.RoundToInt(_displayedCorruption)}%";

            if (normalized >= 1f)
            {
                StopStatsInterpolation();
            }
        }).Every(0);
    }

    private void StopStatsInterpolation()
    {
        if (_statsInterpolationSchedule != null)
        {
            _statsInterpolationSchedule.Pause();
            _statsInterpolationSchedule = null;
        }
    }
}

}
