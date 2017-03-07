using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LaplaceCS))]
public class Lightning2D : MonoBehaviour {

    Texture2D lightning_texture;
    float[,] potential, lightning;
    LaplaceCS laplace_cs;

    int width, height;
    
    public int m = 3;
    public int eta = 1;

    public float branch = 0.00001f;

    void Awake() {
        laplace_cs = GetComponent<LaplaceCS>();
        width = laplace_cs.width;
        height = laplace_cs.height;

        potential = new float[height, width];
        lightning = new float[height, width];

        lightning_texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        lightning_texture.filterMode = FilterMode.Point;
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.I)) {
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    lightning[y, x] = potential[y, x] * 0.5f;   // 背景にポテンシャル
                }
            }
            LightningProcess(new Vector2(Random.Range(0f, width), 0f));
            ApplyTexture();
        }
    }

    void OnGUI() {
        if (laplace_cs.mode == LaplaceCS.Mode.Static) {
            GUI.DrawTexture(new Rect(new Vector2(0, 0), new Vector2(width, height)), lightning_texture);
        }
    }

    // Called From Laplace.cs
    // Laplace.cs mode must be static
    public void InitializeLightning (float[] potential_from_laplace) {
        
        for (int y = 0; y < height; y++) {
            for(int x = 0; x < width; x++) {
                potential[y, x] = potential_from_laplace[y * width + x];
                lightning[y, x] = potential[y, x] * 0.5f;   // 背景にポテンシャル
            }
        }

        LightningProcess(new Vector2(Random.Range(0f, width), 0f)); // 初期座標で実行
        ApplyTexture();
    }

    void LightningProcess(Vector2 leader_pos) {

        Queue<Vector2> leaders = new Queue<Vector2>();

        // 初期状態
        bool land = false;
        leaders.Enqueue(leader_pos);

        while (!land) {

            for(int i = 0; i < leaders.Count; i++) {

                Vector2 tmp_leader = leaders.Dequeue();
                lightning[(int)tmp_leader.y, (int)tmp_leader.x] = 1f;

                if (tmp_leader.y == height - 1 || tmp_leader.x == 0 || tmp_leader.x == width - 1) {
                    land = true;
                    break;
                }

                // 1. 周辺のセルからランダムにM個セルを選ぶ
                List<Vector2> m_cells_index = new List<Vector2>();
                while (m_cells_index.Count < m) {
                    // TODO 長さランダマイズ

                    int tmpx = (int)Random.Range(tmp_leader.x - 5, tmp_leader.x + 5);
                    int tmpy = (int)Random.Range(tmp_leader.y - 5, tmp_leader.y + 5);

                    // セルリーダー重複判定
                    if (tmpx == leader_pos.x && tmpy == leader_pos.y) {
                        continue;
                    }

                    // セル範囲外判定
                    if (tmpx < 0 || tmpx >= width || tmpy < 0 || tmpy >= height) {
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

                    m_cells_index.Add(new Vector2(tmpx, tmpy));
                }

                // 2.進路先の決定
                float total_potential = 0f;
                for (int j = 0; j < m_cells_index.Count; j++) {
                    total_potential += Mathf.Pow(potential[(int)m_cells_index[j].y, (int)m_cells_index[j].x], eta);
                }

                float[] possibilities = new float[m_cells_index.Count];
                for (int j = 0; j < m_cells_index.Count; j++) {
                    possibilities[j] = Mathf.Pow(potential[(int)m_cells_index[j].y, (int)m_cells_index[j].x], eta) / total_potential;
                }

                // ソート(先頭が一番大きい)
                for (int start = 1; start < possibilities.Length; start++) {
                    for (int end = possibilities.Length - 1; end >= start; end--) {
                        if (possibilities[end - 1] < possibilities[end]) {
                            // 確率に基づいて確率配列を祖ソート
                            float tmp = possibilities[end - 1];
                            possibilities[end - 1] = possibilities[end];
                            possibilities[end] = tmp;

                            // 同様にセルインデックスもソート
                            Vector2 tmpv = m_cells_index[end - 1];
                            m_cells_index[end - 1] = m_cells_index[end];
                            m_cells_index[end] = tmpv;
                        }
                    }
                }

                // 最大値に等しいもしくは近い値はリーダー分岐する
                leaders.Enqueue(m_cells_index[0]);
                for (int j = 1; j < m_cells_index.Count; j++) {
                    if (possibilities[0] - possibilities[j] < branch) {
                        leaders.Enqueue(m_cells_index[j]);
                    }
                }
                
            }
            

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

