using System;
using System.Collections.Generic;
using UnityEngine;

namespace onlyone
{
    [DefaultExecutionOrder(-50)]
    public sealed class RopeConfigLoader : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private SphericalPendulum pendulum;
        [SerializeField] private PbdRope rope;

        [Tooltip("اسحب ملفات التجارب (JSON كـ TextAsset) هنا.")]
        [SerializeField] private List<TextAsset> configs = new();

        [Tooltip("رقم التجربة المختارة (تضبطه القائمة المنسدلة في الـ Inspector).")]
        [SerializeField] private int selected;

        private int applied = -1;
 
        public string[] GetConfigNames()
        {
            if (configs == null || configs.Count == 0) return Array.Empty<string>();
            var names = new string[configs.Count];
            for (int i = 0; i < configs.Count; i++)
            {
                if (!configs[i]) { names[i] = $"{i}: (فارغ)"; continue; }
                string label = configs[i].name;
                
                try
                {
                    RopeConfig c = JsonUtility.FromJson<RopeConfig>(configs[i].text);
                    if (c != null && !string.IsNullOrEmpty(c.name)) label = c.name;
                    // Debug.Log("name is : " + c.name);
                    // Debug.Log($"<color=red>[TEST]</color> Loaded stretchDampingRatio from JSON: {c.stretchDampingRatio}");
                }
                catch { /* أبقِ اسم الملف */ }
                names[i] = $"{i + 1}. {label}";
            }
            return names;
        }

        public int Selected { get => selected; set => selected = value; }

        private void Start() => Apply(selected);
        
        // ReSharper disable Unity.PerformanceAnalysis
        public void Apply(int index)
        {
            if (configs == null || configs.Count == 0) return;
            index = Mathf.Clamp(index, 0, configs.Count - 1);
            if (!configs[index]) { Debug.LogWarning($"[ConfigLoader] الخانة {index} فارغة."); return; }

            RopeConfig c;
            try { c = JsonUtility.FromJson<RopeConfig>(configs[index].text); }
            catch (Exception e) { Debug.LogError($"[ConfigLoader] فشل قراءة {configs[index].name}: {e.Message}"); return; }
            if (c == null) return;

            if (pendulum) pendulum.ApplyConfig(c);
            if (rope)     rope.ApplyConfig(c);

            selected = index;
            applied  = index;
            Debug.Log($"[ConfigLoader] طُبّقت التجربة {index + 1}/{configs.Count}: {c.name}");
        }
 
        public void ApplySelectedIfChanged()
        {
            if (Application.isPlaying && selected != applied) Apply(selected);
        }
    }
}
