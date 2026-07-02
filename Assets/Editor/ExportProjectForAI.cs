using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public class ExportProjectForAI
{
    [MenuItem("Tools/Export Project For AI")]
    public static void Export()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;

        string[] folders =
        {
            "Assets/Scripts/Bucket",
            "Assets/Scripts/Fluid Sim 3D",
            
            // "Assets/Scripts/Fluid Sim 3D/cumpute",
            // "Assets/Scripts/Fluid Sim 3D/Data",
            // "Assets/Scripts/Fluid Sim 3D/Display",
            // "Assets/Scripts/Fluid Sim 3D/Systems",
            // "Assets/Scripts/Fluid Sim 3D/Utilities",
            
            // "Assets/Scenes",
            // "Assets/Prefabs",
            // "Assets/ScriptableObjects",
            // "Assets/Resources",
            // "Packages"
        };

        StringBuilder builder = new StringBuilder();

        builder.AppendLine("// ===== UNITY PROJECT EXPORT =====");

        foreach (string folder in folders)
        {
            string fullPath = Path.Combine(projectRoot, folder);

            if (!Directory.Exists(fullPath))
                continue;

            builder.AppendLine();
            builder.AppendLine($"// ===== [{folder}] =====");

            var files = Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                string ext = Path.GetExtension(file);

                if (ext != ".cs"
                    && ext != ".shader"
                    && ext != ".cginc"
                    && ext != ".compute"
                    && ext != ".json"
                    && ext != ".asset"
                    && ext != ".unity"
                    && ext != ".asmdef")
                    continue;

                string relative =
                    file.Replace(projectRoot + "\\", "")
                        .Replace("\\", "/");

                string content = File.ReadAllText(file);

                content = Regex.Replace(content, @"//.*?$", "",
                    RegexOptions.Multiline);

                content = Regex.Replace(content,
                    @"/\*.*?\*/",
                    "",
                    RegexOptions.Singleline);

                content = Regex.Replace(content,
                    @"^\s*$[\r\n]*",
                    "",
                    RegexOptions.Multiline);

                builder.AppendLine();
                builder.AppendLine($"// ===== {relative} =====");
                builder.AppendLine(content);
            }
        }

        string output = Path.Combine(projectRoot, "UnityProjectExport.txt");

        File.WriteAllText(output, builder.ToString());

        Debug.Log("Export Finished : " + output);

        EditorUtility.RevealInFinder(output);
    }
}