using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class FluidCompositePass : ScriptableRenderPass, System.IDisposable
{
    //static readonly int s_PaintColor        = Shader.PropertyToID("_PaintColor");
    static readonly int s_SpecularStrength  = Shader.PropertyToID("_SpecularStrength");
    static readonly int s_Shininess         = Shader.PropertyToID("_Shininess");
    static readonly int s_ReflectStrength   = Shader.PropertyToID("_ReflectStrength");
    static readonly int s_CompTex           = Shader.PropertyToID("_CompTex");
    static readonly int s_NormalTex         = Shader.PropertyToID("_NormalTex");
    static readonly int s_PigmentColorTex   = Shader.PropertyToID("_PigmentColorTex");
    static readonly int s_AmbientStrength   = Shader.PropertyToID("_AmbientStrength");
    static readonly int s_UseHalfLambert    = Shader.PropertyToID("_UseHalfLambert");
    static readonly int s_FillLightStrength = Shader.PropertyToID("_FillLightStrength");
    static readonly int s_FillLightColor    = Shader.PropertyToID("_FillLightColor");

    FluidRendererFeature _feature;
    Material             _mat;
    static RTHandle      s_OutRT;

    public void Setup(FluidRendererFeature feature)
    {
        _feature = feature;
        renderPassEvent = RenderPassEvent.AfterRenderingTransparents+ 4;
   
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

       
        _mat.SetFloat(s_SpecularStrength,  _feature.specularStrength);
        _mat.SetFloat(s_Shininess,         _feature.specularShininess);
        _mat.SetFloat(s_ReflectStrength,   _feature.reflectionStrength);
        _mat.SetFloat(s_AmbientStrength,   _feature.ambientStrength);
        _mat.SetFloat(s_UseHalfLambert,    _feature.useHalfLambert ? 1f : 0f);
        _mat.SetFloat(s_FillLightStrength, _feature.fillLightStrength);
        _mat.SetColor(s_FillLightColor,    _feature.fillLightColor);

      
        _mat.SetTexture(s_CompTex,        PackDepthPass.s_CompRT);
        _mat.SetTexture(s_NormalTex,      NormalReconstructPass.s_NormalRT);
        _mat.SetTexture(s_PigmentColorTex, ParticleDepthPass.s_PigmentColorRT);

        var compHandle        = renderGraph.ImportTexture(PackDepthPass.s_CompRT);
        var normalHandle      = renderGraph.ImportTexture(NormalReconstructPass.s_NormalRT);
        var pigmentHandle     = renderGraph.ImportTexture(ParticleDepthPass.s_PigmentColorRT);
        var outHandle         = renderGraph.ImportTexture(s_OutRT);
        var colorHandle       = resourceData.activeColorTexture;
        var sceneDepthHandle  = resourceData.cameraDepthTexture;

       
        using (var builder = renderGraph.AddRasterRenderPass<PassAData>("Fluid.Composite", out var data))
        {
            data.material = _mat;

            builder.UseTexture(compHandle,    AccessFlags.Read);
            builder.UseTexture(normalHandle,  AccessFlags.Read);
            builder.UseTexture(pigmentHandle, AccessFlags.Read);
            builder.SetRenderAttachment(outHandle, 0, AccessFlags.Write);
            
            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true);

            builder.SetRenderFunc((PassAData d, RasterGraphContext ctx) =>
            {
                
                ctx.cmd.ClearRenderTarget(false, true, Color.clear);
                Blitter.BlitTexture(ctx.cmd, new Vector4(1, 1, 0, 0), d.material, 0);
            });
        }

      
        using (var builder = renderGraph.AddRasterRenderPass<PassBData>("Fluid.CopyToCamera", out var data))
        {
            data.material  = _mat;
            data.outHandle = outHandle;   

            builder.UseTexture(outHandle, AccessFlags.Read);
            builder.UseTexture(sceneDepthHandle, AccessFlags.Read);   
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