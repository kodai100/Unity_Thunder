using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;

public class Laplace : MonoBehaviour {

    public enum Mode {
        Animation, Static
    }
    public Mode mode;

    public int width = 256;
    public int height = 256;

    public float allowed_error = 0.00001f;
    public int allowed_iter = 10000;

    public float left_strength;
    public float right_strength;
    public float up_strength;
    public float bottom_strength;

    public float sor_omega = 1f;

    Texture2D texture;
    float[,] potential_read;
    int count;
    bool finish;

    void Start() {
        texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.filterMode = FilterMode.Point;
        potential_read = new float[texture.height, texture.width];
        count = 0;
        finish = false;

        SetBoundaryCondition();

        if (!finish && mode == Mode.Static) LaplaceEquation();

        ApplyTexture();
    }

    void Update() {

        errorMax = 0f;
        if (!finish && mode == Mode.Animation) AnimatedLaplaceEquation();

    }

    void OnGUI() {
        GUI.DrawTexture(new Rect(new Vector2(0, 0), new Vector2(texture.width, texture.height)), texture);
    }

    void SetBoundaryCondition() {

        for (int y = 0; y < texture.height; y++) {
            for (int x = 0; x < texture.width; x++) {
                potential_read[y, x] = 0f;
            }
        }

        for (int i = 0; i < width; i++) {
            float wave = Mathf.Abs(Mathf.Sin(2 * Mathf.PI * i / (float)width));
            potential_read[0, i] = up_strength * wave;
            potential_read[texture.height - 1, i] = bottom_strength * wave;
            potential_read[i, 0] = left_strength * wave;
            potential_read[i, width - 1] = right_strength * wave;
        }

        //potential_read[height / 2, width / 2] = 1f;
    }

    float errorMax;
    void AnimatedLaplaceEquation() {

        float error;

        for (int y = 1; y < texture.height - 1; y++) {
            for (int x = 1; x < texture.width - 1; x++) {

                float prev = potential_read[y, x];
                potential_read[y, x] = prev +  sor_omega * ( 0.25f * (potential_read[y, x - 1] + potential_read[y, x + 1] + potential_read[y - 1, x] + potential_read[y + 1, x]) - prev);

                error = Mathf.Abs(potential_read[y, x] - prev);

                if (errorMax < error) {
                    errorMax = error;
                }
            }
        }

        UnityEngine.Debug.Log(count++ + ", " + errorMax);

        if (errorMax < allowed_error) {
            finish = true;
        }

        ApplyTexture();

    }

    void LaplaceEquation() {

        float dx = 1f / width;
        float dy = 1f / height;

        float c1 = 0.5f * dx * dx / (dx * dx + dy * dy);
        float c2 = 0.5f * dy * dy / (dx * dx + dy * dy);

        Stopwatch sw = new Stopwatch();
        sw.Start();

        do {
            errorMax = 0;
            for (int y = 1; y < texture.height - 1; y++) {
                for (int x = 1; x < texture.width - 1; x++) {

                    float prev = potential_read[y, x];
                    potential_read[y, x] += sor_omega * (c1 * (potential_read[y, x - 1] + potential_read[y, x + 1]) + c2 * (potential_read[y - 1, x] + potential_read[y + 1, x]) - prev);

                    float error = Mathf.Abs(potential_read[y, x] - prev);

                    if (errorMax < error) {
                        errorMax = error;
                    }
                }
            }

            count++;
            if (count > allowed_iter) break;

        } while (errorMax > allowed_error);

        finish = true;

        sw.Stop();

        UnityEngine.Debug.Log("Finished: " + sw.ElapsedMilliseconds + "ms");

        ApplyTexture();

    }

    void ApplyTexture() {
        for (int y = 0; y < texture.height; y++) {
            for (int x = 0; x < texture.width; x++) {
                texture.SetPixel(x, (texture.height - 1) - y, new Color(0f, 0f, potential_read[y, x]));
            }
        }
        texture.Apply();
    }
}
