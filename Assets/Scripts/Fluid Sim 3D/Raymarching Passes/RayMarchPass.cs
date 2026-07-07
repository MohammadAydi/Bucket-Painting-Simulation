// RayMarchPass.cs  (FIXED)
// ─────────────────────────────────────────────────────────────────────────────
// Fix applied:
//   sceneDepthHandle null guard: resourceData.cameraDepthTexture can return an
//   invalid handle when depth priming is disabled on the URP renderer asset, or
//   during the first frame.  Added an IsValid() check before UseTexture() so
//   the pass doesn't silently fail / throw a RenderGraph validation error when
//   depth is unavailable.  The depth sample in the shader falls back to the
//   far plane (no occlusion culling) which is safe.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class RayMarchPass : ScriptableRenderPass, System.IDisposable
{
    // ── Shader property IDs ───────────────────────────────────────────────────
    static readonly int s_DensityMap        = Shader.PropertyToID("_DensityMap");
    static readonly int s_BoundsSize        = Shader.PropertyToID("_BoundsSize");
    static readonly int s_BoundsCenter      = Shader.PropertyToID("_BoundsCenter");
    static readonly int s_BoundaryWorldToLocal = Shader.PropertyToID("_BoundaryWorldToLocal");
    static readonly int s_BoundaryLocalMin  = Shader.PropertyToID("_BoundaryLocalMin");
    static readonly int s_BoundaryLocalMax  = Shader.PropertyToID("_BoundaryLocalMax");
    static readonly int s_DensityOffset     = Shader.PropertyToID("_DensityOffset");
    static readonly int s_StepSize          = Shader.PropertyToID("_StepSize");
    static readonly int s_PaintColor        = Shader.PropertyToID("_PaintColor");
    static readonly int s_SpecularStrength  = Shader.PropertyToID("_SpecularStrength");
    static readonly int s_Shininess         = Shader.PropertyToID("_Shininess");
    static readonly int s_ReflectStrength   = Shader.PropertyToID("_ReflectStrength");
    static readonly int s_AmbientStrength   = Shader.PropertyToID("_AmbientStrength");
    static readonly int s_UseHalfLambert    = Shader.PropertyToID("_UseHalfLambert");
    static readonly int s_FillLightStrength = Shader.PropertyToID("_FillLightStrength");
    static readonly int s_FillLightColor    = Shader.PropertyToID("_FillLightColor");
    static readonly int s_NormalEpsilon     = Shader.PropertyToID("_NormalEpsilon");
    static readonly int s_InvViewProj       = Shader.PropertyToID("_FluidInvViewProj");
    static readonly int s_CamWorldPos       = Shader.PropertyToID("_FluidCamWorldPos");

    // ── State ─────────────────────────────────────────────────────────────────
    FluidRendererFeature _feature;
    Material             _mat;
    static RTHandle      s_OutRT;

    // ─────────────────────────────────────────────────────────────────────────
    public void Setup(FluidRendererFeature feature)
    {
        _feature = feature;
        renderPassEvent = RenderPassEvent.AfterRenderingTransparents + 4;

        if (_mat == null && feature.raymarchShader != null)
            _mat = CoreUtils.CreateEngineMaterial(feature.raymarchShader);
    }

    // ── Render Graph entry point ──────────────────────────────────────────────
    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_mat == null) return;
        if (VoxelizeDensityPass.DensityMap == null) return;

        var cameraData   = frameData.Get<UniversalCameraData>();
        var resourceData = frameData.Get<UniversalResourceData>();
        int w = cameraData.cameraTargetDescriptor.width;
        int h = cameraData.cameraTargetDescriptor.height;

        FluidRTPool.EnsureCompositeOutRT(ref s_OutRT, w, h);

        SetMaterialParams(cameraData.camera);

        var outHandle        = renderGraph.ImportTexture(s_OutRT);
        var colorHandle      = resourceData.activeColorTexture;
        var sceneDepthHandle = resourceData.cameraDepthTexture;

        // ── Pass A: raymarch into outRT ────────────────────────────────────────
        using (var builder = renderGraph.AddRasterRenderPass<PassAData>("Fluid.RayMarch", out var data))
        {
            data.material = _mat;

            builder.SetRenderAttachment(outHandle, 0, AccessFlags.Write);
            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true);

            builder.SetRenderFunc((PassAData d, RasterGraphContext ctx) =>
            {
                ctx.cmd.ClearRenderTarget(false, true, Color.clear);
                Blitter.BlitTexture(ctx.cmd, new Vector4(1, 1, 0, 0), d.material, 0);
            });
        }

        // ── Pass B: alpha-blend outRT over camera colour ───────────────────────
        using (var builder = renderGraph.AddRasterRenderPass<PassBData>("Fluid.RayMarch.CopyToCamera", out var data))
        {
            data.material  = _mat;
            data.outHandle = outHandle;

            builder.UseTexture(outHandle, AccessFlags.Read);

            // FIX: guard against invalid depth handle (depth priming disabled,
            // first frame, or editor preview cameras that don't emit depth)
            if (sceneDepthHandle.IsValid())
                builder.UseTexture(sceneDepthHandle, AccessFlags.Read);

            builder.SetRenderAttachment(colorHandle, 0, AccessFlags.Write);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc((PassBData d, RasterGraphContext ctx) =>
            {
                Blitter.BlitTexture(ctx.cmd, d.outHandle, new Vector4(1, 1, 0, 0), d.material, 1);
            });
        }
    }

    // ── Material parameter upload ─────────────────────────────────────────────
    void SetMaterialParams(Camera camera)
    {
        _mat.SetTexture(s_DensityMap, VoxelizeDensityPass.DensityMap);

        // FIX: don't rely on UNITY_MATRIX_I_VP / _WorldSpaceCameraPos inside the
        // shader for reconstructing camera rays. Those globals aren't guaranteed
        // to be bound consistently for a custom fullscreen Blit pass across every
        // camera type — in particular Pass A renders into an off-screen RT
        // (s_OutRT), not directly to the backbuffer, and different graphics APIs
        // flip Y for render-to-texture vs. render-to-backbuffer. Scene view and
        // Game view cameras don't always go through identical setup here, which
        // is exactly why the fluid looked offset in Game view and drifted in
        // odd directions as the Scene view camera moved.
        //
        // Fix: build the inverse view-projection matrix explicitly per camera,
        // using GL.GetGPUProjectionMatrix(..., renderIntoTexture: true) so it
        // already accounts for the platform-specific flip for our off-screen
        // target, and upload that (plus the camera's world position) directly
        // instead of trusting engine globals.
        Matrix4x4 gpuProj    = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
        Matrix4x4 view       = camera.worldToCameraMatrix;
        Matrix4x4 viewProj   = gpuProj * view;
        Matrix4x4 invViewProj = viewProj.inverse;

        _mat.SetMatrix(s_InvViewProj, invViewProj);
        _mat.SetVector(s_CamWorldPos, camera.transform.position);

        Vector3 boundsMin = _feature.cachedBoundsMin;
        Vector3 boundsMax = _feature.cachedBoundsMax;
        Matrix4x4 l2w      = _feature.cachedLocalToWorld;
        Matrix4x4 w2l      = _feature.cachedWorldToLocal;

        // FIX: the density map is written by VoxelizeDensityPass using the FULL
        // local->world transform per-voxel (FluidVoxelizer.compute), so it
        // correctly supports a rotated/scaled boundary. This shader, however,
        // was reconstructing an AXIS-ALIGNED world box from the local size by
        // transforming each axis independently (l2w.MultiplyVector(...).magnitude).
        // That reconstruction is only valid when the boundary has NO rotation —
        // with any rotation it produces a box that doesn't match the actual
        // (rotated) density volume, so SampleDensity() looked up the wrong UVW
        // coordinates and the ray-box cull clipped valid regions inconsistently
        // as the view angle changed. That's what caused the density field to
        // look chopped into "cubes" that appeared/disappeared and seemed to
        // shift with the camera.
        //
        // Fix: send the boundary's world-to-local matrix and LOCAL min/max
        // straight to the shader. SampleDensity now transforms the world-space
        // sample position back into boundary-local space — the exact inverse
        // of what the compute shader did — so the UVW lookup is correct for
        // any rotation/scale, not just axis-aligned boxes.
        _mat.SetMatrix(s_BoundaryWorldToLocal, w2l);
        _mat.SetVector(s_BoundaryLocalMin, boundsMin);
        _mat.SetVector(s_BoundaryLocalMax, boundsMax);

        // _BoundsSize/_BoundsCenter are now ONLY used for the coarse world-space
        // ray-box cull (an early-out optimization), so they just need to safely
        // ENCLOSE the true rotated box — not match it exactly. Build that from
        // the 8 transformed corners rather than assuming axis alignment.
        Vector3 worldMin = Vector3.positiveInfinity;
        Vector3 worldMax = Vector3.negativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? boundsMin.x : boundsMax.x,
                (i & 2) == 0 ? boundsMin.y : boundsMax.y,
                (i & 4) == 0 ? boundsMin.z : boundsMax.z);
            Vector3 w = l2w.MultiplyPoint3x4(corner);
            worldMin = Vector3.Min(worldMin, w);
            worldMax = Vector3.Max(worldMax, w);
        }

        Vector3 worldSize   = worldMax - worldMin;
        Vector3 worldCenter = (worldMin + worldMax) * 0.5f;

        _mat.SetVector(s_BoundsSize,   worldSize);
        _mat.SetVector(s_BoundsCenter, worldCenter);

        var rs = _feature.raymarchSettings;
        _mat.SetFloat(s_DensityOffset,  rs != null ? rs.densityOffset  : 80f);
        _mat.SetFloat(s_StepSize,       rs != null ? rs.stepSize       : 0.03f);
        _mat.SetFloat(s_NormalEpsilon,  rs != null ? rs.normalEpsilon  : 0.08f);

        _mat.SetColor(s_PaintColor,        _feature.paintColor);
        _mat.SetFloat(s_SpecularStrength,  _feature.specularStrength);
        _mat.SetFloat(s_Shininess,         _feature.specularShininess);
        _mat.SetFloat(s_ReflectStrength,   _feature.reflectionStrength);
        _mat.SetFloat(s_AmbientStrength,   _feature.ambientStrength);
        _mat.SetFloat(s_UseHalfLambert,    _feature.useHalfLambert ? 1f : 0f);
        _mat.SetFloat(s_FillLightStrength, _feature.fillLightStrength);
        _mat.SetColor(s_FillLightColor,    _feature.fillLightColor);
    }

    public void Dispose()
    {
        CoreUtils.Destroy(_mat);
        s_OutRT?.Release();
        s_OutRT = null;
    }

    // ── Pass data ─────────────────────────────────────────────────────────────
    class PassAData { public Material material; }
    class PassBData
    {
        public Material      material;
        public TextureHandle outHandle;
    }
}