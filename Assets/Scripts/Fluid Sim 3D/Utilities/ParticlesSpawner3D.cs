using UnityEngine;
using static UnityEngine.Mathf;

namespace Rendering {
    public class ParticlesSpawner3D {
        readonly ParticleSettings _settings;

        public ParticlesSpawner3D(ParticleSettings settings) {
            this._settings = settings;
        }
        
        public Vector3[] SpawnGridParticlesRandom() {
            int n = _settings.particleCount;
            Vector3[] positions = new Vector3[n];

            int particlesPerSide = Mathf.Max(1, Mathf.CeilToInt(Mathf.Pow(n, 1f / 3f)));
            float spacing = _settings.radius * 2 + _settings.particleSpacing;

            float maxJitter = _settings.particleSpacing * 0.5f; 

            for (int i = 0; i < n; i++) {
                int xIndex = i % particlesPerSide;
                int yIndex = (i / particlesPerSide) % particlesPerSide;
                int zIndex = i / (particlesPerSide * particlesPerSide);

                float x = (xIndex - particlesPerSide / 2f + 0.5f) * spacing;
                float y = (yIndex - particlesPerSide / 2f + 0.5f) * spacing;
                float z = (zIndex - particlesPerSide / 2f + 0.5f) * spacing;

                float jitterX = UnityEngine.Random.Range(-maxJitter, maxJitter);
                float jitterY = UnityEngine.Random.Range(-maxJitter, maxJitter);
                float jitterZ = UnityEngine.Random.Range(-maxJitter, maxJitter);

                positions[i] = new Vector3(x + jitterX, y + jitterY, z + jitterZ);
            }

            return positions;
        }


        public Vector3[] SpawnGridParticles() {
            int n = _settings.particleCount;
            Vector3[] positions = new Vector3[n];

            int particlesPerSide = Mathf.Max(1, Mathf.CeilToInt(Mathf.Pow(n, 1f / 3f)));
            float spacing = _settings.radius * 2 + _settings.particleSpacing; // <-- fix

            for (int i = 0; i < n; i++) {
                int xIndex = i % particlesPerSide;
                int yIndex = (i / particlesPerSide) % particlesPerSide;
                int zIndex = i / (particlesPerSide * particlesPerSide);

                float x = (xIndex - particlesPerSide / 2f + 0.5f) * spacing;
                float y = (yIndex - particlesPerSide / 2f + 0.5f) * spacing;
                float z = (zIndex - particlesPerSide / 2f + 0.5f) * spacing;
                positions[i] = new Vector3(x, y, z);
            }

            return positions;
        }

        public Vector3[] RandomSpawnParticles(Vector3 boundsMin, Vector3 boundsMax) {
            int n = _settings.particleCount;

            Vector3[] positions = new Vector3[n];

            float padding = _settings.radius;

            for (int i = 0; i < n; i++) {
                float x = Random.Range(
                    boundsMin.x + padding,
                    boundsMax.x - padding
                );

                float y = Random.Range(
                    boundsMin.y + padding,
                    boundsMax.y - padding
                );

                float z = Random.Range(
                    boundsMin.z + padding,
                    boundsMax.z - padding
                );

                positions[i] = new Vector3(x, y, z);
            }

            return positions;
        }
    }
}