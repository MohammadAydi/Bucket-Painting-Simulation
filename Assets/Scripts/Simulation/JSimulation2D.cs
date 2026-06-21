using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Mathematics;

public class JSimulation2D : MonoBehaviour
{
    [Header("Settings")] public float gravity = 9.81f;

    [Range(0, 1)] public float collisionDamping = 0.05f;

    public Vector2 boundsSize;
    [Header("References")] public ComputeShader compute;
    public JParticleSpawner2D spawner;
    public JParticleDisplay2D display;

    // Buffers
    public ComputeBuffer positionBuffer { get; private set; }
    public ComputeBuffer velocityBuffer { get; private set; }

    public int numParticles { get; private set; }

    // Kernel IDs
    const int externalForcesKernel = 0;
    const int updatePositionKernel = 1;
    JParticleSpawner2D.ParticleSpawnData spawnData;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        spawnData = spawner.GetSpawn();
        numParticles = spawnData.positions.Length;

        positionBuffer = ComputeHelper.CreateStructuredBuffer<float2>(numParticles);
        velocityBuffer = ComputeHelper.CreateStructuredBuffer<float2>(numParticles);
        positionBuffer.SetData(spawnData.positions);
        velocityBuffer.SetData(spawnData.velocities);

        ComputeHelper.SetBuffer(compute, positionBuffer, "Positions", externalForcesKernel, updatePositionKernel);
        ComputeHelper.SetBuffer(compute, velocityBuffer, "Velocities", externalForcesKernel, updatePositionKernel);

        compute.SetInt("numParticles", numParticles);
        display.Init(this);
    }

    // Update is called once per frame
    void Update()
    {
        compute.SetFloat("deltaTime", Time.deltaTime);
        compute.SetFloat("gravity", gravity);
        compute.SetFloat("collisionDamping", collisionDamping);
        compute.SetVector("boundsSize", boundsSize);
        
        ComputeHelper.Dispatch(compute, numParticles, kernelIndex: externalForcesKernel);
        ComputeHelper.Dispatch(compute, numParticles, kernelIndex: updatePositionKernel);
    }
    void OnDestroy()
    {
        ComputeHelper.Release(positionBuffer, velocityBuffer);
    }
    
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1, 0, 0.4f);
        Gizmos.DrawWireCube(Vector2.zero, boundsSize);

    }
}