/// WavesCascadeComputeHandler.cs
/// Author: Rohith Vishwajith
/// Created 4/26/2024

using System;
using UnityEngine;

public class WavesCascadeComputeHandler
{
    public RenderTexture Displacement => displacementTexture;
    public RenderTexture Derivatives => derivativesTexture;
    public RenderTexture Turbulence => turbulenceTexture;

    public Texture2D GaussianNoise => gaussianNoise;
    public RenderTexture PrecomputedData => precomputedDataTexture;
    public RenderTexture InitialSpectrum => initialSpectrum;

    const int LOCAL_WORK_GROUPS_X = 16, LOCAL_WORK_GROUPS_Y = 16;

    // The texture resolution.
    readonly int size;

    // Compute shaders for initial spectrum, time-dependent spectrum, and texture mergers.
    readonly ComputeShader initialSpectrumCompute;
    readonly ComputeShader timeDependentSpectrumCompute;
    readonly ComputeShader texturesMergerCompute;

    readonly OceanFFTComputeHandler fft;
    readonly Texture2D gaussianNoise;
    readonly ComputeBuffer paramatersBuffer;
    readonly RenderTexture initialSpectrum;

    // Precomputed data texture.
    readonly RenderTexture precomputedDataTexture;
    readonly RenderTexture bufferTexture;
    readonly RenderTexture DxDzTexture, DyDxzTexture, DyxDyzTexture, DxxDzzTexture;

    // Displacement, derivative, and turbulence textures.
    readonly RenderTexture displacementTexture;
    readonly RenderTexture derivativesTexture;
    readonly RenderTexture turbulenceTexture;

    float lambda;

    // Kernel IDs:
    int KERNEL_INITIAL_SPECTRUM;
    int KERNEL_CONJUGATE_SPECTRUM;
    int KERNEL_TIME_DEPENDENT_SPECTRUMS;
    int KERNEL_RESULT_TEXTURES;

    // Property IDs for size, lengthScale, and cutoffs.
    readonly int SIZE_PROPERTY_ID = Shader.PropertyToID("Size");
    readonly int LENGTH_SCALE_PROPERTY_ID = Shader.PropertyToID("LengthScale");
    readonly int CUTOFF_HIGH_PROPERTY_ID = Shader.PropertyToID("CutoffHigh"),
        CUTOFF_LOW_PROPERTY_ID = Shader.PropertyToID("CutoffLow");

    // Property IDs for noise, wave data, and time.
    readonly int NOISE_PROPERTY_ID = Shader.PropertyToID("Noise");
    readonly int PRECOMPUTED_WAVE_DATA_PROPERTY_ID = Shader.PropertyToID("WavesData");
    readonly int TIME_PROPERTY_ID = Shader.PropertyToID("Time");

    // Property IDs for H0 and H0_k.
    readonly int H0_PROPERTY_ID = Shader.PropertyToID("H0");
    readonly int H0K_PROPERTY_ID = Shader.PropertyToID("H0K");

    // Displacement, derivative, and turbulence properties.
    readonly int DISPLACEMENT_PROPERTY_ID = Shader.PropertyToID("Displacement");
    readonly int DERIVATIVES_PROPERTY_ID = Shader.PropertyToID("Derivatives");
    readonly int TURBULENCE_PROPERTY_ID = Shader.PropertyToID("Turbulence");

    // Derivative properties and lambda.
    readonly int DX_DZ_PROPERTY_ID = Shader.PropertyToID("Dx_Dz");
    readonly int DY_DXZ_PROPERTY_ID = Shader.PropertyToID("Dy_Dxz");
    readonly int DYX_DYZ_PROPERTY_ID = Shader.PropertyToID("Dyx_Dyz");
    readonly int DXX_DZZ_PROPERTY_ID = Shader.PropertyToID("Dxx_Dzz");
    readonly int LAMBDA_PROPERTY_ID = Shader.PropertyToID("Lambda");

