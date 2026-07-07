using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CanvasGenerator : MonoBehaviour
{
    public enum CanvasShape { Circular, Rectangular }
    public enum SurfaceType { Metal, Glass, Carpet, Fabric, Paper }

    [Header("Canvas Settings")]
    public CanvasShape shape = CanvasShape.Rectangular;
    public SurfaceType surfaceType = SurfaceType.Paper;

    [Header("Rectangular Dimensions")]
    public float width = 6f;
    public float length = 4f;

    [Header("Circular Dimensions")]
    public float radius = 3f;
    [Range(3, 128)] public int segments = 64;

    [Header("Orientation")]
    [Tooltip("Control canvas rotation along X, Y, and Z axes")]
    public Vector3 canvasRotation = Vector3.zero; 

    [Header("Surface Materials")]
    public Material metalMaterial;
    public Material glassMaterial;
    public Material carpetMaterial;
    public Material fabricMaterial;
    public Material paperMaterial;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh canvasMesh;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
    }

    private void Start()
    {
        BuildCanvas();
    }

    public void OnValidate()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        
        BuildCanvas();
        UpdateSurfaceMaterial();
        UpdateOrientation();
    }

    public void BuildCanvas()
    {
        if (canvasMesh == null)
        {
            canvasMesh = new Mesh
            {
                name = "Procedural_Canvas_Mesh"
            };
        }
        else
        {
            canvasMesh.Clear();
        }

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        if (shape == CanvasShape.Rectangular)
        {
            float halfW = width / 2f;
            float halfL = length / 2f;

            vertices.Add(new Vector3(-halfW, 0, -halfL));
            vertices.Add(new Vector3(-halfW, 0, halfL));
            vertices.Add(new Vector3(halfW, 0, halfL));
            vertices.Add(new Vector3(halfW, 0, -halfL));

            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(0, 1));
            uvs.Add(new Vector2(1, 1));
            uvs.Add(new Vector2(1, 0));

            triangles.Add(0); triangles.Add(1); triangles.Add(2);
            triangles.Add(0); triangles.Add(2); triangles.Add(3);
        }
        else if (shape == CanvasShape.Circular)
        {
            vertices.Add(Vector3.zero);
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int i = 0; i <= segments; i++)
            {
                float angle = (i * 2 * Mathf.PI) / segments;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;

                vertices.Add(new Vector3(x, 0, z));

                float u = (Mathf.Cos(angle) + 1f) / 2f;
                float v = (Mathf.Sin(angle) + 1f) / 2f;
                uvs.Add(new Vector2(u, v));

                if (i > 0)
                {
                    triangles.Add(0);
                    triangles.Add(vertices.Count - 1);
                    triangles.Add(vertices.Count - 2);
                }
            }
        }

        canvasMesh.vertices = vertices.ToArray();
        canvasMesh.uv = uvs.ToArray();
        canvasMesh.triangles = triangles.ToArray();
        canvasMesh.RecalculateNormals();
        canvasMesh.RecalculateBounds();

        meshFilter.mesh = canvasMesh;
    }

    private void UpdateSurfaceMaterial()
    {
        Material targetMat = paperMaterial;
        switch (surfaceType)
        {
            case SurfaceType.Metal: targetMat = metalMaterial; break;
            case SurfaceType.Glass: targetMat = glassMaterial; break;
            case SurfaceType.Carpet: targetMat = carpetMaterial; break;
            case SurfaceType.Fabric: targetMat = fabricMaterial; break;
            case SurfaceType.Paper: targetMat = paperMaterial; break;
        }

        if (targetMat != null)
        {
            meshRenderer.material = targetMat;
        }
    }

    private void UpdateOrientation()
    {
        transform.localRotation = Quaternion.Euler(canvasRotation);
    }
}