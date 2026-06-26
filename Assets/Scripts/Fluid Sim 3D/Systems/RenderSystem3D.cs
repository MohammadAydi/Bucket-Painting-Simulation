using System;
using UnityEngine;
using UnityEngine.Rendering;

/// Responsible for drawing all fluid particles in a single GPU-instanced call.
/// Uses a sphere mesh (via SphereGenerator) and a baked gradient texture to
/// colour each particle by its velocity magnitude.
/// 
public sealed class RenderSystem3D : IDisposable
{
    // ── Shader property IDs ──────────────────────────────────────────────────
    static readonly int ParticlesId      = Shader.PropertyToID("_Particles");
    static readonly int ParticleRadiusId = Shader.PropertyToID("_ParticleRadius");
    static readonly int ColourMapId      = Shader.PropertyToID("_ColourMap");
    static readonly int VelocityMaxId    = Shader.PropertyToID("_VelocityMax");

    // ── State ────────────────────────────────────────────────────────────────
    readonly Material             _material;
    readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();

    Mesh      _mesh;
    Texture2D _gradientTexture;

    // ── Construction ─────────────────────────────────────────────────────────
    public RenderSystem3D(Material material)
    {
        _material = material;
        _material.enableInstancing = true;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// Builds the sphere mesh and uploads all material parameters.
    public void Initialize(ParticleSettings settings)
    {
        RebuildMesh(settings.sphereResolution);
        SyncMaterial(settings);
    }

    /// Uploads only the material-side settings (colour, velocity max)
    public void SyncMaterial(ParticleSettings settings)
    {
        _material.SetFloat(ParticleRadiusId, settings.radius);
        _material.SetFloat(VelocityMaxId,    settings.velocityDisplayMax);

        BakeGradient(settings.colourMap, settings.gradientResolution);
        _material.SetTexture(ColourMapId, _gradientTexture);
    }

    /// Destroys the current sphere mesh and recreates it at a new resolution.
    public void RebuildMesh(int resolution)
    {
        DestroyMesh();
        _mesh = SebStuff.SphereGenerator.GenerateSphereMesh(resolution);
        _mesh.name = "ParticleSphereMesh";
    }

    /// Issues the instanced draw call. Call every frame from LateUpdate.
    public void Render(ComputeBuffer particlesBuffer, int particleCount, Bounds bounds)
    {
        if (_mesh == null || particlesBuffer == null || particleCount <= 0)
            return;

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
            receiveShadows: false,
            layer: 0,
            camera: null,
            LightProbeUsage.Off,
            lightProbeProxyVolume: null);
    }

    public void Dispose()
    {
        DestroyMesh();
        DestroyGradientTexture();
    }

    // ── Private helpers ──────────────────────────────────────────────────────


    /// Bakes a Unity Gradient into a 1-D Texture2D so the shader can sample it.
    /// Re-uses the existing texture if the resolution has not changed.
    void BakeGradient(Gradient gradient, int resolution)
    {
        resolution = Mathf.Max(2, resolution);

        if (_gradientTexture == null || _gradientTexture.width != resolution)
        {
            DestroyGradientTexture();
            _gradientTexture = new Texture2D(resolution, 1, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode   = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name       = "VelocityGradient"
            };
        }

        Color[] pixels = new Color[resolution];
        for (int i = 0; i < resolution; i++)
        {
            float t = i / (float)(resolution - 1);
            pixels[i] = gradient.Evaluate(t);
        }

        _gradientTexture.SetPixels(pixels);
        _gradientTexture.Apply();
    }

    void DestroyMesh()
    {
        if (_mesh == null) return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(_mesh);
        else
            UnityEngine.Object.DestroyImmediate(_mesh);

        _mesh = null;
    }

    void DestroyGradientTexture()
    {
        if (_gradientTexture == null) return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(_gradientTexture);
        else
            UnityEngine.Object.DestroyImmediate(_gradientTexture);

        _gradientTexture = null;
    }
}