
## codex-al-gg-6 — 2026-10-22 (최신, 실제 v2 G0 통과·정지 G1 연결)

- run5 실제 v2 세 파일을 읽었다. 첫 export182794.6272ms·bake508.5029ms, 원본은 `run-c8cc4448c4f34c249594694aeab12a85`에 보존. 준비 순서/300초 예산 유지, 전역시계201후보·1,000메시 제출 경로 반복0.
- 첫 앵커 대조는262초0.0326676px·4정점,270초22.1796161px·8정점 실패. 270초 실제시계 없는 동적 표102개를 정답과 무관한 공통 메타데이터 규칙으로 원본에 남긴다(첫 슬롯:회전2·위치50·알파50). 새 pending scalar/color는 생성시 시작값+진행0, pending scale은 오래된VStart 거부. pending 변경의 새 실제export 효과는 미측정.
- 262초 대형180도 장식은 CPU quaternion w=-4.37113883e-8/GPU native cos=0이 원인. float 반각/다항식은 대형90도에서0.0703125px 실패도 남겼다. 최종 SM5 double 범위축소/다항식→float quaternion과 sqrt 경계로 해결. RTX4060Ti/D3D11 실행 확인, 다른GPU지원/비용 일반화0.
- 공용 `GpuDecoGpu.cs` 최종 실제G0:262소유1730/2134(81.0684%), 정점최대/평균0.00408344949/0.00000118019px·초과0/6920. 270소유1773/2269(78.1401%),0.00779684167/0.00000439754px·초과0/7092. 276보조17/24·0px. RGBA초과 모두0.
- 같은 최종소유집합의 곡시계 경로 정점최대/평균42.51154843/1.92565580px,176.14624709/2.42965828px. 빠른앵커소스1641/1641·1771/1771이 평균프레임9.22750/9.34693ms 안, 정지/종료/폴백 값 기준 통과. 국소위상이며 실제트리거 시작위상 전체이력은 미측정.
- 회전8200각도 행렬계수최대/평균4.62989e-7/6.26776e-8, 반폭5·가로배율1000의5경계정점사례0px. 새번들55640B 생성·재로드·101475이징/회전통과. 임의 초대형각도·다른GPU의 보장은 아님.
- 새 AUTOTEST `GpuDecoSnapshot` 정지 G1 연결:새export/동일시각·G0·실제버퍼copy 확인 뒤 원본숨김, 비소유 정렬경계별Raw메시/멤버별서브메시/원본재질. draw off·재개·맵/세대변경·해제/파괴 복구. 새쓰기훅/프레임당전체스캔0. 실제게임 수명/복구/정렬/픽셀은 미검증.
- 기본생성기는 새93단계G1 JSON:전체필터·mask none·잡음·A/on/B/draw off/A2·3시점판정. 262/270은G0·소유70%·차이0.3%/최대3,276보조. 누락/중복/밀도대체/복구실패는PASS거부. PNG고유접두사·실패구역장식/텍스처/알파/z/블렌드후보 로그. 이전export-only 루프 그대로 반복 금지.
- 공용실행기 합성채널/정점·소스분리/정답입력독립 통과. 257/2000소스+1dispatch CPU0.0078056/0.0123070ms,80Raw/1200서브메시/81dispatch 평균0.1473152ms·묶음0.137851~0.167101ms,복사0·합성픽셀0. 게임비용/서브메시실제수/게임G1/G2/GPUtimestamp와 구분, 비용합산0.
- 3종빌드/LoadCheck/일반Player제외·데이터/픽셀판정·bake/live·DecoSim·framecmp·ld2a/geometry/delta·steps/SM5 근거와manifest는round7에 보존. 초기IPCexit1/entitlementexit198 후 자연종료/직접Editor 성공. 게임·설치·로그인·강제종료·커밋·푸시·config변경0.
- 정지 G1 준비는 각 pause의 진단비용이다. 재생중 원본평가/Transform쓰기 생략·Dormancy 전환구동·재생전1회준비·≤0.3ms 실시간경로는 미구현/미측정이며, 재개때 원본으로 돌아간다. G1이 나중에통과해도 전체실시간목표 완료가 아님.
- GPUDECO_G0_OFFLINE=PASS measured=3/3 primary=2/2 auxiliary=PASS
- GPUDECO_G1=ERROR G0=PASS game_pixels=UNMEASURED reason=paused_runtime_pixels_required

