/// OceanTextureGenerator.cs
/// Author: Rohith Vishwajith
/// Created 4/30/2024

using UnityEngine;
using UnityEditor;
using Unity.Burst;
using Unity.Mathematics;

public static class OceanTextureGenerator
{
    /// <summary>
    /// A burst-compatible random value generator that uses a preset seed.
    /// </summary>
    static readonly Unity.Mathematics.Random RANDOM_GENERATOR = new(1);

    /// <summary>
    /// Create a render texture with the given size, format, and mipmap settings.
    /// </summary>
    /// <param name="resolution">The pixel dimensions of the texture.</param>
    /// <param name="format">The format of the texture (RGFloat by default).</param>
    /// <param name="useMipMap">Whether the texture should use mipMaps (false by default).</param>
    /// <returns>The generated RenderTexture.</returns>
    [BurstCompile]
    public static RenderTexture CreateRenderTexture(int resolution = 256, RenderTextureFormat format = RenderTextureFormat.RGFloat, bool useMipMap = false)
    {
        var renderTexture = new RenderTexture(resolution, resolution, 0, format, RenderTextureReadWrite.Linear)
        {
            useMipMap = useMipMap,
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
    /// <param name="resolution">The pixel dimensions of the texture.</param>
    /// <param name="saveAsAsset">Whether the texture should be saved as an asset.</param>
    /// <returns>The generated Texture2D.</returns>
    [BurstCompile]
    public static Texture2D CreateGaussianNoise(int resolution, bool saveAsAsset)
    {
        // Create the noise texture.
        var noiseTexture = new Texture2D(resolution, resolution, TextureFormat.RGFloat, mipChain: false, linear: true);
        noiseTexture.filterMode = FilterMode.Point;

        // Populate the pixel data with random values and apply the changes to the texture.
        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                noiseTexture.SetPixel(i, j, new Vector4(GetRandomNormalValue(), GetRandomNormalValue()));
            }
        }
        noiseTexture.Apply();

        // If enabled, store the noise texture as an asset so we don't need to generate it again.
        if (saveAsAsset && Application.isEditor)
        {
            var filePrefix = "Assets/Resources/GaussianNoise/";
            var fileName = filePrefix + resolution + "x" + resolution;
            AssetDatabase.CreateAsset(noiseTexture, fileName + ".asset");
            Debug.Log("Added noise texture at: " + fileName);
        }
        return noiseTexture;
    }

    /// <summary>
    /// Get a random normal value.
    /// </summary>
    /// <returns></returns>
    [BurstCompile]
    public static float GetRandomNormalValue()
    {
        var randA = RANDOM_GENERATOR.NextFloat();
        var randB = RANDOM_GENERATOR.NextFloat();
        return math.cos(2 * math.PI * randA * math.sqrt(-2f * math.log2(randB)));
    }
}