using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaplaceWaveSimulation : MonoBehaviour {

    Texture2D texture;
    float speed = 0.5f;
    float attenuation = 0.999f;

    public float omega = 1f;
    float[,] height, vel;
    float errorMax;

    void Start () {
        texture = new Texture2D(256, 256, TextureFormat.ARGB32, false);
        height = new float[texture.height, texture.width];
        vel = new float[texture.height, texture.width];

        SetBoundaryCondition();
        ApplyTexture();
    }
	
	void Update () {
        WaveSimulation();
        ApplyTexture();
    }

    void OnGUI() {
        GUI.DrawTexture(new Rect(new Vector2(0, 0), new Vector2(256, 256)), texture);
    }

    void SetBoundaryCondition() {
        for (int y = 0; y < texture.height; y++) {
            for (int x = 0; x < texture.width; x++) {
                height[y, x] = 0;
                vel[y, x] = 0;
            }
        }

        height[texture.height / 2, texture.width / 2] = 10f;
    }

    void WaveSimulation() {
        if (Input.GetMouseButtonDown(0)) {
            height[texture.height / 2, texture.width / 2] += 10f;
        }

        errorMax = 0;

        for (int y = 1; y < texture.height - 1; y++) {
            for (int x = 1; x < texture.width - 1; x++) {
                // float accel = height[y, x] + omega * ((height[y, x - 1] + height[y, x + 1] + height[y - 1, x] + height[y + 1, x]) * 0.25f - height[y, x]);
                float accel = omega * (height[y, x - 1] + height[y, x + 1] + height[y - 1, x] + height[y + 1, x]) * 0.25f - height[y, x];
                if (errorMax < Mathf.Abs(accel - height[y, x])) {
                    errorMax = Mathf.Abs(accel - height[y, x]);
                }

                accel *= speed;
                vel[y, x] = (vel[y, x] + accel) * attenuation;
            }
        }

        Debug.Log(errorMax);

        for (int y = 1; y < texture.height - 1; y++) {
            for (int x = 1; x < texture.width - 1; x++) {
                height[y, x] += vel[y, x];
            }
        }
    }

    void ApplyTexture() {
        for (int y = 0; y < texture.height; y++) {
            for (int x = 0; x < texture.width; x++) {
                texture.SetPixel(x, (texture.height - 1) - y, new Color(0f, 0f, height[y, x]));
            }
        }
        texture.Apply();
    }
}
