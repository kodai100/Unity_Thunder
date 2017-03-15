using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum State {
    LIGHTNING, EMPTY, CANDIDATE
}

public struct CellState {
    public int id;
    public float potential;
    public State state;

    public CellState(int id, float potential, State state){
        this.id = id;
        this.potential = potential;
        this.state = state;
    }
};

public class FinalLightning2D : MonoBehaviour {

    Texture2D lightning_texture;
    CellState[,] cells;

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
    
    bool land = false;
    int iter = 0;
    bool processing = false;

    void Awake() {
        InitializeLightning();
        InitializeLaplaceSolver();
    }

    void Start() {

        SetBoundaryCondition();

        cells[0, width / 2].state = State.LIGHTNING;

        // Update内でループ
        processing = true;

        ApplyTexture();
    }

    void Update() {

        if (!land) {

            // 全セルについて候補点の計算
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {

                    if (cells[y, x].state == State.LIGHTNING) {
                        // 
                        if (y == height - 1 || x == 0 || x == width - 1) {

                            land = true;
                            break;
                        }

                        // North cell (初期位置を考慮してy判定も入れておく)
                        if(y - 1 > 0) {
                            if (cells[x, y - 1].state != State.LIGHTNING) cells[x, y - 1].state = State.CANDIDATE;
                        }
                        // South cell
                        if (cells[x, y + 1].state != State.LIGHTNING) cells[x, y + 1].state = State.CANDIDATE;
                        // East cell
                        if (cells[x + 1, y].state != State.LIGHTNING) cells[x + 1, y].state = State.CANDIDATE;
                        // West cell
                        if (cells[x - 1, y].state != State.LIGHTNING) cells[x - 1, y].state = State.CANDIDATE;

                    }
                }
            }

            // 候補点の総和と確率を算出
            float sum = 0;
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    if (cells[y, x].state == State.CANDIDATE) {
                        sum += cells[y, x].potential;
                    }
                }
            }

            // 最も確率の高いセルを選ぶ
            float tmp_max_possibility = 0;
            int tmp_max_possibility_cell_idx = 0;   // ???
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    if(cells[y,x].state == State.CANDIDATE) {
                        float possibility = Mathf.Pow(cells[y, x].potential, eta) / sum;
                        if(tmp_max_possibility < possibility) {
                            tmp_max_possibility = possibility;
                            tmp_max_possibility_cell_idx = cells[y, x].id;
                        }
                    }
                }
            }

            // TODO 最大値に近いセルも選ぶ

            // 最大値のセルのStateをLightningに変える
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    if (cells[y, x].id == tmp_max_possibility_cell_idx) {
                        cells[y, x].state = State.LIGHTNING;
                    }
                }
            }
            
            ApplyTexture();

            if (iter > 10000) processing = false;
            else iter++;

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

    void InitializeLightning() {
        random = new XORShift();
        lightning_texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        lightning_texture.filterMode = FilterMode.Point;

        cells = new CellState[height, width];
        for(int y = 0; y<height; y++) {
            for(int x = 0; x<width; x++) {
                cells[y, x] = new CellState(y * width + x, 0f, State.EMPTY);
            }
        }
    }

    void InitializeLaplaceSolver() {
        bufferSize = width * height;
        potential_read = new float[bufferSize];
        potential_write = new float[bufferSize];
        threadGroupSize = Mathf.CeilToInt(bufferSize / SIMULATION_BLOCK_SIZE) + 1;
        potential_buffer_read = new ComputeBuffer(bufferSize, sizeof(float));
        potential_buffer_write = new ComputeBuffer(bufferSize, sizeof(float));
        phase1_to_2 = new ComputeBuffer(bufferSize, sizeof(float));
    }

    void SetBoundaryCondition() {

        // 初期化
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                cells[y, x].potential = 0.5f;
            }
        }

        // 境界値
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                if (y == 0) cells[y, x].potential = up_strength;
                if (y == height - 1) cells[y, x].potential = bottom_strength;
                if (x == 0) cells[y, x].potential = left_strength;
                if (x == width - 1) cells[y, x].potential = right_strength;
            }
        }

    }

    void LaplaceProcess() {

        // set current potential buffer
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                potential_read[y * width + x] = cells[y, x].potential;
            }
        }

        potential_buffer_read.SetData(potential_read);
        potential_buffer_write.SetData(potential_read);

        for (int i = 0; i < iterPerStep; i++) {
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
                cells[y, x].potential = potential_read[y * width + x];
            }
        }
    }

    void ApplyTexture() {

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                if(cells[y,x].state == State.LIGHTNING) {
                    lightning_texture.SetPixel(x, (height - 1) - y, new Color(1,1,1));
                } else if(cells[y,x].state == State.CANDIDATE) {
                    lightning_texture.SetPixel(x, (height - 1) - y, new Color(0, 0, 1));
                } else {
                    lightning_texture.SetPixel(x, (height - 1) - y, new Color(cells[y, x].potential, cells[y, x].potential, cells[y, x].potential));
                }
            }
        }
        lightning_texture.Apply();
    }

}

