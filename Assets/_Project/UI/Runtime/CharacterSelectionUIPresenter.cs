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
            _detailName.text =
                character.displayName;
        }

        if (_detailTitle != null)
        {
            _detailTitle.text =
                !string.IsNullOrEmpty(character.title)
                    ? character.title.ToUpperInvariant()
                    : "CANDIDATO(A)";
        }

        if (_detailBio != null)
        {
            _detailBio.text =
                character.biography;
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
            _detailAbility.text =
                $"• HABILIDADE ESPECIAL:\n" +
                character.uniqueAbilityDescription;

            _detailAbility.style.display =
                DisplayStyle.Flex;
        }
        else
        {
            _detailAbility.style.display =
                DisplayStyle.None;
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

        if (_statClimate != null)
        {
            _statClimate.text =
                $"{character.initialStats.climaticChanges}%";
        }

        if (_statEconomy != null)
        {
            _statEconomy.text =
                $"{character.initialStats.economy}%";
        }

        if (_statRelations != null)
        {
            _statRelations.text =
                $"{character.initialStats.internationalRelations}%";
        }

        if (_statPeople != null)
        {
            _statPeople.text =
                $"{character.initialStats.popularApproval}%";
        }
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
                character.displayName;
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
}

}
