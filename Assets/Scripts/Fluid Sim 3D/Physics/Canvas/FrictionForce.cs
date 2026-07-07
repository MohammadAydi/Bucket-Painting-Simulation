using UnityEngine;
using static Fluid_Sim_3D.Utilities.ComputeHelper;

// Applies canvas surface friction (formula f_k = -max((1 - delta*d_k)^2, 0) * v_k)
// to particles near/over the canvas.
class FrictionForce : GPUExecuter
{
    public FrictionForce(FluidModel model, ComputeShader compute, string kernelName) : base(model, compute, kernelName)
    {
    }

    public override void BindsBuffers()
    {
        SetBuffers(_compute, _kernel, _model.bufferNameLookup, new ComputeBuffer[]
        {
            _model.PositionsBuffer,
            _model.VelocitiesBuffer,
        });
    }
}
