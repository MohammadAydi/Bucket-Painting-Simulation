// // FluidRendererFeature.cs
// // ──────────────────────────────────────────────────────────────────────────────
// // ScriptableRendererFeature that injects the screen-space fluid pipeline into
// // URP's Render Graph (Unity 6 / URP 17+).
// //
// // Pipeline order (all after opaque geometry):
// //   Pass 1 – ParticleDepthPass   : draw billboard quads → depthRt  (R32_SFloat + depth)
// //   Pass 2 – PackDepthPass       : blit depthRt → compRt RGBA32F   (r=depth, a=depth)
// //   Pass 3 – BilateralPass(es)   : smooth compRt in-place (bilateral 1-D or 2-D)
// //   Pass 4 – NormalReconstructPass: blit compRt → normalRt          (world normals)
// //   Pass 5 – FluidCompositePass  : shade + write to camera colour
// //
// // How to use:
// //   1. Add this feature to your URP Renderer Asset (Add Renderer Feature → FluidRenderer).
// //   2. Assign the FluidManager3D reference via the inspector field on this feature,
// //      OR put a FluidRendererSettings component on the same GameObject as your FluidManager3D
// //      (the passes will find it automatically via FindFirstObjectByType).
// //   3. Assign all shader references in the feature inspector.
// // ──────────────────────────────────────────────────────────────────────────────
//
// using UnityEngine;
// using UnityEngine.Rendering;
// using UnityEngine.Rendering.Universal;
// using UnityEngine.Rendering.RenderGraphModule;
//
// [System.Serializable]
// public class FluidRendererFeature : ScriptableRendererFeature
// {
//     // ── Inspector ─────────────────────────────────────────────────────────────
//     // NOTE: FluidManager3D is a scene MonoBehaviour; this feature is a project
//     // asset (ScriptableObject). Unity cannot serialize cross-references between
//     // assets and scene objects, which causes "Type mismatch" in the inspector.
//     // Solution: find the manager automatically at runtime instead.
//     [System.NonSerialized]
//     public FluidManager3D fluidManager;          // found automatically at runtime
//
//     [Header("Shaders")]
//     public Shader particleDepthShader;           // Fluid/ParticleDepth3D_URP
//     public Shader packDepthShader;               // Fluid/PackDepth_URP
//     public Shader bilateral1DShader;             // Hidden/BilateralFilter1D_URP
//     public Shader bilateral2DShader;             // Hidden/BilateralFilter2D_URP
//     public Shader normalsFromDepthShader;        // Fluid/NormalsFromDepth_URP
//     public Shader fluidCompositeShader;          // Fluid/FluidComposite_URP
//
//     [Header("Particle Depth Settings")]
//     public float depthParticleSize = 0.15f;
//
//     [Header("Blur Type")]
//     public FluidBlurType blurType = FluidBlurType.Bilateral1D;
//
//     [Header("Bilateral Settings")]
//     public BilateralFilterSettings bilateralSettings = new BilateralFilterSettings
//     {
//         worldRadius        = 0.13f,
//         maxScreenSpaceSize = 40,
//         strength           = 0.5f,
//         diffStrength       = 20f,
//         iterations         = 3
//     };
//
//     [Header("Paint / Composite")]
//     [ColorUsage(false, true)]
//     public Color paintColor = new Color(0.2f, 0.5f, 1f);
//     public float specularStrength  = 0.8f;
//     public float specularShininess = 64f;
//     [Range(0f, 1f)]
//     public float reflectionStrength = 0.15f;
//
//     [Header("Lighting Fill")]
//     [Range(0f, 1f)]
//     public float ambientStrength   = 0.25f;
//     public bool  useHalfLambert    = true;
//     [Range(0f, 1f)]
//     public float fillLightStrength = 0.2f;
//     public Color fillLightColor    = new Color(0.4f, 0.35f, 0.3f);
//
//     // ── Nested types ──────────────────────────────────────────────────────────
//     public enum FluidBlurType { Bilateral1D, Bilateral2D }
//
//     [System.Serializable]
//     public struct BilateralFilterSettings
//     {
//         public float worldRadius;
//         public int   maxScreenSpaceSize;
//         [Range(0f, 1f)] public float strength;
//         public float diffStrength;
//         public int   iterations;
//     }
//
//     // ── Private pass references ───────────────────────────────────────────────
//     ParticleDepthPass      _depthPass;
//     PackDepthPass          _packPass;
//     BilateralSmoothPass    _bilateralPass;
//     NormalReconstructPass  _normalPass;
//     FluidCompositePass     _compositePass;
//
//     // ── ScriptableRendererFeature API ─────────────────────────────────────────
//
//     public override void Create()
//     {
//         _depthPass     = new ParticleDepthPass();
//         _packPass      = new PackDepthPass();
//         _bilateralPass = new BilateralSmoothPass();
//         _normalPass    = new NormalReconstructPass();
//         _compositePass = new FluidCompositePass();
//     }
//
//     public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
//     {
//         // Lazy-find manager if not set in inspector
//         if (fluidManager == null)
//             fluidManager = Object.FindFirstObjectByType<FluidManager3D>();
//
//         if (fluidManager == null || fluidManager.PositionsBuffer == null || fluidManager.ParticleCount == 0)
//             return;
//
//         // Skip non-game/scene cameras
//         if (renderingData.cameraData.cameraType == CameraType.Preview) return;
//
//         // Validate shaders
//         if (!ValidateShaders()) return;
//
//         // Create materials lazily (pass references in)
//         _depthPass    .Setup(this);
//         _packPass     .Setup(this);
//         _bilateralPass.Setup(this);
//         _normalPass   .Setup(this);
//         _compositePass.Setup(this);
//
//         // All passes run after skybox/opaques — AfterRenderingSkybox keeps the
//         // background colour intact so we can composite over it.
//         renderer.EnqueuePass(_depthPass);
//         renderer.EnqueuePass(_packPass);
//         renderer.EnqueuePass(_bilateralPass);
//         renderer.EnqueuePass(_normalPass);
//         renderer.EnqueuePass(_compositePass);
//     }
//
//     protected override void Dispose(bool disposing)
//     {
//         _depthPass    ?.Dispose();
//         _packPass     ?.Dispose();
//         _bilateralPass?.Dispose();
//         _normalPass   ?.Dispose();
//         _compositePass?.Dispose();
//     }
//
//     bool ValidateShaders()
//     {
//         bool ok = true;
//         if (!particleDepthShader)     { Debug.LogError("[FluidRenderer] particleDepthShader not assigned!");     ok = false; }
//         if (!packDepthShader)         { Debug.LogError("[FluidRenderer] packDepthShader not assigned!");         ok = false; }
//         if (!bilateral1DShader && blurType == FluidBlurType.Bilateral1D)
//                                       { Debug.LogError("[FluidRenderer] bilateral1DShader not assigned!");       ok = false; }
//         if (!bilateral2DShader && blurType == FluidBlurType.Bilateral2D)
//                                       { Debug.LogError("[FluidRenderer] bilateral2DShader not assigned!");       ok = false; }
//         if (!normalsFromDepthShader)  { Debug.LogError("[FluidRenderer] normalsFromDepthShader not assigned!"); ok = false; }
//         if (!fluidCompositeShader)    { Debug.LogError("[FluidRenderer] fluidCompositeShader not assigned!");   ok = false; }
//         return ok;
//     }
// }
