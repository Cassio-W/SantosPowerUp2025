using System;
using Mandato.Content;
using Mandato.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mandato.UI
{
    public class DecisionOverlayPresenter : MonoBehaviour
    {
        [Header("Configuração de Atalhos")]
        public bool enableKeyboardShortcuts = true;

        public event Action<int> OnChoiceSelected;

        private UIDocument uiDocument;
        private Button btnLeft;
        private Button btnRight;
        private Label lblLeftChoice;
        private Label lblRightChoice;

        private bool isChoicePending = false;

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

            VisualElement root = uiDocument.rootVisualElement;

            btnLeft = root.Q<Button>("btn-left") ?? root.Q<Button>("btn-reject");
            btnRight = root.Q<Button>("btn-right") ?? root.Q<Button>("btn-approve");

            lblLeftChoice = root.Q<Label>("label-left-choice") ?? root.Q<Label>("txt-left");
            lblRightChoice = root.Q<Label>("label-right-choice") ?? root.Q<Label>("txt-right");

            if (btnLeft != null)
            {
                btnLeft.clicked -= OnLeftClicked;
                btnLeft.clicked += OnLeftClicked;
            }

            if (btnRight != null)
            {
                btnRight.clicked -= OnRightClicked;
                btnRight.clicked += OnRightClicked;
            }
        }

        public void PresentChoices(CardDefinition card)
        {
            CacheElements();
            isChoicePending = true;

            if (card != null)
            {
                if (lblLeftChoice != null) lblLeftChoice.text = card.leftChoice?.label ?? "Rejeitar";
                if (lblRightChoice != null) lblRightChoice.text = card.rightChoice?.label ?? "Aprovar";
            }
        }

        public void ClearChoices()
        {
            isChoicePending = false;
        }

        private void OnLeftClicked()
        {
            if (!isChoicePending) return;
            isChoicePending = false;
            OnChoiceSelected?.Invoke(0);
        }

        private void OnRightClicked()
        {
            if (!isChoicePending) return;
            isChoicePending = false;
            OnChoiceSelected?.Invoke(1);
        }

        private void Update()
        {
            if (!enableKeyboardShortcuts || !isChoicePending) return;

            // Opção Esquerda (0)
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Alpha1))
            {
                OnLeftClicked();
            }
            // Opção Direita (1)
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Alpha2))
            {
                OnRightClicked();
            }
        }

        private void OnDestroy()
        {
            if (btnLeft != null) btnLeft.clicked -= OnLeftClicked;
            if (btnRight != null) btnRight.clicked -= OnRightClicked;
        }
    }
}