    public WavesCascadeComputeHandler(int size, ComputeShader initialSpectrumShader,
                        ComputeShader timeDependentSpectrumShader,
                        ComputeShader texturesMergerShader,
                        OceanFFTComputeHandler fft,
                        Texture2D gaussianNoise)
    {
        this.size = size;
        this.initialSpectrumCompute = initialSpectrumShader;
        this.timeDependentSpectrumCompute = timeDependentSpectrumShader;
        this.texturesMergerCompute = texturesMergerShader;
        this.fft = fft;
        this.gaussianNoise = gaussianNoise;

        KERNEL_INITIAL_SPECTRUM = initialSpectrumShader.FindKernel("CalculateInitialSpectrum");
        KERNEL_CONJUGATE_SPECTRUM = initialSpectrumShader.FindKernel("CalculateConjugatedSpectrum");
        KERNEL_TIME_DEPENDENT_SPECTRUMS = timeDependentSpectrumShader.FindKernel("CalculateAmplitudes");
        KERNEL_RESULT_TEXTURES = texturesMergerShader.FindKernel("FillResultTextures");

        // Set up input and spectrum textures,
        initialSpectrum = OceanTextureGenerator.CreateRenderTexture(size, RenderTextureFormat.ARGBFloat);
        precomputedDataTexture = OceanTextureGenerator.CreateRenderTexture(size, RenderTextureFormat.ARGBFloat);
        displacementTexture = OceanTextureGenerator.CreateRenderTexture(size, RenderTextureFormat.ARGBFloat);
        derivativesTexture = OceanTextureGenerator.CreateRenderTexture(size, RenderTextureFormat.ARGBFloat, true);
        turbulenceTexture = OceanTextureGenerator.CreateRenderTexture(size, RenderTextureFormat.ARGBFloat, true);

        // Setup buffer.
        paramatersBuffer = new ComputeBuffer(2, 8 * sizeof(float));
        bufferTexture = OceanTextureGenerator.CreateRenderTexture(size);

        // Set up axis-derivative textures.
        DxDzTexture = OceanTextureGenerator.CreateRenderTexture(size);
        DyDxzTexture = OceanTextureGenerator.CreateRenderTexture(size);
        DyxDyzTexture = OceanTextureGenerator.CreateRenderTexture(size);
        DxxDzzTexture = OceanTextureGenerator.CreateRenderTexture(size);
    }

    public void DisposeBufferData()
    {
        paramatersBuffer?.Release();
    }

    public void CalculateInitials(WavesSettingsAsset wavesSettings, float lengthScale,
                                  float cutoffLow, float cutoffHigh)
    {
        lambda = wavesSettings.lambda;

        initialSpectrumCompute.SetInt(SIZE_PROPERTY_ID, size);
        initialSpectrumCompute.SetFloat(LENGTH_SCALE_PROPERTY_ID, lengthScale);
        initialSpectrumCompute.SetFloat(CUTOFF_HIGH_PROPERTY_ID, cutoffHigh);
        initialSpectrumCompute.SetFloat(CUTOFF_LOW_PROPERTY_ID, cutoffLow);
        wavesSettings.SetParametersToShader(initialSpectrumCompute, KERNEL_INITIAL_SPECTRUM, paramatersBuffer);

        initialSpectrumCompute.SetTexture(KERNEL_INITIAL_SPECTRUM, H0K_PROPERTY_ID, bufferTexture);
        initialSpectrumCompute.SetTexture(KERNEL_INITIAL_SPECTRUM, PRECOMPUTED_WAVE_DATA_PROPERTY_ID, precomputedDataTexture);
        initialSpectrumCompute.SetTexture(KERNEL_INITIAL_SPECTRUM, NOISE_PROPERTY_ID, gaussianNoise);
        initialSpectrumCompute.Dispatch(KERNEL_INITIAL_SPECTRUM, size / LOCAL_WORK_GROUPS_X, size / LOCAL_WORK_GROUPS_Y, 1);

        initialSpectrumCompute.SetTexture(KERNEL_CONJUGATE_SPECTRUM, H0_PROPERTY_ID, initialSpectrum);
        initialSpectrumCompute.SetTexture(KERNEL_CONJUGATE_SPECTRUM, H0K_PROPERTY_ID, bufferTexture);
        initialSpectrumCompute.Dispatch(KERNEL_CONJUGATE_SPECTRUM, size / LOCAL_WORK_GROUPS_X, size / LOCAL_WORK_GROUPS_Y, 1);
    }

