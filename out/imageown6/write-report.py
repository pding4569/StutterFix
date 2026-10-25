import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
out = Path(__file__).resolve().parent
groups = json.loads((out / 'groups.json').read_text(encoding='utf-8'))
status = 'IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=NA registered=93 addressed=89 missing=4 direct_required=36 direct_limit=32 arrays_used=4 array_limit=4 required_slots=40 fallback=NA spikes_30ms=NA reason=WINDOW_TEXTURE_CAPACITY_EXCEEDED'
brief = ('외부run5 직접79줄 원시158~432행 순서대조. 창[257.5,270.25]에서 변형10개 선택·93개 제외 후 '
         '등록93/주소89/누락4(직접4)·직접필요36/32·배열4/4·필요40슬롯/선언36·282.3125MiB다. '
         'live prepare에서 중단, 통합픽셀/FPS/끊김/복원은NA. 크기·포맷44그룹/93개와 누락4개를 결과에 기록하고 '
         '지정된 설계한계 중단조건을 적용한다. 런타임/생성기/기존JSON 변경·새계획/반복시험 요청 없음. '
         '형식별 은행/아틀라스 설계는Claude 담당. 동시작업의 정책48/셰이더 변경을 관측했으나 손대거나 되돌리지 않았고 '
         '32상한 외부run5와 혼합하지 않는다. IMAGEOWN=FAIL reason=WINDOW_TEXTURE_CAPACITY_EXCEEDED. '
         '게임·설치·커밋·푸시·장치·Editor·설정/제어변경·빌드 반복0회. 상세결과/10줄/out/imageown6 참조.')
table = '| 크기 | 포맷 | tier | 등록 | 주소 | 누락 |\n|---|---|---:|---:|---:|---:|\n'
for g in groups:
    table += f"| {g['size']} | {g['format']} | {g['tier']} | {g['textures']} | {g['addressed']} | {g['textures']-g['addressed']} |\n"
