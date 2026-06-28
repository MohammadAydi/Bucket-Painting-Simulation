using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

// [StructLayout(LayoutKind.Sequential)]
// public struct ParticleData3D
// {
//     public Vector4 position;
//     public Vector4 predictedPosition;
//     public Vector4 velocity;
//     public Vector4 force;
//     public float density;
//     public float pressure;

//     public ParticleData3D(Vector3 pos)
//     {
//         position = new Vector4(pos.x, pos.y, pos.z, 0f);
//         predictedPosition = new Vector4(pos.x, pos.y, pos.z, 0f);
//         velocity = Vector4.zero;
//         force = Vector4.zero;
//         density = 0f;
//         pressure = 0f;
//     }
// }

public struct SpawnData3D
{
    public float3[] positions;
    public float3[] velocities;

}