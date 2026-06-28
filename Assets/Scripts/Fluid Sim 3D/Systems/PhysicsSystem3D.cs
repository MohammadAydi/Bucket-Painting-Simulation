using System;
using UnityEngine;
using GPUSorting.Runtime;
using Unity.Mathematics;
using static Fluid_Sim_3D.Utilities.ComputeHelper;
using System.Collections.Generic;
using Fluid_Sim_3D.Utilities.SpatialHash;

public sealed class PhysicsSystem3D : IDisposable
{
    const int ThreadsPerGroup = 64;
    const int ParticleStride = 72; // 4x Vector4 + 2x float
    // const int SpatialEntryStride = 8; // 2x uint

    static readonly int ParticleCountId = Shader.PropertyToID("_ParticleCount");
    // static readonly int PaddedCountId = Shader.PropertyToID("_PaddedCount");
    // static readonly int KId = Shader.PropertyToID("_K");
    // static readonly int JId = Shader.PropertyToID("_J");
    static readonly int DeltaTimeId = Shader.PropertyToID("_DeltaTime");
    static readonly int MassId = Shader.PropertyToID("_Mass");
    static readonly int GravityId = Shader.PropertyToID("_Gravity");
    static readonly int SmoothingRadiusId = Shader.PropertyToID("_SmoothingRadius");
    static readonly int PressureMultiplierId = Shader.PropertyToID("_PressureMultiplier");
    static readonly int TargetDensityId = Shader.PropertyToID("_TargetDensity");
    static readonly int ParticleRadiusId = Shader.PropertyToID("_ParticleRadius");
    static readonly int CollisionDampingId = Shader.PropertyToID("_CollisionDamping");

    static readonly int BoundaryLocalMinId = Shader.PropertyToID("_BoundaryLocalMin");
    static readonly int BoundaryLocalMaxId = Shader.PropertyToID("_BoundaryLocalMax");
    static readonly int BoundaryWorldToLocalId = Shader.PropertyToID("_BoundaryWorldToLocal");
    static readonly int BoundaryLocalToWorldId = Shader.PropertyToID("_BoundaryLocalToWorld");

    static readonly int InteractionInputPosId = Shader.PropertyToID("_InteractionInputPos");
    static readonly int InteractionRadiusId = Shader.PropertyToID("_InteractionRadius");
    static readonly int InteractionStrengthId = Shader.PropertyToID("_InteractionStrength");

    static readonly int ViscosityCoeffId = Shader.PropertyToID("_ViscosityCoeff");
    static readonly int SurfaceTensionCoeffId = Shader.PropertyToID("_SurfaceTensionCoeff");
    static readonly int SurfaceTensionThresholdId = Shader.PropertyToID("_SurfaceTensionThreshold");



    // static readonly int ParticlesId = Shader.PropertyToID("_Particles");
    // static readonly int SpatialLookupId = Shader.PropertyToID("_SpatialLookup");

    static readonly int PositionsId = Shader.PropertyToID("_Positions");
    static readonly int PredictedPositionsId = Shader.PropertyToID("_PredictedPositions");
    static readonly int VelocitiesId = Shader.PropertyToID("_Velocities");
    static readonly int DensitiesId = Shader.PropertyToID("_Densities");
    static readonly int CellKeysId = Shader.PropertyToID("SpatialKeys");
    static readonly int ParticleIndicesId = Shader.PropertyToID("SortedIndices");
    static readonly int StartIndicesId = Shader.PropertyToID("SpatialOffsets");

    static readonly int SortTarget_PositionsId = Shader.PropertyToID("SortTarget_Positions");
    static readonly int SortTarget_PredictedPositionsId = Shader.PropertyToID("SortTarget_PredictedPositions");
    static readonly int SortTarget_VelocitiesId = Shader.PropertyToID("SortTarget_Velocities");

    readonly ComputeShader _compute;
    readonly ComputeShader _oneSweepShader;

    readonly int _predictPositionsKernel;
    readonly int _buildSpatialLookupKernel;
    readonly int _reorderKernel;
    readonly int _reorderCopybackKernel;
    // readonly int _clearStartIndicesKernel;
    // readonly int _bitonicSortKernel;
    // readonly int _buildStartIndicesKernel;
    readonly int _updateDensitiesKernel;
    readonly int _calcPressureKernel;
    readonly int _calcViscosityKernel;
    readonly int _calcSurfaceTensionKernel;
    readonly int _integrateKernel;


