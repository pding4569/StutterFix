"""Offline HLSL syntax/SM5 compilation; no D3D device, Unity or game launch."""
import ctypes as C
from pathlib import Path

source = Path(__file__).with_name('GpuDeco.compute').read_bytes()
compiler = C.WinDLL('d3dcompiler_47.dll')
compile_shader = compiler.D3DCompile
compile_shader.argtypes = [C.c_void_p, C.c_size_t, C.c_char_p, C.c_void_p, C.c_void_p,
                          C.c_char_p, C.c_char_p, C.c_uint, C.c_uint,
                          C.POINTER(C.c_void_p), C.POINTER(C.c_void_p)]
compile_shader.restype = C.c_long


def method(blob, index, restype):
    table = C.cast(blob, C.POINTER(C.POINTER(C.c_void_p))).contents
    return C.WINFUNCTYPE(restype, C.c_void_p)(table[index])(blob)


for entry in ('EaseProbe', 'Evaluate', 'EvaluateLive', 'CopyRun', 'CopyMappedRun', 'RotationProbe', 'LinearProbe'):
    code, errors = C.c_void_p(), C.c_void_p()
    try:
        hr = compile_shader(source, len(source), b'GpuDeco.compute', None, None,
                            entry.encode(), b'cs_5_0', 1 << 15, 0,
                            C.byref(code), C.byref(errors))
        if hr < 0:
            message = C.string_at(method(errors, 3, C.c_void_p)).decode()
            raise RuntimeError(message)
        print(entry, 'cs_5_0 PASS bytes=' + str(method(code, 4, C.c_size_t)))
    finally:
        for blob in (code, errors):
            if blob.value:
                method(blob, 2, C.c_ulong)
print('GPU execution/numerical fidelity/bundle: UNMEASURED')
