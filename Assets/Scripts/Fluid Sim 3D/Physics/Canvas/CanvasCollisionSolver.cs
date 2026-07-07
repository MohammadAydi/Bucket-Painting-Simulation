using UnityEngine;
using static Fluid_Sim_3D.Utilities.ComputeHelper;

// Resolves particle collision against the finite canvas plane.
// Mirrors Integrator's structure: only needs positions/velocities bound.
class CanvasCollisionSolver : GPUExecuter
{
    public CanvasCollisionSolver(FluidModel model, ComputeShader compute, string kernelName) : base(model, compute, kernelName)
    {
    }

    public override void BindsBuffers()
    {
        SetBuffers(_compute, _kernel, _model.bufferNameLookup, new ComputeBuffer[]
        {
            _model.PositionsBuffer,
            _model._predictedPositionsBuffer,
            _model.VelocitiesBuffer,
            _model._debugBuffer,
        });
    }
}
