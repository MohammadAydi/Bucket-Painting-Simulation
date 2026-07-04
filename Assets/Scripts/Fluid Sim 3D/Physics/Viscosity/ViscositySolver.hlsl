void ApplyViscosity(uint3 id)
{
    if (id.x >= _ParticleCount) return;
    
    float3 position = _PredictedPositions[id.x];
    float3 velocity = _Velocities[id.x];
    float density = _Densities[id.x].x;

    float3 viscForce = float3(0.0, 0.0, 0.0);
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
            if (j == id.x) continue;
            
            float3 otherPosition = _PredictedPositions[j];
            float3 offset = otherPosition - position;
            float sqrD = dot(offset, offset);
            
            if (sqrD > sqrH) continue;
            
            float dst = sqrt(sqrD);
            float laplacian = CalculateViscosityWeight(dst, _SmoothingRadius);

            float3 otherVelocity = _Velocities[j];
            float3 velDiff = otherVelocity - velocity;
            float otherDensity = _Densities[j].x;

            viscForce += _Mass * (velDiff / otherDensity) * laplacian;
        }
    }

    velocity += (_ViscosityCoeff * viscForce / density) * _DeltaTime;
    _Velocities[id.x] = velocity;
}