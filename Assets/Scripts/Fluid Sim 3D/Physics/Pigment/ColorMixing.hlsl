// ColorMixing.hlsl
// ─────────────────────────────────────────────────────────────────────────────
// From-scratch subtractive pigment mixing approximation using the RYB
// (Red-Yellow-Blue) color model, WITHOUT any external library.
//
// Real pigments mix by absorbing/reflecting light (Kubelka-Munk theory);
// linear RGB math cannot reproduce that (yellow+blue in RGB always trends
// toward gray, never green). RYB space fixes the specific "primary colors
// mix to a plausible secondary" cases by re-parameterizing color as amounts
// of Red/Yellow/Blue pigment instead of Red/Green/Blue light, using the
// classic 8-corner cube interpolation:
//
//   corner(r,y,b) : r,y,b each 0 or 1, i.e. the 8 corners of the RYB unit cube.
//   (0,0,0) = white   (1,0,0) = red     (0,1,0) = yellow   (0,0,1) = blue
//   (1,1,0) = orange  (1,0,1) = violet  (0,1,1) = green    (1,1,1) = black
//
// RGB_from_RYB(r,y,b) is the trilinear interpolation of the 8 corner colors.
// This is a well-known, published approximation (Gosset & Chen, "Paint
// Inspired Color Mixing and Compositing for Visualization", 2004) — not a
// black-box library, just an interpolation formula anyone can derive/verify.
// ─────────────────────────────────────────────────────────────────────────────

static const float3 RYB_WHITE  = float3(1.0,   1.0,   1.0);
static const float3 RYB_RED    = float3(1.0,   0.0,   0.0);
static const float3 RYB_YELLOW = float3(1.0,   1.0,   0.0);
static const float3 RYB_BLUE   = float3(0.163, 0.373, 0.6);
static const float3 RYB_ORANGE = float3(1.0,   0.5,   0.0);
static const float3 RYB_VIOLET = float3(0.5,   0.0,   0.5);
static const float3 RYB_GREEN  = float3(0.0,   0.66,  0.2);
static const float3 RYB_BLACK  = float3(0.2,   0.094, 0.0);

// Converts an (r, y, b) pigment-amount triple, each in [0,1], to displayable RGB.
float3 RYBtoRGB(float3 ryb)
{
    float r = saturate(ryb.x);
    float y = saturate(ryb.y);
    float b = saturate(ryb.z);

    float3 result =
        RYB_WHITE  * (1 - r) * (1 - y) * (1 - b) +
        RYB_RED    *      r  * (1 - y) * (1 - b) +
        RYB_YELLOW * (1 - r) *      y  * (1 - b) +
        RYB_BLUE   * (1 - r) * (1 - y) *      b  +
        RYB_ORANGE *      r  *      y  * (1 - b) +
        RYB_VIOLET *      r  * (1 - y) *      b  +
        RYB_GREEN  * (1 - r) *      y  *      b  +
        RYB_BLACK  *      r  *      y  *      b;

    return saturate(result);
}
