using UnityEngine;
using static UnityEngine.Mathf;

namespace Rendering {
    public class ParticlesSpawner3D {
        readonly ParticleSettings _settings;

        public ParticlesSpawner3D(ParticleSettings settings) {
            this._settings = settings;
        }


        public Vector2[] SpawnGridParticles() {
            int n = _settings.particleCount;
            Vector2[] positions = new Vector2[n];

            int particlesPerRow = (int)Sqrt(n);
            int particlesPerCol = (n - 1) / particlesPerRow + 1;
            float spacing = _settings.smoothingRadius * 2 + _settings.particleSpacing; // <-- fix

            for (int i = 0; i < n; i++) {
                float x = (i % particlesPerRow - particlesPerRow / 2f + 0.5f) * spacing;
                float y = (i / particlesPerRow - particlesPerCol / 2f + 0.5f) * spacing;
                positions[i] = new Vector2(x, y);
            }

            return positions;
        }

        public (Vector2[], float[]) RandomSpawnParticles(Vector2 boundsMin, Vector2 boundsMax) {
            Debug.Log("boundsMin: " + boundsMin + ", boundsMax: " + boundsMax);
            int n = _settings.particleCount;

            Vector2[] positions = new Vector2[n];
            float[] particleProperties = new float[n];

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

                positions[i] = new Vector2(x, y);
                particleProperties[i] = ExampleFunc(positions[i]);
            }

            return (positions, particleProperties);
        }

        float ExampleFunc(Vector2 samplePoint) {
            return Cos(samplePoint.y - 3 + Sin(samplePoint.x));
        }
    }
}