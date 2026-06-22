using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralParticleRenderer : MonoBehaviour
{
    public ParticleSettings settings;

    [Header("Mode")]
    public bool isDensityRenderer = false;

    Mesh      _mesh;
    Vector3[] _vertices;
    int[]     _triangles;
    Color[]   _colors;

    float _radius;

    int VertsPerParticle => settings.segments + 1;
    int TrisPerParticle  => settings.segments;

    public void Initialize(ParticleSettings cfg)
    {
        if (_mesh == null)
        {
            _mesh = new Mesh { name = isDensityRenderer ? "DensityParticles" : "FluidParticles" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().mesh = _mesh;
        }

        settings = cfg;
        _radius  = isDensityRenderer ? cfg.smoothingRadius : cfg.radius;

        AllocateArrays();
        _mesh.vertices = _vertices;
        BakeTriangles();

        Color c = isDensityRenderer ? cfg.densityColor : cfg.color;
        ApplyColor(c);
    }

    public void UpdatePositions(Vector2[] positions)
    {
        int count = Mathf.Min(positions.Length, settings.particleCount);
        for (int i = 0; i < count; i++)
            WriteCircle(i, positions[i]);

        _mesh.vertices = _vertices;
        _mesh.colors   = _colors;
        _mesh.RecalculateBounds();
    }

    public void SetColor(Color c)
    {
        for (int i = 0; i < _colors.Length; i++)
            _colors[i] = new Color(c.r, c.g, c.b, _colors[i].a);
        _mesh.colors = _colors;
    }

    public void SetRadius(float r)
    {
        _radius = r;
    }

    void AllocateArrays()
    {
        int totalVerts = settings.particleCount * VertsPerParticle;
        int totalTris  = settings.particleCount * TrisPerParticle * 3;

        _vertices  = new Vector3[totalVerts];
        _triangles = new int[totalTris];
        _colors    = new Color[totalVerts];

        _mesh.indexFormat = totalVerts > 65535
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
    }

    void BakeTriangles()
    {
        int t = 0;
        for (int p = 0; p < settings.particleCount; p++)
        {
            int baseV = p * VertsPerParticle;
            for (int s = 0; s < settings.segments; s++)
            {
                _triangles[t++] = baseV;
                _triangles[t++] = baseV + 1 + s;
                _triangles[t++] = baseV + 1 + (s + 1) % settings.segments;
            }
        }
        _mesh.triangles = _triangles;
    }

    void WriteCircle(int index, Vector2 center)
    {
        int   baseV = index * VertsPerParticle;
        float step  = 2f * Mathf.PI / settings.segments;

        // Center — distance 0
        _vertices[baseV] = new Vector3(center.x, center.y, 0f);
        _colors[baseV]   = new Color(_colors[baseV].r, _colors[baseV].g, _colors[baseV].b, 0f);

        for (int s = 0; s < settings.segments; s++)
        {
            float angle = s * step;
            _vertices[baseV + 1 + s] = new Vector3(
                center.x + Mathf.Cos(angle) * _radius,
                center.y + Mathf.Sin(angle) * _radius,
                0f);

            // Rim — distance 1
            _colors[baseV + 1 + s] = new Color(
                _colors[baseV + 1].r,
                _colors[baseV + 1].g,
                _colors[baseV + 1].b,
                1f);
        }
    }

    void ApplyColor(Color c)
    {
        for (int i = 0; i < _colors.Length; i++)
            _colors[i] = new Color(c.r, c.g, c.b, _colors[i].a);
    }
}