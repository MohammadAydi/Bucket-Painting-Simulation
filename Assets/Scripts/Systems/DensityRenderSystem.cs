using System;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class DensityRenderSystem : IDisposable
{
    static readonly int ParticlesId = Shader.PropertyToID("_Particles");
    static readonly int ParticleCountId = Shader.PropertyToID("_ParticleCount");
    static readonly int MassId = Shader.PropertyToID("_Mass");
    static readonly int SmoothingRadiusId = Shader.PropertyToID("_SmoothingRadius");
    static readonly int TargetDensityId = Shader.PropertyToID("_TargetDensity");
    static readonly int LowColorId = Shader.PropertyToID("_LowColor");
    static readonly int TargetColorId = Shader.PropertyToID("_TargetColor");
    static readonly int HighColorId = Shader.PropertyToID("_HighColor");

    readonly Material _material;
    readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();
    readonly Mesh _quadMesh;

    public DensityRenderSystem(Material material)
    {
        _material = material;
        _quadMesh = CreateQuadMesh();
    }

    public void SyncMaterial(ParticleSettings settings, ComputeBuffer particlesBuffer)
    {
        if (_material == null)
        {
            return;
        }

        _material.SetBuffer(ParticlesId, particlesBuffer);
        _material.SetInt(ParticleCountId, settings.particleCount);
        _material.SetFloat(MassId, settings.mass);
        _material.SetFloat(SmoothingRadiusId, settings.smoothingRadius);
        _material.SetFloat(TargetDensityId, settings.targetDensity);
        _material.SetColor(LowColorId, settings.lowDensityColor);
        _material.SetColor(TargetColorId, settings.TargetDensityColor);
        _material.SetColor(HighColorId, settings.highDensityColor);
    }

    public void Render(Vector2 boundsMin, Vector2 boundsMax)
    {
        if (_material == null || _quadMesh == null)
        {
            return;
        }

        Vector3 center = new Vector3((boundsMin.x + boundsMax.x) * 0.5f, (boundsMin.y + boundsMax.y) * 0.5f, 0.1f);
        Vector3 scale = new Vector3(boundsMax.x - boundsMin.x, boundsMax.y - boundsMin.y, 1f);
        Matrix4x4 matrix = Matrix4x4.TRS(center, Quaternion.identity, scale);

        Graphics.DrawMesh(
            _quadMesh,
            matrix,
            _material,
            0,
            null,
            0,
            _propertyBlock,
            ShadowCastingMode.Off,
            false,
            null,
            LightProbeUsage.Off,
            null);
    }

    public void Dispose()
    {
        if (_quadMesh == null)
        {
            return;
        }

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(_quadMesh);
        else
            UnityEngine.Object.DestroyImmediate(_quadMesh);
    }

    static Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh
        {
            name = "DensityFieldQuad"
        };

        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        };

        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        return mesh;
    }
}