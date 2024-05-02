/// OceanFFTComputeHandler.cs
/// Author: Rohith Vishwajith
/// Created 4/26/2024

using UnityEngine;

public class OceanFFTComputeHandler
{
    readonly int size;
    readonly ComputeShader fftShader;
    readonly RenderTexture precomputedData;

    // Kernel IDs:
    readonly int KERNEL_PRECOMPUTE;
    readonly int KERNEL_HORIZONTAL_STEP_FFT;
    readonly int KERNEL_VERTICAL_STEP_FFT;
    readonly int KERNEL_HORIZONTAL_STEP_IFFT;
    readonly int KERNEL_VERTICAL_STEP_IFFT;
    readonly int KERNEL_SCALE;
    readonly int KERNEL_PERMUTE;

    // Property IDs:
    readonly int PROP_ID_PRECOMPUTE_BUFFER = Shader.PropertyToID("PrecomputeBuffer");
    readonly int PRECOMPUTED_PROPERTY_ID = Shader.PropertyToID("PrecomputedData");
    readonly int BUFFER0_PROPERTY_ID = Shader.PropertyToID("Buffer0");
    readonly int BUFFER1_PROPERTY_ID = Shader.PropertyToID("Buffer1");
    readonly int SIZE_PROPERTY_ID = Shader.PropertyToID("Size");
    readonly int STEP_PROPERTY_ID = Shader.PropertyToID("Step");
    readonly int PING_PONG_PROPERTY_ID = Shader.PropertyToID("PingPong");

    // Local work group sizes. Change this to 16 instead of 8?
    const int LOCAL_WORK_GROUPS_X = 16;
    const int LOCAL_WORK_GROUPS_Y = 16;

    public OceanFFTComputeHandler(int size, ComputeShader fftShader)
    {
        this.size = size;
        this.fftShader = fftShader;
        precomputedData = PrecomputedTwiddleAndInputIndicesTexture();
        KERNEL_PRECOMPUTE = fftShader.FindKernel("PrecomputeTwiddleFactorsAndInputIndices");
        KERNEL_HORIZONTAL_STEP_FFT = fftShader.FindKernel("HorizontalStepFFT");
        KERNEL_VERTICAL_STEP_FFT = fftShader.FindKernel("VerticalStepFFT");
        KERNEL_HORIZONTAL_STEP_IFFT = fftShader.FindKernel("HorizontalStepInverseFFT");
        KERNEL_VERTICAL_STEP_IFFT = fftShader.FindKernel("VerticalStepInverseFFT");
        KERNEL_SCALE = fftShader.FindKernel("Scale");
        KERNEL_PERMUTE = fftShader.FindKernel("Permute");
    }

    public void FFT2D(RenderTexture input, RenderTexture buffer, bool outputToInput = false)
    {
        int logSize = (int)Mathf.Log(size, 2);
        bool pingPong = false;

        // Update horizontal textures.
        fftShader.SetTexture(KERNEL_HORIZONTAL_STEP_FFT, PRECOMPUTED_PROPERTY_ID, precomputedData);
        fftShader.SetTexture(KERNEL_HORIZONTAL_STEP_FFT, BUFFER0_PROPERTY_ID, input);
        fftShader.SetTexture(KERNEL_HORIZONTAL_STEP_FFT, BUFFER1_PROPERTY_ID, buffer);

        for (int i = 0; i < logSize; i++)
        {
            // Invert ping pong for each step size.
            pingPong = !pingPong;
            // Update step size.
            fftShader.SetInt(STEP_PROPERTY_ID, i);
            fftShader.SetBool(PING_PONG_PROPERTY_ID, pingPong);
            fftShader.Dispatch(KERNEL_HORIZONTAL_STEP_FFT, size / LOCAL_WORK_GROUPS_X, size / LOCAL_WORK_GROUPS_Y, 1);
        }

        // Update vertical textures.
        fftShader.SetTexture(KERNEL_VERTICAL_STEP_FFT, PRECOMPUTED_PROPERTY_ID, precomputedData);
        fftShader.SetTexture(KERNEL_VERTICAL_STEP_FFT, BUFFER0_PROPERTY_ID, input);
        fftShader.SetTexture(KERNEL_VERTICAL_STEP_FFT, BUFFER1_PROPERTY_ID, buffer);
        for (int i = 0; i < logSize; i++)
        {
            pingPong = !pingPong;
            fftShader.SetInt(STEP_PROPERTY_ID, i);
            fftShader.SetBool(PING_PONG_PROPERTY_ID, pingPong);
            fftShader.Dispatch(KERNEL_VERTICAL_STEP_FFT, size / LOCAL_WORK_GROUPS_X, size / LOCAL_WORK_GROUPS_Y, 1);
        }

        if (pingPong && outputToInput)
            Graphics.Blit(buffer, input);
        else if (!pingPong && !outputToInput)
            Graphics.Blit(input, buffer);
    }

