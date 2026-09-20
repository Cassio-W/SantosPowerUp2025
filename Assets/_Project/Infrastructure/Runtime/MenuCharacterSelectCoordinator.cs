using System;
using System.Collections;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;
using Mandato.Presentation;
using Mandato.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mandato.Infrastructure
{
    /// <summary>
    /// Contextos de interação do menu inicial (MenuV2).
    /// Segue a diretriz de contextos de interação (contextos-interacao-e-setores.md).
    /// </summary>
    public enum MenuInteractionContext
    {
        MainMenuOverview,   // Visão ampla do menu principal: UI 2D (Jogar, Créditos, Sair) ativa.
        CharacterSelect,    // Câmera focada na telinha 3D: carrossel e botões de seleção de personagem ativos.
        Transitioning       // Câmera em movimento: inputs de navegação bloqueados.
    }

    /// <summary>
    /// Orquestra o fluxo e os contextos de interação no MenuV2.
    /// Todos os objetos 3D permanecem permanentemente ativos no cenário (cenário contínuo e diegético).
    /// Apenas a UI 2D do menu principal é ocultada/exibida durante as transições de câmera.
    /// Controla a interatividade e as animações de clique dos botões físicos 3D da urna.
    /// </summary>
    public class MenuCharacterSelectCoordinator : MonoBehaviour
    {
        [Header("Câmera & Contexto")]
        [Tooltip("Anchor de posição/rotação da câmera focando na telinha de seleção.")]
        [SerializeField] private Transform characterSelectCameraAnchor;
        [SerializeField] private float cameraTravelDuration = 1.0f;
        [SerializeField] private AnimationCurve cameraCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Visibilidade da UI 2D")]
        [Tooltip("GameObjects ou presenters da UI do menu principal a serem ocultados na seleção.")]
        [SerializeField] private List<GameObject> menuUIObjects = new List<GameObject>();
        [SerializeField] private MainMenuPresenter mainMenuPresenter;

        [Header("Personagens Disponíveis")]
        [SerializeField] private List<CharacterDefinition> availableCharacters = new List<CharacterDefinition>();

        [Header("Apresentador UI Toolkit da Telinha 3D")]
        [SerializeField] private CharacterSelectionUIPresenter uiPresenter;

        [Header("Botões Físicos 3D da Urna (Opcional - Auto-detectado se vazio)")]
        [SerializeField] private MenuPhysicalButton3D buttonUp;
        [SerializeField] private MenuPhysicalButton3D buttonDown;
        [SerializeField] private MenuPhysicalButton3D buttonConfirm;
        [SerializeField] private MenuPhysicalButton3D buttonBack;
        [SerializeField] private List<MenuPhysicalButton3D> physicalButtons = new List<MenuPhysicalButton3D>();

        [Header("Cena de Gameplay")]
        [SerializeField] private string gameplaySceneName = "JogoV2";

        private Camera _camera;
        private Vector3 _menuCameraPosition;
        private Quaternion _menuCameraRotation;
        private float _menuCameraFov;
        private Coroutine _cameraMoveCoroutine;

        public MenuInteractionContext CurrentContext { get; private set; } = MenuInteractionContext.MainMenuOverview;
        public bool IsInCharacterSelect => CurrentContext == MenuInteractionContext.CharacterSelect;

        public event Action<MenuInteractionContext, MenuInteractionContext> OnContextChanged;

        private void Awake()
        {
            _camera = Camera.main;
            if (mainMenuPresenter == null)
            {
                mainMenuPresenter = FindFirstObjectByType<MainMenuPresenter>();
            }

            FindAndBindPhysicalButtons();
        }

        private void Start()
        {
            if (_camera != null)
            {
                _menuCameraPosition = _camera.transform.position;
                _menuCameraRotation = _camera.transform.rotation;
                _menuCameraFov = _camera.fieldOfView;
            }

            // Inicializa a telinha 3D no mundo com os personagens desde o início
            if (uiPresenter != null && availableCharacters != null && availableCharacters.Count > 0)
            {
                uiPresenter.Initialize(availableCharacters);
            }

            // Garante que os botões físicos 3D comecem desabilitados para interação no menu inicial
            SetPhysicalButtonsInteractable(false);
        }

        private void Update()
        {
            // Roteamento central de comandos de teclado conforme o contexto ativo (Regra 5)
            if (CurrentContext == MenuInteractionContext.CharacterSelect)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    if (buttonBack != null) buttonBack.AnimateClick();
                    ExitCharacterSelect();
                }
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    if (buttonConfirm != null) buttonConfirm.AnimateClick();
                    ConfirmSelection();
                }
                else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                {
                    if (buttonUp != null) buttonUp.AnimateClick();
                    NavigateUp();
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                {
                    if (buttonDown != null) buttonDown.AnimateClick();
                    NavigateDown();
                }
            }
        }

        /// <summary>
        /// Localiza e associa os botões físicos 3D da cena caso não estejam vinculados no Inspector.
        /// </summary>
        private void FindAndBindPhysicalButtons()
        {
            if (physicalButtons == null)
            {
                physicalButtons = new List<MenuPhysicalButton3D>();
            }

            if (physicalButtons.Count == 0)
            {
                var found = FindObjectsByType<MenuPhysicalButton3D>(FindObjectsSortMode.None);
                if (found != null)
                {
                    physicalButtons.AddRange(found);
                }
            }

            foreach (var btn in physicalButtons)
            {
                if (btn == null) continue;

                // Tenta associar com base no nome do GameObject ou características
                string btnName = btn.gameObject.name.ToLowerInvariant();
                if (buttonUp == null && (btnName.Contains("up") || btnName.Contains("cima") || btnName.Contains("prev") || btnName.Contains("laranja") || btnName.Contains("corrige") || btnName.Contains("869")))
                {
                    buttonUp = btn;
                }
                else if (buttonDown == null && (btnName.Contains("down") || btnName.Contains("baixo") || btnName.Contains("next") || btnName.Contains("amarelo") || btnName.Contains("branco") || btnName.Contains("870")))
                {
                    buttonDown = btn;
                }
                else if (buttonConfirm == null && (btnName.Contains("confirm") || btnName.Contains("verde") || btnName.Contains("enter") || btnName.Contains("confirma") || btnName.Contains("868")))
                {
                    buttonConfirm = btn;
                }
                else if (buttonBack == null && (btnName.Contains("back") || btnName.Contains("cancel") || btnName.Contains("vermelho") || btnName.Contains("esc") || btnName.Contains("sair") || btnName.Contains("867")))
                {
                    buttonBack = btn;
                }
            }
        }

        /// <summary>
        /// Define a interatividade de todos os botões físicos da urna.
        /// </summary>
        private void SetPhysicalButtonsInteractable(bool interactable)
        {
            if (physicalButtons == null) return;

            foreach (var btn in physicalButtons)
            {
                if (btn != null)
                {
                    btn.SetInteractable(interactable);
                }
            }
        }

        /// <summary>
        /// Altera o contexto de interação do menu de forma declarativa e centralizada.
        /// </summary>
        public void SetContext(MenuInteractionContext newContext)
        {
            if (CurrentContext == newContext) return;

            var oldContext = CurrentContext;
            CurrentContext = newContext;
            OnContextChanged?.Invoke(newContext, oldContext);
        }

        /// <summary>
        /// Chamado ao clicar em "Jogar".
        /// Transiciona para CharacterSelect: oculta apenas a UI 2D do menu e move a câmera para a cabine 3D.
        /// </summary>
        public void EnterCharacterSelect()
        {
            if (CurrentContext != MenuInteractionContext.MainMenuOverview) return;

            SetContext(MenuInteractionContext.Transitioning);
            SetPhysicalButtonsInteractable(false);

            // 1. Oculta apenas a UI 2D do Menu Principal
            SetMainMenuUIVisible(false);

            // 2. Garante que o carrossel da telinha está atualizado
            if (uiPresenter != null && availableCharacters != null && availableCharacters.Count > 0)
            {
                uiPresenter.Initialize(availableCharacters);
            }

            // 3. Move a câmera até o anchor da telinha na cabine
            if (characterSelectCameraAnchor != null)
            {
                MoveCameraTo(
                    characterSelectCameraAnchor.position,
                    characterSelectCameraAnchor.rotation,
                    _menuCameraFov,
                    cameraTravelDuration,
                    onComplete: () =>
                    {
                        SetContext(MenuInteractionContext.CharacterSelect);
                        SetPhysicalButtonsInteractable(true);
                    });
            }
            else
            {
                SetContext(MenuInteractionContext.CharacterSelect);
                SetPhysicalButtonsInteractable(true);
            }
        }

        /// <summary>
        /// Chamado ao clicar no botão físico 3D "Voltar/Cancelar" ou pressionar ESC.
        /// Transiciona de volta para MainMenuOverview: retorna a câmera e reativa a UI 2D do menu.
        /// </summary>
        public void ExitCharacterSelect()
        {
            if (CurrentContext != MenuInteractionContext.CharacterSelect) return;

            SetContext(MenuInteractionContext.Transitioning);
            SetPhysicalButtonsInteractable(false);

            // Retorna a câmera para a posição original do menu
            MoveCameraTo(_menuCameraPosition, _menuCameraRotation, _menuCameraFov, cameraTravelDuration,
                onComplete: () =>
                {
                    // Reativa a UI 2D do Menu Principal após a câmera concluir o movimento
                    SetMainMenuUIVisible(true);
                    SetContext(MenuInteractionContext.MainMenuOverview);
                });
        }

        /// <summary>
        /// Chamado ao clicar no botão físico 3D "Confirmar" ou pressionar Enter.
        /// Salva o personagem escolhido e carrega a cena de gameplay.
        /// </summary>
        public void ConfirmSelection()
        {
            if (CurrentContext != MenuInteractionContext.CharacterSelect) return;
            if (uiPresenter == null) return;

            var selected = uiPresenter.CurrentCharacter;
            if (selected != null)
            {
                CharacterSelectionPersistence.Save(selected.id);
                Debug.Log($"[MenuCharacterSelectCoordinator] Personagem selecionado: {selected.displayName} ({selected.id})");
            }

            if (!string.IsNullOrEmpty(gameplaySceneName))
            {
                SceneManager.LoadScene(gameplaySceneName);
            }
        }

        /// <summary>Navega para o personagem anterior (slot de cima).</summary>
        public void NavigateUp()
        {
            if (CurrentContext != MenuInteractionContext.CharacterSelect || uiPresenter == null) return;
            uiPresenter.NavigateUp();
        }

        /// <summary>Navega para o próximo personagem (slot de baixo).</summary>
        public void NavigateDown()
        {
            if (CurrentContext != MenuInteractionContext.CharacterSelect || uiPresenter == null) return;
            uiPresenter.NavigateDown();
        }

        private void SetMainMenuUIVisible(bool visible)
        {
            if (mainMenuPresenter != null)
            {
                mainMenuPresenter.SetMenuVisible(visible);
            }

            foreach (var go in menuUIObjects)
            {
                if (go != null) go.SetActive(visible);
            }
        }

        // ── Movimentação Suave de Câmera ────────────────────────────

        private void MoveCameraTo(Vector3 targetPos, Quaternion targetRot, float targetFov, float duration, Action onComplete = null)
        {
            if (_cameraMoveCoroutine != null)
            {
                StopCoroutine(_cameraMoveCoroutine);
            }

            _cameraMoveCoroutine = StartCoroutine(CameraTransitionRoutine(targetPos, targetRot, targetFov, duration, onComplete));
        }

        private IEnumerator CameraTransitionRoutine(Vector3 targetPos, Quaternion targetRot, float targetFov, float duration, Action onComplete)
        {
            if (_camera == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            Transform camTransform = _camera.transform;
            Vector3 startPos = camTransform.position;
            Quaternion startRot = camTransform.rotation;
            float startFov = _camera.fieldOfView;

            float elapsed = 0f;
            duration = Mathf.Max(duration, 0.01f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curveT = cameraCurve != null ? cameraCurve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);

                camTransform.position = Vector3.Lerp(startPos, targetPos, curveT);
                camTransform.rotation = Quaternion.Slerp(startRot, targetRot, curveT);
                _camera.fieldOfView = Mathf.Lerp(startFov, targetFov, curveT);

                yield return null;
            }

            camTransform.position = targetPos;
            camTransform.rotation = targetRot;
            _camera.fieldOfView = targetFov;
            _cameraMoveCoroutine = null;

            onComplete?.Invoke();
        }
    }
}
