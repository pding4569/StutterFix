// sfdxt.ispc - the DXT1/DXT5 encoder of sfnative.c (encode_color / encode_alpha), one 4x4 block per SIMD lane.
//
// Same algorithm and the same integer/float operations in the same order as sfnative.c, so the output is meant to be
// byte-identical (checked block by block against the C code on every PNG in the map folders; see README).
// Build with --opt=disable-fma: fused multiply-add would round the principal-axis search differently from the C code.
// Written for StutterFix (MIT). Build: native/build-sfnative.bat

typedef unsigned int8 u8;

#define NEAR_SOLID 12
#define SOLID_MAX_RANGE 48

// x / 255, x / 3, x / 7 as multiply + shift: exact for every value these call sites can produce
// (checked exhaustively: x <= 16192 for 255, x <= 765 for 3, x <= 1785 for 7).
static inline int div255(int x) { return (int)(((unsigned int32)x * 32897u) >> 23); }
static inline int div3(int x) { return (int)(((unsigned int32)x * 43691u) >> 17); }
static inline int div7(int x) { return (int)(((unsigned int32)x * 9363u) >> 16); }

static inline int to565(int r, int g, int b)
{
    return (div255(r * 31 + 127) << 11) | (div255(g * 63 + 127) << 5) | div255(b * 31 + 127);
}

static inline void expand(int c, int &r, int &g, int &b)
{
    int r5 = (c >> 11) & 31, g6 = (c >> 5) & 63, b5 = c & 31;
    r = (r5 << 3) | (r5 >> 2); g = (g6 << 2) | (g6 >> 4); b = (b5 << 3) | (b5 >> 2);
}

static int match(const int R[16], const int G[16], const int B[16], int c0, int c1, unsigned int32 &idxOut)
{
    int r0, g0, b0, r1, g1, b1;
    expand(c0, r0, g0, b0); expand(c1, r1, g1, b1);
    int dr = r0 - r1, dg = g0 - g1, db = b0 - b1;
    int len2 = dr * dr + dg * dg + db * db;
    int r2 = div3(2 * r0 + r1), g2 = div3(2 * g0 + g1), b2 = div3(2 * b0 + b1);
    int r3 = div3(r0 + 2 * r1), g3 = div3(g0 + 2 * g1), b3 = div3(b0 + 2 * b1);
    unsigned int32 idx = 0; int total = 0;
    int y1 = 2 * len2, y2 = 4 * len2, y3 = 6 * len2;
    for (uniform int i = 15; i >= 0; i--)
    {
        int r = R[i], g = G[i], b = B[i], s = 3, er, eg, eb; unsigned int32 best;
        if (len2 > 0)
        {
            int x = ((r - r1) * dr + (g - g1) * dg + (b - b1) * db) * 6 + len2;
            s = x < y1 ? 0 : x < y2 ? 1 : x < y3 ? 2 : 3;
        }
        if (s <= 0) { best = 1; er = r - r1; eg = g - g1; eb = b - b1; }
        else if (s == 1) { best = 3; er = r - r3; eg = g - g3; eb = b - b3; }
        else if (s == 2) { best = 2; er = r - r2; eg = g - g2; eb = b - b2; }
        else { best = 0; er = r - r0; eg = g - g0; eb = b - b0; }
        total += er * er + eg * eg + eb * eb;
        idx = (idx << 2) | best;
    }
    idxOut = idx;
    return total;
}

// Same result as the C divr: rounded n / d clamped to 0..255. A negative quotient always clamps to 0, and for 0 <= a < 2^31,
// 0 < b < 2^31 the truncated double quotient (int)((double)a / b) equals a / b exactly (the gap to the next integer, >= 1/b,
// is larger than the double rounding error), so the integer division that ISPC would scalarize becomes one vector divide.
static inline int divr(int n, int d)
{
    if (d < 0) { n = -n; d = -d; }
    if (n < 0) return 0;
    int q = (int)((double)(n + (d >> 1)) / (double)d);
    return q > 255 ? 255 : q;
}

