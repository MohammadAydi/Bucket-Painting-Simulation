using UnityEngine;

namespace onlyone
{
    [DefaultExecutionOrder(-10)] 
    public class HandleMidpointTracker : MonoBehaviour
    {
        [Header("المراجع")]
        [SerializeField] private BucketHandleGenerator handleGenerator;
        [SerializeField] private BucketGenerator bucketGenerator; 

        private void LateUpdate()
        {
            if (!handleGenerator || !bucketGenerator) return;
            transform.position = GetHandleMidpoint();
        }

        private Vector3 GetHandleMidpoint()
        {
            float r = bucketGenerator.topRadius + handleGenerator.clearance;
            float h = bucketGenerator.height;

            Vector3 localMid = new Vector3(0f, h + r, 0f);
            return handleGenerator.transform.TransformPoint(localMid);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (handleGenerator == null || bucketGenerator == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(GetHandleMidpoint(), 0.02f);
        }
#endif
    }
}