    /// <summary>
    /// Calculate the wave data at a given time using the inverse fast fourier transform.
    /// This function handles all compute shader dispatching/calculations.
    /// </summary>
    /// <param name="t">The time at which wave data should be calculated.</param>
    public void CalculateWaveData(float t)
    {
        // Calculating complex amplitudes
        timeDependentSpectrumCompute.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUMS, DX_DZ_PROPERTY_ID, DxDzTexture);
        timeDependentSpectrumCompute.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUMS, DY_DXZ_PROPERTY_ID, DyDxzTexture);
        timeDependentSpectrumCompute.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUMS, DYX_DYZ_PROPERTY_ID, DyxDyzTexture);
        timeDependentSpectrumCompute.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUMS, DXX_DZZ_PROPERTY_ID, DxxDzzTexture);
        timeDependentSpectrumCompute.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUMS, H0_PROPERTY_ID, initialSpectrum);
        timeDependentSpectrumCompute.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUMS, PRECOMPUTED_WAVE_DATA_PROPERTY_ID, precomputedDataTexture);
        timeDependentSpectrumCompute.SetFloat(TIME_PROPERTY_ID, t);
        timeDependentSpectrumCompute.Dispatch(KERNEL_TIME_DEPENDENT_SPECTRUMS, size / LOCAL_WORK_GROUPS_X, size / LOCAL_WORK_GROUPS_Y, 1);

        // Calculate inverse FFTs of complex amplitudes.
        void RunInverseTransforms(bool outputToInput = true, bool scale = false, bool permute = true)
        {
            fft.InverseFFT2D(DxDzTexture, bufferTexture, outputToInput, scale, permute);
            fft.InverseFFT2D(DyDxzTexture, bufferTexture, outputToInput, scale, permute);
            fft.InverseFFT2D(DyxDyzTexture, bufferTexture, outputToInput, scale, permute);
            fft.InverseFFT2D(DxxDzzTexture, bufferTexture, outputToInput, scale, permute);
        }

        // Set the textures for displacement, derivatives, turbulence, etc. as the input values
        // for the final compute shader.
        void SetMergerComputeInputs()
        {
            // Filling displacement and normals textures
            texturesMergerCompute.SetFloat("DeltaTime", Time.deltaTime);
            texturesMergerCompute.SetTexture(KERNEL_RESULT_TEXTURES, DX_DZ_PROPERTY_ID, DxDzTexture);
            texturesMergerCompute.SetTexture(KERNEL_RESULT_TEXTURES, DY_DXZ_PROPERTY_ID, DyDxzTexture);
            texturesMergerCompute.SetTexture(KERNEL_RESULT_TEXTURES, DYX_DYZ_PROPERTY_ID, DyxDyzTexture);
            texturesMergerCompute.SetTexture(KERNEL_RESULT_TEXTURES, DXX_DZZ_PROPERTY_ID, DxxDzzTexture);
            texturesMergerCompute.SetTexture(KERNEL_RESULT_TEXTURES, DISPLACEMENT_PROPERTY_ID, displacementTexture);
            texturesMergerCompute.SetTexture(KERNEL_RESULT_TEXTURES, DERIVATIVES_PROPERTY_ID, derivativesTexture);
            texturesMergerCompute.SetTexture(KERNEL_RESULT_TEXTURES, TURBULENCE_PROPERTY_ID, turbulenceTexture);
            texturesMergerCompute.SetFloat(LAMBDA_PROPERTY_ID, lambda);
            texturesMergerCompute.Dispatch(KERNEL_RESULT_TEXTURES, size / LOCAL_WORK_GROUPS_X, size / LOCAL_WORK_GROUPS_Y, 1);
        }

        // Once the textures are done being generated, regenerate image LODs/mipmaps.
        void RegenerateTextureMipMaps()
        {
            derivativesTexture.GenerateMips();
            turbulenceTexture.GenerateMips();
        }

        RunInverseTransforms(true, false, true);
        SetMergerComputeInputs();
        RegenerateTextureMipMaps();
    }
}
