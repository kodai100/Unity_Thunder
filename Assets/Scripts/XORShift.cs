using UnityEngine;

public class XORShift {

    public static Vector4 seed = new Vector4(123456789, 362436069, 521288629, 88675123);

    public XORShift() {
        seed = new Vector4(Random.Range(0, 100000000), Random.Range(0, 100000000), Random.Range(0, 100000000), Random.Range(0, 100000000));
    }

    public float xor() {
        return xor128() / (float)uint.MaxValue;
    }

    public float Range(float min, float max) {
        float range = max - min;
        return xor() * range + min;
    }

    float xor128() {
        uint t = ((uint)seed.x ^ ((uint)seed.x << 11));
        seed.x = (uint)seed.y;
        seed.y = (uint)seed.z;
        seed.z = (uint)seed.w;
        return (seed.w = ((uint)seed.w ^ ((uint)seed.w >> 19)) ^ (t ^ (t >> 8)));
    }
}
