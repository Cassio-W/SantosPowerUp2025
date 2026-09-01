#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FocusableObject))]
[CanEditMultipleObjects]
public class FocusableObjectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        FocusableObject targetObj = (FocusableObject)target;

        // Desenha campos padrao do Inspector
        DrawDefaultInspector();

        if (targets.Length > 1) return;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Ferramentas de Camera e Foco", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (targetObj.CameraFocusPoint == null)
            {
                EditorGUILayout.HelpBox("Nenhum 'Camera Focus Point' atribuido. A camera usara o offset fallback automatico.", MessageType.Info);

                if (GUILayout.Button("Criar Ponto de Foco da Camera", GUILayout.Height(30)))
                {
                    CreateFocusPoint(targetObj);
                }
            }
            else
            {
                EditorGUILayout.HelpBox($"Ponto de Foco: '{targetObj.CameraFocusPoint.name}'", MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Alinhar a Visao da Cena", GUILayout.Height(28)))
                    {
                        AlignFocusPointToSceneView(targetObj);
                    }

                    if (GUILayout.Button("Ver Visao de Foco", GUILayout.Height(28)))
                    {
                        AlignSceneViewToFocusPoint(targetObj);
                    }
                }

                if (GUILayout.Button("Reposicionar na Frente do Objeto", GUILayout.Height(24)))
                {
                    ResetFocusPointPosition(targetObj);
                }
            }
        }
    }

    private void CreateFocusPoint(FocusableObject obj)
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        GameObject focusPointGo = new GameObject($"{obj.gameObject.name}_FocusPoint");
        Undo.RegisterCreatedObjectUndo(focusPointGo, "Create Camera Focus Point");

        focusPointGo.transform.SetParent(obj.transform, false);
        focusPointGo.transform.localPosition = obj.FallbackFocusOffset;

        Vector3 lookDir = (obj.transform.position - focusPointGo.transform.position).normalized;
        if (lookDir != Vector3.zero)
        {
            focusPointGo.transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
        }

        Undo.RecordObject(obj, "Assign Camera Focus Point");
        obj.CameraFocusPoint = focusPointGo.transform;

        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(obj);
        Selection.activeGameObject = focusPointGo;
    }

    private void AlignFocusPointToSceneView(FocusableObject obj)
    {
        if (obj.CameraFocusPoint == null) return;
        if (SceneView.lastActiveSceneView == null) return;

        Camera sceneCam = SceneView.lastActiveSceneView.camera;
        if (sceneCam == null) return;

        Undo.RecordObject(obj.CameraFocusPoint, "Align Focus Point to Scene View");
        obj.CameraFocusPoint.position = sceneCam.transform.position;
        obj.CameraFocusPoint.rotation = sceneCam.transform.rotation;
        EditorUtility.SetDirty(obj.CameraFocusPoint);
        Debug.Log($"[FocusableObject] '{obj.CameraFocusPoint.name}' alinhado com a visao da Scene View!");
    }

    private void AlignSceneViewToFocusPoint(FocusableObject obj)
    {
        if (obj.CameraFocusPoint == null) return;
        if (SceneView.lastActiveSceneView == null) return;

        SceneView sv = SceneView.lastActiveSceneView;
        sv.AlignViewToObject(obj.CameraFocusPoint);
        sv.Repaint();
    }

    private void ResetFocusPointPosition(FocusableObject obj)
    {
        if (obj.CameraFocusPoint == null) return;

        Undo.RecordObject(obj.CameraFocusPoint, "Reset Focus Point Position");
        obj.CameraFocusPoint.localPosition = obj.FallbackFocusOffset;
        Vector3 lookDir = (obj.transform.position - obj.CameraFocusPoint.position).normalized;
        if (lookDir != Vector3.zero)
        {
            obj.CameraFocusPoint.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
        }
        EditorUtility.SetDirty(obj.CameraFocusPoint);
    }
}
#endif
