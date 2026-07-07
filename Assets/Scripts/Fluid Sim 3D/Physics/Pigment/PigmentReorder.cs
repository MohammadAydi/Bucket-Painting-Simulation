using UnityEngine;
using static Fluid_Sim_3D.Utilities.ComputeHelper;

// ─────────────────────────────────────────────────────────────────────────────
// PigmentReorder
// GPUExecuter that participates in the spatial-hash reorder step.
// Every frame NeighborsSearching runs:
//   1. BuildSpatialLookup   — fills SortedIndices (particle → sorted slot)
//   2. GPUCountSort         — sorts SortedIndices by SpatialKeys
//   3. Reorder              — Sorted[i] = Source[SortedIndices[i]]   (positions/vel)
//   4. ReorderCopyBack      — Source[i] = Sorted[i]
//
// PigmentReorder hooks into step 3+4 via two separate kernels:
//   PigmentReorderKernel      — SortTarget_Pigment[i] = Pigment[SortedIndices[i]]
//   PigmentReorderCopyBack    — Pigment[i] = SortTarget_Pigment[i]
//
// This keeps pigment data in the same sorted order as positions/velocities so
// every other kernel can address pigment[i] and position[i] for the same particle.
// ─────────────────────────────────────────────────────────────────────────────
class PigmentReorder : GPUExecuter
{
    readonly int _copybackKernel;

    public PigmentReorder(FluidModel model, ComputeShader compute,
                          string reorderKernelName, string copybackKernelName)
        : base(model, compute, reorderKernelName)
    {
        _copybackKernel = _compute.FindKernel(copybackKernelName);
    }

    public override void BindsBuffers()
    {
        // Reorder kernel: scatter pigment into sort target using SortedIndices
        SetBuffers(_compute, _kernel, _model.bufferNameLookup, new ComputeBuffer[]
        {
            _model.PigmentBuffer,
            _model.SortTarget_PigmentBuffer,
            _model.spatialHash.SpatialIndices,  // SortedIndices — same buffer as positions use
        });

        // CopyBack kernel: overwrite live buffer from sort target
        SetBuffers(_compute, _copybackKernel, _model.bufferNameLookup, new ComputeBuffer[]
        {
            _model.PigmentBuffer,
            _model.SortTarget_PigmentBuffer,
        });
    }

    // Dispatch both kernels (called from NeighborsSearching after the sort)
    public void DispatchReorder()  => _compute.Dispatch(_kernel,         realGroups, 1, 1);
    public void DispatchCopyBack() => _compute.Dispatch(_copybackKernel, realGroups, 1, 1);

    // Base Dispatch() is unused — use the two explicit methods above.
    // It is still valid to call for compatibility with the GPUExecuter interface,
    // but calling both named methods is clearer at the call site.
}
