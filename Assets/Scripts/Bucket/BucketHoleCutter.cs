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

    // LIVE AUTOREFRESH TRIGGER
    private void OnValidate()
    {
        BucketGenerator generator = GetComponent<BucketGenerator>();
        if (generator != null)
        {
            generator.OnValidate(); // Forces immediate reconstruction when inspector values tweak
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
                float faceAngleDeg = faceAngleRad * Mathf.Rad2Deg; // FIXED: Typo fixed here
                if (faceAngleDeg < 0) faceAngleDeg += 360f;

                float angleDelta = Mathf.Abs(faceAngleDeg - hole.angleDegrees);
                if (angleDelta > 180f) angleDelta = 360f - angleDelta;

                if (hole.type == HoleType.Circular)
                {
                    float radians = hole.angleDegrees * Mathf.Deg2Rad;
                    float currentRadius = Mathf.Lerp(bottomRadius, topRadius, hole.heightPosition / bucketHeight);
                    Vector3 holeCenter = new Vector3(Mathf.Cos(radians) * currentRadius, hole.heightPosition, Mathf.Sin(radians) * currentRadius);

                    if (Vector3.Distance(faceCenter, holeCenter) < hole.radius) return true;
                }
                else if (hole.type == HoleType.Rectangular)
                {
                    float averageRadius = (bottomRadius + topRadius) / 2f;
                    float arcDistanceX = (angleDelta * Mathf.Deg2Rad) * averageRadius;
                    float localY = Mathf.Abs(faceCenter.y - hole.heightPosition);

                    if (arcDistanceX < (hole.width / 2f) && localY < (hole.height / 2f)) return true;
                }
            }
            else if (hole.location == HoleLocation.Bottom && isFloor)
            {
                Vector2 faceCenter2D = new Vector2(faceCenter.x, faceCenter.z);
                Vector2 holeCenter2D = hole.bottomOffset;

                float dist2D = Vector2.Distance(faceCenter2D, holeCenter2D);

                if (hole.type == HoleType.Circular && dist2D < hole.radius) return true;

                if (hole.type == HoleType.Rectangular)
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