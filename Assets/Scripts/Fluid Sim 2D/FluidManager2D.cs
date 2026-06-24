using UnityEngine;

[ExecuteAlways]
public class FluidManager2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Camera cameraRef;
    [SerializeField] ParticleSettings settings;
    [SerializeField] ComputeShader fluidComputeShader;
    [SerializeField] Material particleMaterial;
    [SerializeField] Material densityFieldMaterial;

    SpawnSystem2D _spawnSystem;
    PhysicsSystem2D _physicsSystem;
    RenderSystem2D _renderSystem;
    DensityRenderSystem2D _densityRenderSystem;
    bool _initialized;

    int _lastParticleCount;
    int _lastSegments;
    float _lastRadius;
    float _lastSmoothingRadius;
    float _lastParticleSpacing;
    Color _lastColor;
    Color _lastDensityColor;
    float _lastSmoothness;
    bool _lastShowDensity;

    float _lastMass;
    float _lastPressureMultiplier;
    float _lastTargetDensity;
    float _lastCollisionDamping;

    void Awake()
    {
        Debug.Log("FluidManager2D Awake called.");

        if (cameraRef == null)
        {
            cameraRef = Camera.main;
        }
        if (settings != null)
        {
            settings.OnChanged += OnSettingsChanged;
        }

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

    void Start()
    {
        Debug.Log("FluidManager2D Start called.");
        if (settings != null)
        {
            settings.OnChanged += OnSettingsChanged;
        }

        InitializeSystems();
    }

    void OnValidate()
    {
        Debug.Log("FluidManager2D OnValidate called.");
        // Guard against execution if dependencies aren't configured yet
        if (settings == null || fluidComputeShader == null)
        {

            Debug.Log("FluidManager2D OnValidate: Missing settings or compute shader reference.");
            return;
        }

        if (cameraRef == null)
        {
            Debug.Log("FluidManager2D OnValidate: Camera reference is null. Attempting to assign Camera.main.");
            cameraRef = Camera.main;
            if (cameraRef == null) return;
        }

        // When in editing mode (stopped), any inspector adjustment should force a full generation 
        // and trigger an explicit scene view repaint so that rendering systems can reflect changes.
        if (!Application.isPlaying)
        {
            Debug.Log("FluidManager2D OnValidate: Not playing, reinitializing systems.");
            InitializeSystems();

#if UNITY_EDITOR
            Debug.Log("FluidManager2D OnValidate: Forcing SceneView repaint.");
            // Force the Editor Scene View to instantly redraw its graphics context
            UnityEditor.SceneView.RepaintAll();
#endif
            return;
        }
    }

    void Update()
    {
        if (!Application.isPlaying || !_initialized)
        {
            return;
        }

        _physicsSystem.Simulate(settings, Time.deltaTime, BoundsMin, BoundsMax);
    }

    void LateUpdate()
    {
        Debug.Log("FluidManager2D LateUpdate called.");
        // ALLOW rendering to execute in the Editor scene view even when the game is not playing
        if (!_initialized)
        {
            Debug.Log("FluidManager2D LateUpdate: Not initialized, skipping rendering.");
            return;
        }

        Bounds bounds = BuildRenderBounds();
        _renderSystem.Render(_physicsSystem.ParticleBuffer, _physicsSystem.ParticleCount, bounds);

        if (settings.showDensity)
        {
            _densityRenderSystem.Render(BoundsMin, BoundsMax);
        }
    }

    void OnDestroy()
    {
        if (settings != null)
        {
            settings.OnChanged -= OnSettingsChanged;
        }

        DisposeSystems();
    }

    public Vector2 BoundsMin
    {
        get
        {
            float halfHeight = cameraRef.orthographicSize;
            float halfWidth = halfHeight * cameraRef.aspect;

            return new Vector2(
                cameraRef.transform.position.x - halfWidth,
                cameraRef.transform.position.y - halfHeight);
        }
    }

    public Vector2 BoundsMax
    {
        get
        {
            float halfHeight = cameraRef.orthographicSize;
            float halfWidth = halfHeight * cameraRef.aspect;

            return new Vector2(
                cameraRef.transform.position.x + halfWidth,
                cameraRef.transform.position.y + halfHeight);
        }
    }

    void InitializeSystems()
    {
        if (settings == null || fluidComputeShader == null)
        {
            return;
        }

        if (cameraRef == null)
        {
            cameraRef = Camera.main;
        }

        if (cameraRef == null)
        {
            return;
        }

        DisposeSystems();

        _spawnSystem = new SpawnSystem2D(settings);
        _physicsSystem = new PhysicsSystem2D(fluidComputeShader);

        if (particleMaterial == null)
        {
            particleMaterial = new Material(Shader.Find("Fluid/ParticleCircle"));
        }

        if (densityFieldMaterial == null)
        {
            densityFieldMaterial = new Material(Shader.Find("Fluid/DensityField"));
        }

        _renderSystem = new RenderSystem2D(particleMaterial);
        _densityRenderSystem = new DensityRenderSystem2D(densityFieldMaterial);

        ParticleData2D[] particles = _spawnSystem.SpawnParticles(BoundsMin, BoundsMax);
        _physicsSystem.Initialize(settings, particles);
        _renderSystem.Initialize(settings);
        _densityRenderSystem.SyncMaterial(settings, _physicsSystem.ParticleBuffer);

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

        _densityRenderSystem?.Dispose();
        _densityRenderSystem = null;

        _spawnSystem = null;
    }

    void OnSettingsChanged()
    {
        // Only use the runtime change detection filtering when the simulation is active.
        if (settings == null || !_initialized) return;

        // Changing particle layout properties means we reconstruct buffers
        bool requiresReinitialize =
            _lastParticleCount != settings.particleCount ||
            _lastRadius != settings.radius ||
            _lastSmoothingRadius != settings.smoothingRadius ||
            _lastParticleSpacing != settings.particleSpacing ||
            _lastSegments != settings.segments;

        // Changing mechanical traits updates constants on the GPU
        bool physicsChange =
            _lastSmoothingRadius != settings.smoothingRadius ||
            _lastMass != settings.mass ||
            _lastPressureMultiplier != settings.pressureMultiplier ||
            _lastTargetDensity != settings.targetDensity ||
            _lastCollisionDamping != settings.collisionDamping;

        Debug.Log($"Settings changed. Reinitialize: {requiresReinitialize}, Physics change: {physicsChange}");

        if (requiresReinitialize)
        {
            InitializeSystems();
            return;
        }

        if (physicsChange)
        {
            // Note: Since BindStaticUniforms might be private inside your script, 
            // make sure it is public or expose a designated UpdateUniforms method.
            _physicsSystem.BindStaticUniforms(settings);
        }

        if (_lastColor != settings.particleColor || _lastSmoothness != settings.smoothness)
        {
            _renderSystem.SyncMaterial(settings);
        }

        if (_lastDensityColor != settings.TargetDensityColor || _lastShowDensity != settings.showDensity)
        {
            _densityRenderSystem.SyncMaterial(settings, _physicsSystem.ParticleBuffer);
        }

        CacheSettings();
    }

    void CacheSettings()
    {
        _lastMass = settings.mass;
        _lastPressureMultiplier = settings.pressureMultiplier;
        _lastTargetDensity = settings.targetDensity;
        _lastCollisionDamping = settings.collisionDamping;
        _lastParticleCount = settings.particleCount;
        _lastSegments = settings.segments;
        _lastRadius = settings.radius;
        _lastSmoothingRadius = settings.smoothingRadius;
        _lastParticleSpacing = settings.particleSpacing;
        _lastColor = settings.particleColor;
        _lastDensityColor = settings.TargetDensityColor;
        _lastSmoothness = settings.smoothness;
        _lastShowDensity = settings.showDensity;
    }

    Bounds BuildRenderBounds()
    {
        Vector2 min = BoundsMin;
        Vector2 max = BoundsMax;
        Vector3 center = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, 0f);
        Vector3 size = new Vector3(max.x - min.x, max.y - min.y, 1f);
        return new Bounds(center, size);
    }
}