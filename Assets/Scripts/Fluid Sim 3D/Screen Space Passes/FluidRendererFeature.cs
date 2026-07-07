// FluidRendererFeature.cs
// ──────────────────────────────────────────────────────────────────────────────
// Add this once to your URP Renderer Asset and forget about it.
// ALL settings live on the FluidRendererSettings component in your scene.
// ──────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

[System.Serializable]
public class FluidRendererFeature : ScriptableRendererFeature
{
    [Header("Shaders — assign once, never touch again")]
    public Shader particleDepthShader;
    public Shader packDepthShader;
    public Shader bilateral1DShader;
    public Shader bilateral2DShader;
    public Shader normalsFromDepthShader;
    public Shader fluidCompositeShader;

    // ── Runtime refs — found automatically from scene ─────────────────────────
    [System.NonSerialized] public FluidManager3D        fluidManager;
    [System.NonSerialized] public FluidRendererSettings settings;

    // ── Nested types (kept here so FluidRendererSettings can reference them) ──
    public enum FluidBlurType { Bilateral1D, Bilateral2D }

    [System.Serializable]
    public struct BilateralFilterSettings
    {
        public float worldRadius;
        public int   maxScreenSpaceSize;
        [Range(0f,1f)] public float strength;
        public float diffStrength;
        public int   iterations;
    }

    // ── Pigment buffer shortcut — read from FluidManager3D each frame ─────────
    // Null when no pigment system is active; ParticleDepthPass handles null safely.
    public ComputeBuffer      pigmentBuffer       => fluidManager?.PigmentBuffer;

    // ── Shortcuts that read from the scene settings component ─────────────────
    public float              depthParticleSize   => settings ? settings.depthParticleSize   : 0.15f;
    public FluidBlurType      blurType            => settings ? settings.blurType            : FluidBlurType.Bilateral1D;
    public BilateralFilterSettings bilateralSettings => settings ? settings.bilateralSettings : new BilateralFilterSettings { worldRadius=0.3f, maxScreenSpaceSize=40, strength=0.5f, diffStrength=20f, iterations=3 };
    public Color              paintColor          => settings ? settings.paintColor          : new Color(0.2f,0.5f,1f);
    public float              specularStrength    => settings ? settings.specularStrength    : 0.8f;
    public float              specularShininess   => settings ? settings.specularShininess   : 64f;
    public float              reflectionStrength  => settings ? settings.reflectionStrength  : 0.15f;
    public float              ambientStrength     => settings ? settings.ambientStrength     : 0.25f;
    public bool               useHalfLambert      => settings ? settings.useHalfLambert      : true;
    public float              fillLightStrength   => settings ? settings.fillLightStrength   : 0.2f;
    public Color              fillLightColor      => settings ? settings.fillLightColor      : new Color(0.4f,0.35f,0.3f);

    // ── Passes ────────────────────────────────────────────────────────────────
    ParticleDepthPass     _depthPass;
    PackDepthPass         _packPass;
    BilateralSmoothPass   _bilateralPass;
    NormalReconstructPass _normalPass;
    FluidCompositePass    _compositePass;

    public override void Create()
    {
        _depthPass     = new ParticleDepthPass();
        _packPass      = new PackDepthPass();
        _bilateralPass = new BilateralSmoothPass();
        _normalPass    = new NormalReconstructPass();
        _compositePass = new FluidCompositePass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // Auto-find scene objects every frame (cheap FindFirstObjectByType is cached by Unity)
        if (fluidManager == null) fluidManager = Object.FindFirstObjectByType<FluidManager3D>();
        if (settings    == null) settings     = Object.FindFirstObjectByType<FluidRendererSettings>();

        if (fluidManager == null || fluidManager.PositionsBuffer == null || fluidManager.ParticleCount == 0) return;
        if (renderingData.cameraData.cameraType == CameraType.Preview) return;
        if (!ValidateShaders()) return;

        _depthPass    .Setup(this);
        _packPass     .Setup(this);
        _bilateralPass.Setup(this);
        _normalPass   .Setup(this);
        _compositePass.Setup(this);

        renderer.EnqueuePass(_depthPass);
        renderer.EnqueuePass(_packPass);
        renderer.EnqueuePass(_bilateralPass);
        renderer.EnqueuePass(_normalPass);
        renderer.EnqueuePass(_compositePass);
    }

    protected override void Dispose(bool disposing)
    {
        _depthPass    ?.Dispose();
        _packPass     ?.Dispose();
        _bilateralPass?.Dispose();
        _normalPass   ?.Dispose();
        _compositePass?.Dispose();
    }

    bool ValidateShaders()
    {
        bool ok = true;
        if (!particleDepthShader)    { Debug.LogError("[FluidRenderer] particleDepthShader missing!");    ok=false; }
        if (!packDepthShader)        { Debug.LogError("[FluidRenderer] packDepthShader missing!");        ok=false; }
        if (!normalsFromDepthShader) { Debug.LogError("[FluidRenderer] normalsFromDepthShader missing!"); ok=false; }
        if (!fluidCompositeShader)   { Debug.LogError("[FluidRenderer] fluidCompositeShader missing!");   ok=false; }
        if (!bilateral1DShader && blurType == FluidBlurType.Bilateral1D)
                                     { Debug.LogError("[FluidRenderer] bilateral1DShader missing!");      ok=false; }
        if (!bilateral2DShader && blurType == FluidBlurType.Bilateral2D)
                                     { Debug.LogError("[FluidRenderer] bilateral2DShader missing!");      ok=false; }
        return ok;
    }
}