---

## codex-al-gg-5 — 2026-10-22 (이전, 소스별 실제 진행값·v2 export)

- run4는 첫 export184257.5118ms·bake511.2941ms에 3파일을 만들었다. 준비 순서·300초 예산을 유지했다. 최신 파일3개는 v1이며 owned/awake=1730/2134,1875/2268,17/24를 현재 리더로 확인했다. 이전81.50/65.62px는 이 새 입력의 재측정값이 아니다.
- 원본 디컴파일/설치DLL IL로 StartEffect의 상대 기준→Kill(true)→실제 시작값, 현재 Rec.Pos float 진행을 확인했다. 전역 시계 이동201후보·1,000메시 성능·옛 번들 시험을 반복하지 않았다.
- export v2: 실제 FastMove.Run에서 Rec 생성의 소스/프레임/시각/세대/캡처epoch를 기록한다. export에서만 현재 eventTweens의 Pos·실제 구간을 읽는다. 같은 소스의 진행값 충돌은 파일 생성 전 중단, 완료 끝값은 별도 보존, 미관찰/native/direct/미시작은 표 폴백 수를 남긴다. 새 Harmony/쓰기 훅·프레임당 장식 스캔0.
- GPU는 실제 현재 구간+progress[source]로 평가한다. 예약tb로 현재 구간을 먼저 고르면 경계/교체 시작값이 틀릴 수 있어 실제 Rec를 우선한다. 소스 배열1회 SetData, 실제 필드/정점 정답을 GPU 입력으로 사용하지 않는다. v1 읽기만 허용·v2 위장 재저장/새 실제G0 판정은 거부한다.
- G0: 전후 채널 최대/평균px와 국소위상ms, 실제 프레임/tween dt 평균256표본을 기록한다. 빠른 채널의 소스99%가1프레임 이내, 정지근처/종료/폴백은0.01px·색1/255 유지. 필드→실제 Sprite 및 GPU 필드→정점 수치 검사도 별개다. OWNED_LOW 최신70%/awake800 규칙·276보조 유지.
- Unity6000.3.10f1/RTX4060Ti/D3D11 최종 합성v2왕복2,923,234B·소스257·채널32,000·정점8,000. 정점최대/평균0.0000309467/0.0000032826px·초과0, RGBA최대/평균0.0000002384/0.000000006754·초과0. 곡시각+30초 출력동일, source37+7ms→112채널 변화/다른소스0, Actual 변경→GPU 변화0.
- 합성 곡시계 음성 대조는 정점최대524.2243px·평균116.2252px·7936/8000초과로 의도대로 실패. 이 수치를 실제 Arche 개선량으로 쓰지 않는다. 실제 v1 입력 거부도 같은 실행에서 확인했다.
- 소스 업로드+1dispatch CPU 제출500회 평균:257개/1028B=0.0075288ms,2000개/8000B=0.0132740ms. 합성·복사run0·GPUtimestamp없음, 게임 비용/성능 아님. 옛80메시 비용과 합산하지 않았다.
- 3종최종Release오류0/0/0·경고38/4/38, LoadCheck277/229/277·불일치0·일반Player새필드제외, 데이터41·bake/live·DecoSim12·framecmp·ld2a/geometry/delta·steps·SM5통과. 초기IPCexit1/entitlementexit198 보존, 클라이언트 자연종료 뒤 직접Editor 성공exit0. 게임/설치/로그인/라이선스변경/강제종료/커밋/푸시/config수정0.
- 실제v2 export0개이므로 새 G0 전후/시작위상/소유율/실제 준비비용·게임G1/G2미측정. 새번들/게임연결0. 결과·랩결과·README·10줄·새steps·round6근거/manifest 정리. 이는 새 설계 불가능의 증명이 아니며, 게임 금지 범위 안 작업을 마친 상태다.
- GPUDECO_G1=ERROR G0=UNMEASURED stage=source-clock reason=v2_real_anchor_export_required integration=BLOCKED

