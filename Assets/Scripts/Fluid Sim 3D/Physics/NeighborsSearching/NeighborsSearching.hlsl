static const uint hashK1 = 15823;
static const uint hashK2 = 9737333;
static const uint hashK3 = 440817757;

int3 PositionToCellCoord(float3 pos, float radius) { return (int3)(floor(pos / radius)); }
uint GetKey(uint hash, uint tableSize) { return hash % tableSize; }

uint HashCell(int3 cell)
{
    const uint blockSize = 50;
    uint3 ucell = (uint3)(cell + blockSize / 2);
    uint3 localCell = ucell % blockSize;
    uint3 blockID = ucell / blockSize;
    uint blockHash = blockID.x * hashK1 + blockID.y * hashK2 + blockID.z * hashK3;
    return localCell.x + blockSize * (localCell.y + blockSize * localCell.z) + blockHash;
}


void ReorderImpl(uint3 id)
{
    if (id.x >= _ParticleCount) return;
    uint sortedIndex = SortedIndices[id.x];
    SortTarget_Positions[id.x] = _Positions[sortedIndex];
    SortTarget_PredictedPositions[id.x] = _PredictedPositions[sortedIndex];
    SortTarget_Velocities[id.x] = _Velocities[sortedIndex];
}

void ReorderCopyBackImpl(uint3 id)
{
    if (id.x >= _ParticleCount) return;
    _Positions[id.x] = SortTarget_Positions[id.x];
    _PredictedPositions[id.x] = SortTarget_PredictedPositions[id.x];
    _Velocities[id.x] = SortTarget_Velocities[id.x];
}

void BuildSpatialLookupImpl(uint3 id)
{
    if (id.x >= _ParticleCount) return;
    
    uint index = id.x;
    int3 cell = PositionToCellCoord(_PredictedPositions[index], _SmoothingRadius);
    uint hash = HashCell(cell);
    uint key = GetKey(hash, _ParticleCount);
    SpatialKeys[id.x] = key;
}
