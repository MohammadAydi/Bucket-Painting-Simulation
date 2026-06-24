using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class FluidBoundary3D : MonoBehaviour
{
    [SerializeField] BoxCollider boundaryCollider;
    [SerializeField] Material boundaryVisualizerMaterial;
    Mesh _visualizationMesh;
    MaterialPropertyBlock _propertyBlock;

    public BoxCollider BoundaryCollider
    {
        get
        {
            if (boundaryCollider == null)
            {
                boundaryCollider = GetComponent<BoxCollider>();
            }

            return boundaryCollider;
        }
    }

    public Matrix4x4 ColliderLocalToWorldMatrix => transform.localToWorldMatrix;

    public Matrix4x4 WorldToColliderLocalMatrix => transform.worldToLocalMatrix;

    public Vector3 LocalMin => BoundaryCollider.center - BoundaryCollider.size * 0.5f;

    public Vector3 LocalMax => BoundaryCollider.center + BoundaryCollider.size * 0.5f;

    public Bounds WorldBounds => CalculateWorldBounds();

    public Vector3 GetLocalParticlePadding(float worldRadius)
    {
        Vector3 scale = transform.lossyScale;
        return new Vector3(
            worldRadius / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
            worldRadius / Mathf.Max(Mathf.Abs(scale.y), 0.0001f),
            worldRadius / Mathf.Max(Mathf.Abs(scale.z), 0.0001f));
    }

    public Matrix4x4 GetRenderMatrix()
    {
        return transform.localToWorldMatrix * Matrix4x4.TRS(BoundaryCollider.center, Quaternion.identity, BoundaryCollider.size);
    }

    void Reset()
    {
        boundaryCollider = GetComponent<BoxCollider>();
        if (boundaryCollider != null)
        {
            boundaryCollider.isTrigger = true;
        }
    }

    void OnValidate()
    {
        if (boundaryCollider == null)
        {
            boundaryCollider = GetComponent<BoxCollider>();
        }

        if (boundaryCollider != null)
        {
            boundaryCollider.isTrigger = true;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (BoundaryCollider == null)
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.8f);
        Gizmos.matrix = GetRenderMatrix();
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }

    Bounds CalculateWorldBounds()
    {
        Vector3 localMin = LocalMin;
        Vector3 localMax = LocalMax;

        Vector3[] corners =
        {
            new Vector3(localMin.x, localMin.y, localMin.z),
            new Vector3(localMin.x, localMin.y, localMax.z),
            new Vector3(localMin.x, localMax.y, localMin.z),
            new Vector3(localMin.x, localMax.y, localMax.z),
            new Vector3(localMax.x, localMin.y, localMin.z),
            new Vector3(localMax.x, localMin.y, localMax.z),
            new Vector3(localMax.x, localMax.y, localMin.z),
            new Vector3(localMax.x, localMax.y, localMax.z)
        };

        Vector3 first = transform.TransformPoint(corners[0]);
        Bounds bounds = new Bounds(first, Vector3.zero);

        for (int i = 1; i < corners.Length; i++)
        {
            bounds.Encapsulate(transform.TransformPoint(corners[i]));
        }

        return bounds;
    }
}