---

## codex-al-gg-4 — 2026-10-22 (이전, 당시 G0 설계 한계)

- 직전 run3은 기존262.gpudeco 재생성 시 CreateNew 충돌로 중단됐다. 원본 보존·새 고유run 폴더 기본값·명시적 기존 export 폴더 생성 전 거부·런타임 조기 오류를 반영했다. 고정 경로의 옛 JSON은 자동 수정되지 않는다. 같은 게임 시험 재실행을 요청하지 않는다.
- 최신 사용자 정정 적용: **awake≥800 && owned/awake<70%만 OWNED_LOW**, exporter/lab 공통 정책이며 GPU 미지원 이징 제외 후에도 검사한다. 이전 절대800개 기준과 276초 실패 판정은 폐기한다. 276초 소유17/awake24는 보조로 측정했다.
- Unity6000.3.10f1 직접/RTX4060Ti/D3D11 실제3파일 측정: 주262초 정점최대/평균81.501349/1.920398px·초과5936/6920, 주270초65.618709/2.890432px·초과6796/7500. 276초 보조68정점·272RGBA 최대/평균0·초과0, 16슬롯도 모두0. 주 소유율81.0684/82.5991%는 통과했다.
- `measured=3/3 primary=2/2 auxiliary=PASS`지만 G0_FAIL·exit1이다. 276초를 억지로 실패시킨 원인은 제거했지만 주시점의 오차는 그대로다. 최종RGBA 최대0.001122171/0.006678127·초과0/72, 실제 필드→정점 최대0.004081598/0.007796900px·초과0. 이번에도 전역시계201후보로 해결되지 않았다.
- 원본 scrVfxPlus.Update 디컴파일/현재 설치DLL IL로 실제Update 트리거 재확인. 원본float Rec.Pos 누적과 예약T0/tb 무상태표가 다르며 v1에는 실제소스 시작/진행/dt이력이 없다. 시간역산 제품보정·새GPU연결·네이티브전환·새번들0. 현재 설계 한계로 중단한다.
- 3종Release오류0/0/0(기존경고38/4/38), LoadCheck277/229/277·불일치0·일반Player제외, 랩C#오류/경고0, SM5 3커널, 데이터/소유율경계21·출력충돌/steps·bake/live·DecoSim12·framecmp·ld2a 정책/geometry/delta·생성기 통과. 관계없는 합성성능/번들 재시험은 하지 않았다.
- 초기 샌드박스 UPM IPC실패exit1, 다음 entitlement exit198 보존. 클라이언트 자연종료 뒤 새 직접실행은 실제측정/의도한exit1. 초기 이전verify복사본은 stale로 격리하고 최종근거에서 제외했다. 강제종료·로그인·설치·게임실행·커밋·푸시·config수정0회. 기존export SHA2563개일치.
- 결과/랩결과/README/10줄요약 최신화, round5-*로그와manifest 보존. 게임G1/G2·실제시작위상·GPUtimestamp 미측정. GPUDECO_G1=G0_FAIL G0=FAIL game_pixels=UNMEASURED reason=real_export_gate_failed integration=BLOCKED

---

## codex-al-gg-3 — 2026-10-22 (이전, 276초 판정은 gg-4로 정정)

