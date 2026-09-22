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

    private class CarouselSlotView
    {
        public VisualElement root;
        public VisualElement portrait;
        public VisualElement badge;
        public Label name;
    }

    private VisualElement _viewport;
    private VisualElement _track;
    private readonly CarouselSlotView[] _slots = new CarouselSlotView[5];

    private VisualElement _itemPrev => _slots[1]?.root;
    private VisualElement _itemCurr => _slots[2]?.root;
    private VisualElement _itemNext => _slots[3]?.root;

    private VisualElement _portraitPrev => _slots[1]?.portrait;
    private VisualElement _portraitCurr => _slots[2]?.portrait;
    private VisualElement _portraitNext => _slots[3]?.portrait;

    private Label _namePrev => _slots[1]?.name;
    private Label _nameCurr => _slots[2]?.name;
    private Label _nameNext => _slots[3]?.name;

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
    private int _pendingTargetIndex;
    private IVisualElementScheduledItem _animationSchedule;
    private int _lastNavFrame = -1;
    private float _lastNavTime = -1f;

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

        _viewport = _root.Q<VisualElement>("carousel-viewport");
        _track = _root.Q<VisualElement>("carousel-track");

        // Slot 0 (Far Prev)
        _slots[0] = new CarouselSlotView
        {
            root = _root.Q<VisualElement>("carousel-item-far-prev") ?? _root.Q<VisualElement>("slot-far-prev"),
            portrait = _root.Q<VisualElement>("portrait-far-prev"),
            badge = null,
            name = _root.Q<Label>("name-far-prev")
        };

        // Slot 1 (Prev)
        _slots[1] = new CarouselSlotView
        {
            root = _root.Q<VisualElement>("carousel-item-prev") ?? _root.Q<VisualElement>("item-prev"),
            portrait = _root.Q<VisualElement>("portrait-prev"),
            badge = null,
            name = _root.Q<Label>("name-prev")
        };

        // Slot 2 (Curr / Active)
        _slots[2] = new CarouselSlotView
        {
            root = _root.Q<VisualElement>("carousel-item-curr") ?? _root.Q<VisualElement>("item-curr"),
            portrait = _root.Q<VisualElement>("portrait-curr"),
            badge = _root.Q<Label>("badge-curr") ?? _root.Q<VisualElement>("active-badge"),
            name = _root.Q<Label>("name-curr")
        };

        // Slot 3 (Next)
        _slots[3] = new CarouselSlotView
        {
            root = _root.Q<VisualElement>("carousel-item-next") ?? _root.Q<VisualElement>("item-next"),
            portrait = _root.Q<VisualElement>("portrait-next"),
            badge = null,
            name = _root.Q<Label>("name-next")
        };

        // Slot 4 (Far Next)
        _slots[4] = new CarouselSlotView
        {
            root = _root.Q<VisualElement>("carousel-item-far-next") ?? _root.Q<VisualElement>("slot-far-next"),
            portrait = _root.Q<VisualElement>("portrait-far-next"),
            badge = null,
            name = _root.Q<Label>("name-far-next")
        };

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
        if (Time.frameCount == _lastNavFrame || (Time.unscaledTime - _lastNavTime) < 0.05f)
            return;

        _lastNavFrame = Time.frameCount;
        _lastNavTime = Time.unscaledTime;

        if (!CanNavigate())
            return;

        int prevIndex =
            (_currentIndex - 1 + _characters.Count) %
            _characters.Count;

        PlayCarouselAnimation(
            prevIndex,
            -1);
    }

    public void NavigateDown()
    {
        if (Time.frameCount == _lastNavFrame || (Time.unscaledTime - _lastNavTime) < 0.05f)
            return;

        _lastNavFrame = Time.frameCount;
        _lastNavTime = Time.unscaledTime;

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
        return _characters != null &&
               _characters.Count > 1;
    }

    // ---------------------------------------------------------------------
    // Animação Contínua da Roleta (Track Deslizante & Tempo Real)
    // ---------------------------------------------------------------------

    private void PlayCarouselAnimation(
        int nextIndex,
        int direction)
    {
        if (_characters == null ||
            _characters.Count <= 1)
        {
            return;
        }

        // Se o usuário pressionar enquanto anima, conclui a anterior imediatamente sem glitch
        if (_isAnimating)
        {
            FinishCarouselAnimation(_pendingTargetIndex);
            if (direction > 0)
                nextIndex = (_currentIndex + 1) % _characters.Count;
            else
                nextIndex = (_currentIndex - 1 + _characters.Count) % _characters.Count;
        }

        _isAnimating = true;
        _pendingTargetIndex = nextIndex;

        // 1. Atualização IMEDIATA em Tempo Real dos Detalhes e Contador
        CharacterDefinition targetCharacter = _characters[nextIndex];
        UpdateDetails(targetCharacter);

        if (_counterLabel != null)
        {
            _counterLabel.text =
                $"{nextIndex + 1:D2} / {_characters.Count:D2}";
        }

        OnCharacterChanged?.Invoke(targetCharacter);

        // 2. Preenche os 5 slots com os personagens vizinhos corretos
        RefreshSlotsContent(_currentIndex);

        // 3. Calcula a distância precisa de rolagem do track
        float distance = GetStepDistance(direction);

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
                    -direction * (progress * distance);

                if (_track != null)
                {
                    _track.style.translate =
                        new Translate(0f, offset, 0f);
                }
                else
                {
                    ApplyCarouselOffset(offset);
                }

                ApplyMorphTransition(direction, progress);

                if (normalized >= 1f)
                {
                    FinishCarouselAnimation(
                        nextIndex);
                }
            })
            .Every(0);
    }

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

    private float GetStepDistance(int direction)
    {
        if (_slots[2]?.root != null)
        {
            if (direction > 0 && _slots[3]?.root != null)
            {
                float h2 = _slots[2].root.resolvedStyle.height;
                float h3 = _slots[3].root.resolvedStyle.height;
                float dist = (h2 + h3) * 0.5f + 12f;
                if (dist > 50f) return dist;
            }
            else if (direction < 0 && _slots[1]?.root != null)
            {
                float h2 = _slots[2].root.resolvedStyle.height;
                float h1 = _slots[1].root.resolvedStyle.height;
                float dist = (h2 + h1) * 0.5f + 12f;
                if (dist > 50f) return dist;
            }
        }

        return 217f;
    }

    private void ApplyMorphTransition(int direction, float progress)
    {
        if (direction > 0) // Descendo (Next -> Center, Curr -> Top)
        {
            MorphSlot(_slots[2], fromActiveToSide: true, progress);
            MorphSlot(_slots[3], fromActiveToSide: false, progress);
        }
        else // Subindo (Prev -> Center, Curr -> Bottom)
        {
            MorphSlot(_slots[2], fromActiveToSide: true, progress);
            MorphSlot(_slots[1], fromActiveToSide: false, progress);
        }
    }

    private void MorphSlot(CarouselSlotView slot, bool fromActiveToSide, float t)
    {
        if (slot == null || slot.root == null) return;

        float activeFactor = fromActiveToSide ? (1f - t) : t;

        slot.root.style.opacity = Mathf.Lerp(0.65f, 1f, activeFactor);

        Color sideBg = new Color(226f / 255f, 232f / 255f, 240f / 255f, 1f);
        Color centerBg = new Color(226f / 255f, 232f / 255f, 240f / 255f, 0f);
        slot.root.style.backgroundColor = Color.Lerp(sideBg, centerBg, activeFactor);

        if (slot.portrait != null)
        {
            float portraitSize = Mathf.Lerp(75f, 145f, activeFactor);
            slot.portrait.style.width = portraitSize;
            slot.portrait.style.height = portraitSize;
        }

        if (slot.name != null)
        {
            slot.name.style.fontSize = Mathf.Lerp(13f, 26f, activeFactor);
        }
    }

    private void FinishCarouselAnimation(
        int nextIndex)
    {
        StopScheduledAnimationOnly();

        _currentIndex = nextIndex;

        ResetTrackAndMorphs();
        RefreshSlotsContent(_currentIndex);

        _isAnimating = false;
    }

    private void ResetTrackAndMorphs()
    {
        if (_track != null)
        {
            _track.style.translate =
                new Translate(0f, 0f, 0f);
        }

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i]?.root == null) continue;

            _slots[i].root.style.translate =
                new Translate(0f, 0f, 0f);
            _slots[i].root.style.opacity =
                StyleKeyword.Null;
            _slots[i].root.style.backgroundColor =
                StyleKeyword.Null;

            if (_slots[i].portrait != null)
            {
                _slots[i].portrait.style.width =
                    StyleKeyword.Null;
                _slots[i].portrait.style.height =
                    StyleKeyword.Null;
            }

            if (_slots[i].name != null)
            {
                _slots[i].name.style.fontSize =
                    StyleKeyword.Null;
            }
        }
    }

    private void ResetCarouselTransforms()
    {
        ResetTrackAndMorphs();
    }

    private void StopCarouselAnimation()
    {
        StopScheduledAnimationOnly();
        StopAllTypewriters();
        StopStatsInterpolation();

        _isAnimating = false;

        ResetTrackAndMorphs();
    }

    private void StopScheduledAnimationOnly()
    {
        if (_animationSchedule != null)
        {
            _animationSchedule.Pause();
            _animationSchedule = null;
        }
    }

    private void ApplyCarouselOffset(
        float offset)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i]?.root != null)
            {
                _slots[i].root.style.translate =
                    new Translate(0f, offset, 0f);
            }
        }
    }

    // ---------------------------------------------------------------------
    // Refresh & Atualização de Slots
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
        _currentIndex = Mathf.Clamp(_currentIndex, 0, count - 1);

        ResetTrackAndMorphs();
        RefreshSlotsContent(_currentIndex);

        if (_counterLabel != null)
        {
            _counterLabel.text =
                $"{_currentIndex + 1:D2} / {count:D2}";
        }

        // Detalhes
        UpdateDetails(CurrentCharacter);
    }

    private void RefreshSlotsContent(int centerIndex)
    {
        if (_characters == null || _characters.Count == 0) return;
        int count = _characters.Count;

        int idx0 = ((centerIndex - 2) % count + count) % count;
        int idx1 = ((centerIndex - 1) % count + count) % count;
        int idx2 = centerIndex;
        int idx3 = (centerIndex + 1) % count;
        int idx4 = (centerIndex + 2) % count;

        UpdateSlotData(_slots[0], _characters[idx0]);
        UpdateSlotData(_slots[1], _characters[idx1]);
        UpdateSlotData(_slots[2], _characters[idx2]);
        UpdateSlotData(_slots[3], _characters[idx3]);
        UpdateSlotData(_slots[4], _characters[idx4]);
    }

    private void UpdateSlotData(CarouselSlotView slot, CharacterDefinition character)
    {
        if (slot == null || character == null) return;

        if (slot.name != null)
        {
            slot.name.text = SanitizeRetroText(character.displayName);
        }

        if (slot.portrait != null)
        {
            if (character.portrait != null)
            {
                slot.portrait.style.backgroundImage =
                    new StyleBackground(character.portrait);
                slot.portrait.style.display = DisplayStyle.Flex;
            }
            else
            {
                slot.portrait.style.backgroundImage = StyleKeyword.None;
            }
        }
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

            string abilityText = $"TRUQUE DE CAMPANHA:\n{SanitizeRetroText(character.uniqueAbilityDescription)}";
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
