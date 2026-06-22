using System;
using System.Threading.Tasks;
using Rendering;
using static UnityEngine.Mathf;
using UnityEngine;
using Random = UnityEngine.Random;

[ExecuteAlways]
public class FluidSimulation : MonoBehaviour {
    [Header("References")] public ProceduralParticleRenderer particleRenderer;
    public ParticleSettings settings;
    public ParticlesSpawner particlesSpawner;
    [SerializeField] Camera cameraRef;
    private static readonly System.Random _random = new();




    Vector2[] _positions;
    public float[] particleProperty;
    Vector2[] _velocities;
    float[] _densities;

    public float[] Densities => _densities;


    float _lastRadius;
    float _lastSmoothingRadius;
    float _lastParticleSpacing;
    float _lastSmoothness;
    int _lastCount;
    int _lastSegment;
    Color _lastColor;
    Color _lastDensityColor;
    bool _lastShowDensity;

    public Vector2[] Positions => _positions;
    


    public float gravity = 9.81f;

    public Vector2 BoundsMin {
        get {
            float halfHeight = cameraRef.orthographicSize;
            float halfWidth = halfHeight * cameraRef.aspect;

            return new Vector2(
                cameraRef.transform.position.x - halfWidth,
                cameraRef.transform.position.y - halfHeight
            );
        }
    }

    public Vector2 BoundsMax {
        get {
            float halfHeight = cameraRef.orthographicSize;
            float halfWidth = halfHeight * cameraRef.aspect;

            return new Vector2(
                cameraRef.transform.position.x + halfWidth,
                cameraRef.transform.position.y + halfHeight
            );
        }
    }

    void Awake() {
        particlesSpawner = new ParticlesSpawner(settings);
        if (cameraRef == null)
            cameraRef = Camera.main;
    }

    void Start() {
        particleRenderer = GetComponent<ProceduralParticleRenderer>();

        particleRenderer.Initialize(settings);

        _densities = new float[settings.particleCount];
        _velocities = new Vector2[settings.particleCount];
        Array.Fill(_velocities, Vector2.zero);
        (_positions, particleProperty) = particlesSpawner.RandomSpawnParticles(BoundsMin, BoundsMax);
        particleRenderer.UpdatePositions(_positions);

        ApplyDensityVisibility();

        _lastRadius = settings.radius;
        _lastSmoothingRadius = settings.smoothingRadius;
        _lastCount = settings.particleCount;
        _lastColor = settings.color;
        _lastDensityColor = settings.densityColor;
        _lastParticleSpacing = settings.particleSpacing;
        _lastSmoothness = settings.smoothness;
        _lastShowDensity = settings.showDensity;
        settings.OnChanged += OnSettingsChanged;
    }

    void OnValidate() {
        if (Application.isPlaying) return;
        if (settings == null) return;

        if (particleRenderer == null)
            particleRenderer = GetComponent<ProceduralParticleRenderer>();

        particleRenderer.Initialize(settings);

        var result = particlesSpawner?.RandomSpawnParticles(BoundsMin, BoundsMax);

        if (result.HasValue) {
            (_positions, particleProperty) = result.Value;
        }

        if (_positions != null) {
            particleRenderer.UpdatePositions(_positions);
        }

        ApplyDensityVisibility();
     
    }

    void OnDestroy() {
        settings.OnChanged -= OnSettingsChanged;
    }

