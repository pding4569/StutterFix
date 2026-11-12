"""Read-only source/evidence audit; writes Markdown only. No Unity or game imports."""
from pathlib import Path
import argparse
import collections
import hashlib
import itertools
import re

ROOT = Path(__file__).resolve().parents[2]
SRC = Path(r"E:\StutterFixWork\decompiled")
OUT = ROOT / "docs/render-system/data"
CALLBACK = re.compile(r"\b(?:void|IEnumerator)\s+(Update|LateUpdate|FixedUpdate|OnRenderImage|OnPreRender|OnPostRender|OnWillRenderObject|OnGUI|OnBecameVisible|OnBecameInvisible)\s*\(")
PATTERNS = {
    "loop": re.compile(r"\b(?:for|foreach|while)\s*\("),
    "allocation": re.compile(r"\bnew\s+|\.ToArray\(|\.ToList\("),
    "transform": re.compile(r"\btransform\b|\.(?:localPosition|position|localScale|localEulerAngles)\b"),
    "material": re.compile(r"\.(?:material|materials|sharedMaterial|sharedMaterials)\b|new Material\("),
    "lookup": re.compile(r"\b(?:GetComponent\w*|Find\w*|GetComponents\w*)\s*[<(]"),
    "render": re.compile(r"Graphics\.Blit|GetTemporary\(|ReleaseTemporary\(|\.Render\("),
}
STATE = re.compile(r"new Material\(|Shader\.Find\(|\.shader\s*=|SetTexture\(|\.mainTexture\s*=|(?:Enable|Disable)Keyword\(|shaderKeywords|SetPropertyBlock\(|\.sharedMaterial\s*=|\.renderQueue\s*=|sorting(?:LayerName|LayerID|Order)\s*=")

def safe(s):
    return s.replace("|", "&#124;").replace("\n", " ")

def source_audit():
    OUT.mkdir(parents=True, exist_ok=True)
    lines = ["# 게임 소스 전수 정적 인덱스", "", "자동 생성: `python tools/analysis/ingame_audit.py source`.",
             "파일 전체를 어휘 검색한 인덱스다. 숫자는 호출 지점이 있는 줄 수이며 실행 횟수/할당량/실행 시간은 아니다.",
             "실제 재생 시 활성 여부와 함수 간 도달성은 본문 분석과 런타임 자료로 따로 판정한다.", "",
             "| 파일 | 줄 | SHA256 앞16자리 | 콜백(줄) | 반복문 | 할당 표현 | Transform | 재질 | 검색 | 렌더 |", "|---|---:|---|---|---:|---:|---:|---:|---:|---:|"]
    state = ["# 재질·셰이더·텍스처·정렬 쓰기 전수 인덱스", "", "정적 호출 지점이다. 초기화/편집기 전용/실행 중 경로를 섞어 빈도로 해석하지 않는다.", ""]
    filters = ["# OnRenderImage 전수 인덱스", "", "Blit/temp는 파일 전체의 정적 호출 지점 수. 분기·루프 때문에 프레임당 횟수와 다르다.", "", "| 파일:줄 | Blit 지점 | GetTemporary 지점 | Update/LateUpdate |", "|---|---:|---:|---|"]
    total = collections.Counter()
    for assembly in ("Assembly-CSharp", "Assembly-CSharp-firstpass", "DOTween"):
        for p in sorted((SRC / assembly).rglob("*.cs")):
            raw = p.read_bytes()
            body = raw.decode("utf-8-sig", errors="replace").splitlines()
            rel = p.relative_to(SRC).as_posix()
            counts = {k: sum(bool(rx.search(s)) for s in body) for k, rx in PATTERNS.items()}
            cb = [(i, CALLBACK.search(s).group(1)) for i, s in enumerate(body, 1) if CALLBACK.search(s)]
            lines.append("| " + safe(rel) + " | " + str(len(body)) + " | " + hashlib.sha256(raw).hexdigest()[:16] + " | " + ", ".join(f"{n}:{i}" for i,n in cb) + " | " + " | ".join(str(v) for v in counts.values()) + " |")
            hits = [(i,s.strip()) for i,s in enumerate(body,1) if STATE.search(s)]
            if hits:
                state += [f"## {rel}", "", "```text"] + [f"{i}: {s}" for i,s in hits] + ["```", ""]
            for i,n in cb:
                if n == "OnRenderImage":
                    filters.append(f"| {rel}:{i} | {sum('Graphics.Blit(' in s for s in body)} | {sum('GetTemporary(' in s for s in body)} | {', '.join(n for _,n in cb if n in ('Update','LateUpdate'))} |")
            total[assembly] += 1
            total['OnRenderImage'] += sum(n == 'OnRenderImage' for _,n in cb)
            total['callbacks'] += len(cb)
    lines += ["", "## 합계", "", "```text", str(dict(total)), "```", ""]
    for name, content in (("ingame-source-index.md", lines), ("ingame-render-state-index.md", state), ("ingame-filter-index.md", filters)):
        (OUT/name).write_text("\n".join(content), encoding="utf-8")
    print(dict(total))

