void ApplySurfaceTension(uint3 id)
{
    if (id.x >= _ParticleCount) return;
    
    float3 position = _PredictedPositions[id.x];
    float3 velocity = _Velocities[id.x];
    float density = _Densities[id.x].x;

    float sqrH = _SmoothingRadius * _SmoothingRadius;
    float h = _SmoothingRadius;

    float3 colorFieldGradient = float3(0.0, 0.0, 0.0);
    float colorFieldLaplacian = 0.0;
    int3 centreCell = PositionToCellCoord(position, h);
    
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

            float otherDensity = _Densities[j].x;
            float mOverRho = _Mass / otherDensity;
            float3 dir = (dst > 0.0001) ? (offset / dst) : float3(0.0, 1.0, 0.0);
            
            float gradScalar = CalculateSurfaceTensionGradientWeight(dst, sqrD, sqrH, h);
            colorFieldGradient += mOverRho * gradScalar * dir;

            float lapScalar = CalculateSurfaceTensionLaplacianWeight(sqrD, sqrH, h);
            colorFieldLaplacian += mOverRho * lapScalar;
        }
    }

    float normalMag = length(colorFieldGradient);
    if (normalMag <= _SurfaceTensionThreshold) return;

    float3 surfaceNormal = colorFieldGradient / normalMag;
    float3 surfaceForce = -_SurfaceTensionCoeff * colorFieldLaplacian * surfaceNormal;

    velocity += (surfaceForce / density) * _DeltaTime;
    _Velocities[id.x] = velocity;
}