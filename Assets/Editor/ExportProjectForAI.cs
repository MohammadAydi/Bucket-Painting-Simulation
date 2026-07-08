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
            "Assets/ScriptableObjects",
            "Assets/Scripts/Bucket",
            "Assets/Scripts/onlyone",

            // "Assets/Scripts/Fluid Sim 3D/Data",
            // "Assets/Scripts/Fluid Sim 3D/compute",

            "Assets/Scripts/Fluid Sim 3D/Data",
            "Assets/Scripts/Fluid Sim 3D/Display",
            "Assets/Scripts/Fluid Sim 3D/HLSL",
            "Assets/Scripts/Fluid Sim 3D/physics",
            "Assets/Scripts/Fluid Sim 3D/Systems",

            // "Assets/Scripts/Fluid Sim 3D/Utilities",
            
            // "Assets/Scenes",
            // "Packages"
        };

        string[] files =
        {
            // "Assets/Scripts/Fluid Sim 3D/FluidManager3D.cs",
            // "Assets/Scripts/Fluid Sim 3D/Config.cs",
            "Assets/Settings/PC_Renderer.asset",
            "Assets/Scripts/Fluid Sim 3D/Utilities/FluidBoundary3D.cs",
            "Assets/Scripts/Fluid Sim 3D/Utilities/ComputeHelper.cs",
            "Assets/Scripts/Fluid Sim 3D/Utilities/ParticlesSpawner3D.cs",
            "Assets/Scripts/Fluid Sim 3D/Config.cs",
            "Assets/Scripts/Fluid Sim 3D/FluidManager3D.cs",
            "Assets/Scripts/Fluid Sim 3D/FluidRendererSettings.cs",
        };

        StringBuilder builder = new StringBuilder();

        builder.AppendLine("// ===== UNITY PROJECT EXPORT =====");

        foreach (string folder in folders)
        {
            string fullPath = Path.Combine(projectRoot, folder);

            if (!Directory.Exists(fullPath))
                continue;

            builder.AppendLine();
            builder.AppendLine($"// ===== [FOLDER: {folder}] =====");

            var folderFiles = Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories);

            foreach (var file in folderFiles)
            {
                ProcessFile(file, projectRoot, builder);
            }
        }

        foreach (string file in files)
        {
            string fullPath = Path.Combine(projectRoot, file);

            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"File not found: {file}");
                continue;
            }

            builder.AppendLine();
            builder.AppendLine($"// ===== [FILE: {file}] =====");
            
            ProcessFile(fullPath, projectRoot, builder);
        }

        string output = Path.Combine(projectRoot, "UnityProjectExport.txt");

        File.WriteAllText(output, builder.ToString());

        Debug.Log("Export Finished : " + output);

        EditorUtility.RevealInFinder(output);
    }

    private static void ProcessFile(string filePath, string projectRoot, StringBuilder builder)
    {
        string ext = Path.GetExtension(filePath);

        if (ext != ".cs"
            && ext != ".shader"
            && ext != ".cginc"
            && ext != ".compute"
            && ext != ".json"
            && ext != ".asset"
            && ext != ".unity"
            && ext != ".asmdef")
            return;

        string relative = filePath.Replace(projectRoot + "\\", "").Replace("\\", "/");

        string content = File.ReadAllText(filePath);

        content = Regex.Replace(content, @"//.*?$", "", RegexOptions.Multiline);
        content = Regex.Replace(content, @"/\*.*?\*/", "", RegexOptions.Singleline);
        content = Regex.Replace(content, @"^\s*$[\r\n]*", "", RegexOptions.Multiline);

        builder.AppendLine($"// ===== {relative} =====");
        builder.AppendLine(content);
    }
}