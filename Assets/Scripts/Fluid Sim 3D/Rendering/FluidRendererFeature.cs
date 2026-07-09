using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

[System.Serializable]
public class FluidRendererFeature : ScriptableRendererFeature
{
    
    public enum RenderMode
    {
        FluidSurface, 
        VelocityDebug
    }

    private Shader _particleDepthShader;
    private Shader _packDepthShader;
    private Shader _bilateral1DShader;
    private Shader _bilateral2DShader;
    private Shader _normalsFromDepthShader;
    private Shader _fluidCompositeShader;
    private Shader _velocityDebugShader;

    public Shader particleDepthShader => _particleDepthShader;
    public Shader packDepthShader => _packDepthShader;
    public Shader bilateral1DShader => _bilateral1DShader;
    public Shader bilateral2DShader => _bilateral2DShader;
    public Shader normalsFromDepthShader => _normalsFromDepthShader;
    public Shader fluidCompositeShader => _fluidCompositeShader;
    public Shader velocityDebugShader => _velocityDebugShader;

   
    [System.NonSerialized] public FluidManager3D fluidManager;
    [System.NonSerialized] public FluidRendererSettings settings;


    public enum FluidBlurType
    {
        Bilateral1D,
        Bilateral2D
    }

    [System.Serializable]
    public struct BilateralFilterSettings
    {
        public float worldRadius;
        public int maxScreenSpaceSize;
        [Range(0f, 1f)] public float strength;
        public float diffStrength;
        public int iterations;
    }

  
    public ComputeBuffer pigmentBuffer => fluidManager?.PigmentBuffer;

   
    public RenderMode renderMode => settings ? settings.renderMode : RenderMode.FluidSurface;
    public float depthParticleSize => settings ? settings.depthParticleSize : 0.15f;
    public FluidBlurType blurType => settings ? settings.blurType : FluidBlurType.Bilateral1D;

    public BilateralFilterSettings bilateralSettings => settings
        ? settings.bilateralSettings
        : new BilateralFilterSettings
            { worldRadius = 0.3f, maxScreenSpaceSize = 40, strength = 0.5f, diffStrength = 20f, iterations = 3 };

    public float specularStrength => settings ? settings.specularStrength : 0.8f;
    public float specularShininess => settings ? settings.specularShininess : 64f;
    public float reflectionStrength => settings ? settings.reflectionStrength : 0.15f;
    public float ambientStrength => settings ? settings.ambientStrength : 0.25f;
    public bool useHalfLambert => settings ? settings.useHalfLambert : true;
    public float fillLightStrength => settings ? settings.fillLightStrength : 0.2f;
    public Color fillLightColor => settings ? settings.fillLightColor : new Color(0.4f, 0.35f, 0.3f);
  
    ParticleDepthPass _depthPass;
    PackDepthPass _packPass;
    BilateralSmoothPass _bilateralPass;
    NormalReconstructPass _normalPass;
    FluidCompositePass _compositePass;
    VelocityDebugPass _velocityDebugPass; 

    public override void Create()
    {
        LoadShaders();
        _depthPass = new ParticleDepthPass();
        _packPass = new PackDepthPass();
        _bilateralPass = new BilateralSmoothPass();
        _normalPass = new NormalReconstructPass();
        _compositePass = new FluidCompositePass();
        _velocityDebugPass = new VelocityDebugPass();
    }

    private void LoadShaders()
    {
        _particleDepthShader = Resources.Load<Shader>("Shaders/ParticleDepth");
        _packDepthShader = Resources.Load<Shader>("Shaders/PackDepth");
        _bilateral1DShader = Resources.Load<Shader>("Shaders/Bilateral1D");
        _bilateral2DShader = Resources.Load<Shader>("Shaders/Bilateral2D");
        _normalsFromDepthShader = Resources.Load<Shader>("Shaders/NormalsFromDepth");
        _fluidCompositeShader = Resources.Load<Shader>("Shaders/FluidComposite");
        _velocityDebugShader = Resources.Load<Shader>("Shaders/ParticleCircle3D");

        Debug.Assert(_particleDepthShader, "Missing shader: Fluid/Shaders/ParticleDepth");
        Debug.Assert(_packDepthShader, "Missing shader: Fluid/Shaders/PackDepth");
        Debug.Assert(_bilateral1DShader, "Missing shader: Fluid/Shaders/Bilateral1D");
        Debug.Assert(_bilateral2DShader, "Missing shader: Fluid/Shaders/Bilateral2D");
        Debug.Assert(_normalsFromDepthShader, "Missing shader: Fluid/Shaders/NormalsFromDepth");
        Debug.Assert(_fluidCompositeShader, "Missing shader: Fluid/Shaders/FluidComposite");
        Debug.Assert(_velocityDebugShader, "Missing shader: Fluid/Shaders/VelocityDebug");
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
       
        if (fluidManager == null) fluidManager = Object.FindFirstObjectByType<FluidManager3D>();
        if (settings == null) settings = Object.FindFirstObjectByType<FluidRendererSettings>();

        if (fluidManager == null || fluidManager.PositionsBuffer == null || fluidManager.ParticleCount == 0) return;
        if (renderingData.cameraData.cameraType == CameraType.Preview) return;

        if (renderMode == RenderMode.VelocityDebug)
        {
          
            if (_velocityDebugShader == null)
            {
                Debug.LogError("[FluidRenderer] _velocityDebugShader missing! Assign ParticleCircle3D.");
                return;
            }

            _velocityDebugPass.Setup(this);
            renderer.EnqueuePass(_velocityDebugPass);
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
        _velocityDebugPass?.Dispose();
    }

    bool ValidateShaders()
    {
        bool ok = true;
        if (!_particleDepthShader)
        {
            Debug.LogError("[FluidRenderer] _particleDepthShader missing!");
            ok = false;
        }

        if (!_packDepthShader)
        {
            Debug.LogError("[FluidRenderer] _packDepthShader missing!");
            ok = false;
        }

        if (!_normalsFromDepthShader)
        {
            Debug.LogError("[FluidRenderer] _normalsFromDepthShader missing!");
            ok = false;
        }

        if (!_fluidCompositeShader)
        {
            Debug.LogError("[FluidRenderer] _fluidCompositeShader missing!");
            ok = false;
        }

        if (!_bilateral1DShader && blurType == FluidBlurType.Bilateral1D)
        {
            Debug.LogError("[FluidRenderer] _bilateral1DShader missing!");
            ok = false;
        }

        if (!_bilateral2DShader && blurType == FluidBlurType.Bilateral2D)
        {
            Debug.LogError("[FluidRenderer] _bilateral2DShader missing!");
            ok = false;
        }

        return ok;
    }
}