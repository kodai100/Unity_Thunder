
float xor128(float4 seed) {
	uint t = ((uint)seed.x ^ ((uint)seed.x << 11));
	seed.x = (uint)seed.y;
	seed.y = (uint)seed.z;
	seed.z = (uint)seed.w;
	return (seed.w = ((uint)seed.w ^ ((uint)seed.w >> 19)) ^ (t ^ (t >> 8)));
}

float xor(float4 seed) {
	return xor128(seed) / (float)4294967295;
}

