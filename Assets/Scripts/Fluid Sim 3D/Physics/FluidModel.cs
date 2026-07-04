using System;
using System.Collections.Generic;
using Fluid_Sim_3D.Utilities.SpatialHash;
using Unity.Mathematics;
using UnityEngine;
using static Fluid_Sim_3D.Utilities.ComputeHelper;

class FluidModel : IDisposable
{

    public ComputeShader _SPHCompute;
    public ComputeBuffer PositionsBuffer;
    public ComputeBuffer _predictedPositionsBuffer;
    public ComputeBuffer VelocitiesBuffer;
    public ComputeBuffer _densityBuffer;

    public SpatialHash spatialHash;

    public ComputeBuffer sortTarget_positionBuffer;
    public ComputeBuffer sortTarget_velocityBuffer;
    public ComputeBuffer sortTarget_predictedPositionsBuffer;

    GPUExecuter _gravityForce;
    GPUExecuter _predictPositions;
    NeighborsSearching _neighborsSearching;
    GPUExecuter _calcDensity;

    GPUExecuter _viscosityForce;
    GPUExecuter _pressureForce;
    GPUExecuter _surfaceTensionForce;
    GPUExecuter _integrate;
    public int ParticleCount { get; private set; }

    public Dictionary<ComputeBuffer, int> bufferNameLookup;

    public FluidModel(
        SpawnData3D spawnData, ParticleSettings settings,
        ComputeShader sphCompute
    )
    {
        _SPHCompute = sphCompute;
        DisposeBuffers();
        ParticleCount = spawnData.positions.Length;
        if (ParticleCount <= 1)
            return;
        CreateBuffers();
        SetInitialBufferData(spawnData);
        bufferNameLookup = new Dictionary<ComputeBuffer, int>
        {
            { PositionsBuffer, Config.PositionsId },
            { _predictedPositionsBuffer, Config.PredictedPositionsId },
            { VelocitiesBuffer, Config.VelocitiesId },
            { _densityBuffer, Config.DensitiesId },
            { spatialHash.SpatialKeys, Config.CellKeysId },
            { spatialHash.SpatialIndices, Config.ParticleIndicesId },
            { spatialHash.SpatialOffsets, Config.StartIndicesId },
            { sortTarget_positionBuffer, Config.SortTarget_PositionsId },
            { sortTarget_predictedPositionsBuffer, Config.SortTarget_PredictedPositionsId },
            { sortTarget_velocityBuffer, Config.SortTarget_VelocitiesId },
        };
        _gravityForce = new GravityForce(this, sphCompute, "GravityForce");
        _predictPositions = new PositionPredictor(this, sphCompute, "PositionPredictor");
        _neighborsSearching = new NeighborsSearching(this, sphCompute);
        _calcDensity = new DensityCalc(this, sphCompute, "DensityCalculator");
        setPressureSolver(settings.pressureSolverMethod);
        setViscositySolver(settings.viscositySolverMethod);
        setSurfaceTensionSolver(settings.surfaceTensionSolverMethod);
        _integrate = new Integrator(this, sphCompute, "Integrator");
        BindBuffers();
    }

    public void Step()
    {
        _gravityForce.Dispatch();
        _predictPositions.Dispatch();
        _neighborsSearching.Dispatch();
        _calcDensity.Dispatch();
        _pressureForce.Dispatch();
        _viscosityForce.Dispatch();
        // _surfaceTensionForce.Dispatch();
        _integrate.Dispatch();
    }


    public void setViscositySolver(
        ViscositySolverMethod method
    )
    {
        _viscosityForce = method switch
        {
            ViscositySolverMethod.Standard => new StandardViscosity(this, _SPHCompute, "StandardViscositySolver"),
            _ => throw new ArgumentOutOfRangeException()
        };
        _viscosityForce.BindsBuffers();
    }

    public void setPressureSolver(
        PressureSolverMethod method
    )
    {
        _pressureForce = method switch
        {
            PressureSolverMethod.Standard => new WCPressureSolver(this, _SPHCompute, "WCSPressureSolver"),
            PressureSolverMethod.NearPressure => new NearPressureSolver(this, _SPHCompute, "NearPressureSolver"),
            _ => throw new ArgumentOutOfRangeException()
        };
        _pressureForce.BindsBuffers();
    }

