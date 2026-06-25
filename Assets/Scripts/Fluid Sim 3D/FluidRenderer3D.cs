using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class FluidRenderer3D : MonoBehaviour
{
    [Header("References")] public Shader depthShader;
    public Shader smoothShader;

    [Header("Settings")] public float particleScale = 0.15f;
    public float blurRadius = 8; // ← tweak in Inspector
    public float depthFalloff = 20;

    Material _matDepth;
    Mesh _quadMesh;
    RenderTexture _depthRt;
    ComputeBuffer _argsBuffer;

    Material _matSmooth;
    RenderTexture _smoothRt;

    FluidManager3D _fluidManager;


    void Start()
    {
        _fluidManager = GetComponent<FluidManager3D>();
        _matDepth = new Material(depthShader);
        _matSmooth = new Material(smoothShader);
        _quadMesh = CreateQuad();
    }

    void LateUpdate()
    {
        var buf = _fluidManager.ParticleBuffer;
        int count = _fluidManager.ParticleCount;
        if (buf == null || count == 0) return;

        EnsureRTs();

        if (_argsBuffer == null)
            _argsBuffer = new ComputeBuffer(5, sizeof(uint),
                ComputeBufferType.IndirectArguments);
        _argsBuffer.SetData(new uint[]
        {
            _quadMesh.GetIndexCount(0), (uint)count, 0, 0, 0
        });

        // ── 1. Draw particles into depthRt ──────────────────────────────
        _matDepth.SetBuffer("_Particles", buf);
        _matDepth.SetFloat("_ParticleScale", particleScale);

        Graphics.DrawMeshInstancedIndirect(
            _quadMesh, 0, _matDepth,
            new Bounds(Vector3.zero, Vector3.one * 1000),
            _argsBuffer);

        // ── 2. Smooth: horizontal pass depthRt → smoothRt ───────────────
        _matSmooth.SetFloat("_BlurRadius", blurRadius);
        _matSmooth.SetFloat("_DepthFalloff", depthFalloff);
        Graphics.Blit(_depthRt, _smoothRt, _matSmooth, 0); // pass 0 = horizontal

        // ── 3. Smooth: vertical pass smoothRt → depthRt ─────────────────
        Graphics.Blit(_smoothRt, _depthRt, _matSmooth, 1); // pass 1 = vertical

        // ── 4. Show result on screen (debug) ────────────────────────────
        Graphics.Blit(_depthRt, (RenderTexture)null);
    }

    void EnsureRTs()
    {
        if (_depthRt != null && _depthRt.width == Screen.width) return;

        _depthRt?.Release();
        _smoothRt?.Release();

        var fmt = GraphicsFormat.R32_SFloat;
        _depthRt = new RenderTexture(Screen.width, Screen.height, 16, fmt);
        _smoothRt = new RenderTexture(Screen.width, Screen.height, 0, fmt);
        _depthRt.Create();
        _smoothRt.Create();
    }

    Mesh CreateQuad()
    {
        var mesh = new Mesh();
        mesh.vertices = new Vector3[]
        {
            new(-0.5f, 0.5f, 0), new(0.5f, 0.5f, 0),
            new(-0.5f, -0.5f, 0), new(0.5f, -0.5f, 0)
        };
        mesh.uv = new Vector2[]
        {
            new(0, 1), new(1, 1), new(0, 0), new(1, 0)
        };
        mesh.triangles = new int[] { 0, 1, 2, 2, 1, 3 };
        mesh.RecalculateBounds();
        return mesh;
    }

    void OnDestroy()
    {
        _argsBuffer?.Release();
        _depthRt?.Release();
        _smoothRt?.Release();
    }
}