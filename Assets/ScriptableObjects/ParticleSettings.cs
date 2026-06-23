using UnityEngine;

[CreateAssetMenu(fileName = "ParticleSettings", menuName = "Fluid/Particle Settings")]
public class ParticleSettings : ScriptableObject
{
    [Header("Count & Shape")]
    [Range(1, 10000)]
    public int particleCount = 100;
    [Range(0.01f, 1f)]
    public float radius = 0.1f;
    [Range(3, 32)]
    public int segments = 8;

    [Header("Appearance")]
    public Color particleColor = new Color(0f, 0f, 0f, 1f);
    public bool useVertexColor = false;
    [Range(0.01f, 10)]
    public float particleSpacing = 1f;

    [Header("Density Visualization")]
    public bool showDensity = true;
    [Range(0f, 1f)]
    public float smoothness = 1f;
    [Range(0.01f, 4f)]
    public float smoothingRadius = 1f;

    public Color lowDensityColor = new Color(0.1266f, 0.5330f, 0.6886f, 1f);
    public Color TargetDensityColor = new Color(1f, 1f, 1f, 1f);
    public Color highDensityColor = new Color(0.8301f, 0.2914f, 0.2075f, 1f);

    [Header("Physics")]
    [Range(0, 20)]
    public float mass = 1f;
    [Range(0f, 1f)]
    public float collisionDamping = 0.8f;

    [Range(0f, 500f)]
    public float pressureMultiplier = 2.0f;

    [Range(0f, 10f)]
    public float targetDensity = 2.0f;

    public System.Action OnChanged;

    void OnValidate()
    {
        OnChanged?.Invoke();
    }
}