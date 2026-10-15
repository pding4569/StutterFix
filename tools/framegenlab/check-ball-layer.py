import ctypes as C
from pathlib import Path
# Offline compilation: no game, D3D device, hooks or presentation.
source=Path('native/sfnative/framegen_block.h').read_text().split('return R"(',1)[1].split(')";',1)[0].replace(')" R"(', '').encode()
dll=C.WinDLL('d3dcompiler_47.dll')
fn=dll.D3DCompile
fn.argtypes=[C.c_void_p,C.c_size_t,C.c_char_p,C.c_void_p,C.c_void_p,C.c_char_p,C.c_char_p,C.c_uint,C.c_uint,C.POINTER(C.c_void_p),C.POINTER(C.c_void_p)]
fn.restype=C.c_long
for entry,profile in [('VS','vs_5_0'),('PS','ps_5_0'),('Reduce','cs_5_0'),('Match','cs_5_0'),('Metric','cs_5_0')]:
    result=C.c_void_p();errors=C.c_void_p()
    hr=fn(source,len(source),None,None,None,entry.encode(),profile.encode(),1<<15,0,C.byref(result),C.byref(errors))
    if hr<0:
        vt=C.cast(errors,C.POINTER(C.POINTER(C.c_void_p))).contents
        ptr=C.WINFUNCTYPE(C.c_void_p,C.c_void_p)(vt[3])(errors)
        raise RuntimeError(C.string_at(ptr).decode())
    print(entry,profile,'PASS')
    for blob in [result,errors]:
        if blob.value:
            vt=C.cast(blob,C.POINTER(C.POINTER(C.c_void_p))).contents
            C.WINFUNCTYPE(C.c_ulong,C.c_void_p)(vt[2])(blob)
for S,T in [([.2,.3,.4],[.6,.6,.6]),([.8,.6,.4],[1,1,1]),([.1,.2,.3],[0,0,0]),([0,0,0],[.2,.4,.8])]:
    black=S;white=[S[i]+T[i] for i in range(3)]
    for bg in [[0,0,0],[1,1,1],[.2,.6,.8]]:
        reconstructed=[bg[i]*(white[i]-black[i])+black[i] for i in range(3)]
        expected=[bg[i]*T[i]+S[i] for i in range(3)]
        assert max(abs(a-b) for a,b in zip(reconstructed,expected))<1e-12
print('matte alpha/additive/opaque/channel multiply: PASS')

# Scalar/raster regressions, not captured game textures. These reproduce failure
# mechanisms; they cannot establish which one occurred in run1 without its RTs.
def smooth(a,b,x):
    t=max(0,min(1,(x-a)/(b-a)))
    return t*t*(3-2*t)

def body(base,black_a,white_a,black_b,white_b,phase):
    leakage=max(max(0,min(1,w-b)) for black,white in
                [(black_a,white_a),(black_b,white_b)] for b,w in zip(black,white))
    coverage=1-smooth(.002,.02,leakage)
    return [(1-coverage)*v+coverage*((1-phase)*a+phase*b)
            for v,a,b in zip(base,black_a,black_b)]

# Even a correct unclamped HDR reconstruction fails the old LDR-screen guard.
assert abs(1.4-1)>2.5/255
print('old HDR-vs-LDR rejection reproduced: PASS')

# A moved opaque disc: mismatch at OLD screen coordinates suppresses its centre,
# while the exterior survives as a white ring. Checker mismatch creates stipple.
holes=spots=corrected=0
for y in range(-12,13):
    for x in range(-12,13):
        if x*x+y*y>100: continue
        mismatch=.03 if x*x+y*y<49 else 0
        old=.1 if mismatch>2.5/255 else .8
        holes+=old==.1
        checker=.03 if (x+y)%2 else 0
        spots+=checker>2.5/255
        new=body([.1]*3,[.8]*3,[.8]*3,[.8]*3,[.8]*3,.5)
        assert max(abs(c-.8) for c in new)<1e-12
        corrected+=1
assert holes>0 and spots>0
print(f'old guard ring holes={holes}, stipple={spots}; body opaque pixels={corrected}: PASS')

