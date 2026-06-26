using Rendering;
using UnityEngine;

public sealed class SpawnSystem2D
{
    readonly ParticlesSpawner2D _particlesSpawner;

    public SpawnSystem2D(ParticleSettings settings)
    {
        _particlesSpawner = new ParticlesSpawner2D(settings);
    }

    public ParticleData2D[] SpawnParticles(Vector2 boundsMin, Vector2 boundsMax)
    {
        Vector2[] positions = _particlesSpawner.SpawnGridParticles();
        ParticleData2D[] particles = new ParticleData2D[positions.Length];

        for (int i = 0; i < positions.Length; i++)
        {
            particles[i] = new ParticleData2D(positions[i]);
        }

        return particles;
    }
}