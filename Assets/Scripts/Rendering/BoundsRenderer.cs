using UnityEngine;
using static UnityEngine.Mathf;

[RequireComponent(typeof(LineRenderer))]
public class BoundsRenderer : MonoBehaviour
{
    public Vector2 boundsSize = new Vector2(15, 8);

    private LineRenderer _lr;
    
    public Vector2 BoundsMin => new Vector2(-boundsSize.x / 2, -boundsSize.y / 2);
    public Vector2 BoundsMax => new Vector2( boundsSize.x / 2,  boundsSize.y / 2);
    

    private void Awake()
    {
        Initialize();
        UpdateBounds();
    }

    private void OnValidate()
    {
        _lr = GetComponent<LineRenderer>();

        if (_lr == null)
            return;

        Initialize();
        UpdateBounds();
    }

    private void Initialize()
    {
        if (_lr == null)
            _lr = GetComponent<LineRenderer>();

        _lr.loop = true;
        _lr.positionCount = 4;
        _lr.startWidth = 0.01f;
        _lr.endWidth = 0.01f;
        
        _lr.startColor = Color.darkGreen;
        _lr.endColor = Color.darkGreen;
    }

    private void UpdateBounds()
    {
        float halfWidth = boundsSize.x / 2;
        float halfHeight = boundsSize.y / 2;

        _lr.SetPosition(0, new Vector3(-halfWidth, -halfHeight, 0));
        _lr.SetPosition(1, new Vector3(-halfWidth,  halfHeight, 0));
        _lr.SetPosition(2, new Vector3( halfWidth,  halfHeight, 0));
        _lr.SetPosition(3, new Vector3( halfWidth, -halfHeight, 0));
    }
    
    // float SmoothingKernel(float radius, float dst) {
    //     float volume = PI * Pow(radius, 8) / 4;
    //     float value = Max(0, radius * radius - dst * dst);
    //     return value * value * value / volume;
    // }
    //
    // float CalculateDensity(Vector2 samplePoint) {
    //     float density = 0;
    //     const float mass = 1;
    //     foreach (Vector2 position in positions) {
    //         float dst = (position - samplePoint).magnitude;
    //         float influence = SmoothingKernel(smoothingRadius, dst);
    //         density += mass * influence;
    //     }
    //
    //     return density;
    // }
}