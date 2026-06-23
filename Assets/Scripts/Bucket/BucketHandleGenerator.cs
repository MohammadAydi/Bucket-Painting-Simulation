using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class BucketHandleGenerator : MonoBehaviour
{
    [Header("Connected Bucket")]
    [SerializeField] private BucketGenerator bucketGenerator;

    [Header("Handle Dimensions")]
    [Range(0.02f, 0.2f)] public float handleWidth = 0.05f;       // عرض شريط القوس
    [Range(0.01f, 0.1f)] public float handleThickness = 0.02f;   // سماكة شريط القوس
    [Range(8, 64)] public int handleSegments = 32;               // نعومة إنحناء القوس
    public float clearance = 0.05f;                              // مسافة أمان صغيرة لكي لا يلتصق القوس بحافة الدلو تماماً

    private MeshFilter meshFilter;
    private Mesh handleMesh;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
    }

    private void Start()
    {
        if (bucketGenerator == null)
        {
            bucketGenerator = GetComponentInParent<BucketGenerator>();
        }
        GenerateHandle();
    }

    // استدعاء تلقائي عند تعديل القيم من الـ Inspector في وقت المحرر أو اللعب
    public void OnValidate()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        GenerateHandle();
    }

    public void GenerateHandle()
    {
        if (bucketGenerator == null) return;

        if (handleMesh == null)
        {
            handleMesh = new Mesh();
            handleMesh.name = "BucketHandle_Mesh";
            meshFilter.mesh = handleMesh;
        }
        else
        {
            handleMesh.Clear();
        }

        // جلب أبعاد الدلو الحالية ديناميكياً
        float R = bucketGenerator.topRadius + clearance;
        float h = bucketGenerator.height;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        // توليد المقاطع العرضية على مسار نصف دائري (من زاوية 0 إلى 180 درجة)
        for (int i = 0; i <= handleSegments; i++)
        {
            float t = (float)i / handleSegments;
            float theta = t * Mathf.PI; // بالراديان

            // حساب مركز النقطة الحالية على القوس (بالنسبة لمركز الدلو السفلي 0,0,0)
            // القوس يمتد على المحور X ويرتفع على المحور Y، وثابت على Z
            float x = Mathf.Cos(theta) * R;
            float y = h + Mathf.Sin(theta) * R;
            Vector3 centerPos = new Vector3(x, y, 0f);

            // حساب اتجاه المماس (Tangent) لمعرفة زاوية المقطع العرضي
            Vector3 tangent = new Vector3(-Mathf.Sin(theta), Mathf.Cos(theta), 0f).normalized;
            // الاتجاه العمودي (Z-Forward)
            Vector3 forward = Vector3.forward;
            // الاتجاه الجانبي العمودي على المماس في مستوي القوس
            Vector3 normal = Vector3.Cross(tangent, forward).normalized;

            // توليد 4 نقاط للمقطع العرضي (مستطيل حول نقطة المركز centerPos)
            float halfW = handleWidth / 2f;
            float halfT = handleThickness / 2f;

            // 4 نقاط تشكل المربع/المستطيل للمقطع الحالي
            Vector3 v0 = centerPos + (forward * halfW) + (normal * halfT);
            Vector3 v1 = centerPos + (forward * halfW) - (normal * halfT);
            Vector3 v2 = centerPos - (forward * halfW) - (normal * halfT);
            Vector3 v3 = centerPos - (forward * halfW) + (normal * halfT);

            vertices.Add(v0);
            vertices.Add(v1);
            vertices.Add(v2);
            vertices.Add(v3);

            // ربط المقطع الحالي بالمقطع السابق باستخدام المثلثات (Triangles)
            if (i > 0)
            {
                int curr = i * 4;
                int prev = (i - 1) * 4;

                // الوجه العلوي (Top Face)
                triangles.Add(prev + 0); triangles.Add(curr + 0); triangles.Add(prev + 3);
                triangles.Add(prev + 3); triangles.Add(curr + 0); triangles.Add(curr + 3);

                // الوجه السفلي (Bottom Face)
                triangles.Add(prev + 1); triangles.Add(prev + 2); triangles.Add(curr + 1);
                triangles.Add(curr + 1); triangles.Add(prev + 2); triangles.Add(curr + 2);

                // الوجه الأمامي (Front Face)
                triangles.Add(prev + 0); triangles.Add(prev + 1); triangles.Add(curr + 0);
                triangles.Add(curr + 0); triangles.Add(prev + 1); triangles.Add(curr + 1);

                // الوجه الخلفي (Back Face)
                triangles.Add(prev + 2); triangles.Add(prev + 3); triangles.Add(curr + 2);
                triangles.Add(curr + 2); triangles.Add(prev + 3); triangles.Add(curr + 3);
            }
        }

        // إغلاق غطاء البداية (Start Cap) عند زاوية 0
        triangles.Add(0); triangles.Add(3); triangles.Add(1);
        triangles.Add(1); triangles.Add(3); triangles.Add(2);

        // إغلاق غطاء النهاية (End Cap) عند زاوية 180
        int lastCap = handleSegments * 4;
        triangles.Add(lastCap + 0); triangles.Add(lastCap + 1); triangles.Add(lastCap + 3);
        triangles.Add(lastCap + 1); triangles.Add(lastCap + 2); triangles.Add(lastCap + 3);

        // تعيين البيانات للـ Mesh وإعادة الحساب للتنعيم والإضاءة
        handleMesh.vertices = vertices.ToArray();
        handleMesh.triangles = triangles.ToArray();
        handleMesh.RecalculateNormals();
        handleMesh.RecalculateBounds();
    }
}