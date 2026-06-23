using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class BucketGenerator : MonoBehaviour
{
    public enum BucketShape { Circular, Triangular, Square }

    [Header("Bucket Shape")]
    public BucketShape shape = BucketShape.Circular;

    [Header("Bucket Dimensions")]
    public float topRadius = 2.0f;
    public float bottomRadius = 1.5f;
    public float height = 3.0f;
    public float thickness = 0.2f;

    [Header("Mesh Settings")]
    [Range(3, 128)]
    public int segments = 32;

    [Header("Compartments (Pizza Slots)")]
    [Tooltip("Add sections to split the bucket")]
    public List<float> compartmentRatios = new List<float>();

    [Header("Physics (Metadata Only)")]
    public float mass = 1.0f;

    private MeshFilter meshFilter;
    private Mesh bucketMesh;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
    }

    private void Start()
    {
        GenerateBucket();
    }

    private void OnValidate()
    {
        // Enforce logical dimension boundaries
        if (topRadius < 0.1f) topRadius = 0.1f;
        if (bottomRadius < 0.1f) bottomRadius = 0.1f;
        if (height < 0.1f) height = 0.1f;
        if (thickness < 0.01f) thickness = 0.01f;
        if (thickness >= Mathf.Min(topRadius, bottomRadius)) thickness = Mathf.Min(topRadius, bottomRadius) - 0.05f;

        // Manage segments dynamically based on the selected shape
        switch (shape)
        {
            case BucketShape.Triangular:
                segments = 3;
                break;
            case BucketShape.Square:
                segments = 4;
                break;
            case BucketShape.Circular:
                if (segments < 8) segments = 8;
                break;
        }

        // Clean up compartment ratios (no zero or negative numbers allowed)
        if (compartmentRatios != null)
        {
            for (int i = 0; i < compartmentRatios.Count; i++)
            {
                if (compartmentRatios[i] <= 0) compartmentRatios[i] = 1.0f;
            }
        }

        GenerateBucket();
    }

    public void GenerateBucket()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();

        bucketMesh = new Mesh();
        bucketMesh.name = "ProceduralBucket";

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        // ==========================================
        // 1. GENERATE BUCKET SHELL (Outer & Inner)
        // ==========================================
        for (int i = 0; i <= segments; i++)
        {
            float angle = (i % segments) * 2 * Mathf.PI / segments;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            // Outer Vertices
            vertices.Add(new Vector3(cos * bottomRadius, 0, sin * bottomRadius));
            vertices.Add(new Vector3(cos * topRadius, height, sin * topRadius));
        }

        float innerBottomRadius = bottomRadius - thickness;
        float innerTopRadius = topRadius - thickness;

        for (int i = 0; i <= segments; i++)
        {
            float angle = (i % segments) * 2 * Mathf.PI / segments;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            // Inner Vertices
            vertices.Add(new Vector3(cos * innerBottomRadius, thickness, sin * innerBottomRadius));
            vertices.Add(new Vector3(cos * innerTopRadius, height, sin * innerTopRadius));
        }

        int outerBottomCenterIndex = vertices.Count;
        vertices.Add(new Vector3(0, 0, 0)); 

        int innerBottomCenterIndex = vertices.Count;
        vertices.Add(new Vector3(0, thickness, 0)); 

        // Generate Shell Triangles
        int outerOffset = 0;
        int innerOffset = (segments + 1) * 2;

        for (int i = 0; i < segments; i++)
        {
            int currentOuterB = outerOffset + (i * 2);
            int currentOuterT = currentOuterB + 1;
            int nextOuterB = outerOffset + ((i + 1) * 2);
            int nextOuterT = nextOuterB + 1;

            int currentInnerB = innerOffset + (i * 2);
            int currentInnerT = currentInnerB + 1;
            int nextInnerB = innerOffset + ((i + 1) * 2);
            int nextInnerT = nextInnerB + 1;

            // Outer Wall
            triangles.Add(currentOuterB); triangles.Add(currentOuterT); triangles.Add(nextOuterB);
            triangles.Add(nextOuterB); triangles.Add(currentOuterT); triangles.Add(nextOuterT);

            // Inner Wall
            triangles.Add(currentInnerB); triangles.Add(nextInnerB); triangles.Add(currentInnerT);
            triangles.Add(nextInnerB); triangles.Add(nextInnerT); triangles.Add(currentInnerT);

            // Top Rim
            triangles.Add(currentOuterT); triangles.Add(currentInnerT); triangles.Add(nextOuterT);
            triangles.Add(nextOuterT); triangles.Add(currentInnerT); triangles.Add(nextInnerT);

            // Outer Bottom Floor
            triangles.Add(outerBottomCenterIndex); triangles.Add(nextOuterB); triangles.Add(currentOuterB);

            // Inner Bottom Floor
            triangles.Add(innerBottomCenterIndex); triangles.Add(currentInnerB); triangles.Add(nextInnerB);
        }

        // ==========================================
        // 2. GENERATE INTERNAL COMPARTMENT WALLS
        // ==========================================
        if (compartmentRatios != null && compartmentRatios.Count > 1)
        {
            float totalRatioSum = 0;
            foreach (float r in compartmentRatios) totalRatioSum += r;

            float currentAngle = 0f;

            // Generate a divider wall at the start of each compartment
            for (int i = 0; i < compartmentRatios.Count; i++)
            {
                float cos = Mathf.Cos(currentAngle);
                float sin = Mathf.Sin(currentAngle);

                // Define the 4 points of the divider wall quadrilateral
                Vector3 centerBottom = new Vector3(0, thickness, 0);
                Vector3 centerTop = new Vector3(0, height, 0);
                Vector3 perimeterBottom = new Vector3(cos * innerBottomRadius, thickness, sin * innerBottomRadius);
                Vector3 perimeterTop = new Vector3(cos * innerTopRadius, height, sin * innerTopRadius);

                // Add vertices for Side A (Facing one direction)
                int v0 = vertices.Count;
                vertices.Add(centerBottom);
                vertices.Add(centerTop);
                vertices.Add(perimeterBottom);
                vertices.Add(perimeterTop);

                // Triangles Side A
                triangles.Add(v0); triangles.Add(v0 + 1); triangles.Add(v0 + 2);
                triangles.Add(v0 + 2); triangles.Add(v0 + 1); triangles.Add(v0 + 3);

                // Add vertices for Side B (Facing the opposite direction)
                int v1 = vertices.Count;
                vertices.Add(centerBottom);
                vertices.Add(centerTop);
                vertices.Add(perimeterBottom);
                vertices.Add(perimeterTop);

                // Triangles Side B (Reversed winding order)
                triangles.Add(v1); triangles.Add(v1 + 2); triangles.Add(v1 + 1);
                triangles.Add(v1 + 2); triangles.Add(v1 + 3); triangles.Add(v1 + 1);

                // Calculate angle for the next compartment divider
                float normalizedRatio = compartmentRatios[i] / totalRatioSum;
                currentAngle += normalizedRatio * 2 * Mathf.PI;
            }
        }

        // ==========================================
        // 3. ASSIGN AND RECALCULATE MESH
        // ==========================================
        bucketMesh.vertices = vertices.ToArray();
        bucketMesh.triangles = triangles.ToArray();

        bucketMesh.RecalculateNormals();
        bucketMesh.RecalculateBounds();

        meshFilter.mesh = bucketMesh;
    }
}