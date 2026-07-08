// using System;
// using System.Collections.Generic;
// using UnityEngine;

// namespace MyPhysics
// {
//     [DefaultExecutionOrder(-50)]
//     public sealed class MRopeConfigLoader : MonoBehaviour
//     {
//         [Header("Targets")]
//         [SerializeField] private MooringLinePBD_Spherical simulation; // ← تم التصحيح

//         [Tooltip("اسحب ملفات التجارب (JSON كـ TextAsset) هنا.")]
//         [SerializeField] private List<TextAsset> configs = new();

//         [Tooltip("رقم التجربة المختارة (تضبطه القائمة المنسدلة في الـ Inspector).")]
//         [SerializeField] private int selected;

//         private int applied = -1;

//         // ===== Properties للـ Editor =====
//         public List<TextAsset> Configs => configs;
//         public int Selected 
//         { 
//             get => selected; 
//             set => selected = value; 
//         }

//         // ===== الدوال المطلوبة من الـ Editor =====
//         public string[] GetConfigNames()
//         {
//             if (configs == null || configs.Count == 0) 
//                 return Array.Empty<string>();

//             var names = new string[configs.Count];
//             for (int i = 0; i < configs.Count; i++)
//             {
//                 if (!configs[i]) 
//                 { 
//                     names[i] = $"{i}: (فارغ)"; 
//                     continue; 
//                 }

//                 string label = configs[i].name;
//                 try
//                 {
//                     MRopeConfig c = JsonUtility.FromJson<MRopeConfig>(configs[i].text);
//                     if (c != null && !string.IsNullOrEmpty(c.name)) 
//                         label = c.name;
//                 }
//                 catch { /* استخدم اسم الملف */ }

//                 names[i] = $"{i + 1}. {label}";
//             }
//             return names;
//         }

//         // ===== التطبيق =====
//         public void Apply(int index)
//         {
//             if (configs == null || configs.Count == 0) return;
//             index = Mathf.Clamp(index, 0, configs.Count - 1);

//             if (!configs[index]) 
//             { 
//                 Debug.LogWarning($"[ConfigLoader] الخانة {index} فارغة."); 
//                 return; 
//             }

//             MRopeConfig c;
//             try 
//             { 
//                 c = JsonUtility.FromJson<MRopeConfig>(configs[index].text); 
//             }
//             catch (Exception e) 
//             { 
//                 Debug.LogError($"[ConfigLoader] فشل قراءة {configs[index].name}: {e.Message}"); 
//                 return; 
//             }

//             if (c == null) return;

//             if (simulation) 
//                 simulation.ApplyConfig(c); // ← دالة ApplyConfig موجودة في MooringLinePBD_Spherical

//             selected = index;
//             applied = index;
//             Debug.Log($"[ConfigLoader] طُبّقت التجربة {index + 1}/{configs.Count}: {c.name}");
//         }

//         // ===== Wrapper للـ Editor =====
//         public void ApplyConfig(int index) => Apply(index);

//         public void ApplySelectedIfChanged()
//         {
//             if (Application.isPlaying && selected != applied) 
//                 Apply(selected);
//         }

//         // ===== بدء التشغيل =====
//         private void Start() => Apply(selected);
//     }
// }