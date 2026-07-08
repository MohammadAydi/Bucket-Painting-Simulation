using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class BucketGenerator : MonoBehaviour
{


    [Header("Bucket Dimensions")]
    public float topRadius = 0.13f;
    public float bottomRadius = 0.11f;
    public float height = 0.28f;
    public float thickness = 0.01f;

    [Header("Mesh Settings")]
    [Range(3, 128)]
    public int segments = 128;
    [Range(2, 50)]
    public int heightSubdivisions = 50;
    [Range(2, 50)]
    public int floorSubdivisions = 50;

    [Header("Compartments (Pizza Slots)")]
    public List<float> compartmentRatios = new List<float>();
    public float dividerThickness = 0.08f;

    private MeshFilter meshFilter;
    private Mesh bucketMesh;

    // Surface tags: 0 = side wall, 1 = floor, 2 = divider
    private const float TAG_WALL = 0f;
    private const float TAG_FLOOR = 1f;
    private const float TAG_DIVIDER = 2f;

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
        if (topRadius < 0.01f) topRadius = 0.01f;
        if (bottomRadius < 0.01f) bottomRadius = 0.01f;
        if (height < 0.01f) height = 0.01f;
        if (thickness < 0.001f) thickness = 0.001f;
        if (thickness >= Mathf.Min(topRadius, bottomRadius)) thickness = Mathf.Min(topRadius, bottomRadius) - 0.05f;
        if (dividerThickness < 0.001f) dividerThickness = 0.001f;
        if (heightSubdivisions < 2) heightSubdivisions = 2;
        if (floorSubdivisions < 2) floorSubdivisions = 2;

        if (compartmentRatios != null && compartmentRatios.Count > 0)
        {
            float totalRatioSum = 0;
            for (int i = 0; i < compartmentRatios.Count; i++)
            {
                if (compartmentRatios[i] < 0) compartmentRatios[i] = 0;
                totalRatioSum += compartmentRatios[i];
            }

            if (totalRatioSum <= 0)
            {
                for (int i = 0; i < compartmentRatios.Count; i++)
                {
                    compartmentRatios[i] = 1f;
                }
            }
        }

        if (segments < 8) segments = 8;

        GenerateBucket();
    }

    public void GenerateBucket()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();

        bucketMesh = new Mesh();
        bucketMesh.name = "Bucket";

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> surfaceTags = new List<Vector2>();
        List<int> triangles = new List<int>();

        int ringVertexCount = segments + 1;

        for (int h = 0; h <= heightSubdivisions; h++)
        {
            float t = (float)h / heightSubdivisions;
            float currentHeight = t * height;
            float currentRadius = Mathf.Lerp(bottomRadius, topRadius, t);
            for (int i = 0; i <= segments; i++)
            {
                float angle = (i % segments) * 2 * Mathf.PI / segments;
                vertices.Add(new Vector3(Mathf.Cos(angle) * currentRadius, currentHeight, Mathf.Sin(angle) * currentRadius));
                surfaceTags.Add(new Vector2(TAG_WALL, 0f));
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
                surfaceTags.Add(new Vector2(TAG_WALL, 0f));
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
                surfaceTags.Add(new Vector2(TAG_FLOOR, 0f));
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
                surfaceTags.Add(new Vector2(TAG_FLOOR, 0f));
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

                triangles.Add(o_b_curr); triangles.Add(o_t_curr); triangles.Add(o_b_next);
                triangles.Add(o_b_next); triangles.Add(o_t_curr); triangles.Add(o_t_next);

                triangles.Add(i_b_curr); triangles.Add(i_b_next); triangles.Add(i_t_curr);
                triangles.Add(i_b_next); triangles.Add(i_t_next); triangles.Add(i_t_curr);

                if (h == heightSubdivisions - 1)
                {
                    triangles.Add(o_t_curr); triangles.Add(i_t_curr); triangles.Add(o_t_next);
                    triangles.Add(o_t_next); triangles.Add(i_t_curr); triangles.Add(i_t_next);
                }
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

                triangles.Add(o_f_curr); triangles.Add(o_f_top_curr); triangles.Add(o_f_next);
                triangles.Add(o_f_next); triangles.Add(o_f_top_curr); triangles.Add(o_f_top_next);

                triangles.Add(i_f_curr); triangles.Add(i_f_next); triangles.Add(i_f_top_curr);
                triangles.Add(i_f_next); triangles.Add(i_f_top_next); triangles.Add(i_f_top_curr);
            }
        }

        if (compartmentRatios != null && compartmentRatios.Count > 1)
        {
            float totalRatioSum = 0;
            foreach (float r in compartmentRatios) totalRatioSum += r;
            if (totalRatioSum <= 0) totalRatioSum = 1f;

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
                for (int k = 0; k < 8; k++) surfaceTags.Add(new Vector2(TAG_DIVIDER, 0f));

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
        bucketMesh.SetUVs(1, surfaceTags);
        bucketMesh.triangles = triangles.ToArray();
        bucketMesh.RecalculateNormals();
        bucketMesh.RecalculateBounds();

        meshFilter.mesh = bucketMesh;

        BucketHoleShaderFeeder feeder = GetComponent<BucketHoleShaderFeeder>();
        if (feeder != null) feeder.UpdateShaderData();
    }
}