using System;
using System.Collections.Generic;

namespace Mandato.UI
{
    /// <summary>
    /// Coordenador central de modais e foco da interface do MANDATO.
    /// Gerencia a sobreposição de telas (Flip-Phone, EndScreen, Tooltips) e garante
    /// que atalhos de teclado e cliques de fundo sejam bloqueados quando um modal estiver aberto.
    /// </summary>
    public class UIModalCoordinator
    {
        public const string MODAL_FLIP_PHONE = "FlipPhone";
        public const string MODAL_END_SCREEN = "EndScreen";
        public const string MODAL_PERK_TOOLTIP = "PerkTooltip";
        public const string MODAL_CREDITS = "Credits";

        private readonly HashSet<string> openModals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public event Action<string, bool> OnModalStateChanged;

        public bool IsAnyModalOpen => openModals.Count > 0;
        public int OpenModalCount => openModals.Count;

        public bool IsModalOpen(string modalId)
        {
            if (string.IsNullOrEmpty(modalId)) return false;
            return openModals.Contains(modalId);
        }

        public void SetModalState(string modalId, bool isOpen)
        {
            if (string.IsNullOrEmpty(modalId)) return;

            bool changed = false;
            if (isOpen)
            {
                changed = openModals.Add(modalId);
            }
            else
            {
                changed = openModals.Remove(modalId);
            }

            if (changed)
            {
                OnModalStateChanged?.Invoke(modalId, isOpen);
            }
        }

        public void CloseAllModals()
        {
            if (openModals.Count == 0) return;

            var list = new List<string>(openModals);
            openModals.Clear();

            foreach (var modal in list)
            {
                OnModalStateChanged?.Invoke(modal, false);
            }
        }

        /// <summary>
        /// Determina se os atalhos de decisão da proposta (A, D, Espaço, 1, 2, Setas) podem ser processados.
        /// Retorna false se qualquer modal com bloqueio de input (Celular, Fim de Jogo, etc.) estiver aberto.
        /// </summary>
        public bool CanProcessDecisionShortcuts()
        {
            if (IsModalOpen(MODAL_FLIP_PHONE)) return false;
            if (IsModalOpen(MODAL_END_SCREEN)) return false;
            if (IsModalOpen(MODAL_CREDITS)) return false;

            return true;
        }

        /// <summary>
        /// Determina se a tecla de chamar próximo visitante (Espaço) pode ser processada.
        /// </summary>
        public bool CanCallNextVisitor()
        {
            return CanProcessDecisionShortcuts();
        }
    }
}
