using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class BucketFluidCollision3D : MonoBehaviour
{
    // Layout must match HoleGPU in BucketCollision3D.compute
    [StructLayout(LayoutKind.Sequential)]
    private struct HoleGPU
    {
        public float type;
        public float location;
        public float radius;
        public float width;
        public float height;
        public float angleDeg;
        public float heightPos;
        public float offsetX;
        public float offsetY;
    }

    // Layout must match DividerGPU in BucketCollision3D.compute
    [StructLayout(LayoutKind.Sequential)]
    private struct DividerGPU
    {
        public Vector3 dir;
        public Vector3 normal;
    }

    [Header("References")]
    public BucketGenerator bucket;
    public BucketHoleCutter holeCutter;
    public FluidManager3D fluidManager;
    public ComputeShader bucketCollisionShader;

    [Header("Must match the ParticleSettings asset used by FluidManager3D")]
    public float particleRadius = 0.01f;

    [Header("Collision Response")]
    [Range(0f, 1f)] public float restitution = 0.2f;

    private int _kernel;
    private ComputeBuffer _holesBuffer;
    private ComputeBuffer _dividersBuffer;
    private int _holeCount;
    private int _dividerCount;

    private static readonly int PositionsId = Shader.PropertyToID("_Positions");
    private static readonly int VelocitiesId = Shader.PropertyToID("_Velocities");
    private static readonly int HolesId = Shader.PropertyToID("_Holes");
    private static readonly int DividersId = Shader.PropertyToID("_Dividers");
    private static readonly int ParticleCountId = Shader.PropertyToID("_ParticleCount");
    private static readonly int HoleCountId = Shader.PropertyToID("_HoleCount");
    private static readonly int DividerCountId = Shader.PropertyToID("_DividerCount");
    private static readonly int BucketSegmentsId = Shader.PropertyToID("_BucketSegments");
    private static readonly int BottomRadiusId = Shader.PropertyToID("_BottomRadius");
    private static readonly int TopRadiusId = Shader.PropertyToID("_TopRadius");
    private static readonly int BucketHeightId = Shader.PropertyToID("_BucketHeight");
    private static readonly int WallThicknessId = Shader.PropertyToID("_WallThickness");
    private static readonly int DividerHalfThicknessId = Shader.PropertyToID("_DividerHalfThickness");
    private static readonly int ParticleRadiusId = Shader.PropertyToID("_ParticleRadius");
    private static readonly int RestitutionId = Shader.PropertyToID("_Restitution");
    private static readonly int WorldToLocalId = Shader.PropertyToID("_BucketWorldToLocal");
    private static readonly int LocalToWorldId = Shader.PropertyToID("_BucketLocalToWorld");

    private void Start()
    {
        _kernel = bucketCollisionShader.FindKernel("ResolveBucketCollisions");
        BuildHolesBuffer();
        BuildDividersBuffer();
    }

    private void OnDestroy()
    {
        _holesBuffer?.Release();
        _dividersBuffer?.Release();
    }

    // Call this again at runtime if holes or compartments are changed after Start
    public void RebuildGeometryBuffers()
    {
        BuildHolesBuffer();
        BuildDividersBuffer();
    }

    private void BuildHolesBuffer()
    {
        _holesBuffer?.Release();
        List<HoleGPU> data = new List<HoleGPU>();
        if (holeCutter != null && holeCutter.enableHoles && holeCutter.holes != null)
        {
            foreach (HoleData h in holeCutter.holes)
            {
                data.Add(new HoleGPU
                {
                    type = h.type == BucketHoleCutter.HoleType.Circular ? 0f : 1f,
                    location = h.location == BucketHoleCutter.HoleLocation.Side ? 0f : 1f,
                    radius = h.radius,
                    width = h.width,
                    height = h.height,
                    angleDeg = h.angleDegrees,
                    heightPos = h.heightPosition,
                    offsetX = h.bottomOffset.x,
                    offsetY = h.bottomOffset.y
                });
            }
        }
        _holeCount = data.Count;
        _holesBuffer = new ComputeBuffer(Mathf.Max(_holeCount, 1), Marshal.SizeOf<HoleGPU>());
        if (_holeCount > 0) _holesBuffer.SetData(data);
    }

    private void BuildDividersBuffer()
    {
        _dividersBuffer?.Release();
        List<DividerGPU> data = new List<DividerGPU>();
        List<float> ratios = bucket.compartmentRatios;
        if (ratios != null && ratios.Count > 1)
        {
            float total = 0f;
            foreach (float r in ratios) total += r;
            if (total <= 0f) total = 1f;
            float currentAngle = 0f;
            for (int i = 0; i < ratios.Count; i++)
            {
                data.Add(new DividerGPU
                {
                    dir = new Vector3(Mathf.Cos(currentAngle), 0f, Mathf.Sin(currentAngle)),
                    normal = new Vector3(-Mathf.Sin(currentAngle), 0f, Mathf.Cos(currentAngle))
                });
                currentAngle += (ratios[i] / total) * 2f * Mathf.PI;
            }
        }
        _dividerCount = data.Count;
        _dividersBuffer = new ComputeBuffer(Mathf.Max(_dividerCount, 1), Marshal.SizeOf<DividerGPU>());
        if (_dividerCount > 0) _dividersBuffer.SetData(data);
    }

    // Runs after FluidManager3D.Update() has finished integrating this frame's substeps
    private void LateUpdate()
    {
        if (fluidManager == null || bucket == null || bucketCollisionShader == null) return;
        int particleCount = fluidManager.ParticleCount;
        if (particleCount == 0 || fluidManager.PositionsBuffer == null) return;

        bucketCollisionShader.SetBuffer(_kernel, PositionsId, fluidManager.PositionsBuffer);
        bucketCollisionShader.SetBuffer(_kernel, VelocitiesId, fluidManager.VelocitiesBuffer);
        bucketCollisionShader.SetBuffer(_kernel, HolesId, _holesBuffer);
        bucketCollisionShader.SetBuffer(_kernel, DividersId, _dividersBuffer);
        bucketCollisionShader.SetInt(ParticleCountId, particleCount);
        bucketCollisionShader.SetInt(HoleCountId, _holeCount);
        bucketCollisionShader.SetInt(DividerCountId, _dividerCount);
        bucketCollisionShader.SetInt(BucketSegmentsId, bucket.segments);
        bucketCollisionShader.SetFloat(BottomRadiusId, bucket.bottomRadius);
        bucketCollisionShader.SetFloat(TopRadiusId, bucket.topRadius);
        bucketCollisionShader.SetFloat(BucketHeightId, bucket.height);
        bucketCollisionShader.SetFloat(WallThicknessId, bucket.thickness);
        bucketCollisionShader.SetFloat(DividerHalfThicknessId, bucket.dividerThickness * 0.5f);
        bucketCollisionShader.SetFloat(ParticleRadiusId, particleRadius);
        bucketCollisionShader.SetFloat(RestitutionId, restitution);
        bucketCollisionShader.SetMatrix(WorldToLocalId, bucket.transform.worldToLocalMatrix);
        bucketCollisionShader.SetMatrix(LocalToWorldId, bucket.transform.localToWorldMatrix);

        int groups = Mathf.CeilToInt(particleCount / 256f);
        bucketCollisionShader.Dispatch(_kernel, groups, 1, 1);
    }
}