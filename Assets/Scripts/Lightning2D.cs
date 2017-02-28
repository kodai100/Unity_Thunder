using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LaplaceCS))]
public class Lightning2D : MonoBehaviour {

    Texture2D lightning_texture;
    float[,] potential, lightning;
    LaplaceCS laplace_cs;

    int width, height;

    public Vector2 start_position;
    public int m = 3;
    public int eta = 1;

    void Start() {
        laplace_cs = GetComponent<LaplaceCS>();
        width = laplace_cs.width;
        height = laplace_cs.height;

        potential = new float[height, width];
        lightning = new float[height, width];

        lightning_texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        lightning_texture.filterMode = FilterMode.Point;
    }

    // Called From Laplace.cs
    // Laplace.cs mode must be static
    public void InitializeLightning (float[] potential_from_laplace) {
        
        for (int y = 0; y < height; y++) {
            for(int x = 0; x < width; x++) {
                potential[y, x] = potential_from_laplace[y * width + x];
                lightning[y, x] = 0;
            }
        }

        LightningProcess();

        
    }

    void LightningProcess() {

        Vector2 leader_pos = new Vector2( Random.Range(0, width), 0);

        do {
            lightning[(int)leader_pos.y, (int)leader_pos.x] = 1f;

            // 1. 周辺のセルからランダムにM個セルを選ぶ
            List<Vector2> m_cells_index = new List<Vector2>();
            while(m_cells_index.Count < m) {
                int tmpx = (int)Random.Range(leader_pos.x - 2, leader_pos.x + 3);
                int tmpy = (int)Random.Range(leader_pos.y - 2, leader_pos.y + 3);

                // セルリーダー重複判定
                if (tmpx == leader_pos.x && tmpy == leader_pos.y) {
                    continue;
                }

                // セル範囲外判定
                if(tmpx < 0 || tmpx >= width || tmpy < 0 || tmpy >= height) {
                    continue;
                }

                // セル重複判定
                bool isSame = false;
                for (int j = 0; j < m_cells_index.Count; j++) {
                    if ((tmpx == m_cells_index[j].x && tmpy == m_cells_index[j].y)) {
                        isSame = true;
                        break;
                    }
                }
                if (isSame) continue;

                // Debug.Log(tmpx + "," + tmpy);
                m_cells_index.Add(new Vector2(tmpx, tmpy));
            }

            // 2.進路先の決定
            float total_potential = 0f;
            for (int i = 0; i < m_cells_index.Count; i++) {
                total_potential += potential[(int)m_cells_index[i].y, (int)m_cells_index[i].x];
            }

            Vector2 next_leader_pos = leader_pos;
            float max_possibility = 0f;
            for(int i = 0; i<m_cells_index.Count; i++) {
                float tmp_possibility = potential[(int)m_cells_index[i].y, (int)m_cells_index[i].x] / total_potential;
                if(max_possibility < tmp_possibility) {
                    next_leader_pos = new Vector2(m_cells_index[i].x, m_cells_index[i].y);
                    max_possibility = tmp_possibility;
                }
            }
            
            leader_pos = next_leader_pos;

        } while(leader_pos.y != height-1);

        ApplyTexture();
    }
	
	void Update () {
        if (Input.GetKeyDown(KeyCode.I)) {
            LightningProcess();
        }
	}

    void OnGUI() {
        if (laplace_cs.mode == LaplaceCS.Mode.Static) {
            GUI.DrawTexture(new Rect(new Vector2(0, 0), new Vector2(width, height)), lightning_texture);
        }
    }

    void ApplyTexture() {
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                lightning_texture.SetPixel(x, (height - 1) - y, new Color(lightning[y, x] , lightning[y, x], lightning[y, x]));
            }
        }
        lightning_texture.Apply();
    }
}
