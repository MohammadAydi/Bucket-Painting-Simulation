using System;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// PigmentSettings
// ScriptableObject that holds all pigment diffusion parameters.
// Follows the same pattern as ParticleSettings.
// ─────────────────────────────────────────────────────────────────────────────
[CreateAssetMenu(menuName = "Fluid/PigmentSettings", fileName = "PigmentSettings")]
public class PigmentSettings : ScriptableObject
{
    public event Action OnChanged;

    // ─────────────────────────────────────────────────────────────────────
    // Pigment mixing algorithm
    // ─────────────────────────────────────────────────────────────────────
    // LinearRGB : diffuse raw linear RGB (original behavior). Physically
    //             correct for LIGHT, but paint pairs like yellow+blue
    //             produce gray instead of green.
    // RYB       : from-scratch, no external library. Re-parameterizes color
    //             as amounts of Red/Yellow/Blue pigment (the classic 8-corner
    //             RYB cube interpolation) before diffusing, then converts
    //             back to RGB for display. Yellow+blue -> green.
    //             See Physics/Pigment/ColorMixing.hlsl and RYBColorMixing.cs.
    // Mixbox    : uses the Mixbox library (scrtwpns/mixbox) for physically
    //             modeled (Kubelka-Munk) pigment mixing. Requires adding the
    //             Mixbox Unity package and assigning the LUT + the
    //             "Pigment Compute Shader (Mixbox)" slot on FluidManager3D.
    //             CC BY-NC 4.0 license — non-commercial use only.
    public enum PigmentMixingModel { LinearRGB, RYB, Mixbox }

    [Header("Mixing Model")]
    [Tooltip("How particle pigment colors combine when they diffuse into each other.")]
    public PigmentMixingModel mixingModel = PigmentMixingModel.RYB;

    [Header("Diffusion")]
    [Tooltip("How fast pigment diffuses between neighbouring particles. " +
             "Mirrors ViscosityCoeff: 0 = no diffusion, higher = faster mixing.")]
    [Range(0f, 2f)]
    public float diffusionCoeff = 0.4f;

    [Header("Spawn Colors")]
    [Tooltip("One color per particle group. The SpawnSystem will assign each " +
             "particle its group color as the initial pigment value.")]
    public Color[] spawnColors =
    {
        new Color(1.00f, 0.00f, 0.00f), // Red
        new Color(1.00f, 0.50f, 0.00f), // Orange
        new Color(1.00f, 1.00f, 0.00f), // Yellow
        new Color(0.00f, 1.00f, 0.00f), // Green
        new Color(0.00f, 0.00f, 1.00f), // Blue
        new Color(0.29f, 0.00f, 0.51f), // Indigo
        new Color(0.56f, 0.00f, 1.00f), // Violet
    };
    void OnValidate() => OnChanged?.Invoke();
}
