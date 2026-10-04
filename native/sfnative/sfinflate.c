/*
 * sfinflate - StutterFix: streaming PNG decode for "shrink big images".
 *
 *   sf_png_shrink : zlib IDAT -> inflate -> unfilter -> downscale, all in cache.
 *
 * The inflate is libdeflate 1.24's decompressor (MIT, libdeflate/COPYING), compiled here unchanged, plus a second copy of its
 * main loop (stream_template.h, generated from decompress_template.h) that writes into a 544 KiB window and hands full rows
 * to the PNG row code instead of writing the whole image. On very large maps the images are huge but mostly empty
 * (ALPHA maps: 2.75 GB of PNG files unpack to 737 GB), and writing all of it to memory and reading it back was most of the
 * map-open time.
 * The result is the same as libdeflate's full decode followed by sf_unfilter_downscale (checked byte for byte).
 * Written for StutterFix (MIT). Build: native/build-sfnative.bat
 */
#include "libdeflate/lib/deflate_decompress.c"

#define SF_API __declspec(dllexport)

typedef int (*sf_sink_t)(void* ctx, const u8* p, size_t n);

#define SF_HISTORY 32768
#define SF_WINDOW (SF_HISTORY + 512 * 1024)

/* hand the new bytes to the sink (never more than total_expected in all), stop with success once everything expected is out,
 * then keep the last 32 KiB at the start of the window */
#define SF_FLUSH()                                                                                  \
    do {                                                                                            \
        size_t n_ = (size_t)(out_next - out_emit);                                                  \
        if (n_ > total_expected - total) n_ = total_expected - total;                               \
        if (n_) { if (!sink(ctx, out_emit, n_)) return LIBDEFLATE_BAD_DATA; total += n_; }          \
        if (total >= total_expected) return LIBDEFLATE_SUCCESS;                                     \
        {                                                                                           \
            size_t keep_ = MIN((size_t)(out_next - out), (size_t)SF_HISTORY);                       \
            memmove(out, out_next - keep_, keep_);                                                  \
            out_next = out + keep_; out_emit = out_next;                                            \
        }                                                                                           \
    } while (0)

#define FUNCNAME sf_stream_default
#undef ATTRIBUTES
#undef EXTRACT_VARBITS
#undef EXTRACT_VARBITS8
#include "stream_template.h"

/* the BMI2 build of the same loop, chosen at run time like libdeflate does */
#if MSVC_PREREQ(1930) && defined(ARCH_X86_64)
#  define SF_HAVE_BMI2 1
#  define FUNCNAME sf_stream_bmi2
#  define ATTRIBUTES _target_attribute("bmi2")
#  define EXTRACT_VARBITS(word, count)  _bzhi_u64((word), (count))
#  define EXTRACT_VARBITS8(word, count) _bzhi_u64((word), (count))
#  include "stream_template.h"
#endif

typedef enum libdeflate_result (*sf_stream_func)(struct libdeflate_decompressor* d, const void* in, size_t in_nbytes,
                                                 u8* out, size_t out_cap, size_t total_expected, sf_sink_t sink, void* ctx);

static sf_stream_func pick_stream(void)
{
#ifdef SF_HAVE_BMI2
    if (HAVE_BMI2(get_x86_cpu_features())) return sf_stream_bmi2;
#endif
    return sf_stream_default;
}

/* ---- PNG rows: assemble rows from the stream and run them through the row code in sfnative.c ---- */

typedef struct SfFused SfFused;
SfFused* sf_fused_begin(int width, int height, int bpp, uint8_t* d0, int nw, int nh);
int sf_fused_row(SfFused* f, const uint8_t* src);   /* src = filter byte + row */
void sf_fused_detach(SfFused* f);                   /* before the bytes given to sf_fused_row change */
int sf_fused_end(SfFused* f, int ok);                 /* writes the rest when ok, frees; returns ok */

typedef struct
{
    SfFused* f;
    size_t row;      /* 1 + width * bpp */
    u8* part;        /* a row split across two flushes */
    size_t have;
} RowSink;

static int row_sink(void* ctx, const u8* p, size_t n)
{
    RowSink* s = (RowSink*)ctx;
    if (s->have)
    {
        size_t t = MIN(n, s->row - s->have);
        memcpy(s->part + s->have, p, t);
        s->have += t; p += t; n -= t;
        if (s->have < s->row) return 1;
        if (!sf_fused_row(s->f, s->part)) return 0;
        s->have = 0;
    }
    while (n >= s->row)
    {
        if (!sf_fused_row(s->f, p)) return 0;
        p += s->row; n -= s->row;
    }
    sf_fused_detach(s->f);   /* the window moves after this call, and 'part' is about to be overwritten */
    if (n) { memcpy(s->part, p, n); s->have = n; }
    return 1;
}

/* zlib data of the PNG (all IDAT joined) -> the downscaled image in Unity order. 8-bit RGBA (bpp 4) or RGB (bpp 3), not
 * interlaced. Returns 1 on success, 0 if the data is bad or short (the caller then decodes the usual way). */
SF_API int sf_png_shrink(const uint8_t* zlib, int zlen, int width, int height, int bpp, uint8_t* d0, int nw, int nh)
{
    if (!zlib || zlen < 3 || width <= 0 || height <= 0 || (bpp != 3 && bpp != 4)) return 0;
    if ((zlib[0] & 0x0F) != 8 || (zlib[1] & 0x20)) return 0;   /* deflate, no preset dictionary */
    static sf_stream_func impl;
    if (!impl) impl = pick_stream();
    size_t row = 1 + (size_t)width * bpp;
    struct libdeflate_decompressor* d = libdeflate_alloc_decompressor();
    u8* win = (u8*)malloc(SF_WINDOW);
    u8* part = (u8*)malloc(row);
    SfFused* f = d && win && part ? sf_fused_begin(width, height, bpp, d0, nw, nh) : NULL;
    int ok = 0;
    if (f)
    {
        RowSink s = { f, row, part, 0 };
        ok = impl(d, zlib + 2, (size_t)zlen - 2, win, SF_WINDOW, row * (size_t)height, row_sink, &s) == LIBDEFLATE_SUCCESS;
        ok = sf_fused_end(f, ok);
    }
    free(part); free(win); libdeflate_free_decompressor(d);
    return ok;
}

/* measuring only (not used by the mod): the streaming inflate with a sink that does nothing */
static int null_sink(void* ctx, const u8* p, size_t n) { (void)ctx; (void)p; (void)n; return 1; }
SF_API int sf_png_inflate_only(const uint8_t* zlib, int zlen, size_t total)
{
    static sf_stream_func impl;
    if (!impl) impl = pick_stream();
    struct libdeflate_decompressor* d = libdeflate_alloc_decompressor();
    u8* win = (u8*)malloc(SF_WINDOW);
    int ok = d && win && impl(d, zlib + 2, (size_t)zlen - 2, win, SF_WINDOW, total, null_sink, NULL) == LIBDEFLATE_SUCCESS;
    free(win); libdeflate_free_decompressor(d);
    return ok;
}
