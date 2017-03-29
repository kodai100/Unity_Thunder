using UnityEngine;

namespace Lightning3D {

    public class Render : MonoBehaviour {

        public Lightning GPUScript;

        public Material ParticleRenderMat;

        void OnRenderObject() {
            DrawObject();
        }

        void DrawObject() {
            Material m = ParticleRenderMat;
            m.SetPass(0);
            m.SetBuffer("_Cells", GPUScript.GetBuffer());
            Graphics.DrawProcedural(MeshTopology.Points, GPUScript.GetBufferSize());
        }

    }

}