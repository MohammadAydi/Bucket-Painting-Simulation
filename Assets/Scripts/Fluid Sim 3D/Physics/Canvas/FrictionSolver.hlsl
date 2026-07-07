#ifndef FRICTION_SOLVER_INCLUDED
#define FRICTION_SOLVER_INCLUDED

#include "CanvasMath.hlsl"

// Implements:
//   f_k^friction = -max((1 - delta * d_k)^2, 0) * v_k
//
// where d_k is the (positive) distance from particle k to the canvas
// surface, delta controls how far the friction "zone" extends away from
// the surface (delta = 1 / effective range), and v_k is the particle's
// velocity. The force ramps from full strength at the surface (d_k = 0)
// down to zero at d_k = 1/delta, and is exactly zero beyond that.
//
// This only ever removes velocity (it's a drag term), so we apply it as
// a direct velocity damping rather than integrating it as a generic force,
// which keeps it unconditionally stable regardless of dt/mass tuning.
void ApplyCanvasFriction(uint3 id)
{
     if (id.x >= _ParticleCount)
        return;

    float3 position = _Positions[id.x];
    float3 velocity = _Velocities[id.x];

    float3 localPosition = mul(_CanvasWorldToLocal, float4(position, 1.0)).xyz;

    // Outside the finite rectangle -> no friction from this canvas
    if (abs(localPosition.x) > _CanvasHalfExtents.x)
        return;

    if (abs(localPosition.z) > _CanvasHalfExtents.y)
        return;

    // Distance from the plane (plane is y = 0), only meaningful near/above it
    float d_k = localPosition.y;

    if (d_k < 0.0)
        return;

    // f = -max((1 - delta*d)^2, 0) * v
    float falloff = saturate(1.0 - _CanvasFrictionCoeff * d_k);
    float frictionMagnitude = falloff * falloff;

    if (frictionMagnitude == 0.0)
        return;

    float3 frictionForce = -frictionMagnitude * velocity;

    // Apply as an impulse over dt (force -> velocity change)
    velocity += frictionForce * _DeltaTime;

    _Velocities[id.x] = velocity;
}

#endif
