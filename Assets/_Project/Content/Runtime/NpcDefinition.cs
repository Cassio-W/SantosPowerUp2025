using System;
using System.Collections.Generic;
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
        [Tooltip("Deltas de relação (-2 a +2) para cada abordagem: [0]=Arrogante(↑), [1]=Brincalhão(→), [2]=Persuasivo(↓), [3]=Romântico(←)")]
        [SerializeField] private int[] approachDeltas = new int[4] { -1, 1, 2, -2 };

        public IReadOnlyList<int> ApproachDeltas => approachDeltas;

        /// <summary>
        /// Retorna o delta de relação (-2 a +2) que este NPC recebe ao ser abordado com o estilo dado.
        /// </summary>
        public int GetApproachDelta(ApproachStyle approach)
        {
            int index = (int)approach;
            if (approachDeltas != null && index >= 0 && index < approachDeltas.Length)
            {
                return Mathf.Clamp(approachDeltas[index], -2, 2);
            }
            return 0;
        }

        public void SetApproachDeltas(int arrogante, int brincalhao, int persuasivo, int romantico)
        {
            approachDeltas = new int[4]
            {
                Mathf.Clamp(arrogante, -2, 2),
                Mathf.Clamp(brincalhao, -2, 2),
                Mathf.Clamp(persuasivo, -2, 2),
                Mathf.Clamp(romantico, -2, 2)
            };
        }

        private void OnValidate()
        {
            if (approachDeltas == null || approachDeltas.Length != 4)
            {
                var newDeltas = new int[4] { -1, 1, 2, -2 };
                if (approachDeltas != null)
                {
                    for (int i = 0; i < Mathf.Min(approachDeltas.Length, 4); i++)
                        newDeltas[i] = Mathf.Clamp(approachDeltas[i], -2, 2);
                }
                approachDeltas = newDeltas;
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    approachDeltas[i] = Mathf.Clamp(approachDeltas[i], -2, 2);
                }
            }
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