- 직전 외부 시험은 실제 export 3개를 만들었다. 첫 export182998.1268ms, bake worker514.9991ms. 기존 준비 순서를 되돌리거나 시간 상한을 더 늘리지 않았다.
- 262초 소유1730/awake2134(81.0684%), 270초1875/2270(82.5991%), 276초17/24(70.8333%). **276초는 현재 awake를 전부 지원해도24<800**이라 OWNED_LOW다. 잠든 장식으로 수를 채우거나 시점/800개 기준을 바꾸지 않았다. 감소 원인이 맵 연출인지 추적 누락인지는 확정하지 않는다.
- Unity6000.3.10f1 직접 D3D11/RTX4060Ti로 실제 파일을 대조했다. 수정 후 정점 최대/평균은 **262초81.501349/1.920398px(5936/6920초과)**, **270초65.618709/2.890432px(6796/7500초과)**. 최종RGBA 최대/평균0.001122171/0.000043841 및0.006678127/0.000108364, 초과0/27680 및72/30000. 실제 논리 color.a도650/1300개 초과다.
- 실제 논리 필드로 만든 CPU 정점↔실제 SpriteRenderer는 최대0.004081598/0.007796900px, 초과0. CPU Table.Eval↔실제 필드부터 큰 차이가 나므로 GPU 이징 하나의 오류로 간주하지 않았다. 원본 Update-trigger+float Rec.Pos 누적과 표의 예약T0+tb 직접 평가 차이를 디컴파일/설치DLL IL로 교차확인했다.
- HLSL 정점 식에 precise를 추가해 GPU↔CPU표 정점 최대0.481698→0.032653px / 0.124750→0.007797px로 감소했다. 첫 시점4정점은 여전히 실패한다. 수정 뒤 합성 이징101475개·정점8000개·RGBA32000개 초과0을 확인했다. 실제 G0 통과는 아니다.
- **되풀이하지 않을 접근:** 전역 tb 오프셋만 맞추기. 읽기 전용으로−50~+50ms·201후보를 대조해도 최대오차 최소 후보가14.535930/16.227838px였다. 평균 최소도1.129953/1.408870px. 연속 최적해/실제 시작 위상 증명은 아니며 정답 역산 보정을 제품에 적용하지 않았다. 현재 v1파일은 실제 소스 시작/Rec.Pos/dt 이력을 담지 않는다.
- 새 GpuDecoAudit.cs는 원인 분해와 실패 요약을 Verify/Audit에 공용 적용한다. 이전 OWNED_LOW→정상return→exit0 결함을 고쳐 실제 실패 exit1·최종G1 한줄에 OWNED_LOW+G0=FAIL을 남긴다. 하드코딩 요청시각±1/120초를 실제 이벤트 시작위상 검사로 사용하지 않는다.
- 3종Release 오류0(기존경고4/4/4), LoadCheck277/229/277곳 불일치0·일반Player 제외, 랩C#오류/경고0·SM5 3커널·bake/live·데이터14·DecoSim12·서명10·ld2a·Dormancy41·framecmp·steps 통과. WindowCheck 첫 인자 누락 실패를 보존하고 생성JSON 전달 후58건 통과했다.
- 최초IPC exit1/entitlement exit198 후 클라이언트 부재 확인한 새 직접 실행이 성공했다. 강제 종료·로그인·설치·라이선스 변경0. 이번 실제 G0 실패 뒤 새 번들/게임 연결은 중단했다. 이전 서브메시 비용/번들/픽셀은 역사적 합성 증거로만 유지한다.
- 게임 실행·설치·커밋·푸시·config수정0회. 결과문서·랩결과·README·10줄요약·round4증거/해시 갱신. 실제 게임G1/G2·이벤트 시작위상·GPUtimestamp는 미측정. 현 설계 한계로 중단하며 같은 게임 시험 반복은 요청하지 않는다.
- GPUDECO_G1=OWNED_LOW G0=FAIL game_pixels=UNMEASURED reason=real_export_gate_failed integration=BLOCKED

---

## codex-al-gg-2 후속 — 2026-10-21 (이전)

