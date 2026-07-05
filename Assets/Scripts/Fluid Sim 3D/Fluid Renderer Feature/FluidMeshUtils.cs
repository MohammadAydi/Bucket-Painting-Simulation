// FluidMeshUtils.cs
// ──────────────────────────────────────────────────────────────────────────────
// Shared mesh utilities for the fluid renderer.
// ──────────────────────────────────────────────────────────────────────────────

using UnityEngine;

public static class FluidMeshUtils
{
    /// Creates a unit quad mesh (same layout as Sebastian's QuadGenerator).
    /// Vertices go -0.5..+0.5 in XY, UV 0..1.
    public static Mesh CreateQuad()
    {
        var m = new Mesh { name = "FluidQuad" };
        m.vertices  = new[]
        {
            new Vector3(-0.5f,  0.5f, 0),
            new Vector3( 0.5f,  0.5f, 0),
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3( 0.5f, -0.5f, 0)
        };
        m.uv = new[]
        {
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(0, 0), new Vector2(1, 0)
        };
        m.triangles = new[] { 0, 1, 2,  2, 1, 3 };
        m.RecalculateBounds();
        return m;
    }
}
