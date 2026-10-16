"""Synthetic regression of pre-existing image exports against a saved DLL."""
import ctypes as C
from pathlib import Path
import random
import sys

before,after=[C.CDLL(str(Path(p).resolve())) for p in sys.argv[1:3]]
for lib in (before,after):
    lib.sf_dxt_init()
    lib.sf_dxt_encode_rows.argtypes=[C.c_void_p,C.c_int,C.c_int,C.c_int,C.c_int,C.c_void_p,C.c_int,C.c_int]
    lib.sf_unfilter_inplace.argtypes=[C.c_void_p,C.c_void_p,C.c_int,C.c_int,C.c_int]
    lib.sf_unfilter_inplace.restype=C.c_int
rng=random.Random(36)
checks=0
for i in range(300):
    w=rng.randrange(1,17)*4;h=rng.randrange(1,17)*4;layout=i%3;bpp=3 if layout==2 else 4;dxt5=i%2
    source=(C.c_ubyte*(w*h*bpp)).from_buffer_copy(rng.randbytes(w*h*bpp))
    n=(w//4)*(h//4)*(16 if dxt5 else 8)
    outputs=[(C.c_ubyte*n)() for _ in range(2)]
    for lib,output in zip((before,after),outputs):lib.sf_dxt_encode_rows(source,w,h,layout,dxt5,output,0,h//4)
    assert bytes(outputs[0])==bytes(outputs[1]),('DXT',i)
    checks+=1
for i in range(300):
    bpp=3+i%2;n=rng.randrange(1,257)*bpp;data=rng.randbytes(n);previous=(C.c_ubyte*n).from_buffer_copy(rng.randbytes(n))
    outputs=[(C.c_ubyte*n).from_buffer_copy(data) for _ in range(2)]
    results=[lib.sf_unfilter_inplace(output,previous,n,bpp,i%5) for lib,output in zip((before,after),outputs)]
    assert results[0]==results[1] and bytes(outputs[0])==bytes(outputs[1]),('PNG',i)
    checks+=1
print(f'Existing native image regression {checks} cases, mismatch 0; synthetic data, dispatched CPU only')