def changes(rows, field):
    return sum(a[field] != b[field] for a,b in zip(rows, rows[1:]))

def relaxed_min(buckets, field):
    """Exact transition minimum for independent symbolic state within ordered buckets.
    No image equivalence claimed. A group may be split to use the same state at both ends.
    """
    dp = None
    for bucket in buckets:
        occurrences = collections.Counter(r[field] for r in bucket)
        states = set(occurrences)
        k = len(states)
        if dp is None:
            dp = {s: k-1 for s in states}
            continue
        best = min(dp.values())
        enter = {s: min(dp.get(s, best+1), best+1) for s in states}
        if k == 1:
            dp = enter
        else:
            order = sorted(enter, key=enter.get)
            dp = {e: min(enter[e]+k if occurrences[e] >= 2 else float('inf'),
                         enter[order[0] if order[0] != e else order[1]]+k-1) for e in states}
    return min(dp.values()) if dp else 0

def census_rows(p):
    active = False
    rows = []
    for ln, line in enumerate(p.read_text(encoding="utf-8-sig", errors="replace").splitlines(),1):
        if line.startswith('# camera='):
            active = line.startswith('# camera=Camera#')
        if not active or not line.startswith('draw\t'):
            continue
        v = line.split('\t')
        if len(v) != 16:
            raise ValueError(f"{p}:{ln}: unexpected draw schema {len(v)}")
        rows.append(dict(line=ln, shader=v[9], material=v[5], texture=v[15].split(':unity=')[0],
                         key=tuple(v[11:15]), block=v[8], kind=v[3], keywords=v[10], renderer=v[2]))
    return rows

def census_audit():
    import os
    files = sorted((Path(os.environ['LOCALAPPDATA'])/'StutterFix').glob('gpu-census-*.tsv'))
    out = ["# 보관된 gpucensus 연속 후보 열 전환 계산", "", "재현: `python tools/analysis/ingame_audit.py census`.",
           "아래는 **GPU 제출 실측이 아닌 gpucensus 근사 열의 정확한 집계**다. 파일 순서를 그대로 사용한다.",
           "동점 키는 (queue, sorting_layer, order, 소수 셋째자리 distance). 원자료가 거리를 반올림했고 SortingGroup/패스/셰이더 ID가 없어 안전한 동점 증명이 아니다.",
           "완화 최솟값은 인접한 같은 키 구간 안에서 모든 순열을 허용한 독립 상태별 이론 최솟값이다. shader/material/texture 각각 별도 최적화라 동시에 달성된다는 뜻이 아니다.",
           "MPB·마스크·GrabPass·겹침도 무시하므로 가능한 절감의 낙관적 상한 자료일 뿐이다. 현재 순서와 그림을 지키는 경우 전환 수는 그대로다.", "",
           "| 파일 | 행/줄범위 | 연속 키 구간 | 셰이더 현재→완화최소 | 재질 현재→완화최소 | 주텍스처 현재→완화최소 | MPB 행 |", "|---|---:|---:|---:|---:|---:|---:|"]
    n = 0
    for p in files:
        rows = census_rows(p)
        if not rows:
            out.append(f"| {p.name} | draw 열 없음 | — | — | — | — | — |")
            continue
        buckets = [list(g) for _,g in itertools.groupby(rows, key=lambda r:r['key'])]
        cells = [f"{changes(rows,f)}→{relaxed_min(buckets,f)}" for f in ('shader','material','texture')]
        out.append(f"| {p.name} | {len(rows)}/{rows[0]['line']}–{rows[-1]['line']} | {len(buckets)} | " + ' | '.join(cells) + f" | {sum(r['block']=='True' for r in rows)} |")
        n += 1
    out += ["", "입력 폴더: `%LOCALAPPDATA%/StutterFix`. 첫 draw의 상태 설정은 전환에 세지 않는다. 실제 SetPass는 shader 이름 외에 키워드/패스/고정 상태가 필요하므로 계산하지 않았다.", "", "## 입력 SHA256", "", "```text"]
    out += [f'{p.name} {hashlib.sha256(p.read_bytes()).hexdigest()}' for p in files]
    out += ["```", ""]
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT/'ingame-order-counts.md').write_text('\n'.join(out),encoding='utf-8')
    print(f"census files={len(files)} with_draw_rows={n}")