- 직전 증거의 300001.072ms / `새 맵 스냅샷 대기`는 단순 굽기 지연이 아니었다. 편집 open은 PrepVfx를 호출하지 않는다. 원본 `scnEditor.OpenLevelCo`·`scrController.WaitForStartCo` 디컴파일과 설치 DLL IL 호출 목록으로 교차확인했다. 기존 `open → ready → seek` 순서는 첫 재생 준비를 일으키기 전에 기다렸다.
- 수정: `select 0 → play → gpudeco scheduled(실제 collector/worker/ready 관찰) → pause → wait 60 → state → wait 60 → ready → resume → stop`, 그 다음 시점 3곳 seek/export. timeout 420·첫 export 300초 예산 유지. 새 steps는 `tools/gpudeco-lab/evidence/round3-export-steps.json`. 생성기/계약 검사는 통과했고 실제 게임 export 성공은 미검증이다.
- **되풀이하지 않을 접근:** 편집 open 뒤 재생 준비 없이 ready 대기, 또는 1,000개 독립 메시/1,001 dispatch를 성능 성공으로 간주하는 경로. 80개 Raw 메시·메시당 15서브메시를 새로 구현해 검증했다.
- Unity 6000.3.10f1 직접 D3D11 실행 성공. 최초 샌드박스 IPC 실패(exit1)·기존 라이선스 클라이언트 entitlement 실패(exit198)를 보존했다. 해당 클라이언트 종료 후 새 직접 실행은 정상 동작했으며 로그인·설치·라이선스 변경은 없었다.
- 합성 2,000장식/20,000표 구간/80메시/1,200서브메시/81 dispatch. 제출 평균 **0.1522106ms**, 5묶음 평균 최소/최대 **0.147147/0.157122ms**. 8,000정점 복사 불일치 **0**. GPU timestamp 미제공.
- 대규모 128×128 분리/통합 그리기 **0픽셀/최대0**. SpriteRenderer 원본 대조 및 같은 키·중간 비소유 경계·생성 순서 6종 모두 **0픽셀/최대0**. 재질 역전 음성 대조는 각 **2,304픽셀/최대93~110**. 게임 Sprites/Tile와 실제 경계는 미검증이다.
- 첫 랩 픽셀 실패 1,991픽셀/최대117은 비용 측정에서 t가 270.0499로 진행된 GPU와 t=270 정답을 혼합한 시험 결함이었다. 시각과 복사 버퍼를 정답 시각으로 복원한 후 통과했다. 실패/중간/최종 로그를 보존했다.
- Camera.Render wall(에디터128×128, 각100회)은 분리1,200렌더러 **4.237392ms** / 80렌더러·1,200서브메시 **3.068427ms**. 컬링·제출·드라이버 대기를 포함할 수 있으며 순수 엔진 CPU/GPU/게임 FPS 수치가 아니다. 수백 재질/렌더러·게임 수명/복구는 미검증이다.
- 번들 **35,399B** 재생성·재로드·101,475이징 대조 통과. 3종 Release 오류0(기존경고38/4/38), LoadCheck 불일치0·일반 Player 제외, bake/live·데이터·DecoSim·서명·ld2a·framecmp·steps·HLSL fixture 통과. 최신 근거는 `tools/gpudeco-lab/evidence/round3-*` 및 `submeshes.txt`다.
- 게임 실행·설치·커밋·푸시·config 수정 0회. 실제 export **0개**, 실제 준비 시간/소유율/채널 오차/시작 위상/G0/G1/G2 미측정. 실제 export 검증 전 게임 소유/렌더러 연결은 계속 거부한다. 결과 문서·랩 결과·10줄 요약을 최신화했다.
- GPUDECO_G1=ERROR stage=G0-real-export reason=real_export_missing

---

## 회차 1
- Codex 요약: export·독립 랩·steps를 추가했고, 3종 빌드·LoadCheck·오프라인 fixture가 통과했습니다.

**Unity 라이선스 오류(exit 198)로 GPU 검증이 막혀 G0/G1은 미완료**입니다. 인계 기준에 따라 게임 연결 전에 중단했습니다.

게임 실행·설치·커밋·푸시·설정 변경은 하지 않았습니다.

[결과 문서](E:/StutterFixWork/wt-gpu/docs/handoff/codex-gpudeco-1-result.md) · [10줄 요약](E:/StutterFixWork/wt-gpu/docs/handoff/codex-gpudeco-1-result-o.txt)


- 시험 핵심 줄:

## 회차 2

