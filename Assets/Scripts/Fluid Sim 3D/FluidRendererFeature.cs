// FluidRendererFeature.cs  (FIXED)
// ─────────────────────────────────────────────────────────────────────────────
// Fix applied:
//   ValidateShaders() was called unconditionally before the renderMode branch,
//   so switching to Raymarch mode caused it to log errors for the screen-space
//   shaders (particleDepthShader, packDepthShader, etc.) and return false —
//   meaning the raymarch passes were NEVER enqueued.
//
//   Fix: only call ValidateShaders() in ScreenSpace mode.  In Raymarch mode,
//   validate only the raymarch-specific assets (raymarchShader, voxelizerCompute).
// ─────────────────────────────────────────────────────────────────────────────

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

    [Header("Raymarch")]
    public Shader raymarchShader;
    public ComputeShader voxelizerCompute;

    // ── Runtime refs — found automatically from scene ─────────────────────────
    [System.NonSerialized] public FluidManager3D fluidManager;
    [System.NonSerialized] public FluidRendererSettings settings;

    // ── Nested types ──────────────────────────────────────────────────────────
    public enum FluidBlurType { Bilateral1D, Bilateral2D }

    [System.Serializable]
    public struct BilateralFilterSettings
    {
        public float worldRadius;
        public int maxScreenSpaceSize;
        [Range(0f, 1f)] public float strength;
        public float diffStrength;
        public int iterations;
    }

    [System.NonSerialized] public float cachedSmoothingRadius;
    [System.NonSerialized] public float cachedMass;
    [System.NonSerialized] public float cachedSpikyPow2;
    [System.NonSerialized] public Vector3 cachedBoundsMin;
    [System.NonSerialized] public Vector3 cachedBoundsMax;
    [System.NonSerialized] public Matrix4x4 cachedLocalToWorld;
    [System.NonSerialized] public Matrix4x4 cachedWorldToLocal;

    // ── Shortcuts ─────────────────────────────────────────────────────────────
    public float depthParticleSize => settings ? settings.screenSpaceSettings.depthParticleSize : 0.15f;
    public FluidBlurType blurType => settings ? settings.screenSpaceSettings.blurType : FluidBlurType.Bilateral1D;

    public BilateralFilterSettings bilateralSettings => settings
        ? settings.screenSpaceSettings.bilateralSettings
        : new BilateralFilterSettings
            { worldRadius = 0.3f, maxScreenSpaceSize = 40, strength = 0.5f, diffStrength = 20f, iterations = 3 };

    public Color paintColor => settings ? settings.screenSpaceSettings.paintColor : new Color(0.2f, 0.5f, 1f);
    public float specularStrength => settings ? settings.screenSpaceSettings.specularStrength : 0.8f;
    public float specularShininess => settings ? settings.screenSpaceSettings.specularShininess : 64f;
    public float reflectionStrength => settings ? settings.screenSpaceSettings.reflectionStrength : 0.15f;
    public float ambientStrength => settings ? settings.screenSpaceSettings.ambientStrength : 0.25f;
    public bool useHalfLambert => settings ? settings.screenSpaceSettings.useHalfLambert : true;
    public float fillLightStrength => settings ? settings.screenSpaceSettings.fillLightStrength : 0.2f;
    public Color fillLightColor => settings ? settings.screenSpaceSettings.fillLightColor : new Color(0.4f, 0.35f, 0.3f);
    public RaymarchSettings raymarchSettings => settings ? settings.raymarchSettings : null;

    // ── Passes ────────────────────────────────────────────────────────────────
    ParticleDepthPass    _depthPass;
    PackDepthPass        _packPass;
    BilateralSmoothPass  _bilateralPass;
    NormalReconstructPass _normalPass;
    FluidCompositePass   _compositePass;
    VoxelizeDensityPass  _voxelizePass;
    RayMarchPass         _raymarchPass;

    public override void Create()
    {
        _depthPass     = new ParticleDepthPass();
        _packPass      = new PackDepthPass();
        _bilateralPass = new BilateralSmoothPass();
        _normalPass    = new NormalReconstructPass();
        _compositePass = new FluidCompositePass();
        _voxelizePass  = new VoxelizeDensityPass();
        _raymarchPass  = new RayMarchPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (fluidManager == null) fluidManager = Object.FindFirstObjectByType<FluidManager3D>();
        if (settings    == null) settings      = Object.FindFirstObjectByType<FluidRendererSettings>();

        if (fluidManager == null || fluidManager.PositionsBuffer == null || fluidManager.ParticleCount == 0) return;
        if (renderingData.cameraData.cameraType == CameraType.Preview) return;

        // FIX: the raymarch pass reads cachedBoundsMin/Max, cachedLocalToWorld,
        // cachedSmoothingRadius, cachedMass and cachedSpikyPow2 every frame, but
        // nothing was ever writing to them. They defaulted to zero (and a ZERO
        // matrix, not identity, for cachedLocalToWorld), which collapsed the
        // raymarch bounding box to zero size — so RayBoxDst() always returned
        // an empty box and the shader discarded every pixel. Screen-space mode
        // never reads these fields, which is why only raymarch was affected.
        CacheFromFluidManager();

        if (settings?.renderMode == FluidRendererSettings.FluidRenderMode.Raymarch)
        {
            // FIX: validate only the raymarch assets, NOT the screen-space shaders.
            // The old code called ValidateShaders() here which checks particleDepthShader
            // etc. — those are irrelevant in raymarch mode and their absence caused
            // the method to return false, silently preventing the passes from enqueuing.
            if (!ValidateRaymarchAssets()) return;

            _voxelizePass.Setup(this);
            _raymarchPass.Setup(this);
            renderer.EnqueuePass(_voxelizePass);
            renderer.EnqueuePass(_raymarchPass);
        }
        else
        {
            if (!ValidateShaders()) return;

            _depthPass.Setup(this);
            _packPass.Setup(this);
            _bilateralPass.Setup(this);
            _normalPass.Setup(this);
            _compositePass.Setup(this);

            renderer.EnqueuePass(_depthPass);
            renderer.EnqueuePass(_packPass);
            renderer.EnqueuePass(_bilateralPass);
            renderer.EnqueuePass(_normalPass);
            renderer.EnqueuePass(_compositePass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        _depthPass?.Dispose();
        _packPass?.Dispose();
        _bilateralPass?.Dispose();
        _normalPass?.Dispose();
        _compositePass?.Dispose();
        _voxelizePass?.Dispose();
        _raymarchPass?.Dispose();
    }

    // ── Validates only the assets needed for screen-space rendering ───────────
    bool ValidateShaders()
    {
        bool ok = true;
        if (!particleDepthShader)    { Debug.LogError("[FluidRenderer] particleDepthShader missing!");    ok = false; }
        if (!packDepthShader)        { Debug.LogError("[FluidRenderer] packDepthShader missing!");        ok = false; }
        if (!normalsFromDepthShader) { Debug.LogError("[FluidRenderer] normalsFromDepthShader missing!"); ok = false; }
        if (!fluidCompositeShader)   { Debug.LogError("[FluidRenderer] fluidCompositeShader missing!");   ok = false; }
        if (!bilateral1DShader && blurType == FluidBlurType.Bilateral1D)
                                     { Debug.LogError("[FluidRenderer] bilateral1DShader missing!");      ok = false; }
        if (!bilateral2DShader && blurType == FluidBlurType.Bilateral2D)
                                     { Debug.LogError("[FluidRenderer] bilateral2DShader missing!");      ok = false; }
        return ok;
    }

    // ── FIX: populate the cached* fields the raymarch pipeline depends on ─────
    // These were declared but never assigned anywhere in the project, so they
    // silently sat at their C# defaults (zero vectors, and a ZERO matrix — not
    // identity — for the Matrix4x4 fields). Pull the real values every frame
    // from FluidManager3D's SimSettings / BoundaryVolume accessors (added for
    // exactly this purpose, per the comment in FluidManager3D.cs).
    void CacheFromFluidManager()
    {
        var simSettings = fluidManager.SimSettings;
        var boundary    = fluidManager.BoundaryVolume;
        if (simSettings == null || boundary == null) return;

        cachedSmoothingRadius = simSettings.smoothingRadius;
        cachedMass            = simSettings.mass;

        // Same formula FluidModel.SetSmoothingConstant() uses for K_SpikyPow2.
        float h  = cachedSmoothingRadius;
        float h5 = h * h * h * h * h;
        cachedSpikyPow2 = h5 > 0f ? 15f / (2f * Mathf.PI * h5) : 0f;

        cachedBoundsMin    = boundary.LocalMin;
        cachedBoundsMax    = boundary.LocalMax;
        cachedLocalToWorld = boundary.ColliderLocalToWorldMatrix;
        cachedWorldToLocal = boundary.WorldToColliderLocalMatrix;
    }

    // ── Validates only the assets needed for raymarch rendering ──────────────
    bool ValidateRaymarchAssets()
    {
        bool ok = true;
        if (!raymarchShader)   { Debug.LogError("[FluidRenderer] raymarchShader missing! Assign PaintRaymarch_URP.shader.");   ok = false; }
        if (!voxelizerCompute) { Debug.LogError("[FluidRenderer] voxelizerCompute missing! Assign FluidVoxelizer.compute."); ok = false; }
        return ok;
    }
}
