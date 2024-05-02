/// OceanTextureGenerator.cs
/// Author: Rohith Vishwajith
/// Created 4/26/2024

using UnityEngine;
using UnityEditor;
using Unity.Burst;
using Unity.Jobs;

public static class OceanTextureGenerator
{
    /// <summary>
    /// Create a render texture with the given size, format, and mipmap settings.
    /// </summary>
    /// <param name="size">The pixel dimensions of the texture.</param>
    /// <param name="format">The format of the texture (RGFloat by default).</param>
    /// <param name="useMips">Whether the texture should use mipMaps (false by default).</param>
    /// <returns>The generated RenderTexture.</returns>
    // [BurstCompile]
    public static RenderTexture CreateRenderTexture(int size, RenderTextureFormat format = RenderTextureFormat.RGFloat, bool useMips = false)
    {
        var renderTexture = new RenderTexture(size, size, 0, format, RenderTextureReadWrite.Linear)
        {
            useMipMap = useMips,
            autoGenerateMips = false,
            anisoLevel = 6,
            filterMode = FilterMode.Trilinear,
            wrapMode = TextureWrapMode.Repeat,
            enableRandomWrite = true
        };
        if (!renderTexture.Create())
            Debug.Log("OceanTextureGenerator: Failed to create render texture.");
        return renderTexture;
    }

    /// <summary>
    /// Create a new SIZE x SIZE Texture2D with noise values. If enabled, the texture will also be
    /// saved as a Texture2D asset.
    /// </summary>
    /// <param name="size">The pixel dimensions of the texture.</param>
    /// <param name="saveAsAsset">Whether the texture should be saved as an asset.</param>
    /// <returns>The generated Texture2D.</returns>
    // [BurstCompile]
    public static Texture2D NoiseTexture(int size, bool saveAsAsset)
    {
        // Create the noise texture.
        var noiseTexture = new Texture2D(size, size, TextureFormat.RGFloat, mipChain: false, linear: true);
        noiseTexture.filterMode = FilterMode.Point;

        // Populate the pixel data with random values and apply the changes to the texture.
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                noiseTexture.SetPixel(i, j, new Vector4(GetRandomNormalValue(), GetRandomNormalValue()));
            }
        }
        noiseTexture.Apply();

        // If enabled, store the noise texture as an asset so we don't need to generate it again.
        if (saveAsAsset && Application.isEditor)
        {
            var filePrefix = "Assets/Resources/GaussianNoiseTextures/GaussianNoiseTexture";
            var fileName = filePrefix + size.ToString() + "x" + size.ToString();
            AssetDatabase.CreateAsset(noiseTexture, fileName + ".asset");
            Debug.Log("Added noise texture at: " + fileName);
        }
        return noiseTexture;
    }

    /// <summary>
    /// Get a random normal value.
    /// </summary>
    /// <returns></returns>
    public static float GetRandomNormalValue()
    {
        return Mathf.Cos(2 * Mathf.PI * Random.value) * Mathf.Sqrt(-2 * Mathf.Log(Random.value));
    }
}