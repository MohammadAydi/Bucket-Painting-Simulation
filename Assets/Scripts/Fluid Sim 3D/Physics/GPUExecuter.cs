using System;
using System.Collections.Generic;
using UnityEngine;

abstract class GPUExecuter
{

    protected FluidModel _model;
    protected readonly ComputeShader _compute;
    protected readonly int _kernel;
    protected int realGroups;


    public GPUExecuter(FluidModel model, ComputeShader compute, string kernelName)
    {
        _model = model;
        _compute = compute;
        _kernel = _compute.FindKernel(kernelName);
        realGroups = Mathf.CeilToInt(_model.ParticleCount / (float)Config.ThreadsPerGroup);

    }

    abstract public void BindsBuffers();

    public void Dispatch()
    {
        _compute.Dispatch(_kernel, realGroups, 1, 1);
    }
}