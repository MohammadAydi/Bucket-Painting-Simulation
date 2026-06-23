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
    public int segments = 64;
    [Range(2, 50)]
    public int heightSubdivisions = 25;
    [Range(2, 50)]
    public int floorSubdivisions = 25;

    [Header("Compartments (Pizza Slots)")]
    public List<float> compartmentRatios = new List<float>();
    public float dividerThickness = 0.08f;

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

    public void OnValidate()
    {
        if (topRadius < 0.1f) topRadius = 0.1f;
        if (bottomRadius < 0.1f) bottomRadius = 0.1f;
        if (height < 0.1f) height = 0.1f;
        if (thickness < 0.01f) thickness = 0.01f;
        if (thickness >= Mathf.Min(topRadius, bottomRadius)) thickness = Mathf.Min(topRadius, bottomRadius) - 0.05f;
        if (dividerThickness < 0.01f) dividerThickness = 0.01f;
        if (heightSubdivisions < 2) heightSubdivisions = 2;
        if (floorSubdivisions < 2) floorSubdivisions = 2;

        switch (shape)
        {
            case BucketShape.Triangular: segments = 3; break;
            case BucketShape.Square: segments = 4; break;
            case BucketShape.Circular: if (segments < 8) segments = 8; break;
        }

        GenerateBucket();
    }

    private void AddDoubleSidedTriangle(List<int> tris, int v1, int v2, int v3)
    {
        tris.Add(v1); tris.Add(v2); tris.Add(v3);
        tris.Add(v1); tris.Add(v3); tris.Add(v2);
    }

    public void GenerateBucket()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();

        bucketMesh = new Mesh();
        bucketMesh.name = "UltimateSolidBucketNoGaps";

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        BucketHoleCutter cutter = GetComponent<BucketHoleCutter>();
        int ringVertexCount = segments + 1;

        // 1. GENERATE VERTICES
        for (int h = 0; h <= heightSubdivisions; h++)
        {
            float t = (float)h / heightSubdivisions;
            float currentHeight = t * height;
            float currentRadius = Mathf.Lerp(bottomRadius, topRadius, t);
            for (int i = 0; i <= segments; i++)
            {
                float angle = (i % segments) * 2 * Mathf.PI / segments;
                vertices.Add(new Vector3(Mathf.Cos(angle) * currentRadius, currentHeight, Mathf.Sin(angle) * currentRadius));
            }
        }

        int innerGridOffset = vertices.Count;
        float innerBottomRadius = bottomRadius - thickness;
        float innerTopRadius = topRadius - thickness;
        for (int h = 0; h <= heightSubdivisions; h++)
        {
            float t = (float)h / heightSubdivisions;
            float currentHeight = thickness + t * (height - thickness);
            float currentRadius = Mathf.Lerp(innerBottomRadius, innerTopRadius, t);
            for (int i = 0; i <= segments; i++)
            {
                float angle = (i % segments) * 2 * Mathf.PI / segments;
                vertices.Add(new Vector3(Mathf.Cos(angle) * currentRadius, currentHeight, Mathf.Sin(angle) * currentRadius));
            }
        }

        int outerFloorOffset = vertices.Count;
        for (int r = 0; r <= floorSubdivisions; r++)
        {
            float radiusT = (float)r / floorSubdivisions;
            float currentRadius = radiusT * bottomRadius;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (i % segments) * 2 * Mathf.PI / segments;
                vertices.Add(new Vector3(Mathf.Cos(angle) * currentRadius, 0, Mathf.Sin(angle) * currentRadius));
            }
        }

        int innerFloorOffset = vertices.Count;
        for (int r = 0; r <= floorSubdivisions; r++)
        {
            float radiusT = (float)r / floorSubdivisions;
            float currentRadius = radiusT * innerBottomRadius;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (i % segments) * 2 * Mathf.PI / segments;
                vertices.Add(new Vector3(Mathf.Cos(angle) * currentRadius, thickness, Mathf.Sin(angle) * currentRadius));
            }
        }

        // 2. GENERATE SIDE WALLS + SIDE RIMS
        bool[,] sideCutMap = new bool[heightSubdivisions, segments];
        for (int h = 0; h < heightSubdivisions; h++)
        {
            for (int i = 0; i < segments; i++)
            {
                int o_b_curr = h * ringVertexCount + i;
                int o_t_next = (h + 1) * ringVertexCount + (i + 1);
                Vector3 sideFaceCenter = (vertices[o_b_curr] + vertices[o_t_next]) / 2f;
                sideCutMap[h, i] = (cutter != null) && cutter.ShouldCutFace(sideFaceCenter, false, thickness, height, bottomRadius, topRadius);
            }
        }

        for (int h = 0; h < heightSubdivisions; h++)
        {
            for (int i = 0; i < segments; i++)
            {
                int o_b_curr = h * ringVertexCount + i;
                int o_b_next = o_b_curr + 1;
                int o_t_curr = (h + 1) * ringVertexCount + i;
                int o_t_next = o_t_curr + 1;

                int i_b_curr = innerGridOffset + h * ringVertexCount + i;
                int i_b_next = i_b_curr + 1;
                int i_t_curr = innerGridOffset + (h + 1) * ringVertexCount + i;
                int i_t_next = i_t_curr + 1;

                if (!sideCutMap[h, i])
                {
                    triangles.Add(o_b_curr); triangles.Add(o_t_curr); triangles.Add(o_b_next);
                    triangles.Add(o_b_next); triangles.Add(o_t_curr); triangles.Add(o_t_next);

                    triangles.Add(i_b_curr); triangles.Add(i_b_next); triangles.Add(i_t_curr);
                    triangles.Add(i_b_next); triangles.Add(i_t_next); triangles.Add(i_t_curr);
                }
                else
                {
                    if (h == 0 || !sideCutMap[h - 1, i])
                    {
                        AddDoubleSidedTriangle(triangles, o_b_curr, o_b_next, i_b_curr);
                        AddDoubleSidedTriangle(triangles, i_b_curr, o_b_next, i_b_next);
                    }
                    if (h == heightSubdivisions - 1 || !sideCutMap[h + 1, i])
                    {
                        AddDoubleSidedTriangle(triangles, o_t_curr, i_t_curr, o_t_next);
                        AddDoubleSidedTriangle(triangles, o_t_next, i_t_curr, i_t_next);
                    }
                    
                    int prevI = (i == 0) ? segments - 1 : i - 1;
                    if (!sideCutMap[h, prevI])
                    {
                        AddDoubleSidedTriangle(triangles, o_b_curr, o_t_curr, i_b_curr);
                        AddDoubleSidedTriangle(triangles, i_b_curr, o_t_curr, i_t_curr);
                    }
                    
                    int nextI = (i == segments - 1) ? 0 : i + 1;
                    if (!sideCutMap[h, nextI])
                    {
                        AddDoubleSidedTriangle(triangles, o_b_next, i_b_next, o_t_next);
                        AddDoubleSidedTriangle(triangles, o_t_next, i_b_next, i_t_next);
                    }
                }

                if (h == heightSubdivisions - 1)
                {
                    AddDoubleSidedTriangle(triangles, o_t_curr, i_t_curr, o_t_next);
                    AddDoubleSidedTriangle(triangles, o_t_next, i_t_curr, i_t_next);
                }
            }
        }

        // 3. GENERATE FLOORS + FIXED TRIANGLE-LEVEL FLOOR RIMS
        bool[,] floorCutMap = new bool[floorSubdivisions, segments];
        for (int r = 0; r < floorSubdivisions; r++)
        {
            for (int i = 0; i < segments; i++)
            {
                int o_f_curr = outerFloorOffset + r * ringVertexCount + i;
                int o_f_top_next = outerFloorOffset + (r + 1) * ringVertexCount + (i + 1);
                Vector3 floorCenterPos = (vertices[o_f_curr] + vertices[o_f_top_next]) / 2f;
                floorCutMap[r, i] = (cutter != null) && cutter.ShouldCutFace(floorCenterPos, true, thickness, height, bottomRadius, topRadius);
            }
        }

        for (int r = 0; r < floorSubdivisions; r++)
        {
            for (int i = 0; i < segments; i++)
            {
                int o_f_curr = outerFloorOffset + r * ringVertexCount + i;
                int o_f_next = o_f_curr + 1;
                int o_f_top_curr = outerFloorOffset + (r + 1) * ringVertexCount + i;
                int o_f_top_next = o_f_top_curr + 1;

                int i_f_curr = innerFloorOffset + r * ringVertexCount + i;
                int i_f_next = i_f_curr + 1;
                int i_f_top_curr = innerFloorOffset + (r + 1) * ringVertexCount + i;
                int i_f_top_next = i_f_top_curr + 1;

                if (!floorCutMap[r, i])
                {
                    // Draw outer and inner floors normally if not cut
                    triangles.Add(o_f_curr); triangles.Add(o_f_top_curr); triangles.Add(o_f_next);
                    triangles.Add(o_f_next); triangles.Add(o_f_top_curr); triangles.Add(o_f_top_next);

                    triangles.Add(i_f_curr); triangles.Add(i_f_next); triangles.Add(i_f_top_curr);
                    triangles.Add(i_f_next); triangles.Add(i_f_top_next); triangles.Add(i_f_top_curr);
                }
                else
                {
                    // CRISIS SOLVED: Every cut cell forces its 4 borders to verify on a triangle-subdivision basis
                    // 1. Inner Radial Ring Rim
                    if (r == 0 || !floorCutMap[r - 1, i])
                    {
                        AddDoubleSidedTriangle(triangles, o_f_curr, o_f_next, i_f_curr);
                        AddDoubleSidedTriangle(triangles, i_f_curr, o_f_next, i_f_next);
                    }
                    // 2. Outer Radial Ring Rim
                    if (r == floorSubdivisions - 1 || !floorCutMap[r + 1, i])
                    {
                        AddDoubleSidedTriangle(triangles, o_f_top_curr, i_f_top_curr, o_f_top_next);
                        AddDoubleSidedTriangle(triangles, o_f_top_next, i_f_top_curr, i_f_top_next);
                    }
                    // 3. Left Segment Rim
                    int prevI = (i == 0) ? segments - 1 : i - 1;
                    if (!floorCutMap[r, prevI])
                    {
                        AddDoubleSidedTriangle(triangles, o_f_curr, i_f_curr, o_f_top_curr);
                        AddDoubleSidedTriangle(triangles, o_f_top_curr, i_f_curr, i_f_top_curr);
                    }
                    // 4. Right Segment Rim
                    int nextI = (i == segments - 1) ? 0 : i + 1;
                    if (!floorCutMap[r, nextI])
                    {
                        AddDoubleSidedTriangle(triangles, o_f_next, o_f_top_next, i_f_next);
                        AddDoubleSidedTriangle(triangles, i_f_next, o_f_top_next, i_f_top_next); // FIXED: Forces both halves of the triangle link to snap solid
                    }
                }
            }
        }

        // 4. GENERATE COMPARTMENTS
        if (compartmentRatios != null && compartmentRatios.Count > 1)
        {
            float totalRatioSum = 0;
            foreach (float r in compartmentRatios) totalRatioSum += r;

            float currentAngle = 0f;
            float halfThickness = dividerThickness / 2f;

            for (int i = 0; i < compartmentRatios.Count; i++)
            {
                Vector3 wallDir = new Vector3(Mathf.Cos(currentAngle), 0, Mathf.Sin(currentAngle));
                Vector3 wallNormal = new Vector3(-Mathf.Sin(currentAngle), 0, Mathf.Cos(currentAngle));

                Vector3 c_bottom_left  = new Vector3(0, thickness, 0) - (wallNormal * halfThickness);
                Vector3 c_bottom_right = new Vector3(0, thickness, 0) + (wallNormal * halfThickness);
                Vector3 c_top_left     = new Vector3(0, height, 0) - (wallNormal * halfThickness);
                Vector3 c_top_right    = new Vector3(0, height, 0) + (wallNormal * halfThickness);

                Vector3 p_bottom_left  = (wallDir * innerBottomRadius) + new Vector3(0, thickness, 0) - (wallNormal * halfThickness);
                Vector3 p_bottom_right = (wallDir * innerBottomRadius) + new Vector3(0, thickness, 0) + (wallNormal * halfThickness);
                Vector3 p_top_left     = (wallDir * innerTopRadius) + new Vector3(0, height, 0) - (wallNormal * halfThickness);
                Vector3 p_top_right    = (wallDir * innerTopRadius) + new Vector3(0, height, 0) + (wallNormal * halfThickness);

                int baseIndex = vertices.Count;
                vertices.Add(c_bottom_left); vertices.Add(c_top_left); vertices.Add(p_bottom_left); vertices.Add(p_top_left);
                vertices.Add(c_bottom_right); vertices.Add(c_top_right); vertices.Add(p_bottom_right); vertices.Add(p_top_right);

                triangles.Add(baseIndex + 0); triangles.Add(baseIndex + 1); triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 2); triangles.Add(baseIndex + 1); triangles.Add(baseIndex + 3);

                triangles.Add(baseIndex + 4); triangles.Add(baseIndex + 6); triangles.Add(baseIndex + 5);
                triangles.Add(baseIndex + 6); triangles.Add(baseIndex + 7); triangles.Add(baseIndex + 5);

                triangles.Add(baseIndex + 1); triangles.Add(baseIndex + 5); triangles.Add(baseIndex + 3);
                triangles.Add(baseIndex + 3); triangles.Add(baseIndex + 5); triangles.Add(baseIndex + 7);

                float normalizedRatio = compartmentRatios[i] / totalRatioSum;
                currentAngle += normalizedRatio * 2 * Mathf.PI;
            }
        }

        bucketMesh.vertices = vertices.ToArray();
        bucketMesh.triangles = triangles.ToArray();
        bucketMesh.RecalculateNormals();
        bucketMesh.RecalculateBounds();

        meshFilter.mesh = bucketMesh;
    }
}