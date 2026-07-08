// using UnityEngine;

// public class MooringLinePBD_Rope : MonoBehaviour
// {
//     [Header("Rope Topology")]
//     [Tooltip("Number of rope segments. Node count = numSegments + 1 (node 0 = pivot/hawsehole).")]
//     public int numSegments = 20;
//     [Tooltip("Used only if pivot/ball are both unassigned or coincide (degenerate placement).")]
//     public float fallbackRopeLength = 5.0f;

//     [Header("Mass Properties")]
//     [Tooltip("Mass of each intermediate rope/chain node (kg).")]
//     public float nodeMass = 0.05f;
//     [Tooltip("Mass of the heavy ball/bucket at the free end, node index = numSegments (kg).")]
//     public float ballMass = 2.0f;

//     [Header("Sub-stepping")]
//     [Tooltip("Number of physics sub-steps per FixedUpdate.")]
//     [Range(1, 20)]
//     public int subSteps = 6;

//     [Header("1. Distance Constraint (Section IV-A)")]
//     [Range(1, 20)] public int distanceIterations = 4;
//     [Tooltip("Stretch stiffness k_stret in [0,1]. 1 = nearly inextensible (real mooring line behavior).")]
//     [Range(0f, 1f)] public float stretchStiffness = 1.0f;
//     [Tooltip("Compression stiffness k_comp in [0,1]. Lower values allow some slack/looseness under compression, as a real cable does.")]
//     [Range(0f, 1f)] public float compressionStiffness = 0;

//     [Header("2. Bending Constraint (Section IV-B)")]
//     public bool enableBending = true;
//     [Range(1, 20)] public int bendingIterations = 2;
//     [Tooltip("Bending stiffness k_bend in [0,1]. The paper uses a very low value (0.02) so the line stays flexible like a real chain/cable.")]
//     [Range(0f, 1f)] public float bendingStiffness = 0.02f;

//     [Header("3. Long Range Attachment (Section IV-C)")]
//     public bool enableLRA = true;
//     [Range(1, 10)] public int lraIterations = 1;

//     [Header("Damping (frame-rate independent)")]
//     [Tooltip("Linear drag coefficient (1/s), applied as continuous decay inside the sub-step loop.")]
//     public float damping = 0.05f;

//     [Header("Anchoring")]
//     [Tooltip("Static transform the line hangs from (the 'hawsehole' / pin constraint target).")]
//     public Transform pivot;
//     [Tooltip("Visual transform for the heavy ball/bucket. Its EDITOR position at Awake() defines the line's initial length and layout.")]
//     public Transform ball;
//     [Header("Spherical Pendulum (initial conditions for the free end)")]
//     [Tooltip("When true, overrides positions[numSegments] and velocities[numSegments] at Awake() " +
//             "using the spherical-coordinate initial conditions below (same convention as " +
//             "SphericalPendulum.cs). When false: original behavior, free end starts at rest.")]
//     public bool useSphericalInit = true;
//     [Tooltip("Initial polar angle theta_0 from the downward vertical (deg).")]
//     public float startTheta = 30f;
//     [Tooltip("Initial azimuthal angle phi_0 (deg).")]
//     public float startPhi = 0f;
//     [Tooltip("Initial polar angular velocity theta'_0 (deg/s).")]
//     public float startThetaDot = 0f;
//     [Tooltip("Initial azimuthal angular velocity phi'_0 (deg/s). 0 = planar swing; non-zero = spherical/conical swing.")]
//     public float startPhiDot = 120f;

//     [Header("Visual Binding - Option A: Pre-authored bones/segments")]
//     public Transform[] ropeSegments;

//     [Header("Visual Binding - Option B: LineRenderer fallback")]
//     public LineRenderer lineRenderer;
//     public float lineWidth = 0.03f;

//     private Vector3[] positions;
//     private Vector3[] predicted;
//     private Vector3[] velocities;
//     private float[] invMass;
//     private float[] mass;