static bool refine(const int R[16], const int G[16], const int B[16], unsigned int32 idx, int &c0, int &c1)
{
    int aa = 0, bb = 0, ab = 0, axr = 0, axg = 0, axb = 0, bxr = 0, bxg = 0, bxb = 0;
    for (uniform int i = 0; i < 16; i++)
    {
        int k = (int)((idx >> (2 * i)) & 3);
        int a = k == 0 ? 3 : k == 1 ? 0 : k == 2 ? 2 : 1;
        int b = 3 - a;
        int r = R[i], g = G[i], bl = B[i];
        aa += a * a; bb += b * b; ab += a * b;
        axr += a * r; axg += a * g; axb += a * bl;
        bxr += b * r; bxg += b * g; bxb += b * bl;
    }
    int det = aa * bb - ab * ab;
    c0 = 0; c1 = 0;
    if (det == 0) return false;
    c0 = to565(divr(3 * (axr * bb - bxr * ab), det), divr(3 * (axg * bb - bxg * ab), det), divr(3 * (axb * bb - bxb * ab), det));
    c1 = to565(divr(3 * (bxr * aa - axr * ab), det), divr(3 * (bxg * aa - axg * ab), det), divr(3 * (bxb * aa - axb * ab), det));
    return true;
}

static void encode_color(const int R[16], const int G[16], const int B[16],
                         uniform const u8 O5a[], uniform const u8 O5b[], uniform const u8 O6a[], uniform const u8 O6b[],
                         int &outC0, int &outC1, unsigned int32 &outIdx)
{
    int sr = 0, sg = 0, sb = 0, srr = 0, srg = 0, srb = 0, sgg = 0, sgb = 0, sbb = 0;
    int minR = 255, minG = 255, minB = 255, maxR = 0, maxG = 0, maxB = 0;
    for (uniform int i = 0; i < 16; i++)
    {
        int r = R[i], g = G[i], b = B[i];
        sr += r; sg += g; sb += b;
        srr += r * r; srg += r * g; srb += r * b; sgg += g * g; sgb += g * b; sbb += b * b;
        if (r < minR) minR = r; if (r > maxR) maxR = r; if (g < minG) minG = g; if (g > maxG) maxG = g; if (b < minB) minB = b; if (b > maxB) maxB = b;
    }
    int c0, c1; unsigned int32 idx;
    if (minR == maxR && minG == maxG && minB == maxB)
    {
        c0 = ((int)O5a[minR] << 11) | ((int)O6a[minG] << 5) | (int)O5a[minB];
        c1 = ((int)O5b[minR] << 11) | ((int)O6b[minG] << 5) | (int)O5b[minB];
        idx = 0xAAAAAAAAu;
    }
    else
    {
        int range = (maxR - minR) + (maxG - minG) + (maxB - minB);
        if (range <= NEAR_SOLID)
        {
            int ar0 = (sr + 8) >> 4, ag0 = (sg + 8) >> 4, ab0 = (sb + 8) >> 4;
            c0 = ((int)O5a[ar0] << 11) | ((int)O6a[ag0] << 5) | (int)O5a[ab0];
            c1 = ((int)O5b[ar0] << 11) | ((int)O6b[ag0] << 5) | (int)O5b[ab0];
            match(R, G, B, c0, c1, idx);
        }
        else
        {
            float crr = (float)(16 * srr - sr * sr), crg = (float)(16 * srg - sr * sg), crb = (float)(16 * srb - sr * sb);
            float cgg = (float)(16 * sgg - sg * sg), cgb = (float)(16 * sgb - sg * sb), cbb = (float)(16 * sbb - sb * sb);
            float vr = (float)(maxR - minR), vg = (float)(maxG - minG), vb = (float)(maxB - minB);
            for (uniform int it = 0; it < 4; it++)
            {
                float nr = vr * crr + vg * crg + vb * crb, ng = vr * crg + vg * cgg + vb * cgb, nb = vr * crb + vg * cgb + vb * cbb;
                float anr = nr < 0 ? -nr : nr, ang = ng < 0 ? -ng : ng, anb = nb < 0 ? -nb : nb;
                float m = anr > ang ? anr : ang; if (anb > m) m = anb;
                if (m < 1e-6f) break;
                vr = nr / m; vg = ng / m; vb = nb / m;
            }
            int ar = (int)(vr * 256), ag = (int)(vg * 256), ab = (int)(vb * 256);
            int lo = 0x7FFFFFFF, hi = (int)0x80000000, li = 0, hiI = 0;
            for (uniform int i = 0; i < 16; i++)
            {
                int d = R[i] * ar + G[i] * ag + B[i] * ab;
                if (d < lo) { lo = d; li = i; }
                if (d > hi) { hi = d; hiI = i; }
            }
            c0 = to565(R[hiI], G[hiI], B[hiI]);
            c1 = to565(R[li], G[li], B[li]);
            int err = match(R, G, B, c0, c1, idx);
            if (range <= SOLID_MAX_RANGE)
            {
                int ar2 = (sr + 8) >> 4, ag2 = (sg + 8) >> 4, ab2 = (sb + 8) >> 4;
                int s0 = ((int)O5a[ar2] << 11) | ((int)O6a[ag2] << 5) | (int)O5a[ab2];
                int s1 = ((int)O5b[ar2] << 11) | ((int)O6b[ag2] << 5) | (int)O5b[ab2];
                unsigned int32 sidx; int serr = match(R, G, B, s0, s1, sidx);
                if (serr < err) { c0 = s0; c1 = s1; idx = sidx; err = serr; }
            }
            // one refine pass (RefinePasses = 1 in sfnative.c)
            int n0, n1;
            if (refine(R, G, B, idx, n0, n1))
            {
                unsigned int32 nidx; int nerr = match(R, G, B, n0, n1, nidx);
                if (nerr < err) { c0 = n0; c1 = n1; idx = nidx; err = nerr; }
            }
        }
    }
    // put_color
    if (c0 < c1) { int t = c0; c0 = c1; c1 = t; idx ^= 0x55555555u; }
    else if (c0 == c1) idx = 0;
    outC0 = c0; outC1 = c1; outIdx = idx;
}