    /// <summary>
    /// Runs a 2D inverse fast-fourier transform on a given input texture with a given buffer
    /// texture and data management values.
    /// </summary>
    /// <param name="inputTexture">The input texture.</param>
    /// <param name="bufferTexture">The buffer texture.</param>
    /// <param name="outputToInput">If enabled, bufferTexture writes back to inputTexture</param>
    /// <param name="scale">Controls whether scaling is enabled.</param>
    /// <param name="permute">Controls whether permutation is enabled.</param>
    public void InverseFFT2D(RenderTexture inputTexture, RenderTexture bufferTexture, bool outputToInput = false, bool scale = true, bool permute = false)
    {
        int logSize = (int)Mathf.Log(size, 2);
        bool pingPong = false;

        fftShader.SetTexture(KERNEL_HORIZONTAL_STEP_IFFT, PRECOMPUTED_PROPERTY_ID, precomputedData);
        fftShader.SetTexture(KERNEL_HORIZONTAL_STEP_IFFT, BUFFER0_PROPERTY_ID, inputTexture);
        fftShader.SetTexture(KERNEL_HORIZONTAL_STEP_IFFT, BUFFER1_PROPERTY_ID, bufferTexture);
        for (int i = 0; i < logSize; i++)
        {
            pingPong = !pingPong;
            fftShader.SetInt(STEP_PROPERTY_ID, i);
            fftShader.SetBool(PING_PONG_PROPERTY_ID, pingPong);
            fftShader.Dispatch(KERNEL_HORIZONTAL_STEP_IFFT, size / LOCAL_WORK_GROUPS_X, size / LOCAL_WORK_GROUPS_Y, 1);
        }

        fftShader.SetTexture(KERNEL_VERTICAL_STEP_IFFT, PRECOMPUTED_PROPERTY_ID, precomputedData);
        fftShader.SetTexture(KERNEL_VERTICAL_STEP_IFFT, BUFFER0_PROPERTY_ID, inputTexture);
        fftShader.SetTexture(KERNEL_VERTICAL_STEP_IFFT, BUFFER1_PROPERTY_ID, bufferTexture);

        for (int i = 0; i < logSize; i++)
        {
            pingPong = !pingPong;
            fftShader.SetInt(STEP_PROPERTY_ID, i);
            fftShader.SetBool(PING_PONG_PROPERTY_ID, pingPong);
            fftShader.Dispatch(KERNEL_VERTICAL_STEP_IFFT, size / LOCAL_WORK_GROUPS_X, size / LOCAL_WORK_GROUPS_Y, 1);
        }

        if (pingPong && outputToInput)
            Graphics.Blit(bufferTexture, inputTexture);
        else if (!pingPong && !outputToInput)
            Graphics.Blit(inputTexture, bufferTexture);

        // Compute permutation if enabled.
        if (permute)
        {
            fftShader.SetInt(SIZE_PROPERTY_ID, size);
            fftShader.SetTexture(KERNEL_PERMUTE, BUFFER0_PROPERTY_ID, outputToInput ? inputTexture : bufferTexture);
            fftShader.Dispatch(KERNEL_PERMUTE, size / LOCAL_WORK_GROUPS_X, size / LOCAL_WORK_GROUPS_Y, 1);
        }

        // Compute scaling if enabled.
        if (scale)
        {
            fftShader.SetInt(SIZE_PROPERTY_ID, size);
            fftShader.SetTexture(KERNEL_SCALE, BUFFER0_PROPERTY_ID, outputToInput ? inputTexture : bufferTexture);
            fftShader.Dispatch(KERNEL_SCALE, size / LOCAL_WORK_GROUPS_X, size / LOCAL_WORK_GROUPS_Y, 1);
        }
    }

    /// <summary>
    /// Generate a render texture with a twiddle factor for FFT:
    /// Source: https://en.wikipedia.org/wiki/Twiddle_factor
    /// <returns>A RenderTexture with twiddle factors.</returns>
    RenderTexture PrecomputedTwiddleAndInputIndicesTexture()
    {
        int logSize = (int)Mathf.Log(size, 2);
        var rt = new RenderTexture(logSize, size, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
            enableRandomWrite = true
        };
        rt.Create();
        fftShader.SetInt(SIZE_PROPERTY_ID, size);
        fftShader.SetTexture(KERNEL_PRECOMPUTE, PROP_ID_PRECOMPUTE_BUFFER, rt);
        fftShader.Dispatch(KERNEL_PRECOMPUTE, logSize, size / 2 / LOCAL_WORK_GROUPS_Y, 1);
        return rt;
    }
}
