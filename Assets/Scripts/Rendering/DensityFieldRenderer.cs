using UnityEngine;

public class DensityFieldRenderer : MonoBehaviour {
    [Header("References")] public FluidSimulation fluidSimulation;
    public Material densityFieldMaterial;

    [Header("Density Coloring")] public float targetDensity = 2f;

    public Color lowDensityColor = new Color(0.1266f, 0.5330f, 0.6886f, 1f);
    public Color targetDensityColor = new Color(1f, 1f, 1f, 1f);
    public Color highDensityColor = new Color(0.8301f, 0.2914f, 0.2075f, 1f);
    // public Color lowDensityColor = new Color(0f, 0f, 0f, 1f);
    // public Color targetDensityColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
    // public Color highDensityColor = new Color(1f, 1f, 1f, 1f);

    // cached shader property IDs
    static readonly int PositionsID = Shader.PropertyToID("_Positions");
    static readonly int MassID = Shader.PropertyToID("_Mass");
    static readonly int ParticleCountID = Shader.PropertyToID("_ParticleCount");
    static readonly int SmoothingRadID = Shader.PropertyToID("_SmoothingRadius");
    static readonly int TargetDensityID = Shader.PropertyToID("_TargetDensity");
    static readonly int LowColorID = Shader.PropertyToID("_LowColor");
    static readonly int TargetColorID = Shader.PropertyToID("_TargetColor");
    static readonly int HighColorID = Shader.PropertyToID("_HighColor");
    static readonly int BoundsMinID = Shader.PropertyToID("_BoundsMin");
    static readonly int BoundsMaxID = Shader.PropertyToID("_BoundsMax");


    // shader needs float4 array — we pack Vector2 positions into Vector4
    Vector4[] _positionsV4 = new Vector4[700];

    void Start() {
        Debug.Log("start density field renderer");
        Camera cam = Camera.main;
        float height = cam.orthographicSize * 2f;
        float width = height * cam.aspect;
        Debug.Log("height: " + height + " width: " + width);
        transform.localScale = new Vector3(width, height, 1f);
        transform.position = new Vector3(0f, 0f, 0.1f);
    }

    void Update() {
        if (!Application.isPlaying) return;
        if (densityFieldMaterial is null) return;

        Vector2[] positions = fluidSimulation.Positions;
        int count = Mathf.Min(fluidSimulation.Positions.Length, 700);
        float smoothRadius = fluidSimulation.settings.smoothingRadius;

        // pack Vector2 → Vector4 (shader arrays must be float4)
        for (int i = 0; i < count; i++) {
            _positionsV4[i] = new Vector4(positions[i].x, positions[i].y, 0f, 0f);
        }

        densityFieldMaterial.SetVectorArray(PositionsID, _positionsV4);
        densityFieldMaterial.SetInt(ParticleCountID, count);
        densityFieldMaterial.SetFloat(SmoothingRadID, smoothRadius);
        densityFieldMaterial.SetFloat(MassID, fluidSimulation.settings.mass);
        densityFieldMaterial.SetFloat(TargetDensityID, targetDensity);
        densityFieldMaterial.SetColor(LowColorID, lowDensityColor);
        densityFieldMaterial.SetColor(TargetColorID, targetDensityColor);
        densityFieldMaterial.SetColor(HighColorID, highDensityColor);
        densityFieldMaterial.SetVector(BoundsMinID, fluidSimulation.BoundsMin);
        densityFieldMaterial.SetVector(BoundsMaxID, fluidSimulation.BoundsMax);
    }
}