//     private float[] lraMaxDistance;

//     private float[] bendRestAngle;

//     private float totalRopeLength;
//     private float restLengthPerSegment;
//     private bool usingLineRendererFallback;

//     void Awake()
//     {
//         int n = numSegments + 1;
//         positions = new Vector3[n];
//         predicted = new Vector3[n];
//         velocities = new Vector3[n];
//         invMass = new float[n];
//         mass = new float[n];
//         lraMaxDistance = new float[n];
//         bendRestAngle = new float[n];

//         Vector3 pivotPos = (pivot != null) ? pivot.position : transform.position;
//         Vector3 ballPos = (ball != null) ? ball.position : pivotPos + Vector3.down * fallbackRopeLength;

//         totalRopeLength = Vector3.Distance(pivotPos, ballPos);
//         if (totalRopeLength < 1e-4f)
//         {
//             ballPos = pivotPos + Vector3.down * Mathf.Max(fallbackRopeLength, 0.1f);
//             totalRopeLength = Vector3.Distance(pivotPos, ballPos);
//         }

//         restLengthPerSegment = totalRopeLength / numSegments;

//         for (int i = 0; i < n; i++)
//         {
//             float factor = (float)i / numSegments;
//             positions[i] = Vector3.Lerp(pivotPos, ballPos, factor);
//             velocities[i] = Vector3.zero;

//             if (i == 0)
//             {
//                 mass[i] = float.PositiveInfinity;
//                 invMass[i] = 0f;
//             }
//             else if (i == numSegments)
//             {
//                 mass[i] = Mathf.Max(ballMass, 1e-6f);
//                 invMass[i] = 1f / mass[i];
//             }
//             else
//             {
//                 mass[i] = Mathf.Max(nodeMass, 1e-6f);
//                 invMass[i] = 1f / mass[i];
//             }

//             lraMaxDistance[i] = Vector3.Distance(positions[i], pivotPos);
//         }

//         for (int m = 1; m < numSegments; m++)
//         {
//             Vector3 vA = positions[m - 1] - positions[m];
//             Vector3 vB = positions[m + 1] - positions[m];
//             float lenA = vA.magnitude;
//             float lenB = vB.magnitude;
//             if (lenA < 1e-8f || lenB < 1e-8f) { bendRestAngle[m] = Mathf.PI; continue; }

//             float d = Mathf.Clamp(Vector3.Dot(vA / lenA, vB / lenB), -0.9999f, 0.9999f);
//             bendRestAngle[m] = Mathf.Acos(d);
//         }
//         if (useSphericalInit)
//         {
//             float thetaRad    = startTheta    * Mathf.Deg2Rad;
//             float phiRad      = startPhi      * Mathf.Deg2Rad;
//             float thetaDotRad = startThetaDot * Mathf.Deg2Rad;
//             float phiDotRad   = startPhiDot   * Mathf.Deg2Rad;

//             float s  = Mathf.Sin(thetaRad);
//             float c  = Mathf.Cos(thetaRad);
//             float sp = Mathf.Sin(phiRad);
//             float cp = Mathf.Cos(phiRad);

//             Vector3 eR     = new Vector3(s * cp, -c, -s * sp);
//             Vector3 eTheta = new Vector3(c * cp,  s,  -c * sp);
//             Vector3 ePhi   = new Vector3(-sp,     0f, -cp);

//             positions[numSegments] = pivotPos + totalRopeLength * eR;

//             // v = L * ( thetaDot * e_theta + phiDot * sin(theta) * e_phi )
//             velocities[numSegments] =
//                 totalRopeLength * (thetaDotRad * eTheta + phiDotRad * s * ePhi);

//             for (int i = 1; i < numSegments; i++)
//             {
//                 float factor = (float)i / numSegments;
//                 positions[i] = Vector3.Lerp(pivotPos, positions[numSegments], factor);
//             }
//         }

