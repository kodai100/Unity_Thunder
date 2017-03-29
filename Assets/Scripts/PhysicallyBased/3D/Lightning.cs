using UnityEngine;
using System.Runtime.InteropServices;

namespace Lightning3D {

    struct Cell {
        public bool isBoundary;
        public int state;   // 0: none, 1: candidate, 2: lightning
        public float potential;
        public Vector3 idx;
        public Vector3 pos;
    }

    public class Lightning : MonoBehaviour {

        Cell[] cells;

        #region GPU
        const int SIMULATION_BLOCK_SIZE = 32;
        int threadGroupSize;
        int bufferSize;
        public ComputeShader LightningCS;
        ComputeBuffer bufferRead, bufferWrite;
        static int LIGHTNING = 2;
        static int CANDIDATE = 1;
        static int NONE = 0;
        #endregion GPU

        public int width = 30;
        public int height = 30;
        public int depth = 30;

        public int laplace_iter = 4;
        public int eta = 1;
        public float possibility = 0.5f;
        public int rod_height = 1;

        [Range(0f, 1f)] public float front_strength;
        [Range(0f, 1f)] public float back_strength;
        [Range(0f, 1f)] public float left_strength;
        [Range(0f, 1f)] public float right_strength;
        [Range(0f, 1f)] public float up_strength;
        [Range(0f, 1f)] public float down_strength;

        float time;
        bool land;

        void Start() {

            cells = new Cell[width * height * depth];
            bufferSize = cells.Length;
            time = 0;
            
            threadGroupSize = Mathf.CeilToInt(bufferSize / SIMULATION_BLOCK_SIZE) + 1;
            bufferRead = new ComputeBuffer(bufferSize, Marshal.SizeOf(typeof(Cell)));
            bufferWrite = new ComputeBuffer(bufferSize, Marshal.SizeOf(typeof(Cell)));

            SetBoundaryCondition();

            InitializeLightning();

            bufferRead.SetData(cells);
            bufferWrite.SetData(cells);
        }

        void Update() {

            time += Time.deltaTime;

            if (!land) {
                LightningProcess();
            }
            

        }

        void OnDestroy() {
            bufferRead.Release();
            bufferWrite.Release();
        }

        void SetBoundaryCondition() {
            
            for(int z = 0; z < depth; z++) {
                for(int y = 0; y < height; y++) {
                    for(int x = 0; x < width; x++) {
                        int idx = width * height * z + width * y + x;
                        cells[idx].pos = new Vector3(x/ (float)width, y/ (float)height, z/ (float)depth);
                        cells[idx].idx = new Vector3(x, y, z);

                        cells[idx].potential = 0.5f;
                        cells[idx].state = NONE;
                        cells[idx].isBoundary = false;
                        // front
                        if (z == 0) {
                            cells[idx].potential = front_strength;
                            cells[idx].isBoundary = true;
                        }
                        // back
                        if (z == depth - 1) {
                            cells[idx].potential = back_strength;
                            cells[idx].isBoundary = true;
                        }
                        // left
                        if (x == 0) {
                            cells[idx].potential = left_strength;
                            cells[idx].isBoundary = true;
                        }
                        // right
                        if (x == width - 1) {
                            cells[idx].potential = right_strength;
                            cells[idx].isBoundary = true;
                        }
                        // down
                        if (y == 0) {
                            cells[idx].potential = down_strength;
                            cells[idx].isBoundary = true;
                        }
                        // up
                        if (y == height - 1) {
                            cells[idx].potential = up_strength;
                            cells[idx].isBoundary = true;
                        }
                    }
                }
            }
            
        }

        void InitializeLightning() {
            for(int i = 1; i<rod_height; i++) {
                cells[CalcArrayIdx(width / 2, height - i, depth / 2)].state = LIGHTNING;
            }
            
        }

        void LightningProcess() {
            
            for(int i = 0; i < laplace_iter; i++) {
                CalcLaplace();
            }

            // 候補点の計算
            CalcCandidate();

            // 候補点の総和と確率を算出
            bufferWrite.GetData(cells);
            float sum = 0;
            for (int i = 0; i < cells.Length; i++) {
                if (cells[i].state == CANDIDATE) {
                    sum += cells[i].potential;
                }
            }

            Breakdown(sum);

            SwapBuffer();

            LandCheck();

        }

        void CalcLaplace() {
            
            LightningCS.SetInt("WIDTH", width);
            LightningCS.SetInt("HEIGHT", height);
            LightningCS.SetInt("DEPTH", depth);

            int kernel = LightningCS.FindKernel("Laplace3D");
            LightningCS.SetBuffer(kernel, "_Read", bufferRead);
            LightningCS.SetBuffer(kernel, "_Write", bufferWrite);

            LightningCS.Dispatch(kernel, threadGroupSize, 1, 1);

        }

        void CalcCandidate() {
            int kernel = LightningCS.FindKernel("CalcCandidate");
            LightningCS.SetBuffer(kernel, "_Read", bufferRead);
            LightningCS.SetBuffer(kernel, "_Write", bufferWrite);
            LightningCS.Dispatch(kernel, threadGroupSize, 1, 1);
        }

        void Breakdown(float candidate_potential_sum) {
            LightningCS.SetFloat("_CandidatePotentialSum", candidate_potential_sum);
            LightningCS.SetInt("_Eta", eta);
            LightningCS.SetFloat("_Time", time);
            LightningCS.SetFloat("_Possibility", possibility);

            int kernel = LightningCS.FindKernel("Breakdown");
            LightningCS.SetBuffer(kernel, "_Read", bufferRead);
            LightningCS.SetBuffer(kernel, "_Write", bufferWrite);

            LightningCS.Dispatch(kernel, threadGroupSize, 1, 1);
        }

        void LandCheck() {
            bufferRead.GetData(cells);
            for (int z = 0; z < depth; z++) {
                for (int y = 0; y < height; y++) {
                    for (int x = 0; x < width; x++) {
                        if (y == 1) {
                            int idx = width * height * z + width * y + x;
                            if (cells[idx].state == LIGHTNING) {
                                land = true;
                                break;
                            }
                        }
                        if (x == 1) {
                            int idx = width * height * z + width * y + x;
                            if (cells[idx].state == LIGHTNING) {
                                land = true;
                                break;
                            }
                        }
                        if (x == width - 2) {
                            int idx = width * height * z + width * y + x;
                            if (cells[idx].state == LIGHTNING) {
                                land = true;
                                break;
                            }
                        }
                        if (z == 1) {
                            int idx = width * height * z + width * y + x;
                            if (cells[idx].state == LIGHTNING) {
                                land = true;
                                break;
                            }
                        }
                        if (z == depth - 1) {
                            int idx = width * height * z + width * y + x;
                            if (cells[idx].state == LIGHTNING) {
                                land = true;
                                break;
                            }
                        }
                    }
                }
            }
        }

        void SwapBuffer() {
            ComputeBuffer tmp = bufferWrite;
            bufferWrite = bufferRead;
            bufferRead = tmp;
        }

        int CalcArrayIdx(int x, int y, int z) {
            return z * width * height + y * width + x; 
        }

        public ComputeBuffer GetBuffer() {
            return bufferRead;
        }

        public int GetBufferSize() {
            return bufferSize;
        }
    }
}