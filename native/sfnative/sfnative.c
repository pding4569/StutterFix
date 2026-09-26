/*
 * sfnative - StutterFix native helpers (image loading on worker threads).
 *
 *   sf_unfilter_to / sf_unfilter_inplace : PNG row unfiltering (same rules and results as PngDecoder.UnfilterTo / UnfilterPtr)
 *   sf_dxt_encode_rows                    : DXT1/DXT5 block compression (same algorithm as DxtEncoder.EncodeRows)
 *
 * Integer code gives bit-identical results to the C# versions. The principal-axis search in the DXT colour encoder uses
 * float like the C# code; rounding may differ in rare blocks, so the DXT output is checked for quality, not bit equality.
 * Written for StutterFix (MIT). Build: native/build-sfnative.bat
 */
#include <stdint.h>
#include <string.h>
#include <emmintrin.h>

#define SF_API __declspec(dllexport)

SF_API int sf_version(void) { return 1; }

/* ---------------------------------------------------------------- PNG unfilter ---------------------------------------------------------------- */

static __m128i abs16(__m128i v) { __m128i m = _mm_srai_epi16(v, 15); return _mm_sub_epi16(_mm_xor_si128(v, m), m); }
/* x <= y for signed 16-bit lanes: all ones where true */
static __m128i le16(__m128i x, __m128i y) { return _mm_xor_si128(_mm_cmpgt_epi16(x, y), _mm_set1_epi16(-1)); }
static __m128i sel(__m128i m, __m128i a, __m128i b) { return _mm_or_si128(_mm_and_si128(m, a), _mm_andnot_si128(m, b)); }

static __m128i load4(const uint8_t* p) { int v; memcpy(&v, p, 4); return _mm_cvtsi32_si128(v); }
static __m128i load3(const uint8_t* p) { int v = p[0] | (p[1] << 8) | (p[2] << 16); return _mm_cvtsi32_si128(v); }
static void store4(uint8_t* d, __m128i v) { int x = _mm_cvtsi128_si32(v); memcpy(d, &x, 4); }
static void store3(uint8_t* d, __m128i v) { int x = _mm_cvtsi128_si32(v); d[0] = (uint8_t)x; d[1] = (uint8_t)(x >> 8); d[2] = (uint8_t)(x >> 16); }

