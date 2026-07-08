using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class FolderTreeExporterCombined
{
    // Add the folders you want to export here
    private static readonly string[] FolderPaths =
    {
        // "Assets/fluid sim 3D/Data",
        "Assets/scripts/fluid sim 3D",
        "Assets/scripts/Bucket",
        "Assets/scripts/onlyone",

        // "Assets/Display",
        // "Assets/systems",
        // "Assets/physics"
    };

    [MenuItem("Tools/Export Folder Trees (Combined)")]
    private static void ExportFolderTreesCombined()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;

        if (FolderPaths.Length == 0)
        {
            Debug.LogWarning("No folders specified for export.");
            return;
        }

        StringBuilder combinedBuilder = new StringBuilder();
        
        // Add header with timestamp
        combinedBuilder.AppendLine("=== Combined Folder Tree Export ===");
        combinedBuilder.AppendLine($"Generated: {System.DateTime.Now}");
        combinedBuilder.AppendLine("====================================\n");

        int exportedCount = 0;

        foreach (string rootFolder in FolderPaths)
        {
            if (!Directory.Exists(rootFolder))
            {
                Debug.LogWarning($"Folder does not exist: {rootFolder}");
                continue;
            }

            combinedBuilder.AppendLine($"\n--- {Path.GetFileName(rootFolder)} ---");
            combinedBuilder.AppendLine($"Path: {rootFolder}");
            combinedBuilder.AppendLine();

            // Write the tree structure
            WriteDirectory(rootFolder, combinedBuilder, "");

            exportedCount++;
        }

        if (exportedCount == 0)
        {
            Debug.LogWarning("No valid folders found to export.");
            return;
        }

        string outputPath = Path.Combine(projectRoot, "CombinedFolderTrees.txt");
        File.WriteAllText(outputPath, combinedBuilder.ToString());

        Debug.Log($"Exported combined folder trees to: {outputPath}");
        Debug.Log($"Total folders exported: {exportedCount}");

        // Reveal the file in Finder/Explorer
        EditorUtility.RevealInFinder(outputPath);
    }

    private static void WriteDirectory(string directory, StringBuilder builder, string indent)
    {
        string[] directories = Directory.GetDirectories(directory);
        string[] files = Directory.GetFiles(directory);

        int totalItems = directories.Length + files.Length;
        int currentItem = 0;

        foreach (string dir in directories)
        {
            currentItem++;
            bool last = currentItem == totalItems;

            builder.Append(indent);
            builder.Append(last ? "└── " : "├── ");
            builder.AppendLine(Path.GetFileName(dir));

            WriteDirectory(dir, builder, indent + (last ? "    " : "│   "));
        }

        foreach (string file in files)
        {
            currentItem++;
            bool last = currentItem == totalItems;

            builder.Append(indent);
            builder.Append(last ? "└── " : "├── ");
            builder.AppendLine(Path.GetFileName(file));
        }
    }
}