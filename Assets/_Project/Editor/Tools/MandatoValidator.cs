using System.Collections.Generic;
using Mandato.Content;
using UnityEditor;
using UnityEngine;

namespace Mandato.Editor
{
    public static class MandatoValidator
    {
        [MenuItem("Mandato/Validar Conteúdo")]
        public static void ValidateAllContent()
        {
            int totalChecked = 0;
            int warningCount = 0;
            var ids = new HashSet<string>();

            // 1. Valida CardDefinition
            string[] cardGuids = AssetDatabase.FindAssets("t:CardDefinition");
            foreach (string guid in cardGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardDefinition>(path);
                if (card == null) continue;

                totalChecked++;

                if (string.IsNullOrEmpty(card.id))
                {
                    Debug.LogWarning($"[MandatoValidator] Carta sem ID no path: {path}");
                    warningCount++;
                }
                else if (ids.Contains(card.id))
                {
                    Debug.LogWarning($"[MandatoValidator] ID duplicado '{card.id}' encontrado em: {path}");
                    warningCount++;
                }
                else
                {
                    ids.Add(card.id);
                }

                if (string.IsNullOrEmpty(card.description))
                {
                    Debug.LogWarning($"[MandatoValidator] Carta '{card.name}' sem descrição em: {path}");
                    warningCount++;
                }
            }

            // 2. Valida Deals antigos
            string[] dealGuids = AssetDatabase.FindAssets("t:Deal");
            foreach (string guid in dealGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var dealObj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (dealObj == null) continue;

                totalChecked++;
                var converted = LegacyDealAdapter.ConvertToCardDefinition(dealObj);
                if (converted == null)
                {
                    Debug.LogWarning($"[MandatoValidator] Falha ao converter Deal legado em: {path}");
                    warningCount++;
                }
            }

            if (warningCount == 0)
            {
                Debug.Log($"<color=#44FF44><b>[MandatoValidator] Sucesso!</b></color> {totalChecked} propostas validadas sem nenhum erro encontrado.");
            }
            else
            {
                Debug.LogWarning($"<b>[MandatoValidator] Concluído com avisos:</b> {totalChecked} propostas verificadas, {warningCount} problemas encontrados.");
            }
        }
    }
}
