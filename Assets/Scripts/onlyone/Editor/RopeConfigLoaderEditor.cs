#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace onlyone.Editor
{ 
    [CustomEditor(typeof(RopeConfigLoader))]
    public sealed class RopeConfigLoaderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        { 
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "selected", "m_Script");
            serializedObject.ApplyModifiedProperties();

            var loader = (RopeConfigLoader)target;
            string[] names = loader.GetConfigNames();

            EditorGUILayout.Space(6);

            EditorGUI.BeginChangeCheck();
            int newSel = EditorGUILayout.Popup("current experiment", Mathf.Clamp(loader.Selected, 0, names.Length - 1), names);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(loader, "Select Rope Config");
                loader.Selected = newSel;
                EditorUtility.SetDirty(loader);
                loader.ApplySelectedIfChanged();   
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            { 
                    loader.Apply(loader.Selected);
            }

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("The selected experiment is applied automatically on Play, " +
                                        "or immediately if you change the selection while playing.", MessageType.None);
        }
    }
}
#endif
