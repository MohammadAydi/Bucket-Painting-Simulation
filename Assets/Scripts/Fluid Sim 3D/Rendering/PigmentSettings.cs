using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Fluid/PigmentSettings", fileName = "PigmentSettings")]
public class PigmentSettings : ScriptableObject
{
    public event Action OnChanged;

    public enum PigmentMixingModel
    {
        LinearRGB,
        Mixbox
    }

    [Header("Mixing Model")] [Tooltip("How particle pigment colors combine when they diffuse into each other.")]
    public PigmentMixingModel mixingModel = PigmentMixingModel.Mixbox;

    [Header("Diffusion")]
    [Tooltip("How fast pigment diffuses between neighbouring particles. " +
             "0 = no diffusion, higher = faster mixing.")]
    [Range(0f, 2f)]
    public float diffusionCoeff = 0.4f;

    [Header("Spawn Colors")]
    [Tooltip("One color per particle group. The SpawnSystem will assign each " +
             "particle its group color as the initial pigment value.")]
    public Color[] spawnColors =
    {
        new Color(1.00f, 0.00f, 0.00f),
        new Color(1.00f, 0.50f, 0.00f),
        new Color(1.00f, 1.00f, 0.00f),
        new Color(0.00f, 1.00f, 0.00f),
        new Color(0.00f, 0.00f, 1.00f),
        new Color(0.29f, 0.00f, 0.51f),
        new Color(0.56f, 0.00f, 1.00f),
    };

    void OnValidate() => OnChanged?.Invoke();
}