/* d = output row, s = filtered row, p = previous output row (all n bytes). d may equal s (in place). */
static int unfilter(uint8_t* d, const uint8_t* s, const uint8_t* p, int n, int bpp, int filter)
{
    int i;
    switch (filter)
    {
    case 0:
        if (d != s) memmove(d, s, (size_t)n);
        return 1;
    case 1:
        for (i = 0; i < bpp && i < n; i++) d[i] = s[i];
        if (bpp == 4 && (n & 3) == 0)
        {
            __m128i a = load4(d);
            for (i = 4; i < n; i += 4) { a = _mm_add_epi8(load4(s + i), a); store4(d + i, a); }
        }
        else
            for (i = bpp; i < n; i++) d[i] = (uint8_t)(s[i] + d[i - bpp]);
        return 1;
    case 2:
        for (i = 0; i + 16 <= n; i += 16)
            _mm_storeu_si128((__m128i*)(d + i), _mm_add_epi8(_mm_loadu_si128((const __m128i*)(s + i)), _mm_loadu_si128((const __m128i*)(p + i))));
        for (; i < n; i++) d[i] = (uint8_t)(s[i] + p[i]);
        return 1;
    case 3:
        if ((bpp == 4 && (n & 3) == 0) || (bpp == 3 && n % 3 == 0))
        {
            __m128i z = _mm_setzero_si128(), a = z, m = _mm_set1_epi16(0xFF);
            for (i = 0; i < n; i += bpp)
            {
                __m128i b = _mm_unpacklo_epi8(bpp == 4 ? load4(p + i) : load3(p + i), z);
                __m128i x = _mm_unpacklo_epi8(bpp == 4 ? load4(s + i) : load3(s + i), z);
                __m128i o = _mm_and_si128(_mm_add_epi16(x, _mm_srli_epi16(_mm_add_epi16(a, b), 1)), m);
                __m128i packed = _mm_packus_epi16(o, o);
                if (bpp == 4) store4(d + i, packed); else store3(d + i, packed);
                a = o;
            }
            return 1;
        }
        for (i = 0; i < bpp && i < n; i++) d[i] = (uint8_t)(s[i] + (p[i] >> 1));
        for (i = bpp; i < n; i++) d[i] = (uint8_t)(s[i] + ((d[i - bpp] + p[i]) >> 1));
        return 1;
    case 4:
        if ((bpp == 4 && (n & 3) == 0) || (bpp == 3 && n % 3 == 0))
        {
            /* a = left (output), b = up, c = up-left. First pixel: a = c = 0, which gives prediction b like the scalar rule. */
            __m128i z = _mm_setzero_si128(), a = z, c = z, m = _mm_set1_epi16(0xFF);
            for (i = 0; i < n; i += bpp)
            {
                __m128i b = _mm_unpacklo_epi8(bpp == 4 ? load4(p + i) : load3(p + i), z);
                __m128i x = _mm_unpacklo_epi8(bpp == 4 ? load4(s + i) : load3(s + i), z);
                __m128i pa = abs16(_mm_sub_epi16(b, c));
                __m128i pb = abs16(_mm_sub_epi16(a, c));
                __m128i pc = abs16(_mm_sub_epi16(_mm_add_epi16(a, b), _mm_add_epi16(c, c)));
                __m128i useA = _mm_and_si128(le16(pa, pb), le16(pa, pc));
                __m128i pred = sel(useA, a, sel(le16(pb, pc), b, c));
                __m128i o = _mm_and_si128(_mm_add_epi16(x, pred), m);
                __m128i packed = _mm_packus_epi16(o, o);
                if (bpp == 4) store4(d + i, packed); else store3(d + i, packed);
                a = o; c = b;
            }
            return 1;
        }
        for (i = 0; i < bpp && i < n; i++) d[i] = (uint8_t)(s[i] + p[i]);
        for (i = bpp; i < n; i++)
        {
            int a = d[i - bpp], b = p[i], cc = p[i - bpp];
            int pa = b - cc, pb = a - cc, pc = pa + pb;
            pa = pa < 0 ? -pa : pa; pb = pb < 0 ? -pb : pb; pc = pc < 0 ? -pc : pc;
            d[i] = (uint8_t)(s[i] + ((pa <= pb && pa <= pc) ? a : (pb <= pc ? b : cc)));
        }
        return 1;
    default:
        return 0;
    }
}

SF_API int sf_unfilter_to(uint8_t* d, const uint8_t* s, const uint8_t* p, int n, int bpp, int filter) { return unfilter(d, s, p, n, bpp, filter); }
SF_API int sf_unfilter_inplace(uint8_t* c, const uint8_t* p, int n, int bpp, int filter) { return unfilter(c, c, p, n, bpp, filter); }

/* ---------------------------------------------------------------- DXT ---------------------------------------------------------------- */

static uint8_t O5a[256], O5b[256], O6a[256], O6b[256];
static volatile int tablesReady;

static void opt_table(uint8_t* ta, uint8_t* tb, int bits)
{
    int n = 1 << bits, v, a, b;
    for (v = 0; v < 256; v++)
    {
        int best = 0x7FFFFFFF;
        for (a = 0; a < n; a++)
            for (b = 0; b < n; b++)
            {
                int xa = bits == 5 ? (a << 3) | (a >> 2) : (a << 2) | (a >> 4), xb = bits == 5 ? (b << 3) | (b >> 2) : (b << 2) | (b >> 4);
                int d1 = (2 * xa + xb) / 3 - v, d2 = xa - xb;
                int e = (d1 < 0 ? -d1 : d1) * 100 + (d2 < 0 ? -d2 : d2) * 3;
                if (e < best) { best = e; ta[v] = (uint8_t)a; tb[v] = (uint8_t)b; }
            }
    }
}

