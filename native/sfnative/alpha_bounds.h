/* StutterFix original MIT code. Read-only alpha bounds, RGBA/ARGB, SSE2.
 * Coordinates follow the caller's row order; output is x/y/width/height.
 * No decoding, allocation, texture replacement or sprite mutation here. */
SF_API int sf_alpha_bounds_v1(const uint8_t* pixels, size_t bytes, int width,
                            int height, int stride, int alpha, int* bounds)
{
    if (!bounds) return 0;
    bounds[0] = bounds[1] = bounds[2] = bounds[3] = 0;
    if (!pixels || width <= 0 || height <= 0 || stride <= 0 || alpha < 0 || alpha > 3) return 0;
    uint64_t row = (uint64_t)(unsigned)width * 4;
    uint64_t required = (uint64_t)(unsigned)(height - 1) * (unsigned)stride + row;
    if (row > (unsigned)stride || required > bytes) return 0;
    int min_x = width, max_x = -1, min_y = height, max_y = -1;
    __m128i mask = _mm_set1_epi32((int)(255u << (alpha * 8)));
    __m128i zero = _mm_setzero_si128();
    for (int y = 0; y < height; ++y)
    {
        const uint8_t* line = pixels + (size_t)y * (unsigned)stride;
        int left = width, right = -1, x = 0;
        for (; x <= width - 4; x += 4)
        {
            __m128i v = _mm_loadu_si128((const __m128i*)(line + (size_t)x * 4));
            int transparent = _mm_movemask_ps(_mm_castsi128_ps(_mm_cmpeq_epi32(_mm_and_si128(v, mask), zero)));
            if (transparent == 15) continue;
            for (int k = 0; k < 4; ++k)
                if (!(transparent & (1 << k)))
                { if (x + k < left) left = x + k; right = x + k; }
        }
        for (; x < width; ++x)
            if (line[(size_t)x * 4 + alpha]) { if (x < left) left = x; right = x; }
        if (right < 0) continue;
        if (left < min_x) min_x = left;
        if (right > max_x) max_x = right;
        if (y < min_y) min_y = y;
        max_y = y;
    }
    if (max_x >= 0)
    {
        bounds[0] = min_x; bounds[1] = min_y;
        bounds[2] = max_x - min_x + 1; bounds[3] = max_y - min_y + 1;
    }
    return 1;
}
