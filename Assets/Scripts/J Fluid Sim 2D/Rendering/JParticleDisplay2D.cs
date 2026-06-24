using System;
using UnityEngine;

public class JParticleDisplay2D : MonoBehaviour
{
    public Mesh mesh;
    public Shader shader;
    public float scale;
    Material material;

    public ComputeBuffer argsBuffer;
    public Bounds bounds;
    public void Init(JSimulation2D sim)
    {
        material = new Material(shader);
        material.SetBuffer("Positions2D", sim.positionBuffer);
        material.SetBuffer("Velocities", sim.velocityBuffer);
        material.SetFloat("scale", scale);
        argsBuffer = ComputeHelper.CreateArgsBuffer(mesh, sim.positionBuffer.count);
        bounds = new Bounds(Vector3.zero, Vector3.one * 10000);
    }

    private void LateUpdate()
    {
        if (shader != null)
        {
            Graphics.DrawMeshInstancedIndirect(mesh, 0, material, bounds, argsBuffer);
        }
    }

    void OnDestroy()
    {
        ComputeHelper.Release(argsBuffer);
    }
}
