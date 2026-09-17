using System;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;
using Mandato.UI;
using UnityEngine;

namespace Mandato.Infrastructure
{
    /// <summary>
    /// Gerenciador dedicado do fluxo de Tutorial do MANDATO.
    /// Responsável por interceptar propostas de tutorial, exibi-las no NpcSpeechBubblePresenter
    /// (desativando a animação de papel físico e o DecisionOverlay padrão) e gerenciar
    /// a progressão e extensões futuras do tutorial.
    /// </summary>
    public class TutorialManager
    {
        private RunStateMachine stateMachine;
        private RunCatalog catalog;
        private NpcSpeechBubblePresenter speechBubble;
        private PaperDocumentPresenter paperPresenter;
        private DecisionOverlayPresenter decisionOverlay;

        private int currentStepIndex = 0;
        private bool isTutorialActive = false;
        private string defaultTutorialSpeaker = "GUIA DO MANDATO";

        public bool IsTutorialActive => isTutorialActive;
        public int CurrentStepIndex => currentStepIndex;
        public NpcSpeechBubblePresenter SpeechBubble => speechBubble;

        public event Action OnTutorialStarted;
        public event Action<CardDefinition, int> OnTutorialStepStarted;
        public event Action<int> OnTutorialStepCompleted;
        public event Action OnTutorialFinished;

        public void Initialize(
            RunStateMachine stateMachine,
            RunCatalog catalog,
            NpcSpeechBubblePresenter speechBubble,
            PaperDocumentPresenter paperPresenter = null,
            DecisionOverlayPresenter decisionOverlay = null)
        {
            this.stateMachine = stateMachine;
            this.catalog = catalog;
            this.speechBubble = speechBubble;
            this.paperPresenter = paperPresenter;
            this.decisionOverlay = decisionOverlay;
            this.currentStepIndex = 0;
            this.isTutorialActive = false;

            if (this.speechBubble != null)
            {
                this.speechBubble.OnSpeechCompleted -= HandleSpeechCompleted;
                this.speechBubble.OnSpeechCompleted += HandleSpeechCompleted;
            }
        }

        public void SetDefaultTutorialSpeaker(string speakerName)
        {
            defaultTutorialSpeaker = speakerName;
        }

        /// <summary>
        /// Verifica se a proposta é uma carta de tutorial.
        /// </summary>
        public bool IsTutorialCard(CardDefinition card)
        {
            if (card == null) return false;
            if (card.isTutorial) return true;

            if (catalog != null && catalog.IsTutorialCardId(card.id))
            {
                return true;
            }

            return card.categoryTag.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   card.title.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   card.id.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Verifica se ainda há cartas de tutorial na fila de compras prioritárias.
        /// </summary>
        public bool HasRemainingTutorialCards()
        {
            if (stateMachine == null || stateMachine.DeckState == null) return false;

            if (stateMachine.DeckState.priorityDrawPile != null)
            {
                foreach (var cardId in stateMachine.DeckState.priorityDrawPile)
                {
                    if (catalog != null && catalog.IsTutorialCardId(cardId)) return true;
                    if (catalog != null && catalog.Cards.TryGetValue(cardId, out var c) && IsTutorialCard(c)) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Apresenta o passo do tutorial usando o NpcSpeechBubblePresenter e suprime
        /// a animação de papel físico e os botões do DecisionUI.
        /// </summary>
        public void PresentTutorialStep(CardDefinition card)
        {
            if (card == null) return;

            if (!isTutorialActive)
            {
                isTutorialActive = true;
                OnTutorialStarted?.Invoke();
            }

            // 1. Suprime papel 3D e botões de decisão
            if (paperPresenter != null)
            {
                paperPresenter.SetPaperActive(false);
                paperPresenter.SetPaperInteractable(false);
            }

            if (decisionOverlay != null)
            {
                decisionOverlay.ClearChoices();
                decisionOverlay.SetVisible(false);
            }

            // 2. Extrai interlocutor e texto da carta de tutorial
            string speaker = !string.IsNullOrEmpty(card.title) ? card.title : defaultTutorialSpeaker;
            string dialogue = !string.IsNullOrEmpty(card.FormattedDescription) ? card.FormattedDescription : card.description;

            // 3. Exibe o balão de fala no rodapé da tela
            if (speechBubble != null)
            {
                speechBubble.Show(speaker, dialogue, useTypewriter: false);
            }

            OnTutorialStepStarted?.Invoke(card, currentStepIndex);
        }

        /// <summary>
        /// Trata a conclusão do balão de fala (clique em Avançar ou tecla de atalho).
        /// </summary>
        private void HandleSpeechCompleted()
        {
            if (!isTutorialActive) return;

            int completedStep = currentStepIndex;
            currentStepIndex++;
            OnTutorialStepCompleted?.Invoke(completedStep);

            // Submete a escolha de avanço (índice 0) na máquina de estados
            if (stateMachine != null)
            {
                stateMachine.SubmitChoice(0, catalog?.Quests, catalog?.Perks);
            }
        }

        /// <summary>
        /// Finaliza a sessão de tutorial e restaura a interface para as propostas regulares.
        /// </summary>
        public void CompleteTutorial()
        {
            if (!isTutorialActive) return;

            isTutorialActive = false;

            if (speechBubble != null)
            {
                speechBubble.Hide();
            }

            if (decisionOverlay != null)
            {
                decisionOverlay.SetVisible(true);
            }

            OnTutorialFinished?.Invoke();
        }

        /// <summary>
        /// Força o cancelamento ou pulo do tutorial.
        /// </summary>
        public void AbortTutorial()
        {
            CompleteTutorial();
        }
    }
}
