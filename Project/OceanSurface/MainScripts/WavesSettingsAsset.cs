using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Ocean Waves Settings", menuName = "Ocean/Waves Settings")]
public class WavesSettingsAsset : ScriptableObject
{
    public float gravity = 9.8f;
    public float depth = 500;

    [Range(0, 1)] public float lambda;
    public SpectrumSettingsMenuAsset local;
    public SpectrumSettingsMenuAsset swell;

    SpectrumSettings[] spectrums = new SpectrumSettings[2];

    readonly int GRAVITY_PROPERTY_ID = Shader.PropertyToID("GravityAcceleration");
    readonly int DEPTH_PROPERTY_ID = Shader.PropertyToID("Depth");
    readonly int SPECTRUMS_PROPERTY_ID = Shader.PropertyToID("Spectrums");

    public void SetParametersToShader(ComputeShader shader, int kernelIndex, ComputeBuffer paramsBuffer)
    {
        shader.SetFloat(GRAVITY_PROPERTY_ID, gravity);
        shader.SetFloat(DEPTH_PROPERTY_ID, depth);

        FillSettingsStruct(local, ref spectrums[0]);
        FillSettingsStruct(swell, ref spectrums[1]);

        paramsBuffer.SetData(spectrums);
        shader.SetBuffer(kernelIndex, SPECTRUMS_PROPERTY_ID, paramsBuffer);
    }

    void FillSettingsStruct(SpectrumSettingsMenuAsset display, ref SpectrumSettings settings)
    {
        settings.scale = display.scale;
        settings.angle = display.windDirection / 180 * Mathf.PI;
        settings.spreadBlend = display.spreadBlend;
        settings.swell = Mathf.Clamp(display.swell, 0.01f, 1);
        settings.alpha = JonswapAlpha(gravity, display.fetch, display.windSpeed);
        settings.peakOmega = JonswapPeakFrequency(gravity, display.fetch, display.windSpeed);
        settings.gamma = display.peakEnhancement;
        settings.shortWavesFade = display.shortWavesFade;
    }

    float JonswapAlpha(float gravity, float fetch, float windSpeed)
    {
        return 0.076f * Mathf.Pow(gravity * fetch / windSpeed / windSpeed, -0.22f);
    }

    float JonswapPeakFrequency(float gravity, float fetch, float windSpeed)
    {
        return 22 * Mathf.Pow(windSpeed * fetch / gravity / gravity, -0.33f);
    }
}

[System.Serializable]
public struct SpectrumSettingsMenuAsset
{
    [Range(0, 1)] public float scale;
    public float windSpeed;
    public float windDirection;
    public float fetch;
    [Range(0, 1)] public float spreadBlend;
    [Range(0, 1)] public float swell;
    public float peakEnhancement;
    public float shortWavesFade;
}

public struct SpectrumSettings
{
    public float scale;
    public float angle;
    public float spreadBlend;
    public float swell;
    public float alpha;
    public float peakOmega;
    public float gamma;
    public float shortWavesFade;
}