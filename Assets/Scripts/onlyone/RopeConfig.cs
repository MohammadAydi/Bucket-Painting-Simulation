using System;
using UnityEngine;

namespace onlyone
{ 
    [Serializable]
    public class RopeConfig
    {
        [Header("Identity")]
        public string name = "Preset";
        public string description = "";

        [Header("Shared (تُوزَّع على النواس والحبل للاتساق)")]
        public float gravity = 9.81f;
        public float fixedStep = 0.004f;
        public float bucketMass = 3f;           // = pendulum.mass = rope.bucketMass
        public float bucketRadius = 0.13f;      // = pendulum.bucketRadius = rope.bucketRadius
        public float airDensity = 1.225f;
        public float bucketDragCoefficient = 1.0f;

        [Header("Pendulum (النواس - شروط البداية)")]
        public float length = 1.2f;
        public float startTheta = 32f;           
        public float startPhi = 0f;             
        public float startThetaDot = 0f;        
        public float startPhiDot = 0f;           
        public float pivotFriction = 0.02f;

        [Header("Rope (الحبل)")]
        public int   segments = 24;
        public float ropeLength = 1.22f;
        public float ropeWidth = 0.008f;
        public float linearDensity = 0.08f;
        public int   constraintIterations = 45;
        public float compliance = 0.00005f;   
        public float damping = 0.9998f;        
        public float stretchLimit = 1.2f;
        public Vector3 wind = Vector3.zero;
        public bool  dynamicBucket = true;

        [Header("Torsion (الالتواء)")]
        public bool  enableTorsion = true;
        public float torsionalStiffness = 40f;
        public float torsionalDamping = 0.985f;
        public float torsionRadius = 0.01f;
        public int   torsionIterations = 8;
        public bool  clampPivotTwist = true;
        public float maxTwistRate = 200f; 
    }
}
