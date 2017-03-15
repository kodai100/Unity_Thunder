using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewLightning2D : MonoBehaviour {

    Texture2D lightning_texture;
    float[,] potential, lightning;

    public enum Mode {
        Cloud, Tower, Circle
    }
    public Mode mode = Mode.Cloud;
    public int towerPos = 200;

    #region Laplace
    #region GPU
    const int SIMULATION_BLOCK_SIZE = 256;
    int threadGroupSize;
    int bufferSize;
    public ComputeShader LaplaceCS_1;   // Phase1 odd
    public ComputeShader LaplaceCS_2;   // Phase2 even
    ComputeBuffer potential_buffer_read, potential_buffer_write, phase1_to_2;
    #endregion GPU

    [Range(0f, 10f)] public float left_strength;
    [Range(0f, 10f)] public float right_strength;
    [Range(0f, 10f)] public float up_strength;
    [Range(0f, 10f)] public float bottom_strength;

    public float sor_coef = 1.8f;
    public int iterPerStep = 10;
    float[] potential_read, potential_write;
    #endregion Laplace

    public int width, height;
    public bool dispQuantity = true;
    public int m = 3;
    public int eta = 1;
    public float branch = 0.00001f;
    XORShift random;

    void Awake() {

        random = new XORShift();

        // Initialization Of Laplace Equation Solver Variables
        bufferSize = width * height;
        potential_read = new float[bufferSize];
        potential_write = new float[bufferSize];
        threadGroupSize = Mathf.CeilToInt(bufferSize / SIMULATION_BLOCK_SIZE) + 1;
        potential_buffer_read = new ComputeBuffer(bufferSize, sizeof(float));
        potential_buffer_write = new ComputeBuffer(bufferSize, sizeof(float));
        phase1_to_2 = new ComputeBuffer(bufferSize, sizeof(float));

        lightning_texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        lightning_texture.filterMode = FilterMode.Point;
    }

    void Start() {

        // Initialization Of Lightning Simulation Visualizer Variables
        lightning = new float[height, width];
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                lightning[y, x] = 0f;
            }
        }

        SetBoundaryCondition();

        leaders.Enqueue(new Vector2(width / 4f, 0));
        leaders.Enqueue(new Vector2(width/2f, 0));
        leaders.Enqueue(new Vector2(width * 3f / 4f, 0));

        // Update内でループ
        processing = true;
    }



    Queue<Vector2> leaders = new Queue<Vector2>();
    bool land = false;
    int c = 0;
    bool processing = false;

    void Update() {

        if(mode == Mode.Tower) {
            for(int i = 0; i<height/2; i++) {
                potential[height-1 - i, towerPos] = 1f;
                potential[height - 1 - i, towerPos+1] = 1f;
                potential[height - 1 - i, towerPos + 2] = 1f;
                potential[height - 1 - i, towerPos -1] = 1f;
                potential[height - 1 - i, towerPos-2] = 1f;

                lightning[height - 1 - i, towerPos] = 1f;
                lightning[height - 1 - i, towerPos + 1] = 1f;
                lightning[height - 1 - i, towerPos + 2] = 1f;
                lightning[height - 1 - i, towerPos - 1] = 1f;
                lightning[height - 1 - i, towerPos - 2] = 1f;
            }
        }

        if (processing == true) {
            c++;

            // ラプラス方程式のイテレーションを10程度進める
            LaplaceProcess();

            for (int i = 0; i < leaders.Count; i++) {

                Vector2 tmp_leader = leaders.Dequeue();
                lightning[(int)tmp_leader.y, (int)tmp_leader.x] = 1f;
                potential[(int)tmp_leader.y, (int)tmp_leader.x] = 0f;   // 通過した地点のポテンシャルを0に

                if (tmp_leader.y == height - 1 || tmp_leader.x == 0 || tmp_leader.x == width - 1) {
                    leaders.Clear();
                    land = true;
                    processing = false;
                    break;
                }
                

                // 1. 周辺のセルからランダムにM個セルを選ぶ
                List<Vector2> m_cells_index = new List<Vector2>();
                while (m_cells_index.Count < m) {
                    // TODO 長さランダマイズ

                    int tmpx = (int)random.Range(tmp_leader.x - 2, tmp_leader.x + 2);
                    int tmpy = (int)random.Range(tmp_leader.y, tmp_leader.y + 2);

                    // セルリーダー重複判定
                    if (tmpx == tmp_leader.x && tmpy == tmp_leader.y) {
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

                // Loop over hop sites until the chosen hop site is found.
                //float rnd = random.xor();
                //float sum = 0f;
                //for (int k = 0; k < possibilities.Length; k++) {
                //    Debug.Log(rnd + ", " + possibilities[k]);
                //    if (rnd >= sum && rnd < sum + possibilities[k]) {

                //        leaders.Enqueue(m_cells_index[k]);
                //        break;
                //    }
                //    sum += possibilities[k];
                //}


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

            ApplyTexture();
        } else {
            if (Input.GetKeyUp(KeyCode.I)) {
                Start();
            }
        }
    }

    void OnGUI() {
        GUI.DrawTexture(new Rect(new Vector2(0, 0), new Vector2(width, height)), lightning_texture);
    }

    void OnDestroy() {
        potential_buffer_read.Release();
        potential_buffer_write.Release();
        phase1_to_2.Release();
    }

    void SetBoundaryCondition() {
        potential = new float[height, width];

        // 初期化
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                potential[y, x] = 0.5f;
            }
        }

        // 境界値
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                if (y == 0) potential[y, x] = up_strength;
                if (y == height - 1) potential[y, x] = bottom_strength;
                if(x == 0) potential[y, x] = left_strength;
                if (x == width - 1) potential[y, x] = right_strength;
            }
        }

    }

    void LaplaceProcess() {

        // set current potential buffer
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                potential_read[y * width + x] = potential[y, x];
            }
        }

        potential_buffer_read.SetData(potential_read);
        potential_buffer_write.SetData(potential_read);

        for (int i = 0; i<iterPerStep; i++) {
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

            SwapBuffer();
        }

        potential_buffer_write.GetData(potential_read);

        ApplyPotential();
    }

    void SwapBuffer() {
        ComputeBuffer tmp = potential_buffer_write;
        potential_buffer_write = potential_buffer_read;
        potential_buffer_read = tmp;
    }

    void ApplyPotential() {
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                potential[y, x] = potential_read[y * width + x];
            }
        }
    }

    void ApplyTexture() {
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                if(dispQuantity) lightning_texture.SetPixel(x, (height - 1) - y, new Color(lightning[y, x] + potential[y, x]*0.5f, lightning[y, x] + potential[y, x] * 0.5f, lightning[y, x] + potential[y, x] * 0.5f));
                else lightning_texture.SetPixel(x, (height - 1) - y, new Color(lightning[y, x], lightning[y, x], lightning[y, x]));
            }
        }
        lightning_texture.Apply();
    }

}

