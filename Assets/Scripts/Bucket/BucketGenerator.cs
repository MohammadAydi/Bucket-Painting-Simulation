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
    
    [Tooltip("Thickness of the internal divider walls.")]
    public float dividerThickness = 0.05f;

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
        if (dividerThickness < 0.01f) dividerThickness = 0.01f;

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

        // Clean up compartment ratios
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

            vertices.Add(new Vector3(cos * innerBottomRadius, thickness, sin * innerBottomRadius));
            vertices.Add(new Vector3(cos * innerTopRadius, height, sin * innerTopRadius));
        }

        int outerBottomCenterIndex = vertices.Count;
        vertices.Add(new Vector3(0, 0, 0)); 

        int innerBottomCenterIndex = vertices.Count;
        vertices.Add(new Vector3(0, thickness, 0)); 

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
        // 2. GENERATE THICK INTERNAL COMPARTMENT WALLS
        // ==========================================
        if (compartmentRatios != null && compartmentRatios.Count > 1)
        {
            float totalRatioSum = 0;
            foreach (float r in compartmentRatios) totalRatioSum += r;

            float currentAngle = 0f;
            float halfThickness = dividerThickness / 2f;

            for (int i = 0; i < compartmentRatios.Count; i++)
            {
                // Direction vector along the divider wall line
                Vector3 wallDir = new Vector3(Mathf.Cos(currentAngle), 0, Mathf.Sin(currentAngle));
                // Perpendicular vector to push vertices sideways and create thickness
                Vector3 wallNormal = new Vector3(-Mathf.Sin(currentAngle), 0, Mathf.Cos(currentAngle));

                // 8 Vertices to form a 3D block for the divider wall
                // Center-side vertices (shifted left and right by half thickness)
                Vector3 c_bottom_left  = new Vector3(0, thickness, 0) - (wallNormal * halfThickness);
                Vector3 c_bottom_right = new Vector3(0, thickness, 0) + (wallNormal * halfThickness);
                Vector3 c_top_left     = new Vector3(0, height, 0) - (wallNormal * halfThickness);
                Vector3 c_top_right    = new Vector3(0, height, 0) + (wallNormal * halfThickness);

                // Perimeter-side vertices (shifted left and right by half thickness)
                Vector3 p_bottom_left  = (wallDir * innerBottomRadius) + new Vector3(0, thickness, 0) - (wallNormal * halfThickness);
                Vector3 p_bottom_right = (wallDir * innerBottomRadius) + new Vector3(0, thickness, 0) + (wallNormal * halfThickness);
                Vector3 p_top_left     = (wallDir * innerTopRadius) + new Vector3(0, height, 0) - (wallNormal * halfThickness);
                Vector3 p_top_right    = (wallDir * innerTopRadius) + new Vector3(0, height, 0) + (wallNormal * halfThickness);

                int baseIndex = vertices.Count;

                // Add vertices to list
                vertices.Add(c_bottom_left);  // 0
                vertices.Add(c_top_left);     // 1
                vertices.Add(p_bottom_left);  // 2
                vertices.Add(p_top_left);     // 3
                vertices.Add(c_bottom_right); // 4
                vertices.Add(c_top_right);    // 5
                vertices.Add(p_bottom_right); // 6
                vertices.Add(p_top_right);    // 7

                // Side A (Left face)
                triangles.Add(baseIndex + 0); triangles.Add(baseIndex + 1); triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 2); triangles.Add(baseIndex + 1); triangles.Add(baseIndex + 3);

                // Side B (Right face)
                triangles.Add(baseIndex + 4); triangles.Add(baseIndex + 6); triangles.Add(baseIndex + 5);
                triangles.Add(baseIndex + 6); triangles.Add(baseIndex + 7); triangles.Add(baseIndex + 5);

                // Top Face of the divider
                triangles.Add(baseIndex + 1); triangles.Add(baseIndex + 5); triangles.Add(baseIndex + 3);
                triangles.Add(baseIndex + 3); triangles.Add(baseIndex + 5); triangles.Add(baseIndex + 7);

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