using UnityEngine;
using static Fluid_Sim_3D.Utilities.ComputeHelper;


class PigmentDiffusion : GPUExecuter
{
    readonly Texture2D _mixboxLUT;

    public PigmentDiffusion(FluidModel model, ComputeShader compute, string kernelName, Texture2D mixboxLUT = null)
        : base(model, compute, kernelName)
    {
        _mixboxLUT = mixboxLUT;
    }

    public override void BindsBuffers()
    {
        SetBuffers(_compute, _kernel, _model.bufferNameLookup, new ComputeBuffer[]
        {
            _model._predictedPositionsBuffer,
            _model._densityBuffer,
            _model.spatialHash.SpatialKeys,
            _model.spatialHash.SpatialOffsets,
            _model.PigmentBuffer,
            _model.PigmentBufferWrite,
        });

        if (_mixboxLUT)
            _compute.SetTexture(_kernel, "_MixboxLUT", _mixboxLUT);
    }
}