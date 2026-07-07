// RaymarchSettings.cs
// ─────────────────────────────────────────────────────────────────────────────
// Data class referenced by FluidRendererSettings.raymarchSettings.
// This file was missing from the project — the compiler would have thrown
// CS0246 for any file that references RaymarchSettings.
//
// densityOffset tuning guide:
//   Too high  → surface never found, nothing renders.
//   Too low   → surface appears before the fluid volume, looks inflated.
//   Start at ~10–30 and adjust until the surface sits on the particle cloud.
//   The right value depends on your particle count, mass, and smoothing radius.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;

[System.Serializable]
public class RaymarchSettings
{
    [Header("Voxelization")]
    [Tooltip("Resolution of the 3D density texture (N×N×N). Higher = more detail but more GPU cost. Powers of 2 preferred.")]
    [Range(16, 256)]
    public int voxelResolution = 64;

    [Header("Raymarching")]
    [Tooltip("Iso-surface threshold. Pixels where density > this value are considered inside the fluid. " +
             "Start at 10–30 and tune up/down until the surface aligns with the particle cloud.")]
    public float densityOffset = 20f;

    [Tooltip("Primary ray step size in world units. Smaller = more accurate surface but more steps.")]
    public float stepSize = 0.03f;

    [Tooltip("Finite-difference epsilon for normal estimation. Should be ~1–2× stepSize.")]
    public float normalEpsilon = 0.06f;
}