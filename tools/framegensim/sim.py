#!/usr/bin/env python3
"""프레임 생성 방식 비교 시뮬레이션 (게임 없이, 얼불춤 비슷한 2D 장면).

장면: 타일 길, 행성 두 개(한 박에 180도 도는 얼불춤 방식), 행성을 따라가는 카메라(부드럽게 따라감 + 박마다 확대 펄스),
따로 움직이는 장식 하나. 진짜 프레임은 약한 PC 처럼 기본 FPS 에 흔들림·가끔 긴 프레임을 섞어 만든다.
모니터 주사율(기본 164Hz)마다 각 방식이 무엇을 화면에 내는지 그려서, 정답(그 순간의 진짜 장면)과 비교한다.

방식
  none        진짜 프레임만 (지금)
  reproj      마지막 진짜 그림을 카메라가 움직인 만큼 옮김 (행성·장식은 멈춰 있음)
  reproj+pl   위 + 행성만 그 순간 위치로 다시 그림
  interp      두 진짜 프레임 사이를 만드는 보간 (보간 자체는 완벽하다고 가정 = 가장 좋게 봐준 경우)
  render      그리기만 하는 프레임 (그 순간 장면을 진짜로 그림, 로직은 건너뜀)

재는 것
  행성 나이   화면에 보이는 행성 위치가 몇 ms 전 것인가 (박자를 읽는 데 직접 영향 = 화면 지연)
  행성 오차   그 순간 진짜 행성 위치와 화면 위 거리(픽셀)
  화면 오차   그 순간 정답 그림과의 평균 픽셀 차이
  움직임 고르기  화면 간격마다 행성이 움직인 거리의 들쭉날쭉함(표준편차/평균, 작을수록 부드러움)

사용: python3 sim.py [--base 60] [--hz 164] [--seconds 4] [--gif out.gif]
"""
import argparse
import math
import random

import numpy as np
from PIL import Image, ImageDraw

W, H = 256, 144          # 화면 (작게)
PPU = 34.0               # 확대 1 일 때 한 칸 = 34 픽셀
OVER = 1.25              # 재투영용으로 화면보다 크게 그리는 비율(가장자리 빈 곳 막기)
BPM = 150.0
BEAT = 60.0 / BPM


# ── 장면 ──
def build_path(n=200, seed=3):
    rnd = random.Random(seed)
    ang = [0.0]
    for _ in range(n - 1):
        r = rnd.random()
        prev = ang[-1]
        if r < 0.55:
            ang.append(prev)                       # 직선 1박
        elif r < 0.8:
            ang.append((prev + 90) % 360)          # 위로 꺾기 1/2박 (시계 방향)
        else:
            ang.append((prev - 90) % 360)          # 아래로 꺾기 3/2박
    pos = [np.zeros(2)]
    for a in ang[:-1]:
        pos.append(pos[-1] + np.array([math.cos(math.radians(a)), math.sin(math.radians(a))]))
    # 타일마다 도착 시각: 돈 각도 = (a[i-1] + 180 - a[i]) mod 360 (0 이면 360), 박 = 각도/180
    t = [0.0]
    for i in range(1, n):
        travel = (ang[i - 1] + 180 - ang[i]) % 360 or 360
        t.append(t[-1] + travel / 180.0 * BEAT)
    return ang, pos, t


ANG, POS, TT = build_path()


def tile_at(t):
    lo, hi = 0, len(TT) - 1
    while lo < hi:
        mid = (lo + hi + 1) // 2
        if TT[mid] <= t:
            lo = mid
        else:
            hi = mid - 1
    return lo


def planets(t):
    """(중심 행성 위치, 도는 행성 위치, 중심이 빨강인가)"""
    k = tile_at(t)
    piv = POS[k]
    if k + 1 >= len(TT):
        return piv, piv + np.array([1.0, 0.0]), k % 2 == 0
    a_in = ANG[k - 1] if k > 0 else ANG[0]
    travel = (a_in + 180 - ANG[k]) % 360 or 360
    frac = (t - TT[k]) / (TT[k + 1] - TT[k])
    a = math.radians(a_in + 180 - travel * frac)
    return piv, piv + np.array([math.cos(a), math.sin(a)]), k % 2 == 0


# 카메라: 중심 행성을 부드럽게 따라감 (1ms 간격으로 미리 적분), 박마다 확대 펄스
CAM_DT = 0.001
_cam = []


def _build_cam(seconds):
    c = POS[0].copy()
    for i in range(int(seconds / CAM_DT) + 2):
        tgt, _, _ = planets(i * CAM_DT)
        c = c + (tgt - c) * (1 - math.exp(-CAM_DT * 7.0))
        _cam.append(c.copy())


def camera(t):
    i = min(max(int(t / CAM_DT), 0), len(_cam) - 1)
    ph = (t % BEAT) / BEAT
    zoom = 1.0 + 0.08 * math.exp(-ph * 9.0)
    return _cam[i], zoom


