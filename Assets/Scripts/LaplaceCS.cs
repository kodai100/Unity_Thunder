using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;

public class LaplaceCS : MonoBehaviour {

    public enum Mode {
        Animation, Static
    }
    public Mode mode;

    #region GPU
    const int SIMULATION_BLOCK_SIZE = 256;
    int threadGroupSize;
    int bufferSize;
    public ComputeShader LaplaceCS_1;   // Phase1 odd
    public ComputeShader LaplaceCS_2;   // Phase2 even
    ComputeBuffer potential_buffer_read, potential_buffer_write, phase1_to_2;
    #endregion GPU

    public int width = 256;
    public int height = 256;

    public float allowed_error = 0.00001f;
    public int allowed_iter = 1000;

    [Range(0f, 10f)] public float left_strength;
    [Range(0f, 10f)] public float right_strength;
    [Range(0f, 10f)] public float up_strength;
    [Range(0f, 10f)] public float bottom_strength;

    [Range(1.0f, 2.0f)] public float sor_coef = 1f;

    Texture2D texture;
    float[] potential_read, potential_write;
    
    int count;
    bool finish;
    float errorMax;

    Lightning2D lightning_cs;

    void Start() {
        texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.filterMode = FilterMode.Point;

        count = 0;
        finish = false;

        bufferSize = width * height;
        potential_read = new float[bufferSize];
        potential_write = new float[bufferSize];
        threadGroupSize = Mathf.CeilToInt(bufferSize / SIMULATION_BLOCK_SIZE) + 1;
        potential_buffer_read = new ComputeBuffer(bufferSize, sizeof(float));
        potential_buffer_write = new ComputeBuffer(bufferSize, sizeof(float));
        phase1_to_2 = new ComputeBuffer(bufferSize, sizeof(float));

        lightning_cs = GetComponent<Lightning2D>();
        
        SetBoundaryCondition();

        if (!finish && mode == Mode.Static) LaplaceEquation();
    }

    void Update() {

        errorMax = 0f;
        if (!finish && mode == Mode.Animation) AnimatedLaplaceEquation();

    }

    void OnGUI() {
        if(mode == Mode.Animation) {
            GUI.DrawTexture(new Rect(new Vector2(0, 0), new Vector2(texture.width, texture.height)), texture);
        }
    }

    void OnDestroy() {
        potential_buffer_read.Release();
        potential_buffer_write.Release();
        phase1_to_2.Release();
    }

    void SetBoundaryCondition() {

        for (int i = 0; i < bufferSize; i++) {
            potential_read[i] = 0.5f;
        }

        for (int i = 0; i < bufferSize; i++) {
            if (i < width) potential_read[i] = up_strength;
            if(i >= bufferSize - width) potential_read[i] = bottom_strength;
            if(i % width == 0) potential_read[i] = left_strength;
            if(i % width == width - 1) potential_read[i] = right_strength;
        }

        potential_buffer_read.SetData(potential_read);
        potential_buffer_write.SetData(potential_read);
        
    }

