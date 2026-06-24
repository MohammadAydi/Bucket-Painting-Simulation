using System;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class DensityRenderSystem3D : IDisposable
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
    readonly Mesh _cubeMesh;

    public DensityRenderSystem3D(Material material)
    {
        _material = material;
        _cubeMesh = CreateCubeMesh();
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

    public void Render(Matrix4x4 matrix)
    {
        if (_material == null || _cubeMesh == null)
        {
            return;
        }

        Graphics.DrawMesh(
            _cubeMesh,
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
        if (_cubeMesh == null)
        {
            return;
        }

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(_cubeMesh);
        else
            UnityEngine.Object.DestroyImmediate(_cubeMesh);
    }

    static Mesh CreateCubeMesh()
    {
        Mesh mesh = new Mesh
        {
            name = "DensityFieldCube"
        };

        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f)
        };

        mesh.triangles = new[]
        {
            0, 2, 1, 0, 3, 2,
            4, 5, 6, 4, 6, 7,
            0, 1, 5, 0, 5, 4,
            2, 3, 7, 2, 7, 6,
            0, 4, 7, 0, 7, 3,
            1, 2, 6, 1, 6, 5
        };
        mesh.RecalculateBounds();
        return mesh;
    }
}