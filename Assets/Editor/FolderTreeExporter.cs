using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class FolderTreeExporter
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

    [MenuItem("Tools/Export Folder Trees")]
    private static void ExportFolderTrees()
    {
        foreach (string rootFolder in FolderPaths)
        {
            if (!Directory.Exists(rootFolder))
            {
                Debug.LogWarning($"Folder does not exist: {rootFolder}");
                continue;
            }

            StringBuilder builder = new StringBuilder();

            builder.AppendLine(Path.GetFileName(rootFolder));

            WriteDirectory(rootFolder, builder, "");

            string outputPath = Path.Combine(rootFolder, "FolderTree.txt");

            File.WriteAllText(outputPath, builder.ToString());

            Debug.Log($"Exported: {outputPath}");
        }

        EditorUtility.RevealInFinder(FolderPaths[0]);
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