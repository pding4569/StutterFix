#!/usr/bin/env python3
"""Read one explicitly named .adofai; never load images, run the game or write files.

Unique source decoration rows, NOT runtime instance counts. Defaults/migrations and
runtime-only predicates remain unknown. stdout is Markdown suitable for a handoff.
"""
import argparse
from collections import Counter, defaultdict
import hashlib
import json
import math
from pathlib import Path
import re


STRING = r'"(?:\\.|[^"\\])*"'
EASES = {"Linear", "INTERNAL_Zero"} | {
    prefix + name for prefix in ("In", "Out", "InOut")
    for name in ("Sine", "Quad", "Cubic", "Quart", "Quint", "Expo", "Circ", "Elastic", "Back", "Bounce", "Flash")
} | {"Flash"}
INITIAL = "초기 배치 Global 이외"
SCALE2 = "크기 지연 Startup/축 간 읽기 순서"
EVIDENCE = {
    INITIAL: "DecoBake.SetupPostfix: placementType != Global; scrDecoration.SetPlacementType의 Tile/Camera 분기",
    SCALE2: "bake-2 Reasons의 scaleUsed 조건; bake-3 CopyScale로 해제",
    "이미지 이외 장식": "SetupPostfix: !(d is scrVisualDecoration)",
    "가시성/깊이 변경": "Reasons: visibleUsed || depthUsed",
    "이미지/원래크기/크롭/wrap": "Reasons: imageFilenameUsed || originalSizeUsed || smoothingUsed || cropPivotOffsetUsed",
    "Camera-동적 화면 투영": "UpdateScreenClamp + scrParallax.SetTrans: pixelRect/ScreenToWorldPoint; 줌/비율 종속",
    "components의 직접 쓰기/조건 영향": "scrDecoration.Setup: AddComponent/FieldInfo.SetValue; 작성자와 필드 영향 분석 필요",
}


def load_level(raw):
    s = raw.decode("utf-8-sig")
    repairs = Counter()

    def trailing(m):
        if m[0].startswith('"'):
            return m[0]
        repairs["후행 쉼표"] += 1
        return ""

    s = re.sub(STRING + r'|,\s*(?=[}\]])', trailing, s)

    def boundary(m):
        if m[0].startswith('"'):
            return m[0]
        repairs["decorations 앞 구분 쉼표 누락"] += 1
        return m[0] + ","

    # Known legacy exporter omission. Strings are consumed first, never rewritten.
    s = re.sub(STRING + r'|\]\s*(?="decorations"\s*:)', boundary, s)
    return json.loads(s), repairs


def on(v):
    return v is True or v in ("Enabled", "True")


def used(e, key):
    # Raw export only. Actual evnt.disabled is populated by the game's schema.
    return key in e and e[key] is not None


def tags(value):
    return set(str(value or "").split()) or {"NO TAG"}


def nonfinite(value):
    if isinstance(value, (int, float)):
        return not math.isfinite(value)
    if isinstance(value, list):
        return any(nonfinite(x) for x in value if x is not None)
    return False


