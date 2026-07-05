using Rendering;
using Unity.Mathematics;
using UnityEngine;

public sealed class SpawnSystem3D
{
    readonly ParticleSettings _settings;
    readonly ParticlesSpawner3D _particlesSpawner;

    public SpawnSystem3D(ParticleSettings settings)
    {
        _settings = settings;
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

    public SpawnData3D SpawnParticlesInBucket(BucketGenerator bucket)
    {
        int n = _settings.particleCount;
        SpawnData3D spawnData = new SpawnData3D
        {
            positions = new float3[n],
            velocities = new float3[n]
        };

        float innerRadius = bucket.bottomRadius - bucket.thickness - _settings.radius;
        float minHeight = bucket.thickness + _settings.radius;
        float maxHeight = bucket.height + 0.05f;

        Matrix4x4 localToWorld = bucket.transform.localToWorldMatrix;

        for (int i = 0; i < n; i++)
        {
            float r = innerRadius * Mathf.Sqrt(UnityEngine.Random.value);
            float theta = UnityEngine.Random.value * 2f * Mathf.PI;
            float y = Mathf.Lerp(minHeight, maxHeight, UnityEngine.Random.value);

            float x = r * Mathf.Cos(theta);
            float z = r * Mathf.Sin(theta);

            Vector3 localPos = new Vector3(x, y, z);
            Vector3 worldPos = localToWorld.MultiplyPoint3x4(localPos);

            spawnData.positions[i] = (float3)worldPos;
            spawnData.velocities[i] = float3.zero;
        }

        return spawnData;
    }
}