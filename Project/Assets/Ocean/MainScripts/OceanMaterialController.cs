/// OceanMaterialController.cs
/// Author: Rohith Vishwajith
/// Created 4/30/2024

using UnityEngine;

/// <summary>
/// Initializes the (shared) textures for the ocean surface material. Will also handle the ocean
/// underside in the future.
/// </summary>
public class OceanMaterialController : MonoBehaviour
{
    [SerializeField] OceanSurfaceController oceanSurfaceController = null;
    [SerializeField] Material sharedOceanSurfaceMaterial;

    bool TrySetupOceanController()
    {
        if (oceanSurfaceController != null)
            return true;
        return TryGetComponent(out oceanSurfaceController);
    }

    void InitializeOceanMaterialTextures()
    {
        // Early exit cases (no material or cascade).
        if (sharedOceanSurfaceMaterial == null || !TrySetupOceanController())
        {
            Debug.Log("Ocean surface material or OceanSurfaceController is null.");
            return;
        }

        // For each cascade, set the textures for displacement, derivatives, and turbulence.
        for (var i = 0; i < oceanSurfaceController.cascades.Length; i++)
        {
            var cascade = oceanSurfaceController.cascades[i];
            var suffix = "_c" + i;
            sharedOceanSurfaceMaterial.SetTexture("_Displacement" + suffix, cascade.Displacement);
            sharedOceanSurfaceMaterial.SetTexture("_Derivatives" + suffix, cascade.Derivatives);
            sharedOceanSurfaceMaterial.SetTexture("_Turbulence" + suffix, cascade.Turbulence);
            // Debug.Log("Cascade " + i + ": Set displacement/derivative/turbulence textures.");
        }
    }

    void Start()
    {
        if (sharedOceanSurfaceMaterial != null && TrySetupOceanController())
        {
            InitializeOceanMaterialTextures();
            return;
        }
        Debug.Log("Ocean material or OceanSurfaceController is null.");
    }
}