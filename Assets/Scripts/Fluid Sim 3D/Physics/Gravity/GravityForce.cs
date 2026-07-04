using UnityEngine;
using static Fluid_Sim_3D.Utilities.ComputeHelper;

class GravityForce : GPUExecuter
{
    public GravityForce(FluidModel model, ComputeShader compute, string kernelName) : base(model, compute, kernelName)
    {
    }

    public override void BindsBuffers()
    {
        SetBuffers(_compute, _kernel, _model.bufferNameLookup, new ComputeBuffer[]
        {
            _model.PositionsBuffer,
            _model._predictedPositionsBuffer,
            _model.VelocitiesBuffer,
        });
    }
}