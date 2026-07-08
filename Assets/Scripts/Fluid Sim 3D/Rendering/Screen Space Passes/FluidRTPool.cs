
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class FluidRTPool
{
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
            depthBufferBits: DepthBits.None,     
            filterMode:      FilterMode.Bilinear,
            wrapMode:        TextureWrapMode.Clamp,
            useMipMap:       false,
            name:            "Fluid_DepthRT");
    }

    
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
            colorFormat:     GraphicsFormat.None,  
            filterMode:      FilterMode.Point,
            wrapMode:        TextureWrapMode.Clamp,
            useMipMap:       false,
            name:            "Fluid_DepthZRT");
    }

   
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
