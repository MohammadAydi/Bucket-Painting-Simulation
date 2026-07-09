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

    public ComputeBuffer _debugBuffer;

    // ── Pigment buffers ───────────────────────────────────────────────────────
    // PigmentBuffer            : live per-particle color (float4 linear RGBA)
    // PigmentBufferWrite       : double-buffer write target for diffusion kernel
    // SortTarget_PigmentBuffer : scratch used during the spatial-sort reorder
    public ComputeBuffer PigmentBuffer;
    public ComputeBuffer PigmentBufferWrite;
    public ComputeBuffer SortTarget_PigmentBuffer;

    GPUExecuter _gravityForce;
    GPUExecuter _predictPositions;
    NeighborsSearching _neighborsSearching;
    GPUExecuter _calcDensity;

    GPUExecuter _viscosityForce;
    GPUExecuter _pressureForce;
    GPUExecuter _surfaceTensionForce;
    GPUExecuter _canvasCollision;
    GPUExecuter _frictionForce;
    GPUExecuter _integrate;

    // ── Pigment executors ─────────────────────────────────────────────────────
    PigmentReorder _pigmentReorder;
    PigmentDiffusion _pigmentDiffusion;
    ComputeShader _pigmentCompute;
    Texture2D _mixboxLUT;

    public int ParticleCount { get; private set; }

    public Dictionary<ComputeBuffer, int> bufferNameLookup;

    public FluidModel(
        SpawnData3D spawnData, ParticleSettings settings,
        ComputeShader sphCompute,
        ComputeShader pigmentCompute = null,
        PigmentSettings pigmentSettings = null,
        Texture2D mixboxLUT = null
    )
    {
        _SPHCompute = sphCompute;

        // Pick which pigment compute shader is actually active. Both files
        // expose the same three kernel names (PigmentReorderKernel,
        // PigmentReorderCopyBack, PigmentDiffusionKernel), so nothing else
        // needs to change based on which one is selected.
        _pigmentCompute = pigmentCompute;
        bool useMixbox = pigmentSettings != null
                         && pigmentSettings.mixingModel == PigmentSettings.PigmentMixingModel.Mixbox;
        _mixboxLUT = useMixbox ? mixboxLUT : null;
        string diffusionKernel = useMixbox
            ? "PigmentDiffusionKernelMixBox"
            : "PigmentDiffusionKernelAdditive";
        DisposeBuffers();
        ParticleCount = spawnData.positions.Length;
        if (ParticleCount <= 1)
            return;
        CreateBuffers();
        SetInitialBufferData(spawnData, pigmentSettings);
        bufferNameLookup = new Dictionary<ComputeBuffer, int>
        {
            { PositionsBuffer,                     Config.PositionsId },
            { _predictedPositionsBuffer,           Config.PredictedPositionsId },
            { VelocitiesBuffer,                    Config.VelocitiesId },
            { _densityBuffer,                      Config.DensitiesId },
            { spatialHash.SpatialKeys,             Config.CellKeysId },
            { spatialHash.SpatialIndices,          Config.ParticleIndicesId },
            { spatialHash.SpatialOffsets,          Config.StartIndicesId },
            { sortTarget_positionBuffer,           Config.SortTarget_PositionsId },
            { sortTarget_predictedPositionsBuffer, Config.SortTarget_PredictedPositionsId },
            { sortTarget_velocityBuffer,           Config.SortTarget_VelocitiesId },
            { _debugBuffer,                        Config.DebugId },
            // Pigment entries — PigmentDiffusion.compute looks these up by name ID
            { PigmentBuffer,            Config.PigmentsId },
            { PigmentBufferWrite,       Config.PigmentsWriteId },
            { SortTarget_PigmentBuffer, Config.SortTarget_PigmentsId },
        };
        _gravityForce = new GravityForce(this, sphCompute, "GravityForce");
        _predictPositions = new PositionPredictor(this, sphCompute, "PositionPredictor");
        _neighborsSearching = new NeighborsSearching(this, sphCompute);
        _calcDensity = new DensityCalc(this, sphCompute, "DensityCalculator");
        _canvasCollision = new CanvasCollisionSolver(this, sphCompute, "CanvasCollision");
        _frictionForce = new FrictionForce(this, sphCompute, "FrictionForce");
        setPressureSolver(settings.pressureSolverMethod);
        setViscositySolver(settings.viscositySolverMethod);
        setSurfaceTensionSolver(settings.surfaceTensionSolverMethod);
        _integrate = new Integrator(this, sphCompute, "Integrator");

        // Pigment system — only initialised when pigmentCompute is supplied
        if (_pigmentCompute != null)
        {
            _pigmentReorder = new PigmentReorder(this, _pigmentCompute,
                "PigmentReorderKernel", "PigmentReorderCopyBack");
            _pigmentDiffusion = new PigmentDiffusion(this, _pigmentCompute,
                diffusionKernel, _mixboxLUT);
            _pigmentReorder.BindsBuffers();
            _pigmentDiffusion.BindsBuffers();
        }

        BindBuffers();
    }

    public void Step(CanvasSurface canvas = null)
    {
        _gravityForce.Dispatch();
        _predictPositions.Dispatch();
        // NeighborsSearching.Dispatch() also reorders the pigment buffer in
        // lock-step with positions/velocities (via DispatchPigmentReorder).
        _neighborsSearching.Dispatch();
        _calcDensity.Dispatch();
        _pressureForce.Dispatch();
        _viscosityForce.Dispatch();
        _surfaceTensionForce.Dispatch();
        _integrate.Dispatch();
        if (canvas != null)
        {
            _canvasCollision.Dispatch();
            _frictionForce.Dispatch();
        }

        // Pigment diffusion runs after integration so particle positions are final.
        // Diffusion reads PigmentBuffer, writes PigmentBufferWrite, then we swap
        // so PigmentBuffer always holds the current-frame result.
        if (_pigmentDiffusion != null)
        {
            _pigmentDiffusion.Dispatch();
            SwapPigmentBuffers();
        }
    }

    // Called by NeighborsSearching to reorder the pigment buffer in lock-step
    // with positions/velocities. Null-safe: does nothing when no pigment system
    // is active.
    public void DispatchPigmentReorder()
    {
        _pigmentReorder?.DispatchReorder();
        _pigmentReorder?.DispatchCopyBack();
    }

    // Swap PigmentBuffer <-> PigmentBufferWrite so the live buffer always holds
    // the most recently diffused values. Updates the lookup and re-binds the
    // diffusion kernel so it reads/writes the correct buffers next frame.
    void SwapPigmentBuffers()
    {
        (PigmentBuffer, PigmentBufferWrite) = (PigmentBufferWrite, PigmentBuffer);
        bufferNameLookup[PigmentBuffer] = Config.PigmentsId;
        bufferNameLookup[PigmentBufferWrite] = Config.PigmentsWriteId;
        _pigmentDiffusion?.BindsBuffers();
        // PigmentReorder also reads/writes PigmentBuffer/PigmentBufferWrite (via
        // bufferNameLookup), so it must be re-bound whenever the references swap —
        // otherwise it keeps reordering the stale frame-0 buffer objects while
        // diffusion moves on to the new ones, desyncing pigment[i] from position[i]
        // a little more each frame (seen as random color speckling over time).
        _pigmentReorder?.BindsBuffers();
    }


    public void setViscositySolver(ViscositySolverMethod method)
    {
        _viscosityForce = method switch
        {
            ViscositySolverMethod.Standard => new StandardViscosity(this, _SPHCompute, "StandardViscositySolver"),
            _ => throw new ArgumentOutOfRangeException()
        };
        _viscosityForce.BindsBuffers();
    }

    public void setPressureSolver(PressureSolverMethod method)
    {
        _pressureForce = method switch
        {
            PressureSolverMethod.Standard => new WCPressureSolver(this, _SPHCompute, "WCSPressureSolver"),
            PressureSolverMethod.NearPressure => new NearPressureSolver(this, _SPHCompute, "NearPressureSolver"),
            _ => throw new ArgumentOutOfRangeException()
        };
        _pressureForce.BindsBuffers();
    }

    public void setSurfaceTensionSolver(SurfaceTensionSolverMethod method)
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

        _debugBuffer = CreateStructuredBuffer<float>(2);

        // Pigment buffers — float4 per particle (linear RGBA)
        PigmentBuffer = CreateStructuredBuffer<Vector4>(ParticleCount);
        PigmentBufferWrite = CreateStructuredBuffer<Vector4>(ParticleCount);
        SortTarget_PigmentBuffer = CreateStructuredBuffer<Vector4>(ParticleCount);
    }

    void SetInitialBufferData(SpawnData3D spawnData, PigmentSettings pigmentSettings)
    {
        PositionsBuffer.SetData(spawnData.positions);
        _predictedPositionsBuffer.SetData(spawnData.positions);
        VelocitiesBuffer.SetData(spawnData.velocities);

        // Initialise per-particle pigment colors from spawnData.
        // SpawnData3D.pigmentColors is populated by SpawnSystem3D when
        // PigmentSettings is provided; fall back to white when null.
        if (spawnData.pigmentColors != null && spawnData.pigmentColors.Length == ParticleCount)
        {
            PigmentBuffer.SetData(spawnData.pigmentColors);
            PigmentBufferWrite.SetData(spawnData.pigmentColors);
        }
        else
        {
            var white = new Vector4[ParticleCount];
            for (int i = 0; i < ParticleCount; i++) white[i] = Vector4.one;
            PigmentBuffer.SetData(white);
            PigmentBufferWrite.SetData(white);
        }
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
        _canvasCollision.BindsBuffers();
        _frictionForce.BindsBuffers();
        _integrate.BindsBuffers();
    }

    // Push pigment-shader uniforms. Called each frame from FluidManager3D
    // before Step(), mirrors the pattern of BindStaticUniforms.
    public void BindPigmentUniforms(PigmentSettings pigmentSettings, ParticleSettings particleSettings, float deltaTime)
    {
        if (_pigmentCompute == null) return;
        _pigmentCompute.SetInt(Config.ParticleCountId, ParticleCount);
        _pigmentCompute.SetFloat(Config.MassId, particleSettings.mass);
        _pigmentCompute.SetFloat(Config.SmoothingRadiusId, particleSettings.smoothingRadius);
        _pigmentCompute.SetFloat(Config.DeltaTimeId, deltaTime);
        if (pigmentSettings != null)
            _pigmentCompute.SetFloat(Config.DiffusionCoeffId, pigmentSettings.diffusionCoeff);
    }

    public void BindStaticUniforms(
        ParticleSettings settings, float deltaTime,
        Vector3 boundsMin,
        Vector3 boundsMax,
        Matrix4x4 worldToLocal,
        Matrix4x4 localToWorld,
        Vector3 interactionPos,
        float interactionStrength,
        CanvasSurface canvas
    )
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

        if (canvas == null) return;

        canvas.GetPlaneData(out Vector3 center, out Vector3 normal,
            out Vector3 tangent, out Vector3 bitangent, out Vector2 halfExtents);

        _SPHCompute.SetVector(Config.CanvasCenterId, center);
        _SPHCompute.SetVector(Config.CanvasNormalId, normal);
        _SPHCompute.SetVector(Config.CanvasTangentId, tangent);
        _SPHCompute.SetVector(Config.CanvasBitangentId, bitangent);
        _SPHCompute.SetVector(Config.CanvasHalfExtentsId, halfExtents);
        _SPHCompute.SetFloat(Config.CanvasFrictionCoeffId, canvas.CurrentFrictionDelta);
        _SPHCompute.SetFloat(Config.CanvasCollisionDampingId, canvas.CurrentCollisionDamping);
        _SPHCompute.SetMatrix(Config.CanvasWorldToLocalId, canvas.WorldToColliderLocalMatrix);
        _SPHCompute.SetMatrix(Config.CanvasLocalToWorldId, canvas.ColliderLocalToWorldMatrix);
    }


    public void SetSmoothingConstant(float h)
    {
        // Sync viscosity-Laplacian constant to the pigment compute shader so
        // the diffusion kernel uses the same value as the SPH shader.
        if (_pigmentCompute != null)
        {
            float h6pigment = h * h * h * h * h * h;
            _pigmentCompute.SetFloat(Config.ViscosityLaplacianId, 45f / (Mathf.PI * h6pigment));
        }

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

        Release(_debugBuffer);
        _debugBuffer = null;

        Release(PigmentBuffer);
        PigmentBuffer = null;

        Release(PigmentBufferWrite);
        PigmentBufferWrite = null;

        Release(SortTarget_PigmentBuffer);
        SortTarget_PigmentBuffer = null;

        spatialHash?.Release();
        spatialHash = null;

        bufferNameLookup = null;
        _pigmentReorder = null;
        _pigmentDiffusion = null;
    }

    public void Dispose()
    {
        DisposeBuffers();
    }
}