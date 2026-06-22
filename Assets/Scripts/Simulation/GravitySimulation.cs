using System;
using Rendering;
using static UnityEngine.Mathf;
using UnityEngine;
using Random = UnityEngine.Random;

[ExecuteAlways]
public class GravitySimulation : MonoBehaviour {
     [Header("References")] public ProceduralParticleRenderer particleRenderer;
    public ParticleSettings settings;
    public ParticlesSpawner particlesSpawner;

    // ── simulation state ────────────────────────────────────────────────────
    Vector2[] _positions;
    Vector2[] _velocities;

    float _lastRadius;
    float _lastParticleSpacing;
    int _lastCount;
    int _lastSegment;
    Color _lastColor;

    [Header("References")] public BoundsRenderer boundsRenderer; // drag it in the Inspector

    public float gravity = 9.81f;


// Replace the two fields used in Simulate() and SpawnParticles()
    private Vector2 BoundsMin => boundsRenderer.BoundsMin;
    private Vector2 BoundsMax => boundsRenderer.BoundsMax;

    private void Awake() {
        particlesSpawner = new ParticlesSpawner(settings);
        _velocities = new Vector2[settings.particleCount];
        _positions = new Vector2[settings.particleCount];
        Debug.Log("Awake FluidSimulation");
    }

    void Start() {
        Debug.Log("Starting FluidSimulation");
        particleRenderer = GetComponent<ProceduralParticleRenderer>();
        boundsRenderer = GetComponent<BoundsRenderer>();
        particleRenderer.Initialize(settings);
        (_positions, _) = particlesSpawner.RandomSpawnParticles(BoundsMin, BoundsMax);
        
        _lastRadius = settings.smoothingRadius;
        _lastCount = settings.particleCount;
        _lastColor = settings.color;
        _lastParticleSpacing = settings.particleSpacing;

        // Subscribe to inspector changes
        settings.OnChanged += OnSettingsChanged;
    }

    void OnValidate() {
        Debug.Log("OnValidate FluidSimulation");
        if (Application.isPlaying) {
            return;
        }

        if (settings == null) {
            return;
        }
    }

    void OnDestroy() {
        settings.OnChanged -= OnSettingsChanged;
    }


    void OnSettingsChanged() {
        Debug.Log("Settings Changed");
        if (_lastCount != settings.particleCount) {
            particleRenderer.Initialize(settings);
            (_positions, _)  = particlesSpawner.RandomSpawnParticles(BoundsMin, BoundsMax);
            particleRenderer.UpdatePositions(_positions);
            _lastCount = settings.particleCount;
        }
        else if (_lastRadius != settings.smoothingRadius) {
            particleRenderer.Initialize(settings);
            particleRenderer.UpdatePositions(_positions);
            _lastRadius = settings.smoothingRadius;
        }
        

        if (_lastColor != settings.color) {
            particleRenderer.SetColor(settings.color);
            _lastColor = settings.color;
        }

        if (_lastParticleSpacing != settings.particleSpacing) {
            (_positions, _)  = particlesSpawner.RandomSpawnParticles(BoundsMin, BoundsMax);
            particleRenderer.UpdatePositions(_positions);
            _lastParticleSpacing = settings.particleSpacing;
        }

        if (_lastSegment != settings.segments) {
            particleRenderer.Initialize(settings);
            particleRenderer.UpdatePositions(_positions);
            _lastSegment = settings.segments;
        }
    }

    // Update() becomes clean again — no settings checks
    void Update() {
        if (!Application.isPlaying)
            return;
        Simulate(Time.deltaTime);
        particleRenderer.UpdatePositions(_positions);
    }

    // ── runtime controls (wire these to UI sliders) ─────────────────────────
    public void OnColorChanged(Color c) => particleRenderer.SetColor(c);

    // ── private ─────────────────────────────────────────────────────────────


    void Simulate(float dt) {
        Vector2 halfBoundsSize = BoundsMax - Vector2.one * _lastRadius;
        for (int i = 0; i < _positions.Length; i++) {
            _velocities[i] += Vector2.down * (gravity * dt);
            _positions[i] += _velocities[i] * dt;

            // Bounce off bounds
            if (Abs(_positions[i].x) > halfBoundsSize.x) {
                _positions[i].x = halfBoundsSize.x * Sign(_positions[i].x);
                _velocities[i].x *= -settings.collisionDamping;
            }

            if (Abs(_positions[i].y) > halfBoundsSize.y) {
                _positions[i].y = halfBoundsSize.y * Sign(_positions[i].y);
                _velocities[i].y *= -settings.collisionDamping;
            }
        }
    }
}