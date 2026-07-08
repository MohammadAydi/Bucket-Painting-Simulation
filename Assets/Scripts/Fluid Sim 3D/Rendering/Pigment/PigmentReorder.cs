using UnityEngine;
using static Fluid_Sim_3D.Utilities.ComputeHelper;

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
        SetBuffers(_compute, _kernel, _model.bufferNameLookup, new ComputeBuffer[]
        {
            _model.PigmentBuffer,
            _model.SortTarget_PigmentBuffer,
            _model.spatialHash.SpatialIndices,  
        });

        SetBuffers(_compute, _copybackKernel, _model.bufferNameLookup, new ComputeBuffer[]
        {
            _model.PigmentBuffer,
            _model.SortTarget_PigmentBuffer,
        });
    }

    public void DispatchReorder()  => _compute.Dispatch(_kernel,         realGroups, 1, 1);
    public void DispatchCopyBack() => _compute.Dispatch(_copybackKernel, realGroups, 1, 1);

}
