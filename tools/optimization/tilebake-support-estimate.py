#!/usr/bin/env python3
"""Read-only, event-file upper bound for FloorBake; never loads the game.

Normal custom level / High / full remakeFloors assumed. Counts are unique tiles,
not summed event references. Unknowns never become confirmed runtime support.
"""
import argparse
import collections
import hashlib
import json
import math
from pathlib import Path

DEFAULTS = [Path('D:/얼불춤 맵 파일/Lost Requiem/Ludicin-Lost-Requiem.adofai'),
            Path('D:/얼불춤 맵 파일/HELLO (BPM) 2026/level.adofai')]
EASES = {'Linear', 'Flash', 'InFlash', 'OutFlash', 'InOutFlash'} | {
    prefix + family for prefix in ('In', 'Out', 'InOut')
    for family in ('Sine', 'Quad', 'Cubic', 'Quart', 'Quint', 'Expo', 'Circ', 'Elastic', 'Back', 'Bounce')}


def active(e):
    return e.get('active', True) not in (False, 'Disabled')


def tile_id(spec, origin, count):
    offset, relative = spec
    base = {'ThisTile': origin, 'Start': 0, 'End': count - 1}[relative]
    return max(0, min(count - 1, base + int(offset)))  # scnGame.IDFromTile


def targets(e, origin, count):
    lo, hi = sorted(tile_id(e[k], origin, count) for k in ('startTile', 'endTile'))
    step = 1 + int(e.get('gapLength', 0))
    if step <= 0:
        raise ValueError('invalid gap')
    return set(range(lo, hi + 1, step))


def analyse(data):
    count = len(data.get('angleData', data.get('pathData', []))) + 1
    if not 1 < count <= 100000:
        raise ValueError('tile-limit/missing geometry (limit 100000)')
    universe = set(range(count))
    events = [e for e in data.get('actions', []) if active(e)]
    excluded, unknown, features = (collections.defaultdict(set) for _ in range(3))
    by_floor = collections.defaultdict(list)
    for e in events:
        by_floor[int(e['floor'])].append(e)
    # MakeLevel propagates animation settings in floor order; omitted fields
    # preserve the previous value. PrepFloor makes one linked writer per floor.
    appear = data.get('settings', {}).get('trackAnimation', 'Fade')
    disappear = data.get('settings', {}).get('trackDisappearAnimation', 'Fade')
    for tile in range(count):
        for e in by_floor[tile]:
            if e['eventType'] == 'AnimateTrack':
                if e.get('trackAnimation') is not None:
                    appear = e['trackAnimation']
                if e.get('trackDisappearAnimation') is not None:
                    disappear = e['trackDisappearAnimation']
        if appear != 'None':
            excluded['writer:ffxFloorAppearPlus'].add(tile)
        if disappear != 'None':
            excluded['writer:ffxFloorDisappearPlus'].add(tile)

    repeats = collections.defaultdict(dict)
    conditions = collections.defaultdict(set)
    for e in events:
        if e['eventType'] == 'RepeatEvents':
            for tag in e.get('tag', '').split(' '):
                repeats[e['floor']][tag] = e
        if e['eventType'] == 'SetConditionalEvents':
            conditions[e['floor']].update(str(v) for k, v in e.items()
                                         if k.endswith('Tag') and v not in (None, '', 'NONE'))
        if 'FreeRoam' in e['eventType'] or 'Freeroam' in e['eventType']:
            unknown['freeroam-target-scope'].update(universe)
        if e['eventType'] in ('CallMethod', 'AddComponent', 'LightBridge'):
            excluded['writer-unknown-range:' + e['eventType']].update(universe)

    resets = []
    for index, e in enumerate(events):
        if e['eventType'] != 'MoveTrack':
            continue
        origin = int(e['floor'])
        tag = e.get('eventTag', '')
        conditional = bool(tag and tag in conditions[origin])
        origins = {origin}
        repeat = next((repeats[origin][tag] for tag in e.get('eventTag', '').split(' ')
                       if tag in repeats[origin]), None)
        # MakeLevel uses the original floor unless executeOnCurrentFloor=true.
        # Beat repeats have the same tile range; Floor repeats can move origin.
        if not conditional and repeat and repeat.get('repeatType', 'Beat') == 'Floor' and repeat.get('executeOnCurrentFloor', False):
            step = int(repeat.get('gapLength', 1))
            repetitions = int(repeat.get('floorCount', 1))
            if step <= 0 or repetitions > 100000:
                unknown['repeat-range-unmapped'].update(universe)
            else:
                origins.update(origin + j * step for j in range(1, repetitions + 1)
                               if origin + j * step < count)
        try:
            scope = set().union(*(targets(e, o, count) for o in origins))
        except (KeyError, TypeError, ValueError):
            unknown['range-gap-unmapped'].update(universe)
            continue
        if conditional:
            excluded['conditional/manual/hit'].update(scope)
        if tag:
            unknown['hitbox/manual-tag-binding'].update(scope)
        if e.get('maxVfxOnly', False) or e.get('minVfxOnly', False):
            excluded['visual-setting-condition'].update(scope)
        if e.get('ease', 'Linear') not in EASES:
            excluded['unknown-ease'].update(scope)
        duration = e.get('duration', 1)
        if not isinstance(duration, (float, int)) or not math.isfinite(duration) or duration < 0:
            excluded['duration-invalid'].update(scope)
        rotation = e.get('rotationOffset')
        if rotation is not None:
            # startRot.z and its Euler normalization are runtime snapshot values.
            unknown['rotation-wrap/origin-getter'].update(scope)
            if abs(rotation) >= 360:
                features['rotation-offset-outside-one-turn'].update(scope)
        scale = e.get('scale')
        if isinstance(scale, list) and any(v is not None for v in scale):
            features['scale-supported-channel'].update(scope)
            if len(scale) == 2 and all(v is not None for v in scale):
                unknown['scale-cross-axis-frame-dependent-skip'].update(scope)
        if scale == [100, 100] and e.get('positionOffset') == [0, 0] and rotation == 0:
            resets.append((index, e, targets(e, origin, count)))
    # These guards require live state; don't conflate them with file exclusions.
    unknown['runtime:initial/tweens/scene/foreign-patches/clock'].update(universe)
    blocked = set().union(*excluded.values()) if excluded else set()
    candidate = universe - blocked
    return count, excluded, unknown, features, candidate, resets


