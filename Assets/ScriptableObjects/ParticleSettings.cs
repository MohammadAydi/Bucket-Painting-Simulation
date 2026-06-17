using UnityEngine;

[CreateAssetMenu(fileName = "ParticleSettings", menuName = "Fluid/Particle Settings")]
public class ParticleSettings : ScriptableObject
{
    [Header("Count & Shape")]
    [Range(1, 1000)]
    public int   particleCount = 100;
    [Range(0.01f, 1f)]
    public float radius        = 0.1f;
    [Range(3, 32)]
    public int   segments      = 8;       // polygon approximation of a circle

    [Header("Appearance")]
    public Color color         = new Color(0.2f, 0.6f, 1f, 0.85f);
    public bool  useVertexColor = false;  // override per-particle if needed
    
    [Range(0.01f, 10)]
    public float particleSpacing = 1f;

    // [Header("Simulation Bounds")]
    // public Vector2 boundsMin   = new(-10f, -5f);
    // public Vector2 boundsMax   = new( 10f,  5f);
    
    public System.Action OnChanged;

    void OnValidate()
    {
        Debug.Log("ParticleSettings OnValidate");
        OnChanged?.Invoke();
    }
}