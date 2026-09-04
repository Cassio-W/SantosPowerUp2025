#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Utilitário de Editor para formatar os textos de todas as propostas (Deals) do projeto,
/// aplicando quebra de linha após o ponto final de cada frase.
/// </summary>
public static class DealFormatterEditor
{
    [MenuItem("Tools/Mandato/Formatar Textos de Deals (Quebrar Linha após '.')")]
    public static void FormatAllDealsInProject()
    {
        string[] guids = AssetDatabase.FindAssets("t:Deal");
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Deal deal = AssetDatabase.LoadAssetAtPath<Deal>(path);
            if (deal != null && !string.IsNullOrEmpty(deal.Description))
            {
                string formatted = Deal.FormatSentenceBreaks(deal.Description);
                if (formatted != deal.Description)
                {
                    deal.Description = formatted;
                    EditorUtility.SetDirty(deal);
                    count++;
                }
            }
        }

        if (count > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[DealFormatter] {count} propostas (Deals) foram formatadas com quebra de linha após o ponto.");
        EditorUtility.DisplayDialog("Formatação de Deals", 
            $"{count} propostas foram atualizadas para que cada frase fique em uma nova linha.", "OK");
    }
}
#endif
