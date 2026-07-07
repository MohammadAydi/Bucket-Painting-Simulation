using System;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class RenderSystem3D : IDisposable
{
    static readonly int PositionId    = Shader.PropertyToID("_Position");
    static readonly int VelocityId    = Shader.PropertyToID("_Velocity");
    static readonly int PigmentsId    = Shader.PropertyToID("_Pigments");
    static readonly int ParticleRadiusId = Shader.PropertyToID("_ParticleRadius");
    static readonly int ColourMapId   = Shader.PropertyToID("_ColourMap");
    static readonly int VelocityMaxId = Shader.PropertyToID("_VelocityMax");

    readonly Material _material;
    readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();

    Mesh _quadMesh;
    Texture2D _gradientTexture;
    
    // Added ComputeBuffer for Indirect Drawing
    ComputeBuffer _argsBuffer;
    int _cachedParticleCount = -1;

    public RenderSystem3D(Material material)
    {
        _material = material;
        _material.enableInstancing = true;
    }

    public void Initialize(
     ParticleSettings settings,
     ComputeBuffer positionsBuffer,
     ComputeBuffer velocitiesBuffer,
     ComputeBuffer pigmentBuffer = null)
    {
        CreateQuadMesh();
        SyncMaterial(settings);

        _material.SetBuffer(PositionId, positionsBuffer);
        _material.SetBuffer(VelocityId, velocitiesBuffer);
        if (pigmentBuffer != null)
            _material.SetBuffer(PigmentsId, pigmentBuffer);
    }

    public void SyncMaterial(ParticleSettings settings)
    {
        _material.SetFloat(ParticleRadiusId, settings.radius);
        _material.SetFloat(VelocityMaxId, settings.velocityDisplayMax);

        BakeGradient(settings.colourMap, settings.gradientResolution);
        _material.SetTexture(ColourMapId, _gradientTexture);
    }

    public void Render(int particleCount, Bounds bounds)
    {
        if (particleCount <= 0)
            return;

        UpdateArgsBuffer(particleCount);

        // Switched to DrawMeshInstancedIndirect for GPU-driven performance
        Graphics.DrawMeshInstancedIndirect(
             _quadMesh,
             0,
             _material,
             bounds,
             _argsBuffer,
             0,
             _propertyBlock,
             ShadowCastingMode.Off,
             receiveShadows: false,
             layer: 0,
             camera: null,
             LightProbeUsage.Off,
             lightProbeProxyVolume: null
         );
    }

    public void Dispose()
    {
        DestroyGradientTexture();
        DestroyMesh();
        ReleaseArgsBuffer();
    }

    void CreateQuadMesh()
    {
        DestroyMesh();
        _quadMesh = new Mesh
        {
            name = "ParticleQuad",
            vertices = new Vector3[] {
                new Vector3(-1, -1, 0),
                new Vector3( 1, -1, 0),
                new Vector3(-1,  1, 0),
                new Vector3( 1,  1, 0)
            },

            // Add back-facing normals so the vertex shader can handle lighting natively
            normals = new Vector3[] {
                Vector3.back, Vector3.back, Vector3.back, Vector3.back
            },

            triangles = new int[] { 0, 2, 1, 2, 3, 1 }
        };
    }

    void UpdateArgsBuffer(int particleCount)
    {
        if (_argsBuffer == null)
        {
            _argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        }

        if (_cachedParticleCount != particleCount)
        {
            _cachedParticleCount = particleCount;
            uint[] args = new uint[5] {
                _quadMesh.GetIndexCount(0), // Index count per instance
                (uint)particleCount,        // Instance count
                0, 0, 0                     // Start index, base vertex, start instance
            };
            _argsBuffer.SetData(args);
        }
    }

    void ReleaseArgsBuffer()
    {
        if (_argsBuffer != null)
        {
            _argsBuffer.Release();
            _argsBuffer = null;
        }
    }

    void DestroyMesh()
    {
        if (_quadMesh == null) return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(_quadMesh);
        else
            UnityEngine.Object.DestroyImmediate(_quadMesh);

        _quadMesh = null;
    }

    void BakeGradient(Gradient gradient, int resolution)
    {
        resolution = Mathf.Max(2, resolution);

        if (_gradientTexture == null || _gradientTexture.width != resolution)
        {
            DestroyGradientTexture();
            _gradientTexture = new Texture2D(resolution, 1, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "VelocityGradient"
            };
        }

        Color[] pixels = new Color[resolution];
        for (int i = 0; i < resolution; i++)
        {
            float t = i / (float)(resolution - 1);
            pixels[i] = gradient.Evaluate(t);
        }

        _gradientTexture.SetPixels(pixels);
        _gradientTexture.Apply();
    }

    void DestroyGradientTexture()
    {
        if (_gradientTexture == null) return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(_gradientTexture);
        else
            UnityEngine.Object.DestroyImmediate(_gradientTexture);

        _gradientTexture = null;
    }
}