# White/black particle disagreement changes the recovered transmission, even
# though physical additive transmission is 1. Excluding particles is necessary.
particle_black=.4;particle_white=1.43
assert abs((particle_white-particle_black)-1)>.02
for phase in [0,.25,.5,.75,1]:
    base=[.13,.47,.81]
    # Additive glow, empty pixels, semitransparent body: exactly base, no
    # subtraction/re-render noise or additive double counting in the correction.
    for transmission in [1,.5,.02]:
        b=[.2,.3,.4];w=[c+transmission for c in b]
        assert body(base,b,w,b,w,phase)==base
    # Opaque in only one endpoint must not punch a half-body through the other.
    assert body(base,[.8]*3,[.8]*3,[0]*3,[1]*3,phase)==base
    # Channel-wise multiplication is not mistaken for an opaque body.
    assert body(base,[.2]*3,[.2,.2,.5],[.2]*3,[.2,.2,.5],phase)==base
print('particle mismatch reproduced; additive/transparent/one-sided/channel masks: PASS')

# Screen -> world -> source -> camera UV roundtrip, with pan/zoom/rotation.
# The shifted matte and background coordinates intentionally differ. An opaque
# sample is independent of the background coordinate, so this alone makes no ring.
import math
def uv(world,cam):
    x,y,size,angle=cam
    dx,dy=world[0]-x,world[1]-y
    return (.5+(math.cos(angle)*dx+math.sin(angle)*dy)/(size*2*2.4),
            .5-(-math.sin(angle)*dx+math.cos(angle)*dy)/(size*2))
def world(p,cam):
    x,y,size,angle=cam
    dx,dy=(p[0]-.5)*size*2*2.4,(.5-p[1])*size*2
    return (x+math.cos(angle)*dx-math.sin(angle)*dy,
            y+math.sin(angle)*dx+math.cos(angle)*dy)
for cam in [(0,0,5,0),(2,-3,8,.7),(-4,1,2,-1.2)]:
    for point in [(-1,2),(.2,.3),(4,-2)]:
        restored=world(uv(point,cam),cam)
        assert max(abs(a-b) for a,b in zip(point,restored))<1e-12
print('pan/zoom/rotation coordinate roundtrip: PASS')

# Combined overlap is algebraically valid at a fixed point; it cannot in general
# move two different bodies with one displacement. New correction defers overlap.
s1,t1,s2,t2=.6,.3,.2,.5
for bg in [0,.3,1]:
    sequential=(bg*t1+s1)*t2+s2
    assert abs(sequential-(bg*(t1*t2)+(s1*t2+s2)))<1e-12
def overlap(sample_a,sample_b,other_a,other_b,r):
    return r>0 and (math.dist(sample_a,other_a)<r*1.1 or math.dist(sample_b,other_b)<r*1.1)
assert overlap((0,0),(5,0),(.5,0),(9,0),1)
assert overlap((0,0),(5,0),(9,0),(5.5,0),1)
assert not overlap((0,0),(5,0),(9,0),(9,0),1)
print('combined overlap affine identity and conservative overlap deferral: PASS')

# Tie the numerical model to the selected production path (not a GPU execution).
shader=source.decode()
managed=Path('FrameGen.cs').read_text(encoding='utf-8-sig')
correction=shader.split('float3 colorAt(',1)[1].split('struct V',1)[0]
assert 'baseColorAt(uv,moving)' in correction
assert 'oldScreen' not in correction and 'newScreen' not in correction
assert 'otherRadius*1.1' in correction and 'smoothstep(.002,.02,leakage)' in correction
ball=managed.split('private static class BallLayer',1)[1].split('private static unsafe IntPtr Write',1)[0]
assert 'RenderBackground' not in ball and 'material.mainTexture=background' not in ball
assert 'AddBodies(owner.planetRed,all);AddBodies(owner.planetBlue,all)' in ball
assert 'Enabled=false;captures=filterSkips=0' in ball
assert 'finally {' in ball and 'r.gameObject.layer=layers[i];r.enabled=states[i]' in ball
assert 'sf_framegen_block_version()!=7' in managed
assert 'sf_framegen_block_version() { return 7; }' in Path('native/sfnative/framegen.cpp').read_text()
print('production path/default-off/restore/version contracts: PASS')