- 첫 회차와 같은 게임 시험을 반복하지 않았다. 게임 실행·설치·커밋·푸시·설정 변경 0회.
- timeout 420 / wait 60 두 번 / 실제 state / ready, open부터 첫 export까지 300초 예산·전체 prepare_ms 기록을 반영했다. 실게임 5분 달성은 미측정이다.
- 코드상 추가 차단: framecmp pause의 DOTween.timeScale=0이 bake의 frozenScale 변경 거부를 유발한다. 동일 controller의 온전한 framecmp 소유 정지만 인정하도록 수정했다. 실게임 export 성공은 미검증이다.
- Unity 6000.3.10f1 직접 호출(-batchmode -quit -force-d3d11, 파이프로 종료 대기)은 실제 실행 성공. 로그인/설치 없이 RTX 4060 Ti에서 검증했다. -nographics는 Null Device이므로 GPU 시험용이 아니다.
- GPU 최초 이징 101,475개에서 최대 0.00165259838104 / 초과 526. Circ 경계·Elastic asin 위상 근사를 수정한 뒤 최대 1.97160989046e-6 / 평균 2.09635552387e-8 / 초과 0.
- 합성 정점 8,000개 최대 0.000477268022223px / 평균 0.0000223719397063px / 초과 0. 실제 Arche G0 결과가 아니다.
- 1,000개 독립 Raw 메시 복사 차이 0이어도 UInt16 조합은 빈 출력이었다. CPU 사본 폐기·바운드·동기 컴파일만으로 해결되지 않았고 UInt32로 맞춘 뒤 1,573픽셀을 그렸다. 단일/분할 128×128 RGBA 비교 차이 0. 이를 일반적인 Unity UInt16 결함으로 단정하지 않는다.
- 번들 최초 재로드 실패는 generic 버전 오류와 함께 AssetBundle module disabled 로그가 있었다. 독립 프로젝트의 기존 내장 모듈 참조 수정 후 **35,399B 생성·재로드·수치 대조 통과**, 게임 적재는 미검증.
- **그대로 되풀이하지 않을 경로:** 통합 평가 1회 + 1,000개 CopyRun을 미리 기록한 CommandBuffer. 합성 2,000장식 / 20,000표 구간 / 1,000메시에서 제출 **2.723905ms**, dispatch **1,001회**. 0.3ms 목표의 약 9.08배라 이 경로의 성능 성공으로 게임 연결하지 않는다. GPU timestamp/게임 FPS/매 프레임 renderer 엔진 비용은 미측정이다. 네이티브 대안은 비교만 했고 구현하지 않았다.
- 3종 빌드 오류 0·LoadCheck·bake/live·데이터·DecoSim·서명·ld2a·framecmp·steps fixture 통과. 실제 export 0파일·실제 소유율/prepare_ms/시작 위상/게임 G0/G1은 미검증.
- 결과: [본 결과](codex-gpudeco-1-result.md), [랩 수치](codex-gpudeco-lab-result.md), [10줄 요약](codex-gpudeco-1-result-o.txt). fixture 종료 코드와 수치 로그는 tools/gpudeco-lab/evidence/에 있다.
- GPUDECO_G1=ERROR stage=G0-real-export reason=real_export_missing_and_1000_run_submit_over_budget


## 회차 1
- Codex 요약: 인계대로 수정·검증했습니다. 게임 실행·설치·커밋은 하지 않았습니다.

- export 대기와 일시정지 시 bake 폐기 문제 수정
- GPU 수치·번들 재로드, 3종 빌드·LoadCheck·기존 fixture 통과
- 1,000구간 제출 **2.724ms**로 목표 **0.3ms 초과**
- 실제 export가 없어 게임 G0·G1은 미검증이며, 게임 연결은 중단했습니다.

[결과 문서](/E:/StutterFixWork/wt-gpu/docs/handoff/codex-gpudeco-1-result.md) · [10줄 요약](/E:/StutterFixWork/wt-gpu/docs/handoff/codex-gpudeco-1-result-o.txt)


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] prepare_begin budget_ms=300000 includes=open,bake,seek,export
  - [StutterFix] [gpudeco] GPUDECO_G1=ERROR stage=prepare prepare_ms=300001.072000 reason=first_export_budget_300s status=새 맵 스냅샷 대기

