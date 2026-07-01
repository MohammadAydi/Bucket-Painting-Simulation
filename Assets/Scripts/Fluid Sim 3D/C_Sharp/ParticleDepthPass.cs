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

    // ── RT handles (shared with other passes via FluidRTPool) ────────────────
    internal static RTHandle s_DepthRT;   // R32_SFloat color — the depth VALUE
    internal static RTHandle s_DepthZRT;  // Depth16 only   — GPU Z-buffer

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

        // Resize / create the two persistent RTs
        FluidRTPool.EnsureDepthRT (ref s_DepthRT,  w, h);
        FluidRTPool.EnsureDepthZRT(ref s_DepthZRT, w, h);

        // Import both into the render graph for this frame
        var colorHandle = renderGraph.ImportTexture(s_DepthRT);
        var depthHandle = renderGraph.ImportTexture(s_DepthZRT);

        // Upload per-frame data to material (outside graph — immediate calls)
        _mat.SetBuffer(s_Positions, fm.PositionsBuffer);
        _mat.SetFloat (s_Scale,     _feature.depthParticleSize);

        // Ensure args buffer
        EnsureArgsBuffer(fm.ParticleCount);

        // ── Record raster pass ───────────────────────────────────────────────
        using (var builder = renderGraph.AddRasterRenderPass<PassData>("Fluid.ParticleDepth", out var data))
        {
            data.material   = _mat;
            data.quad       = _quad;
            data.argsBuffer = _argsBuffer;

            // Color attachment → receives the depth VALUE written to SV_Target
            builder.SetRenderAttachment     (colorHandle, 0, AccessFlags.Write);
            // Depth attachment → separate texture used only for GPU Z-testing
            builder.SetRenderAttachmentDepth(depthHandle,    AccessFlags.Write);

            builder.AllowPassCulling(false);

            builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
            {
                // Clear color to huge sentinel value, clear depth to 1.0
                ctx.cmd.ClearRenderTarget(
                    clearDepth:      true,
                    clearColor:      true,
                    backgroundColor: Color.white * 10_000_000f,
                    depth:           1f);

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
