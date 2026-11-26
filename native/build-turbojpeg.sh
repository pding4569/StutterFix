#!/usr/bin/env bash
# Rebuilds native/libturbojpeg.so from the official libjpeg-turbo source (x86_64, SIMD).
#   Source: https://github.com/libjpeg-turbo/libjpeg-turbo  tag 3.2.0 (commit c85e6b905bf237038faa936dab160ebfc5da0344)
#   Tools:  gcc/clang, cmake, nasm, make (or ninja)
#   Usage:  ./build-turbojpeg.sh SRC_DIR
set -euo pipefail

if [ "$#" -lt 1 ]; then
    echo "Usage: $0 SRC_DIR" >&2
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC="$(cd "$1" && pwd)"
OUT="$SCRIPT_DIR/libturbojpeg.so"

WORK="$(mktemp -d "${TMPDIR:-/tmp}/turbojpeg_build.XXXXXX")"
trap 'rm -rf "$WORK"' EXIT

cmake -S "$SRC" -B "$WORK/build" \
    -DCMAKE_BUILD_TYPE=Release \
    -DENABLE_STATIC=OFF \
    -DENABLE_SHARED=ON \
    -DWITH_TURBOJPEG=ON \
    -DWITH_TOOLS=OFF \
    -DWITH_TESTS=OFF \
    -DCMAKE_SKIP_RPATH=ON

cmake --build "$WORK/build" --target turbojpeg --parallel

BUILT="$(find "$WORK/build" -maxdepth 2 -name 'libturbojpeg.so.*' -type f | head -n 1)"
if [ -z "$BUILT" ]; then
    BUILT="$WORK/build/libturbojpeg.so"
fi
install -m 755 "$BUILT" "$OUT"

echo "OK $OUT"
