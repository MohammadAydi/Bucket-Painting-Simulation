using UnityEngine;
using static Fluid_Sim_3D.Utilities.ComputeHelper;


class NeighborsSearching
{

    FluidModel _model;
    readonly ComputeShader _compute;
    protected readonly int _reorderKernel;
    protected readonly int _reorderCopybackKernel;
    protected readonly int _buildSpatialLookupKernel;
    protected int realGroups;


    public NeighborsSearching(FluidModel model, ComputeShader compute)
    {
        _model = model;
        _compute = compute;
        _reorderKernel = _compute.FindKernel("Reorder");
        _reorderCopybackKernel = _compute.FindKernel("ReorderCopyBack");
        _buildSpatialLookupKernel = _compute.FindKernel("BuildSpatialLookup");
        realGroups = Mathf.CeilToInt(_model.ParticleCount / (float)Config.ThreadsPerGroup);
    }

    public void BindsBuffers()
    {
        SetBuffers(_compute, _buildSpatialLookupKernel, _model.bufferNameLookup, new ComputeBuffer[]
       {
            _model.spatialHash.SpatialKeys,
            // spatialHash.SpatialOffsets,
            // _predictedPositionsBuffer,
            _model._predictedPositionsBuffer,
           // spatialHash.SpatialIndices,
       });
        SetBuffers(_compute, _reorderKernel, _model.bufferNameLookup, new ComputeBuffer[]
                {
                _model.PositionsBuffer,
                _model.sortTarget_positionBuffer,
                _model._predictedPositionsBuffer,
                _model.sortTarget_predictedPositionsBuffer,
                _model.VelocitiesBuffer,
                _model.sortTarget_velocityBuffer,
                _model.spatialHash.SpatialIndices
                });

        // Reorder copyback kernel
        SetBuffers(_compute, _reorderCopybackKernel, _model.bufferNameLookup, new ComputeBuffer[]
        {
                _model.PositionsBuffer,
                _model.sortTarget_positionBuffer,
                _model._predictedPositionsBuffer,
                _model.sortTarget_predictedPositionsBuffer,
                _model.VelocitiesBuffer,
                _model.sortTarget_velocityBuffer,
                // spatialHash.SpatialIndices
        });
    }

    public void BindStaticUniforms(ParticleSettings settings)
    {
        _compute.SetInt(Config.ParticleCountId, _model.ParticleCount);
        _compute.SetFloat(Config.SmoothingRadiusId, settings.smoothingRadius);
    }

    public void Dispatch()
    {
        _compute.Dispatch(_buildSpatialLookupKernel, realGroups, 1, 1);
        _model.spatialHash.Run();
        _compute.Dispatch(_reorderKernel, realGroups, 1, 1);
        _compute.Dispatch(_reorderCopybackKernel, realGroups, 1, 1);
    }

}