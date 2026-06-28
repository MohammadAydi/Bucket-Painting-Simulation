using UnityEngine;

// [ExecuteAlways]
public class FluidManager3D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] FluidBoundary3D boundaryVolume;
    [SerializeField] ParticleSettings settings;
    [SerializeField] ComputeShader fluidComputeShader;
    [SerializeField] ComputeShader oneSweepShader;
    [SerializeField] Material particleMaterial;

    [Header("Time Step")] public float normalTimeScale = 1;
    public float slowTimeScale = 0.1f;
    public float maxTimestepFPS = 60; // if time-step dips lower than this fps, simulation will run slower (set to 0 to disable)
    public int iterationsPerFrame = 3;
    public bool inSlowMode = false;


    SpawnSystem3D _spawnSystem;
    PhysicsSystem3D _physicsSystem;
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

    public ComputeBuffer PositionsBuffer => _physicsSystem?.PositionsBuffer;
    public ComputeBuffer VelocitiesBuffer => _physicsSystem?.VelocitiesBuffer;
    public int ParticleCount => _physicsSystem?.ParticleCount ?? 0;

    void Awake()
    {
        if (boundaryVolume == null)
            boundaryVolume = FindObjectOfType<FluidBoundary3D>();
    }

    void Start()
    {
        Debug.Log("FluidManager3D Start called.");
        if (settings != null)
            settings.OnChanged += OnSettingsChanged;

        InitializeSystems();
    }

    void OnEnable()
    {
        // 1. Hook up the event listener
        if (settings != null)
        {
            settings.OnChanged -= OnSettingsChanged; // Avoid double-subscribing
            settings.OnChanged += OnSettingsChanged;
        }

        // 2. Force full setup on enable so Edit Mode works immediately
        InitializeSystems();
    }


    void OnValidate()
    {
        if (Application.isPlaying) return;

        if (boundaryVolume == null)
            boundaryVolume = FindObjectOfType<FluidBoundary3D>();

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
        _physicsSystem.BindStaticUniforms(
            settings,
            subStepDeltaTime,
            boundaryVolume.LocalMin,
            boundaryVolume.LocalMax,
            boundaryVolume.WorldToColliderLocalMatrix,
            boundaryVolume.ColliderLocalToWorldMatrix,
            Vector3.zero,
            0
        );
        // Simulation sub-steps
        for (int i = 0; i < iterationsPerFrame; i++)
        {
            _physicsSystem.Simulate();
        }

        if (!_initialized || boundaryVolume == null) return;
        Bounds bounds = boundaryVolume.WorldBounds;
        // _renderSystem.Render(_physicsSystem.ParticleCount, bounds);

    }


    void OnDestroy()
    {
        if (settings != null) settings.OnChanged -= OnSettingsChanged;
        DisposeSystems();
    }

    void InitializeSystems()
    {
        if (settings == null || fluidComputeShader == null || oneSweepShader == null || boundaryVolume == null) return;

        DisposeSystems();

        _spawnSystem = new SpawnSystem3D(settings);
        _physicsSystem = new PhysicsSystem3D(fluidComputeShader, oneSweepShader);

        if (particleMaterial == null)
            particleMaterial = new Material(Shader.Find("Fluid/ParticleCircle3D"));

        _renderSystem = new RenderSystem3D(particleMaterial);
        SpawnData3D spawnData = _spawnSystem.SpawnParticles(boundaryVolume);

        _physicsSystem.Initialize(
            settings, spawnData, Time.fixedDeltaTime / 3,
            boundaryVolume.LocalMin,
            boundaryVolume.LocalMax,
            boundaryVolume.WorldToColliderLocalMatrix,
            boundaryVolume.ColliderLocalToWorldMatrix,
            Vector3.zero,
            0
        );
        _renderSystem.Initialize(settings, _physicsSystem.PositionsBuffer, _physicsSystem.VelocitiesBuffer);

        CacheSettings();
        _initialized = true;
    }

    void DisposeSystems()
    {
        _initialized = false;
        _physicsSystem?.Dispose();
        _physicsSystem = null;
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
            _physicsSystem.SetSmoothingConstant(settings.smoothingRadius);
        }

        if (requiresReinitialize)
        {
            InitializeSystems();
            return;
        }

        if (_lastVelocityDisplayMax != settings.velocityDisplayMax)
            _renderSystem.SyncMaterial(settings);

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
    }
}