def estimate(level, version=4):
    decs = level.get("decorations", [])
    if any(str(e.get("eventType", "")).startswith("AddDec") for e in level.get("actions", [])):
        raise ValueError("actions 내부 생성 장식은 인스턴스 대응 미구현: 지원률 추정 거부")
    reasons = [set() for _ in decs]
    unknown = [set() for _ in decs]
    index = defaultdict(set)
    for i, d in enumerate(decs):
        for t in tags(d.get("tag")):
            index[t].add(i)
        r, u = reasons[i], unknown[i]
        if d.get("eventType") != "AddDecoration":
            r.add("이미지 이외 장식")
        if "relativeTo" not in d:
            u.add("초기 배치 기본값/버전 변환")
        elif version == 3 and d["relativeTo"] != "Global":
            r.add(INITIAL)
        elif version == 4:
            placement = d["relativeTo"]
            if placement in ("Camera", "CameraAspect"):
                r.add("Camera-동적 화면 투영")
            elif placement == "Tile" and on(d.get("stickToFloor")):
                r.add("Tile-동적 바닥 추종")
            elif placement not in ("Global", "Tile"):
                r.add("초기 배치 미지원")
        if d.get("maskingType", "None") != "None":
            r.add("초기 마스크")
        if on(d.get("failHitbox")) or d.get("hitbox", "None") != "None":
            r.add("히트박스/충돌")
        if any(on(d.get(k)) for k in ("lockRotation", "lockScale", "stickToFloor")):
            r.add("추종/고정/바닥 배치")
        if d.get("relativeTo") in ("RedPlanet", "BluePlanet", "GreenPlanet"):
            r.add("추종/고정/바닥 배치")
        if any(nonfinite(d.get(k)) for k in ("position", "pivotOffset", "scale", "rotation", "opacity", "parallax", "parallaxOffset")):
            r.add("초기값 비유한")
        if str(d.get("components", "")).strip():
            u.add("components의 직접 쓰기/조건 영향")
    scale_targets = set()
    moves, unmatched = 0, 0
    for e in level.get("actions", []):
        kind = e.get("eventType", "")
        if kind == "MoveDecorations":
            moves += 1
            target = set().union(*(index[t] for t in tags(e.get("tag"))))
            if not target:
                unmatched += 1
            r = set()
            if used(e, "scale"):
                scale_targets.update(target)
            if any(used(e, k) for k in ("decorationImage", "originalSize", "cropPivotOffset", "imageSmoothing")):
                r.add("이미지/원래크기/크롭/wrap")
            if any(used(e, k) for k in ("maskingType", "maskingTarget", "useMaskingDepth", "maskingFrontDepth", "maskingBackDepth")):
                r.add("마스크 변경")
            if any(used(e, k) for k in ("visible", "depth")):
                r.add("가시성/깊이 변경")
            if used(e, "relativeTo") and e["relativeTo"] not in ("Global", "LastPosition"):
                placement = e["relativeTo"]
                r.add("Global 이외 배치 변경" if version == 3 else
                      "Tile-동적 효과 시점 재배치" if placement == "Tile" else
                      "Camera-동적 배치 변경" if placement in ("Camera", "CameraAspect") else "Global 이외 배치 변경")
            if e.get("ease", "Linear") not in EASES:
                r.add("미확인/사용자 이징")
            if nonfinite(e.get("duration", 0)) or e.get("duration", 0) < 0:
                r.add("길이 비유한/음수")
            if any(nonfinite(e.get(k)) for k in ("scale", "positionOffset", "pivotOffset", "rotationOffset", "opacity", "parallax", "parallaxOffset")):
                r.add("목표값 비유한")
            if any(on(e.get(k)) for k in ("minVfxOnly", "maxVfxOnly")):
                r.add("시각 효과 설정 조건")
            if any(on(e.get(k)) for k in ("runManually", "runOnHit")) or e.get("conditionalInfo"):
                r.add("조건/수동/타격")
            for i in target:
                reasons[i].update(r)
        elif "Decora" in kind or kind in ("CallMethod", "AddComponent"):
            # Same conservative global exclusion as UnknownWriter, subject to runtime class mapping.
            for r in reasons:
                r.add("다른 장식 효과/태그 변경 가능")
        elif kind not in {"MoveTrack", "SetFilter", "MoveCamera", "Twirl", "Flash", "ShakeScreen", "RecolorTrack", "SetSpeed", "Bloom", "PositionTrack", "RepeatEvents", "AnimateTrack", "Pause", "Bookmark", "CustomBackground", "SetText", "ScaleRadius"}:
            for u in unknown:
                u.add("미분류 이벤트 종류: " + kind)
    bake2 = [r | ({SCALE2} if i in scale_targets else set()) for i, r in enumerate(reasons)]
    return decs, bake2, reasons, unknown, moves, unmatched