## 회차 2
- Codex 요약: 준비 대기 순서를 수정하고 서브메시 랩을 검증했습니다.

- CPU 제출 평균 **0.1522ms**, 합성 픽셀 차이 **0**
- 3종 빌드·LoadCheck·기존 fixture·번들 재로드 통과
- 실제 export가 없어 **게임 G0/G1은 미검증**

게임 실행·설치·커밋은 하지 않았습니다.

[결과 문서](E:/StutterFixWork/wt-gpu/docs/handoff/codex-gpudeco-1-result.md) · [10줄 요약](E:/StutterFixWork/wt-gpu/docs/handoff/codex-gpudeco-1-result-o.txt)


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] prepare_begin budget_ms=300000 includes=open,bake,seek,export
  - [StutterFix] [gpudeco] prepare_ms=182998.126800 phase=first_export includes=open,bake,seek,export bake_wall_ms=514.999100
  - [StutterFix] [gpudeco] prepare_ms=4623.995800 phase=export owned=1730 awake=2134 raw=262.01982366340235 tb=262.00703288420073 path=E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\exports\262.gpudeco
  - [StutterFix] [gpudeco] prepare_ms=4590.392900 phase=export owned=1875 awake=2270 raw=270.01799260871485 tb=270.0076849285594 path=E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\exports\270.gpudeco
  - [StutterFix] [gpudeco] prepare_ms=4386.468400 phase=export owned=17 awake=24 raw=276.00734197394922 tb=276.0021744821363 path=E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\exports\276.gpudeco

## 회차 3
- Codex 요약: 인계대로 수정·검증했고, 설계 한계를 기록한 뒤 중단했습니다.

- GPU 산술·실패 판정 수정, 3종 빌드·LoadCheck·회귀 검사 통과.
- 실제 G0 정점 최대 오차 **81.50px / 65.62px**로 실패.
- 276초 소유 **17개**로 `OWNED_LOW`. G1은 미통과입니다.

게임 실행·설치·커밋은 하지 않았습니다.

[결과 문서](/E:/StutterFixWork/wt-gpu/docs/handoff/codex-gpudeco-1-result.md) · [10줄 요약](/E:/StutterFixWork/wt-gpu/docs/handoff/codex-gpudeco-1-result-o.txt)


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] prepare_begin budget_ms=300000 includes=open,bake,seek,export

## 회차 4
- Codex 요약: 인계대로 수정·검증하고 설계 한계를 기록한 뒤 중단했습니다.

- export 파일 충돌·276초 `OWNED_LOW` 오판 수정
- 3종 빌드·LoadCheck·관련 fixture 통과
- 실제 G0 최대 오차 **81.50px / 65.62px**로 실패. G1은 미측정

게임 실행·설치·커밋은 하지 않았습니다.

[결과 문서](/E:/StutterFixWork/wt-gpu/docs/handoff/codex-gpudeco-1-result.md) · [10줄 요약](/E:/StutterFixWork/wt-gpu/docs/handoff/codex-gpudeco-1-result-o.txt)


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] prepare_begin budget_ms=300000 includes=open,bake,seek,export
  - [StutterFix] [gpudeco] prepare_ms=184257.511800 phase=first_export includes=open,bake,seek,export bake_wall_ms=511.294100
  - [StutterFix] [gpudeco] prepare_ms=4675.029100 phase=export owned=1730 awake=2134 raw=262.01784002082422 tb=262.00576381071068 path=E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\exports\run-bd81aca8d8c149
  - [StutterFix] [gpudeco] prepare_ms=4645.123000 phase=export owned=1875 awake=2268 raw=270.01600896613672 tb=270.00624654298775 path=E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\exports\run-bd81aca8d8c149
  - [StutterFix] [gpudeco] prepare_ms=4439.191700 phase=export owned=17 awake=24 raw=276.005327813793 tb=275.99734248026226 path=E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\exports\run-bd81aca8d8c149fa963a

