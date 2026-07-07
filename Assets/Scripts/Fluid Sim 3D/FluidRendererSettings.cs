using UnityEngine;

public class FluidRendererSettings : MonoBehaviour
{
    public enum FluidRenderMode
    {
        ScreenSpace,
        Raymarch
    }

    [Header("Rendering")]
    public FluidRenderMode renderMode = FluidRenderMode.ScreenSpace;

    [Header("Screen Space")]
    public ScreenSpaceSettings screenSpaceSettings = new();

    [Header("Raymarch")]
    public RaymarchSettings raymarchSettings = new();
}