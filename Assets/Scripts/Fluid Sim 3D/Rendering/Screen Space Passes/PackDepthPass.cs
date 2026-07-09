
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class PackDepthPass : ScriptableRenderPass, System.IDisposable
{
    static readonly int s_DepthTex = Shader.PropertyToID("Depth");

    internal static RTHandle s_CompRT;   

    FluidRendererFeature _feature;
    Material             _mat;

   public void Setup(FluidRendererFeature feature)
    {
        _feature = feature;
        renderPassEvent = RenderPassEvent.AfterRenderingTransparents+ 1;

        if (_mat == null && feature.packDepthShader)
            _mat = CoreUtils.CreateEngineMaterial(feature.packDepthShader);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_mat == null) return;

        var cameraData = frameData.Get<UniversalCameraData>();
        int w = cameraData.cameraTargetDescriptor.width;
        int h = cameraData.cameraTargetDescriptor.height;

        FluidRTPool.EnsureCompRT(ref s_CompRT, w, h);

     
        var srcHandle  = renderGraph.ImportTexture(ParticleDepthPass.s_DepthRT);
        var compHandle = renderGraph.ImportTexture(s_CompRT);

        
        _mat.SetTexture(s_DepthTex, ParticleDepthPass.s_DepthRT);

        using (var builder = renderGraph.AddRasterRenderPass<PassData>("Fluid.PackDepth", out var data))
        {
            data.material   = _mat;

            builder.UseTexture(srcHandle,  AccessFlags.Read);
            builder.SetRenderAttachment(compHandle, 0, AccessFlags.Write);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
            {
               
                Blitter.BlitTexture(ctx.cmd, new Vector4(1, 1, 0, 0), d.material, 0);
            });
        }
    }

    public void Dispose()
    {
        CoreUtils.Destroy(_mat);
    }

    class PassData
    {
        public Material material;
    }
}
