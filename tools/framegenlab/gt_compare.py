"""생성 화면을 같은 곡 시각의 진짜 고프레임(ground truth)과 맞대어 본다.

  py gt_compare.py <진짜 덤프 폴더> <생성 덤프 폴더> [너비=1280 높이=720] [축소=4]

덤프는 dupcap.exe 의 d###_<QPC초>.bgra (BGRA, 1280x720). 진짜 쪽은 프레임 늘리기를 끄고 진짜 165 FPS 로
돌린 것(곡 시각 같은 자리, 1초 남짓), 생성 쪽은 60 FPS + 프레임 늘리기.
생성 그림마다 진짜 그림들 중 가장 비슷한 것을 찾아(이웃 둘로 포물선 보간) 그 진짜 시각을 "그림이 나타내는 시각"으로 읽는다.
 - 시각 고름: 이웃한 생성 그림의 표시 간격 대비 그림 시각 증가량(1 이면 정확). 표준편차가 클수록 덜컹임.
 - 닮음(MAD): 가장 비슷한 진짜 그림과의 평균 절대 차이(0~255, 회색 축소). 작을수록 진짜 같음.
   같은 간격의 이웃 진짜 그림끼리의 차이(움직임 크기)를 함께 적어 기준으로 삼는다.
순수 PIL 만 쓴다(numpy 없음).
"""
import os, sys, glob, re, statistics as st
from PIL import Image, ImageChops, ImageStat

BOX = None
for a in sys.argv[1:]:
    if a.startswith('--box='):
        BOX = tuple(int(v) for v in a[6:].split(','))
sys.argv = [a for a in sys.argv if not a.startswith('--box=')]
W = int(sys.argv[3]) if len(sys.argv) > 3 else 1280
H = int(sys.argv[4]) if len(sys.argv) > 4 else 720
S = int(sys.argv[5]) if len(sys.argv) > 5 else 4

def load(path):
    raw = open(path, 'rb').read()
    im = Image.frombuffer('RGBA', (W, H), raw, 'raw', 'BGRA', 0, 1).convert('L')
    if BOX: im = im.crop(BOX)
    return im.resize((im.width // S, im.height // S), Image.BILINEAR)

def files(d):
    out = []
    for f in glob.glob(os.path.join(d, 'd*.bgra')):
        m = re.match(r'd(\d+)_([0-9.]+)\.bgra$', os.path.basename(f))
        if m and os.path.getsize(f) == W * H * 4:
            out.append((float(m.group(2)), f))
    out.sort()
    return out

def mad(a, b):
    return ImageStat.Stat(ImageChops.difference(a, b)).mean[0]

gt = files(sys.argv[1]); gen = files(sys.argv[2])
if len(gt) < 8 or len(gen) < 4:
    print('덤프가 모자람: 진짜 %d, 생성 %d' % (len(gt), len(gen))); sys.exit(1)
G = [load(f) for _, f in gt]; T = [t for t, _ in gt]
X = [load(f) for _, f in gen]; XT = [t for t, _ in gen]
step = [mad(G[i], G[i + 1]) for i in range(len(G) - 1)]
print('진짜 %d장 (%.3f초), 생성 %d장 (%.3f초); 진짜 이웃 차이 중앙 %.2f' % (len(G), T[-1] - T[0], len(X), XT[-1] - XT[0], st.median(step)))
pos = []; best = []
for k, x in enumerate(X):
    errs = [mad(x, g) for g in G]
    j = min(range(len(errs)), key=errs.__getitem__)
    t = T[j]
    if 0 < j < len(errs) - 1:
        a, b, c = errs[j - 1], errs[j], errs[j + 1]
        den = a - 2 * b + c
        if den > 1e-9:
            d = max(-.5, min(.5, .5 * (a - c) / den))
            t = T[j] + d * ((T[j + 1] - T[j]) if d > 0 else (T[j] - T[j - 1]))
    pos.append(t); best.append(errs[j])
inc = []
for k in range(1, len(X)):
    dt = XT[k] - XT[k - 1]
    if dt > 0.0025:
        inc.append(((pos[k] - pos[k - 1]) / dt, k))
vals = [v for v, _ in inc]
print('닮음 MAD: 중앙 %.2f, 최대 %.2f (진짜 이웃 차이 중앙 %.2f)' % (st.median(best), max(best), st.median(step)))
if vals:
    print('시각 증가/표시 간격: 중앙 %.2f, 표준편차 %.2f, 최소 %.2f, 최대 %.2f  (n=%d)' % (st.median(vals), st.pstdev(vals), min(vals), max(vals), len(vals)))
print(' '.join('%.2f' % v for v in vals[:40]))
