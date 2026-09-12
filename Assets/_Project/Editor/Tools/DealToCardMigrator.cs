using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mandato.Content;
using Mandato.Core;
using UnityEditor;
using UnityEngine;

namespace Mandato.Editor
{
    public static class DealToCardMigrator
    {
        private const string SourceFolder = "Assets/SO Deals";
        private const string TargetBaseFolder = "Assets/_Project/Content/Definitions/Cards";

        [MenuItem("Mandato/Migração/Migrar SO Deals para CardDefinitions")]
        public static void MigrateAllDeals()
        {
            EnsureDirectoriesExist();
            CreateDefaultPerks();

            string[] dealGuids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { SourceFolder });
            int migratedCount = 0;
            int skippedCount = 0;

            foreach (string guid in dealGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var dealObj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                if (dealObj == null || dealObj.GetType().Name != "Deal")
                {
                    skippedCount++;
                    continue;
                }

                bool isTutorial = assetPath.IndexOf("Tutorial Deals", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  dealObj.name.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0;

                bool isNonStarting = assetPath.IndexOf("NonStarting Deals", StringComparison.OrdinalIgnoreCase) >= 0;

                string targetSubfolder = isTutorial ? "Tutorial" : (isNonStarting ? "NonStarting" : "Main");
                string targetFolder = $"{TargetBaseFolder}/{targetSubfolder}";

                if (!AssetDatabase.IsValidFolder(targetFolder))
                {
                    AssetDatabase.CreateFolder(TargetBaseFolder, targetSubfolder);
                }

                string targetPath = $"{targetFolder}/{dealObj.name}.asset";

                var existingCard = AssetDatabase.LoadAssetAtPath<CardDefinition>(targetPath);
                bool isNew = existingCard == null;
                var card = isNew ? ScriptableObject.CreateInstance<CardDefinition>() : existingCard;

                PopulateCardFromDeal(card, dealObj, isTutorial);

                if (isNew)
                {
                    AssetDatabase.CreateAsset(card, targetPath);
                }
                else
                {
                    EditorUtility.SetDirty(card);
                }

                migratedCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#00ffaa><b>[DealToCardMigrator] Migração Concluída com Sucesso!</b></color> " +
                      $"Total de {migratedCount} cartas migradas/atualizadas em '{TargetBaseFolder}'. ({skippedCount} ignorados).");
        }

        [MenuItem("Mandato/Migração/Criar Perks Padrão")]
        public static void CreateDefaultPerks()
        {
            string perksFolder = "Assets/_Project/Content/Definitions/Perks";
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Content/Definitions"))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Content", "Definitions");
            }
            if (!AssetDatabase.IsValidFolder(perksFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Content/Definitions", "Perks");
            }

            var perks = new List<(string id, string title, string desc, StatBlock deltas)>
            {
                ("ReservaFlorestal", "Reserva Florestal Protegida", "Garante a preservação de biomas estratégicos e proteção ambiental contínua.", new StatBlock(1, 0, 0, 1, 0)),
                ("Cripto", "Hub de Criptoativos", "Incentivos à economia digital e blockchain aumentam a inovação econômica.", new StatBlock(0, 1, 0, 0, 1)),
                ("AliancaEUA", "Aliança Estratégica com os EUA", "Cooperação comercial e diplomática contínua com a maior economia ocidental.", new StatBlock(0, 1, 1, 0, 0)),
                ("TratadoInternacional", "Tratado Comercial do Oriente", "Abertura de novos mercados bilaterais e fluxos de comércio exterior.", new StatBlock(0, 1, 1, 0, 0)),
                ("InvestimentoUsina", "Complexo Nuclear em Operação", "Geração contínua de energia limpa para a matriz industrial nacional.", new StatBlock(1, 1, 0, 0, 0))
            };

            foreach (var p in perks)
            {
                string path = $"{perksFolder}/{p.id}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<PerkDefinition>(path);
                if (existing == null)
                {
                    var perk = ScriptableObject.CreateInstance<PerkDefinition>();
                    perk.id = p.id;
                    perk.title = p.title;
                    perk.description = p.desc;
                    perk.statDeltasPerMonth = p.deltas;
                    perk.durationMonths = 0;
                    AssetDatabase.CreateAsset(perk, path);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00ffaa>[DealToCardMigrator] Perks padrão criados com sucesso em Assets/_Project/Content/Definitions/Perks!</color>");
        }

        private static void PopulateCardFromDeal(CardDefinition card, ScriptableObject legacyDeal, bool isTutorial)
        {
            Type dealType = legacyDeal.GetType();

            card.id = legacyDeal.name;
            card.title = legacyDeal.name;
            card.description = GetFieldValue<string>(legacyDeal, dealType, "Description", string.Empty);
            card.categoryTag = GetFieldValue<string>(legacyDeal, dealType, "tag", string.Empty);
            card.isTutorial = isTutorial;
            card.baseWeight = 100;

            bool hasCorruption = GetFieldValue<bool>(legacyDeal, dealType, "hasCorruptionMods", false);

            var npcObj = GetFieldValue<GameObject>(legacyDeal, dealType, "NPC", null);
            if (npcObj != null)
            {
                card.npcId = npcObj.name;
                card.npcPrefab = npcObj;
            }
            card.sourceLegacyAsset = legacyDeal;

            // Left Choice
            string leftLabel = GetFieldValue<string>(legacyDeal, dealType, "leftAnswer", "Aceitar");
            object leftImpactsObj = GetFieldOrPropertyValue(legacyDeal, dealType, "impactsLeft");
            StatBlock leftImpacts = ExtractStatBlock(leftImpactsObj);

            card.leftChoice = new ChoiceDefinition(string.IsNullOrEmpty(leftLabel) ? "Aceitar" : leftLabel, leftImpacts, hasCorruption);
            card.leftChoice.injectCardIds = ExtractCardNamesFromList(legacyDeal, dealType, "newDealsIfLeft");
            card.leftChoice.grantPerkId = ExtractPerkName(legacyDeal, dealType, "perkIfLeft");

            // Right Choice
            string rightLabel = GetFieldValue<string>(legacyDeal, dealType, "rightAnswer", "Recusar");
            object rightImpactsObj = GetFieldOrPropertyValue(legacyDeal, dealType, "impactsRight");
            StatBlock rightImpacts = ExtractStatBlock(rightImpactsObj);

            card.rightChoice = new ChoiceDefinition(string.IsNullOrEmpty(rightLabel) ? "Recusar" : rightLabel, rightImpacts, hasCorruption);
            card.rightChoice.injectCardIds = ExtractCardNamesFromList(legacyDeal, dealType, "newDealsIfRight");
            card.rightChoice.grantPerkId = ExtractPerkName(legacyDeal, dealType, "perkIfRight");
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

        private static void EnsureDirectoriesExist()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Content/Definitions"))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Content", "Definitions");
            }

            if (!AssetDatabase.IsValidFolder(TargetBaseFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Content/Definitions", "Cards");
            }
        }
    }
}
