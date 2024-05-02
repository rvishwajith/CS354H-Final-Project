/// OceanController.cs
/// Author: Rohith Vishwajith
/// Created 4/25/2024

using UnityEngine;
using UnityEngine.Rendering;

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
    OceanFFTComputeHandler fft = null;
    Texture2D physicsReadbackTexture = null;

    private void Awake()
    {
        // Application.targetFrameRate = -1;
        Texture2D GetNoiseTexture(int size)
        {
            var filePrefix = "GuassianNoiseTexture/" + "GuassianNoiseTexture";
            var fileName = filePrefix + size.ToString() + "x" + size.ToString();
            var noise = Resources.Load<Texture2D>(fileName);
            return noise ? noise : OceanTextureGenerator.NoiseTexture(size, false);
        }

        fft = new OceanFFTComputeHandler(size, fourierTransformShader);
        gaussianNoiseTexture = GetNoiseTexture(size);
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


        cascade0 = new(size, initialSpectrumShader, timeDependentSpectrumShader, textureMergerShader, fft, gaussianNoiseTexture);
        cascade1 = new(size, initialSpectrumShader, timeDependentSpectrumShader, textureMergerShader, fft, gaussianNoiseTexture);
        cascade2 = new(size, initialSpectrumShader, timeDependentSpectrumShader, textureMergerShader, fft, gaussianNoiseTexture);
        // cascade0.CalculateInitials(wavesSettings, cascadeLengthScale0, 0.0001f, boundary1);
        // cascade1.CalculateInitials(wavesSettings, cascadeLengthScale1, boundary1, boundary2);
        // cascade2.CalculateInitials(wavesSettings, cascadeLengthScale2, boundary2, 9999);
        cascades = new WavesCascadeComputeHandler[] {
            cascade0,
            cascade1,
            cascade2
        };

        var cascadeCutoffs = new Vector2[] {
            new(0.0001f, ComputeCascadeBoundary(cascadeLengthScale1)),
            new(ComputeCascadeBoundary(cascadeLengthScale1), ComputeCascadeBoundary(cascadeLengthScale2)),
            new(ComputeCascadeBoundary(cascadeLengthScale2), 9999.9999f)
        };

        for (int i = 0; i < cascades.Length; i++)
        {
            var lengthScale = cascadeLengthScales[i];
            var cutoffs = cascadeCutoffs[i];
            cascades[i].CalculateInitials(wavesSettings, lengthScale, cutoffs.x, cutoffs.y);
        }
    }

    float ComputeCascadeBoundary(float lengthScale)
    {
        return 6f * 2f * Mathf.PI / lengthScale;
    }

    private void Update()
    {
        if (alwaysRecalculateInitials)
            InitialiseCascades();
        for (var i = 0; i < cascades.Length; i++)
            cascades[i].CalculateWaveData(Time.time);

        // Replacement of:
        // cascade0.CalculateWavesAtTime(Time.time);
        // cascade1.CalculateWavesAtTime(Time.time);
        // cascade2.CalculateWavesAtTime(Time.time);
        // Start an asynchornous request to get displacement data from the GPU for cascade 0.
        AsyncGPUReadback.Request(cascade0.Displacement, 0, TextureFormat.RGBAFloat, OnCompleteReadback);
    }

    /// <summary>
    /// When this component is removed, dispose of all the buffer data in the cascades.
    /// </summary>
    private void OnDestroy()
    {
        // cascade0.Dispose();
        // cascade1.Dispose();
        // cascade2.Dispose();
        for (var i = 0; i < cascades.Length; i++)
            cascades[i].DisposeBufferData();
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
    public float GetWaterHeight(Vector3 point)
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
    public Vector3 GetDisplacementAtPoint(Vector3 point)
    {
        var scaledWorldXZPos = point / cascadeLengthScale0;
        var c = physicsReadbackTexture.GetPixelBilinear(scaledWorldXZPos.x, scaledWorldXZPos.y);
        return new Vector3(c.r, c.g, c.b);
    }

    /// <summary>
    /// Called when the AsyncGPUReadback request is completed. Checks if the request is validated,
    /// and if so, applies the changes to the texture.
    /// </summary>
    /// <param name="request">The readback request.</param>
    void OnCompleteReadback(AsyncGPUReadbackRequest request)
    {
        // Early exit cases: Async request failed or result is null when the request is supposed
        // to be complete already.
        if (request.hasError)
        {
            // Debug.Log("OceanController.OnCompleteReadback, request has error.");
            return;
        }
        ApplyChangesToTexture(request, physicsReadbackTexture);
    }

    /// <summary>
    /// Called when an AsyncGPUReadback request has been completed, with an additional paramater
    /// for the texture where the request result is stored.
    /// </summary>
    /// <param name="request">The readback request.</param>
    /// <param name="requestResult">The Texture2D storing the request result.</param>
    void ApplyChangesToTexture(AsyncGPUReadbackRequest request, Texture2D requestResult)
    {
        if (request.hasError)
        {
            // Debug.Log("OceanController.OnCompleteReadback, ");
            return;
        }
        else if (request.done && requestResult == null)
        {
            Debug.Log("GPU readback error detected or resulting texture is null.");
            return;
        }
        requestResult.LoadRawTextureData(request.GetData<Color>());
        requestResult.Apply();
    }
}
