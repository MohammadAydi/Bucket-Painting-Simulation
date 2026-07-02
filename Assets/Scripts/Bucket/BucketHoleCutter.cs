using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class HoleData
{
    public string holeName = "Hole Instance";
    public BucketHoleCutter.HoleType type = BucketHoleCutter.HoleType.Circular;
    public BucketHoleCutter.HoleLocation location = BucketHoleCutter.HoleLocation.Side;

    public float radius = 0.35f;
    public float width = 0.5f;
    public float height = 0.5f;

    [Range(0f, 360f)]
    public float angleDegrees = 60f;
    public float heightPosition = 1.2f;
    public Vector2 bottomOffset = new Vector2(0.5f, 0.5f);
}

public class BucketHoleCutter : MonoBehaviour
{
    public enum HoleType { Circular, Rectangular }
    public enum HoleLocation { Side, Bottom }

    [Header("Master Toggle")]
    public bool enableHoles = true;

    [Header("List of Holes")]
    public List<HoleData> holes = new List<HoleData>();

    private void OnValidate()
    {
        BucketGenerator generator = GetComponent<BucketGenerator>();
        if (generator != null)
        {
            generator.OnValidate();
        }
    }

    public bool ShouldCutFace(Vector3 faceCenter, bool isFloor, float thickness, float bucketHeight, float bottomRadius, float topRadius)
    {
        if (!enableHoles || holes == null || holes.Count == 0) return false;

        foreach (var hole in holes)
        {
            if (hole.location == HoleLocation.Side && !isFloor)
            {
                float faceAngleRad = Mathf.Atan2(faceCenter.z, faceCenter.x);
                float faceAngleDeg = faceAngleRad * Mathf.Rad2Deg;
                if (faceAngleDeg < 0) faceAngleDeg += 360f;

                float angleDelta = Mathf.Abs(faceAngleDeg - hole.angleDegrees);
                if (angleDelta > 180f) angleDelta = 360f - angleDelta;

                // Unroll the curved wall into a flat plane: X axis = arc length, Y axis = height
                float faceRadius = Mathf.Lerp(bottomRadius, topRadius, Mathf.Clamp01(faceCenter.y / bucketHeight));
                float arcDistance = (angleDelta * Mathf.Deg2Rad) * faceRadius;
                float heightDistance = faceCenter.y - hole.heightPosition;

                if (hole.type == HoleType.Circular)
                {
                    float dist = Mathf.Sqrt(arcDistance * arcDistance + heightDistance * heightDistance);
                    if (dist < hole.radius) return true;
                }
                else if (hole.type == HoleType.Rectangular)
                {
                    if (arcDistance < (hole.width / 2f) && Mathf.Abs(heightDistance) < (hole.height / 2f)) return true;
                }
            }
            else if (hole.location == HoleLocation.Bottom && isFloor)
            {
                Vector2 faceCenter2D = new Vector2(faceCenter.x, faceCenter.z);
                Vector2 holeCenter2D = hole.bottomOffset;

                if (hole.type == HoleType.Circular)
                {
                    if (Vector2.Distance(faceCenter2D, holeCenter2D) < hole.radius) return true;
                }
                else if (hole.type == HoleType.Rectangular)
                {
                    float localX = Mathf.Abs(faceCenter2D.x - holeCenter2D.x);
                    float localZ = Mathf.Abs(faceCenter2D.y - holeCenter2D.y);
                    if (localX < (hole.width / 2f) && localZ < (hole.height / 2f)) return true;
                }
            }
        }

        return false;
    }
}