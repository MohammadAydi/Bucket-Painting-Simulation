using Rendering;
using UnityEngine;

public sealed class SpawnSystem3D
{
    readonly ParticlesSpawner3D _particlesSpawner;

    public SpawnSystem3D(ParticleSettings settings)
    {
        _particlesSpawner = new ParticlesSpawner3D(settings);
    }

    public ParticleData3D[] SpawnParticles(Vector2 boundsMin, Vector2 boundsMax)
    {
        Vector2[] positions = _particlesSpawner.RandomSpawnParticles(boundsMin, boundsMax).Item1;
        ParticleData3D[] particles = new ParticleData3D[positions.Length];

        for (int i = 0; i < positions.Length; i++)
        {
            particles[i] = new ParticleData3D(positions[i]);
        }

        return particles;
    }
}