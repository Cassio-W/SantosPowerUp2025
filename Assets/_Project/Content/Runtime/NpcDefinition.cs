using System;
using UnityEngine;

namespace Mandato.Content
{
    /// <summary>
    /// Estilo de abordagem utilizado nos minigames de conversa (ex: Festa Corporativa).
    /// A ordem segue a roda de interação no sentido horário a partir de cima:
    /// Arrogante (↑) → Brincalhão (→) → Persuasivo (↓) → Romântico (←).
    /// </summary>
    public enum ApproachStyle
    {
        Arrogante  = 0,  // cima
        Brincalhao = 1,  // direita
        Persuasivo = 2,  // baixo
        Romantico  = 3   // esquerda
    }

    [CreateAssetMenu(fileName = "NewNPC", menuName = "Mandato/NPC Definition")]
    public class NpcDefinition : ScriptableObject
    {
        [Header("Identidade")]
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string role = string.Empty; // Ex: "Ministro da Fazenda & Mago da Faria Lima"

        [Header("Apresentação Visual")]
        public GameObject prefab; // Modelo 3D instanciado no gabinete
        public Sprite portrait;
        public string visualModelId = string.Empty;

        [Header("Inclinação Ideológica Base")]
        [Range(-10, 10)] public int politicalBiasX = 0; // -10 Esquerda a +10 Direita
        [Range(-10, 10)] public int politicalBiasY = 0; // -10 Liberal a +10 Autoritário

        [Header("Biografia Cômica / Satírica")]
        [TextArea(3, 6)] public string bio = string.Empty;

        [Header("Festa Corporativa — Abordagens")]
        [Tooltip("Abordagem preferida: +2 de relação ao ser usada.")]
        public ApproachStyle favoriteApproach  = ApproachStyle.Persuasivo;
        [Tooltip("Abordagem gostada: +1 de relação ao ser usada.")]
        public ApproachStyle likedApproach     = ApproachStyle.Brincalhao;
        [Tooltip("Abordagem desgostada: -1 de relação ao ser usada.")]
        public ApproachStyle dislikedApproach  = ApproachStyle.Arrogante;
        [Tooltip("Abordagem odiada: -2 de relação ao ser usada.")]
        public ApproachStyle hatedApproach     = ApproachStyle.Romantico;

        /// <summary>
        /// Retorna o delta de relação que este NPC recebe ao ser abordado com o estilo dado.
        /// Tabela: favorita=+2, gostada=+1, desgostada=-1, odiada=-2.
        /// </summary>
        public int GetApproachDelta(ApproachStyle approach)
        {
            if (approach == favoriteApproach)  return  2;
            if (approach == likedApproach)     return  1;
            if (approach == dislikedApproach)  return -1;
            if (approach == hatedApproach)     return -2;
            return 0; // segurança: abordagem não mapeada
        }

        public static NpcDefinition CreateRuntimeInstance(
            string id,
            string displayName,
            string role = "",
            GameObject prefab = null,
            int biasX = 0,
            int biasY = 0,
            string bio = "")
        {
            var npc = CreateInstance<NpcDefinition>();
            npc.id = id;
            npc.displayName = displayName;
            npc.role = role;
            npc.prefab = prefab;
            npc.politicalBiasX = biasX;
            npc.politicalBiasY = biasY;
            npc.bio = bio;
            return npc;
        }
    }
}
