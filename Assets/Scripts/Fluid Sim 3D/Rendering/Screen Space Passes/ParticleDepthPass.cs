using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class ParticleDepthPass : ScriptableRenderPass, System.IDisposable
{
    static readonly int s_Positions = Shader.PropertyToID("Positions");
    static readonly int s_Scale = Shader.PropertyToID("scale");
    static readonly int s_Pigments = Shader.PropertyToID("_Pigments");


    internal static RTHandle s_DepthRT;
    internal static RTHandle s_DepthZRT;
    internal static RTHandle s_PigmentColorRT;


    FluidRendererFeature _feature;
    Material _mat;
    Mesh _quad;
    ComputeBuffer _argsBuffer;
    int _lastParticleCount = -1;


    public void Setup(FluidRendererFeature feature)
    {
        _feature = feature;


        renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        if (_mat == null && feature.particleDepthShader)
            _mat = CoreUtils.CreateEngineMaterial(feature.particleDepthShader);

        if (_quad == null)
            _quad = FluidMeshUtils.CreateQuad();
    }


    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_mat == null || _feature?.fluidManager == null) return;
        var fm = _feature.fluidManager;
        if (fm.PositionsBuffer == null || fm.ParticleCount == 0) return;

        var cameraData = frameData.Get<UniversalCameraData>();
        int w = cameraData.cameraTargetDescriptor.width;
        int h = cameraData.cameraTargetDescriptor.height;


        FluidRTPool.EnsureDepthRT(ref s_DepthRT, w, h);
        FluidRTPool.EnsureDepthZRT(ref s_DepthZRT, w, h);
        FluidRTPool.EnsurePigmentColorRT(ref s_PigmentColorRT, w, h);

        var resourceData = frameData.Get<UniversalResourceData>();

        var depthColorHandle = renderGraph.ImportTexture(s_DepthRT);
        var pigmentHandle = renderGraph.ImportTexture(s_PigmentColorRT);
        var sceneDepthHandle = resourceData.activeDepthTexture;

        _mat.SetBuffer(s_Positions, fm.PositionsBuffer);
        _mat.SetFloat(s_Scale, _feature.depthParticleSize);

        if (fm.PigmentBuffer != null)
            _mat.SetBuffer(s_Pigments, fm.PigmentBuffer);

        EnsureArgsBuffer(fm.ParticleCount);

        using (var builder = renderGraph.AddRasterRenderPass<PassData>("Fluid.ParticleDepth", out var data))
        {
            data.material = _mat;
            data.quad = _quad;
            data.argsBuffer = _argsBuffer;

            builder.SetRenderAttachment(depthColorHandle, 0, AccessFlags.Write);
            builder.SetRenderAttachment(pigmentHandle, 1, AccessFlags.Write);
            builder.SetRenderAttachmentDepth(sceneDepthHandle, AccessFlags.Write);

            builder.AllowPassCulling(false);

            builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
            {
                ctx.cmd.ClearRenderTarget(false, true, Color.white * 10_000_000f);
                ctx.cmd.DrawMeshInstancedIndirect(d.quad, 0, d.material, 0, d.argsBuffer);
            });
        }
    }

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

    public void Dispose()
    {
        _argsBuffer?.Release();
        CoreUtils.Destroy(_mat);
        CoreUtils.Destroy(_quad);
    }

    class PassData
    {
        public Material material;
        public Mesh quad;
        public ComputeBuffer argsBuffer;
    }
}