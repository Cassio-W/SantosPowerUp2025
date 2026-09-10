using System;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;
using UnityEngine;

namespace Mandato.Presentation
{
    public class RunPresentationCoordinator : MonoBehaviour
    {
        [Header("Apresentadores")]
        public EnvironmentPresentation environment;
        public NpcPresentation activeNpc;

        [Header("Spawn de NPCs")]
        public Transform npcSpawnPoint;
        public List<GameObject> npcPrefabs = new List<GameObject>();

        public event Action<CardDefinition> OnProposalOnDesk;
        public event Action<ResolutionReport> OnConsequencesFinished;

        private RunStateMachine stateMachine;
        private IReadOnlyDictionary<string, CardDefinition> cardCatalog;

        public void Bind(RunStateMachine runStateMachine, IReadOnlyDictionary<string, CardDefinition> catalog)
        {
            stateMachine = runStateMachine;
            cardCatalog = catalog;

            if (stateMachine != null)
            {
                stateMachine.OnProposalReady += PresentProposal;
                stateMachine.OnConsequencesReady += PresentConsequences;
            }
        }

        public void PresentProposal(CardDefinition card)
        {
            if (card == null) return;

            // Se o NPC estiver em cena, comanda a entrada e entrega
            if (activeNpc != null)
            {
                activeNpc.EnterAndDeliver(() =>
                {
                    OnProposalOnDesk?.Invoke(card);
                });
            }
            else
            {
                // Fallback sem NPC 3D (ex: testes ou cena simplificada)
                OnProposalOnDesk?.Invoke(card);
            }
        }

        public void PresentConsequences(ResolutionReport report)
        {
            if (report == null) return;

            // 1. Toca efeitos de ambiente e sons de cue
            if (environment != null)
            {
                environment.PlayPresentationCue(report.presentationCue);
            }

            // 2. Comanda reação do NPC e saída
            bool isPositive = report.choiceIndex == 1; // 1 = geralmente Aprovar
            if (activeNpc != null)
            {
                activeNpc.ReactAndExit(isPositive, report.presentationCue, () =>
                {
                    OnConsequencesFinished?.Invoke(report);
                    stateMachine?.CompleteTurnAndAdvance();
                });
            }
            else
            {
                OnConsequencesFinished?.Invoke(report);
                stateMachine?.CompleteTurnAndAdvance();
            }
        }

        private void OnDestroy()
        {
            if (stateMachine != null)
            {
                stateMachine.OnProposalReady -= PresentProposal;
                stateMachine.OnConsequencesReady -= PresentConsequences;
            }
        }
    }
}
