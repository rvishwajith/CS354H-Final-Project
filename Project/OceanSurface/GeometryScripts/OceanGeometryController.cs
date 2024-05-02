using System.Collections.Generic;
using UnityEngine;

class OceanGeometryElement
{
    public Transform transform;
    public MeshRenderer meshRenderer;

    public OceanGeometryElement(Transform transform, MeshRenderer meshRenderer)
    {
        this.transform = transform;
        this.meshRenderer = meshRenderer;
    }
}

public class OceanGeometryController : MonoBehaviour
{
    [SerializeField] OceanSurfaceController oceanController;
    [SerializeField] Transform viewer;
    [SerializeField] Material oceanMaterial;
    [SerializeField] bool updateMaterialProperties;
    [SerializeField] bool showMaterialLods = false;

    [SerializeField] float lengthScale = 10;
    [SerializeField, Range(1, 40)] int vertexDensity = 30;
    [SerializeField, Range(0, 8)] int clipLevels = 8;
    [SerializeField, Range(0, 100)] float skirtSize = 50;

    // Ocean Geometry Fields ---------------------------------------------------------------------
    OceanGeometryElement center;
    OceanGeometryElement skirt;
    List<OceanGeometryElement> rings = new();
    List<OceanGeometryElement> trims = new();
    Quaternion[] trimRotations;
    int previousVertexDensity;
    float previousSkirtSize;

    public int GridSize { get { return 4 * vertexDensity + 1; } }

    Material[] materials;

    private void Start()
    {
        if (viewer == null)
            viewer = Camera.main.transform;

        void SetMaterialTextures()
        {
            // For each cascade, set the textures for displacement, derivatives, and turbulence.
            for (var i = 0; i < oceanController.cascades.Length; i++)
            {
                var cascade = oceanController.cascades[i];
                var suffix = "_c" + i;
                oceanMaterial.SetTexture("_Displacement" + suffix, cascade.Displacement);
                oceanMaterial.SetTexture("_Derivatives" + suffix, cascade.Derivatives);
                oceanMaterial.SetTexture("_Turbulence" + suffix, cascade.Turbulence);
            }
        }

        SetMaterialTextures();

        void InitializeLODMaterials()
        {
            materials = new Material[3];
            materials[0] = new Material(oceanMaterial);
            materials[0].EnableKeyword("CLOSE");

            materials[1] = new Material(oceanMaterial);
            materials[1].EnableKeyword("MID");
            materials[1].DisableKeyword("CLOSE");

            materials[2] = new Material(oceanMaterial);
            materials[2].DisableKeyword("MID");
            materials[2].DisableKeyword("CLOSE");
        }

        InitializeLODMaterials();

        trimRotations = new Quaternion[]
        {
            Quaternion.AngleAxis(180, Vector3.up),
            Quaternion.AngleAxis(90, Vector3.up),
            Quaternion.AngleAxis(270, Vector3.up),
            Quaternion.identity,
        };
        InstantiateMeshes();
    }

    private void Update()
    {
        if (rings.Count != clipLevels || trims.Count != clipLevels
            || previousVertexDensity != vertexDensity || !Mathf.Approximately(previousSkirtSize, skirtSize))
        {
            InstantiateMeshes();
            previousVertexDensity = vertexDensity;
            previousSkirtSize = skirtSize;
        }

        // if (Time.time > 1f) return;
        UpdatePositions();
        UpdateMaterials();
    }

    void UpdateMaterials()
    {
        if (updateMaterialProperties && !showMaterialLods)
        {
            for (int i = 0; i < 3; i++)
            {
                materials[i].CopyPropertiesFromMaterial(oceanMaterial);
            }
            materials[0].EnableKeyword("CLOSE");
            materials[1].EnableKeyword("MID");
            materials[1].DisableKeyword("CLOSE");
            materials[2].DisableKeyword("MID");
            materials[2].DisableKeyword("CLOSE");
        }
        if (showMaterialLods)
        {
            materials[0].SetColor("_Color", Color.red * 0.6f);
            materials[1].SetColor("_Color", Color.green * 0.6f);
            materials[2].SetColor("_Color", Color.blue * 0.6f);
        }

        int activeLevels = ActiveLodlevels();
        center.meshRenderer.material = GetMaterial(clipLevels - activeLevels - 1);

        for (int i = 0; i < rings.Count; i++)
        {
            rings[i].meshRenderer.material = GetMaterial(clipLevels - activeLevels + i);
            trims[i].meshRenderer.material = GetMaterial(clipLevels - activeLevels + i);
        }
    }

    Material GetMaterial(int lodLevel)
    {
        if (lodLevel - 2 <= 0)
            return materials[0];
        if (lodLevel - 2 <= 2)
            return materials[1];
        return materials[2];
    }