    public ComputeBuffer PositionsBuffer;
    ComputeBuffer _predictedPositionsBuffer;
    public ComputeBuffer VelocitiesBuffer;
    ComputeBuffer _densityBuffer;

    SpatialHash spatialHash;

    ComputeBuffer sortTarget_positionBuffer;
    ComputeBuffer sortTarget_velocityBuffer;
    ComputeBuffer sortTarget_predictedPositionsBuffer;

    // int _paddedCount;

    public int ParticleCount { get; private set; }


    ComputeBuffer _tempKeys;
    ComputeBuffer _tempPayload;

    ComputeBuffer _tempGlobalHistogram;

    ComputeBuffer _tempPassHistogram;

    ComputeBuffer _tempIndex;
    OneSweep _sorter;

    Dictionary<ComputeBuffer, int> bufferNameLookup;

    public PhysicsSystem3D(ComputeShader computeShader, ComputeShader oneSweepShader)
    {
        _compute = computeShader;
        _oneSweepShader = oneSweepShader;
        _predictPositionsKernel = _compute.FindKernel("PredictPositions");
        _buildSpatialLookupKernel = _compute.FindKernel("BuildSpatialLookup");
        _reorderKernel = _compute.FindKernel("Reorder");
        _reorderCopybackKernel = _compute.FindKernel("ReorderCopyBack");
        // _clearStartIndicesKernel = _compute.FindKernel("ClearStartIndices");
        // _bitonicSortKernel = _compute.FindKernel("BitonicSort");
        // _buildStartIndicesKernel = _compute.FindKernel("BuildStartIndices");
        _updateDensitiesKernel = _compute.FindKernel("UpdateDensities");
        _calcPressureKernel = _compute.FindKernel("CalculatePressureForces");
        _calcViscosityKernel = _compute.FindKernel("CalculateViscosityForces");
        _calcSurfaceTensionKernel = _compute.FindKernel("CalculateSurfaceTension");
        _integrateKernel = _compute.FindKernel("Integrate");
    }

    public void Initialize(ParticleSettings settings, SpawnData3D spawnData, float deltaTime,
        Vector3 boundsMin,
        Vector3 boundsMax,
        Matrix4x4 worldToLocal,
        Matrix4x4 localToWorld,
        Vector3 interactionPos,
        float interactionStrength)
    {
        DisposeBuffers();
        ParticleCount = spawnData.positions.Length;
        if (ParticleCount == 0)
            return;
        CreateBuffers();
        SetInitialBufferData(spawnData);
        bufferNameLookup = new Dictionary<ComputeBuffer, int>
        {
            { PositionsBuffer, PositionsId },
            { _predictedPositionsBuffer, PredictedPositionsId },
            { VelocitiesBuffer, VelocitiesId },
            { _densityBuffer, DensitiesId },
            { spatialHash.SpatialKeys, CellKeysId },
            { spatialHash.SpatialIndices, ParticleIndicesId },
            { spatialHash.SpatialOffsets, StartIndicesId },
            { sortTarget_positionBuffer, SortTarget_PositionsId },
            { sortTarget_predictedPositionsBuffer, SortTarget_PredictedPositionsId },
            { sortTarget_velocityBuffer, SortTarget_VelocitiesId },
        };
        BindAllBuffers();


        _sorter =
        new OneSweep(
        _oneSweepShader,
        ParticleCount,
        ref _tempKeys,
        ref _tempPayload,
        ref _tempGlobalHistogram,
        ref _tempPassHistogram,
        ref _tempIndex
        );

        BindStaticUniforms(settings, deltaTime, boundsMin, boundsMax, worldToLocal, localToWorld, interactionPos, interactionStrength);
        BindAllBuffers();
    }

