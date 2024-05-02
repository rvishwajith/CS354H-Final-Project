using UnityEngine;

public class OceanLODController : MonoBehaviour
{
    [SerializeField] LODGroup lodGroup = null;
    [SerializeField] Material oceanMaterial = null;
    [SerializeField] int currentLODLevel = -1;

    /// <summary>
    /// Get the current LOD level from the LOD Group component, assuming it exists.
    /// </summary>
    /// <returns>Current LOD level from LOD Group, or -1 if any errors occur.</returns>
    public int GetCurrentLODLevel()
    {
        if (lodGroup == null)
            return -1;
        var lods = lodGroup.GetLODs();
        for (int i = 0; i < lods.Length; i++)
        {
            var lod = lods[i];
            if (lod.renderers.Length > 0 && lod.renderers[0].isVisible)
                return i;
        }
        return -1;
    }

    public Material GetCurrentLODMaterial()
    {
        return lodGroup.GetLODs()[currentLODLevel].renderers[0].material;
    }

    void Start() { UpdateLODLevels(); }

    /// <summary>
    /// Update the LOD level for the ocean shader based on the LOD level of each group.
    /// TODO: For some reason, this is very slow (halfs FPS) and causes a 100ms GC.Collect()
    /// call every few seconds.
    /// </summary>
    void UpdateLODLevels()
    {
        currentLODLevel = GetCurrentLODLevel();
        if (currentLODLevel == -1)
            return;
        var material = lodGroup.GetLODs()[currentLODLevel].renderers[0].material;
        if (currentLODLevel <= 1)
        {
            material.EnableKeyword("CLOSE");
            material.DisableKeyword("MID");
        }
        else if (currentLODLevel <= 4)
        {
            material.DisableKeyword("CLOSE");
            material.EnableKeyword("MID");
        }
        else
        {
            material.DisableKeyword("CLOSE");
            material.DisableKeyword("MID");
        }
    }
}