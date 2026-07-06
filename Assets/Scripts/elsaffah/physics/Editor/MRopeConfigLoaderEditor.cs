// #if UNITY_EDITOR
// using UnityEditor;
// using UnityEngine;

// namespace MyPhysics
// {
//     [CustomEditor(typeof(MRopeConfigLoader))]
//     public sealed class MRopeConfigLoaderEditor : UnityEditor.Editor
//     {
//         public override void OnInspectorGUI()
//         {
//             serializedObject.Update();
//             DrawPropertiesExcluding(serializedObject, "m_Script");
//             serializedObject.ApplyModifiedProperties();

//             var loader = (MRopeConfigLoader)target;

//             EditorGUILayout.Space(6);

//             // عرض قائمة بأسماء الملفات
//             if (loader.Configs != null && loader.Configs.Count > 0)
//             {
//                 string[] names = new string[loader.Configs.Count];
//                 for (int i = 0; i < loader.Configs.Count; i++)
//                 {
//                     names[i] = loader.Configs[i] != null 
//                         ? loader.Configs[i].name 
//                         : $"فارغ {i}";
//                 }

//                 EditorGUI.BeginChangeCheck();
//                 int newSel = EditorGUILayout.Popup(
//                     "Current Experiment", 
//                     Mathf.Clamp(loader.Selected, 0, names.Length - 1), 
//                     names
//                 );

//                 if (EditorGUI.EndChangeCheck())
//                 {
//                     Undo.RecordObject(loader, "Select Config");
//                     loader.Selected = newSel;
//                     EditorUtility.SetDirty(loader);

//                     if (Application.isPlaying)
//                     {
//                         loader.ApplyConfig(newSel);
//                     }
//                 }

//                 using (new EditorGUI.DisabledScope(!Application.isPlaying))
//                 {
//                     if (GUILayout.Button("Apply Now"))
//                         loader.ApplyConfig(loader.Selected);
//                 }
//             }
//             else
//             {
//                 EditorGUILayout.HelpBox(
//                     "لا توجد ملفات إعدادات. أضف TextAssets إلى القائمة.", 
//                     MessageType.Warning
//                 );
//             }

//             if (!Application.isPlaying)
//             {
//                 EditorGUILayout.HelpBox(
//                     "اختر تجربة ثم اضغط Play لتطبيقها.", 
//                     MessageType.None
//                 );
//             }
//         }
//     }
// }
// #endif