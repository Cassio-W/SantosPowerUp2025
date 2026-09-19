using System;
using System.Collections.Generic;
using Mandato.Core;
using UnityEngine;

namespace Mandato.Content
{
    [Serializable]
    public struct NpcRelationOverride
    {
        public string npcId;
        public int initialRelation;

        public NpcRelationOverride(string npcId, int initialRelation)
        {
            this.npcId = npcId;
            this.initialRelation = initialRelation;
        }
    }

    [CreateAssetMenu(fileName = "NewCharacter", menuName = "Mandato/Character Definition")]
    public class CharacterDefinition : ScriptableObject
    {
        [Header("Identidade & Apresentação")]
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string title = string.Empty; // ex: "Economista", "Líder Comunitário", "Diplomata"
        [TextArea(3, 6)] public string biography = string.Empty;
        public Sprite portrait;
        public GameObject modelPrefab; // Modelo 3D para ser instanciado no gabinete

        [Header("Atributos Iniciais")]
        public bool overrideInitialStats = false;
        public StatBlock initialStats = new StatBlock(50, 50, 50, 50, 0);

        [Header("Eixo Político Inicial")]
        public bool overridePoliticalAxis = false;
        [Range(PoliticalAxis.MinValue, PoliticalAxis.MaxValue)]
        public int initialPoliticalX = PoliticalAxis.Center;
        [Range(PoliticalAxis.MinValue, PoliticalAxis.MaxValue)]
        public int initialPoliticalY = PoliticalAxis.Center;
        public bool lockPoliticalAxis = false;

        [Header("Perks Iniciais e Restrições")]
        public List<string> startingPerkIds = new List<string>();
        public List<string> blockedPerkIds = new List<string>();

        [Header("Ações do Flip-Phone")]
        public List<string> extraUnlockedActionIds = new List<string>();
        public List<string> lockedActionIds = new List<string>();

        [Header("Relacionamentos Iniciais com NPCs")]
        public List<NpcRelationOverride> initialNpcRelations = new List<NpcRelationOverride>();

        [Header("Habilidades Únicas (Aberto para scripts/extensões)")]
        public List<string> uniqueAbilityIds = new List<string>();
        [TextArea(2, 4)] public string uniqueAbilityDescription = string.Empty;

        public static CharacterDefinition CreateRuntimeInstance(
            string id,
            string displayName,
            string title = "",
            string biography = "",
            StatBlock stats = null,
            int polX = 0,
            int polY = 0,
            bool lockPol = false)
        {
            var charDef = CreateInstance<CharacterDefinition>();
            charDef.id = id;
            charDef.displayName = displayName;
            charDef.title = title;
            charDef.biography = biography;
            charDef.overrideInitialStats = (stats != null);
            charDef.initialStats = stats != null ? stats.Clone() : new StatBlock(50, 50, 50, 50, 0);
            charDef.overridePoliticalAxis = (polX != 0 || polY != 0 || lockPol);
            charDef.initialPoliticalX = polX;
            charDef.initialPoliticalY = polY;
            charDef.lockPoliticalAxis = lockPol;
            return charDef;
        }
    }
}
