using Fluid_Sim_3D.Utilities;
using UnityEngine;

namespace Fluid_Sim_3D.Utilities.SpatialHash.Offsets
{
	public class SpatialOffsetCalculator
	{
		readonly ComputeShader cs = ComputeHelper.LoadComputeShader("SpatialOffsets");
		static readonly int NumInputs = Shader.PropertyToID("numInputs");
		static readonly int Offsets = Shader.PropertyToID("Offsets");
		static readonly int SortedKeys = Shader.PropertyToID("SortedKeys");

		const int initKernel = 0;
		const int offsetsKernel = 1;


		// needsInit: Set to true if offsets buffer has not already been initialized with values >= its length.
		public void Run(bool needsInit, ComputeBuffer sortedKeys, ComputeBuffer offsets)
		{
			if (sortedKeys.count != offsets.count) throw new System.Exception("Count mismatch");
			cs.SetInt(NumInputs, sortedKeys.count);

			if (needsInit)
			{
				cs.SetBuffer(initKernel, Offsets, offsets);
				global::J_Fluid_Sim_2D.Utilities.ComputeHelper.Dispatch(cs, sortedKeys.count, kernelIndex: initKernel);
			}

			cs.SetBuffer(offsetsKernel, Offsets, offsets);
			cs.SetBuffer(offsetsKernel, SortedKeys, sortedKeys);
			global::J_Fluid_Sim_2D.Utilities.ComputeHelper.Dispatch(cs, sortedKeys.count, kernelIndex: offsetsKernel);
		}
	}
}