//         SetupVisuals(n);
//     }


//     private void SetupVisuals(int nodeCount)
//     {
//         usingLineRendererFallback = (ropeSegments == null || ropeSegments.Length == 0);

//         if (!usingLineRendererFallback)
//         {
//             if (ropeSegments.Length != numSegments)
//             {
//                 Debug.LogWarning(
//                     $"[MooringLinePBD] ropeSegments length ({ropeSegments.Length}) does not match " +
//                     $"numSegments ({numSegments}). Visual binding will only cover the assigned slots.");
//             }
//             return;
//         }

//         if (lineRenderer == null)
//         {
//             lineRenderer = GetComponent<LineRenderer>();
//             if (lineRenderer == null)
//             {
//                 lineRenderer = gameObject.AddComponent<LineRenderer>();
//             }
//         }

//         if (lineRenderer.sharedMaterial == null)
//         {
//             Shader fallbackShader = Shader.Find("Unlit/Color");
//             if (fallbackShader == null) fallbackShader = Shader.Find("Sprites/Default");
//             Material defaultMat = new Material(fallbackShader);
//             defaultMat.color = Color.white;
//             lineRenderer.material = defaultMat;
//         }

//         lineRenderer.positionCount = nodeCount;
//         lineRenderer.startWidth = lineWidth;
//         lineRenderer.endWidth = lineWidth;
//         lineRenderer.useWorldSpace = true;
//     }

//     void FixedUpdate()
//     {
//         float dt = Time.fixedDeltaTime;
//         if (dt <= 0f) return;

//         float h = dt / subSteps;

//         for (int s = 0; s < subSteps; s++)
//         {
//             SimulateSubstep(h);
//         }

//         UpdateVisuals();
//     }

//     private void SimulateSubstep(float h)
//     {
//         int n = positions.Length;
//         Vector3 pivotPos = (pivot != null) ? pivot.position : positions[0];

//         for (int i = 0; i < n; i++)
//         {
//             if (invMass[i] == 0f)
//             {
//                 predicted[i] = pivotPos;
//                 continue;
//             }

//             velocities[i] += h * Physics.gravity;
//             velocities[i] *= Mathf.Clamp01(1.0f - damping * h);

//             predicted[i] = positions[i] + h * velocities[i];
//         }

//         for (int iter = 0; iter < distanceIterations; iter++)
//         {
//             bool forward = (iter % 2 == 0);
//             if (forward)
//                 for (int c = 0; c < numSegments; c++)
//                     SolveDistanceConstraint(c, c + 1, restLengthPerSegment);
//             else
//                 for (int c = numSegments - 1; c >= 0; c--)
//                     SolveDistanceConstraint(c, c + 1, restLengthPerSegment);
//         }

//         if (enableBending)
//         {
//             for (int iter = 0; iter < bendingIterations; iter++)
//             {
//                 bool forward = (iter % 2 == 0);
//                 if (forward)
//                     for (int m = 1; m < numSegments; m++)
//                         SolveBendingConstraint(m - 1, m, m + 1);
//                 else
//                     for (int m = numSegments - 1; m >= 1; m--)
//                         SolveBendingConstraint(m - 1, m, m + 1);
//             }
//         }

//         if (enableLRA)
//         {
//             for (int iter = 0; iter < lraIterations; iter++)
//             {
//                 for (int i = 1; i < n; i++)
//                 {
//                     SolveLRAConstraint(i, pivotPos);
//                 }
//             }
//         }

//         for (int i = 0; i < n; i++)
//         {
//             if (invMass[i] != 0f)
//             {
//                 velocities[i] = (predicted[i] - positions[i]) / h;
//             }
//             positions[i] = predicted[i];
//         }
//     }

//     private void SolveDistanceConstraint(int i, int j, float restLength)
//     {
//         float wi = invMass[i];
//         float wj = invMass[j];
//         float wSum = wi + wj;
//         if (wSum <= 0f) return;

