#ifndef CANVAS_COLLISION_INCLUDED
#define CANVAS_COLLISION_INCLUDED

#include "CanvasMath.hlsl"

// Resolves collision of particle `id` against the finite canvas plane.
// Mirrors the style of your old box collision: push the particle back to
// the surface along the normal and damp/reflect the normal velocity
// component. Particles outside the canvas's rectangular extent (i.e. off
// the edge of the canvas) are left untouched by this kernel.
void ResolveCanvasCollision(uint3 id)
{
    if (id.x >= _ParticleCount)
        return;

    float3 position = _Positions[id.x];
    float3 velocity = _Velocities[id.x];

    float3 localPosition = mul(_CanvasWorldToLocal, float4(position, 1.0)).xyz;
    float3 localVelocity = mul((float3x3)_CanvasWorldToLocal, velocity);

    // In-plane bounds check: normal is local Y, so the plane spans local X/Z
    if (abs(localPosition.x) > _CanvasHalfExtents.x)
        return;

    if (abs(localPosition.z) > _CanvasHalfExtents.y)
        return;

    // Distance from the plane (plane is y = 0)
    float signedDistance = localPosition.y;

    if (signedDistance > _ParticleRadius)
        return;

    localPosition.y = _ParticleRadius;

    if (localVelocity.y < 0.0)
    {
        localVelocity.y *= -_CanvasCollisionDamping;
    }

    position = mul(_CanvasLocalToWorld, float4(localPosition, 1.0)).xyz;
    velocity = mul((float3x3)_CanvasLocalToWorld, localVelocity);

    _Positions[id.x] = position;
    _PredictedPositions[id.x] = position;
    _Velocities[id.x] = velocity;
}

#endif
