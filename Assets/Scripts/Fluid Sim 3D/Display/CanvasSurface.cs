using UnityEngine;

public enum CanvasSurfaceType
{
    Fabric,
    Metal
}

// Attach this to the flat quad/plane GameObject that acts as the paint
// canvas. Position, rotation, and localScale on the transform freely
// define where the canvas is and how big it is -- this script reads that
// transform each frame (cheap: it's a handful of dot products) and pushes
// the resulting plane description + material parameters to the compute
// shader so CanvasCollision and FrictionSolver can use it.
//
// The plane is assumed to be a standard Unity Quad (or any mesh whose
// local up axis is +Z... adjust `LocalPlaneNormalAxis` below if your mesh
// convention differs). Local X/Y map to the plane's tangent/bitangent,
// scaled by transform.localScale, giving the finite half-extents.
[DisallowMultipleComponent]
public class CanvasSurface : MonoBehaviour
{
    [Header("Canvas Type")]
    public CanvasSurfaceType surfaceType = CanvasSurfaceType.Fabric;

    [Header("Material Presets")]
    [Tooltip("Friction 'delta' from f = -max((1 - delta*d),0)^2 * v. Larger = shorter-range, sharper friction falloff.")]
    public float fabricFrictionDelta = 8f;
    public float metalFrictionDelta = 25f;

    [Tooltip("Extra velocity damping applied on collision bounce (0 = no bounce, just stop).")]
    public float fabricCollisionDamping = 0.0f;
    public float metalCollisionDamping = 0.3f;

    [Header("Renderer (optional, auto-assigns material by type)")]
    public Renderer canvasRenderer;
    public Material fabricMaterial;
    public Material metalMaterial;

    public Matrix4x4 ColliderLocalToWorldMatrix => transform.localToWorldMatrix;

    public Matrix4x4 WorldToColliderLocalMatrix => transform.worldToLocalMatrix;

    // A standard Unity Quad's face normal is -Z in local space (it faces
    // the camera looking down -Z by default). If you're using a Plane
    // primitive instead (+Y normal), flip this.
    public enum LocalNormalAxis { LocalPosZ, LocalNegZ, LocalPosY, LocalNegY }
    [Header("Mesh Convention")]
    public LocalNormalAxis localNormalAxis = LocalNormalAxis.LocalPosY;

    void Reset()
    {
        canvasRenderer = GetComponent<Renderer>();
    }

    void OnValidate()
    {
        ApplyMaterial();
    }

    void ApplyMaterial()
    {
        if (canvasRenderer == null) return;
        Material mat = surfaceType == CanvasSurfaceType.Fabric ? fabricMaterial : metalMaterial;
        if (mat != null) canvasRenderer.sharedMaterial = mat;
    }

    public float CurrentFrictionDelta =>
        surfaceType == CanvasSurfaceType.Fabric ? fabricFrictionDelta : metalFrictionDelta;

    public float CurrentCollisionDamping =>
        surfaceType == CanvasSurfaceType.Fabric ? fabricCollisionDamping : metalCollisionDamping;

    Vector3 LocalNormal()
    {
        switch (localNormalAxis)
        {
            case LocalNormalAxis.LocalPosZ: return Vector3.forward;
            case LocalNormalAxis.LocalNegZ: return Vector3.back;
            case LocalNormalAxis.LocalPosY: return Vector3.up;
            case LocalNormalAxis.LocalNegY: return Vector3.down;
        }
        return Vector3.back;
    }

    // Tangent/bitangent picked to always be perpendicular to whichever
    // axis is the normal, using the object's local X/Y (or X/Z) as the
    // in-plane basis, matching Unity's default Quad UV layout.
    void GetLocalTangentBasis(out Vector3 tangentLocal, out Vector3 bitangentLocal)
    {
        switch (localNormalAxis)
        {
            case LocalNormalAxis.LocalPosZ:
            case LocalNormalAxis.LocalNegZ:
                tangentLocal = Vector3.right;
                bitangentLocal = Vector3.up;
                break;
            case LocalNormalAxis.LocalPosY:
                tangentLocal = Vector3.right;
                bitangentLocal = Vector3.forward;
                break;
            case LocalNormalAxis.LocalNegY:
                tangentLocal = Vector3.right;
                bitangentLocal = Vector3.forward;
                break;
            default:
                tangentLocal = Vector3.right;
                bitangentLocal = Vector3.up;
                break;
        }
    }

    /// <summary>
    /// Computes the current world-space plane description. Half-extents
    /// are derived from transform.localScale, matching a stretched Quad
    /// (Quad's default size is 1x1, so localScale.x/y are the full width/
    /// height in world units for an axis-aligned unrotated quad -- with
    /// rotation/nonuniform parent scale this still holds because we
    /// transform the basis vectors themselves through the full local-to-
    /// world matrix).
    /// </summary>
    public void GetPlaneData(out Vector3 center, out Vector3 normal,
       out Vector3 tangent, out Vector3 bitangent, out Vector2 halfExtents)
    {
        GetLocalTangentBasis(out Vector3 tangentLocal, out Vector3 bitangentLocal);
        Vector3 normalLocal = LocalNormal();

        center = transform.position;
        normal = transform.TransformDirection(normalLocal).normalized;

        Vector3 tangentWorld = transform.TransformVector(tangentLocal);
        Vector3 bitangentWorld = transform.TransformVector(bitangentLocal);

        tangent = tangentWorld.normalized;
        bitangent = bitangentWorld.normalized;

        // Mesh bounds are in local, unscaled space -- same space the
        // shader's localPosition ends up in after _CanvasWorldToLocal.
        Vector3 meshLocalSize = MeshLocalSize();
        float baseHalfWidth = meshLocalSize.x * 0.5f;
        float baseHalfHeight = meshLocalSize.z * 0.5f;

        halfExtents = new Vector2(baseHalfWidth, baseHalfHeight);
    }

    Vector3 MeshLocalSize()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            return mf.sharedMesh.bounds.size;
        }

        // Fallback if no MeshFilter present
        return new Vector3(1f, 1f, 1f);
    }
}