def report(path):
    raw = path.read_bytes()
    level, repairs = load_level(raw)
    decs, _, b3, unknown, moves, unmatched = estimate(level)
    b2 = estimate(level, version=3)[2]  # comparison: bake-3, current: bake-4
    n = len(decs)
    if not n:
        raise ValueError("장식 0개")
    pct = lambda x: f"{x:,} ({100*x/n:.4f}%)"
    print(f"파일: `{path}`\n\nSHA256: `{hashlib.sha256(raw).hexdigest()}`\n")
    print(f"소스 장식 행 {n:,}: " + ", ".join(f"{k} {v:,}" for k, v in Counter(d.get('eventType') for d in decs).items()))
    print(f"\nMoveDecorations {moves:,}; 대상 없는 원본 이벤트 {unmatched:,}; 메모리 내 문법 보정 {dict(repairs)}\n")
    print("| 사유(각 사유 안 고유 장식, 사유 간 중복) | bake-3 | bake-4 |")
    print("|---|---:|---:|")
    counts = [Counter(r for row in rows for r in row) for rows in (b2, b3)]
    standard = {INITIAL, "Tile-동적 바닥 추종", "Tile-동적 효과 시점 재배치", "Camera-동적 화면 투영", "Camera-동적 배치 변경", "초기 배치 미지원", "초기 마스크", "마스크 변경", "히트박스/충돌", "이미지 이외 장식", "이미지/원래크기/크롭/wrap", "가시성/깊이 변경", "미확인/사용자 이징", "조건/수동/타격", "시각 효과 설정 조건", "추종/고정/바닥 배치", "Global 이외 배치 변경", "초기값 비유한", "목표값 비유한", "길이 비유한/음수", "다른 장식 효과/태그 변경 가능"}
    for reason in sorted(standard | counts[0].keys() | counts[1].keys(), key=lambda r: (-counts[1][r], -counts[0][r], r)):
        print(f"| {reason} | {pct(counts[0][reason])} | {pct(counts[1][reason])} |")
    print("\n| 구분 | bake-3 | bake-4 |\n|---|---:|---:|")
    candidates = [sum(not r and not u for r, u in zip(rows, unknown)) for rows in (b2, b3)]
    print(f"| 파일상 제외 없음(조건부 상한) | {pct(candidates[0])} | {pct(candidates[1])} |")
    print(f"| 파일상 제외 있음 | {pct(sum(bool(r) for r in b2))} | {pct(sum(bool(r) for r in b3))} |")
    print(f"| 파일 내 추가 미정 있음(중복 가능) | {pct(sum(bool(u) for u in unknown))} | 同左 |")
    print(f"| 최종 런타임 지원 판정 미정 | {pct(n)} | {pct(n)} |")
    print("\n이벤트 파일만으로 확정 지원은 두 버전 모두 미정. 런타임 미정과 파일상 제외는 중복한다. 실제 지원율은 bake-3 0~"
          f"{100*candidates[0]/n:.4f}%, bake-4 0~{100*candidates[1]/n:.4f}%의 조건부 범위다.")
    print("\n| 미정 조건 | 영향 범위 |\n|---|---:|")
    for r in ("시차 부품 생성/축 고정/화면 고정", "외부 모드 setter 패치/직접 쓰기", "High/커스텀 일반 맵/pitch/DOTween 고정 시계", "에디터 버전 변환/disabled 기본값/실제 초기 비유한값", "예약 목록 밖 효과/히트박스 조건 연결", "실제 생성 인스턴스/이미지 로딩/실행 순서"):
        print(f"| {r} | {pct(n)} |")
    for r, v in Counter(r for row in unknown for r in row).most_common():
        print(f"| {r} | {pct(v)} |")
    print("\n| bake-4 완화 우선순위 | 해당 사유 수 | 단독 해제 시 순증 후보 | 앞 순위와 누적 해제 시 추가 후보 | 코드 근거 |\n|---|---:|---:|---:|---|")
    # Include file-local unknown components as an independent research proposal.
    # This does not mark them supported in the candidate count above.
    b3 = [r | u for r, u in zip(b3, unknown)]
    counts[1] = Counter(r for row in b3 for r in row)
    proposals = []
    for reason in counts[1]:
        proposals.append((reason, reason, {i for i, r in enumerate(b3) if reason in r}))
    relaxed = [set() for _ in decs]
    for rank in range(min(5, len(proposals))):
        def gain(proposal, prior):
            _, reason, affected = proposal
            return sum(bool(b3[i] - prior[i]) and not (b3[i] - prior[i] - {reason}) for i in affected)
        proposal = max(proposals, key=lambda p: (gain(p, relaxed), len(p[2]), p[0]))
        label, reason, affected = proposal
        print(f"| {rank+1}. {label} | {pct(len(affected))} | {pct(gain(proposal, [set() for _ in decs]))} | {pct(gain(proposal, relaxed))} | {EVIDENCE.get(reason, 'DecoBake.Reasons/SetupPostfix/UnknownWriter 동명 조건')} |")
        for i in affected:
            relaxed[i].add(reason)
        proposals.remove(proposal)
    print("\n상위 5개에는 파일 내 components 미정의 해소도 포함한다. 순증은 파일상 후보이며 런타임 미정 해소를 뜻하지 않는다.")
    print("\n가정: 태그는 공백 분리 OR 합집합/빈 태그 NO TAG. 반복은 고유 대상을 늘리지 않아 확장하지 않는다. 소스 행은 합치지 않는다. Tile 초기 위치는 정적(stickToFloor 제외), Camera 계열은 화면 투영으로 동적. 이벤트 Tile은 효과 시점 재배치로 제외한다.")
    print("\n원본 파일은 읽기만 했으며 이미지/게임/다른 맵을 열지 않았다. SetText는 현재 UnknownWriter 이름 조건 밖이고 AddText는 이미지 이외로 제외된다.")


