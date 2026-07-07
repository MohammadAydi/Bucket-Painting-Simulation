// ParticleDepthPass.cs
// ──────────────────────────────────────────────────────────────────────────────
// Pass 1: Draw billboard quads for every particle into a custom R32_SFloat
//         color render target, with a SEPARATE depth buffer for Z-testing.
//
// WHY TWO TEXTURES:
//   URP Render Graph does not allow the same RTHandle to be bound as both
//   a color attachment (SetRenderAttachment) AND a depth attachment
//   (SetRenderDepthAttachment). So we use:
//     s_DepthRT  = R32_SFloat color  → stores the depth value in .r
//     s_DepthZRT = Depth16 only      → GPU Z-buffer for depth testing only
//
// WHY AfterRenderingSkybox (not AfterRenderingOpaques):
//   Running at AfterRenderingOpaques conflicts with URP's internal ZBinningJob
//   in Unity 6 / URP 17, causing an InvalidOperationException. Moving one
//   event later avoids the race condition.
// ──────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class ParticleDepthPass : ScriptableRenderPass, System.IDisposable
{
    // ── Shader property IDs ──────────────────────────────────────────────────
    static readonly int s_Positions = Shader.PropertyToID("Positions");
    static readonly int s_Scale     = Shader.PropertyToID("scale");
    static readonly int s_Pigments  = Shader.PropertyToID("_Pigments");

    // ── RT handles (shared with other passes via FluidRTPool) ────────────────
    internal static RTHandle s_DepthRT;        // R32_SFloat color — the depth VALUE
    internal static RTHandle s_DepthZRT;       // Depth16 only   — GPU Z-buffer
    internal static RTHandle s_PigmentColorRT; // RGBA16F — per-particle pigment color

    // ── State ────────────────────────────────────────────────────────────────
    FluidRendererFeature _feature;
    Material             _mat;
    Mesh                 _quad;
    ComputeBuffer        _argsBuffer;
    int                  _lastParticleCount = -1;

    // ─────────────────────────────────────────────────────────────────────────
    public void Setup(FluidRendererFeature feature)
    {
        _feature = feature;

        // AfterRenderingSkybox avoids the ZBinningJob conflict in Unity 6 URP 17
        renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
     //   ConfigureInput(ScriptableRenderPassInput.Depth);
        if (_mat == null && feature.particleDepthShader)
            _mat = CoreUtils.CreateEngineMaterial(feature.particleDepthShader);

        if (_quad == null)
            _quad = FluidMeshUtils.CreateQuad();
    }

    // ── Render Graph entry point ──────────────────────────────────────────────
    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_mat == null || _feature?.fluidManager == null) return;
        var fm = _feature.fluidManager;
        if (fm.PositionsBuffer == null || fm.ParticleCount == 0) return;

        var cameraData = frameData.Get<UniversalCameraData>();
        int w = cameraData.cameraTargetDescriptor.width;
        int h = cameraData.cameraTargetDescriptor.height;

        // Resize / create the persistent RTs
        FluidRTPool.EnsureDepthRT       (ref s_DepthRT,        w, h);
        FluidRTPool.EnsureDepthZRT      (ref s_DepthZRT,       w, h);
        FluidRTPool.EnsurePigmentColorRT(ref s_PigmentColorRT, w, h);
        
        var resourceData = frameData.Get<UniversalResourceData>();

        var depthColorHandle  = renderGraph.ImportTexture(s_DepthRT);
        var pigmentHandle     = renderGraph.ImportTexture(s_PigmentColorRT);
        var sceneDepthHandle  = resourceData.activeDepthTexture;

        // Upload per-frame data to material (outside graph — immediate calls)
        _mat.SetBuffer(s_Positions, fm.PositionsBuffer);
        _mat.SetFloat (s_Scale,     _feature.depthParticleSize);

        // Bind pigment buffer when available (null-safe: shader uses white fallback)
        if (fm.PigmentBuffer != null)
            _mat.SetBuffer(s_Pigments, fm.PigmentBuffer);

        // Ensure args buffer
        EnsureArgsBuffer(fm.ParticleCount);

        // ── Record raster pass (MRT: SV_Target0 = depth, SV_Target1 = pigment) ─
        using (var builder = renderGraph.AddRasterRenderPass<PassData>("Fluid.ParticleDepth", out var data))
        {
            data.material   = _mat;
            data.quad       = _quad;
            data.argsBuffer = _argsBuffer;

            // Target 0: depth value (R32_SFloat)
            builder.SetRenderAttachment(depthColorHandle, 0, AccessFlags.Write);
            // Target 1: pigment color (RGBA16F)
            builder.SetRenderAttachment(pigmentHandle,    1, AccessFlags.Write);
            // Depth attachment for Z-testing
            builder.SetRenderAttachmentDepth(sceneDepthHandle, AccessFlags.Write);

            builder.AllowPassCulling(false);

            builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
            {
                // Clear depth-color RT to sentinel; pigment RT to black-transparent
                ctx.cmd.ClearRenderTarget(false, true, Color.white * 10_000_000f);
                ctx.cmd.DrawMeshInstancedIndirect(d.quad, 0, d.material, 0, d.argsBuffer);
            });
        }
    }

    // ── Args buffer helpers ───────────────────────────────────────────────────
    void EnsureArgsBuffer(int count)
    {
        if (_argsBuffer == null || !_argsBuffer.IsValid() || count != _lastParticleCount)
        {
            _argsBuffer?.Release();
            _argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            _lastParticleCount = count;
        }

        _argsBuffer.SetData(new uint[]
        {
            _quad.GetIndexCount(0),
            (uint)_lastParticleCount,
            _quad.GetIndexStart(0),
            _quad.GetBaseVertex(0),
            0
        });
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────
    public void Dispose()
    {
        _argsBuffer?.Release();
        CoreUtils.Destroy(_mat);
        CoreUtils.Destroy(_quad);
    }

    // ── Pass data struct (for the render graph lambda) ────────────────────────
    class PassData
    {
        public Material      material;
        public Mesh          quad;
        public ComputeBuffer argsBuffer;
    }
}
