using System;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class RenderSystem2D : IDisposable
{
    static readonly int ParticlesId = Shader.PropertyToID("_Particles");
    static readonly int ParticleRadiusId = Shader.PropertyToID("_ParticleRadius");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    readonly Material _material;
    readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();
    Mesh _mesh;

    public RenderSystem2D(Material material)
    {
        _material = material;
        _material.enableInstancing = true;
    }

    public void Initialize(ParticleSettings settings)
    {
        RebuildMesh(settings.segments);
        SyncMaterial(settings);
    }

    public void SyncMaterial(ParticleSettings settings)
    {
        _material.SetFloat(ParticleRadiusId, settings.radius);
        _material.SetColor(ColorId, settings.particleColor);
    }

    public void RebuildMesh(int segments)
    {
        if (_mesh != null)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(_mesh);
            else
                UnityEngine.Object.DestroyImmediate(_mesh);
        }

        _mesh = CreateCircleMesh(segments);
    }

    public void Render(ComputeBuffer particlesBuffer, int particleCount, Bounds bounds)
    {
        if (_mesh == null || particlesBuffer == null || particleCount <= 0)
        {
            return;
        }

        _propertyBlock.Clear();
        _propertyBlock.SetBuffer(ParticlesId, particlesBuffer);

        Graphics.DrawMeshInstancedProcedural(
            _mesh,
            0,
            _material,
            bounds,
            particleCount,
            _propertyBlock,
            ShadowCastingMode.Off,
            false,
            0,
            null,
            LightProbeUsage.Off,
            null);
    }

    public void Dispose()
    {
        if (_mesh == null)
        {
            return;
        }

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(_mesh);
        else
            UnityEngine.Object.DestroyImmediate(_mesh);

        _mesh = null;
    }

    static Mesh CreateCircleMesh(int segments)
    {
        segments = Mathf.Max(3, segments);

        Vector3[] vertices = new Vector3[segments + 1];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;

        float step = Mathf.PI * 2f / segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * step;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        }

        int t = 0;
        for (int i = 0; i < segments; i++)
        {
            triangles[t++] = 0;
            triangles[t++] = i + 1;
            triangles[t++] = i == segments - 1 ? 1 : i + 2;
        }

        Mesh mesh = new Mesh
        {
            name = "ParticleCircleMesh"
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }
}