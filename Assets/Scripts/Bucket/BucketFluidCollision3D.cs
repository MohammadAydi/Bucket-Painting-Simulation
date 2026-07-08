using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class BucketFluidCollision3D : MonoBehaviour
{
    [StructLayout(LayoutKind.Sequential)]
    private struct HoleGPU
    {
        public float type; public float location; public float radius;
        public float width; public float height; public float angleDeg;
        public float heightPos; public float offsetX; public float offsetY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DividerGPU { public Vector3 dir; public Vector3 normal; }

    [Header("References")]
    public BucketGenerator bucket;
    public BucketHoleCutter holeCutter;
    public FluidManager3D fluidManager;
    public ComputeShader bucketCollisionShader;

    [Header("Physics Settings")]
    public float particleRadius = 0.04f;
    [Range(0f, 1f)] public float restitution = 0.1f;

    private int _kernel;
    private ComputeBuffer _holesBuffer;
    private ComputeBuffer _dividersBuffer;
    private int _holeCount;
    private int _dividerCount;

    private void Start()
    {
        if (bucketCollisionShader != null)
            _kernel = bucketCollisionShader.FindKernel("ResolveBucketCollisions");
            
        BuildHolesBuffer();
        BuildDividersBuffer();
    }

    private void OnDestroy()
    {
        _holesBuffer?.Release();
        _dividersBuffer?.Release();
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
                    radius = h.radius, width = h.width, height = h.height,
                    angleDeg = h.angleDegrees, heightPos = h.heightPosition,
                    offsetX = h.bottomOffset.x, offsetY = h.bottomOffset.y
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
                data.Add(new DividerGPU {
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

    // دالة جديدة تستقبل الموضع والدوران اللحظي لتحديث مصفوفات التحويل
    public void ResolveCollisionsInterp(Vector3 interpPos, Quaternion interpRot)
    {
        if (fluidManager == null || bucket == null || bucketCollisionShader == null) return;
        
        int particleCount = fluidManager.ParticleCount;
        if (particleCount == 0 || fluidManager.PositionsBuffer == null) return;

        bucketCollisionShader.SetBuffer(_kernel, "_Positions", fluidManager.PositionsBuffer);
        bucketCollisionShader.SetBuffer(_kernel, "_Velocities", fluidManager.VelocitiesBuffer);
        bucketCollisionShader.SetBuffer(_kernel, "_Holes", _holesBuffer);
        bucketCollisionShader.SetBuffer(_kernel, "_Dividers", _dividersBuffer);

        bucketCollisionShader.SetInt("_ParticleCount", particleCount);
        bucketCollisionShader.SetInt("_HoleCount", _holeCount);
        bucketCollisionShader.SetInt("_DividerCount", _dividerCount);

        bucketCollisionShader.SetFloat("_BottomRadius", bucket.bottomRadius);
        bucketCollisionShader.SetFloat("_TopRadius", bucket.topRadius);
        bucketCollisionShader.SetFloat("_BucketHeight", bucket.height);
        bucketCollisionShader.SetFloat("_WallThickness", bucket.thickness);
        bucketCollisionShader.SetFloat("_DividerHalfThickness", bucket.dividerThickness * 0.5f);
        bucketCollisionShader.SetFloat("_ParticleRadius", particleRadius);
        bucketCollisionShader.SetFloat("_Restitution", restitution);

        // بناء مصفوفات التحويل بناءً على الموضع المستوفى
        Matrix4x4 localToWorld = Matrix4x4.TRS(interpPos, interpRot, bucket.transform.lossyScale);
        Matrix4x4 worldToLocal = localToWorld.inverse;

        bucketCollisionShader.SetMatrix("_BucketWorldToLocal", worldToLocal);
        bucketCollisionShader.SetMatrix("_BucketLocalToWorld", localToWorld);

        int groups = Mathf.CeilToInt(particleCount / 256f);
        bucketCollisionShader.Dispatch(_kernel, groups, 1, 1);
    }
}