    public void setSurfaceTensionSolver(
        SurfaceTensionSolverMethod method
    )
    {
        _surfaceTensionForce = method switch
        {
            SurfaceTensionSolverMethod.Standard => new StandardSurfaceTension(this, _SPHCompute, "StandardSurfaceTension"),
            _ => throw new ArgumentOutOfRangeException()
        };
        _surfaceTensionForce.BindsBuffers();
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

    void SetInitialBufferData(SpawnData3D spawnData)
    {
        PositionsBuffer.SetData(spawnData.positions);
        _predictedPositionsBuffer.SetData(spawnData.positions);
        VelocitiesBuffer.SetData(spawnData.velocities);
    }

    void BindBuffers()
    {
        _gravityForce.BindsBuffers();
        _predictPositions.BindsBuffers();
        _neighborsSearching.BindsBuffers();
        _calcDensity.BindsBuffers();
        _pressureForce.BindsBuffers();
        _viscosityForce.BindsBuffers();
        _surfaceTensionForce.BindsBuffers();
        _integrate.BindsBuffers();
    }

    public void BindStaticUniforms(ParticleSettings settings, float deltaTime,
     Vector3 boundsMin,
     Vector3 boundsMax,
     Matrix4x4 worldToLocal,
     Matrix4x4 localToWorld,
     Vector3 interactionPos,
     float interactionStrength)
    {
        _SPHCompute.SetInt(Config.ParticleCountId, ParticleCount);
        _SPHCompute.SetFloat(Config.MassId, settings.mass);
        _SPHCompute.SetFloat(Config.GravityId, settings.gravity);
        _SPHCompute.SetFloat(Config.SmoothingRadiusId, settings.smoothingRadius);
        _SPHCompute.SetFloat(Config.PressureMultiplierId, settings.pressureMultiplier);
        _SPHCompute.SetFloat(Config.TargetDensityId, settings.targetDensity);
        _SPHCompute.SetFloat(Config.NearPressureMultiplierId, settings.nearPressureMultiplier);
        _SPHCompute.SetFloat(Config.ParticleRadiusId, settings.radius);
        _SPHCompute.SetFloat(Config.CollisionDampingId, settings.collisionDamping);
        _SPHCompute.SetFloat(Config.InteractionRadiusId, settings.interactionRadius);
        _SPHCompute.SetFloat(Config.ViscosityCoeffId, settings.viscosityCoeff);
        _SPHCompute.SetFloat(Config.SurfaceTensionCoeffId, settings.surfaceTensionCoeff);
        _SPHCompute.SetFloat(Config.SurfaceTensionThresholdId, settings.surfaceTensionThreshold);
        _SPHCompute.SetFloat(Config.DeltaTimeId, deltaTime);
        _SPHCompute.SetVector(Config.BoundaryLocalMinId, boundsMin);
        _SPHCompute.SetVector(Config.BoundaryLocalMaxId, boundsMax);
        _SPHCompute.SetMatrix(Config.BoundaryWorldToLocalId, worldToLocal);
        _SPHCompute.SetMatrix(Config.BoundaryLocalToWorldId, localToWorld);
        _SPHCompute.SetVector(Config.InteractionInputPosId, interactionPos);
        _SPHCompute.SetFloat(Config.InteractionStrengthId, interactionStrength);
    }


    public void SetSmoothingConstant(float h)
    {
        float h2 = h * h;
        float h3 = h2 * h;
        float h4 = h2 * h2;
        float h5 = h4 * h;
        float h6 = h3 * h3;
        float h9 = h6 * h3;

        // Standard SPH kernels
        _SPHCompute.SetFloat(Config.Poly6Id,
            315f / (64f * Mathf.PI * h9));

        _SPHCompute.SetFloat(Config.SpikyGradientId,
            -45f / (Mathf.PI * h6));

        _SPHCompute.SetFloat(Config.ViscosityLaplacianId,
            45f / (Mathf.PI * h6));

        _SPHCompute.SetFloat(Config.Poly6GradientId,
            -945f / (32f * Mathf.PI * h9));

        _SPHCompute.SetFloat(Config.Poly6LaplacianId,
            -945f / (32f * Mathf.PI * h9));

        // Custom kernels
        _SPHCompute.SetFloat(Config.SpikyPow2Id,
            15f / (2f * Mathf.PI * h5));

        _SPHCompute.SetFloat(Config.SpikyPow3Id,
            15f / (Mathf.PI * h6));

        _SPHCompute.SetFloat(Config.SpikyPow2GradId,
            15f / (Mathf.PI * h5));

        _SPHCompute.SetFloat(Config.SpikyPow3GradId,
            45f / (Mathf.PI * h6));

        _SPHCompute.SetFloat(Config.CubicSplineId,
            8f / (Mathf.PI * h3));
    }


    private void DisposeBuffers()
    {
        Release(PositionsBuffer);
        PositionsBuffer = null;

        Release(_predictedPositionsBuffer);
        _predictedPositionsBuffer = null;

        Release(VelocitiesBuffer);
        VelocitiesBuffer = null;

        Release(_densityBuffer);
        _densityBuffer = null;

        Release(sortTarget_positionBuffer);
        sortTarget_positionBuffer = null;

        Release(sortTarget_predictedPositionsBuffer);
        sortTarget_predictedPositionsBuffer = null;

        Release(sortTarget_velocityBuffer);
        sortTarget_velocityBuffer = null;

        spatialHash?.Release();
        spatialHash = null;

        bufferNameLookup = null;
    }

    public void Dispose()
    {
        DisposeBuffers();
    }
}