    public void Simulate(
      )
    {
        if (ParticleCount == 0) return;



        int realGroups = Mathf.CeilToInt(ParticleCount / (float)ThreadsPerGroup);

        _compute.Dispatch(_predictPositionsKernel, realGroups, 1, 1);
        _compute.Dispatch(_buildSpatialLookupKernel, realGroups, 1, 1);
        spatialHash.Run();
        _compute.Dispatch(_reorderKernel, realGroups, 1, 1);
        _compute.Dispatch(_reorderCopybackKernel, realGroups, 1, 1);
        // DispatchRadixSort();
        // DispatchBitonicSort(paddedGroups);
        // _compute.Dispatch(_clearStartIndicesKernel, realGroups, 1, 1);
        // _compute.Dispatch(_buildStartIndicesKernel, realGroups, 1, 1);
        _compute.Dispatch(_updateDensitiesKernel, realGroups, 1, 1);
        _compute.Dispatch(_calcPressureKernel, realGroups, 1, 1);
        _compute.Dispatch(_calcViscosityKernel, realGroups, 1, 1);
        _compute.Dispatch(_calcSurfaceTensionKernel, realGroups, 1, 1);
        _compute.Dispatch(_integrateKernel, realGroups, 1, 1);
    }

    public void Dispose() => DisposeBuffers();

    public void BindStaticUniforms(ParticleSettings settings, float deltaTime,
        Vector3 boundsMin,
        Vector3 boundsMax,
        Matrix4x4 worldToLocal,
        Matrix4x4 localToWorld,
        Vector3 interactionPos,
        float interactionStrength)
    {
        _compute.SetInt(ParticleCountId, ParticleCount);
        _compute.SetFloat(MassId, settings.mass);
        _compute.SetFloat(GravityId, settings.gravity);
        _compute.SetFloat(SmoothingRadiusId, settings.smoothingRadius);
        _compute.SetFloat(PressureMultiplierId, settings.pressureMultiplier);
        _compute.SetFloat(TargetDensityId, settings.targetDensity);
        _compute.SetFloat(ParticleRadiusId, settings.radius);
        _compute.SetFloat(CollisionDampingId, settings.collisionDamping);
        _compute.SetFloat(InteractionRadiusId, settings.interactionRadius);
        _compute.SetFloat(ViscosityCoeffId, settings.viscosityCoeff);
        _compute.SetFloat(SurfaceTensionCoeffId, settings.surfaceTensionCoeff);
        _compute.SetFloat(SurfaceTensionThresholdId, settings.surfaceTensionThreshold);
        _compute.SetFloat(DeltaTimeId, deltaTime);
        _compute.SetVector(BoundaryLocalMinId, boundsMin);
        _compute.SetVector(BoundaryLocalMaxId, boundsMax);
        _compute.SetMatrix(BoundaryWorldToLocalId, worldToLocal);
        _compute.SetMatrix(BoundaryLocalToWorldId, localToWorld);

        _compute.SetVector(InteractionInputPosId, interactionPos);
        _compute.SetFloat(InteractionStrengthId, interactionStrength);
    }

    void SetInitialBufferData(SpawnData3D spawnData)
    {
        PositionsBuffer.SetData(spawnData.positions);
        _predictedPositionsBuffer.SetData(spawnData.positions);
        VelocitiesBuffer.SetData(spawnData.velocities);
    }

    void CreateBuffers()
    {
        spatialHash = new SpatialHash(ParticleCount);
        PositionsBuffer = CreateStructuredBuffer<float3>(ParticleCount);
        _predictedPositionsBuffer = CreateStructuredBuffer<float3>(ParticleCount);
        VelocitiesBuffer = CreateStructuredBuffer<float3>(ParticleCount);
        _densityBuffer = CreateStructuredBuffer<float2>(ParticleCount);

        sortTarget_positionBuffer = CreateStructuredBuffer<float3>(ParticleCount);
        sortTarget_predictedPositionsBuffer = CreateStructuredBuffer<float3>(ParticleCount);
        sortTarget_velocityBuffer = CreateStructuredBuffer<float3>(ParticleCount);
    }

