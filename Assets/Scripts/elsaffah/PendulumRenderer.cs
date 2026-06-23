using UnityEngine;

public class PendulumRenderer : MonoBehaviour
{
    public Transform pivot;
    public Transform ball;

    public int segments = 20;

    private LineRenderer line;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();

        line.positionCount = segments + 1;
        line.useWorldSpace = true;
    }

    private void Update()
    {
        if (pivot == null || ball == null)
            return;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;

            Vector3 p =
                Vector3.Lerp(
                    pivot.position,
                    ball.position,
                    t);

            line.SetPosition(i, p);
        }
    }
}