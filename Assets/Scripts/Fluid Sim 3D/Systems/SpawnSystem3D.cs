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

    public SpawnData3D SpawnParticles(FluidBoundary3D boundary, PigmentSettings pigmentSettings = null)
    {
        int n      = _settings.particleCount;
        int groups = (pigmentSettings?.spawnColors != null) ? pigmentSettings.spawnColors.Length : 0;
        Matrix4x4 l2w = boundary.ColliderLocalToWorldMatrix;

        float pad  = _settings.radius;
        float xMin = boundary.LocalMin.x + pad;
        float xMax = boundary.LocalMax.x - pad;
        float yMin = boundary.LocalMin.y + pad;
        float yMax = boundary.LocalMax.y - pad;
        float zMin = boundary.LocalMin.z + pad;
        float zMax = boundary.LocalMax.z - pad;

        SpawnData3D spawnData = new SpawnData3D
        {
            positions     = new float3[n],
            velocities    = new float3[n],
            pigmentColors = groups > 0 ? new Vector4[n] : null
        };

        // When exactly 2 color groups: spawn two separate edge-hugging cubes.
        // Each cube occupies the inner 25 % of the X range on its respective side,
        // leaving a clear gap in the middle so they start visually separated.
        if (groups == 2)
        {
            float fullWidth = xMax - xMin;
            float cubeWidth = fullWidth * 0.25f;
            // Left cube  : [xMin,              xMin + cubeWidth]
            // Right cube : [xMax - cubeWidth,  xMax            ]
            float leftXMax  = xMin + cubeWidth;
            float rightXMin = xMax - cubeWidth;

            int half = n / 2;

            for (int i = 0; i < n; i++)
            {
                // First half → left cube, second half → right cube
                bool isRight = i >= half;

                float localXMin = isRight ? rightXMin : xMin;
                float localXMax = isRight ? xMax      : leftXMax;

                float x = Mathf.Lerp(localXMin, localXMax, UnityEngine.Random.value);
                float y = Mathf.Lerp(yMin, yMax, UnityEngine.Random.value);
                float z = Mathf.Lerp(zMin, zMax, UnityEngine.Random.value);

                Vector3 localPos = new Vector3(x, y, z);
                spawnData.positions[i]  = (float3)l2w.MultiplyPoint3x4(localPos);
                spawnData.velocities[i] = float3.zero;

                // Color is tied to the physical X position, not the loop index,
                // so it survives any buffer reordering FluidModel may do.
                // midLocal is the gap centre in local space.
                float midLocal = (leftXMax + rightXMin) * 0.5f;
                int   g        = x < midLocal ? 0 : 1;

                Color linear = pigmentSettings.spawnColors[g].linear;
                spawnData.pigmentColors[i] = new Vector4(linear.r, linear.g, linear.b, linear.a);
            }

            return spawnData;
        }

        // Fallback for 0 or 1 groups: fully random spawn (original behavior)
        if (groups <= 1)
        {
            Vector3[] localPositions = _particlesSpawner.RandomSpawnParticles(
                boundary.LocalMin, boundary.LocalMax);

            for (int i = 0; i < localPositions.Length; i++)
            {
                Vector3 pos = localPositions[i];
                spawnData.positions[i]  = (float3)l2w.MultiplyPoint3x4(pos);
                spawnData.velocities[i] = float3.zero;

                if (groups == 1)
                {
                    Color linear = pigmentSettings.spawnColors[0].linear;
                    spawnData.pigmentColors[i] = new Vector4(linear.r, linear.g, linear.b, linear.a);
                }
            }

            return spawnData;
        }

        // General case: N groups → N equal vertical slabs across X
        {
            Vector3[] localPositions = _particlesSpawner.RandomSpawnParticles(
                boundary.LocalMin, boundary.LocalMax);

            float slabW = (xMax - xMin) / groups;

            for (int i = 0; i < n; i++)
            {
                Vector3 pos     = localPositions[i];
                int     g       = Mathf.Clamp((int)((float)i / n * groups), 0, groups - 1);
                float slabXMin  = xMin + g       * slabW;
                float slabXMax  = xMin + (g + 1) * slabW;

                pos.x = Mathf.Lerp(slabXMin, slabXMax, UnityEngine.Random.value);
                pos.y = Mathf.Clamp(pos.y, yMin, yMax);
                pos.z = Mathf.Clamp(pos.z, zMin, zMax);

                spawnData.positions[i]  = (float3)l2w.MultiplyPoint3x4(pos);
                spawnData.velocities[i] = float3.zero;

                Color linear = pigmentSettings.spawnColors[g].linear;
                spawnData.pigmentColors[i] = new Vector4(linear.r, linear.g, linear.b, linear.a);
            }

            return spawnData;
        }
    }

    public SpawnData3D SpawnParticlesInBucket(BucketGenerator bucket, PigmentSettings pigmentSettings = null)
    {
        int n = _settings.particleCount;
        SpawnData3D spawnData = new SpawnData3D
        {
            positions  = new float3[n],
            velocities = new float3[n]
        };

        float innerRadius = bucket.bottomRadius - bucket.thickness - _settings.radius;
        float minHeight   = bucket.thickness + _settings.radius;
        float maxHeight   = bucket.height + 0.05f;
        Matrix4x4 l2w     = bucket.transform.localToWorldMatrix;

        for (int i = 0; i < n; i++)
        {
            float r     = innerRadius * Mathf.Sqrt(UnityEngine.Random.value);
            float theta = UnityEngine.Random.value * 2f * Mathf.PI;
            float y     = Mathf.Lerp(minHeight, maxHeight, UnityEngine.Random.value);
            float x     = r * Mathf.Cos(theta);
            float z     = r * Mathf.Sin(theta);

            spawnData.positions[i]  = (float3)l2w.MultiplyPoint3x4(new Vector3(x, y, z));
            spawnData.velocities[i] = float3.zero;
        }

        int groups = (pigmentSettings?.spawnColors != null) ? pigmentSettings.spawnColors.Length : 0;
        if (groups > 0)
        {
            spawnData.pigmentColors = new Vector4[n];
            for (int i = 0; i < n; i++)
            {
                int   g      = Mathf.Clamp((int)((float)i / n * groups), 0, groups - 1);
                Color linear = pigmentSettings.spawnColors[g].linear;
                spawnData.pigmentColors[i] = new Vector4(linear.r, linear.g, linear.b, linear.a);
            }
        }

        return spawnData;
    }
}