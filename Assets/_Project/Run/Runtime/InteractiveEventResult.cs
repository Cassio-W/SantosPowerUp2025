using System;
using System.Collections.Generic;
using Mandato.Core;

namespace Mandato.Run
{
    /// <summary>
    /// Resultado devolvido por um evento interativo ao concluir.
    /// O evento preenche apenas os campos que utilizou — todos são opcionais.
    /// O jogo aplica o resultado e retoma o fluxo normal sem saber o que aconteceu
    /// dentro do evento (princípio de caixa preta).
    /// </summary>
    [Serializable]
    public class InteractiveEventResult
    {
        public string eventId = string.Empty;

        /// <summary>
        /// false = o evento foi abortado ou pulado (sem consequências aplicadas).
        /// </summary>
        public bool wasCompleted = true;

        // Todos os campos abaixo são opcionais.
        // O evento só preenche o que fizer sentido para ele.

        /// <summary>Impactos em atributos. null = sem impacto.</summary>
        public StatBlock statImpacts;

        /// <summary>Perks concedidos pelo evento.</summary>
        public List<string> grantPerkIds = new List<string>();

        /// <summary>Cartas injetadas no baralho pelo evento.</summary>
        public List<string> injectCardIds = new List<string>();

        /// <summary>Cartas removidas do baralho pelo evento.</summary>
        public List<string> removeCardIds = new List<string>();

        /// <summary>Variações de relação com NPCs. chave = npcId, valor = delta.</summary>
        public Dictionary<string, int> npcRelationDeltas = new Dictionary<string, int>();

        /// <summary>ID de evento a agendar para o próximo mês disponível.</summary>
        public string scheduleNextEventId = string.Empty;

        /// <summary>Cue narrativa opcional para a camada de apresentação.</summary>
        public string narrativeCue = string.Empty;
    }
}
