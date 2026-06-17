using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralParticleRenderer : MonoBehaviour
{
    // ── public API ──────────────────────────────────────────────────────────
    public ParticleSettings settings;

    // ── private state ───────────────────────────────────────────────────────
    Mesh         _mesh;
    Vector3[]    _vertices;
    int[]        _triangles;
    Color[]      _colors;

    int          _particleCount;
    int          _segments;
    float        _radius;

    // vertexCount = segments + 1 (center) per particle
    int VertsPerParticle  => _segments + 1;
    int TrisPerParticle   => _segments;     // fan from center

    // ── lifecycle ───────────────────────────────────────────────────────────
    // void Awake()
    // {
    //     _mesh = new Mesh { name = "FluidParticles" };
    //     _mesh.MarkDynamic();  // hint Unity we update every frame
    //     GetComponent<MeshFilter>().mesh = _mesh;
    // }

    // Call once (or when settings change) to allocate arrays and bake topology
    public void Initialize(ParticleSettings cfg)
    {
        // Create mesh if it doesn't exist yet (Edit Mode safety)
        if (_mesh == null)
        {
            _mesh = new Mesh { name = "FluidParticles" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().mesh = _mesh;
        }
        
        settings       = cfg;
        _particleCount = cfg.particleCount;
        _segments      = cfg.segments;
        _radius        = cfg.radius;

        AllocateArrays();
        // Push empty vertices first so Unity knows the vertex count
        _mesh.vertices = _vertices;
        BakeTriangles();
        ApplyColor(cfg.color);
    }

    // Call every frame (or whenever positions change)
    public void UpdatePositions(Vector2[] positions)
    {
        int count = Mathf.Min(positions.Length, _particleCount);
        for (int i = 0; i < count; i++)
            WriteCircle(i, positions[i]);

        _mesh.vertices  = _vertices;
        _mesh.colors    = _colors;
        _mesh.RecalculateBounds();
    }

    // Hot-swap color at runtime without rebuilding topology
    public void SetColor(Color c)
    {
        for (int i = 0; i < _colors.Length; i++) _colors[i] = c;
        _mesh.colors = _colors;
    }

    // Hot-swap radius — must rebuild vertex positions
    public void SetRadius(float r)
    {
        _radius = r;
        // positions will be refreshed next UpdatePositions call
    }

    // ── private helpers ─────────────────────────────────────────────────────
    void AllocateArrays()
    {
        int totalVerts = _particleCount * VertsPerParticle;
        int totalTris  = _particleCount * TrisPerParticle * 3;

        _vertices  = new Vector3[totalVerts];
        _triangles = new int[totalTris];
        _colors    = new Color[totalVerts];

        // Unity limit: 65535 verts for 16-bit index buffer
        _mesh.indexFormat = totalVerts > 65535
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
    }

    // Triangle topology never changes — write it once
    void BakeTriangles()
    {
        int t = 0;
        for (int p = 0; p < _particleCount; p++)
        {
            int baseV = p * VertsPerParticle;   // center vertex index
            for (int s = 0; s < _segments; s++)
            {
                _triangles[t++] = baseV;                               // center
                _triangles[t++] = baseV + 1 + s;                      // current rim
                _triangles[t++] = baseV + 1 + (s + 1) % _segments;    // next rim
            }
        }
        _mesh.triangles = _triangles;
    }

    // Write one circle's vertices into the shared array
    void WriteCircle(int index, Vector2 center)
    {
        int baseV = index * VertsPerParticle;
        _vertices[baseV] = new Vector3(center.x, center.y, 0f);   // center

        float step = 2f * Mathf.PI / _segments;
        for (int s = 0; s < _segments; s++)
        {
            float angle = s * step;
            _vertices[baseV + 1 + s] = new Vector3(
                center.x + Mathf.Cos(angle) * _radius,
                center.y + Mathf.Sin(angle) * _radius,
                0f);
        }
    }

    void ApplyColor(Color c)
    {
        for (int i = 0; i < _colors.Length; i++) _colors[i] = c;
    }
}