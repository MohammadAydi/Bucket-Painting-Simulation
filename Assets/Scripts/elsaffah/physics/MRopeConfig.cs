// using System;
// using UnityEngine;

// namespace MyPhysics
// {
//     [Serializable]
//     public class MRopeConfig
//     {
//         [Header("Identity")]
//         public string name = "Preset";
//         public string description = "";

//         [Header("Shared")]
//         public float gravity = 9.81f;
//         public float fixedStep = 0.004f;
//         public float bucketMass = 3f;
//         public float bucketRadius = 0.13f;
//         public float airDensity = 1.225f;
//         public float bucketDragCoefficient = 1.0f;

//         [Header("Pendulum (شروط البداية)")]
//         public float length = 1.2f;
//         public float startTheta = 32f;
//         public float startPhi;
//         public float startThetaDot;
//         public float startPhiDot;
//         public float pivotFriction = 0.02f;

//         [Header("Rope")]
//         public int segments = 26;
//         public float ropeLength = 1.22f;
//         public float ropeWidth = 0.008f;
//         public float linearDensity = 0.1f;
//         public int substeps = 4;
//         public int constraintIterations = 8;
//         public float compliance = 0.00004f;
//         public float internalDampingRate = 0.05f;
//         public float stretchLimit = 1.2f;
//         public bool enableBending = true;
//         public float bendingStiffness = 0.02f;
//         public Vector3 wind = Vector3.zero;
//         public bool dynamicBucket = true;

//         [Header("Torsion")]
//         public bool enableTorsion = true;
//         public float initialTurns = 8f;
//         public float torsionalStiffness = 25f;
//         public float dampingRatio = 0.10f;
//         public int torsionIterations = 8;
//         public bool clampPivotTwist = true;
//         public float maxTwistRate = 600f;
//         public float stretchDampingRatio;

//         [Header("Material Properties")]
//         public string materialName = "Braided Nylon";
//         public float density = 1140f;
//         public float youngModulus = 3e8f;
//         public float poissonRatio = 0.4f;
//         public bool deriveFromMaterial = true;
//         public bool breakable = true;
//         public float ropeRadius = 0.004f;
//         public float ultimateStress = 6e7f;
//         public float safetyFactor = 1.2f;
//         public float breakingStrain;
//         public bool allowPlasticFailure;
//         public float yieldStressRatio = 0.7f;
//     }
// }