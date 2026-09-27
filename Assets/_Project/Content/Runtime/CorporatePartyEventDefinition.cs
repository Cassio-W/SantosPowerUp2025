using System.Collections.Generic;
using UnityEngine;

namespace Mandato.Content
{
    /// <summary>
    /// Configuração estática de uma instância do evento Festa Corporativa.
    /// Este ScriptableObject é imutável em runtime — só define parâmetros.
    /// O estado da festa (interações restantes, quem foi abordado, etc.)
    /// é gerenciado pelo PartyEventController durante a execução.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCorporatePartyEvent", menuName = "Mandato/Events/Corporate Party")]
    public class CorporatePartyEventDefinition : ScriptableObject
    {
        public string eventId = string.Empty;

        [Header("Pool de NPCs Convidados")]
        [Tooltip("Lista de possíveis convidados. A festa sorteia entre minGuests e maxGuests desse pool.")]
        public List<NpcDefinition> guestPool = new List<NpcDefinition>();

        [Min(1)]
        [Tooltip("Mínimo de convidados presentes na festa.")]
        public int minGuests = 3;

        [Min(1)]
        [Tooltip("Máximo de convidados presentes na festa.")]
        public int maxGuests = 6;

        [Header("Limite de Interações (Ânimo)")]
        [Min(1)]
        [Tooltip("Número base de interações que o jogador tem na festa.")]
        public int baseInteractionLimit = 4;

        [Tooltip("ID do Perk que concede interações extras. Deixe vazio para não usar.")]
        public string bonusInteractionPerkId = string.Empty;

        [Min(1)]
        [Tooltip("Quantidade de interações extras concedidas pelo Perk acima.")]
        public int bonusInteractionAmount = 1;

        [Header("Cena Unity")]
        [Tooltip("Nome exato da cena registrada no Build Settings.")]
        public string sceneName = "Event_CorporateParty";

        /// <summary>
        /// Calcula o limite total de interações considerando o perk bônus ativo.
        /// </summary>
        public int GetInteractionLimit(IEnumerable<string> activePerks)
        {
            if (!string.IsNullOrEmpty(bonusInteractionPerkId) && activePerks != null)
            {
                foreach (var p in activePerks)
                {
                    if (string.Equals(p, bonusInteractionPerkId, System.StringComparison.OrdinalIgnoreCase))
                        return baseInteractionLimit + bonusInteractionAmount;
                }
            }
            return baseInteractionLimit;
        }
    }
}
