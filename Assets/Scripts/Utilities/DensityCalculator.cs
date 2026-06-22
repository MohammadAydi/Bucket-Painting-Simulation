using UnityEngine;

public static class DensityCalculator
{
    public static float SmoothingKernel(float radius, float dst)
    {
        if (dst >= radius) return 0f;
        float volume = Mathf.PI * Mathf.Pow(radius, 8) / 4f;
        float value  = radius * radius - dst * dst;
        return value * value * value / volume;
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