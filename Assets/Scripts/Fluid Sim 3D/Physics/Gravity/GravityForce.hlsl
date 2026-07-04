void ApplyGravity(uint3 id)
{
    if (id.x >= _ParticleCount) return;
    
    float3 velocity = _Velocities[id.x];
    float3 gravityAccel = float3(0.0, _Gravity, 0.0);
    
    // Optional interaction logic goes here
    
    velocity += gravityAccel * _DeltaTime;
    _Velocities[id.x] = velocity;
}