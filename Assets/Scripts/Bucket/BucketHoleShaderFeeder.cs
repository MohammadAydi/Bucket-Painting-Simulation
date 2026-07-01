using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class BucketHoleShaderFeeder : MonoBehaviour
{
    public const int MAX_HOLES = 8;

    public BucketGenerator bucket;
    public BucketHoleCutter cutter;

    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock block;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
    }

    private void Start()
    {
        UpdateShaderData();
    }

    public void UpdateShaderData()
    {
        if (bucket == null || cutter == null) return;
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if (block == null) block = new MaterialPropertyBlock();

        float[] type = new float[MAX_HOLES];
        float[] location = new float[MAX_HOLES];
        float[] radius = new float[MAX_HOLES];
        float[] width = new float[MAX_HOLES];
        float[] heightArr = new float[MAX_HOLES];
        float[] angle = new float[MAX_HOLES];
        float[] heightPos = new float[MAX_HOLES];
        float[] offsetX = new float[MAX_HOLES];
        float[] offsetY = new float[MAX_HOLES];

        int count = 0;
        if (cutter.enableHoles && cutter.holes != null)
        {
            count = Mathf.Min(cutter.holes.Count, MAX_HOLES);
            for (int i = 0; i < count; i++)
            {
                HoleData h = cutter.holes[i];
                type[i] = (h.type == BucketHoleCutter.HoleType.Circular) ? 0f : 1f;
                location[i] = (h.location == BucketHoleCutter.HoleLocation.Side) ? 0f : 1f;
                radius[i] = h.radius;
                width[i] = h.width;
                heightArr[i] = h.height;
                angle[i] = h.angleDegrees;
                heightPos[i] = h.heightPosition;
                offsetX[i] = h.bottomOffset.x;
                offsetY[i] = h.bottomOffset.y;
            }
        }

        meshRenderer.GetPropertyBlock(block);
        block.SetInt("_HoleCount", count);
        block.SetFloatArray("_HoleType", type);
        block.SetFloatArray("_HoleLocation", location);
        block.SetFloatArray("_HoleRadius", radius);
        block.SetFloatArray("_HoleWidth", width);
        block.SetFloatArray("_HoleHeight", heightArr);
        block.SetFloatArray("_HoleAngle", angle);
        block.SetFloatArray("_HoleHeightPos", heightPos);
        block.SetFloatArray("_HoleOffsetX", offsetX);
        block.SetFloatArray("_HoleOffsetY", offsetY);
        block.SetFloat("_BucketHeight", bucket.height);
        block.SetFloat("_BottomRadius", bucket.bottomRadius);
        block.SetFloat("_TopRadius", bucket.topRadius);
        meshRenderer.SetPropertyBlock(block);
    }

    private void OnValidate()
    {
        UpdateShaderData();
    }
}