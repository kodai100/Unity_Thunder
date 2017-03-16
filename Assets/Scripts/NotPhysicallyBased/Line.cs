using UnityEngine;

public class Line {
    public Vector2 A;
    public Vector2 B;
    public float Thickness;

    public Line() { }
    public Line(Vector2 a, Vector2 b, float thickness = 1) {
        A = a;
        B = b;
        Thickness = thickness;
    }
}
