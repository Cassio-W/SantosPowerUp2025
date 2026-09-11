#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Mandato.EditorScripts
{
    /// <summary>
    /// Utilitário do Editor para prevenir os erros 'SerializedObjectNotCreatableException'
    /// e 'MissingReferenceException: The variable m_Targets of GameObjectInspector doesn't exist anymore'
    /// limpando seleções órfãs ou nulas ao entrar/sair do Play Mode.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeSelectionCleaner
    {
        static PlayModeSelectionCleaner()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                CleanInvalidSelection();
            }
        }

        private static void CleanInvalidSelection()
        {
            var selection = Selection.objects;
            if (selection == null || selection.Length == 0) return;

            bool hasNullOrMissing = false;
            for (int i = 0; i < selection.Length; i++)
            {
                if (selection[i] == null)
                {
                    hasNullOrMissing = true;
                    break;
                }
            }

            if (hasNullOrMissing)
            {
                Selection.objects = new Object[0];
                Selection.activeObject = null;
            }
        }

        [MenuItem("Tools/Mandato/Resetar Seleção do Inspector")]
        public static void ClearSelectionMenu()
        {
            Selection.objects = new Object[0];
            Selection.activeObject = null;
            GUIUtility.hotControl = 0;
            GUIUtility.keyboardControl = 0;
            Debug.Log("[PlayModeSelectionCleaner] Seleção do Unity Editor limpa com sucesso.");
        }
    }
}
#endif
