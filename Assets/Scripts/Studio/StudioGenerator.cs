using UnityEngine;

public class StudioGenerator : MonoBehaviour
{
    [Header("Studio Dimensions")]
    public float roomWidth = 16f;
    public float roomHeight = 8f;
    public float roomDepth = 16f;
    public float wallThickness = 0.2f;

    [Header("Materials")]
    public Material floorMaterial;
    public Material wallMaterial;
    public Material ceilingMaterial;

    [Header("Lighting Settings")]
    public Color ambientColor = new Color(0.15f, 0.15f, 0.15f);
    public Color studioLightColor = new Color(1f, 0.95f, 0.85f); 
    public float studioLightIntensity = 2.0f;

    private GameObject floor;
    private GameObject ceiling;
    private GameObject frontWall;
    private GameObject backWall;
    private GameObject leftWall;
    private GameObject rightWall;
    private GameObject overheadLight;

    private void Start()
    {
        GenerateStudio();
        SetupLighting();
    }

    [ContextMenu("Generate Studio")]
    public void GenerateStudio()
    {
        ClearStudio();

        // 1. Create Floor (Centered at bottom)
        floor = CreateStudioElement("Floor", new Vector3(roomWidth, wallThickness, roomDepth), new Vector3(0, -wallThickness / 2f, 0), floorMaterial);

        // 2. Create Ceiling (Centered at top)
        ceiling = CreateStudioElement("Ceiling", new Vector3(roomWidth, wallThickness, roomDepth), new Vector3(0, roomHeight + (wallThickness / 2f), 0), ceilingMaterial);

        // 3. Create 4 Surrounding Walls
        frontWall = CreateStudioElement("FrontWall", new Vector3(roomWidth, roomHeight, wallThickness), new Vector3(0, roomHeight / 2f, roomDepth / 2f), wallMaterial);
        backWall = CreateStudioElement("BackWall", new Vector3(roomWidth, roomHeight, wallThickness), new Vector3(0, roomHeight / 2f, -roomDepth / 2f), wallMaterial);
        leftWall = CreateStudioElement("LeftWall", new Vector3(wallThickness, roomHeight, roomDepth), new Vector3(-roomWidth / 2f, roomHeight / 2f, 0), wallMaterial);
        rightWall = CreateStudioElement("RightWall", new Vector3(wallThickness, roomHeight, roomDepth), new Vector3(roomWidth / 2f, roomHeight / 2f, 0), wallMaterial);
    }

    private GameObject CreateStudioElement(string elementName, Vector3 scale, Vector3 position, Material mat)
    {
        GameObject element = new GameObject(elementName);
        element.transform.parent = this.transform;
        element.transform.localPosition = position;
        element.transform.localScale = scale;

        MeshFilter meshFilter = element.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = element.AddComponent<MeshRenderer>();

        meshFilter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

        if (mat != null)
        {
            meshRenderer.material = mat;
        }

        return element;
    }

    private void SetupLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;

        if (overheadLight == null)
        {
            overheadLight = new GameObject("Studio_Overhead_Light");
            overheadLight.transform.parent = this.transform;
            overheadLight.transform.localPosition = new Vector3(0, roomHeight - 0.5f, 0);
            overheadLight.transform.localRotation = Quaternion.Euler(90f, 0, 0); 

            Light lightComponent = overheadLight.AddComponent<Light>();
            lightComponent.type = LightType.Spot;
            lightComponent.spotAngle = 75f;
            lightComponent.range = roomHeight * 1.8f;
            lightComponent.color = studioLightColor;
            lightComponent.intensity = studioLightIntensity;
            lightComponent.shadows = LightShadows.Soft;
        }
    }

    public void ClearStudio()
    {
        while (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }
        overheadLight = null;
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            // UnityEditor.EditorApplication.delayCall += () => {
            //     if (this != null) GenerateStudio();
            // };
        }
    }
}