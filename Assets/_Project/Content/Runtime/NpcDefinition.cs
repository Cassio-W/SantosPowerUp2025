using System;
using UnityEngine;

namespace Mandato.Content
{
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
