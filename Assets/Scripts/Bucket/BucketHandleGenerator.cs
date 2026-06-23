using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class BucketHandleGenerator : MonoBehaviour
{
    [Header("Connected Bucket")]
    [SerializeField] private BucketGenerator bucketGenerator;

    [Header("Handle Dimensions")]
    [Range(0.02f, 0.2f)] public float handleWidth = 0.05f;       
    [Range(0.01f, 0.1f)] public float handleThickness = 0.02f;   
    [Range(8, 64)] public int handleSegments = 32;               
    public float clearance = 0.05f;                              

    private MeshFilter meshFilter;
    private Mesh handleMesh;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
    }

    private void Start()
    {
        if (bucketGenerator == null)
        {
            bucketGenerator = GetComponentInParent<BucketGenerator>();
        }
        GenerateHandle();
    }

    public void OnValidate()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        GenerateHandle();
    }

    public void GenerateHandle()
    {
        if (bucketGenerator == null) return;

        if (handleMesh == null)
        {
            handleMesh = new Mesh();
            handleMesh.name = "BucketHandle_Mesh";
            meshFilter.mesh = handleMesh;
        }
        else
        {
            handleMesh.Clear();
        }

        float R = bucketGenerator.topRadius + clearance;
        float h = bucketGenerator.height;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int i = 0; i <= handleSegments; i++)
        {
            float t = (float)i / handleSegments;
            float theta = t * Mathf.PI; 

            float x = Mathf.Cos(theta) * R;
            float y = h + Mathf.Sin(theta) * R;
            Vector3 centerPos = new Vector3(x, y, 0f);

            Vector3 tangent = new Vector3(-Mathf.Sin(theta), Mathf.Cos(theta), 0f).normalized;
            Vector3 forward = Vector3.forward;
            Vector3 normal = Vector3.Cross(tangent, forward).normalized;

            float halfW = handleWidth / 2f;
            float halfT = handleThickness / 2f;

            Vector3 v0 = centerPos + (forward * halfW) + (normal * halfT);
            Vector3 v1 = centerPos + (forward * halfW) - (normal * halfT);
            Vector3 v2 = centerPos - (forward * halfW) - (normal * halfT);
            Vector3 v3 = centerPos - (forward * halfW) + (normal * halfT);

            vertices.Add(v0);
            vertices.Add(v1);
            vertices.Add(v2);
            vertices.Add(v3);

            if (i > 0)
            {
                int curr = i * 4;
                int prev = (i - 1) * 4;

                // Top Face
                triangles.Add(prev + 0); triangles.Add(curr + 0); triangles.Add(prev + 3);
                triangles.Add(prev + 3); triangles.Add(curr + 0); triangles.Add(curr + 3);

                // Bottom Face
                triangles.Add(prev + 1); triangles.Add(prev + 2); triangles.Add(curr + 1);
                triangles.Add(curr + 1); triangles.Add(prev + 2); triangles.Add(curr + 2);

                // Front Face
                triangles.Add(prev + 0); triangles.Add(prev + 1); triangles.Add(curr + 0);
                triangles.Add(curr + 0); triangles.Add(prev + 1); triangles.Add(curr + 1);

                // Back Face
                triangles.Add(prev + 2); triangles.Add(prev + 3); triangles.Add(curr + 2);
                triangles.Add(curr + 2); triangles.Add(prev + 3); triangles.Add(curr + 3);
            }
        }

        // Start Cap
        triangles.Add(0); triangles.Add(3); triangles.Add(1);
        triangles.Add(1); triangles.Add(3); triangles.Add(2);

        // End Cap
        int lastCap = handleSegments * 4;
        triangles.Add(lastCap + 0); triangles.Add(lastCap + 1); triangles.Add(lastCap + 3);
        triangles.Add(lastCap + 1); triangles.Add(lastCap + 2); triangles.Add(lastCap + 3);

        handleMesh.vertices = vertices.ToArray();
        handleMesh.triangles = triangles.ToArray();
        handleMesh.RecalculateNormals();
        handleMesh.RecalculateBounds();
    }
}