static int alpha_try(const int A[16], int max, int min, unsigned int64 &bitsOut)
{
    unsigned int64 bits = 0; int range = max - min, total = 0;
    if (range <= 0)
    {
        for (uniform int i = 0; i < 16; i++) total += (A[i] - max) * (A[i] - max);
        bitsOut = 0;
        return total;
    }
    int y = 2 * range;
    for (uniform int i = 15; i >= 0; i--)
    {
        int a = A[i];
        int x = (max - a) * 14 + range, s = 0;
        // min(7, x / y) for x >= 0 (0 when x < y): count k in 1..7 with k * y <= x
        s = (x >= y ? 1 : 0) + (x >= 2 * y ? 1 : 0) + (x >= 3 * y ? 1 : 0) + (x >= 4 * y ? 1 : 0) + (x >= 5 * y ? 1 : 0) + (x >= 6 * y ? 1 : 0) + (x >= 7 * y ? 1 : 0);
        int v = div7((7 - s) * max + s * min);
        int best = s == 0 ? 0 : s == 7 ? 1 : s + 1;
        total += (a - v) * (a - v);
        bits = (bits << 3) | (unsigned int64)best;
    }
    bitsOut = bits;
    return total;
}

static void encode_alpha(const int A[16], int &outMax, int &outMin, unsigned int64 &outBits)
{
    int min = 255, max = 0;
    for (uniform int i = 0; i < 16; i++) { int a = A[i]; if (a < min) min = a; if (a > max) max = a; }
    unsigned int64 bits; int e1 = alpha_try(A, max, min, bits);
    int inset = (max - min) >> 5;
    if (inset > 0)
    {
        unsigned int64 b2; int e2 = alpha_try(A, max - inset, min + inset, b2);
        if (e2 < e1) { bits = b2; max -= inset; min += inset; }
    }
    if (max == min) bits = 0;
    outMax = max; outMin = min; outBits = bits;
}

