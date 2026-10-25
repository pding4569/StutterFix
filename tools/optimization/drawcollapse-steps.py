"""DrawCollapse 2관문 ABBA·원본 픽셀 계획을 생성한다. 게임 실행·설치 기능 없음.

기존 baseline/abba/single 모드의 명시적 출력은 보존한다.
"""
import argparse
import json
from pathlib import Path


def window(cached=None, single=None):
    option = 'submitsingle' if single is not None else 'latecache'
    selected = single if single is not None else cached
    steps = [] if selected is None else ['gpudeco ' + option + ' ' + ('on' if selected else 'off')]
    steps += ['seek 260 12', 'decobake ready', 'waitsongpos 256 40',
              'gpudeco packets compiled', 'gpudeco live on', 'waitsongpos 260 40',
              'gpudeco live census']
    if selected is not None:
        steps += ['gpudeco ' + option + ' validate']
    steps += ['fuse begin', 'onupdate begin', 'phasewindow begin', 'gpudeco micro begin',
              'waitsongpos 270 25', 'gpudeco micro end', 'phasewindow end', 'onupdate end', 'fuse end']
    if selected is not None:
        steps += ['gpudeco ' + option + ' validate', 'gpudeco ' + option + ' state']
    return steps + ['gpudeco live off', 'stop']


def collapse(chart):
    root = Path(__file__).resolve().parents[2]
    bundle = root / 'tools/gpudeco-lab/evidence/drawcollapse4/bundle/stutterfix_gpudeco'
    steps = ['timeout 600', 'auto on', 'decobake on', 'gpudeco off',
             'gpudeco reset', 'gpudeco drawcollapse reset', 'gpudeco costprobe off',
             'gpudeco effectcache off', 'gpudeco submitsingle off', 'gpudeco latecache off',
             'gpudeco nombp off', 'gpudeco nativebatch on', 'gpudeco nativecopy on8masked',
             'gpudeco zeroalpha on', 'gpudeco bundle ' + bundle.as_posix(),
             'open ' + str(chart), 'gpuprobe off', 'gpusplit off', 'matshare off', 'livedeco off',
             'select 0', 'play', 'gpudeco scheduled', 'pause', 'decobake ready',
             'gpudeco live scope song', 'gpudeco drawcollapse prepare', 'gpudeco live prepare',
             'resume', 'stop']

    def enter():
        return ['seek 260 12', 'decobake ready', 'waitsongpos 256 40',
                'gpudeco packets compiled', 'gpudeco live on']

    for index, merged in enumerate((False, True, True, False)):
        steps += enter() + ['waitsongpos 260 40', 'gpudeco drawcollapse ready',
                            'gpudeco drawcollapse ' + ('on' if merged else 'off'),
                            f'gpudeco drawcollapse begin {index}', 'waitsongpos 270 30',
                            'gpudeco drawcollapse end', 'gpudeco drawcollapse off',
                            'gpudeco live off', f'gpudeco drawcollapse restore {index}', 'stop']
    for index, second in enumerate((260, 262, 270)):
        steps += enter() + [f'waitsongpos {second} 40', 'pause', 'gpudeco live off',
                            'framecmp clear', 'framecmp mask none', 'framecmp filters on',
                            'framecmp ui exclude', 'framecmp grab O', 'gpudeco live on',
                            'gpudeco drawcollapse ready', 'framecmp noise A N',
                            'gpudeco drawcollapse on', 'framecmp grab B',
                            'gpudeco drawcollapse off', 'framecmp grab A2',
                            f'gpudeco drawcollapse pixel {index}']
        steps += [f'framecmp save {name}' for name in ('O', 'A', 'N', 'B', 'A2')]
        steps += ['framecmp clear', 'gpudeco live off', 'resume', 'stop']
    steps += ['gpudeco drawcollapse result', 'quit']
    return dict(steps=steps, settings={'FrameGenOutside': 0, 'FrameGenRefresh': False},
                timeout_min=25, tag='drawcollapse-gate2-260-270-abba4',
                notes={'status': '생성만; 새 번들의 Unity 빌드·재로드 성공 뒤 외부에서만 실행',
                       'bundle': bundle.as_posix(),
                       'A': '독립 GPU', 'B': '구간당 단일 서브메시, EvaluateCollapse 직접 기록',
                       'pixels': '260/262/270초 CPU 원본 O 대 병합 B; 복구 A/A2 및 잡음 A/N',
                       'ownership': '재개 전 live off; ABBA 중 pause/resume 및 캡처 없음',
                       'acceptance': 'maxdiff<=3; B FPS>=220; engine draw<=300; spikes>=30ms=0',
                       'environment': '같은 프로세스 내 해상도·주사율 유지; 디스플레이 변경 없음',
                       'installation_restore': '외부 실행기 책임; 이번에는 설치·게임 실행 없음',
                       'full_lifetime': '전곡·retry·reopen·UMM unload 별도 미검증'})


