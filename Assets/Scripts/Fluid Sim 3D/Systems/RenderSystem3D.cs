using System;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class RenderSystem3D : IDisposable
{
    // ── Shader property IDs ──────────────────────────────────────────────────
    static readonly int PositionId = Shader.PropertyToID("_Position");
    static readonly int VelocityId = Shader.PropertyToID("_Velocity");
    static readonly int ParticleRadiusId = Shader.PropertyToID("_ParticleRadius");
    static readonly int ColourMapId = Shader.PropertyToID("_ColourMap");
    static readonly int VelocityMaxId = Shader.PropertyToID("_VelocityMax");

    // ── State ────────────────────────────────────────────────────────────────
    readonly Material _material;
    readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();

    Mesh _quadMesh;
    Texture2D _gradientTexture;

    // ── Construction ─────────────────────────────────────────────────────────
    public RenderSystem3D(Material material)
    {
        _material = material;
        _material.enableInstancing = true;
    }

    // ── Public API ───────────────────────────────────────────────────────────
    public void Initialize(
     ParticleSettings settings,
     ComputeBuffer positionsBuffer,
     ComputeBuffer velocitiesBuffer)
    {
        CreateQuadMesh();
        SyncMaterial(settings);

        _material.SetBuffer(PositionId, positionsBuffer);
        _material.SetBuffer(VelocityId, velocitiesBuffer);
    }

    public void SyncMaterial(ParticleSettings settings)
    {
        _material.SetFloat(ParticleRadiusId, settings.radius);
        _material.SetFloat(VelocityMaxId, settings.velocityDisplayMax);

        BakeGradient(settings.colourMap, settings.gradientResolution);
        _material.SetTexture(ColourMapId, _gradientTexture);
    }

    /// Issues the procedural draw call. Call every frame from LateUpdate.
    public void Render(int particleCount, Bounds bounds)
    {
        if (particleCount <= 0)
            return;


        // Switch to DrawProcedural generating 4 vertices per instance (quads)
        Graphics.DrawMeshInstancedProcedural(
             _quadMesh,
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
             lightProbeProxyVolume: null
         );
    }

    public void Dispose()
    {
        DestroyGradientTexture();
    }
    /// Creates a 4-vertex quad spanning from -1 to 1.
    void CreateQuadMesh()
    {
        DestroyMesh();
        _quadMesh = new Mesh { name = "ParticleQuad" };

        // Vertices from -1 to 1 to match our circular alpha mask math perfectly
        _quadMesh.vertices = new Vector3[] {
            new Vector3(-1, -1, 0),
            new Vector3( 1, -1, 0),
            new Vector3(-1,  1, 0),
            new Vector3( 1,  1, 0)
        };

        _quadMesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
    }

    void DestroyMesh()
    {
        if (_quadMesh == null) return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(_quadMesh);
        else
            UnityEngine.Object.DestroyImmediate(_quadMesh);

        _quadMesh = null;
    }

    // ── Private helpers ──────────────────────────────────────────────────────
    void BakeGradient(Gradient gradient, int resolution)
    {
        resolution = Mathf.Max(2, resolution);

        if (_gradientTexture == null || _gradientTexture.width != resolution)
        {
            DestroyGradientTexture();
            _gradientTexture = new Texture2D(resolution, 1, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "VelocityGradient"
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