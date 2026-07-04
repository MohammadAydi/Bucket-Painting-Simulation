using UnityEngine;
using static Fluid_Sim_3D.Utilities.ComputeHelper;
class DensityCalc : GPUExecuter
{
    public DensityCalc(FluidModel model, ComputeShader compute, string kernelName) : base(model, compute, kernelName)
    {
    }
    public override void BindsBuffers()
    {
        SetBuffers(_compute, _kernel, _model.bufferNameLookup, new ComputeBuffer[]
{
            _model._predictedPositionsBuffer,
            // PositionsBuffer,
            _model._densityBuffer,
            _model.spatialHash.SpatialKeys,
            _model.spatialHash.SpatialOffsets,
        });
    }
}