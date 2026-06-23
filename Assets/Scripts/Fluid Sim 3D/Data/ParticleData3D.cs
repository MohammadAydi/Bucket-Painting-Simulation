using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential)]
public struct ParticleData3D
{
    public Vector2 position;
    public Vector2 velocity;
    public Vector2 force;
    public float density;
    public float pressure;

    public ParticleData3D(Vector2 position)
    {
        this.position = position;
        velocity = Vector2.zero;
        force = Vector2.zero;
        density = 0f;
        pressure = 0f;
    }
}