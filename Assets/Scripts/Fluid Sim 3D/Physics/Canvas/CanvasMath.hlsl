#ifndef CANVAS_MATH_INCLUDED
#define CANVAS_MATH_INCLUDED


// Signed distance from a world-space point to the canvas plane.
// Positive = in front of the canvas (on the normal side).
float Canvas_SignedDistance(float3 worldPos)
{
    return dot(worldPos - _CanvasCenter, _CanvasNormal);
}

// Projects a world point onto the canvas plane and returns the local
// (tangent, bitangent) coordinates of that projection.
float2 Canvas_LocalCoords(float3 worldPos)
{
    float3 rel = worldPos - _CanvasCenter;
    float u = dot(rel, _CanvasTangent);
    float v = dot(rel, _CanvasBitangent);
    return float2(u, v);
}

// True if the projected point falls within the finite rectangular extent
// of the canvas (i.e. actually over the canvas, not just over its infinite plane).
bool Canvas_WithinExtent(float2 localCoords)
{
    return abs(localCoords.x) <= _CanvasHalfExtents.x &&
           abs(localCoords.y) <= _CanvasHalfExtents.y;
}

#endif
