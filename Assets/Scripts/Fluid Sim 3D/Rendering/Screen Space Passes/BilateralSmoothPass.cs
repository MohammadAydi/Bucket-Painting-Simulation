using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class BilateralSmoothPass : ScriptableRenderPass, System.IDisposable
{
    static readonly int s_WorldRadius        = Shader.PropertyToID("worldRadius");
    static readonly int s_MaxScreenSpaceSize = Shader.PropertyToID("maxScreenSpaceRadius");
    static readonly int s_Strength           = Shader.PropertyToID("strength");
    static readonly int s_DiffStrength       = Shader.PropertyToID("diffStrength");
    static readonly int s_SmoothMask         = Shader.PropertyToID("smoothMask");
    static readonly int s_MainTex            = Shader.PropertyToID("_MainTex");

    FluidRendererFeature _feature;
    Material             _mat1D;
    Material             _mat2D;

    public void Setup(FluidRendererFeature feature)
    {
        _feature = feature;
        renderPassEvent = RenderPassEvent.AfterRenderingTransparents + 2;

        if (_mat1D == null && feature.bilateral1DShader)
            _mat1D = CoreUtils.CreateEngineMaterial(feature.bilateral1DShader);
        if (_mat2D == null && feature.bilateral2DShader)
            _mat2D = CoreUtils.CreateEngineMaterial(feature.bilateral2DShader);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        var s = _feature.bilateralSettings;
        Material activeMat = _feature.blurType == FluidRendererFeature.FluidBlurType.Bilateral1D ? _mat1D : _mat2D;
        if (activeMat == null) return;

        activeMat.SetFloat (s_WorldRadius,        s.worldRadius);
        activeMat.SetInt   (s_MaxScreenSpaceSize, s.maxScreenSpaceSize);
        activeMat.SetFloat (s_Strength,           s.strength);
        activeMat.SetFloat (s_DiffStrength,        s.diffStrength);
        activeMat.SetVector(s_SmoothMask,          new Vector3(1, 1, 0));

        var cameraData = frameData.Get<UniversalCameraData>();
        int w = cameraData.cameraTargetDescriptor.width;
        int h = cameraData.cameraTargetDescriptor.height;

       var compHandle = renderGraph.ImportTexture(PackDepthPass.s_CompRT);
        int iterations = Mathf.Max(1, s.iterations);

        if (_feature.blurType == FluidRendererFeature.FluidBlurType.Bilateral1D)
            RecordBilateral1D(renderGraph, compHandle, w, h, activeMat, iterations);
        else
            RecordBilateral2D(renderGraph, compHandle, w, h, activeMat, iterations);

       if (ParticleDepthPass.s_PigmentColorRT != null)
        {
            var pigmentHandle = renderGraph.ImportTexture(ParticleDepthPass.s_PigmentColorRT);
            activeMat.SetVector(s_SmoothMask, new Vector3(1, 1, 1));

            if (_feature.blurType == FluidRendererFeature.FluidBlurType.Bilateral1D)
                RecordBilateral1D(renderGraph, pigmentHandle, w, h, activeMat, iterations);
            else
                RecordBilateral2D(renderGraph, pigmentHandle, w, h, activeMat, iterations);
        }
    }

    void RecordBilateral1D(RenderGraph rg, TextureHandle compHandle,
                            int w, int h, Material mat, int iterations)
    {
        var tempDesc = new RenderTextureDescriptor(w, h,
            UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat, 0)
        {
            useMipMap        = false,
            autoGenerateMips = false,
        };
        var tempHandle = rg.CreateTexture(new TextureDesc(tempDesc)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
            name       = "Fluid.Bilateral1D_Temp"
        });

        for (int i = 0; i < iterations; i++)
        {
          
            using (var builder = rg.AddRasterRenderPass<BlitPassData>("Fluid.Bilateral1D_H", out var data))
            {
                data.material  = mat;
                data.passIndex = 0;
                data.srcTex    = compHandle;   

                builder.UseTexture(compHandle, AccessFlags.Read);
                builder.SetRenderAttachment(tempHandle, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((BlitPassData d, RasterGraphContext ctx) =>
                {
                    ctx.cmd.SetGlobalTexture(s_MainTex, d.srcTex);
                    Blitter.BlitTexture(ctx.cmd, new Vector4(1, 1, 0, 0), d.material, d.passIndex);
                });
            }

          
            using (var builder = rg.AddRasterRenderPass<BlitPassData>("Fluid.Bilateral1D_V", out var data))
            {
                data.material  = mat;
                data.passIndex = 1;
                data.srcTex    = tempHandle;  

                builder.UseTexture(tempHandle, AccessFlags.Read);
                builder.SetRenderAttachment(compHandle, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((BlitPassData d, RasterGraphContext ctx) =>
                {
                    ctx.cmd.SetGlobalTexture(s_MainTex, d.srcTex);
                    Blitter.BlitTexture(ctx.cmd, new Vector4(1, 1, 0, 0), d.material, d.passIndex);
                });
            }
        }
    }

    void RecordBilateral2D(RenderGraph rg, TextureHandle compHandle,
                            int w, int h, Material mat, int iterations)
    {
        var tempDesc = new RenderTextureDescriptor(w, h,
            UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat, 0)
        {
            useMipMap        = false,
            autoGenerateMips = false,
        };
        var tempHandle = rg.CreateTexture(new TextureDesc(tempDesc)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
            name       = "Fluid.Bilateral2D_Temp"
        });

        for (int i = 0; i < iterations; i++)
        {
            bool even      = (i % 2 == 0);
            TextureHandle src = even ? compHandle : tempHandle;
            TextureHandle dst = even ? tempHandle  : compHandle;

            using (var builder = rg.AddRasterRenderPass<BlitPassData>($"Fluid.Bilateral2D_{i}", out var data))
            {
                data.material  = mat;
                data.passIndex = 0;
                data.srcTex    = src;

                builder.UseTexture(src, AccessFlags.Read);
                builder.SetRenderAttachment(dst, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((BlitPassData d, RasterGraphContext ctx) =>
                {
                    ctx.cmd.SetGlobalTexture(s_MainTex, d.srcTex);
                    Blitter.BlitTexture(ctx.cmd, new Vector4(1, 1, 0, 0), d.material, d.passIndex);
                });
            }
        }

       
        if (iterations % 2 != 0)
        {
            using (var builder = rg.AddRasterRenderPass<BlitPassData>("Fluid.Bilateral2D_Finalize", out var data))
            {
                data.material  = mat;
                data.passIndex = 0;
                data.srcTex    = tempHandle;

                builder.UseTexture(tempHandle, AccessFlags.Read);
                builder.SetRenderAttachment(compHandle, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((BlitPassData d, RasterGraphContext ctx) =>
                {
                    ctx.cmd.SetGlobalTexture(s_MainTex, d.srcTex);
                    Blitter.BlitTexture(ctx.cmd, new Vector4(1, 1, 0, 0), d.material, d.passIndex);
                });
            }
        }
    }

    public void Dispose()
    {
        CoreUtils.Destroy(_mat1D);
        CoreUtils.Destroy(_mat2D);
    }

    class BlitPassData
    {
        public Material      material;
        public int           passIndex;
        public TextureHandle srcTex; 
    }
}