def build(chart, mode='collapse'):
    if mode == 'collapse':
        return collapse(chart)
    if mode == 'diag':
        base = build(chart, 'baseline')
        # 검증된 곡 처음 준비 -> seek -> live on 순서는 그대로 쓴다.
        steps = base['steps'][:base['steps'].index('phasewindow on')]
        steps[3:3] = ['gpudeco submitsingle off', 'gpudeco latecache off']
        for second in (260, 262, 264, 266, 268, 270):
            # 정지 후 재개는 Hitch.SongStarted -> EffectBudget.Suspend의
            # 60프레임 원본 실행 보호를 다시 건다. 소유 중 resume하지 않고
            # 각 표본을 독립 seek로 채취한다. 기존 보호 경로는 바꾸지 않는다.
            steps += ['seek 260 12', 'decobake ready', 'waitsongpos 256 40',
                      'gpudeco packets compiled', 'gpudeco live on',
                      f'waitsongpos {second} 40', 'pause',
                      'gpudeco drawcollapse validate', 'gpudeco drawcollapse diag',
                      'gpudeco live off', 'resume', 'stop']
        steps += ['quit']
        return dict(steps=steps, settings=base['settings'], timeout_min=25,
                    tag='drawcollapse-gate1-260-270-diag2-isolated')
    if mode not in ('baseline', 'abba', 'single'):
        raise ValueError('unknown mode: ' + mode)
    steps = ['gpudeco costprobe off', 'gpudeco effectcache off', 'timeout 600',
             'auto on', 'decobake on', 'gpudeco off', 'gpudeco reset', 'gpudeco nombp off',
             'gpudeco g2 reset', 'gpudeco nativebatch on', 'gpudeco nativecopy on8masked',
             'gpudeco zeroalpha on',
             'gpudeco bundle E:/StutterFixWork/wt-gpu/tools/gpudeco-lab/evidence/bundle/stutterfix_gpudeco',
             'open ' + str(chart), 'select 0', 'play', 'gpudeco scheduled', 'pause',
             'decobake state', 'decobake ready', 'gpudeco live scope song', 'gpudeco live prepare',
             'resume', 'stop', 'gpuprobe off', 'gpusplit off', 'matshare off', 'livedeco off', 'phasewindow on']
    if mode == 'single':
        steps += ['gpudeco latecache off']
        for single in (False, True, True, False):
            steps += window(single=single)
    else:
        for cached in ((None, None) if mode == 'baseline' else (False, True, True, False)):
            steps += window(cached)
    steps += ['quit']
    return dict(steps=steps, settings={'FrameGenOutside': 0, 'FrameGenRefresh': False},
                timeout_min=25, tag={'baseline': 'drawcollapse-260-270', 'abba': 'modcpu-260-270-abba-latecache1',
                                     'single': 'drawcollapse-260-270-abba-submitsingle2'}[mode])


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--chart', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--mode', choices=('collapse', 'diag', 'baseline', 'abba', 'single'), default='collapse')
    args = parser.parse_args()
    path = Path(args.output)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(build(args.chart, args.mode), ensure_ascii=False, indent=1), encoding='utf-8')


if __name__ == '__main__':
    main()
