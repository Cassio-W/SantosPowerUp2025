using System;
using System.Collections.Generic;

namespace Mandato.UI
{
    /// <summary>
    /// Contextos de interação e modos de visualização exclusivos do jogador na cena.
    /// Define onde a atenção/câmera está focada e quais permissões de input e UI estão ativas.
    /// </summary>
    public enum InteractionContext
    {
        DeskOverview,   // Visão ampla da mesa: NPC na frente, botão de chamar ativo, celular e PC clicáveis.
        PaperInspect,   // Foco no documento da proposta: botões de decisão A/D e botão Voltar ativos.
        PcTerminal,     // Foco no monitor CRT retrô: sistema do PC interativo, decisões e telefone bloqueados.
        PhoneDrawer,    // Flip-Phone aberto em primeiro plano: menu de ações ministeriais ativo, botão de chamar habilitado.
        TutorialStep,   // Diálogo guiado do tutorial em andamento: apenas avanço com Espaço/Enter.
        EndSummary      // Painel de fim de jogo (Vitória ou Derrota): todas as ações bloqueadas.
    }

    /// <summary>
    /// Coordenador central de modais e contextos de interação da interface do MANDATO.
    /// Gerencia os modos de visão (Desk, Papel, PC, Celular, Tutorial, Fim de Jogo) e garante
    /// que atalhos de teclado e elementos de tela respeitem a matriz de permissões do contexto ativo.
    /// </summary>
    public class UIModalCoordinator
    {
        public const string MODAL_FLIP_PHONE = "FlipPhone";
        public const string MODAL_END_SCREEN = "EndScreen";
        public const string MODAL_PERK_TOOLTIP = "PerkTooltip";
        public const string MODAL_CREDITS = "Credits";
        public const string MODAL_PC_FOCUS = "PCFocus";

        private readonly HashSet<string> openModals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public InteractionContext CurrentContext { get; private set; } = InteractionContext.DeskOverview;

        public event Action<InteractionContext, InteractionContext> OnContextChanged;
        public event Action<string, bool> OnModalStateChanged;

        public bool IsAnyModalOpen => openModals.Count > 0 || CurrentContext != InteractionContext.DeskOverview;
        public int OpenModalCount => openModals.Count;

        /// <summary>
        /// Transiciona para um novo contexto de interação na cena, atualizando permissões e notificando ouvintes.
        /// </summary>
        public void SetContext(InteractionContext newContext)
        {
            if (CurrentContext == newContext) return;

            var oldContext = CurrentContext;
            CurrentContext = newContext;

            // Sincroniza flags modais legadas para compatibilidade transparente
            SyncLegacyModals(newContext, oldContext);

            OnContextChanged?.Invoke(newContext, oldContext);
        }

        public bool IsModalOpen(string modalId)
        {
            if (string.IsNullOrEmpty(modalId)) return false;

            if (modalId.Equals(MODAL_PC_FOCUS, StringComparison.OrdinalIgnoreCase))
                return CurrentContext == InteractionContext.PcTerminal;
            if (modalId.Equals(MODAL_FLIP_PHONE, StringComparison.OrdinalIgnoreCase))
                return CurrentContext == InteractionContext.PhoneDrawer;
            if (modalId.Equals(MODAL_END_SCREEN, StringComparison.OrdinalIgnoreCase))
                return CurrentContext == InteractionContext.EndSummary;

            return openModals.Contains(modalId);
        }

        public void SetModalState(string modalId, bool isOpen)
        {
            if (string.IsNullOrEmpty(modalId)) return;

            bool changed = false;
            if (isOpen)
            {
                changed = openModals.Add(modalId);

                // Mapeia modais principais para o InteractionContext correspondente
                if (modalId.Equals(MODAL_PC_FOCUS, StringComparison.OrdinalIgnoreCase) && CurrentContext != InteractionContext.PcTerminal)
                {
                    SetContext(InteractionContext.PcTerminal);
                }
                else if (modalId.Equals(MODAL_FLIP_PHONE, StringComparison.OrdinalIgnoreCase) && CurrentContext != InteractionContext.PhoneDrawer)
                {
                    SetContext(InteractionContext.PhoneDrawer);
                }
                else if (modalId.Equals(MODAL_END_SCREEN, StringComparison.OrdinalIgnoreCase) && CurrentContext != InteractionContext.EndSummary)
                {
                    SetContext(InteractionContext.EndSummary);
                }
            }
            else
            {
                changed = openModals.Remove(modalId);

                if (modalId.Equals(MODAL_PC_FOCUS, StringComparison.OrdinalIgnoreCase) && CurrentContext == InteractionContext.PcTerminal)
                {
                    SetContext(InteractionContext.DeskOverview);
                }
                else if (modalId.Equals(MODAL_FLIP_PHONE, StringComparison.OrdinalIgnoreCase) && CurrentContext == InteractionContext.PhoneDrawer)
                {
                    SetContext(InteractionContext.DeskOverview);
                }
            }

            if (changed)
            {
                OnModalStateChanged?.Invoke(modalId, isOpen);
            }
        }

