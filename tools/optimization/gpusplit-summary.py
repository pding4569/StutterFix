#!/usr/bin/env python3
"""Summarize one eight-window gpusplit run; never treats absent samples as zero."""
import argparse
from dataclasses import dataclass, field
from pathlib import Path
import re

ORDER = ['none', 'deco-image', 'tiles', 'planets', 'deco-text+deco-particle', 'other', 'all', 'none']


@dataclass
class Window:
    group: str
    start: float | None
    end: float | None = None
    completed: int = 0
    cameras: dict = field(default_factory=dict)
    quality: dict = field(default_factory=dict)


def parse(text):
    windows, current, group, song = [], None, 'none', None
    for line in text.splitlines():
        match = re.search(r'\[gpusplit\] hide=(\S+)', line)
        if match:
            if current is not None:
                raise ValueError('측정 창 안에서 숨김 그룹 변경/refresh 발생')
            group = match[1]
        match = re.search(r'\[archegpu\] stamp song=([\d.eE+-]+)', line)
        if match:
            song = float(match[1])
            if current is not None:
                current.end = song
        if '[gpuprobe] reset queued' in line:
            current = Window(group, song)
            windows.append(current)
        if current is None:
            continue
        if re.search(r'\[gpuprobe\] ready=', line):
            current.quality = {k: int(v) for k, v in re.findall(r'(\w+)=(\d+)', line)}
            current.completed = current.quality.get('completed', 0)
            current.cameras.clear()  # state retry replaces the whole prior snapshot
        match = re.search(r'\[gpuprobe\] camera:([^#]+)#(-?\d+) active_frames=(\d+) calls=(\d+) mean_frame_ms=([\d.eE+-]+)', line)
        if match:
            current.cameras[match[1] + '#' + match[2]] = (float(match[5]), int(match[3]))
        if '[gpuprobe] residual_frame_minus_cameras_ms=' in line:
            current = None
    return windows


def summarize(windows, camera='Camera'):
    if [w.group for w in windows] != ORDER:
        raise ValueError('창 순서/개수 불일치: ' + ', '.join(w.group for w in windows))
    means, camera_id = [], None
    for i, w in enumerate(windows):
        matches = [(k, v) for k, v in w.cameras.items() if k.rsplit('#', 1)[0] == camera]
        if w.completed <= 0 or len(matches) != 1 or matches[0][1][1] <= 0:
            raise ValueError(f'창 {i+1}: 유효한 본 카메라 표본이 없음/모호함')
        if w.start is None or w.end is None or not 3.5 <= w.end - w.start <= 4.5:
            raise ValueError(f'창 {i+1}: stamp 누락/4초 창 이탈 ({w.start}, {w.end})')
        key, (mean, active) = matches[0]
        if camera_id is not None and key != camera_id:
            raise ValueError('창 사이 본 카메라 ID 변경')
        camera_id = key
        # Mean over completed frames is only comparable when this camera ran every sampled frame.
        if active != w.completed:
            raise ValueError(f'창 {i+1}: 본 카메라 active_frames != completed')
        if w.quality.get('ready') != 1 or any(w.quality.get(k, 0) for k in ('errors', 'overflow', 'unknown_camera', 'unknown_effect', 'effect_outside_camera')):
            raise ValueError(f'창 {i+1}: gpuprobe 계측 오류 {w.quality}')
        means.append(mean)
    baseline = (means[0] + means[-1]) / 2
    drift = means[-1] - means[0]
    lines = [f'본 카메라: {camera_id}; 기준 평균 {baseline:.4f} ms; 기준 차이(뒤−앞) {drift:+.4f} ms',
             f'드리프트 오차 지표 ±{abs(drift):.4f} ms (통계적 신뢰구간 아님)', '',
             '| 창 | 곡 시각(s) | GPU 평균(ms) | 기준−숨김(ms) | completed |',
             '|---|---:|---:|---:|---:|']
    for i, (w, mean) in enumerate(zip(windows, means)):
        delta = '—' if i in (0, 7) else f'{baseline-mean:+.4f}'
        lines.append(f'| {i+1} {w.group} | {w.start:.3f}–{w.end:.3f} | {mean:.4f} | {delta} | {w.completed} |')
    total = sum(baseline - means[i] for i in range(1, 6))
    all_delta = baseline - means[6]
    lines += ['', f'그룹 몫 합: {total:+.4f} ms; 전체 숨김 차이: {all_delta:+.4f} ms; 불일치(합−전체): {total-all_delta:+.4f} ms',
              '시간에 따른 내용·오버드로·공유 패스·GPU 유휴 변화가 섞인 제거 차이 추정치다. 음수도 보존한다.',
              'gpuprobe는 GPU timestamp 경과 시간이며 GPU busy/순수 shader 시간은 아니다. 필터는 본 카메라에 포함된다.']
    for i, w in enumerate(windows):
        if any(w.quality.get(k, 0) for k in ('invalid', 'ring_full', 'not_ready')):
            lines.append(f'창 {i+1} 누락/미회수 누적 카운터: {w.quality}')
    return '\n'.join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('log', type=Path)
    parser.add_argument('-o', '--output', type=Path)
    parser.add_argument('--camera', default='Camera')
    args = parser.parse_args()
    try:
        report = summarize(parse(args.log.read_text(encoding='utf-8-sig', errors='replace')), args.camera)
    except ValueError as exc:
        parser.exit(2, f'측정 거부: {exc}\n')
    if args.output:
        args.output.write_text(report + '\n', encoding='utf-8')
    else:
        print(report)


if __name__ == '__main__':
    main()