def deco(t):
    """따로 움직이는 장식 (월드 좌표, 카메라 근처를 돎)"""
    c, _ = camera(t)
    a = t * 2.3
    return c + np.array([2.6 * math.cos(a), 1.3 * math.sin(a * 1.7)])


# ── 그리기 ──
def to_screen(p, cam, zoom, w, h):
    s = PPU * zoom
    return (w / 2 + (p[0] - cam[0]) * s, h / 2 - (p[1] - cam[1]) * s)


def render(t, cam=None, zoom=None, scale=1.0, planets_on=True, planet_t=None):
    """시각 t 의 장면. cam/zoom 을 주면 그 카메라로. scale>1 이면 크게(여백 포함) 그림."""
    if cam is None:
        cam, zoom = camera(t)
    w, h = int(W * scale), int(H * scale)
    img = Image.new("RGB", (w, h), (14, 16, 22))
    d = ImageDraw.Draw(img)
    s = PPU * zoom
    k = tile_at(t)
    for i in range(max(0, k - 25), min(len(POS), k + 25)):
        x, y = to_screen(POS[i], cam, zoom, w, h)
        r = 0.42 * s
        if -r < x < w + r and -r < y < h + r:
            shade = 90 if i == k else 55
            d.rectangle([x - r, y - r, x + r, y + r], fill=(shade, shade + 8, shade + 22))
    dx, dy = to_screen(deco(t), cam, zoom, w, h)
    d.rectangle([dx - 0.25 * s, dy - 0.25 * s, dx + 0.25 * s, dy + 0.25 * s], fill=(200, 170, 60))
    if planets_on:
        pt = t if planet_t is None else planet_t
        piv, mov, red = planets(pt)
        for p, col in ((piv, (232, 82, 74) if red else (61, 139, 255)), (mov, (61, 139, 255) if red else (232, 82, 74))):
            x, y = to_screen(p, cam, zoom, w, h)
            r = 0.36 * s
            d.ellipse([x - r, y - r, x + r, y + r], fill=col)
    return img


def warp(img_big, cam0, zoom0, cam1, zoom1):
    """크게 그린 그림(cam0, zoom0)을 화면 크기 cam1, zoom1 시점으로 옮김 (카메라 이동·확대만)."""
    wb, hb = img_big.size
    # 화면 좌표 (u,v) -> 월드 -> 큰 그림 좌표
    s0, s1 = PPU * zoom0, PPU * zoom1
    a = s1 / s0
    # 월드 x = cam1.x + (u - W/2)/s1 ;  큰 그림 X = wb/2 + (x - cam0.x)*s0
    # X = wb/2 + (cam1.x - cam0.x)*s0 + (u - W/2)/a
    cx = wb / 2 + (cam1[0] - cam0[0]) * s0 - (W / 2) / a
    cy = hb / 2 - (cam1[1] - cam0[1]) * s0 - (H / 2) / a
    return img_big.transform((W, H), Image.AFFINE, (1 / a, 0, cx, 0, 1 / a, cy), resample=Image.BILINEAR, fillcolor=(0, 0, 0))


def planet_screen(t_content, t_display):
    """화면에 보이는 빨간 행성의 화면 위치: 행성은 t_content 것, 카메라는 t_display 것
    (도는 행성은 타일마다 빨강·파랑이 바뀌므로 색 하나를 따라가야 움직임이 이어진다)"""
    cam, zoom = camera(t_display)
    piv, mov, red = planets(t_content)
    return np.array(to_screen(piv if red else mov, cam, zoom, W, H))


# ── 진짜 프레임 ──
def real_frames(base, seconds, seed=7):
    rnd = random.Random(seed)
    frames, t = [], 0.05
    while t < seconds:
        dt = (1.0 / base) * math.exp(rnd.gauss(0, 0.15))
        if rnd.random() < 0.02:
            dt += rnd.uniform(0.02, 0.05)          # 가끔 긴 프레임
        frames.append((t, t + dt))                 # (게임이 시간을 읽은 시각, 화면에 낼 수 있게 된 시각)
        t += dt
    return frames


