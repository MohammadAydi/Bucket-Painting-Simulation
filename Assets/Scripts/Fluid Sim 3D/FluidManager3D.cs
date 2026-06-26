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

    SpawnSystem3D _spawnSystem;
    PhysicsSystem3D _physicsSystem;
    RenderSystem3D _renderSystem;
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

    public ComputeBuffer ParticleBuffer => _physicsSystem?.ParticleBuffer;
    public int ParticleCount => _physicsSystem?.ParticleCount ?? 0;

    void Awake()
    {
        if (boundaryVolume == null)
            boundaryVolume = FindObjectOfType<FluidBoundary3D>();
    }

    void Start()
    {
        Debug.Log("FluidManager3D Start called.");
        float deltaTime = 1 / 60f;
        Time.fixedDeltaTime = deltaTime;
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

    void FixedUpdate()
    {
        if (!Application.isPlaying || !_initialized || boundaryVolume == null) return;

        // Interaction placeholder. In 3D you would normally raycast to find an interaction plane.
        Vector3 interactionPos = Vector3.zero;
        float currentStrength = 0f;

        for (int i = 0; i < 3; i++)
        {
            _physicsSystem.Simulate(
          settings,
          Time.fixedDeltaTime / 3,
          boundaryVolume.LocalMin,
          boundaryVolume.LocalMax,
          boundaryVolume.WorldToColliderLocalMatrix,
          boundaryVolume.ColliderLocalToWorldMatrix,
          interactionPos,
          currentStrength);
        }

    }

    void LateUpdate()
    {
        if (!_initialized || boundaryVolume == null) return;
        Bounds bounds = boundaryVolume.WorldBounds;
        _renderSystem.Render(_physicsSystem.ParticleCount, bounds);
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
        ParticleData3D[] particles = _spawnSystem.SpawnParticles(boundaryVolume);

        _physicsSystem.Initialize(settings, particles);
        _renderSystem.Initialize(settings, _physicsSystem.ParticleBuffer);

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
            _lastSmoothingRadius != settings.smoothingRadius ||
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

        if (requiresReinitialize)
        {
            InitializeSystems();
            return;
        }

        if (physicsChange)
        {
            _physicsSystem.BindStaticUniforms(settings);
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