section = '''## 자동 반복 6회차 — 창 축소 후에도 슬롯 초과, 지정된 설계 한계에서 중단

이번 게임 실행·설치·커밋·푸시·자동 장치 호출·Unity Editor 실행·설정/제어 파일 변경은 **모두 0회**다. 코드·시험 생성기·기존 JSON을 수정하지 않았고 새 계획이나 동일 시험 재실행을 요청하지 않았다. 자동 장치를 직접 정지한 것도 아니다. 코드 변경이 없어 3빌드·LoadCheck·기존 단위 검사는 반복하지 않았다.

### 외부 run5의 실제 결과

`im-autoloop-run5-evidence.txt`의 직접 증거 **79줄**을 `E:/StutterFixWork/night-results/al-im-5.log`의 **158~432행**과 순서·중복까지 대조했다. 시작 선언만 1,500자 접두부이고 나머지는 전체 줄이 일치한다. 아래는 외부의 기존 시험이며 이번 호출의 새 게임 측정은 없다. 근거: `out/imageown6/analyze.py`, `analysis.txt`, `groups.json`.

| 원시 행 | 관측값 |
|---|---|
| 377 | 소유 후보28,523·창257.5~270.25초·선택 변형10·제외93·등록93·초기 물리 텍스처 유지 |
| 378~379 | 정확크기 DXT5 배열7680×4320×4·전체 계획4배열·직접32·활성/선언36·배열296,026,112B=282.312500MiB |
| 380~384 | 등록93·주소89·누락4(직접4/배열0)·직접필요36/32·배열4/4·누락변형4·valid=False |
| 429~432 | `IMAGEOWN=FAIL reason=COLLAPSE_TEXTURE_COVERAGE`·`gpudeco live prepare` 실패·종료 요청 |

**現在 배정 방식에서 필요 슬롯은 4배열+36직접=40개이고 선언은36개라 직접4슬롯이 부족하다.** 작은 DXT5 배열 대상53개+정확크기 배열4개+직접36개=등록93개이며, 주소가 있는 항목은53+4+32=89개다. 379행의 `arrays_allocated=0 copy_calls=0`은 할당 전 계획 로그다. 이후384행에는 배열4개가 보고되지만 추가 메모리의 실사용/여유VRAM을 별도 측정한 증거는 없다.

실제 live on·통합 픽셀·ABBA 결과는 원시에 없다. FPS/+20%·260/262/270초 통합 최대차이·폴백·30ms 이상 끊김·전곡 수명·Rec/설치 복원·종료 native_contexts는 **NA**다. `종료 요청`을 정상 종료/복원 성공으로 확대하지 않는다. 과거run2의 독립 소유 max1은 이번 통합의 결과가 아니다.

### 코드로 확인한 제한과 중단 판단

`GpuDecoImageTimeline.SelectWindowVariants`와 `RegisterGpuImageCollapseVariants`는 이미 소유 대상별 창 내 이벤트+창 직전 상태를 선택하고 초기 물리 텍스처를 유지한다. 실제 선택10/제외93은 이 경로가 실행됐음을 보인다. `PrepareDrawCollapse`는 작은 DXT5 tier별 배열과 가장 큰 동일크기 DXT5 그룹 하나를 배열에 배정하고 나머지는 `Take(DirectBindings)`로 직접 슬롯에 넣는다. `RememberGpuImageCollapseAddresses`가 요청 합집합과 주소를 대조해 누락을 잡고 CPU 기록 분리 전 준비를 거부했다. 이 결과는 누락 검사 오작동이나 정점 수식 실패의 근거가 아니다.

숨은 소유 항목도 `CaptureGpuImageActivation`/이미지 교체/Flush에서 `RefreshGpuImageGeometry → UpdateDrawCollapseTextureSlot`의 대상이다. 단순히 `VisibleInWindow` 밖이라는 이유로 등록을 더 버리면 기존 업로드 계약을 깨뜨릴 수 있다. 게임 `scrVisualDecoration.SetSprite` 근사 디컴파일(206~245행)과 설치 DLL ILScan을 대조해 sprite/마스크 대입·ResizeCollider·샘플링 재질·V15 Clamp 호출을 확인했다(`out/imageown6/game-visual-il.txt`). LazySprite는 물리 sprite 적용을 지연할 수 있다. 이 정적 대조만으로 초기 물리 텍스처가 불필요하다거나 픽셀 원인이 확정됐다고 판단하지 않는다.

지시서의 **“그래도 넘치면 숫자(포맷별·크기별 개수, 필요 슬롯 수)를 결과 문서에 적고 멈춰라”** 조건이 충족됐다. 현재4배열+32직접 배정 방식의 이 창 수용량 제한으로 중단한다. GPU 이미지 소유/통합 설계 전체가 불가능하다는 결론은 아니다. **소유자 요청:** 아래 수치로 형식별 은행/아틀라스 설계를 검토하고 정확한 포맷·픽셀·원래 순서·부분 업로드·끄기 복원을 보존하라. 상한 증가·누락검사 해제·누락 업로드 생략은 이번 지시의 해결책으로 수행하지 않는다. 설계 수정 없이 같은333단계 계획을 재시도하지 않는다.

### 포맷·크기별 필수 텍스처

포맷별 등록/주소/누락: DXT5 **65/64/1**, RGBA8 **20/17/3**, RGB8 **5/5/0**, DXT1 **1/1/0**, 원시 포맷표기`88` **2/2/0**. `88`의 실제 포맷 이름은 이 로그로 확정하지 않았다. 총 **44그룹·93/89/4**다. tier0이어도7680×4320 DXT5 4개는 정확크기 배열에 배정됐으므로 직접 슬롯에 중복 계산하지 않는다.

누락 ID: `-2775148`=2500×2500 DXT5, `-2775156`=1489×1489 RGBA8, `-2775164`=682×682 RGBA8, `-2775224`=7677×4320 RGBA8. 표의 주소/누락은 고유 텍스처 계수이며 장식 개수나 가시 픽셀 수가 아니다.

''' + table + '''
### 동시 작업과 검증 범위

진행 중 다른 작업자의 `DrawCollapsePolicy.cs`에 `DirectBindings=48`, `GpuDecoBatch.shader` 및 별도 Editor 로그 변경이 관측됐다. **이 작업이 만든 변경이 아니며 수정·복원·채택하지 않았다.** 외부run5는명시적으로32직접/4배열/선언36인 이전 실행이다. 현재 소스48을 run5의 시험 상한으로 바꾸거나 새 번들/수용량/성능 성공으로 간주하지 않는다. 다른 작업자의 변경을 되돌리면 충돌하므로 보존하며, 소유자가 이 지시의32상한 금지와 별도로 정합성을 확인해야 한다.

검증은 원시79줄 순서·필수계수·44그룹 합산·결과 부재 확인, 설치 DLL 읽기 전용 ILScan, 이미지 소스/허용helper/생성기/기존계획15개 SHA256 보존, 문서 정합성·10줄 요약·`git diff --check`다. 증거 추출 스크립트의 첫 실행은 게임 DLL 폴더명을 잘못 써 파일 존재 검사에서 멈췄고 csproj의 실제 경로로 바로잡았다. 게임/설치 쓰기는 없었다. 기존 빌드 성공은5회차 이력으로만 남기며 이번 성공으로 재표기하지 않는다.

아래는 이전 회차 이력이며 `WINDOW_COVERAGE_NOT_RUN`은 외부run5 수신 전 상태다.

'''
section = section.replace('現在', '현재')
path = root / 'docs/handoff/codex-imageown-result.md'
old = path.read_text(encoding='utf-8-sig')
assert '## 자동 반복 6회차' not in old
assert old.startswith('IMAGEOWN=FAIL')
path.write_text(status + '\n\n' + section + old.split('\n', 1)[1].lstrip('\n'), encoding='utf-8')
for name, heading in [('imageown-autoloop-ledger.md', '## 2026-10-25 — 외부 run5 분석 6회차, 창 수용량 제한으로 중단'),
                      ('im-autoloop-ledger.md', '## 회차 6 — Codex 외부 run5 분석, 지정된 설계 한계로 중단')]:
    path = root / 'docs/handoff' / name
    old = path.read_text(encoding='utf-8-sig')
    assert heading not in old
    addition = heading + '\n\n' + brief + '\n\n'
    if old.startswith('# '):
        title, rest = old.split('\n', 1)
        text = title + '\n\n' + addition + rest.lstrip('\n')
    else:
        text = addition + old
    path.write_text(text, encoding='utf-8')
