using UnityEngine;
using static Fluid_Sim_3D.Utilities.ComputeHelper;

// ─────────────────────────────────────────────────────────────────────────────
// PigmentDiffusion
// GPUExecuter for the pigment diffusion kernel.
// Follows the same pattern as StandardViscosity:
//   - inherits GPUExecuter
//   - binds only the buffers its kernel needs
//   - is instantiated and stored in FluidModel
// ─────────────────────────────────────────────────────────────────────────────
class PigmentDiffusion : GPUExecuter
{
    public PigmentDiffusion(FluidModel model, ComputeShader compute, string kernelName)
        : base(model, compute, kernelName)
    {
    }

    public override void BindsBuffers()
    {
        SetBuffers(_compute, _kernel, _model.bufferNameLookup, new ComputeBuffer[]
        {
            _model._predictedPositionsBuffer,   // read particle positions for neighbor search
            _model._densityBuffer,              // read density for SPH weight normalization
            _model.spatialHash.SpatialKeys,     // sorted cell keys
            _model.spatialHash.SpatialOffsets,  // cell start offsets
            _model.PigmentBuffer,               // read current pigment values
            _model.PigmentBufferWrite,          // write diffused pigment values (double-buffer)
        });
    }
}
