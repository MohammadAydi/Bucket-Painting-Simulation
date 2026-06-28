using Rendering;
using Unity.Mathematics;
using UnityEngine;

public sealed class SpawnSystem3D
{
    readonly ParticlesSpawner3D _particlesSpawner;

    public SpawnSystem3D(ParticleSettings settings)
    {
        _particlesSpawner = new ParticlesSpawner3D(settings);
    }

    public SpawnData3D SpawnParticles(FluidBoundary3D boundary)
    {
        Vector3[] localPositions = _particlesSpawner.RandomSpawnParticles(
            boundary.LocalMin,
            boundary.LocalMax);

        SpawnData3D spawnData = new SpawnData3D
        {
            positions = new float3[localPositions.Length],
            velocities = new float3[localPositions.Length]
        };

        Matrix4x4 localToWorld = boundary.ColliderLocalToWorldMatrix;

        for (int i = 0; i < localPositions.Length; i++)
        {
            Vector3 worldPos = localToWorld.MultiplyPoint3x4(localPositions[i]);

            spawnData.positions[i] = (float3)worldPos;
            spawnData.velocities[i] = float3.zero;
        }

        return spawnData;
    }
}