path = root / 'docs/handoff/codex-master-20261022.md'
old = path.read_text(encoding='utf-8-sig')
title, rest = old.split('\n', 1)
assert '**최신 imageown 6회차' not in old
path.write_text(title + '\n\n**최신 imageown 6회차(2026-10-25):** ' + brief + '\n\n' + rest.lstrip('\n'), encoding='utf-8')
lines = [status,
         '이번 게임·설치·커밋·푸시·장치·Editor·설정/제어변경0회·새게임측정/빌드반복0회.',
         '외부run5 직접79줄 원시158~432행 순서일치·選択10/제외93·창257.5~270.25·등록93/주소89/누락4.',
         '직접필요36/32·배열4/4·필요40/선언36슬롯·배열계획282.312500MiB·준비실패.',
         '누락:2500x2500 DXT5·1489x1489/682x682/7677x4320 RGBA8;모두직접·누락변형4.',
         '44크기/포맷그룹93개=작은배열53+정확크기배열4+직접36;전체표/포맷합산은결과문서.',
         '지정된창축소후초과중단조건충족·Claude은행/아틀라스설계인계·동일333단계재시도요청없음.',
         '런타임/생성기/기존JSON수정0·새계획0·이미지관련15개SHA유지·원시/집계/ILScan/문서검증.',
         '동시작업정책48/셰이더변경은손대거나되돌리지않음·외부32상한시험과혼합금지.',
         '통합픽셀/FPS/+20%/폴백/끊김/전곡/Rec/설치복원/native_contexts NA·상세결과/out/imageown6 참조.']
lines = [s.replace('選択', '선택') for s in lines]
assert len(lines) == 10
(root / 'docs/handoff/codex-imageown-summary.txt').write_text('\n'.join(lines) + '\n', encoding='utf-8')
(out / 'checkpoints.log').write_text('2026-10-25 task-observer v3.5.0: no new generalizable observations; task-specific capacity evidence retained in imageown6 report.\n', encoding='utf-8')
print('updated: result, 10-line summary, two ledgers, master; no source/plan changes')