/* Must be called once before sf_dxt_encode_rows (from one thread). */
SF_API void sf_dxt_init(void)
{
    if (tablesReady) return;
    opt_table(O5a, O5b, 5); opt_table(O6a, O6b, 6);
    tablesReady = 1;
}

static int to565(int r, int g, int b) { return (((r * 31 + 127) / 255) << 11) | (((g * 63 + 127) / 255) << 5) | ((b * 31 + 127) / 255); }
static void expand(int c, int* r, int* g, int* b)
{
    int r5 = (c >> 11) & 31, g6 = (c >> 5) & 63, b5 = c & 31;
    *r = (r5 << 3) | (r5 >> 2); *g = (g6 << 2) | (g6 >> 4); *b = (b5 << 3) | (b5 >> 2);
}

static int match(const uint8_t* p, int c0, int c1, uint32_t* idxOut)
{
    int r0, g0, b0, r1, g1, b1, i;
    expand(c0, &r0, &g0, &b0); expand(c1, &r1, &g1, &b1);
    int dr = r0 - r1, dg = g0 - g1, db = b0 - b1;
    int len2 = dr * dr + dg * dg + db * db;
    int r2 = (2 * r0 + r1) / 3, g2 = (2 * g0 + g1) / 3, b2 = (2 * b0 + b1) / 3;
    int r3 = (r0 + 2 * r1) / 3, g3 = (g0 + 2 * g1) / 3, b3 = (b0 + 2 * b1) / 3;
    uint32_t idx = 0; int total = 0;
    const uint8_t* q = p + 60;
    /* C#: s = (dot * 6 + len2) / (2 * len2) (truncating), then only s <= 0 / 1 / 2 / >= 3 matter.
       With x = dot * 6 + len2 and y = 2 * len2 > 0 that is exactly: x < y, x < 2y, x < 3y (no division). */
    int y1 = 2 * len2, y2 = 4 * len2, y3 = 6 * len2;
    for (i = 15; i >= 0; i--, q -= 4)
    {
        int r = q[0], g = q[1], b = q[2], s = 3, er, eg, eb; uint32_t best;
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
    *idxOut = idx;
    return total;
}

static int divr(int n, int d)
{
    int q;
    if (d < 0) { n = -n; d = -d; }
    q = n >= 0 ? (n + d / 2) / d : -((-n + d / 2) / d);
    return q < 0 ? 0 : q > 255 ? 255 : q;
}

static int refine(const uint8_t* p, uint32_t idx, int* c0, int* c1)
{
    int aa = 0, bb = 0, ab = 0, axr = 0, axg = 0, axb = 0, bxr = 0, bxg = 0, bxb = 0, i, det;
    for (i = 0; i < 16; i++)
    {
        int k = (int)((idx >> (2 * i)) & 3);
        int a = k == 0 ? 3 : k == 1 ? 0 : k == 2 ? 2 : 1, b = 3 - a;
        int r = p[i * 4], g = p[i * 4 + 1], bl = p[i * 4 + 2];
        aa += a * a; bb += b * b; ab += a * b;
        axr += a * r; axg += a * g; axb += a * bl;
        bxr += b * r; bxg += b * g; bxb += b * bl;
    }
    det = aa * bb - ab * ab;
    *c0 = *c1 = 0;
    if (det == 0) return 0;
    *c0 = to565(divr(3 * (axr * bb - bxr * ab), det), divr(3 * (axg * bb - bxg * ab), det), divr(3 * (axb * bb - bxb * ab), det));
    *c1 = to565(divr(3 * (bxr * aa - axr * ab), det), divr(3 * (bxg * aa - axg * ab), det), divr(3 * (bxb * aa - axb * ab), det));
    return 1;
}

static void put_color(uint8_t* dst, int c0, int c1, uint32_t idx)
{
    if (c0 < c1) { int t = c0; c0 = c1; c1 = t; idx ^= 0x55555555u; }
    else if (c0 == c1) idx = 0;
    dst[0] = (uint8_t)c0; dst[1] = (uint8_t)(c0 >> 8); dst[2] = (uint8_t)c1; dst[3] = (uint8_t)(c1 >> 8);
    dst[4] = (uint8_t)idx; dst[5] = (uint8_t)(idx >> 8); dst[6] = (uint8_t)(idx >> 16); dst[7] = (uint8_t)(idx >> 24);
}

enum { NearSolid = 12, SolidMaxRange = 48, RefinePasses = 1 };

static void encode_color(const uint8_t* p, uint8_t* dst)
{
    int sr = 0, sg = 0, sb = 0, srr = 0, srg = 0, srb = 0, sgg = 0, sgb = 0, sbb = 0;
    int minR = 255, minG = 255, minB = 255, maxR = 0, maxG = 0, maxB = 0, i, it, pass;
    for (i = 0; i < 64; i += 4)
    {
        int r = p[i], g = p[i + 1], b = p[i + 2];
        sr += r; sg += g; sb += b;
        srr += r * r; srg += r * g; srb += r * b; sgg += g * g; sgb += g * b; sbb += b * b;
        if (r < minR) minR = r; if (r > maxR) maxR = r; if (g < minG) minG = g; if (g > maxG) maxG = g; if (b < minB) minB = b; if (b > maxB) maxB = b;
    }
    if (minR == maxR && minG == maxG && minB == maxB)
    {
        int c0 = (O5a[minR] << 11) | (O6a[minG] << 5) | O5a[minB], c1 = (O5b[minR] << 11) | (O6b[minG] << 5) | O5b[minB];
        put_color(dst, c0, c1, 0xAAAAAAAAu);
        return;
    }
    int range = (maxR - minR) + (maxG - minG) + (maxB - minB);
    if (range <= NearSolid)
    {
        int ar0 = (sr + 8) >> 4, ag0 = (sg + 8) >> 4, ab0 = (sb + 8) >> 4;
        int n0 = (O5a[ar0] << 11) | (O6a[ag0] << 5) | O5a[ab0], n1 = (O5b[ar0] << 11) | (O6b[ag0] << 5) | O5b[ab0];
        uint32_t nidx; match(p, n0, n1, &nidx);
        put_color(dst, n0, n1, nidx);
        return;
    }
    float crr = (float)(16 * srr - sr * sr), crg = (float)(16 * srg - sr * sg), crb = (float)(16 * srb - sr * sb);
    float cgg = (float)(16 * sgg - sg * sg), cgb = (float)(16 * sgb - sg * sb), cbb = (float)(16 * sbb - sb * sb);
    float vr = (float)(maxR - minR), vg = (float)(maxG - minG), vb = (float)(maxB - minB);
    for (it = 0; it < 4; it++)
    {
        float nr = vr * crr + vg * crg + vb * crb, ng = vr * crg + vg * cgg + vb * cgb, nb = vr * crb + vg * cgb + vb * cbb;
        float anr = nr < 0 ? -nr : nr, ang = ng < 0 ? -ng : ng, anb = nb < 0 ? -nb : nb;
        float m = anr > ang ? anr : ang; if (anb > m) m = anb;
        if (m < 1e-6f) break;
        vr = nr / m; vg = ng / m; vb = nb / m;
    }
    int ar = (int)(vr * 256), ag = (int)(vg * 256), ab = (int)(vb * 256);
    int lo = 0x7FFFFFFF, hi = (int)0x80000000, li = 0, hiI = 0;
    for (i = 0; i < 16; i++)
    {
        int d = p[i * 4] * ar + p[i * 4 + 1] * ag + p[i * 4 + 2] * ab;
        if (d < lo) { lo = d; li = i; }
        if (d > hi) { hi = d; hiI = i; }
    }
    int c0 = to565(p[hiI * 4], p[hiI * 4 + 1], p[hiI * 4 + 2]);
    int c1 = to565(p[li * 4], p[li * 4 + 1], p[li * 4 + 2]);
    uint32_t idx; int err = match(p, c0, c1, &idx);
    if (range <= SolidMaxRange)
    {
        int ar2 = (sr + 8) >> 4, ag2 = (sg + 8) >> 4, ab2 = (sb + 8) >> 4;
        int s0 = (O5a[ar2] << 11) | (O6a[ag2] << 5) | O5a[ab2], s1 = (O5b[ar2] << 11) | (O6b[ag2] << 5) | O5b[ab2];
        uint32_t sidx; int serr = match(p, s0, s1, &sidx);
        if (serr < err) { c0 = s0; c1 = s1; idx = sidx; err = serr; }
    }
    for (pass = 0; pass < RefinePasses; pass++)
    {
        int n0, n1; uint32_t nidx; int nerr;
        if (!refine(p, idx, &n0, &n1)) break;
        nerr = match(p, n0, n1, &nidx);
        if (nerr >= err) break;
        c0 = n0; c1 = n1; idx = nidx; err = nerr;
    }
    put_color(dst, c0, c1, idx);
}

static int alpha_try(const uint8_t* p, int max, int min, uint64_t* bitsOut)
{
    uint64_t bits = 0; int range = max - min, total = 0, i;
    if (range <= 0) { for (i = 3; i < 64; i += 4) total += (p[i] - max) * (p[i] - max); *bitsOut = 0; return total; }
    /* C#: s = ((max - a) * 14 + range) / (2 * range) (truncating), clamped to 0..7. x < 0 always ends at 0 after the clamp,
       and for x >= 0 truncation is floor, so s = number of k in 1..7 with k * y <= x. */
    int y = 2 * range;
    for (i = 15; i >= 0; i--)
    {
        int a = p[i * 4 + 3];
        int x = (max - a) * 14 + range, s = 0;
        if (x >= y) { s = x / y; if (s > 7) s = 7; }
        int v = ((7 - s) * max + s * min) / 7;
        int best = s == 0 ? 0 : s == 7 ? 1 : s + 1;
        total += (a - v) * (a - v);
        bits = (bits << 3) | (uint64_t)best;
    }
    *bitsOut = bits;
    return total;
}

static void encode_alpha(const uint8_t* p, uint8_t* dst)
{
    int min = 255, max = 0, i, k;
    for (i = 3; i < 64; i += 4) { int a = p[i]; if (a < min) min = a; if (a > max) max = a; }
    uint64_t bits; int e1 = alpha_try(p, max, min, &bits);
    int inset = (max - min) >> 5;
    if (inset > 0)
    {
        uint64_t b2; int e2 = alpha_try(p, max - inset, min + inset, &b2);
        if (e2 < e1) { bits = b2; max -= inset; min += inset; }
    }
    dst[0] = (uint8_t)max; dst[1] = (uint8_t)min;
    if (max == min) bits = 0;
    for (k = 0; k < 6; k++) dst[2 + k] = (uint8_t)(bits >> (8 * k));
}

/* layout: 0 = RGBA32, 1 = ARGB32, 2 = RGB24. Compresses block rows by0..by1-1; dst is the start of the whole output. */
SF_API void sf_dxt_encode_rows(const uint8_t* src, int w, int h, int layout, int dxt5, uint8_t* dst, int by0, int by1)
{
    int bpp = layout == 2 ? 3 : 4;
    int ro = layout == 1 ? 1 : 0, go = ro + 1, bo = ro + 2, ao = layout == 1 ? 0 : 3;
    uint8_t blk[64];
    int bw = w / 4, by, bx, y, x;
    long long rowStride = (long long)w * bpp;
    (void)h;
    dst += (long long)by0 * bw * (dxt5 ? 16 : 8);
    for (by = by0; by < by1; by++)
        for (bx = 0; bx < bw; bx++)
        {
            for (y = 0; y < 4; y++)
            {
                const uint8_t* s = src + (long long)(by * 4 + y) * rowStride + (long long)bx * 4 * bpp;
                uint8_t* d = blk + y * 16;
                for (x = 0; x < 4; x++, s += bpp, d += 4) { d[0] = s[ro]; d[1] = s[go]; d[2] = s[bo]; d[3] = bpp == 4 ? s[ao] : 255; }
            }
            if (dxt5) { encode_alpha(blk, dst); dst += 8; }
            encode_color(blk, dst); dst += 8;
        }
}