def run(base, hz, seconds, gif=None, gif_seconds=1.0):
    _cam.clear()
    _build_cam(seconds + 1)
    fr = real_frames(base, seconds)
    med = float(np.median([b - a for a, b in fr]))
    ticks = np.arange(0.3, seconds - 0.05, 1.0 / hz)
    methods = ["none", "reproj", "reproj+pl", "interp", "render"]
    age = {m: [] for m in methods}
    perr = {m: [] for m in methods}
    ierr = {m: [] for m in methods}
    ppos = {m: [] for m in methods + ["truth"]}
    big_cache = {}
    gif_frames = []
    j = 0
    for T in ticks:
        while j + 1 < len(fr) and fr[j + 1][1] <= T:
            j += 1
        s_k = fr[j][0]
        truth = np.asarray(render(T), dtype=np.int16)
        tp = planet_screen(T, T)
        ppos["truth"].append(tp)
        out = {}
        # none: 마지막 진짜 프레임 그대로
        out["none"] = (render(s_k), s_k, planet_screen(s_k, s_k))
        # reproj: 마지막 진짜 그림을 지금 카메라로 옮김
        if j not in big_cache:
            c0, z0 = camera(s_k)
            big_cache = {j: (render(s_k, c0, z0, OVER), render(s_k, c0, z0, OVER, planets_on=False), c0, z0)}
        big, big_np, c0, z0 = big_cache[j]
        c1, z1 = camera(T)
        out["reproj"] = (warp(big, c0, z0, c1, z1), s_k, planet_screen(s_k, T))
        # reproj+pl: 행성 없는 그림을 옮기고 행성만 지금 위치로
        im = warp(big_np, c0, z0, c1, z1)
        dr = ImageDraw.Draw(im)
        tp_pl = T - 0.001                          # 옮기기 + 행성 그리기 약 1ms 로 봄
        piv, mov, red = planets(tp_pl)
        for p, col in ((piv, (232, 82, 74) if red else (61, 139, 255)), (mov, (61, 139, 255) if red else (232, 82, 74))):
            x, y = to_screen(p, c1, z1, W, H)
            r = 0.36 * PPU * z1
            dr.ellipse([x - r, y - r, x + r, y + r], fill=col)
        out["reproj+pl"] = (im, tp_pl, planet_screen(tp_pl, T))
        # interp: 두 진짜 프레임 사이 (완벽한 보간으로 가정) - 다음 프레임을 기다려야 하므로 내용이 늦다
        c = min(max(T - 2.0 * med, fr[max(j - 1, 0)][0]), s_k)
        out["interp"] = (render(c), c, planet_screen(c, c))
        # render: 그 순간 진짜 장면 (그리기 시간 약 2ms 로 봄)
        out["render"] = (render(T - 0.002), T - 0.002, planet_screen(T - 0.002, T - 0.002))
        for m in methods:
            img, ct, pp = out[m]
            age[m].append((T - ct) * 1000)
            perr[m].append(float(np.linalg.norm(pp - tp)))
            ierr[m].append(float(np.abs(np.asarray(img, dtype=np.int16) - truth).mean()))
            ppos[m].append(pp)
        if gif and T < 0.3 + gif_seconds:
            gif_frames.append([Image.fromarray(truth.astype(np.uint8))] + [out[m][0] for m in methods])
    # 움직임 고르기: 화면 간격마다 행성 이동 거리의 변동계수
    smooth = {}
    for m in methods + ["truth"]:
        p = np.array(ppos[m])
        step = np.linalg.norm(np.diff(p, axis=0), axis=1)
        smooth[m] = float(step.std() / max(step.mean(), 1e-6))
    print(f"\n기본 {base} FPS (중앙값 프레임 {med*1000:.1f}ms), 모니터 {hz}Hz, {seconds}초, 화면 {len(ticks)}장")
    print(f"{'방식':<10} {'행성 나이 평균/95%(ms)':>22} {'행성 오차 평균/95%(px)':>22} {'화면 오차':>8} {'움직임 들쭉날쭉':>14}")
    print(f"{'(정답)':<10} {'':>22} {'':>22} {'':>8} {smooth['truth']:>14.2f}")
    for m in methods:
        a, e = np.array(age[m]), np.array(perr[m])
        print(f"{m:<10} {a.mean():>12.1f} / {np.percentile(a,95):>6.1f}  {e.mean():>12.1f} / {np.percentile(e,95):>6.1f}  {np.mean(ierr[m]):>8.2f} {smooth[m]:>14.2f}")
    if gif:
        labels = ["truth", "none", "reproj", "reproj+pl", "interp", "render"]
        frames = []
        for row in gif_frames:
            canvas = Image.new("RGB", (W * 3 + 8, H * 2 + 8 + 28), (0, 0, 0))
            dd = ImageDraw.Draw(canvas)
            for i, im in enumerate(row):
                x, y = (i % 3) * (W + 4), (i // 3) * (H + 18) + 14
                canvas.paste(im, (x, y))
                dd.text((x + 3, y - 13), labels[i], fill=(230, 230, 230))
            frames.append(canvas)
        frames[0].save(gif, save_all=True, append_images=frames[1:], duration=40, loop=0)
        print(f"GIF: {gif} ({len(frames)}장, 화면 한 장을 40ms 로 = 약 {hz*0.04:.1f}배 느리게)")


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", type=float, nargs="+", default=[60.0])
    ap.add_argument("--hz", type=float, default=164.0)
    ap.add_argument("--seconds", type=float, default=4.0)
    ap.add_argument("--gif", default="")
    args = ap.parse_args()
    for i, b in enumerate(args.base):
        run(b, args.hz, args.seconds, gif=(args.gif if i == 0 and args.gif else None))
