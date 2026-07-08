// FluidRTPool.cs
// ──────────────────────────────────────────────────────────────────────────────
// Static helpers that own the persistent RTHandles used by the fluid pipeline.
// Each "Ensure" method creates/resizes the RT only when the resolution changes.
//
// IMPORTANT: In URP Render Graph, a single RTHandle cannot be used as BOTH
// a color attachment AND a depth attachment at the same time.
// So the particle depth pass uses TWO separate textures:
//   s_DepthRT     = R32_SFloat color  (stores the depth VALUE we care about)
//   s_DepthZRT    = Depth16 only       (the real GPU depth buffer for Z-testing)
// ──────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class FluidRTPool
{
    // ── Depth RT : R32_SFloat colour only (no depth buffer) ───────────────────
    // Stores the depth VALUE written by the particle shader's SV_Target output.
    public static void EnsureDepthRT(ref RTHandle handle, int w, int h)
    {
        if (handle != null && handle.rt != null &&
            handle.rt.width == w && handle.rt.height == h)
            return;

        handle?.Release();
        handle = RTHandles.Alloc(
            width:           w,
            height:          h,
            colorFormat:     GraphicsFormat.R32_SFloat,
            depthBufferBits: DepthBits.None,       // NO depth buffer here
            filterMode:      FilterMode.Bilinear,
            wrapMode:        TextureWrapMode.Clamp,
            useMipMap:       false,
            name:            "Fluid_DepthRT");
    }

    // ── Depth Z RT : depth-only buffer for Z-testing during particle draw ─────
    // Used as SetRenderDepthAttachment in ParticleDepthPass.
    // Not read by any downstream pass — purely for GPU depth testing.
    public static void EnsureDepthZRT(ref RTHandle handle, int w, int h)
    {
        if (handle != null && handle.rt != null &&
            handle.rt.width == w && handle.rt.height == h)
            return;

        handle?.Release();
        handle = RTHandles.Alloc(
            width:           w,
            height:          h,
            depthBufferBits: DepthBits.Depth16,
            colorFormat:     GraphicsFormat.None,  // depth-only, no color
            filterMode:      FilterMode.Point,
            wrapMode:        TextureWrapMode.Clamp,
            useMipMap:       false,
            name:            "Fluid_DepthZRT");
    }

    // ── Comp RT : RGBA32F, no depth ───────────────────────────────────────────
    // Ping-pong target for Pack → Bilateral → Normal → Composite.
    public static void EnsureCompRT(ref RTHandle handle, int w, int h)
    {
        if (handle != null && handle.rt != null &&
            handle.rt.width == w && handle.rt.height == h)
            return;

        handle?.Release();
        handle = RTHandles.Alloc(
            width:           w,
            height:          h,
            colorFormat:     GraphicsFormat.R32G32B32A32_SFloat,
            depthBufferBits: DepthBits.None,
            filterMode:      FilterMode.Bilinear,
            wrapMode:        TextureWrapMode.Clamp,
            useMipMap:       false,
            name:            "Fluid_CompRT");
    }

    // ── Normal RT : RGBA32F, no depth ─────────────────────────────────────────
    // Stores reconstructed world-space normals (xyz) and coverage (w).
    public static void EnsureNormalRT(ref RTHandle handle, int w, int h)
    {
        if (handle != null && handle.rt != null &&
            handle.rt.width == w && handle.rt.height == h)
            return;

        handle?.Release();
        handle = RTHandles.Alloc(
            width:           w,
            height:          h,
            colorFormat:     GraphicsFormat.R32G32B32A32_SFloat,
            depthBufferBits: DepthBits.None,
            filterMode:      FilterMode.Bilinear,
            wrapMode:        TextureWrapMode.Clamp,
            useMipMap:       false,
            name:            "Fluid_NormalRT");
    }


    // ── Pigment Color RT : RGBA16F, no depth ─────────────────────────────────
    // ParticleDepthPass writes per-particle pigment color into this RT at the
    // same time it writes depth values into s_DepthRT.
    // FluidCompositePass samples it to shade each fluid pixel with the correct
    // pigment color instead of the global _PaintColor uniform.
    public static void EnsurePigmentColorRT(ref RTHandle handle, int w, int h)
    {
        if (handle != null && handle.rt != null &&
            handle.rt.width == w && handle.rt.height == h)
            return;

        handle?.Release();
        handle = RTHandles.Alloc(
            width:           w,
            height:          h,
            colorFormat:     GraphicsFormat.R16G16B16A16_SFloat,
            depthBufferBits: DepthBits.None,
            filterMode:      FilterMode.Bilinear,
            wrapMode:        TextureWrapMode.Clamp,
            useMipMap:       false,
            name:            "Fluid_PigmentColorRT");
    }

    // ── Composite Output RT : RGBA32F, no depth ───────────────────────────────
    // FluidCompositePass renders into this, then a copy pass blits it to camera.
    // This avoids the "same texture read+write in one pass" Render Graph error.
    public static void EnsureCompositeOutRT(ref RTHandle handle, int w, int h)
    {
        if (handle != null && handle.rt != null &&
            handle.rt.width == w && handle.rt.height == h)
            return;

        handle?.Release();
        handle = RTHandles.Alloc(
            width:           w,
            height:          h,
            colorFormat:     GraphicsFormat.R16G16B16A16_SFloat,
            depthBufferBits: DepthBits.None,
            filterMode:      FilterMode.Bilinear,
            wrapMode:        TextureWrapMode.Clamp,
            useMipMap:       false,
            name:            "Fluid_CompositeOutRT");
    }
}
