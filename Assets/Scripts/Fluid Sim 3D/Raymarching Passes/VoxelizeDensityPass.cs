// VoxelizeDensityPass.cs  (FIXED)
// ─────────────────────────────────────────────────────────────────────────────
// Fix applied:
//   Removed the SpatialIndices ("SortedIndices") buffer binding entirely.
//   FluidVoxelizer.compute no longer needs it — see the fix comment at the top
//   of that file. _predictedPositionsBuffer is already physically reordered
//   into sorted order by ReorderCopyBack every step, so the voxelizer indexes
//   it directly with the loop counter, exactly like DensityCalc.hlsl does.
//   Binding SpatialIndices here and then not using it in the kernel would just
//   be dead weight; removing it keeps the two sides honest with each other.
//
//   Also: ValidateShaders() in FluidRendererFeature was returning false
//   in Raymarch mode because particleDepthShader etc. are only needed for
//   screen-space mode.  The raymarch branch is now guarded so ValidateShaders()
//   is NOT called when renderMode == Raymarch.  See FluidRendererFeature.cs fix.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class VoxelizeDensityPass : ScriptableRenderPass, System.IDisposable
{
    // ── Property IDs ─────────────────────────────────────────────────────────
    static readonly int s_DensityMap     = Shader.PropertyToID("DensityMap");
    static readonly int s_DensityMapSize = Shader.PropertyToID("_DensityMapSize");

    // Spatial hash buffer names — must match FluidVoxelizer.compute declarations
    static readonly int s_PredictedPositions = Shader.PropertyToID("_PredictedPositions");
    static readonly int s_SpatialKeys        = Shader.PropertyToID("SpatialKeys");
    static readonly int s_SpatialOffsets     = Shader.PropertyToID("SpatialOffsets");

    static readonly int s_BoundaryLocalToWorld = Shader.PropertyToID("_BoundaryLocalToWorld");

    // ── Public output — RayMarchPass reads this ───────────────────────────────
    public static RenderTexture DensityMap { get; private set; }

    // ── State ─────────────────────────────────────────────────────────────────
    FluidRendererFeature _feature;
    ComputeShader            _cs;
    int                      _kernel = -1;
    int                      _lastResolution = -1;

    // ─────────────────────────────────────────────────────────────────────────
    public void Setup(FluidRendererFeature feature)
    {
        _feature = feature;
        renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;

        if (_cs == null && feature.voxelizerCompute != null)
        {
            _cs     = feature.voxelizerCompute;
            _kernel = _cs.FindKernel("VoxelizeDensity");
        }
    }

    // ── Render Graph entry point ──────────────────────────────────────────────
    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_cs == null || _kernel < 0) return;
        var fm = _feature.fluidManager;
        if (fm == null || fm.PositionsBuffer == null || fm.ParticleCount == 0) return;

        int res = _feature.raymarchSettings != null
            ? _feature.raymarchSettings.voxelResolution
            : 64;

        EnsureDensityMap(res);

        using (var builder = renderGraph.AddUnsafePass<PassData>("Fluid.VoxelizeDensity", out var data))
        {
            data.cs          = _cs;
            data.kernel      = _kernel;
            data.densityMap  = DensityMap;
            data.fm          = fm;
            data.res         = res;
            data.feature     = _feature;

            builder.AllowPassCulling(false);

            builder.SetRenderFunc((PassData d, UnsafeGraphContext ctx) =>
            {
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                BindAndDispatch(cmd, d);
            });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    static void BindAndDispatch(CommandBuffer cmd, PassData d)
    {
        var fm      = d.fm;
        var feature = d.feature;
        var cs      = d.cs;
        int k       = d.kernel;
        int res     = d.res;

        var model = fm.FluidModelInternal;

        // ── Buffers ───────────────────────────────────────────────────────────
        cmd.SetComputeBufferParam(cs, k, s_PredictedPositions, model._predictedPositionsBuffer);

        // SpatialHash.SpatialKeys → sorted keys (ascending), consistent with the
        // buffer's CURRENT physical order, since ReorderCopyBack has already
        // rewritten _PredictedPositions into that same sorted order this step.
        cmd.SetComputeBufferParam(cs, k, s_SpatialKeys,   model.spatialHash.SpatialKeys);
        cmd.SetComputeBufferParam(cs, k, s_SpatialOffsets, model.spatialHash.SpatialOffsets);

        // ── Uniforms ──────────────────────────────────────────────────────────
        cmd.SetComputeIntParam   (cs, Config.ParticleCountId,     fm.ParticleCount);
        cmd.SetComputeFloatParam (cs, Config.SmoothingRadiusId,   feature.cachedSmoothingRadius);
        cmd.SetComputeFloatParam (cs, Config.MassId,              feature.cachedMass);
        cmd.SetComputeFloatParam (cs, Config.SpikyPow2Id,         feature.cachedSpikyPow2);
        cmd.SetComputeVectorParam(cs, Config.BoundaryLocalMinId,  feature.cachedBoundsMin);
        cmd.SetComputeVectorParam(cs, Config.BoundaryLocalMaxId,  feature.cachedBoundsMax);
        cmd.SetComputeMatrixParam(cs, s_BoundaryLocalToWorld,     feature.cachedLocalToWorld);

        // ── Output texture ────────────────────────────────────────────────────
        cmd.SetComputeTextureParam(cs, k, s_DensityMap, d.densityMap);
        cmd.SetComputeIntParams   (cs, s_DensityMapSize, new int[] { res, res, res });

        // ── Dispatch — numthreads(8,8,8) ─────────────────────────────────────
        int groups = Mathf.CeilToInt(res / 8f);
        cmd.DispatchCompute(cs, k, groups, groups, groups);
    }

    // ── Density map lifetime ──────────────────────────────────────────────────
    void EnsureDensityMap(int res)
    {
        if (DensityMap != null && _lastResolution == res) return;

        DensityMap?.Release();
        _lastResolution = res;

        // Use RFloat as fallback-safe format; RHalf is fine on desktop/console
        // but can fail on some mobile GPUs with enableRandomWrite on a Tex3D.
        var fmt = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RHalf)
            ? RenderTextureFormat.RHalf
            : RenderTextureFormat.RFloat;

        DensityMap = new RenderTexture(res, res, 0, fmt, RenderTextureReadWrite.Linear)
        {
            dimension         = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth       = res,
            enableRandomWrite = true,
            filterMode        = FilterMode.Bilinear,
            wrapMode          = TextureWrapMode.Clamp,
            name              = "Fluid_DensityMap3D"
        };
        DensityMap.Create();
    }

    public void Dispose()
    {
        DensityMap?.Release();
        DensityMap = null;
    }

    // ── Pass data ─────────────────────────────────────────────────────────────
    class PassData
    {
        public ComputeShader         cs;
        public int                   kernel;
        public RenderTexture         densityMap;
        public FluidManager3D        fm;
        public int                   res;
        public FluidRendererFeature  feature;
    }
}