        public void CloseAllModals()
        {
            if (openModals.Count > 0)
            {
                var list = new List<string>(openModals);
                openModals.Clear();

                foreach (var modal in list)
                {
                    OnModalStateChanged?.Invoke(modal, false);
                }
            }

            if (CurrentContext != InteractionContext.DeskOverview)
            {
                SetContext(InteractionContext.DeskOverview);
            }
        }

        private void SyncLegacyModals(InteractionContext newContext, InteractionContext oldContext)
        {
            if (oldContext == InteractionContext.PcTerminal) openModals.Remove(MODAL_PC_FOCUS);
            if (oldContext == InteractionContext.PhoneDrawer) openModals.Remove(MODAL_FLIP_PHONE);
            if (oldContext == InteractionContext.EndSummary) openModals.Remove(MODAL_END_SCREEN);

            if (newContext == InteractionContext.PcTerminal) openModals.Add(MODAL_PC_FOCUS);
            if (newContext == InteractionContext.PhoneDrawer) openModals.Add(MODAL_FLIP_PHONE);
            if (newContext == InteractionContext.EndSummary) openModals.Add(MODAL_END_SCREEN);
        }

        /// <summary>
        /// Determina se os atalhos de decisão da proposta (A, D, 1, 2, Setas) podem ser processados.
        /// Permitido apenas em PaperInspect e DeskOverview (quando nenhuma tela bloqueadora estiver ativa).
        /// </summary>
        public bool CanProcessDecisionShortcuts()
        {
            if (CurrentContext == InteractionContext.PcTerminal) return false;
            if (CurrentContext == InteractionContext.PhoneDrawer) return false;
            if (CurrentContext == InteractionContext.TutorialStep) return false;
            if (CurrentContext == InteractionContext.EndSummary) return false;
            if (IsModalOpen(MODAL_CREDITS)) return false;

            return true;
        }

        /// <summary>
        /// Determina se a tecla de chamar próximo visitante (Espaço) pode ser processada.
        /// Permitido em DeskOverview e PhoneDrawer (o celular aberto não impede de chamar visitantes).
        /// </summary>
        public bool CanCallNextVisitor()
        {
            if (CurrentContext == InteractionContext.PcTerminal) return false;
            if (CurrentContext == InteractionContext.PaperInspect) return false;
            if (CurrentContext == InteractionContext.TutorialStep) return false;
            if (CurrentContext == InteractionContext.EndSummary) return false;
            if (IsModalOpen(MODAL_CREDITS)) return false;

            return true;
        }

        /// <summary>
        /// Determina se o Flip-Phone pode ser aberto pelo jogador.
        /// </summary>
        public bool CanOpenFlipPhone()
        {
            if (CurrentContext == InteractionContext.PcTerminal) return false;
            if (CurrentContext == InteractionContext.PaperInspect) return false;
            if (CurrentContext == InteractionContext.TutorialStep) return false;
            if (CurrentContext == InteractionContext.EndSummary) return false;
            if (IsModalOpen(MODAL_CREDITS)) return false;

            return true;
        }

        /// <summary>
        /// Determina se a tecla ESC ou clique fora pode desfocar o objeto atual e retornar à mesa.
        /// </summary>
        public bool CanProcessEscapeUnfocus()
        {
            if (CurrentContext == InteractionContext.DeskOverview) return false;
            if (CurrentContext == InteractionContext.TutorialStep) return false;
            if (CurrentContext == InteractionContext.EndSummary) return false;

            return true;
        }
    }
}
