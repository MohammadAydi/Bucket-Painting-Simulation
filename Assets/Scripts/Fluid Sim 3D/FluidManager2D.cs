using UnityEngine;

[ExecuteAlways]
public class FluidManager3D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Camera cameraRef;
    [SerializeField] ParticleSettings settings;
    [SerializeField] ComputeShader fluidComputeShader;
    [SerializeField] Material particleMaterial;
    [SerializeField] Material densityFieldMaterial;

    SpawnSystem3D _spawnSystem;
    PhysicsSystem3D _physicsSystem;
    RenderSystem3D _renderSystem;
    DensityRenderSystem3D _densityRenderSystem;
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

    void Awake()
    {
        if (cameraRef == null)
        {
            cameraRef = Camera.main;
        }
    }

    void Start()
    {
        if (settings != null)
        {
            settings.OnChanged += OnSettingsChanged;
        }

        InitializeSystems();
    }

    void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        if (cameraRef == null)
        {
            cameraRef = Camera.main;
        }

        InitializeSystems();
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
        if (!Application.isPlaying || !_initialized)
        {
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

        _spawnSystem = new SpawnSystem3D(settings);
        _physicsSystem = new PhysicsSystem3D(fluidComputeShader);

        if (particleMaterial == null)
        {
            particleMaterial = new Material(Shader.Find("Fluid/ParticleCircle"));
        }

        if (densityFieldMaterial == null)
        {
            densityFieldMaterial = new Material(Shader.Find("Fluid/DensityField"));
        }

        _renderSystem = new RenderSystem3D(particleMaterial);
        _densityRenderSystem = new DensityRenderSystem3D(densityFieldMaterial);

        ParticleData3D[] particles = _spawnSystem.SpawnParticles(BoundsMin, BoundsMax);
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
        if (settings == null || !Application.isPlaying)
        {
            return;
        }

        bool requiresReinitialize =
            _lastParticleCount != settings.particleCount ||
            _lastRadius != settings.radius ||
            _lastSmoothingRadius != settings.smoothingRadius ||
            _lastParticleSpacing != settings.particleSpacing ||
            _lastSegments != settings.segments;

        if (requiresReinitialize)
        {
            InitializeSystems();
            return;
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