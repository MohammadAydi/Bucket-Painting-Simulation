
float3 ComputeStandardPressureForce(float dst, float3 dir, float pressure, float otherDensity, float otherPressure, float radius, float mass)
{
    float slope = CalculatePressureWeight(dst, radius);
    float sharedPressure = (pressure + otherPressure) * 0.5;
    return dir * (sharedPressure * slope * mass) / otherDensity;
}
float3 ComputeNearPressureForce(float dst, float3 dir, float nearPressure, float otherNearDensity, float otherNearPressure, float radius, float mass)
{
    float safeOtherNearDensity = otherNearDensity;
    float nearSlope = CalculateNearPressureWeight(dst, radius);
    float sharedNearPressure = (nearPressure + otherNearPressure) * 0.5;
    return dir * (sharedNearPressure * nearSlope * mass) / safeOtherNearDensity;
}

float DensityToPressure(float density, float targetDensity, float pressureMultiplier)
{
    return (density - targetDensity) * pressureMultiplier;
}

// Near-pressure has no target/rest value: near-density is purely a measure of
// "too closely packed", so any near-density at all produces a (positive,
// purely repulsive) near-pressure. Used by PRESSURE_MODE_NEAR_PRESSURE to
// stop particle clustering that plain density/pressure alone can let through.
float NearDensityToPressure(float nearDensity, float nearPressureMultiplier)
{
    return nearDensity * nearPressureMultiplier;
}

void ApplyWCSPressure(uint3 id)
{
    if (id.x >= _ParticleCount) return;
    
    float3 position = _PredictedPositions[id.x];
    float3 velocity = _Velocities[id.x];

    float2 selfDensities = _Densities[id.x];
    float density = selfDensities.x;
    float pressure = DensityToPressure(density, _TargetDensity, _PressureMultiplier);
    
    float3 pressForce = float3(0.0, 0.0, 0.0);
    float sqrH = _SmoothingRadius * _SmoothingRadius;
    int3 centreCell = PositionToCellCoord(position, _SmoothingRadius);
    
    [unroll]
    for (int n = 0; n < 27; n++)
    {
        int3 cell = centreCell + CellOffsets[n];
        uint key = GetKey(HashCell(cell), _ParticleCount);
        uint start = SpatialOffsets[key];

        if (start == UINT_MAX) continue;
        
        for (uint j = start; j < _ParticleCount; j++)
        {
            uint cellKey = SpatialKeys[j];
            if (cellKey != key) break;
            
            uint other_idx = j;
            if (other_idx == id.x) continue;
            
            float3 otherPosition = _PredictedPositions[other_idx];
            float3 offset = otherPosition - position;
            float sqrD = dot(offset, offset);
            
            if (sqrD > sqrH) continue;
            
            float dst = sqrt(sqrD);
            float3 dir = (dst > 0.0) ? (offset / dst) : float3(0.0, 1.0, 0.0);

            float otherDensity = _Densities[other_idx].x;
            float otherPressure = DensityToPressure(otherDensity, _TargetDensity, _PressureMultiplier);

            pressForce += ComputeStandardPressureForce(dst, dir, pressure, otherDensity, otherPressure, _SmoothingRadius, _Mass);
        }
    }

    velocity += (pressForce / density) * _DeltaTime;
    _Velocities[id.x] = velocity;
}

void ApplyNearPressure(uint3 id)
{
    if (id.x >= _ParticleCount) return;
    
    float3 position = _PredictedPositions[id.x];
    float3 velocity = _Velocities[id.x];

    float2 selfDensities = _Densities[id.x];
    float density = selfDensities.x;
    float pressure = DensityToPressure(density, _TargetDensity, _PressureMultiplier);

    float nearDensity = selfDensities.y;
    float nearPressure = NearDensityToPressure(nearDensity, _NearPressureMultiplier);
    
    float3 pressForce = float3(0.0, 0.0, 0.0);
    float sqrH = _SmoothingRadius * _SmoothingRadius;
    int3 centreCell = PositionToCellCoord(position, _SmoothingRadius);
    
    [unroll]
    for (int n = 0; n < 27; n++)
    {
        int3 cell = centreCell + CellOffsets[n];
        uint key = GetKey(HashCell(cell), _ParticleCount);
        uint start = SpatialOffsets[key];

        if (start == UINT_MAX) continue;
        
        for (uint j = start; j < _ParticleCount; j++)
        {
            uint cellKey = SpatialKeys[j];
            if (cellKey != key) break;
            
            uint other_idx = j;
            if (other_idx == id.x) continue;
            
            float3 otherPosition = _PredictedPositions[other_idx];
            float3 offset = otherPosition - position;
            float sqrD = dot(offset, offset);
            
            if (sqrD > sqrH) continue;
            
            float dst = sqrt(sqrD);
            float3 dir = (dst > 0.0) ? (offset / dst) : float3(0.0, 1.0, 0.0);

            float2 otherDensities = _Densities[other_idx];
            float otherDensity = otherDensities.x;
            float otherPressure = DensityToPressure(otherDensity, _TargetDensity, _PressureMultiplier);

            float otherNearDensity = otherDensities.y;
            float otherNearPressure = NearDensityToPressure(otherNearDensity, _NearPressureMultiplier);

            pressForce += ComputeStandardPressureForce(dst, dir, pressure, otherDensity, otherPressure, _SmoothingRadius, _Mass);
            pressForce += ComputeNearPressureForce(dst, dir, nearPressure, otherNearDensity, otherNearPressure, _SmoothingRadius, _Mass);
        }
    }

    velocity += (pressForce / density) * _DeltaTime;
    _Velocities[id.x] = velocity;
}