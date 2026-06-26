using Rendering;
using UnityEngine;

public sealed class SpawnSystem3D
{
    readonly ParticlesSpawner3D _particlesSpawner;

    public SpawnSystem3D(ParticleSettings settings)
    {
        _particlesSpawner = new ParticlesSpawner3D(settings);
    }

    public ParticleData3D[] SpawnParticles(FluidBoundary3D boundary)
    {
        Vector3[] localPositions = _particlesSpawner.RandomSpawnParticles(boundary.LocalMin, boundary.LocalMax);
        ParticleData3D[] particles = new ParticleData3D[localPositions.Length];
        Matrix4x4 localToWorld = boundary.ColliderLocalToWorldMatrix;

        for (int i = 0; i < localPositions.Length; i++)
        {
            particles[i] = new ParticleData3D(localToWorld.MultiplyPoint3x4(localPositions[i]));
        }

        return particles;
    }
}