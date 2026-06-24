using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential)]
public struct ParticleData3D
{
    public Vector4 position;
    public Vector4 velocity;
    public Vector4 force;
    public float density;
    public float pressure;

    public ParticleData3D(Vector3 position)
    {
        this.position = new Vector4(position.x, position.y, position.z, 0f);
        velocity = Vector4.zero;
        force = Vector4.zero;
        density = 0f;
        pressure = 0f;
    }
}