def report(path):
    if path.stat().st_size > 80_000_000:
        raise ValueError('file-size-limit (80MB)')
    raw = path.read_bytes()
    data = json.loads(raw.decode('utf-8-sig'))
    count, excluded, unknown, features, candidate, resets = analyse(data)
    print(f'\n## {path.name}\n\n파일: `{path}`\n\nSHA256: `{hashlib.sha256(raw).hexdigest()}`')
    print(f'\n타일 {count:,}개 (geometry 길이+시작 타일), normal custom / High / remakeFloors 가정.')
    print('\n| 구분 | 사유 | 고유 타일 | 전체 비율 |\n|---|---|---:|---:|')
    for label, rows in [('제외 추정', excluded), ('미정', unknown), ('기능/위험 노출', features)]:
        if not rows:
            print(f'| {label} | 해당 없음 | 0 | 0.000% |')
        for reason, scope in sorted(rows.items(), key=lambda x: (-len(x[1]), x[0])):
            print(f'| {label} | {reason} | {len(scope):,} | {len(scope)/count:.3%} |')
    for reason in ('conditional/manual/hit', 'visual-setting-condition', 'unknown-ease', 'duration-invalid'):
        if reason not in excluded:
            print(f'| 제외 추정 | {reason} | 0 | 0.000% |')
    if 'freeroam-target-scope' not in unknown:
        print('| 파일상 유무 | 자유 이동 이벤트 | 0 | 0.000% |')
    blocked = count - len(candidate)
    print(f'| 합집합 | 파일에서 제외 추정 | {blocked:,} | {blocked/count:.3%} |')
    print(f'| 후보 상한 | 알려진 제외가 없는 타일 | {len(candidate):,} | {len(candidate)/count:.3%} |')
    print('| 확정 지원 | 런타임 미정 때문에 확정 불가 | 미정 | 미정 |')
    print('\n사유는 중복 가능. 후보는 실제 지원률이 아닌 상한이며 회전·크기 근접 비교는 초기값/시간순서가 필요하다.')
    print('\n| 큰 전체 되돌리기 형태 (위치0/회전0/크기100%) | 범위 | 타일 | duration 비트 | opacity % | 후보 |\n|---|---|---:|---:|---:|---:|')
    for index, e, scope in sorted(resets, key=lambda x: len(x[2]), reverse=True)[:5]:
        print(f'| actions(active) #{index}, floor {e["floor"]} | {min(scope)}..{max(scope)} | {len(scope):,} | {e.get("duration")} | {e.get("opacity", "미사용")} | {len(scope & candidate):,}/{len(scope):,} |')
    if not resets:
        print('| 해당 형태 없음 | - | 0 | - | - | - |')
    print('\n다음에 풀 사유 (다른 제외를 고정했을 때 단독으로 되살아나는 수 / 현재 노출 수):')
    proposals = []
    for reason, scope in excluded.items():
        others = set().union(*(v for k, v in excluded.items() if k != reason))
        proposals.append((len(scope - others), len(scope), reason))
    for gain, exposure, reason in sorted(proposals, reverse=True)[:5]:
        print(f'- {reason}: 단독 +{gain:,}, 노출 {exposure:,}; FloorBake.ExcludeWriter/Copy, ffxChangeTrack.PrepFloor 근거.')
    if len(proposals) < 5:
        pending = [k for k in unknown if not k.startswith('runtime:')]
        for reason in sorted(pending, key=lambda k: -len(unknown[k]))[:5-len(proposals)]:
            print(f'- 미정 해소 {reason}: {len(unknown[reason]):,}타일; 지원 증가량 미정 (FloorBake.Snapshot/Collect, FloorBakeCore.Compile).')
    print('\n531.8초는 인계의 런타임 시각 식별자다. 이 도구는 tempo/entryTime을 복원하지 않아 시각·초 단위 길이를 확정하지 않는다.')


