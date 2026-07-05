void PredictPositions(uint3 id)
{
    if (id.x >= _ParticleCount) return;
    
    float3 position = _Positions[id.x];
    float3 velocity = _Velocities[id.x];
    
    // Using 1/120 as a stable prediction step
    float3 predictedPosition = position + velocity * 1 / 120.0;
    _PredictedPositions[id.x] = predictedPosition;
}