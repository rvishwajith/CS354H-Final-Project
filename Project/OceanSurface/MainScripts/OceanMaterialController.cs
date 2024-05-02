/// OceanMaterialController.cs
/// Author: Rohith Vishwajith
/// Created 4/26/2024

using UnityEngine;

/// <summary>
/// Initializes the (shared) textures for the ocean surface material. Will also handle the ocean
/// underside in the future.
/// </summary>
public class OceanMaterialController : MonoBehaviour
{
    [SerializeField] OceanSurfaceController oceanController = null;
    [SerializeField] Material sharedOceanMaterial;

    bool TrySetupOceanController()
    {
        if (oceanController != null)
            return true;
        return TryGetComponent(out oceanController);
    }

    void InitializeOceanMaterialTextures()
    {
        // Early exit cases (no material or cascade).
        if (sharedOceanMaterial == null || !TrySetupOceanController())
        {
            Debug.Log("Ocean material or OceanController is null.");
            return;
        }

        // For each cascade, set the textures for displacement, derivatives, and turbulence.
        for (var i = 0; i < oceanController.cascades.Length; i++)
        {
            var cascade = oceanController.cascades[i];
            var suffix = "_c" + i;
            sharedOceanMaterial.SetTexture("_Displacement" + suffix, cascade.Displacement);
            sharedOceanMaterial.SetTexture("_Derivatives" + suffix, cascade.Derivatives);
            sharedOceanMaterial.SetTexture("_Turbulence" + suffix, cascade.Turbulence);
            // Debug.Log("Cascade " + i + ": Set displacement/derivative/turbulence textures.");
        }
    }

    void Start()
    {
        if (sharedOceanMaterial != null && TrySetupOceanController())
        {
            InitializeOceanMaterialTextures();
            return;
        }
        Debug.Log("Ocean material or OceanController is null.");
    }
}