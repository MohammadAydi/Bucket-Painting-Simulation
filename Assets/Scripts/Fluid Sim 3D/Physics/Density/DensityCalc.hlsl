void CalculateDensity(uint3 id)
{
    if (id.x >= _ParticleCount) return;

    float3 pos = _PredictedPositions[id.x];
    float sqrH = _SmoothingRadius * _SmoothingRadius;
    float density = 0.0;
    float nearDensity = 0.0;

    int3 centreCell = PositionToCellCoord(pos, _SmoothingRadius);
    
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
            
            float3 other = _PredictedPositions[j];
            float3 delta = other - pos;
            float sqrD = dot(delta, delta);
            
            if (sqrD <= sqrH)
            {
                float dst = sqrt(sqrD);
                density += _Mass * CalculateDensityWeight(dst, _SmoothingRadius);
                nearDensity += _Mass * CalculateNearDensityWeight(dst, _SmoothingRadius);
            }
        }
    }
    _Densities[id.x] = float2(density, nearDensity);
}