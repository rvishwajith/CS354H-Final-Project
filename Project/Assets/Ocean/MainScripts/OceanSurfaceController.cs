/// OceanSurfaceController.cs
/// Author: Rohith Vishwajith
/// Created 4/30/2024

using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;

/// <summary>
/// The primary controller for the FFT ocean simulation. Handles wave generation via asynchronous
/// compute shader dispatching and readback.
/// </summary>
public class OceanSurfaceController : MonoBehaviour
{
    [SerializeField] WavesSettingsAsset wavesSettings;
    [HideInInspector] public WavesCascadeComputeHandler cascade0;
    [HideInInspector] public WavesCascadeComputeHandler cascade1;
    [HideInInspector] public WavesCascadeComputeHandler cascade2;
    [HideInInspector] public WavesCascadeComputeHandler[] cascades = null;

    [Header("Quality Settings")]
    [SerializeField] int size = 256;
    [SerializeField] bool alwaysRecalculateInitials = false;

    [Header("Cascade Scales")]
    [SerializeField] float cascadeLengthScale0 = 250;
    [SerializeField] float cascadeLengthScale1 = 17;
    [SerializeField] float cascadeLengthScale2 = 5;
    [HideInInspector] public float[] cascadeLengthScales = null;

    [Header("Compute Shaders")]
    [SerializeField] ComputeShader fourierTransformShader;
    [SerializeField] ComputeShader initialSpectrumShader;
    [SerializeField] ComputeShader timeDependentSpectrumShader;
    [SerializeField] ComputeShader textureMergerShader;

    Texture2D gaussianNoiseTexture = null;
    OceanFFTComputeHandler fftHandler = null;
    Texture2D physicsReadbackTexture = null;

    private void Awake()
    {
        // Application.targetFrameRate = -1;
        Texture2D GetGaussianNoiseTexture(int resolution)
        {
            var fileName = "GaussianNoise/" + resolution + "x" + resolution;
            var noise = Resources.Load<Texture2D>(fileName);
            if (noise)
            {
                Debug.Log("OceanSurfaceShader: Loaded existing gaussian noise texture.");
                return noise;
            }
            Debug.Log("OceanSurfaceShader: Created " + resolution + "x" + resolution + " gaussian noise texture.");
            return OceanTextureGenerator.CreateGaussianNoise(resolution, false);
        }
        fftHandler = new(size, fourierTransformShader);
        gaussianNoiseTexture = GetGaussianNoiseTexture(size);
        physicsReadbackTexture = new(size, size, TextureFormat.RGBAFloat, false);
        InitialiseCascades();
    }

    void InitialiseCascades()
    {
        cascadeLengthScales = new float[] {
            cascadeLengthScale0, cascadeLengthScale1, cascadeLengthScale2
        };
        Shader.SetGlobalFloat("LengthScale0", cascadeLengthScale0);
        Shader.SetGlobalFloat("LengthScale1", cascadeLengthScale1);
        Shader.SetGlobalFloat("LengthScale2", cascadeLengthScale2);

        cascade0 = new(size, initialSpectrumShader, timeDependentSpectrumShader, textureMergerShader, fftHandler, gaussianNoiseTexture);
        cascade1 = new(size, initialSpectrumShader, timeDependentSpectrumShader, textureMergerShader, fftHandler, gaussianNoiseTexture);
        cascade2 = new(size, initialSpectrumShader, timeDependentSpectrumShader, textureMergerShader, fftHandler, gaussianNoiseTexture);
        // cascade0.CalculateInitials(wavesSettings, cascadeLengthScale0, 0.0001f, boundary1);
        // cascade1.CalculateInitials(wavesSettings, cascadeLengthScale1, boundary1, boundary2);
        // cascade2.CalculateInitials(wavesSettings, cascadeLengthScale2, boundary2, 9999);

        cascades = new WavesCascadeComputeHandler[] {
            cascade0,
            cascade1,
            cascade2
        };

        var cascadeCutoffs = new Vector2[] {
            new(0.0001f, OceanMath.ComputeCascadeBoundary(cascadeLengthScale1)),
            new(OceanMath.ComputeCascadeBoundary(cascadeLengthScale1), OceanMath.ComputeCascadeBoundary(cascadeLengthScale2)),
            new(OceanMath.ComputeCascadeBoundary(cascadeLengthScale2), 9999.9999f)
        };

        for (int i = 0; i < cascades.Length; i++)
        {
            var lengthScale = cascadeLengthScales[i];
            var cutoffs = cascadeCutoffs[i];
            cascades[i].CalculateInitials(wavesSettings, lengthScale, cutoffs.x, cutoffs.y);
        }
    }

