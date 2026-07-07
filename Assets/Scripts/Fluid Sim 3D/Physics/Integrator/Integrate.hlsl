void ResolveCollisions(inout float3 position, inout float3 velocity)
{
    float3 positionLocal = mul(_BoundaryWorldToLocal, float4(position, 1.0)).xyz; 
    float3 velocityLocal = mul((float3x3)_BoundaryWorldToLocal, velocity); 

    float3 minBound = _BoundaryLocalMin + _ParticleRadius; 
    float3 maxBound = _BoundaryLocalMax - _ParticleRadius; 
    
    if (positionLocal.x < minBound.x) 
    {
        positionLocal.x = minBound.x; 
        velocityLocal.x *= -_CollisionDamping; 
    }
    else if (positionLocal.x > maxBound.x)
    {
        positionLocal.x = maxBound.x; 
        velocityLocal.x *= -_CollisionDamping; 
    }

    if (positionLocal.y < minBound.y)
    {
        positionLocal.y = minBound.y; 
        velocityLocal.y *= -_CollisionDamping; 
    }
    else if (positionLocal.y > maxBound.y)
    {
        positionLocal.y = maxBound.y; 
        velocityLocal.y *= -_CollisionDamping; 
    }

    if (positionLocal.z < minBound.z)
    {
        positionLocal.z = minBound.z; 
        velocityLocal.z *= -_CollisionDamping; 
    }
    else if (positionLocal.z > maxBound.z)
    {
        positionLocal.z = maxBound.z; 
        velocityLocal.z *= -_CollisionDamping; 
    }

    position = mul(_BoundaryLocalToWorld, float4(positionLocal, 1.0)).xyz; 
    velocity = mul((float3x3)_BoundaryLocalToWorld, velocityLocal); 
}

void ApplyIntegration(uint3 id)
{
    if (id.x >= _ParticleCount) return; 
    
    float3 position = _Positions[id.x]; 
    float3 velocity = _Velocities[id.x]; 

    position += velocity * _DeltaTime; 
    // ResolveCollisions(position, velocity); 

    _Positions[id.x] = position; 
    _Velocities[id.x] = velocity; 
}