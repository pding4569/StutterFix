#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC="$SCRIPT_DIR/sfnative"
OUT="$SCRIPT_DIR/libsfnative.so"
CC="${CC:-gcc}"
CXX="${CXX:-g++}"

WORK="$(mktemp -d "${TMPDIR:-/tmp}/sfnative_build.XXXXXX")"
trap 'rm -rf "$WORK"' EXIT

ISPC_OBJS=()
EXTRA_CFLAGS=()

if command -v ispc >/dev/null 2>&1; then
    ispc "$SRC/sfdxt.ispc" -O2 --pic --arch=x86-64 --target-os=linux \
        --target=sse2-i32x4,sse4.1-i32x4,avx2-i32x8 --opt=disable-fma \
        -o "$WORK/sfdxt.o"
    ispc "$SRC/sfmotion.ispc" -O2 --pic --arch=x86-64 --target-os=linux \
        --target=sse2-i32x4,sse4.1-i32x4,avx2-i32x8,avx512skx-x16 --opt=disable-fma \
        -o "$WORK/sfmotion.o"
    ISPC_OBJS+=(
        "$WORK/sfdxt.o" "$WORK/sfdxt_sse2.o" "$WORK/sfdxt_sse4.o" "$WORK/sfdxt_avx2.o"
        "$WORK/sfmotion.o" "$WORK/sfmotion_sse2.o" "$WORK/sfmotion_sse4.o" "$WORK/sfmotion_avx2.o" "$WORK/sfmotion_avx512skx.o"
    )
else
    EXTRA_CFLAGS+=(-DSF_NO_ISPC)
fi

"$CC" -O2 -fPIC -fvisibility=hidden -fno-fast-math -ffp-contract=off -msse2 -Wall -Wextra \
    "${EXTRA_CFLAGS[@]}" \
    -c "$SRC/sfnative.c" -o "$WORK/sfnative.o"

"$CC" -O2 -fPIC -fvisibility=hidden -msse2 -Wall \
    -c "$SRC/sfinflate.c" -o "$WORK/sfinflate.o"

"$CC" -O2 -fPIC -fvisibility=hidden -msse2 -Wall \
    -c "$SRC/libdeflate/lib/x86/cpu_features.c" -o "$WORK/cpu_features.o"

"$CC" -O2 -fPIC -fvisibility=hidden -msse2 -Wall \
    -c "$SRC/libdeflate/lib/utils.c" -o "$WORK/utils.o"

"$CXX" -O2 -fPIC -fvisibility=hidden -std=c++17 -fno-exceptions -fno-rtti \
    -fno-fast-math -ffp-contract=off -msse2 -mfpmath=sse -Wall -Wextra -Werror \
    -c "$SRC/decobatch.cpp" -o "$WORK/decobatch.o"

cat > "$WORK/exports.map" <<'EOF'
{
    global:
        sf_*;
    local:
        *;
};
EOF

"$CC" -shared -Wl,-soname,libsfnative.so -Wl,--version-script="$WORK/exports.map" \
    -o "$OUT" \
    "$WORK/sfnative.o" "$WORK/sfinflate.o" "$WORK/cpu_features.o" "$WORK/utils.o" "$WORK/decobatch.o" \
    "${ISPC_OBJS[@]}" \
    -lm

echo "OK $OUT"