    void OnSettingsChanged() {
        if (_lastCount != settings.particleCount) {
            particleRenderer.Initialize(settings);
            (_positions, particleProperty) = particlesSpawner.RandomSpawnParticles(BoundsMin, BoundsMax);
            particleRenderer.UpdatePositions(_positions);
            _lastCount = settings.particleCount;
        }

        if (_lastRadius != settings.radius) {
            particleRenderer.Initialize(settings);
            particleRenderer.UpdatePositions(_positions);
            _lastRadius = settings.radius;
        }

        if (_lastSmoothingRadius != settings.smoothingRadius) {
            _lastSmoothingRadius = settings.smoothingRadius;
        }

        if (_lastColor != settings.color) {
            particleRenderer.SetColor(settings.color);
            _lastColor = settings.color;
        }

        if (_lastDensityColor != settings.densityColor) {
            _lastDensityColor = settings.densityColor;
        }

        if (_lastParticleSpacing != settings.particleSpacing) {
            (_positions, particleProperty) = particlesSpawner.RandomSpawnParticles(BoundsMin, BoundsMax);
            particleRenderer.UpdatePositions(_positions);
            _lastParticleSpacing = settings.particleSpacing;
        }

        if (_lastSegment != settings.segments) {
            particleRenderer.Initialize(settings);
            particleRenderer.UpdatePositions(_positions);
            _lastSegment = settings.segments;
        }

        if (_lastSmoothness != settings.smoothness) {
         
            _lastSmoothness = settings.smoothness;
        }

        if (_lastShowDensity != settings.showDensity) {
            ApplyDensityVisibility();
            _lastShowDensity = settings.showDensity;
        }
    }

    void Update() {
        if (!Application.isPlaying) return;
        // UpdateDensities();
        Simulate(Time.deltaTime);
        particleRenderer.UpdatePositions(_positions);
    }
    

    // shows or hides the density renderer GameObject
    void ApplyDensityVisibility() {
    }
    


    float ConvertDensityToPressure(float density) {
        float densityError = density - settings.targetDensity;
        float pressure = densityError * settings.pressureMultiplier;
        return pressure;
    }


    float SmoothingKernelDerivative(float dst, float radius) {
        if (dst >= radius) return 0;
        float scale = 12 / (Pow(radius, 4) * PI);
        return (dst - radius) * scale;
    }

    float CalculateSharedPressure(float densityA, float densityB) {
        float pressureA = ConvertDensityToPressure(densityA);
        float pressureB = ConvertDensityToPressure(densityB);
        return (pressureA + pressureB) / 2;
    }

    Vector2 CalculatePressureForce(int particleIndex) {
        Vector2 pressureForce = Vector2.zero;
        for (int otherParticleIndex = 0; otherParticleIndex < _positions.Length; otherParticleIndex++) {
            if (particleIndex == otherParticleIndex) continue;
            Vector2 offset = _positions[otherParticleIndex] - _positions[particleIndex];
            float dst = offset.magnitude;
            Vector2 dir = dst == 0 ? GetRandomDir() : offset / dst;
            float slope = SmoothingKernelDerivative(dst, settings.smoothingRadius);
            float density = _densities[otherParticleIndex];
            float sharedPressure = CalculateSharedPressure(density, _densities[particleIndex]);
            pressureForce += dir * (sharedPressure * slope * settings.mass) / density; // mass = 1
        }

        return pressureForce;
    }

    private static Vector2 GetRandomDir() {
        lock (_random) {
            float angle = (float)(_random.NextDouble() * Math.PI * 2.0);
            return new Vector2(Cos(angle), Sin(angle));
        }
    }

    private void ResolveCollision(ref Vector2 position, ref Vector2 velocity, Vector2 halfBoundsSize) {
        if (Mathf.Abs(position.x) > halfBoundsSize.x) {
            position.x = halfBoundsSize.x * Mathf.Sign(position.x);
            velocity.x *= -settings.collisionDamping;
        }

        if (Mathf.Abs(position.y) > halfBoundsSize.y) {
            position.y = halfBoundsSize.y * Mathf.Sign(position.y);
            velocity.y *= -settings.collisionDamping;
        }
    }

    void Simulate(float dt) {
        Vector2 halfBoundsSize = BoundsMax - Vector2.one * settings.radius;
        Parallel.For(0, _positions.Length, i => {
            // _velocities[i] += Vector2.down * (gravity * dt);
            _densities[i] = DensityCalculator.CalculateDensity(
                _positions[i], _positions, settings.smoothingRadius);
        });

        Parallel.For(0, _positions.Length, i => {
            Vector2 pressureForce = CalculatePressureForce(i);
            Vector2 PressureAcceleration = pressureForce / _densities[i];
            _velocities[i] += PressureAcceleration * dt;
        });

        Parallel.For(0, _positions.Length, i => {
            _positions[i] += _velocities[i] * dt;
            ResolveCollision(ref _positions[i], ref _velocities[i], halfBoundsSize);
        });
    }
}