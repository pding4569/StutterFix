"""Compile the prototype fragment HLSL with Windows D3DCompile; no Unity.

This checks HLSL syntax/resource limits only, not ShaderLab import or rendering.
Unity's v2f_img interface is replaced by its UV field for this isolated check.
"""
import ctypes as c
from pathlib import Path
import re


def blob_data(blob):
    table = c.cast(blob, c.POINTER(c.POINTER(c.c_void_p))).contents
    pointer = c.WINFUNCTYPE(c.c_void_p, c.c_void_p)(table[3])(blob)
    size = c.WINFUNCTYPE(c.c_size_t, c.c_void_p)(table[4])(blob)
    return c.string_at(pointer, size)


def release(blob):
    if blob:
        table = c.cast(blob, c.POINTER(c.POINTER(c.c_void_p))).contents
        c.WINFUNCTYPE(c.c_ulong, c.c_void_p)(table[2])(blob)


if __name__ == "__main__":
    shader = Path(__file__).with_name("StackFold.shader").read_text(encoding="utf-8")
    source = shader.split("CGPROGRAM", 1)[1].split("ENDCG", 1)[0]
    source = re.sub(r"^\s*#(?:pragma|include).*?$", "", source, flags=re.M)
    source = ("struct v2f_img { float2 uv : TEXCOORD0; };\n" + source).encode("utf-8")
    compiler = c.WinDLL("d3dcompiler_47.dll").D3DCompile
    compiler.restype = c.c_long
    compiler.argtypes = [c.c_void_p, c.c_size_t, c.c_char_p, c.c_void_p, c.c_void_p,
                         c.c_char_p, c.c_char_p, c.c_uint, c.c_uint, c.POINTER(c.c_void_p), c.POINTER(c.c_void_p)]
    code, errors = c.c_void_p(), c.c_void_p()
    try:
        result = compiler(source, len(source), b"StackFold.shader", None, None, b"frag", b"ps_5_0", (1 << 11) | (1 << 13), 0,
                          c.byref(code), c.byref(errors))  # strict IEEE arithmetic
        if errors:
            print(blob_data(errors).decode("utf-8", errors="replace"))
        if result < 0:
            raise SystemExit(1)
        print(f"STACKFOLD_HLSL=PASS profile=ps_5_0 bytes={len(blob_data(code))} scope=isolated-fragment-only")
    finally:
        release(code)
        release(errors)
