using System.IO;
using UnityEngine;
using UnityEngine.InputSystem; 

public class CanvasExporter : MonoBehaviour
{
    [Header("Export Configuration")]
    [Tooltip("The RenderTexture used by the liquid team for painting")]
    public RenderTexture paintRenderTexture; 
    public string fileName = "MyPaintCanvas";

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            SaveCanvasToPNG();
        }
    }

    [ContextMenu("Export Canvas to PNG")]
    public void SaveCanvasToPNG()
    {
        if (paintRenderTexture == null)
        {
            Debug.LogError("CanvasExporter: Please assign the RenderTexture first!");
            return;
        }

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture.active = paintRenderTexture;

        Texture2D texture2D = new Texture2D(paintRenderTexture.width, paintRenderTexture.height, TextureFormat.RGBA32, false);
        texture2D.ReadPixels(new Rect(0, 0, paintRenderTexture.width, paintRenderTexture.height), 0, 0);
        texture2D.Apply();

        RenderTexture.active = previousActive;

        byte[] pngBytes = texture2D.EncodeToPNG();
        DestroyImmediate(texture2D);

        string folderPath = Application.dataPath + "/ExportedPaintings/";
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fullPath = folderPath + fileName + "_" + timestamp + ".png";

        File.WriteAllBytes(fullPath, pngBytes);

        Debug.Log($"<color=green>Canvas exported successfully to:</color> {fullPath}");

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }
}