def evidence_audit():
    base = Path(r"E:\StutterFixWork\night-results")
    names = [f'n1110-{m}-rlP.txt' for m in ('hl','wf','sh','s7','dd','ar')]
    names += [f'gp2-{m}-rlP.txt' for m in ('hl','wf','sh','s7')]
    names += ['gp6-dd-rlP.txt', 'rts/mt1-hl-rlP.sym60.txt', 'rts/ss-hl-gfx.sym40.txt', 'rts/rtcensus-hlbase.txt']
    out = ['# 재사용한 런타임 증거 발췌', '', '새 측정 없음. 원자료 파일:줄과 SHA256을 기록한다. 서로 다른 판/해상도/시각을 합산하지 않는다.', '']
    rx = re.compile(r'FPS 구간|phase_window end|render_counter=(Draw_Calls|SetPass)|mark-gp2-|camera:Camera#|effect:.*(Blur_Movie|SuperComputer|VideoBloom)|TransformChangeDispatch::ScanAndHandleChangesJob|RunLod:PreCullBody|ThreadedStreamBuffer::HandleOutOfBufferToReadFrom|GpuProgram::ApplyValueParameters|TexturesD3D11Base::SetTexture')
    for name in names:
        p = base/name
        if not p.exists():
            continue
        raw = p.read_bytes()
        out += [f'## {name}', '', f'SHA256 `{hashlib.sha256(raw).hexdigest()}`', '', '```text']
        matches = [(i,s) for i,s in enumerate(raw.decode('utf-8-sig', errors='replace').splitlines(),1) if rx.search(s)]
        out += [f'{p.as_posix()}:{i}: {s}' for i,s in matches[:55]]
        out += ['```', '']
    (OUT/'ingame-runtime-evidence.md').write_text('\n'.join(out), encoding='utf-8')
    print(f'evidence files={len(names)}')

def validate():
    import ast
    import importlib.util
    import sys
    sys.dont_write_bytecode = True
    for p in (Path(__file__), Path(r'E:\StutterFixWork\order1.py')):
        ast.parse(p.read_text(encoding='utf-8'),filename=str(p))
    p = ROOT/'docs/render-system/ingame-full-analysis.md'
    body = p.read_text(encoding='utf-8')
    roots = {'G':SRC/'Assembly-CSharp', 'F':SRC/'Assembly-CSharp-firstpass', 'D':SRC/'DOTween', 'M':ROOT, 'R':Path(r'E:\StutterFixWork\night-results')}
    refs = re.findall(r'([GFDMR])/([^`\s,;]+?\.(?:cs|md|txt|tsv)):(\d+)',body)
    bad = []
    for alias,name,num in refs:
        f = roots[alias]/name
        if not f.is_file() or int(num) > len(f.read_text(encoding='utf-8-sig',errors='replace').splitlines()):
            bad.append(f'{alias}/{name}:{num}')
    assert not bad, bad
    assert '\ufffd' not in body
    for link in re.findall(r'\]\(([^)]+)\)',body):
        assert (p.parent/link).is_file(), link
    trials = [('aba','bc'), ('ab','ba'), ('abc','aba','bc'), ('aa','bcb','ab')]
    for trial in trials:
        buckets = [[{'s':c} for c in b] for b in trial]
        perms = [set(itertools.permutations(b)) for b in trial]
        expected = min(sum(a!=b for a,b in zip(seq,seq[1:]))
                       for choice in itertools.product(*perms)
                       for seq in [sum(choice,())])
        assert relaxed_min(buckets,'s') == expected, trial
    spec = importlib.util.spec_from_file_location('order1',r'E:\StutterFixWork\order1.py')
    order1 = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(order1)
    plans = 0
    for map_key,config,sort in itertools.product(('hl','wf','sh','s7','dd','ar'),('rlP','gBsSp'),(False,True)):
        plan = order1.make_plan(map_key,config,sort)
        assert plan['steps'][-1] == 'quit'
        assert 'rdcapture' not in plan['steps']
        assert all(float(s.split()[1]) <= 60 for s in plan['steps'] if s.startswith('wait '))
        assert ('sortprobe all 50 20261110' in plan['steps']) == sort
        assert ('gpucensus' in plan['steps']) != sort
        plans += 1
    print(f'validated: Python AST, {len(refs)} source references, local links, Unicode, {len(trials)} exhaustive DP fixtures, {plans} dry plans; no game run')

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('mode',choices=('source','census','evidence','validate','all'))
    args = parser.parse_args()
    if args.mode in ('source','all'): source_audit()
    if args.mode in ('census','all'): census_audit()
    if args.mode in ('evidence','all'): evidence_audit()
    if args.mode == 'validate': validate()
