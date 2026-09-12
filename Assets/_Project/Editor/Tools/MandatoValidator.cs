using System;
using System.Collections.Generic;
using Mandato.Content;
using UnityEditor;
using UnityEngine;

namespace Mandato.Editor
{
    public static class MandatoValidator
    {
        [MenuItem("Mandato/Validação/Validar Catálogo e Integridade")]
        public static void ValidateAllContent()
        {
            int totalChecked = 0;
            int warningCount = 0;

            var cardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var perkIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var eventIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var questIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var endingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var actionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Carrega catálogo de Perks
            string[] perkGuids = AssetDatabase.FindAssets("t:PerkDefinition");
            foreach (string guid in perkGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var perk = AssetDatabase.LoadAssetAtPath<PerkDefinition>(path);
                if (perk == null) continue;

                totalChecked++;
                if (string.IsNullOrEmpty(perk.id))
                {
                    Debug.LogWarning($"[MandatoValidator] ⚠️ Perk sem ID em: {path}");
                    warningCount++;
                }
                else if (perkIds.Contains(perk.id))
                {
                    Debug.LogWarning($"[MandatoValidator] ⚠️ ID de Perk duplicado '{perk.id}' em: {path}");
                    warningCount++;
                }
                else
                {
                    perkIds.Add(perk.id);
                }
            }

            // 2. Carrega catálogo de Eventos
            string[] eventGuids = AssetDatabase.FindAssets("t:RunEventDefinition");
            foreach (string guid in eventGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ev = AssetDatabase.LoadAssetAtPath<RunEventDefinition>(path);
                if (ev == null) continue;

                totalChecked++;
                if (string.IsNullOrEmpty(ev.id))
                {
                    Debug.LogWarning($"[MandatoValidator] ⚠️ Evento sem ID em: {path}");
                    warningCount++;
                }
                else if (eventIds.Contains(ev.id))
                {
                    Debug.LogWarning($"[MandatoValidator] ⚠️ ID de Evento duplicado '{ev.id}' em: {path}");
                    warningCount++;
                }
                else
                {
                    eventIds.Add(ev.id);
                }
            }

            // 3. Carrega catálogo de Quests
            string[] questGuids = AssetDatabase.FindAssets("t:QuestDefinition");
            foreach (string guid in questGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
                if (quest == null) continue;

                totalChecked++;
                if (string.IsNullOrEmpty(quest.id))
                {
                    Debug.LogWarning($"[MandatoValidator] ⚠️ Quest sem ID em: {path}");
                    warningCount++;
                }
                else if (questIds.Contains(quest.id))
                {
                    Debug.LogWarning($"[MandatoValidator] ⚠️ ID de Quest duplicado '{quest.id}' em: {path}");
                    warningCount++;
                }
                else
                {
                    questIds.Add(quest.id);
                }
            }

            // 4. Carrega catálogo de Finais
            string[] endingGuids = AssetDatabase.FindAssets("t:EndingDefinition");
            foreach (string guid in endingGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ending = AssetDatabase.LoadAssetAtPath<EndingDefinition>(path);
                if (ending == null) continue;

                totalChecked++;
                if (string.IsNullOrEmpty(ending.id))
                {
                    Debug.LogWarning($"[MandatoValidator] ⚠️ Final sem ID em: {path}");
                    warningCount++;
                }
                else if (endingIds.Contains(ending.id))
                {
                    Debug.LogWarning($"[MandatoValidator] ⚠️ ID de Final duplicado '{ending.id}' em: {path}");
                    warningCount++;
                }
                else
                {
                    endingIds.Add(ending.id);
                }
            }

            // 5. Carrega catálogo de Ações do Flip-Phone
            string[] actionGuids = AssetDatabase.FindAssets("t:FlipPhoneActionDefinition");
            foreach (string guid in actionGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var act = AssetDatabase.LoadAssetAtPath<FlipPhoneActionDefinition>(path);
                if (act == null) continue;

                totalChecked++;
                if (string.IsNullOrEmpty(act.id))
                {
                    Debug.LogWarning($"[MandatoValidator] ⚠️ Ação de celular sem ID em: {path}");
                    warningCount++;
                }
                else if (actionIds.Contains(act.id))
                {
                    Debug.LogWarning($"[MandatoValidator] ⚠️ ID de Ação duplicado '{act.id}' em: {path}");
                    warningCount++;
                }
                else
                {
                    actionIds.Add(act.id);
                }
            }

            // 6. Valida CardDefinition e referências cruzadas
            var allCards = new List<(string path, CardDefinition card)>();
            string[] cardGuids = AssetDatabase.FindAssets("t:CardDefinition");
            foreach (string guid in cardGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardDefinition>(path);
                if (card == null) continue;

                totalChecked++;
                allCards.Add((path, card));

                if (string.IsNullOrEmpty(card.id))
                {
                    Debug.LogWarning($"[MandatoValidator] ⚠️ Carta sem ID em: {path}");
                    warningCount++;
                }
                else if (cardIds.Contains(card.id))
                {
                    Debug.LogWarning($"[MandatoValidator] ⚠️ ID de Carta duplicado '{card.id}' em: {path}");
                    warningCount++;
                }
                else
                {
                    cardIds.Add(card.id);
                }

                if (string.IsNullOrEmpty(card.description))
                {
                    Debug.LogWarning($"[MandatoValidator] ⚠️ Carta '{card.name}' sem descrição em: {path}");
                    warningCount++;
                }
            }

            // 7. Validação de referências cruzadas (injectCardIds e grantPerkId)
            foreach (var (path, card) in allCards)
            {
                ValidateChoiceReferences(card.leftChoice, card.id, "Escolha Esquerda", path, cardIds, perkIds, ref warningCount);
                ValidateChoiceReferences(card.rightChoice, card.id, "Escolha Direita", path, cardIds, perkIds, ref warningCount);

                if (card.conditions != null)
                {
                    foreach (var cond in card.conditions)
                    {
                        if (cond == null) continue;
                        if (!string.IsNullOrEmpty(cond.requiredPerkId) && !perkIds.Contains(cond.requiredPerkId))
                        {
                            Debug.LogWarning($"[MandatoValidator] ⚠️ Carta '{card.id}' requer perk '{cond.requiredPerkId}' não encontrado no catálogo. Path: {path}");
                            warningCount++;
                        }
                        if (!string.IsNullOrEmpty(cond.requiredQuestId) && !questIds.Contains(cond.requiredQuestId))
                        {
                            Debug.LogWarning($"[MandatoValidator] ⚠️ Carta '{card.id}' requer quest '{cond.requiredQuestId}' não encontrada no catálogo. Path: {path}");
                            warningCount++;
                        }
                    }
                }
            }

            if (warningCount == 0)
            {
                Debug.Log($"<color=#00ffaa><b>[MandatoValidator] ✅ Sucesso!</b></color> {totalChecked} assets validados ({cardIds.Count} cartas, {perkIds.Count} perks, {eventIds.Count} eventos, {questIds.Count} quests, {endingIds.Count} finais, {actionIds.Count} ações). Nenhum erro encontrado.");
            }
            else
            {
                Debug.LogWarning($"<b>[MandatoValidator] ⚠️ Concluído com avisos:</b> {totalChecked} assets verificados, {warningCount} problemas encontrados.");
            }
        }

        private static void ValidateChoiceReferences(
            ChoiceDefinition choice,
            string cardId,
            string choiceName,
            string path,
            HashSet<string> cardIds,
            HashSet<string> perkIds,
            ref int warningCount)
        {
            if (choice == null) return;

            if (choice.injectCardIds != null)
            {
                foreach (var injectId in choice.injectCardIds)
                {
                    if (!string.IsNullOrEmpty(injectId) && !cardIds.Contains(injectId))
                    {
                        Debug.LogWarning($"[MandatoValidator] ⚠️ Carta '{cardId}' ({choiceName}) injeta carta '{injectId}' não encontrada no catálogo. Path: {path}");
                        warningCount++;
                    }
                }
            }

            if (!string.IsNullOrEmpty(choice.grantPerkId) && !perkIds.Contains(choice.grantPerkId))
            {
                // Apenas aviso informativo, pois o perk pode ser dinâmico ou adicionado via SO
                Debug.LogWarning($"[MandatoValidator] ⚠️ Carta '{cardId}' ({choiceName}) concede perk '{choice.grantPerkId}' não registrado no catálogo de Perks. Path: {path}");
                warningCount++;
            }
        }
    }
}