    // For Static Mode
    void LaplaceEquation() {
        Stopwatch sw = new Stopwatch();
        sw.Start();

        do {
            errorMax = 0;

            // Phase 1
            LaplaceCS_1.SetFloat("SOR_COEF", sor_coef);
            LaplaceCS_1.SetInt("BUFFER_SIZE", bufferSize);
            LaplaceCS_1.SetInt("WIDTH", width);
            LaplaceCS_1.SetInt("HEIGHT", height);

            int kernel = LaplaceCS_1.FindKernel("Laplace_Phase1");
            LaplaceCS_1.SetBuffer(kernel, "_PotentialBufferRead", potential_buffer_read);
            LaplaceCS_1.SetBuffer(kernel, "_PotentialBufferWrite", phase1_to_2);

            LaplaceCS_1.Dispatch(kernel, threadGroupSize, 1, 1);


            // Phase 2
            LaplaceCS_2.SetFloat("SOR_COEF", sor_coef);
            LaplaceCS_2.SetInt("BUFFER_SIZE", bufferSize);
            LaplaceCS_2.SetInt("WIDTH", width);
            LaplaceCS_2.SetInt("HEIGHT", height);

            kernel = LaplaceCS_2.FindKernel("Laplace_Phase2");
            LaplaceCS_2.SetBuffer(kernel, "_PotentialBufferRead", phase1_to_2);
            LaplaceCS_2.SetBuffer(kernel, "_PotentialBufferWrite", potential_buffer_write);

            LaplaceCS_2.Dispatch(kernel, threadGroupSize, 1, 1);


            // Error Check
            potential_buffer_read.GetData(potential_read);
            potential_buffer_write.GetData(potential_write);

            float error;
            for (int i = 0; i < bufferSize; i++) {
                error = Mathf.Abs(potential_read[i] - potential_write[i]);
                if (errorMax < error) {
                    errorMax = error;
                }
            }

            SwapBuffer();

            count++;
            if (count > allowed_iter) break;

        } while (errorMax > allowed_error);

        finish = true;

        sw.Stop();
        UnityEngine.Debug.Log("Finished: " + sw.ElapsedMilliseconds + "ms, " + count);
        
        lightning_cs.InitializeLightning(potential_read);
    }

    // For Animation Mode
    void AnimatedLaplaceEquation() {

        // Phase 1
        LaplaceCS_1.SetFloat("SOR_COEF", sor_coef);
        LaplaceCS_1.SetInt("BUFFER_SIZE", bufferSize);
        LaplaceCS_1.SetInt("WIDTH", width);
        LaplaceCS_1.SetInt("HEIGHT", height);

        int kernel = LaplaceCS_1.FindKernel("Laplace_Phase1");
        LaplaceCS_1.SetBuffer(kernel, "_PotentialBufferRead", potential_buffer_read);
        LaplaceCS_1.SetBuffer(kernel, "_PotentialBufferWrite", phase1_to_2);

        LaplaceCS_1.Dispatch(kernel, threadGroupSize, 1, 1);


        // Phase 2
        LaplaceCS_2.SetFloat("SOR_COEF", sor_coef);
        LaplaceCS_2.SetInt("BUFFER_SIZE", bufferSize);
        LaplaceCS_2.SetInt("WIDTH", width);
        LaplaceCS_2.SetInt("HEIGHT", height);

        kernel = LaplaceCS_2.FindKernel("Laplace_Phase2");
        LaplaceCS_2.SetBuffer(kernel, "_PotentialBufferRead", phase1_to_2);
        LaplaceCS_2.SetBuffer(kernel, "_PotentialBufferWrite", potential_buffer_write);

        LaplaceCS_2.Dispatch(kernel, threadGroupSize, 1, 1);

        
        // Error Check
        potential_buffer_read.GetData(potential_read);
        potential_buffer_write.GetData(potential_write);
        
        float error;
        for (int i = 0; i < bufferSize; i++) {
            error = Mathf.Abs(potential_read[i] - potential_write[i]);
            if (errorMax < error) {
                errorMax = error;
            }
        }
        

        UnityEngine.Debug.Log(count++ + ", " + errorMax);

        
        if (errorMax < allowed_error) {
            finish = true;
        }
        

        // Result
        ApplyTexture();

        // Buffer Swap
        SwapBuffer();

    }

    void SwapBuffer() {
        ComputeBuffer tmp = potential_buffer_write;
        potential_buffer_write = potential_buffer_read;
        potential_buffer_read = tmp;
    }

    void ApplyTexture() {
        for (int i = 0; i < bufferSize; i++) {
            texture.SetPixel(i % width, (height-1) - i/width, Color.HSVToRGB(0.5f + potential_read[i] * 0.5f, 1.0f, 1.0f));
        }
        texture.Apply();
    }
}