//         Vector3 delta = predicted[i] - predicted[j];
//         float currentLength = delta.magnitude;
//         if (currentLength < 1e-8f) return;

//         Vector3 n = delta / currentLength;
//         float C = currentLength - restLength;
//         float k = (C > 0f) ? stretchStiffness : compressionStiffness;

//         Vector3 corr = -(C / wSum) * n * k;
//         predicted[i] += wi * corr;
//         predicted[j] -= wj * corr;
//     }

//     private void SolveBendingConstraint(int prevIdx, int midIdx, int nextIdx)
//     {
//         float wMid = invMass[midIdx];
//         float wPrev = invMass[prevIdx];
//         float wNext = invMass[nextIdx];
//         if (wMid + wPrev + wNext <= 0f) return;

//         Vector3 vA = predicted[prevIdx] - predicted[midIdx];
//         Vector3 vB = predicted[nextIdx] - predicted[midIdx];
//         float lenA = vA.magnitude;
//         float lenB = vB.magnitude;
//         if (lenA < 1e-8f || lenB < 1e-8f) return;

//         Vector3 nA = vA / lenA;
//         Vector3 nB = vB / lenB;
//         float d = Mathf.Clamp(Vector3.Dot(nA, nB), -0.9999f, 0.9999f);
//         float invSqrt = 1f / Mathf.Sqrt(1f - d * d);

//         float phi0 = bendRestAngle[midIdx];
//         float C = Mathf.Acos(d) - phi0;

//         Vector3 gPrev = -invSqrt * (1f / lenA) * (nB - d * nA);
//         Vector3 gNext = -invSqrt * (1f / lenB) * (nA - d * nB);
//         Vector3 gMid = -(gPrev + gNext);

//         float denom = wMid * gMid.sqrMagnitude + wPrev * gPrev.sqrMagnitude + wNext * gNext.sqrMagnitude;
//         if (denom < 1e-8f) return;

//         float lambda = -C / denom;

//         predicted[midIdx] += bendingStiffness * wMid * lambda * gMid;
//         predicted[prevIdx] += bendingStiffness * wPrev * lambda * gPrev;
//         predicted[nextIdx] += bendingStiffness * wNext * lambda * gNext;
//     }

//     private void SolveLRAConstraint(int i, Vector3 anchor)
//     {
//         if (invMass[i] == 0f) return;

//         Vector3 toParticle = predicted[i] - anchor;
//         float dist = toParticle.magnitude;
//         float maxDist = lraMaxDistance[i];

//         if (dist > maxDist && dist > 1e-8f)
//         {
//             predicted[i] = anchor + toParticle * (maxDist / dist);
//         }
//     }

//     private void UpdateVisuals()
//     {
//         if (!usingLineRendererFallback)
//         {
//             int count = Mathf.Min(ropeSegments.Length, numSegments);
//             for (int i = 0; i < count; i++)
//             {
//                 if (ropeSegments[i] == null) continue;

//                 ropeSegments[i].position = positions[i];

//                 Vector3 lookDir = positions[i + 1] - positions[i];
//                 if (lookDir.sqrMagnitude > 1e-10f)
//                 {
//                     ropeSegments[i].rotation = Quaternion.LookRotation(lookDir);
//                 }
//             }
//         }
//         else if (lineRenderer != null)
//         {
//             for (int i = 0; i < positions.Length; i++)
//             {
//                 lineRenderer.SetPosition(i, positions[i]);
//             }
//         }

//         if (ball != null)
//         {
//             ball.position = positions[positions.Length - 1];
//         }
//     }

//     void OnDrawGizmosSelected()
//     {
//         if (positions == null) return;
//         Gizmos.color = new Color(0.2f, 0.6f, 1f);
//         for (int i = 0; i < positions.Length; i++)
//         {
//             Gizmos.DrawSphere(positions[i], 0.03f);
//         }
//     }
// }