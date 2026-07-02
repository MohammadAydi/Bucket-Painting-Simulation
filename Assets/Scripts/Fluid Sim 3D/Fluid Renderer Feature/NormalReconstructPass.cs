using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class NormalReconstructPass : ScriptableRenderPass, System.IDisposable
{
    internal static RTHandle s_NormalRT;

    static readonly int s_MainTex = Shader.PropertyToID("_MainTex");

    FluidRendererFeature _feature;
    Material             _mat;

    public void Setup(FluidRendererFeature feature)
    {
        _feature = feature;
        renderPassEvent = RenderPassEvent.AfterRenderingOpaques+ 3;

        if (_mat == null && feature.normalsFromDepthShader)
            _mat = CoreUtils.CreateEngineMaterial(feature.normalsFromDepthShader);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_mat == null) return;

        var cameraData = frameData.Get<UniversalCameraData>();
        int w = cameraData.cameraTargetDescriptor.width;
        int h = cameraData.cameraTargetDescriptor.height;

        FluidRTPool.EnsureNormalRT(ref s_NormalRT, w, h);

        // Bind compRT directly on the material — it's a persistent RTHandle so
        // this is safe outside the graph and guaranteed to be set when the shader runs.
        _mat.SetTexture(s_MainTex, PackDepthPass.s_CompRT);

        var compHandle   = renderGraph.ImportTexture(PackDepthPass.s_CompRT);
        var normalHandle = renderGraph.ImportTexture(s_NormalRT);

        using (var builder = renderGraph.AddRasterRenderPass<PassData>("Fluid.NormalReconstruct", out var data))
        {
            data.material = _mat;

            builder.UseTexture(compHandle, AccessFlags.Read);
            builder.SetRenderAttachment(normalHandle, 0, AccessFlags.Write);
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
        s_NormalRT?.Release();
        s_NormalRT = null;
    }

    class PassData { public Material material; }
}
