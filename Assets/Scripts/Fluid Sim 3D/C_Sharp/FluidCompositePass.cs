using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class FluidCompositePass : ScriptableRenderPass, System.IDisposable
{
    static readonly int s_PaintColor       = Shader.PropertyToID("_PaintColor");
    static readonly int s_SpecularStrength = Shader.PropertyToID("_SpecularStrength");
    static readonly int s_Shininess        = Shader.PropertyToID("_Shininess");
    static readonly int s_ReflectStrength  = Shader.PropertyToID("_ReflectStrength");
    static readonly int s_CompTex          = Shader.PropertyToID("_CompTex");
    static readonly int s_NormalTex        = Shader.PropertyToID("_NormalTex");

    FluidRendererFeature _feature;
    Material             _mat;
    static RTHandle      s_OutRT;

    public void Setup(FluidRendererFeature feature)
    {
        _feature = feature;
        renderPassEvent = RenderPassEvent.AfterRenderingTransparents + 4;

        if (_mat == null && feature.fluidCompositeShader)
            _mat = CoreUtils.CreateEngineMaterial(feature.fluidCompositeShader);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_mat == null) return;

        var cameraData   = frameData.Get<UniversalCameraData>();
        var resourceData = frameData.Get<UniversalResourceData>();
        int w = cameraData.cameraTargetDescriptor.width;
        int h = cameraData.cameraTargetDescriptor.height;

        FluidRTPool.EnsureCompositeOutRT(ref s_OutRT, w, h);

        _mat.SetColor(s_PaintColor,       _feature.paintColor);
        _mat.SetFloat(s_SpecularStrength, _feature.specularStrength);
        _mat.SetFloat(s_Shininess,        _feature.specularShininess);
        _mat.SetFloat(s_ReflectStrength,  _feature.reflectionStrength);

        // Bind fluid textures directly on the material — persistent RTHandles,
        // safe to set outside the graph, guaranteed bound when the shader runs.
        _mat.SetTexture(s_CompTex,   PackDepthPass.s_CompRT);
        _mat.SetTexture(s_NormalTex, NormalReconstructPass.s_NormalRT);

        var compHandle   = renderGraph.ImportTexture(PackDepthPass.s_CompRT);
        var normalHandle = renderGraph.ImportTexture(NormalReconstructPass.s_NormalRT);
        var outHandle    = renderGraph.ImportTexture(s_OutRT);
        var colorHandle  = resourceData.activeColorTexture;

        // ── Pass A: Clear outRT to (0,0,0,0), then shade fluid pixels into it ─
        // MUST clear every frame — outRT is persistent and discard() leaves stale
        // data from previous frames, causing paint bleed across the whole scene.
        // After the clear, fluid pixels write (color, alpha=1).
        // Background pixels discard → stay (0,0,0,0).
        using (var builder = renderGraph.AddRasterRenderPass<PassAData>("Fluid.Composite", out var data))
        {
            data.material = _mat;

            builder.UseTexture(compHandle,   AccessFlags.Read);
            builder.UseTexture(normalHandle, AccessFlags.Read);
            builder.SetRenderAttachment(outHandle, 0, AccessFlags.Write);
            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true);

            builder.SetRenderFunc((PassAData d, RasterGraphContext ctx) =>
            {
                // Clear to fully transparent black so background pixels stay alpha=0
                ctx.cmd.ClearRenderTarget(false, true, Color.clear);
                Blitter.BlitTexture(ctx.cmd, new Vector4(1, 1, 0, 0), d.material, 0);
            });
        }

        // ── Pass B: Alpha-blend outRT over camera colour ──────────────────────
        // fluid pixels  (alpha=1) → fully overwrite camera colour
        // background    (alpha=0) → camera colour unchanged
        using (var builder = renderGraph.AddRasterRenderPass<PassBData>("Fluid.CopyToCamera", out var data))
        {
            data.material  = _mat;
            data.outHandle = outHandle;   // store handle in PassData — no lambda closure capture

            builder.UseTexture(outHandle, AccessFlags.Read);
            builder.SetRenderAttachment(colorHandle, 0, AccessFlags.Write);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc((PassBData d, RasterGraphContext ctx) =>
            {
                Blitter.BlitTexture(ctx.cmd, d.outHandle, new Vector4(1, 1, 0, 0), d.material, 1);
            });
        }
    }

    public void Dispose()
    {
        CoreUtils.Destroy(_mat);
        s_OutRT?.Release();
        s_OutRT = null;
    }

    class PassAData { public Material material; }
    class PassBData
    {
        public Material      material;
        public TextureHandle outHandle;
    }
}
