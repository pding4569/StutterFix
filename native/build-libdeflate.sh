#!/usr/bin/env bash
# Rebuilds native/libdeflate.so from the official source (decompression only, x86_64).
#   Source: https://github.com/ebiggers/libdeflate  tag v1.24 (commit 96836d7d9d10e3e0d53e6edb54eb908514e336c4)
#   Usage:  ./build-libdeflate.sh SRC_DIR
set -euo pipefail

if [ "$#" -lt 1 ]; then
    echo "Usage: $0 SRC_DIR" >&2
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC="$(cd "$1" && pwd)"
OUT="$SCRIPT_DIR/libdeflate.so"
CC="${CC:-gcc}"

WORK="$(mktemp -d "${TMPDIR:-/tmp}/libdeflate_build.XXXXXX")"
trap 'rm -rf "$WORK"' EXIT

cat > "$WORK/exports.map" <<'EOF'
{
    global:
        libdeflate_alloc_decompressor;
        libdeflate_free_decompressor;
        libdeflate_zlib_decompress;
    local:
        *;
};
EOF

"$CC" -O2 -fPIC -fvisibility=hidden -DLIBDEFLATE_DLL -I"$SRC" \
    "$SRC/lib/deflate_decompress.c" \
    "$SRC/lib/zlib_decompress.c" \
    "$SRC/lib/adler32.c" \
    "$SRC/lib/utils.c" \
    "$SRC/lib/x86/cpu_features.c" \
    -shared -Wl,-soname,libdeflate.so -Wl,--version-script="$WORK/exports.map" \
    -o "$OUT"

echo "OK $OUT"