def self_test():
    data = {'angleData': [0] * 9, 'settings': {'trackAnimation': 'None', 'trackDisappearAnimation': 'None'},
            'actions': [{'floor': 2, 'eventType': 'MoveTrack', 'startTile': [0, 'ThisTile'],
                         'endTile': [0, 'End'], 'gapLength': 1, 'scale': [100, 100], 'rotationOffset': 0,
                         'positionOffset': [0, 0]},
                        {'floor': 3, 'eventType': 'AnimateTrack', 'trackDisappearAnimation': 'Fade'},
                        {'floor': 5, 'eventType': 'AnimateTrack', 'trackDisappearAnimation': 'None'}]}
    n, excluded, unknown, features, candidate, resets = analyse(data)
    assert n == 10 and excluded['writer:ffxFloorDisappearPlus'] == {3, 4}
    assert resets[0][2] == {2, 4, 6, 8} and len(candidate) == 8
    assert tile_id([-100, 'End'], 3, 10) == 0 and tile_id([99, 'ThisTile'], 3, 10) == 9
    data['actions'].extend([
        {'floor': 2, 'eventType': 'RepeatEvents', 'tag': 'a', 'repeatType': 'Floor', 'floorCount': 2,
         'gapLength': 1, 'executeOnCurrentFloor': True}])
    data['actions'][0]['eventTag'] = 'a'
    _, _, _, features, _, _ = analyse(data)
    assert features['scale-supported-channel'] == {2, 3, 4, 5, 6, 7, 8, 9}
    # MakeLevel breaks its repetition loop after setting conditionalInfo.
    data['actions'].append({'floor': 2, 'eventType': 'SetConditionalEvents', 'perfectTag': 'a'})
    _, excluded, _, features, _, _ = analyse(data)
    assert excluded['conditional/manual/hit'] == {2, 4, 6, 8}
    print('PASS estimator: ranges/clamps/gaps/animation inheritance/Floor repeat/conditional unique union')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('paths', nargs='*', type=Path)
    parser.add_argument('--self-test', action='store_true')
    args = parser.parse_args()
    if args.self_test:
        self_test()
    else:
        print('# tilebake 이벤트 파일 지원률 추정\n\n읽기 전용. 게임 실행/설치/파일 수정 없음.')
        for path in args.paths or DEFAULTS:
            if path.is_file():
                report(path)
            else:
                print(f'\n미정: 파일 없음 `{path}`')