    /// <summary>
    /// Called every frame. Recalculates wave data based on the current time, then submits the
    /// next GPU readback request to 
    /// </summary>
    private void Update()
    {
        if (alwaysRecalculateInitials)
            InitialiseCascades();
        for (var i = 0; i < cascades.Length; i++)
            cascades[i].CalculateWaveData(Time.time);
        // Replaced:
        // cascade0.CalculateWavesAtTime(Time.time);
        // cascade1.CalculateWavesAtTime(Time.time);
        // cascade2.CalculateWavesAtTime(Time.time);
        // Once wave data is calculated, start an async request for physics readback based on
        // the first wave cascade (highest length scale) displacement.
        AsyncGPUReadback.Request(
            src: cascades[0].Displacement,
            mipIndex: 0,
            dstFormat: TextureFormat.RGBAFloat,
            callback: OnCompletePhysicsReadback);
    }

    /// <summary>
    /// When this component is removed (when the scene ends), dispose of all the buffer data
    /// for the compute shaders in the cascades.
    /// </summary>
    private void OnDestroy()
    {
        // cascade0.Dispose();
        // cascade1.Dispose();
        // cascade2.Dispose();
        for (var i = 0; i < cascades.Length; i++)
        {
            // Dispose of compute shader buffer data.
            cascades[i].DisposeBufferData();
        }
    }

    /// <summary>
    /// Gets the water height at a given position. This is useful when the height is needed at a
    /// given X/Z position and the points are being horizontally displaced. Since FFT is
    /// deterministic, this can be done in the same complexity as computing the displacement at
    /// a given point.
    /// TODO: Could this be be done in the merger shader as well (?).
    /// </summary>
    /// <param name="point">The point to compute the water height at.</param>
    /// <returns></returns>
    public float GetWaterHeight(float3 point)
    {
        var offset = GetDisplacementAtPoint(point);
        var displacement = GetDisplacementAtPoint(point - offset);
        return GetDisplacementAtPoint(point - displacement).y;
    }

    /// <summary>
    /// Computes and returns the water displacement at a given position. Displacement is on all 3
    /// axes.
    /// </summary>
    /// <param name="point">The point to compute the displacement at.</param>
    /// <returns>The displacement.</returns>
    public float3 GetDisplacementAtPoint(float3 point)
    {
        var pointUV = point / cascadeLengthScale0;
        var displacement = physicsReadbackTexture.GetPixelBilinear(pointUV.x, pointUV.y);
        return new float3(displacement.r, displacement.g, displacement.b);
    }

    /// <summary>
    /// Called when an AsyncGPUReadback request is completed. Checks if the request is validated,
    /// and if so, applies the changes to the physics readback texture.
    /// </summary>
    /// <param name="request">The readback request.</param>
    void OnCompletePhysicsReadback(AsyncGPUReadbackRequest request)
    {
        // Early exit cases:
        // 1. The scene has ended.
        // 2. The request had an error.
        if (Application.isEditor && !Application.isPlaying)
            return;
        else if (request.hasError)
        {
            Debug.Log("OceanController.OnCompleteReadback, async GPU request had an error.");
            return;
        }
        // Apply the changes to the readback texture.
        ApplyChangesToTexture(request, physicsReadbackTexture);
    }

    /// <summary>
    /// Called when an AsyncGPUReadback request has been completed, with an additional paramater
    /// for the texture where the request result is stored.
    /// </summary>
    /// <param name="request">The readback request.</param>
    /// <param name="requestOutputTexture">The Texture2D to store the request result onto.</param>
    void ApplyChangesToTexture(AsyncGPUReadbackRequest request, Texture2D requestOutputTexture)
    {
        // Early exit cases: Request is done 
        if (request.done && requestOutputTexture == null)
        {
            Debug.Log("GPU readback request is complete but output texture is null.");
            return;
        }
        requestOutputTexture.LoadRawTextureData(request.GetData<Color>());
        requestOutputTexture.Apply();
        // requestOutputTexture.SetPixelData(request.GetData<Color>(), 0);
    }
}
