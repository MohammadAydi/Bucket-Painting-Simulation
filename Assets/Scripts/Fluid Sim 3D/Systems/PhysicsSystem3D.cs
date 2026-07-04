// using System;
// using UnityEngine;
// using GPUSorting.Runtime;
// using Unity.Mathematics;
// using static Fluid_Sim_3D.Utilities.ComputeHelper;
// using System.Collections.Generic;
// using Fluid_Sim_3D.Utilities.SpatialHash;

// public sealed class PhysicsSystem3D : IDisposable
// {

    
//     // readonly ComputeShader _oneSweepShader;

//     readonly int _predictPositionsKernel;
//     readonly int _buildSpatialLookupKernel;
//     // readonly int _buildSpatialLookupPredictedKernel;
//     readonly int _reorderKernel;
//     readonly int _reorderCopybackKernel;
//     // readonly int _clearStartIndicesKernel;
//     // readonly int _bitonicSortKernel;
//     // readonly int _buildStartIndicesKernel;
//     // readonly int _calculateExternalForceKernel;
//     readonly int _updateDensitiesKernel;
//     readonly int _calcPressureKernel;
//     readonly int _calcViscosityKernel;
//     readonly int _calcSurfaceTensionKernel;
//     readonly int _integrateKernel;



//     public PhysicsSystem3D()
//     {
      
//     }

//     public void Initialize(ParticleSettings settings, SpawnData3D spawnData, float deltaTime,
//         Vector3 boundsMin,
//         Vector3 boundsMax,
//         Matrix4x4 worldToLocal,
//         Matrix4x4 localToWorld,
//         Vector3 interactionPos,
//         float interactionStrength)
//     {



//         // _sorter =
//         // new OneSweep(
//         // _oneSweepShader,
//         // ParticleCount,
//         // ref _tempKeys,
//         // ref _tempPayload,
//         // ref _tempGlobalHistogram,
//         // ref _tempPassHistogram,
//         // ref _tempIndex
//         // );

//     }

//     public void Simulate(
//       )
//     {
//         if (ParticleCount == 0) return;



//         int realGroups = Mathf.CeilToInt(ParticleCount / (float)ThreadsPerGroup);

//         _compute.Dispatch(_predictPositionsKernel, realGroups, 1, 1);
        
//         // DispatchRadixSort();
//         // DispatchBitonicSort(paddedGroups);
//         // _compute.Dispatch(_clearStartIndicesKernel, realGroups, 1, 1);
//         // _compute.Dispatch(_buildStartIndicesKernel, realGroups, 1, 1);
//         _compute.Dispatch(_updateDensitiesKernel, realGroups, 1, 1);
//         _compute.Dispatch(_calcPressureKernel, realGroups, 1, 1);
//         // _compute.Dispatch(_calculateExternalForceKernel, realGroups, 1, 1);
//         _compute.Dispatch(_calcViscosityKernel, realGroups, 1, 1);
//         _compute.Dispatch(_calcSurfaceTensionKernel, realGroups, 1, 1);
//         _compute.Dispatch(_integrateKernel, realGroups, 1, 1);
//     }

//     public void Dispose() => DisposeBuffers();



//     void BindAllBuffers()
//     {


       

//         // SetBuffers(_compute, _buildSpatialLookupPredictedKernel, bufferNameLookup, new ComputeBuffer[]
//         // {
//         //     spatialHash.SpatialKeys,
//         //     // spatialHash.SpatialOffsets,
//         //     _predictedPositionsBuffer,
//         //     // PositionsBuffer,
//         //     // spatialHash.SpatialIndices,
//         // });

//         // Reorder kernel
        


//         // SetBuffers(_compute, _clearStartIndicesKernel, bufferNameLookup, new ComputeBuffer[]
//         // {
//         //     _startIndicesBuffer,
//         // });


//         // SetBuffers(_compute, _buildStartIndicesKernel, bufferNameLookup, new ComputeBuffer[]
//         // {
//         //     _cellKeysBuffer,
//         //     _startIndicesBuffer,
//         // });

   

 

//         // SetBuffers(_compute, _calculateExternalForceKernel, bufferNameLookup, new ComputeBuffer[]
//         // {
//         //     // _predictedPositionsBuffer,
//         //     PositionsBuffer,
//         //     VelocitiesBuffer,
//         //     _densityBuffer,
//         //     spatialHash.SpatialKeys,
//         //     spatialHash.SpatialOffsets,
//         // });

     

   
//     }

//     // void DispatchRadixSort()
//     // {
//     //     _sorter.Sort(
//     //  ParticleCount,

//     //  _cellKeysBuffer,
//     //  _particleIndicesBuffer,

//     //  _tempKeys,
//     //  _tempPayload,
//     //  _tempGlobalHistogram,
//     //  _tempPassHistogram,
//     //  _tempIndex,

//     //  typeof(uint),
//     //  typeof(uint),

//     //  true);
//     // }

//     // void DispatchBitonicSort(int paddedGroups)
//     // {
//     //     for (int k = 2; k <= _paddedCount; k *= 2)
//     //     {
//     //         for (int j = k / 2; j > 0; j /= 2)
//     //         {
//     //             _compute.SetInt(KId, k);
//     //             _compute.SetInt(JId, j);
//     //             _compute.Dispatch(_bitonicSortKernel, paddedGroups, 1, 1);
//     //         }
//     //     }
//     // }

//     // static int NextPowerOfTwo(int n)
//     // {
//     //     int p = 1;
//     //     while (p < n) p *= 2;
//     //     return p;
//     // }
// }