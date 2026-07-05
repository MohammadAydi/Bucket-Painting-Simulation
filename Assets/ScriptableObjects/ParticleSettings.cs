using UnityEngine;

[CreateAssetMenu(fileName = "ParticleSettings", menuName = "Fluid/Particle Settings")]
public class ParticleSettings : ScriptableObject
{
    // ─────────────────────────────────────────────────────────────────────
    // Solver modes
    // ─────────────────────────────────────────────────────────────────────
    // Each entry here must have a matching #define in FluidMath.hlsl with the
    // same int value (e.g. PressureSolverMode.NearPressure == 1 must match
    // #define PRESSURE_MODE_NEAR_PRESSURE 1). The int value of the enum is
    // sent to the compute shader as-is via Shader.SetInt.
    //
    // To add a new pressure solver: add a value here, add the matching
    // PRESSURE_MODE_XXX define + Compute*PressureForce() function in
    // FluidMath.hlsl, and a case for it in CalculatePressureForces in
    // FluidCompute3D.compute. No other C# or shader plumbing changes needed.

    [Header("Count & Shape")]
    [Range(1, 1000000)]
    public int particleCount = 400000;
    [Range(0.01f, 1f)]
    public float radius = 0.04f;
    [Range(3, 32)]
    public int segments = 8;

    // Number of subdivisions for the icosphere mesh (0 = octahedron, 1–4 = smoother).
    // Matches SphereGenerator.GenerateSphereMesh(resolution).
    [Range(0, 4)]
    public int sphereResolution = 2;

    [Header("Appearance")]
    [Range(0.0001f, 10)]
    public float particleSpacing = 0.0001f;
    public Color particleColor = new Color(0f, 0f, 0f, 1f);
    public bool useVertexColor = false;


    // ── Velocity-based colour gradient ───────────────────────────────────────
    // Gradient is baked into a 1-D texture inside RenderSystem3D and sampled
    // in the shader based on each particle's speed (0 → velocityDisplayMax).
    [Header("Velocity Visualization")]
    public bool useSpeedColor = false; // Added toggle variable here
    public Gradient colourMap = DefaultGradient();
    [Range(16, 256)]
    public int gradientResolution = 64;
    [Range(0.1f, 50f)]
    public float velocityDisplayMax = 8f;

    [Header("Mouse Interaction Settings")]
    [SerializeField] public float interactionRadius = 3.0f;
    [SerializeField] public float interactionStrength = 60.0f;

    [Header("Density Visualization")]
    public bool showDensity = true;
    [Range(0f, 1f)]
    public float smoothness = 1f;
    [Range(0.01f, 4f)]
    public float smoothingRadius = 0.2f;

    public Color lowDensityColor = new Color(0.1266f, 0.5330f, 0.6886f, 1f);
    public Color TargetDensityColor = new Color(1f, 1f, 1f, 1f);
    public Color highDensityColor = new Color(0.8301f, 0.2914f, 0.2075f, 1f);

    [Header("Physics")]
    [Range(0, 20)]
    public float mass = 1f;

    [Range(-20f, 20f)]
    public float gravity = -10f;
    [Range(0f, 1f)]
    public float collisionDamping = 0.95f;

    [Header("Pressure Solver")]
    // Switch between pressure solver implementations to compare behaviour.
    // Standard: classic SPH pressure only (your original solver).
    // NearPressure: Standard + an additional near-density/near-pressure term
    // for stronger short-range repulsion (matches the instructor reference).
    public PressureSolverMethod pressureSolverMethod = PressureSolverMethod.NearPressure;
    [Range(0f, 1000f)]
    public float pressureMultiplier = 288f;
    [Range(0f, 1000f)]
    public float targetDensity = 630f;
    // β — near-pressure multiplier (only used when pressureSolverMode == NearPressure).
    // Scales the extra short-range repulsion driven by near density. Start small
    // (around the same order as pressureMultiplier) and increase if particles
    // still visibly clump/overlap at high density.
    [Range(0f, 1000f)]
    public float nearPressureMultiplier = 2.15f;

    [Header("Viscosity")]

    public ViscositySolverMethod viscositySolverMethod = ViscositySolverMethod.Standard;
    // μ — dynamic viscosity. Higher values make the fluid thicker (more honey-like).
    // Start around 0.1–0.5 for water-like behaviour; raise to 2–10 for syrup.
    [Range(0f, 1000f)]
    public float viscosityCoeff = 0f;

    [Header("Surface Tension")]
    public SurfaceTensionSolverMethod surfaceTensionSolverMethod = SurfaceTensionSolverMethod.Standard;

    // σ — surface tension coefficient. Controls how strongly the surface minimises
    // its curvature. Start small (0.01–0.1) to avoid numerical blow-up.
    [Range(0f, 100)]
    public float surfaceTensionCoeff = 0.0f;
    // l — surface-normal threshold (Eq. 23). Force is only applied where |∇cₛ| > l,
    // i.e., near an actual surface. Raise this if interior particles fire the kernel.
    [Range(0f, 2f)]
    public float surfaceTensionThreshold = 0.0f;


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