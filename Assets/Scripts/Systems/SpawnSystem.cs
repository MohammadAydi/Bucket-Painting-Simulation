using Rendering;
using UnityEngine;

public sealed class SpawnSystem
{
    readonly ParticlesSpawner _particlesSpawner;

    public SpawnSystem(ParticleSettings settings)
    {
        _particlesSpawner = new ParticlesSpawner(settings);
    }

    public ParticleData[] SpawnParticles(Vector2 boundsMin, Vector2 boundsMax)
    {
        Vector2[] positions = _particlesSpawner.RandomSpawnParticles(boundsMin, boundsMax).Item1;
        ParticleData[] particles = new ParticleData[positions.Length];

        for (int i = 0; i < positions.Length; i++)
        {
            particles[i] = new ParticleData(positions[i]);
        }

        return particles;
    }
}