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
