// VelocityDebugPass.cs
// ──────────────────────────────────────────────────────────────────────────────
// Debug pass: renders every particle as a billboard quad coloured by speed.
// Replaces the standalone RenderSystem3D. Enqueued by FluidRendererFeature
// only when FluidRendererSettings.renderMode == RenderMode.VelocityDebug.
// ──────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public sealed class VelocityDebugPass : ScriptableRenderPass, System.IDisposable
{
    // ── Shader property IDs ──────────────────────────────────────────────────
    static readonly int s_PositionId    = Shader.PropertyToID("_Position");
    static readonly int s_VelocityId    = Shader.PropertyToID("_Velocity");
    static readonly int s_RadiusId      = Shader.PropertyToID("_ParticleRadius");
    static readonly int s_VelocityMaxId = Shader.PropertyToID("_VelocityMax");
    static readonly int s_ColourMapId   = Shader.PropertyToID("_ColourMap");

    // ── Pass-local GPU resources ─────────────────────────────────────────────
    Material      _mat;
    Mesh          _quad;
    ComputeBuffer _argsBuffer;
    Texture2D     _gradientTex;

    // ── Cached values to detect when we need to re-bake the gradient ─────────
    int   _cachedParticleCount = -1;
    int   _cachedGradientRes   = -1;

    // ── Render Graph pass data ────────────────────────────────────────────────
    class PassData
    {
        public Mesh           quad;
        public Material       material;
        public ComputeBuffer  argsBuffer;
        public Bounds         bounds;
    }

    // ─────────────────────────────────────────────────────────────────────────
    public void Setup(FluidRendererFeature feature)
    {
        renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        // Create material from the velocity debug shader assigned in the feature
        if (_mat == null && feature.velocityDebugShader != null)
        {
            _mat = CoreUtils.CreateEngineMaterial(feature.velocityDebugShader);
            _mat.enableInstancing = true;
        }

        if (_quad == null)
            _quad = FluidMeshUtils.CreateQuad();

        var manager  = feature.fluidManager;
        var settings = manager?.SimSettings;
        if (manager == null || settings == null) return;

        // ── Bind GPU buffers onto the material ───────────────────────────────
        // These are stable references; re-binding every frame is cheap.
        _mat.SetBuffer(s_PositionId,    manager.PositionsBuffer);
        _mat.SetBuffer(s_VelocityId,    manager.VelocitiesBuffer);
        _mat.SetFloat (s_RadiusId,      settings.radius);
        _mat.SetFloat (s_VelocityMaxId, settings.velocityDisplayMax);

        // Re-bake gradient only when resolution changes
        BakeGradientIfNeeded(settings.colourMap, settings.gradientResolution);
        _mat.SetTexture(s_ColourMapId, _gradientTex);

        // Update indirect args if particle count changed
        UpdateArgsBuffer(manager.ParticleCount);
    }

    // ── Render Graph entry point ──────────────────────────────────────────────
    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_mat == null || _quad == null || _argsBuffer == null) return;

        var resourceData = frameData.Get<UniversalResourceData>();
        var cameraData   = frameData.Get<UniversalCameraData>();

        // Build bounds from the camera far plane as a conservative fallback
        // (the proper bounds come from the boundary volume; we use a generous
        //  estimate here so GPU culling never clips the particles)
        Bounds bounds = new Bounds(
            cameraData.camera.transform.position,
            Vector3.one * cameraData.camera.farClipPlane * 2f
        );

        using (var builder = renderGraph.AddRasterRenderPass<PassData>(
            "Fluid.VelocityDebug", out var data))
        {
            data.quad        = _quad;
            data.material    = _mat;
            data.argsBuffer  = _argsBuffer;
            data.bounds      = bounds;

            // Write into the camera's active color target
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0,
                AccessFlags.Write);
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture,
                AccessFlags.Write);

            builder.AllowPassCulling(false);

            builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
            {
                ctx.cmd.DrawMeshInstancedIndirect(
                    d.quad, 0, d.material, 0, d.argsBuffer);
            });
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────────────
    public void Dispose()
    {
        DestroyMaterial();
        DestroyMesh();
        ReleaseArgsBuffer();
        DestroyGradientTexture();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    void BakeGradientIfNeeded(Gradient gradient, int resolution)
    {
        resolution = Mathf.Max(2, resolution);
        if (_cachedGradientRes == resolution && _gradientTex != null) return;

        DestroyGradientTexture();
        _gradientTex = new Texture2D(resolution, 1, TextureFormat.RGBA32, mipChain: false)
        {
            wrapMode    = TextureWrapMode.Clamp,
            filterMode  = FilterMode.Bilinear,
            name        = "VelocityGradient"
        };

        Color[] pixels = new Color[resolution];
        for (int i = 0; i < resolution; i++)
            pixels[i] = gradient.Evaluate(i / (float)(resolution - 1));

        _gradientTex.SetPixels(pixels);
        _gradientTex.Apply();
        _cachedGradientRes = resolution;
    }

    void UpdateArgsBuffer(int particleCount)
    {
        if (_argsBuffer == null)
            _argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint),
                ComputeBufferType.IndirectArguments);

        if (_cachedParticleCount == particleCount) return;

        _cachedParticleCount = particleCount;
        uint[] args = new uint[5]
        {
            _quad.GetIndexCount(0),  // index count per instance
            (uint)particleCount,     // instance count
            0, 0, 0
        };
        _argsBuffer.SetData(args);
    }

    void DestroyMaterial()
    {
        if (_mat == null) return;
        CoreUtils.Destroy(_mat);
        _mat = null;
    }

    void DestroyMesh()
    {
        if (_quad == null) return;
        if (Application.isPlaying) Object.Destroy(_quad);
        else                       Object.DestroyImmediate(_quad);
        _quad = null;
    }

    void ReleaseArgsBuffer()
    {
        _argsBuffer?.Release();
        _argsBuffer = null;
    }

    void DestroyGradientTexture()
    {
        if (_gradientTex == null) return;
        if (Application.isPlaying) Object.Destroy(_gradientTex);
        else                       Object.DestroyImmediate(_gradientTex);
        _gradientTex = null;
        _cachedGradientRes = -1;
    }
}
