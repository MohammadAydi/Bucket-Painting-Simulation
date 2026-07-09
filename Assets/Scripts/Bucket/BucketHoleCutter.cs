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
}