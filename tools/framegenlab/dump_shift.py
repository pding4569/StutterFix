"""dupcap 덤프(.bgra, 1280x720)의 연속 갱신 쌍마다 전체 화면의 이동량(dx,dy)을 찾는다.
부드러운 보간이면 이동량이 비슷하게 이어지고, 계단이면 0 과 2배가 번갈아 나오며, 떨림이면 방향이 뒤집힌다.
  py dump_shift.py <덤프 폴더> [가장 최근 N초 안의 파일만=30] [축소=4]
순수 PIL (ImageChops)만 쓴다."""
import os, sys, glob
from PIL import Image, ImageChops, ImageStat
W, H = 1280, 720
def load(p, s):
    return Image.frombuffer('RGBA', (W, H), open(p, 'rb').read(), 'raw', 'BGRA', 0, 1).convert('L').resize((W // s, H // s))
def shift(a, b, r=8):
    w, h = a.size
    box = (r, r, w - r, h - r)
    ca = a.crop(box); best = None
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            cb = b.crop((r + dx, r + dy, w - r + dx, h - r + dy))
            m = ImageStat.Stat(ImageChops.difference(ca, cb)).mean[0]
            if best is None or m < best[0]: best = (m, dx, dy)
    return best
d = sys.argv[1]; win = float(sys.argv[2]) if len(sys.argv) > 2 else 30; s = int(sys.argv[3]) if len(sys.argv) > 3 else 4
files = [f for f in glob.glob(os.path.join(d, 'd*.bgra')) if os.path.getsize(f) == W * H * 4]
files.sort(key=os.path.getmtime); newest = os.path.getmtime(files[-1])
files = [f for f in files if newest - os.path.getmtime(f) < win]; files.sort()
imgs = [load(f, s) for f in files]
out = []
for i in range(1, len(imgs)):
    m, dx, dy = shift(imgs[i - 1], imgs[i])
    out.append((dx * s, dy * s, m))
print('n=%d  (이동 dx,dy 픽셀 원본 기준 / 남은 차이)' % len(out))
print(' '.join('(%d,%d|%.1f)' % o for o in out))