    void BindAllBuffers()
    {

        SetBuffers(_compute, _predictPositionsKernel, bufferNameLookup, new ComputeBuffer[]
        {
            PositionsBuffer,
            _predictedPositionsBuffer,
            VelocitiesBuffer,
        });
        SetBuffers(_compute, _buildSpatialLookupKernel, bufferNameLookup, new ComputeBuffer[]
        {
            spatialHash.SpatialKeys,
            spatialHash.SpatialOffsets,
            _predictedPositionsBuffer,
            spatialHash.SpatialIndices,
        });

        // Reorder kernel
        SetBuffers(_compute, _reorderKernel, bufferNameLookup, new ComputeBuffer[]
        {
                PositionsBuffer,
                sortTarget_positionBuffer,
                _predictedPositionsBuffer,
                sortTarget_predictedPositionsBuffer,
                VelocitiesBuffer,
                sortTarget_velocityBuffer,
                spatialHash.SpatialIndices
        });

        // Reorder copyback kernel
        SetBuffers(_compute, _reorderCopybackKernel, bufferNameLookup, new ComputeBuffer[]
        {
                PositionsBuffer,
                sortTarget_positionBuffer,
                _predictedPositionsBuffer,
                sortTarget_predictedPositionsBuffer,
                VelocitiesBuffer,
                sortTarget_velocityBuffer,
                spatialHash.SpatialIndices
        });


        // SetBuffers(_compute, _clearStartIndicesKernel, bufferNameLookup, new ComputeBuffer[]
        // {
        //     _startIndicesBuffer,
        // });


        // SetBuffers(_compute, _buildStartIndicesKernel, bufferNameLookup, new ComputeBuffer[]
        // {
        //     _cellKeysBuffer,
        //     _startIndicesBuffer,
        // });

        SetBuffers(_compute, _updateDensitiesKernel, bufferNameLookup, new ComputeBuffer[]
        {
            _predictedPositionsBuffer,
            _densityBuffer,
            spatialHash.SpatialKeys,
            spatialHash.SpatialOffsets,
        });

        SetBuffers(_compute, _calcViscosityKernel, bufferNameLookup, new ComputeBuffer[]
        {
            _predictedPositionsBuffer,
            VelocitiesBuffer,
            _densityBuffer,
            spatialHash.SpatialKeys,
            spatialHash.SpatialOffsets,
        });
        SetBuffers(_compute, _calcPressureKernel, bufferNameLookup, new ComputeBuffer[]
        {
            _predictedPositionsBuffer,
            VelocitiesBuffer,
            _densityBuffer,
            spatialHash.SpatialKeys,
            spatialHash.SpatialOffsets,
        });

        SetBuffers(_compute, _calcSurfaceTensionKernel, bufferNameLookup, new ComputeBuffer[]
        {
            _predictedPositionsBuffer,
            VelocitiesBuffer,
            _densityBuffer,
            spatialHash.SpatialKeys,
            spatialHash.SpatialOffsets,
        });

        SetBuffers(_compute, _integrateKernel, bufferNameLookup, new ComputeBuffer[]
        {
            PositionsBuffer,
            VelocitiesBuffer,
        });
    }

    // void DispatchRadixSort()
    // {
    //     _sorter.Sort(
    //  ParticleCount,

    //  _cellKeysBuffer,
    //  _particleIndicesBuffer,

    //  _tempKeys,
    //  _tempPayload,
    //  _tempGlobalHistogram,
    //  _tempPassHistogram,
    //  _tempIndex,

    //  typeof(uint),
    //  typeof(uint),

    //  true);
    // }

    // void DispatchBitonicSort(int paddedGroups)
    // {
    //     for (int k = 2; k <= _paddedCount; k *= 2)
    //     {
    //         for (int j = k / 2; j > 0; j /= 2)
    //         {
    //             _compute.SetInt(KId, k);
    //             _compute.SetInt(JId, j);
    //             _compute.Dispatch(_bitonicSortKernel, paddedGroups, 1, 1);
    //         }
    //     }
    // }

    // static int NextPowerOfTwo(int n)
    // {
    //     int p = 1;
    //     while (p < n) p *= 2;
    //     return p;
    // }

    private void DisposeBuffers()
    {
        if (bufferNameLookup == null)
            return;

        foreach (var kvp in bufferNameLookup)
            Release(kvp.Key);

        spatialHash?.Release();
        spatialHash = null;
        bufferNameLookup = null;
    }
}