    void UpdatePositions()
    {
        int k = GridSize;
        int activeLevels = ActiveLodlevels();

        float scale = ClipLevelScale(-1, activeLevels);
        Vector3 prevSnappedPos = Snap(viewer.position, scale * 2);
        center.transform.position = prevSnappedPos + OffsetFromCenter(-1, activeLevels);
        center.transform.localScale = new Vector3(scale, 1, scale);

        for (int i = 0; i < clipLevels; i++)
        {
            rings[i].transform.gameObject.SetActive(i < activeLevels);
            trims[i].transform.gameObject.SetActive(i < activeLevels);
            if (i >= activeLevels) continue;

            scale = ClipLevelScale(i, activeLevels);
            Vector3 centerOffset = OffsetFromCenter(i, activeLevels);
            Vector3 snappedPosition = Snap(viewer.position, scale * 2);

            Vector3 trimPosition = centerOffset + snappedPosition + scale * (k - 1) / 2 * new Vector3(1, 0, 1);
            int shiftX = prevSnappedPos.x - snappedPosition.x < float.Epsilon ? 1 : 0;
            int shiftZ = prevSnappedPos.z - snappedPosition.z < float.Epsilon ? 1 : 0;
            trimPosition += shiftX * (k + 1) * scale * Vector3.right;
            trimPosition += shiftZ * (k + 1) * scale * Vector3.forward;
            trims[i].transform.position = trimPosition;
            trims[i].transform.rotation = trimRotations[shiftX + 2 * shiftZ];
            trims[i].transform.localScale = new Vector3(scale, 1, scale);

            rings[i].transform.position = snappedPosition + centerOffset;
            rings[i].transform.localScale = new Vector3(scale, 1, scale);
            prevSnappedPos = snappedPosition;
        }

        scale = lengthScale * 2 * Mathf.Pow(2, clipLevels);
        skirt.transform.position = new Vector3(-1, 0, -1) * scale * (skirtSize + 0.5f - 0.5f / GridSize) + prevSnappedPos;
        skirt.transform.localScale = new Vector3(scale, 1, scale);
    }

    int ActiveLodlevels()
    {
        return clipLevels - Mathf.Clamp((int)Mathf.Log((1.7f * Mathf.Abs(viewer.position.y) + 1) / lengthScale, 2), 0, clipLevels);
    }

    float ClipLevelScale(int level, int activeLevels)
    {
        return lengthScale / GridSize * Mathf.Pow(2, clipLevels - activeLevels + level + 1);
    }

    Vector3 OffsetFromCenter(int level, int activeLevels)
    {
        return (Mathf.Pow(2, clipLevels) + GeometricProgressionSum(2, 2, clipLevels - activeLevels + level + 1, clipLevels - 1))
               * lengthScale / GridSize * (GridSize - 1) / 2 * new Vector3(-1, 0, -1);
    }

    float GeometricProgressionSum(float b0, float q, int n1, int n2)
    {
        return b0 / (1 - q) * (Mathf.Pow(q, n2) - Mathf.Pow(q, n1));
    }

    Vector3 Snap(Vector3 coords, float scale)
    {
        if (coords.x >= 0)
            coords.x = Mathf.Floor(coords.x / scale) * scale;
        else
            coords.x = Mathf.Ceil((coords.x - scale + 1) / scale) * scale;

        if (coords.z < 0)
            coords.z = Mathf.Floor(coords.z / scale) * scale;
        else
            coords.z = Mathf.Ceil((coords.z - scale + 1) / scale) * scale;

        coords.y = 0;
        return coords;
    }

    void InstantiateMeshes()
    {
        foreach (var child in gameObject.GetComponentsInChildren<Transform>())
        {
            if (child != transform)
                Destroy(child.gameObject);
        }
        rings.Clear();
        trims.Clear();

        center = InstantiateElement("Center", OceanGeometryGenerator.CreatePlaneMesh(
            2 * GridSize, 2 * GridSize, 1, Seams.All),
            materials[materials.Length - 1]);
        var ring = OceanGeometryGenerator.CreateRingMesh(GridSize, 1);
        var trim = OceanGeometryGenerator.CreateTrimMesh(GridSize, 1);
        for (int i = 0; i < clipLevels; i++)
        {
            rings.Add(InstantiateElement("Ring " + i, ring, materials[materials.Length - 1]));
            trims.Add(InstantiateElement("Trim " + i, trim, materials[materials.Length - 1]));
        }
        var skirtMesh = OceanGeometryGenerator.CreateSkirtMesh(GridSize, skirtSize);
        var skirtMat = materials[^1];
        skirt = InstantiateElement("Skirt", skirtMesh, skirtMat);
    }

    OceanGeometryElement InstantiateElement(string name, Mesh mesh, Material mat)
    {
        GameObject obj = new GameObject();
        obj.name = name;
        obj.transform.SetParent(transform);
        obj.transform.localPosition = Vector3.zero;
        var meshFilter = obj.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        var meshRenderer = obj.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = true;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
        meshRenderer.material = mat;
        meshRenderer.allowOcclusionWhenDynamic = false;
        return new(obj.transform, meshRenderer);
    }
}

[System.Flags]
public enum Seams
{
    None = 0,
    Left = 1,
    Right = 2,
    Top = 4,
    Bottom = 8,
    All = Left | Right | Top | Bottom
};