using UnityEngine;

[CreateAssetMenu(fileName = "ParticleSettings", menuName = "Fluid/Particle Settings")]
public class ParticleSettings : ScriptableObject
{
    [Header("Count & Shape")]
    [Range(1, 10000)]
    public int particleCount = 1000;
    [Range(0.01f, 1f)]
    public float radius = 0.1f;
    [Range(3, 32)]
    public int segments = 8;

    // Number of subdivisions for the icosphere mesh (0 = octahedron, 1–4 = smoother).
    // Matches SphereGenerator.GenerateSphereMesh(resolution).
    [Range(0, 4)]
    public int sphereResolution = 2;

    [Header("Appearance")]
    [Range(0.0001f, 10)]
    public float particleSpacing = 1f;
    public Color particleColor = new Color(0f, 0f, 0f, 1f);
    public bool useVertexColor = false;


    // ── Velocity-based colour gradient ───────────────────────────────────────
    // Gradient is baked into a 1-D texture inside RenderSystem3D and sampled
    // in the shader based on each particle's speed (0 → velocityDisplayMax).
    [Header("Velocity Visualization")]
    public bool useSpeedColor = false; // Added toggle variable here
    public Gradient colourMap = DefaultGradient();
    [Range(16, 256)]
    public int gradientResolution = 128;
    [Range(0.1f, 50f)]
    public float velocityDisplayMax = 5f;

    [Header("Mouse Interaction Settings")]
    [SerializeField] public float interactionRadius = 3.0f;
    [SerializeField] public float interactionStrength = 60.0f;

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

    [Range(-20f, 20f)]
    public float gravity = -9.81f;
    [Range(0f, 1f)]
    public float collisionDamping = 0.8f;
    [Range(0f, 500f)]
    public float pressureMultiplier = 2.0f;
    [Range(0f, 100f)]
    public float targetDensity = 2.0f;

    [Header("Viscosity")]
    // μ — dynamic viscosity. Higher values make the fluid thicker (more honey-like).
    // Start around 0.1–0.5 for water-like behaviour; raise to 2–10 for syrup.
    [Range(0f, 1000f)]
    public float viscosityCoeff = 0.1f;

    [Header("Surface Tension")]
    // σ — surface tension coefficient. Controls how strongly the surface minimises
    // its curvature. Start small (0.01–0.1) to avoid numerical blow-up.
    [Range(0f, 100)]
    public float surfaceTensionCoeff = 0.07f;
    // l — surface-normal threshold (Eq. 23). Force is only applied where |∇cₛ| > l,
    // i.e., near an actual surface. Raise this if interior particles fire the kernel.
    [Range(0f, 2f)]
    public float surfaceTensionThreshold = 0.1f;


    public System.Action OnChanged;

    void OnValidate() => OnChanged?.Invoke();

    // ── Default gradient: blue → cyan → green → yellow → red (like instructor) ─
    static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.07f, 0.38f, 0.82f), 0.00f),   // blue
                new GradientColorKey(new Color(0.04f, 0.76f, 0.60f), 0.33f),   // teal-green
                new GradientColorKey(new Color(0.95f, 0.82f, 0.13f), 0.66f),   // yellow
                new GradientColorKey(new Color(0.90f, 0.25f, 0.13f), 1.00f),   // red-orange
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f),
            });
        return g;
    }
}