## 회차 5
- Codex 요약: 소스별 실제 진행값을 쓰는 **export v2·GPU 랩 경로**를 구현했습니다.

- 3종 빌드·LoadCheck·관련 회귀 검사 통과
- 합성 정점 최대 오차 **0.000031px**
- 실제 v2 데이터가 없어 **게임 G0/G1은 미검증**

게임 실행·설치·커밋은 하지 않았습니다. [결과 문서](E:/StutterFixWork/wt-gpu/docs/handoff/codex-gpudeco-1-result.md)와 장부·10줄 요약을 갱신했습니다.


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] prepare_begin budget_ms=300000 includes=open,bake,seek,export source_clock_capture=True
  - [StutterFix] [gpudeco] prepare_ms=182794.627200 phase=first_export includes=open,bake,seek,export bake_wall_ms=508.502900
  - [StutterFix] [gpudeco] prepare_ms=4608.814900 phase=export owned=1730 awake=2134 raw=262.0162225891836 tb=262.00338249949118 path=E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\exports\run-c8cc4448c4f34c2
  - [StutterFix] [gpudeco] prepare_ms=4582.164200 phase=export owned=1875 awake=2269 raw=270.01600896613672 tb=270.00644121286507 path=E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\exports\run-c8cc4448c4f34c
  - [StutterFix] [gpudeco] prepare_ms=4331.409900 phase=export owned=17 awake=24 raw=276.005327813793 tb=276.00549467433461 path=E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\exports\run-c8cc4448c4f34c249594

## 회차 6
- Codex 요약: 실제 v2 **G0는 통과**했습니다. 정점 최대 오차는 **0.00408 / 0.00780px**입니다.

정지 G1 연결·93단계 시험 JSON을 구현했고, 3종 빌드와 검사가 통과했습니다. **게임 G1 실측과 재생 중 구동은 미완료**입니다.

게임 실행·설치·커밋은 하지 않았습니다.

[결과 문서](/E:/StutterFixWork/wt-gpu/docs/handoff/codex-gpudeco-1-result.md) · [G1 시험 JSON](/E:/StutterFixWork/wt-gpu/tools/gpudeco-lab/evidence/round7-g1-steps.json)


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] prepare_begin budget_ms=300000 includes=open,bake,seek,export source_clock_capture=True
  - [StutterFix] [gpudeco] prepare_ms=183007.730400 phase=first_export includes=open,bake,seek,export bake_wall_ms=590.569000
  - [StutterFix] [gpudeco] prepare_ms=4546.154600 phase=export owned=1730 awake=2134 raw=262.01784002082422 tb=262.00360795103336 path=E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\exports\run-276faa32e39743
  - [StutterFix] [gpudeco] on=True scope=paused_G1_only owned=1730 awake=2134 runs=66 submeshes=1730 dispatches=67 prepare_ms=1534.260200 prepare_scope=paused_diagnostic steady_cost=UNMEASURED G0=PASS
  - [StutterFix] [gpudeco] prepare_ms=4574.053000 phase=export owned=1773 awake=2217 raw=270.01600896613672 tb=270.00776393793404 path=E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\exports\run-276faa32e39743
  - [StutterFix] [gpudeco] on=True scope=paused_G1_only owned=1773 awake=2217 runs=109 submeshes=1773 dispatches=110 prepare_ms=202.236000 prepare_scope=paused_diagnostic steady_cost=UNMEASURED G0=PASS
  - [StutterFix] [gpudeco] prepare_ms=4369.730700 phase=export owned=17 awake=24 raw=276.005327813793 tb=275.99820220604687 path=E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\exports\run-276faa32e3974320be72
  - [StutterFix] [gpudeco] on=True scope=paused_G1_only owned=17 awake=24 runs=4 submeshes=17 dispatches=5 prepare_ms=138.324600 prepare_scope=paused_diagnostic steady_cost=UNMEASURED G0=PASS
  - [StutterFix] [gpudeco] GPUDECO_G1=PIXEL_FAIL scope=paused_G1 exports=3 reason=PIXEL_FAIL live_animation=UNMEASURED G2=UNMEASURED
