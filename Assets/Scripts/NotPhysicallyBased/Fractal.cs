using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fractal : MonoBehaviour {

    List<Vector3> points;
    public float power = 1f;

    public int iter_max = 1;

    public Vector3 start = new Vector3(0, 0, 0);
    public Vector3 end = new Vector3(1, 0, 0);

    #region Rendering
    Mesh mesh;
    public Material displayMat;
    MaterialPropertyBlock _block;
    public MaterialPropertyBlock block {
        get {
            if (_block == null) {
                _block = new MaterialPropertyBlock();
            }
            return _block;
        }
    }
    #endregion rendering

    void Start() {
        
    }

    Mesh BuildMesh() {
        Mesh particleMesh = new Mesh();

        var vertices = new Vector3[points.Count];
        var uvs = new Vector2[points.Count];
        var indices = new int[points.Count];

        for (int i = 0; i < points.Count; i++) {
            vertices[i] = new Vector3(points[i].x, points[i].y, 0);
            uvs[i] = new Vector2(0, 0);
            indices[i] = i;
        }

        particleMesh.vertices = vertices;
        particleMesh.uv = uvs;

        particleMesh.SetIndices(indices, MeshTopology.LineStrip, 0);

        return particleMesh;
    }

    void Update() {
        points = new List<Vector3>();

        points.Add(transform.position + start);
        RecursiveCenter(iter_max, transform.position + start, transform.position + end);
        points.Add(transform.position + end);

        mesh = BuildMesh();
        Graphics.DrawMesh(mesh, transform.localToWorldMatrix, displayMat, 0, null, 0, block);
    }

    void RecursiveCenter(int n, Vector3 p1, Vector3 p2) {

        if (n == 0) return;

        Vector3 center = (p2 + p1) / 2f + new Vector3(0, power * Random.Range(-1f, 1f), 0);

        RecursiveCenter(n - 1, p1, center);
        points.Add(center); // ここ
        RecursiveCenter(n - 1, center, p2);
    }

    void OnDrawGizmos() {
        if (Application.isPlaying) {
            for (int i = 0; i < points.Count; i++) {
                int next = (i + 1) % points.Count;
                if (next == 0) break;
                Gizmos.DrawLine(points[i], points[next]);
            }
        }
    }
}
