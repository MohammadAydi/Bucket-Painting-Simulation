using System;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class RenderSystem2D : IDisposable
{
    static readonly int ParticlesId = Shader.PropertyToID("_Particles");
    static readonly int ParticleRadiusId = Shader.PropertyToID("_ParticleRadius");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    
    // New Shader Property IDs
    static readonly int UseSpeedColorId = Shader.PropertyToID("_UseSpeedColor");
    static readonly int VelocityMaxId = Shader.PropertyToID("_VelocityMax");
    static readonly int GradientTexId = Shader.PropertyToID("_GradientTex");

    readonly Material _material;
    readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();
    Mesh _mesh;
    Texture2D _gradientTexture; // Holds the baked gradient representation

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

        // Send visualization states down to the shader
        _material.SetFloat(UseSpeedColorId, settings.useSpeedColor ? 1f : 0f);
        _material.SetFloat(VelocityMaxId, settings.velocityDisplayMax);

        // Bake and assign the texture array
        UpdateGradientTexture(settings.colourMap, settings.gradientResolution);
        if (_gradientTexture != null)
        {
            _material.SetTexture(GradientTexId, _gradientTexture);
        }
    }

    private void UpdateGradientTexture(Gradient gradient, int resolution)
    {
        // Recreate the texture reference safely if resolution setting shifts
        if (_gradientTexture == null || _gradientTexture.width != resolution)
        {
            if (_gradientTexture != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_gradientTexture);
                else UnityEngine.Object.DestroyImmediate(_gradientTexture);
            }

            _gradientTexture = new Texture2D(resolution, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        // Evaluate and write gradient colors across pixels
        Color[] colors = new Color[resolution];
        for (int i = 0; i < resolution; i++)
        {
            float t = (float)i / (resolution - 1);
            colors[i] = gradient.Evaluate(t);
        }
        _gradientTexture.SetPixels(colors);
        _gradientTexture.Apply();
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
        if (_mesh != null)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(_mesh);
            else
                UnityEngine.Object.DestroyImmediate(_mesh);
            _mesh = null;
        }

        // Prevent memory leaks by properly releasing the generated texture
        if (_gradientTexture != null)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(_gradientTexture);
            else
                UnityEngine.Object.DestroyImmediate(_gradientTexture);
            _gradientTexture = null;
        }
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