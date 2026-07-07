using UnityEngine;

// [ExecuteAlways]
public class FluidManager3D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] FluidBoundary3D boundaryVolume;
    [SerializeField] ParticleSettings settings;
    [SerializeField] ComputeShader fluidComputeShader;
    [SerializeField] Material particleMaterial;
    [SerializeField] BucketGenerator bucket;
    [SerializeField] BucketFluidCollision3D bucketCollision;
    [SerializeField] CanvasSurface canvasSurface;

    FluidModel _fluidModel;

    [Header("Time Step")] public float normalTimeScale = 1;
    public float slowTimeScale = 0.1f;
    public float maxTimestepFPS = 60; // if time-step dips lower than this fps, simulation will run slower (set to 0 to disable)
    public int iterationsPerFrame = 3;
    public bool inSlowMode = false;

    SpawnSystem3D _spawnSystem;
    RenderSystem3D _renderSystem;

    private float ActiveTimeScale => inSlowMode ? slowTimeScale : normalTimeScale;
    bool _initialized;

    int _lastParticleCount;
    int _lastSphereResolution;
    float _lastRadius;
    float _lastSmoothingRadius;
    float _lastParticleSpacing;
    float _lastVelocityDisplayMax;

    float _lastMass;
    float _lastGravity;
    float _lastPressureMultiplier;
    float _lastTargetDensity;
    float _lastCollisionDamping;
    float _lastInteractionRadius;
    float _lastViscosityCoeff;
    float _lastSurfaceTensionCoeff;
    float _lastSurfaceTensionThreshold;
    PressureSolverMethod _lastPressureSolverMethod;
    ViscositySolverMethod _lastViscositySolverMethod;
    SurfaceTensionSolverMethod _lastSurfaceTensionSolverMethod;

    public ComputeBuffer PositionsBuffer => _fluidModel?.PositionsBuffer;
    public ComputeBuffer VelocitiesBuffer => _fluidModel?.VelocitiesBuffer;
    public int ParticleCount => _fluidModel?.ParticleCount ?? 0;

    void Awake()
    {
        if (boundaryVolume == null)
            boundaryVolume = FindAnyObjectByType<FluidBoundary3D>();
    }

    void Start()
    {
        if (settings != null)
            settings.OnChanged += OnSettingsChanged;

        InitializeSystems();
    }

    void OnEnable()
    {
        if (settings != null)
        {
            settings.OnChanged -= OnSettingsChanged;
            settings.OnChanged += OnSettingsChanged;
        }

        InitializeSystems();
    }

    void Update()
    {
        if (!Application.isPlaying || !_initialized || boundaryVolume == null) return;
        float maxDeltaTime = maxTimestepFPS > 0 ? 1 / maxTimestepFPS : float.PositiveInfinity; // If framerate dips too low, run the simulation slower than real-time
        float dt = Mathf.Min(Time.deltaTime * ActiveTimeScale, maxDeltaTime);
        RunSimulationFrame(dt);
    }

    void RunSimulationFrame(float frameDeltaTime)
    {
        float subStepDeltaTime = frameDeltaTime / iterationsPerFrame;
        _fluidModel.BindStaticUniforms(
            settings,
            subStepDeltaTime,
            boundaryVolume.LocalMin,
            boundaryVolume.LocalMax,
            boundaryVolume.WorldToColliderLocalMatrix,
            boundaryVolume.ColliderLocalToWorldMatrix,
            Vector3.zero,
            0,
            canvasSurface
        );
        // Simulation sub-steps
        for (int i = 0; i < iterationsPerFrame; i++)
        {
            _fluidModel.Step();
            if (bucketCollision != null && bucketCollision.enabled)
            {
                bucketCollision.ResolveCollisions();
            }
        }

        if (!_initialized || boundaryVolume == null) return;
        Bounds bounds = boundaryVolume.WorldBounds;
        _renderSystem.Render(_fluidModel.ParticleCount, bounds);

    }


    void OnDestroy()
    {
        if (settings != null) settings.OnChanged -= OnSettingsChanged;
        DisposeSystems();
    }

    void InitializeSystems()
    {
        if (settings == null || fluidComputeShader == null || boundaryVolume == null) return;

        DisposeSystems();

        if (bucket == null)
            bucket = FindAnyObjectByType<BucketGenerator>();
        if (bucketCollision == null)
            bucketCollision = FindAnyObjectByType<BucketFluidCollision3D>();

        _spawnSystem = new SpawnSystem3D(settings);
        SpawnData3D spawnData = (bucket != null) ? _spawnSystem.SpawnParticlesInBucket(bucket) : _spawnSystem.SpawnParticles(boundaryVolume);
        _fluidModel = new FluidModel(spawnData, settings, fluidComputeShader);
        _fluidModel.SetSmoothingConstant(settings.smoothingRadius);
        if (particleMaterial == null)
            particleMaterial = new Material(Shader.Find("Fluid/ParticleCircle3D"));

        _renderSystem = new RenderSystem3D(particleMaterial);
        _renderSystem.Initialize(settings, _fluidModel.PositionsBuffer, _fluidModel.VelocitiesBuffer);
        CacheSettings();
        _initialized = true;
    }

    void DisposeSystems()
    {
        _initialized = false;
        _fluidModel?.Dispose();
        _fluidModel = null;
        _renderSystem?.Dispose();
        _renderSystem = null;
        _spawnSystem = null;
    }

    void OnSettingsChanged()
    {
        if (settings == null || !_initialized) return;


        bool requiresReinitialize =
            _lastParticleCount != settings.particleCount ||
            _lastRadius != settings.radius ||
            _lastParticleSpacing != settings.particleSpacing ||
            _lastSphereResolution != settings.sphereResolution;

        bool physicsChange =
            _lastMass != settings.mass ||
            _lastGravity != settings.gravity ||
            _lastPressureMultiplier != settings.pressureMultiplier ||
            _lastTargetDensity != settings.targetDensity ||
            _lastCollisionDamping != settings.collisionDamping ||
            _lastInteractionRadius != settings.interactionRadius ||
            _lastViscosityCoeff != settings.viscosityCoeff ||
            _lastSurfaceTensionCoeff != settings.surfaceTensionCoeff ||
            _lastSurfaceTensionThreshold != settings.surfaceTensionThreshold;

        if (_lastSmoothingRadius != settings.smoothingRadius)
        {
            _fluidModel.SetSmoothingConstant(settings.smoothingRadius);
        }

        if (requiresReinitialize)
        {
            InitializeSystems();
            return;
        }

        if (_lastVelocityDisplayMax != settings.velocityDisplayMax)
            _renderSystem.SyncMaterial(settings);

        if (_lastPressureSolverMethod != settings.pressureSolverMethod)
        {
            _fluidModel.setPressureSolver(settings.pressureSolverMethod);
        }
        if (_lastViscositySolverMethod != settings.viscositySolverMethod)
        {
            _fluidModel.setViscositySolver(settings.viscositySolverMethod);
        }
        if (_lastSurfaceTensionSolverMethod != settings.surfaceTensionSolverMethod)
        {
            _fluidModel.setSurfaceTensionSolver(settings.surfaceTensionSolverMethod);
        }

        CacheSettings();
    }

    void CacheSettings()
    {
        _lastParticleCount = settings.particleCount;
        _lastSphereResolution = settings.sphereResolution;
        _lastRadius = settings.radius;
        _lastSmoothingRadius = settings.smoothingRadius;
        _lastParticleSpacing = settings.particleSpacing;
        _lastVelocityDisplayMax = settings.velocityDisplayMax;

        _lastMass = settings.mass;
        _lastGravity = settings.gravity;
        _lastPressureMultiplier = settings.pressureMultiplier;
        _lastTargetDensity = settings.targetDensity;
        _lastCollisionDamping = settings.collisionDamping;
        _lastInteractionRadius = settings.interactionRadius;
        _lastViscosityCoeff = settings.viscosityCoeff;
        _lastSurfaceTensionCoeff = settings.surfaceTensionCoeff;
        _lastSurfaceTensionThreshold = settings.surfaceTensionThreshold;

        _lastPressureSolverMethod = settings.pressureSolverMethod;
        _lastViscositySolverMethod = settings.viscositySolverMethod;
        _lastSurfaceTensionSolverMethod = settings.surfaceTensionSolverMethod;
    }
}