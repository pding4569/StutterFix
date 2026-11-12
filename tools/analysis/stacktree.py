"""메인 스레드 표본(ProfSamp 의 *.resolved.txt, 안쪽부터 한 줄 = 표본 하나)을 경로 단위로 집계한다.
   python stacktree.py <resolved.txt> [맵이름]
  1) 단계: PlayerLoop 바로 아래 단계(스크립트 Update/LateUpdate, 렌더, 캔버스, 오디오 ...)별 비율
  2) 스크립트 진입점: 엔진이 부르는 MonoBehaviour 메서드별 비율과 그 안의 소유자(우리/게임/다른 모드/DOTween/엔진 호출)
  3) 엔진 함수: 스크립트 밖(렌더·갱신)에서 가장 많이 쓰는 엔진 함수(포함 비율)
"""
import sys, collections, re

fn = sys.argv[1]
name = sys.argv[2] if len(sys.argv) > 2 else fn
S = []
for ln in open(fn, encoding='utf-8', errors='replace'):
    t = [x for x in ln.rstrip('\n').split(';') if x]
    t = [('[Unity] ?' + x[2:]) if x.startswith('U:') else (x[2:] if x[:2] in ('M:', 'X:') else x) for x in t]
    if t: S.append(list(reversed(t)))   # 뿌리부터
n = len(S)
print(f"## {name}: 표본 {n}")

def is_unity(x): return x.startswith('[Unity]')
def clean(x):
    x = x.replace('(wrapper dynamic-method) MonoMod.Utils.DynamicMethodDefinition:', 'PATCHED:')
    x = x.replace('(wrapper runtime-invoke) object:runtime_invoke_void__this__', 'runtime-invoke')
    return x[:110]

# 1) 단계 = 두 번째 ExecutePlayerLoop 바로 다음 프레임
phase = collections.Counter(); byphase = collections.defaultdict(list)
for s in S:
    k = None
    last = -1
    for i, x in enumerate(s):
        if 'ExecutePlayerLoop' in x: last = i
    if last >= 0 and last + 1 < len(s): k = clean(s[last + 1]).replace('[Unity] ', '')
    else: k = '(루프 밖)' if not any('PlayerLoop' in x for x in s) else '(루프 직속)'
    phase[k] += 1; byphase[k].append(s[last + 1:] if last >= 0 else s)
print("\n== 1) PlayerLoop 단계별 (메인 스레드 표본 %)")
for k, v in phase.most_common(14): print(f"  {100*v/n:5.1f}%  {k}")

def owner(x):
    if x.startswith('StutterFix.'): return 'StutterFix'
    if x.startswith('PATCHED:'):
        return 'PATCHED(게임/모드 원본+패치)'
    if x.startswith('DG.Tweening'): return 'DOTween'
    if x.startswith('Quartz'): return 'Quartz'
    if x.startswith('UnityModManager'): return 'UMM'
    if x.startswith('Rewired'): return 'Rewired'
    if x.startswith('TMPro'): return 'TMP'
    if x.startswith('UnityEngine.') or x.startswith('System.'): return 'Unity/BCL 관리 코드'
    if is_unity(x): return '엔진'
    return '게임/기타'

# 2) 스크립트 진입점 (ScriptingInvocation::Invoke 바로 아래 첫 관리 프레임)
print("\n== 2) 스크립트 진입점 (엔진이 직접 부르는 메서드)")
entry = collections.Counter(); inside = collections.defaultdict(collections.Counter); under = collections.defaultdict(list)
for s in S:
    idx = None
    for i, x in enumerate(s):
        if 'runtime-invoke' in x: idx = i; break
    if idx is None: continue
    j = idx + 1
    while j < len(s) and (is_unity(s[j]) or s[j].startswith('[') or 'runtime-invoke' in s[j] or 'mono-2.0' in s[j]): j += 1
    if j >= len(s): continue
    e = clean(s[j]); entry[e] += 1; under[e].append(s[j + 1:])
    own = set(owner(x) for x in s[j + 1:] if not is_unity(x))
    for o in own: inside[e][o] += 1
tot = sum(entry.values())
print(f"  스크립트 안 표본 {100*tot/n:.1f}% (엔진 호출 비용 포함)")
for k, v in entry.most_common(22):
    own = ", ".join(f"{o} {100*c/v:.0f}%" for o, c in inside[k].most_common(4))
    print(f"  {100*v/n:5.2f}%  {k}   [{own}]")

# 3) 스크립트 밖 엔진 함수(포함 비율)
print("\n== 3) 스크립트 밖 엔진 함수 (포함 %, 제너릭한 뿌리 제외)")
skip = ('ExecutePlayerLoop', 'PlayerLoop', 'PerformMainLoop', 'MainMessageLoop', 'UnityMain', 'ScriptingInvocation', 'CallMethodIfAvailable', 'CommonUpdate')
inc = collections.Counter()
for s in S:
    if any('runtime-invoke' in x for x in s): continue
    seen = set()
    for x in s:
        if is_unity(x) and not any(k in x for k in skip) and x not in seen:
            seen.add(x); inc[x] += 1
out = sum(1 for s in S if not any('runtime-invoke' in x for x in s))
print(f"  스크립트 밖 표본 {100*out/n:.1f}%")
for k, v in inc.most_common(28): print(f"  {100*v/n:5.2f}%  {k.replace('[Unity] ','')[:100]}")