// layout: 0 = RGBA32, 1 = ARGB32, 2 = RGB24. Compresses block rows by0..by1-1; dst is the start of the whole output.
export void sf_dxt_rows_ispc(uniform const u8 src[], uniform int w, uniform int h, uniform int layout, uniform int dxt5, uniform u8 dst[],
                             uniform int by0, uniform int by1,
                             uniform const u8 O5a[], uniform const u8 O5b[], uniform const u8 O6a[], uniform const u8 O6b[])
{
    uniform int bpp = layout == 2 ? 3 : 4;
    uniform int ro = layout == 1 ? 1 : 0;
    uniform int go = ro + 1, bo = ro + 2, ao = layout == 1 ? 0 : 3;
    uniform int bw = w / 4;
    uniform int64 rowStride = (uniform int64)w * bpp;
    uniform int bs = dxt5 ? 16 : 8;
    for (uniform int by = by0; by < by1; by++)
    {
        // base pointers per block row keep the varying offsets small (32-bit addressing)
        uniform const u8 * uniform rowp = src + (uniform int64)by * 4 * rowStride;
        uniform unsigned int32 * uniform drow = (uniform unsigned int32 * uniform)(dst + (uniform int64)by * bw * bs);
        foreach (bx = 0 ... bw)
        {
            int R[16], G[16], B[16], A[16];
            if (bpp == 4 || (by + 1) * 4 < h)
            {
                // one 32-bit gather per pixel. For RGB24 this reads one byte past the pixel, which is the next pixel or the next
                // row - it exists everywhere except after the very last pixel of the image, so the last block row takes the byte path.
                for (uniform int y = 0; y < 4; y++)
                {
                    int o = y * (int)rowStride + bx * 4 * bpp;
                    for (uniform int x = 0; x < 4; x++, o += bpp)
                    {
                        uniform const u8 * varying pp = rowp + o;
                        unsigned int32 v = *((uniform const unsigned int32 * varying)pp);
                        R[y * 4 + x] = (int)((v >> (8 * ro)) & 0xFF); G[y * 4 + x] = (int)((v >> (8 * go)) & 0xFF); B[y * 4 + x] = (int)((v >> (8 * bo)) & 0xFF);
                        A[y * 4 + x] = bpp == 4 ? (int)((v >> (8 * ao)) & 0xFF) : 255;
                    }
                }
            }
            else
            {
                for (uniform int y = 0; y < 4; y++)
                {
                    int o = y * (int)rowStride + bx * 4 * bpp;
                    for (uniform int x = 0; x < 4; x++, o += bpp)
                    {
                        R[y * 4 + x] = rowp[o + ro]; G[y * 4 + x] = rowp[o + go]; B[y * 4 + x] = rowp[o + bo];
                        A[y * 4 + x] = bpp == 4 ? (int)rowp[o + ao] : 255;
                    }
                }
            }
            int wi = bx * (bs / 4);
            if (dxt5)
            {
                int amax, amin; unsigned int64 abits;
                encode_alpha(A, amax, amin, abits);
                drow[wi] = (unsigned int32)amax | ((unsigned int32)amin << 8) | ((unsigned int32)(abits & 0xFFFF) << 16);
                drow[wi + 1] = (unsigned int32)(abits >> 16);
                wi += 2;
            }
            int c0, c1; unsigned int32 idx;
            encode_color(R, G, B, O5a, O5b, O6a, O6b, c0, c1, idx);
            drow[wi] = ((unsigned int32)c0 & 0xFFFF) | ((unsigned int32)c1 << 16);
            drow[wi + 1] = idx;
        }
    }
}
