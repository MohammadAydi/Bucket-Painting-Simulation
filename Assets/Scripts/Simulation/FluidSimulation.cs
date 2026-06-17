using System;
using static UnityEngine.Mathf;
using UnityEngine;

[ExecuteAlways]
public class FluidSimulation : MonoBehaviour {
    [Header("References")] public ProceduralParticleRenderer particleRenderer;
    public ParticleSettings settings;

    // ── simulation state ────────────────────────────────────────────────────
    Vector2[] _positions;
    Vector2[] _velocities;

    float _lastRadius;
    float _lastParticleSpacing;
    int _lastCount;
    int _lastSegment;
    Color _lastColor;

    [Header("References")] public BoundsRenderer boundsRenderer; // drag it in the Inspector

    public float mass = 1f;
    public float gravity = 9.81f;
    public float collisionDamping = 0.8f;


// Replace the two fields used in Simulate() and SpawnParticles()
    private Vector2 BoundsMin => boundsRenderer.BoundsMin;
    private Vector2 BoundsMax => boundsRenderer.BoundsMax;

    private void Awake() {
        Debug.Log("Awake FluidSimulation");
    }

    void Start() {
        Debug.Log("Starting FluidSimulation");
        particleRenderer = GetComponent<ProceduralParticleRenderer>();
        boundsRenderer = GetComponent<BoundsRenderer>();
        particleRenderer.Initialize(settings);
        SpawnParticles();

        _lastRadius = settings.radius;
        _lastCount = settings.particleCount;
        _lastColor = settings.color;
        _lastParticleSpacing = settings.particleSpacing;

        // Subscribe to inspector changes
        settings.OnChanged += OnSettingsChanged;
    }
    
    void OnValidate()
    {
        Debug.Log("OnValidate FluidSimulation");
        if (Application.isPlaying) {
            return;
        }
        if (settings == null) {
            return;
        }
        if (particleRenderer == null)
            particleRenderer = GetComponent<ProceduralParticleRenderer>();
        if (boundsRenderer == null)
            boundsRenderer = GetComponent<BoundsRenderer>();
        particleRenderer.Initialize(settings);
        SpawnParticles();
        particleRenderer.UpdatePositions(_positions);
    }

    void OnDestroy() {
        Debug.Log("Destroy FluidSimulation");
        settings.OnChanged -= OnSettingsChanged;
    }
    

    void OnSettingsChanged() {
        Debug.Log("Settings Changed");
        if (_lastCount != settings.particleCount) {
            particleRenderer.Initialize(settings);
            SpawnParticles();
            particleRenderer.UpdatePositions(_positions);
            _lastCount = settings.particleCount;
        }
        else if (_lastRadius != settings.radius) {
            particleRenderer.Initialize(settings);
            particleRenderer.UpdatePositions(_positions);
            _lastRadius = settings.radius;
        }

        if (_lastColor != settings.color) {
            particleRenderer.SetColor(settings.color);
            _lastColor = settings.color;
        }
        if (_lastParticleSpacing != settings.particleSpacing) {
            SpawnParticles();
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
    public void OnRadiusChanged(float r) => particleRenderer.SetRadius(r);

    // ── private ─────────────────────────────────────────────────────────────
  void SpawnParticles() {
    int n = settings.particleCount;
    _positions = new Vector2[n];
    _velocities = new Vector2[n];

    int particlesPerRow = (int)Sqrt(n);
    int particlesPerCol = (n - 1) / particlesPerRow + 1;
    float spacing = settings.radius * 2 + settings.particleSpacing;  // <-- fix

    for (int i = 0; i < n; i++) {
        float x = (i % particlesPerRow - particlesPerRow / 2f + 0.5f) * spacing;
        float y = (i / particlesPerRow - particlesPerCol / 2f + 0.5f) * spacing;
        _positions[i] = new Vector2(x, y);
    }
}

    void Simulate(float dt) {
        Vector2 halfBoundsSize = BoundsMax - Vector2.one * _lastRadius;
        for (int i = 0; i < _positions.Length; i++) {
            _velocities[i] += Vector2.down * (gravity * dt);
            _positions[i] += _velocities[i] * dt;

            // Bounce off bounds
            if (Abs(_positions[i].x) > halfBoundsSize.x) {
                _positions[i].x = halfBoundsSize.x * Sign(_positions[i].x);
                _velocities[i].x *= -collisionDamping;
            }

            if (Abs(_positions[i].y) > halfBoundsSize.y) {
                _positions[i].y = halfBoundsSize.y * Sign(_positions[i].y);
                _velocities[i].y *= -collisionDamping;
            }
        }
    }
}