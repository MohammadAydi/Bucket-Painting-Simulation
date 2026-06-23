using static UnityEngine.Mathf;
using UnityEngine;

public static class DensityCalculator3D
{
    static float SmoothingKernel(float radius, float dst)
    {
        if (dst >= radius) return 0.0f;
        float volume = PI * Pow(radius, 4) / 6.0f;
        return (radius - dst) * (radius - dst) / volume;
    }

    public static float CalculateDensity(Vector2 samplePoint, Vector2[] positions, float smoothingRadius)
    {
        float density = 0f;
        const float mass = 1f;

        foreach (Vector2 position in positions)
        {
            float dst      = (position - samplePoint).magnitude;
            float influence = SmoothingKernel(smoothingRadius, dst);
            density        += mass * influence;
        }

        return density;
    }
}