using System;
using UnityEngine;
using static UnityEngine.Mathf;

public class CircleBody : MonoBehaviour {
    public Vector2 velocity;
    public Vector2 acceleration;
    
    public float radius = 0.5f;
    public float mass = 1f;
    public float gravity = 9.81f;

    private BoundsRenderer _boundsRenderer;
    public Vector2 boundsSize;
    public float collisionDamping = 0.8f;


    private void OnValidate() {
        transform.localScale = Vector3.one * (radius * 2f);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start() {
        _boundsRenderer = FindFirstObjectByType<BoundsRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        velocity += Vector2.down * (gravity * Time.deltaTime);
        transform.position += (Vector3) velocity * Time.deltaTime;
        ResolveCollisions();
    }

    void ResolveCollisions() {
        boundsSize = _boundsRenderer.boundsSize;
        Vector2 halfBoundsSize =
            boundsSize / 2 - Vector2.one * radius;

        Vector3 pos = transform.position;

        if (Abs(pos.x) > halfBoundsSize.x)
        {
            pos.x = halfBoundsSize.x * Sign(pos.x);
            velocity.x *= -collisionDamping;
        }

        if (Abs(pos.y) > halfBoundsSize.y)
        {
            pos.y = halfBoundsSize.y * Sign(pos.y);
            velocity.y *= -collisionDamping;
        }

        transform.position = pos;
    }

  
}