def self_test():
    raw = b'{"actions":[{"eventType":"MoveDecorations","tag":"a b","scale":[200,null],},]\n"decorations":[{"eventType":"AddDecoration","relativeTo":"Global","tag":"a b","decorationImage":"x,}",},]}'
    level, repairs = load_level(raw)
    d, b2, b3, u, moves, unmatched = estimate(level)
    assert level["decorations"][0]["decorationImage"] == "x,}"
    assert len(d) == 1 and b2 == [{SCALE2}] and b3 == [set()] and moves == 1 and unmatched == 0
    assert repairs == {"후행 쉼표": 4, "decorations 앞 구분 쉼표 누락": 1}
    level["actions"].append({"eventType": "MoveDecorations", "tag": "a", "decorationImage": "new"})
    assert estimate(level)[2] == [{"이미지/원래크기/크롭/wrap"}]
    placement_level = {"decorations": [
        {"eventType": "AddDecoration", "relativeTo": "Tile", "tag": "static"},
        {"eventType": "AddDecoration", "relativeTo": "Tile", "stickToFloor": "Enabled", "tag": "follow"},
        {"eventType": "AddDecoration", "relativeTo": "Camera", "tag": "camera"},
        {"eventType": "AddDecoration", "relativeTo": "CameraAspect", "tag": "aspect"},
    ], "actions": [{"eventType": "MoveTrack"}, {"eventType": "MoveCamera", "zoom": 200}]}
    rows = estimate(placement_level)[2]
    assert rows == [set(), {"Tile-동적 바닥 추종", "추종/고정/바닥 배치"},
                    {"Camera-동적 화면 투영"}, {"Camera-동적 화면 투영"}]
    placement_level["actions"].append({"eventType": "MoveDecorations", "tag": "static", "relativeTo": "Tile"})
    assert estimate(placement_level)[2][0] == {"Tile-동적 효과 시점 재배치"}
    placement_level["actions"][-1]["relativeTo"] = "Global"
    assert estimate(placement_level)[2][0] == set()
    assert estimate(placement_level, version=3)[2][0] == {INITIAL}
    print("통과: 문자열 보존/명시적 문법 보정/OR 중복 태그 고유장식/NaN 축/나중 제외 전파")
    print("통과: Tile 정적/바닥 추종/Tile 이벤트 재배치/Camera 정적 좌표의 동적 투영/Global 전환/bake-3 비교")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path", nargs="?", type=Path, help="명시적인 단일 .adofai 경로; 디렉터리/와일드카드 검색 없음")
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        self_test()
    elif args.path is None or args.path.suffix.lower() != ".adofai" or not args.path.is_file():
        parser.error("존재하는 단일 .adofai 파일을 정확히 지정하세요")
    else:
        report(args.path)
