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

    // Encodes an author-facing sRGB spawn color into whatever representation
    // the active PigmentMixingModel diffuses in:
    static Vector4 EncodePigment(Color spawnColor, PigmentSettings pigmentSettings)
    {
        Color linear = spawnColor.linear;
        return new Vector4(linear.r, linear.g, linear.b, linear.a);
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

        if (groups == 2)
        {
            float fullWidth = xMax - xMin;
            float cubeWidth = fullWidth * 0.25f;
            float leftXMax  = xMin + cubeWidth;
            float rightXMin = xMax - cubeWidth;

            int half = n / 2;

            for (int i = 0; i < n; i++)
            {
                bool isRight = i >= half;
                float localXMin = isRight ? rightXMin : xMin;
                float localXMax = isRight ? xMax      : leftXMax;

                float x = Mathf.Lerp(localXMin, localXMax, UnityEngine.Random.value);
                float y = Mathf.Lerp(yMin, yMax, UnityEngine.Random.value);
                float z = Mathf.Lerp(zMin, zMax, UnityEngine.Random.value);

                Vector3 localPos = new Vector3(x, y, z);
                spawnData.positions[i]  = (float3)l2w.MultiplyPoint3x4(localPos);
                spawnData.velocities[i] = float3.zero;

                float midLocal = (leftXMax + rightXMin) * 0.5f;
                int   g        = x < midLocal ? 0 : 1;

                spawnData.pigmentColors[i] = EncodePigment(pigmentSettings.spawnColors[g], pigmentSettings);
            }

            return spawnData;
        }

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
                    spawnData.pigmentColors[i] = EncodePigment(pigmentSettings.spawnColors[0], pigmentSettings);
                }
            }

            return spawnData;
        }

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

                spawnData.pigmentColors[i] = EncodePigment(pigmentSettings.spawnColors[g], pigmentSettings);
            }

            return spawnData;
        }
    }

    public SpawnData3D SpawnParticlesInBucket(BucketGenerator bucket, PigmentSettings pigmentSettings = null)
    {
        int n = _settings.particleCount;
        int groups = (pigmentSettings?.spawnColors != null) ? pigmentSettings.spawnColors.Length : 0;

        SpawnData3D spawnData = new SpawnData3D
        {
            positions  = new float3[n],
            velocities = new float3[n],
            pigmentColors = groups > 0 ? new Vector4[n] : null
        };

        float particleRadius = _settings.radius;
        // تقييد التوليد ليكون فوق أرضية الدلو وتحت الحافة العلوية بقليل
        float minHeight = bucket.thickness + particleRadius;
        float maxHeight = bucket.height - particleRadius; 
        Matrix4x4 l2w = bucket.transform.localToWorldMatrix;

        for (int i = 0; i < n; i++)
        {
            Vector3 localPos = Vector3.zero;
            bool validPoint = false;
            int maxAttempts = 100; // منع الحلقات اللانهائية

            // حلقة للبحث عن نقطة توليد صحيحة (Rejection Sampling)
            while (!validPoint && maxAttempts > 0)
            {
                maxAttempts--;
                
                float y = Mathf.Lerp(minHeight, maxHeight, UnityEngine.Random.value);
                
                // 1. حساب نصف القطر الداخلي عند هذا الارتفاع (لمراعاة الدلو المخروطي)
                float currentOuterRadius = Mathf.Lerp(bucket.bottomRadius, bucket.topRadius, y / bucket.height);
                float innerRadius = currentOuterRadius - bucket.thickness - particleRadius;
                
                if (innerRadius <= 0) break; // أمان في حال كانت السماكة أكبر من نصف القطر
                
                float r = innerRadius * Mathf.Sqrt(UnityEngine.Random.value);
                float theta = UnityEngine.Random.value * 2f * Mathf.PI;
                
                float x = r * Mathf.Cos(theta);
                float z = r * Mathf.Sin(theta);
                
                validPoint = true;

                // 2. التحقق من الحواجز (Dividers) وتفادي التوليد بداخلها
                if (bucket.compartmentRatios != null && bucket.compartmentRatios.Count > 1)
                {
                    float totalRatioSum = 0;
                    foreach (float ratio in bucket.compartmentRatios) totalRatioSum += ratio;
                    if (totalRatioSum <= 0) totalRatioSum = 1f;

                    float currentAngle = 0f;
                    for (int j = 0; j < bucket.compartmentRatios.Count; j++)
                    {
                        // حساب المتجه العمودي للحاجز
                        Vector3 wallNormal = new Vector3(-Mathf.Sin(currentAngle), 0, Mathf.Cos(currentAngle));
                        
                        // المسافة العمودية بين النقطة والحاجز
                        float distToDivider = Mathf.Abs(Vector3.Dot(new Vector3(x, 0, z), wallNormal));

                        // إذا كانت النقطة داخل نطاق الحاجز يتم رفضها
                        if (distToDivider < (bucket.dividerThickness / 2f) + particleRadius)
                        {
                            validPoint = false;
                            break;
                        }
                        
                        currentAngle += (bucket.compartmentRatios[j] / totalRatioSum) * 2f * Mathf.PI;
                    }
                }

                if (validPoint)
                {
                    localPos = new Vector3(x, y, z);
                }
            }

            spawnData.positions[i]  = (float3)l2w.MultiplyPoint3x4(localPos);
            spawnData.velocities[i] = float3.zero;

            if (groups > 0)
            {
                // إبقاء توزيع الألوان بناءً على مؤشر التوليد ليتوافق مع الإعدادات السابقة
                int g = Mathf.Clamp((int)((float)i / n * groups), 0, groups - 1);
                spawnData.pigmentColors[i] = EncodePigment(pigmentSettings.spawnColors[g], pigmentSettings);
            }
        }

        return spawnData;
    }
}