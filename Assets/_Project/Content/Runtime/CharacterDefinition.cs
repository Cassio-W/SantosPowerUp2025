using System;
using System.Collections.Generic;
using Mandato.Core;
using UnityEngine;

namespace Mandato.Content
{
    [Serializable]
    public struct NpcRelationOverride
    {
        [Tooltip("Arraste a NpcDefinition diretamente.")]
        public NpcDefinition npc;
        [Tooltip("ID textual do NPC (usado como fallback).")]
        public string npcId;
        public int initialRelation;

        public NpcRelationOverride(string npcId, int initialRelation)
        {
            this.npc = null;
            this.npcId = npcId ?? string.Empty;
            this.initialRelation = initialRelation;
        }

        public NpcRelationOverride(NpcDefinition npc, int initialRelation)
        {
            this.npc = npc;
            this.npcId = npc != null ? (!string.IsNullOrEmpty(npc.id) ? npc.id : npc.name) : string.Empty;
            this.initialRelation = initialRelation;
        }

        public string GetNpcId()
        {
            if (npc != null)
                return !string.IsNullOrEmpty(npc.id) ? npc.id : npc.name;
            return npcId ?? string.Empty;
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
        [Tooltip("Arraste os ScriptableObjects de Perks iniciais.")]
        public List<PerkDefinition> startingPerks = new List<PerkDefinition>();
        [Tooltip("IDs em texto dos Perks iniciais (fallback).")]
        public List<string> startingPerkIds = new List<string>();

        [Tooltip("Arraste os ScriptableObjects de Perks bloqueados para este personagem.")]
        public List<PerkDefinition> blockedPerks = new List<PerkDefinition>();
        [Tooltip("IDs em texto dos Perks bloqueados (fallback).")]
        public List<string> blockedPerkIds = new List<string>();

        [Header("Ações do Flip-Phone")]
        [Tooltip("Arraste as ações do celular desbloqueadas para este personagem.")]
        public List<FlipPhoneActionDefinition> extraUnlockedActions = new List<FlipPhoneActionDefinition>();
        [Tooltip("IDs em texto das ações desbloqueadas (fallback).")]
        public List<string> extraUnlockedActionIds = new List<string>();

        [Tooltip("Arraste as ações do celular bloqueadas para este personagem.")]
        public List<FlipPhoneActionDefinition> lockedActions = new List<FlipPhoneActionDefinition>();
        [Tooltip("IDs em texto das ações bloqueadas (fallback).")]
        public List<string> lockedActionIds = new List<string>();

        [Header("Relacionamentos Iniciais com NPCs")]
        public List<NpcRelationOverride> initialNpcRelations = new List<NpcRelationOverride>();

        [Header("Habilidades Únicas (Aberto para scripts/extensões)")]
        public List<string> uniqueAbilityIds = new List<string>();
        [TextArea(2, 4)] public string uniqueAbilityDescription = string.Empty;

        public IEnumerable<string> GetStartingPerkIds()
        {
            if (startingPerks != null)
            {
                foreach (var p in startingPerks)
                {
                    if (p != null)
                        yield return !string.IsNullOrEmpty(p.id) ? p.id : p.name;
                }
            }
            if (startingPerkIds != null)
            {
                foreach (var id in startingPerkIds)
                {
                    if (!string.IsNullOrEmpty(id))
                        yield return id;
                }
            }
        }

        public HashSet<string> GetBlockedPerkIds()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (blockedPerks != null)
            {
                foreach (var p in blockedPerks)
                {
                    if (p != null)
                        set.Add(!string.IsNullOrEmpty(p.id) ? p.id : p.name);
                }
            }
            if (blockedPerkIds != null)
            {
                foreach (var id in blockedPerkIds)
                {
                    if (!string.IsNullOrEmpty(id))
                        set.Add(id);
                }
            }
            return set;
        }

        public IEnumerable<string> GetExtraUnlockedActionIds()
        {
            if (extraUnlockedActions != null)
            {
                foreach (var a in extraUnlockedActions)
                {
                    if (a != null)
                        yield return !string.IsNullOrEmpty(a.id) ? a.id : a.name;
                }
            }
            if (extraUnlockedActionIds != null)
            {
                foreach (var id in extraUnlockedActionIds)
                {
                    if (!string.IsNullOrEmpty(id))
                        yield return id;
                }
            }
        }

        public IEnumerable<string> GetLockedActionIds()
        {
            if (lockedActions != null)
            {
                foreach (var a in lockedActions)
                {
                    if (a != null)
                        yield return !string.IsNullOrEmpty(a.id) ? a.id : a.name;
                }
            }
            if (lockedActionIds != null)
            {
                foreach (var id in lockedActionIds)
                {
                    if (!string.IsNullOrEmpty(id))
                        yield return id;
                }
            }
        }

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
