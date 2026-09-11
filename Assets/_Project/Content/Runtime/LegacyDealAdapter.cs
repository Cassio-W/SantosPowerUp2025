using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Mandato.Core;
using UnityEngine;

namespace Mandato.Content
{
    public static class LegacyDealAdapter
    {
        public static CardDefinition ConvertToCardDefinition(ScriptableObject legacyDeal)
        {
            if (legacyDeal == null) return null;

            Type dealType = legacyDeal.GetType();
            var card = ScriptableObject.CreateInstance<CardDefinition>();

            card.id = legacyDeal.name;
            card.title = legacyDeal.name;
            card.description = GetFieldValue<string>(legacyDeal, dealType, "Description", string.Empty);
            card.categoryTag = GetFieldValue<string>(legacyDeal, dealType, "tag", string.Empty);

            bool hasCorruption = GetFieldValue<bool>(legacyDeal, dealType, "hasCorruptionMods", false);

            // NPC (GameObject e ID por nome)
            var npcObj = GetFieldValue<GameObject>(legacyDeal, dealType, "NPC", null);
            if (npcObj != null)
            {
                card.npcId = npcObj.name;
                card.npcPrefab = npcObj;
            }
            card.sourceLegacyAsset = legacyDeal;

            // Left Choice (Aprovar / Aceitar)
            string leftLabel = GetFieldValue<string>(legacyDeal, dealType, "leftAnswer", "Aceitar");
            object leftImpactsObj = GetFieldOrPropertyValue(legacyDeal, dealType, "impactsLeft");
            StatBlock leftImpacts = ExtractStatBlock(leftImpactsObj);

            card.leftChoice = new ChoiceDefinition(string.IsNullOrEmpty(leftLabel) ? "Aceitar" : leftLabel, leftImpacts, hasCorruption);
            card.leftChoice.injectCardIds = ExtractCardNamesFromList(legacyDeal, dealType, "newDealsIfLeft");
            card.leftChoice.grantPerkId = ExtractPerkName(legacyDeal, dealType, "perkIfLeft");

            // Right Choice (Recusar / Rejeitar)
            string rightLabel = GetFieldValue<string>(legacyDeal, dealType, "rightAnswer", "Recusar");
            object rightImpactsObj = GetFieldOrPropertyValue(legacyDeal, dealType, "impactsRight");
            StatBlock rightImpacts = ExtractStatBlock(rightImpactsObj);

            card.rightChoice = new ChoiceDefinition(string.IsNullOrEmpty(rightLabel) ? "Recusar" : rightLabel, rightImpacts, hasCorruption);
            card.rightChoice.injectCardIds = ExtractCardNamesFromList(legacyDeal, dealType, "newDealsIfRight");
            card.rightChoice.grantPerkId = ExtractPerkName(legacyDeal, dealType, "perkIfRight");

            return card;
        }

        private static StatBlock ExtractStatBlock(object impactsObj)
        {
            if (impactsObj == null) return new StatBlock(0, 0, 0, 0, 0);

            Type type = impactsObj.GetType();
            int climate = GetFieldValue<int>(impactsObj, type, "climaticChanges", 0);
            int relations = GetFieldValue<int>(impactsObj, type, "internationalRelations", 0);
            int approval = GetFieldValue<int>(impactsObj, type, "populationalApproval", 0);
            int economy = GetFieldValue<int>(impactsObj, type, "economy", 0);
            int corruption = GetFieldValue<int>(impactsObj, type, "corruption", 0);

            return new StatBlock(climate, relations, approval, economy, corruption);
        }

        private static List<string> ExtractCardNamesFromList(object target, Type targetType, string fieldName)
        {
            var list = new List<string>();
            var rawList = GetFieldOrPropertyValue(target, targetType, fieldName) as IEnumerable;
            if (rawList != null)
            {
                foreach (var item in rawList)
                {
                    if (item is UnityEngine.Object uObj && uObj != null)
                    {
                        list.Add(uObj.name);
                    }
                }
            }
            return list;
        }

        private static string ExtractPerkName(object target, Type targetType, string fieldName)
        {
            var perkObj = GetFieldOrPropertyValue(target, targetType, fieldName);
            if (perkObj is UnityEngine.Object uObj && uObj != null)
            {
                return uObj.name;
            }
            return string.Empty;
        }

        private static T GetFieldValue<T>(object target, Type type, string name, T defaultValue)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                object val = field.GetValue(target);
                if (val is T typedVal) return typedVal;
            }
            return defaultValue;
        }

        private static object GetFieldOrPropertyValue(object target, Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) return field.GetValue(target);

            PropertyInfo prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null) return prop.GetValue(target);

            return null;
        }
    }
}
