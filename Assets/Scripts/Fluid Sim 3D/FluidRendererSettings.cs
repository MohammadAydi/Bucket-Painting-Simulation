// FluidRendererSettings.cs
// Add this to your FluidManager GameObject (or any scene object).
// The Renderer Feature reads settings FROM this component automatically.
// You never need to open the URP Renderer Asset again.

using UnityEngine;

public class FluidRendererSettings : MonoBehaviour
{
    public enum FluidRenderMode { ScreenSpace, Raymarch }
    public FluidRenderMode renderMode;
    [Header("Particle Depth")]
    public float depthParticleSize = 0.15f;

    [Header("Blur Type")]
    public FluidRendererFeature.FluidBlurType blurType = FluidRendererFeature.FluidBlurType.Bilateral1D;

    [Header("Bilateral Settings")]
    public FluidRendererFeature.BilateralFilterSettings bilateralSettings = new FluidRendererFeature.BilateralFilterSettings
    {
        worldRadius        = 0.13f,
        maxScreenSpaceSize = 32,
        strength           = 0.45f,
        diffStrength       = 3.7f,
        iterations         = 5
    };

    [Header("Paint / Composite")]
    [ColorUsage(false, true)]
    public Color paintColor         = new Color(0.2f, 0.5f, 1f);
    public float specularStrength   = 0.8f;
    public float specularShininess  = 64f;
    [Range(0f, 1f)]
    public float reflectionStrength = 0.15f;

    [Header("Lighting Fill")]
    [Range(0f, 1f)]
    public float ambientStrength    = 0.25f;
    public bool  useHalfLambert     = true;
    [Range(0f, 1f)]
    public float fillLightStrength  = 0.2f;
    public Color fillLightColor     = new Color(0.4f, 0.35f, 0.3f);
}
