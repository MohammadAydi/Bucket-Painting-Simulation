using UnityEngine;

[ExecuteAlways]
public class FluidManager3D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] FluidBoundary3D  boundaryVolume;
    [SerializeField] ParticleSettings settings;
    [SerializeField] ComputeShader    fluidComputeShader;
    [SerializeField] Material         particleMaterial;

    SpawnSystem3D       _spawnSystem;
    PhysicsSystem3D     _physicsSystem;
    RenderSystem3D      _renderSystem;
    bool _initialized;

    // ── Settings cache (used to detect what actually changed) ────────────────
    int     _lastParticleCount;
    int     _lastSphereResolution;
    float   _lastRadius;
    float   _lastSmoothingRadius;
    float   _lastParticleSpacing;
    float   _lastVelocityDisplayMax;


    // ── Unity messages ───────────────────────────────────────────────────────

    void Awake()
    {
        if (boundaryVolume == null)
            boundaryVolume = FindObjectOfType<FluidBoundary3D>();
    }

    void Start()
    {
        if (settings != null)
            settings.OnChanged += OnSettingsChanged;

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

        _physicsSystem.Simulate(
            settings,
            Time.deltaTime,
            boundaryVolume.LocalMin,
            boundaryVolume.LocalMax,
            boundaryVolume.WorldToColliderLocalMatrix,
            boundaryVolume.ColliderLocalToWorldMatrix);
    }

    void LateUpdate()
    {
        if (!Application.isPlaying || !_initialized || boundaryVolume == null) return;

        Bounds bounds = boundaryVolume.WorldBounds;
        _renderSystem.Render(_physicsSystem.ParticleBuffer, _physicsSystem.ParticleCount, bounds);

    }

    void OnDestroy()
    {
        if (settings != null)
            settings.OnChanged -= OnSettingsChanged;

        DisposeSystems();
    }

    // ── Public accessors ─────────────────────────────────────────────────────

    public Vector3 BoundsMin => boundaryVolume != null ? boundaryVolume.LocalMin  : Vector3.zero;
    public Vector3 BoundsMax => boundaryVolume != null ? boundaryVolume.LocalMax  : Vector3.one;

    // ── Initialization ───────────────────────────────────────────────────────

    void InitializeSystems()
    {
        if (settings == null || fluidComputeShader == null) return;

        if (boundaryVolume == null)
            boundaryVolume = FindObjectOfType<FluidBoundary3D>();

        if (boundaryVolume == null) return;

        DisposeSystems();

        _spawnSystem   = new SpawnSystem3D(settings);
        _physicsSystem = new PhysicsSystem3D(fluidComputeShader);

        if (particleMaterial == null)
            particleMaterial = new Material(Shader.Find("Fluid/ParticleCircle3D"));

        _renderSystem        = new RenderSystem3D(particleMaterial);
        ParticleData3D[] particles = _spawnSystem.SpawnParticles(boundaryVolume);
        _physicsSystem.Initialize(settings, particles);
        _renderSystem.Initialize(settings);

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

    // ── Settings change handling ─────────────────────────────────────────────

    void OnSettingsChanged()
    {
        if (settings == null || !Application.isPlaying) return;

        bool requiresReinitialize =
            _lastParticleCount     != settings.particleCount     ||
            _lastRadius            != settings.radius            ||
            _lastSmoothingRadius   != settings.smoothingRadius   ||
            _lastParticleSpacing   != settings.particleSpacing   ||
            _lastSphereResolution  != settings.sphereResolution;

        if (requiresReinitialize)
        {
            InitializeSystems();
            return;
        }

        // Only gradient / velocity visuals changed — cheaper sync
        if (_lastVelocityDisplayMax != settings.velocityDisplayMax)
            _renderSystem.SyncMaterial(settings);


        CacheSettings();
    }

    void CacheSettings()
    {
        _lastParticleCount    = settings.particleCount;
        _lastSphereResolution = settings.sphereResolution;
        _lastRadius           = settings.radius;
        _lastSmoothingRadius  = settings.smoothingRadius;
        _lastParticleSpacing  = settings.particleSpacing;
        _lastVelocityDisplayMax = settings.velocityDisplayMax;
    }
}