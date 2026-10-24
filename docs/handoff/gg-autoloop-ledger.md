## 최신 이어받기 — round99 이미지 대입 / round100 GPU 부분 갱신 / round101 격리 픽셀 대조

- 99: 실제 이미지 대입6052(즉시1137/지연4915)/post불일치0/null인자59/unknown0/errors0. 이미지973경계·자산검사973/불일치0. 로드된선택자104=quad103+빈null1. wrap Clamp1813/그외4180이므로 원래조건/순서를 유지한다. 원래 대입 뒤 크기/마스크 전 관측이며 전체곡/픽셀/FPS 증명0.
- 100: AUTOTEST `UpdateImageGeometry`로 기존 VB·인덱스 유지, static10/UV4 슬롯 업로드. 실제103자산×2슬롯206갱신의 fresh GPU 대조 불일치0/이웃변경0/포인터교체0. invalid12/12 거부·거부 뒤 결과변경0. GPU 업로드2개는 원자성 보장0이며 실제 호출자는 실패 시 기존 CPU 복귀에 연결해야 한다.
- 101: 임시 SpriteRenderer vs 모양/UV갱신+텍스처바인딩 MeshRenderer, 256×256 흰색/pass0/단일 복제재질. 103그림 RGBA다른픽셀0/max0, 비균일102/첫그림과다름102. 라이브 장식/소유권/정렬·충돌체 변경0. 실제 맵 전체픽셀/색/마스크/연속이벤트/native복사연결/400FPS 확인 안 됨. 별도 캡처파일은 만들지 않은 픽셀 fixture이고 성능판0이다.
- 99/100/101 각각3빌드·LoadCheck 오류0, 일반Player/Dev에서 새 이미지연구 타입·업데이트메서드 제외 확인. 각각 exit0/게임정상quit/설치9원본9/9복원. native/셰이더 변경0, 커밋/푸시/main/태그/릴리스/ZIP0. 문서 `gpudeco-image-trace-20261024.md`, out/round99-image-results-summary.json·round100-image-geometry-summary.json·round101-image-texture-summary.json.
- 현재 owned 게임/runner 없음. 다음은 실제 scalar/깊이→이미지 대입 순서와 stale CPU 필드·LazySprite flush·재질/property block·texture wrap를 보존한 소유권 연결이다. `GpuLiveSkip`가 이미지 블록 전 전체 target을 건너뛰므로 제외 이유만 제거하지 않는다. 이전 정상 약167~170FPS/400미달 유지, 성능 완료판 반복0/예약삭제 유지.

## 이전 — round95 공유 저장 미채택 / round97~98 이미지 표·실제 경계 검사

- 95 종료: 실제2560×1440. GPU23547을507정렬 버킷/507버퍼로 분리했어도 원본/후보4075픽셀차이로 gate실패. 같은 판 기존/원본295픽셀, 기존/후보직접3780픽셀·최대RGB5,7,2였다. C/C·I/I·B/B반복0. 성능96자동시작0/exit0/9원본복원. 공유저장 우회 중단·원래native경로유지/미채택. `gpudeco-render-cost-20261024.md` 최신 결과 확인.
- 97/98 새 구현 `GpuDecoImageTimeline.cs`/`GpuDecoImagePlan.cs`: AUTOTEST 전용 이미지 선택자/순서/대상 불변 표. 실제Arche 이미지 이벤트1833/선택자104/대상참조8751/장식5087/빈선택자1/Source재사용0, payload하한247540B(실제상주힙아님). 빈 선택자는 이미지 삭제라고 가정하지 않는다. 최종코드 EmptySelectorEvents로 명확화.
- 98 요청256~270초 FastMove.Run의 실제 이미지 이벤트973/헤더·전체대상순서일치973/unknown0/mismatch0/errors0. 원래 CPU setter 실행 그대로·이미지 GPU재생0·GPU소유권확대0. 미관측 원래 코드 경로/전체1833/실제그림/기하/LazySprite결과 증명이 아니다. 순수fixture20/20, 3빌드/LoadCheck279·229·279/일반Player 연구제외 확인. 두게임 exit0/정상quit/설치9상태복원9/9. 최종 명칭 정리 뒤 추가 게임 재시험0. 자료 `gpudeco-image-trace-20261024.md`/out/round97-98-image-plan-summary.json.
- 현재 owned게임/runner없음. 예약삭제 유지, 커밋/푸시/main/태그/릴리스/ZIP0. 기존 정상화면 약167~170FPS이며400FPS/완성형완료/전체장식비용0 미달·미측정 유지. 다음은 이미 불러온 CustomSprite의 정점/UV/텍스처/삼각형 메타데이터, 원래 이미지 setter+LazySprite pending 결과의 대조 후 GPU 갱신 연결이다. 이미지에 찍힌 같은 파일/스프라이트만 보고 noop이라 하지 않는다. 새 전체곡/G2 반복부터 시작하지 않는다.

## 이전 — round92/93 이미지 제외 원인, round94 색상 가설 제외, round95 정렬 버킷 후보 진행

- 사용자 최신은 테스트를 한 묶음으로 실행하고 사용량/속도를 아끼라는 지시다. API1 저장 계획/경계 census/MCP 요약으로 처리한다. 성능 완료판 재시도0, 자료는 `gpudeco-render-cost-20261024.md` 최신 절과 out/round92-93-residual-summary.json·round94-color-policy-summary.json.
- CPU잔류의 이미지 사유를 분리했다. 93 실제260.009/265.007/270.014초 잔류304/378/434개 중296/370/413개는 이미지 지정 플래그만1(원래크기/부드럽게/크롭0). 같은 이미지 재지정인지 실제 변경인지는 미확인이라 admission/계산/그림 변경0. 새 계측은 AUTOTEST 수집 메타데이터·명시적 경계만이며 일반Player 제외. 92/93 정상quit/exit0/9복원/native오류0/소유GPU23547유지.
- 94는 실제2560×1440. 원본C/기존I 동일0/3686400, 반복 C/C·I/I·비반올림R/R·반올림Q/Q도0. 공유 R은712164/max7,6,9; Q는425359/max4,6,5로 실패, 성능0/미채택. native 복사는 raw지만 앞에 CopyRun 정규화가 있으므로 반올림 누락 가설을 철회했다. 시간 잡음도 이 표본 반복0과 맞지 않는다. exit0/9복원. 이전3440 성능과 합산0.
- 현재 `round95-sort-bucket-storage` owned run 진행: shared storage를 원본 layer/order/queue/distance 버킷마다 별도 Combine, 원래 renderer keys/material/MPB/채널/시계 유지. 픽셀 gate가 실패하면 추가 우회 중단. 3빌드/LoadCheck279·229·279/일반Player 격리 확인, native/셰이더 수정0. 완료 전 통과/FPS/채택0. 실제 manifest/진행 중 runner부터 확인하고 시험 중 빌드/DLL교체0.
- 정상화면260~270초 기존 약167~170FPS, 400미달. GPU 버텍스/키 일치만으로 픽셀 동일성을 대신하지 않는다. 예약삭제/커밋·푸시·main·태그·릴리스·ZIP0. 95 종료 뒤 설치9상태 복원부터 확인한다.

## 이전 — round78~91 묶음 검증/MCP 요약 완료, 400FPS 미달

- 실제3440×1440/144Hz/D3D11/동기0. 같은DLL78~82 정상170.162→복사생략182.388→그리기생략224.203→둘생략247.038→뒤정상167.879FPS. 생략판은 그림을 고정/숨기므로 실제 최적화 성과가 아니다.83~85 경계 CPU추가 동일DLL: 정상169.211/main5.556583ms/render3.485577ms; root비활성+복사생략246.755/main3.555465/render1.333299; CopySubresourceRegion149.372로 느려 미채택. main/render 병렬이므로합산0.8판 정상quit/9복원각9/9. 상세gpudeco-render-cost-20261024.md.
- 86은 맵1회 open/같은프로세스 정상→rootcopy 두구간260~270초. GPU23547유지/오류0/9복원. 정상167.475/진단246.404FPS. 정상단계경과 Render2.26/Update1.62/Late1.24ms, rootcopy0.86/1.27/1.05ms(대기 포함,CPU시간 아님).87 부모계측: managerLate0.48/모드애니메이션0.51/효과발동0.22ms. CPU잔류436이미지장식/보임377/히트박스0/추종·타일붙음0/시차꺼짐436,속성1+2+5+9:170·1+2+5:168·5:58·1+2:31 등. 옛CPU 루프 미세조정 대신 완성형 트레이스/실제남은렌더비용에 집중한다.
- 88 shared vertex storage는23547개별renderer/2GPUbuffer·복귀/일반Player격리/빌드 통과했으나 기존화면대비489263/4953600픽셀·maxRGB5/9/6으로 실패, 성능단계 중단.89 기존backend on/off/on 독립대조는0/4953600·CPU왕복0·9복원.90은 새 GpuDecoStorage.cs로 같은Gpu/Data/채널/시계/기존renderer의material/MPB0/정렬bounds를 보존한 storage전환만 시험. 그래도490772/4953600·maxRGB5/7/6으로 엄격비교실패, 성능단계 미실행. 원인은확인안됨(정렬·정밀도·렌더상태 중 하나로 단정0). 두 실패판도 exit0/9복원/기존native경로복원 확인. 원시PNG I90/S90은D:Mods/framecmp에 남아 있다. 원본배포/정식설정에넣지않는다.
- global nativebatch staticbounds의 일반 live prepare는 다시 차단했다. 새 storage shared/independent/state는 AUTOTEST 명시적paused 연구명령뿐이며 기본0. oldNativeCopy/버퍼/mesh/renderer를보유하고 off/Stop/Dispose에원상복구한다. 자원probe는숨겨보유한원래mesh/renderer도기록한다. 의미변경3빌드/LoadCheck279·229·279/일반Player에서GpuStorageCommand제외 확인. native/shader수정0. 새backend의전곡/PixelPASS/400FPS/일반채택0.
- 91은Python MCP요약만수정: status.micro_windows 최근8개·곡시각/QPC초/프레임/진짜FPS·ablation조건·opt-in main/render경계CPU를반환. 기존real_fps는곡요약임을구분. 비유한/불완전/되감기/다른G2계측섞임/사적경로출력거부15/15. 설정된C:helper는summarize_log+math import 외 나머지동일성을비교한뒤추가분만반영, server/config변경0. 완료86raw재분석만으로167.475/246.404 반환확인·새게임0. 각시험마다전체로그를읽지않고기존sf_run API1의저장계획/요약을사용한다.
- 현재owned게임/runner없음. 설치9상태원본복원, 예약삭제/커밋·푸시·main·태그·릴리스·ZIP0. 400FPS미달이며 품질손실로 목표를 맞추지 않는다. 다음은남은CPU장식436의미지원조건/구운이벤트지원과동일셰이더·실제정렬을보존하는렌더제출방법이다. static batching 실패를단순half-up누락이나시간가설로확정/품질기준완화하지않는다. 완료한G2/ABBA/완주를같은조건으로반복0.

## 이전 — round78~82 복사/그리기 기여도 시험 진행

- 사용자 목표는 Arche260~270초 진짜400FPS까지 계속 개발. 예약은 삭제 상태 유지, 커밋/푸시/배포0. roadmap의 미측정 기본루프1.75ms/복사2ms/400FPS 예산은 가설이다. 현재 wholeSong은 한 slice이므로 다중 slice가 주요 병목이라고 하지 않는다. animated 시작/끝 alpha0만으로 숨기기·대상 정렬/중복제거·복귀 anchor 단순화는 채택하지 않는다.
- round74/75 완료: 같은DLL 기존GPU/전체native패킷 218~222초313.246/316.779FPS,260~270초167.760/168.822FPS(+0.63%).270초 양쪽GPU유지/native오류0/CPU10854/10854복귀/순서미관측0/자원0/정상quit/9복원. 단1쌍이고400미달, 유리한 수치를 찾으려 재시험하지 않는다.
- round76 compiledcheck 원래Take2929회/163511채널·anchor·binding 비교 불일치0, guard9거부 및 pure fixture0. round77 compiled 실제캐시반환2929회·불일치0·CPU2884/2884복귀/자원0/정상quit/9복원.77의218~222초305.403FPS는 별도표본이며 성능개선으로 채택하지 않는다. FieldRef279/229/279 확인·normalPlayer격리통과. cached행의 참조/리스트version/태그·사전 교체를 검사하고 변화하면 기존Take로 폴백한다.
- 새 GpuDecoAblation.cs는 AUTOTEST만: replacement renderer forceRenderingOff 저장/복원, native masked queue에 mask0을 보내 목적지복사만 생략. normalization/Evaluate/공유시계·이벤트는 유지한다. per-target timer/패치/새스레드/native변경0. off/Stop/Dispose에서복원. copy는그림을고정하고 draw는장식을숨기므로 시각통과판이 아니다. FPS차이를 독립적인 가산 비용으로 해석하지 않는다.
- 현재 owned driver tools/optimization/out/run-round78-82-render-ablate.py: 같은DLL 새프로세스 off/copy/draw/both/off 각각260~270초. 상태별 실제3440/주사율 확인·셰이더/효과/FG 동일, native오류·복귀·9복원 확인. 완료manifest 우선, 시험중 빌드/DLL변경0. 새3빌드오류0/LoadCheck279·229·279/일반Player에서 ablation·packet 코드제외 확인. 다음은 결과로 가장 큰 부분을 선택하고 시각/복귀 검증을 붙인다.

## 이전 — 2026-10-24 round74/75 비계측 전체패킷 비교 진행

- round73 완료: pure animated128/거부256·static128/거부256·1024연산/덮어쓰기6/거부12 불일치0. 실제Arche214~222초 static1085묶음/24910대상 + animated1846묶음/3874대상, 누적163511채널/anchor/바인딩/미러 대조0·animated precommit거부1회.222초GPU유지/native오류0/CPU2884/2884복귀/원래순서미관측0/자원0/정상quit/9복원.73 skipped_recs=5는새native경로의통계증가누락이며raw보존;74에서기존keys×대상누적으로고쳤다(실제재생변경0).
- 현재 owned driver tools/optimization/out/run-round74-75-packet-pair.py: 동일새DLL/native/번들·새프로세스 GPU기존(packets off) vs전체패킷(packets replay) 한쌍.218~222와260~270초를별도micro로기록.계획명 round74-packet-off-heavy / round75-packet-replay-heavy.전체완료/정상quit/9복원 전빌드/DLL교체0.종료manifest와raw를먼저확인한다.완료전성능결론0.
- mode5 replay는oracle/미러검사/캡처0. mode4 all은검증용.준비된이벤트/대상순서/세대검증,실제공유float클럭/CPU복귀순서/부분CPU대상순서유지.생성순번과source membership detach/AddRange는아직관리측에있다.장식전체CPU0/완성형완료라고하지않는다.
- 74 소스3빌드0/LoadCheck277·229·277오류0/일반Player격리통과. native는73과같음.기본mode0·AUTOTEST명령만.예약삭제유지,커밋/푸시/main/태그/릴리스/ZIP0.다음은새후보픽셀/복귀이후연속성·자연0초전곡·배속/중간재개.옛G2/완주를새후보승인으로인용0.

## 이전 — round73 길이있는 패킷 검증 진행

- round70 완료: 순수 연산1024/덮어쓰기6/거부12·static commit128마스크/256거부 불일치0. 실제Arche214~222초 native static1084묶음/24909대상, oracle/채널/48Banchor/슬라이스미러163511채널 불일치0. 실제precommit거부1회 뒤 기존경로로 진행.222초 activeTrue/native오류0·명시off CPU2774/2774복귀/미관측순서0·자원0·정상quit/9원본복원. oracle 비용 포함이라 성능판 아님.
- round71/72 같은DLL 새프로세스 짧은 성능한쌍:218~222초 GPU기존312.525→길이0 native314.713FPS(약+0.70%). native1085묶음/24910대상·대조계산/미러oracle끔, 양쪽activeTrue/native오류0/정상quit/9복원. 작은한쌍/전체무거운260~270초아님·개선확정0·동일시험반복0. summary=tools/optimization/out/round68-72-packet-summary.json.
- 현재 round73-packet-all-hot: 길이있는 이벤트도 native가 master/anchor/소스바인딩/슬라이스/dirty를 일괄 적용. 기존 소스클럭의 대상membership detach와 AddRange, 원래타깃/키순서의 rec/engine 생성순번은 C#에서 보존. 새로운시간추정/정렬/그림/충돌체변경0. 순수 animated128조건/256거부 fixture 후 실제구간·animated precommit거부·CPU복귀를 검증 중이다. 완료 전 통과0. 현재 runner/manifest부터 읽고 게임 중 빌드/DLL교체0.
- 새코드=GpuDecoPackets.cs / GpuDecoPacketFixture.cs / native/sfnative/deco_packet{,_anim}.h.73 native→3빌드0/LoadCheck277·229·277오류0/Player격리 통과. mode0기본, AUTOTEST명령만, 일반관리Player 제외.73은oracle켜진 mode4라 성능판 아님. 길이있는 경로의 비계측 성능·전체곡·배속·중간재개·픽셀·400FPS는 아직 미완료다. 예약은 삭제상태 유지, 커밋/푸시/main/태그/릴리스/ZIP0.

## 이전 — round68~70 이벤트 패킷 재생

- 사용자 최신 우선순위: 완성형 트레이스로 대체될 옛 경로의 미세 최적화는 넘긴다. 이벤트/대상 사전 컴파일 → 실제 소스 클럭을 쓰는 네이티브 일괄 전이 → CPU 복귀 정보 유지에 집중한다. 예약 stutterfix는 사용자 요청으로 삭제 완료; 다시 만들지 않는다. 커밋/푸시/main/태그/릴리스/ZIP0.
- round68 새 native 순수 연산: 실제 Mono에서 이벤트1024/같은 프레임 덮어쓰기6/거부12 불일치0. 4000대상×4채널 호출 평균0.190765ms는 네이티브 검증/연산/임시표/commit과 P/Invoke만이며 게임·GPU 비용이 아니다. 최초 fixture에 float 중간 변수를 추가해 기존 Mono 표현식과 반올림이 달랐던 실패4판을 보존했다. 최종은 기존 표현식 그대로와 double 중간 연산을 비교한다. 이 표본으로 모든 런타임 동일성을 주장하지 않는다.
- round69 Arche 짧은 실제 이벤트 연결: 사전 컴파일74636이벤트/소유참조187289/최대연속대상14269/중복Source모호성0.214초부터GPU,218~222초를 포함한2932묶음/28785대상/163511채널 대조 불일치0·거부0·계획미포함913. 기존 렌더링/시계/CPU복귀 경로가 실제 결과를 만들고 native는 별도 표에 계산했다. 게임성능 통과/완성형 채택이 아니다.222.009초 active=True,명시live off222.019초 CPU2834/2834 복귀·원래순서미관측0·종료자원0/정상quit/9원본복원. manifest early_recoveries1은 portal 전에 명시live_off한 줄도 세므로 자연전곡 조기복귀 수로 인용하지 않는다.
- 현재 round70-packet-static-hot 진행: native 길이0 패킷이 master채널/48바이트anchor/한 native-live slice/dirty를 일괄 commit한다. 소스 목록 detach는 기존 클럭 소유자에 남기며 실패 전에는 역순/원래 목록 위치로 복원한다. C# 논리 oracle와 출력미러/anchor 대조가 있어 성능판이 아니다. 실제 commit 전 거부1회 주입도 포함한다. 새 native fixture128마스크/256거부를 실행하고 기존 짧은 효과 구간을 검증 중; manifest 완료 전 통과0. DLL/native 변경·빌드 금지, runner 종료/9원본복원을 먼저 확인한다.
- 최신 소스: GpuDecoPackets.cs / GpuDecoPacketFixture.cs / native/sfnative/deco_packet.h 및 FastMove/Export의 연구 연결. 기본mode0·명시AUTOTEST만·관리Player 타입제외 확인. native→3빌드 exit0/LoadCheck277·229·277오류0/Player --no-bake 통과. source clocks/정렬/renderer/충돌체/일반설정 변경0. GpuPackets는 일반 재생 표 전체의 대체 완료가 아니며 길이있는 native commit/전곡/시각 검증/400FPS는 미완료다.

## 이전: round66/67 effect-cache short-window research (2026-10-24)

- Source changes: GpuDecoRecovery.cs / GpuDecoExport.cs. Research cache default FALSE; ascending used-channel list and pure easing endpoints computed once per effect. Normal Player excluded. Dev/Player/PlayerAuto builds exit0; LoadCheck277/229/277 errors0; Player --no-bake passed.
- New AUTOTEST-only micro begin/end uses two boundary QPC reads, no per-frame timers. Short owned trials seek first, enable GPU while already playing at214s, measure218..222s; this is a new bounded activation path, not full-song equivalence. Existing callback guards unchanged.
- Before next build/trial, read tools/optimization/out/round66-67-handoff.json and actual manifests/driver run-round66-67-short.py. Ended states are not broad quality approval. Review results and restore counts before continuing; no full-song repeat without candidate evidence. Current driver PID session is owned; no C# build/DLL changes while active.

## 최신 이어받기 — 2026-10-24 round64/65 현재DLL CPU/GPU 전곡 한쌍 완료

- 64/65 새프로세스·동일DLL/native/번들SHA·실제3440/144/동기0·FG끔. 원래CPU경로(굽기/GPU준비0) vs재생전준비GPU경로. 전곡441→431FPS(약−2.3%),260~270초106→166(약+56.6%)·400미달. CPU308.003497초明示시계→waitend종료, GPUportal308.397624초/GPU23547/native133316오류0/조기복귀0. 두판명령오류0·정상exit0·9/9복원. 한쌍/반올림FPS이며ABBA/품질/物理표시성공아님. 상세evidence/round64-65-summary.json·round64-65-note.md.
- 곡중CPU114.6초159ms(효과0/게임바깥분류·원인未확인),GPU220.4초35ms각1회. 같은시각/같은원인이아니므로159→35개선으로쓰지않는다. GPUportal뒤309.2초78ms로그도보존. 현재 owned게임/driver없음, 다음은길이0효과GPU표갱신/비지원CPU잔류원인계수와배속·중간재개·남은시각근거. 게임C#/바이너리변경0. 아래진행중문장은당시기록이다.

## 이전 이어받기 — round64 CPU 전곡 완료 / round65 GPU 전곡 중

- 기존MCP 저장계획으로 현재DLL의 FG끔 새프로세스 CPU끔/GPU켬 전곡 한쌍을 진행한다. round64 CPU는GPU준비0/소유0/native제출0,308.003497초 명시시계→waitend종료/정상quit0/9원본복원. 실제3440/144·진짜441FPS·260~270초106FPS·114.6초159ms1회(분류게임바깥,원인확정0). round65 GPU는현재진행중,완료전비교/통과0. owneddriver tools/optimization/out/run-round64-65-pair.py·각manifest부터확인한다. 게임중빌드/설치수정0.
- 두판완료뒤SHA/실제화면조건일치부터확인,한쌍은ABBA/모든환경증명이아님. CPU판은GPU준비·자원0인원래경로이며GPU판은재생전준비를포함한다. CPU판의159ms를GPU220.4초35ms와같은원인개선으로쓰지않는다. 이전62 raw를다시확인해220.389초33.319ms/효과25.439ms/장식이동24.0ms(길이있음0.2ms),기존요약의35ms를33ms로정정했다.56의35/36ms는그대로다.
- API1에CPU시계 song_min_s 검사·waitend타임아웃실패집계를추가·단위11/11. C:helper동기화·C#DLL/native/shader변경0. 자세한63완료결과는아래보존한다.

## 이전 이어받기 — round63 MCP HELLO 전곡·복원 완료

- 기존 sfmeasure의 sf_run에 trace API1(catalog/start/status/note)을 추가. 단위10/10·실제stdio4/4. 실행은 즉시 반환, 조회는 짧은JSON, 계획+DLL/native/번들SHA 중복방지·owned정상종료·원본존재여부9상태복원·중단잠금보존. 기존MCP허용목록/config변경0. C:설정서버에추가분만설치·기존RE_ERR보존·원본구현E:wt-gpu. 오래된연결은sf_status의API1마커먼저확인. 서로다른DLLreload/관리자PM전용runner는별도.
- round63-hello-full은 최신62DLL로 실제MCP stdio tools/call 자연HELLO185.033060초까지완료. 실제3440/144·GPU소유21/native47328오류0·조기복귀0·명령오류0·프로세스정상종료0·9원본복원9/9. 진짜255FPS·최악40ms·끊김1은보존하며품질/물리표시/장식전체0증명아님. 원시39395바이트→반환589바이트(정확한사용토큰절감량未測定). 같은계획중복시DUPLICATE·새게임0. 상세tools/sfmeasure/README.md와evidence/round63-mcp-summary.json.
- round62 자연0→221.008399초/native108032/108032·오류0·GPU23547·정상quit/8복원.219→221초삭제3684/탐색상한3684/이동59693int(238772바이트)/최대목록40. 실제CPU시간은未測定, List.Remove가26ms를지배한다는근거없음·인덱스초안미채택.200.4초168ms는사용자StarDesk개입주석·raw보존,220.4초35ms와구분. 진단판을성능통과로쓰지않는다.
- 61 실제다른DLLreload/새맵/GPU재활성화/자원해제/자기패치0/9복원통과.56FG2/4자연전곡통과와무거운260~270초158/130FPS는별도.400FPS/장식전체CPU0미달·CPU0.5ms참고·옛실패보존. 커밋/푸시/main/태그/릴리스/ZIP0.

## 이전 이어받기 — round61 reload 수정 통과 / round62 비용 진단 중

- 일반GPU/DecoBake끔59b에서도 최초StackOverflow 재현. TileCull이UMM의StutterFix1사본을자기모듈로제외하지않아자기enabled래퍼까지재작성하는경로를찾았다. 접두어제외+최종patch경계검사만수정.61 실제A→B 새MVID/새맵open/GPU재활성화통과·자기transpiler0/외부189(양쪽)·GPU23547/native5685/5685와5564/5564오류0·이전자원47098ID생존0/token1/1해제각2회·정상quit/설치9원본복원. 과거실패삭제0.
- 최신62는Source MasterSlots.Remove의탐색상한/이동한int수를명시costprobe에서만기록한다. 진단끔원래Remove그대로/활성시IndexOf1회+같은RemoveAt/개별시계0. Renderer/정렬/시계/shader/native변경0.3빌드/LoadCheck277/229/277/관리Player연구제외통과. 현재owned round62-cost-hot의210/219/221초누적보고로220.4효과비용을먼저확인. 진단판을성능통과로사용0. List.Remove원인확정/연결인덱스채택0.
- 다음63 최신HELLO전곡 JSON준비만완료·게임실행0. 무거운구간400FPS미달:옛262~276초123→157(+27.4%),56 FG2/4의260~270초158/130FPS. 전곡평균384/370을그구간결과로사용0. 사용자기상후현재상태/미완료를설명했고계속진행중이다.

- FG2/4 자연전곡308.397684/308.398708초·GPU23547 유지·native118611/118611,114409/114409/오류0·조기복귀0·8/8복원.220.4초36/35ms각1회 남음.10→45초 성공제출배율1.999807/3.761431(물리표시 아님).
- round57b 실제Shutdown CPU9954/9954·384메타0·12프레임1342비교0/진행898·표시자원47098ID생존0/token1/1해제. 같은맵on은Setup재수집이 없어 실패; 첫57audit취소실패와 함께 보존.8/8+probe복원True.
- round58 실제A→B MVID변경·47098ID생존0/token1/1해제 후 새맵open에서0xC0000005 종료.58b 덤프관측은 앞선StackOverflow→AV, 최종네이티브스택은ntdll/SteamOverlay/Mono→미해석JIT다. Steam원인확정/우회0.두판9/9복원. 셰이더/native변경0.
- 다음은 GPU/DecoBake 미사용 일반reload 대조59b.59는 한글문자열 파이프손실로 도구사전검사실패(새맵재열기0), 정상quit/9복원, 제외.59b는 ASCII조건·최초StackOverflow필터·동일A/B. 현재 owned runner/manifest부터 확인한다.
- 자세한 최신 절은 codex-gpudeco-followup-20261023.md의round56~59. 시험중빌드/설치변경0(명시적reload용B교체만 예외), 커밋/푸시/main/태그/릴리스/ZIP0. 완료시험반복0·CPU0.5ms참고·전체장식비용0미측정. 같은원인30분진전없으면재시도중단후독립항목진행.


- 현재 owned runner run-round56-natural-fg.py:55 DLL/native/번들고정·매판새프로세스·FrameGen2/4초기설정·0초자연재생。2배45초GPU23547/native20514/20514오류0、완료/4배전곡결과먼저확인。시험중빌드/DLL변경0。
- 52 handoff같은재개프레임9954/9954CPU복원·384메타데이터/12프레임722논리비교불일치0/진행488/복원8。supported직접시드호출만GPU유지skip2、이후실제효과9004/9004CPU복귀라지속유지증명0。
- 53/54는첫구간가드로실패중단·복원8。54entries71/exits70·同thread1·현재Prefix의Take거절경로라runningTrue를재진입/동시성/메모리훼손으로해석0。55정확한거절reason0/catchupTrue/decoActiveTrue：Hitch재개가EffectBudget.Suspend→60frame원본되감기보호를다시건다。보호강제해제0、D3D11충돌단정0。중간pause/seek뒤GPU유지는미완료·이전실패보존。
- 55기능core52와같음/진단만추가·3빌드/LoadCheck277/229/277오류0/일반연구제외·gate16通過、명시originaltrace기본false/AUTOTEST·한경계/첫2진입스택만·상시타이머0。48/49/50chain종료후未실행。SourceSlots초안はoutだけ/main포함/빌드/시험0。
- 이후FG실제유지/자원·오류복귀/Unload·새DLL·배속/같은core끔전곡대조/220.4효과비용근거。CPU0.5참고·과거rawG2보존·미세반복0·정식채택0。기본끔/커밋·푸시·main·태그·릴리스·ZIP0、정상quit/설치8원본바이트복원。詳細followup최신절。

## 이전 이어받기 — 2026-10-24 round52 같은 프레임 재개 회귀검사 중

- round46 현43core Arche308.398216초 portal까지GPU23547유지/native137572/137572오류0/peak2249/8192/관리평균0.164275ms·최대4.0798ms。445FPS·最悪34ms·220.4초효과26.0ms/CPU33.5ms·끊김1、전체끔대조未완료。SF/자원0·안정성2/2·복원8。
- round47 FG2/4는첫resume258.017840/258.007159초FastMoveOriginal→CPU복귀、native각2회/이후activeFalse/skipped0。동시사용채택0/2、L276PNG4는CPU대조라GPUhandoff증명0。복귀로그continuedTrue지만resumed0/expected0으로연속성증명아님。복원8、예상밖recovery로chain실패·48/49/50대기chain종료。
- 단일수정52: 기존FrameCompare.PlaybackContinuity의정상resume한같은프레임을FastMove GPU판정과CPU복귀canSync에도적용。Hitch캐시상태/기존Player동작변경0。원본우회경계상태로그추가·매프레임타이머0/native·shader변경0。3빌드/LoadCheck277/229/277오류0·일반연구제외·gate16通過。현재owned round52-resume-frame의supported기존효과시드/GPU유지와handoff12프레임audit결과부터확인、끝나기전빌드0。
- 다른단일후보인MasterSlots 선형Remove→순서보존인덱스연결초안은ignored out/source-slots-draft뿐、main포함/빌드/게임시험0。220.4초인과확정0·52검증끝나기전섞지않는다。
- 이후52해결근거뒤FG동시새판/필요한경계픽셀·자원/배속·Unload·새DLL/한번의최종ABBA·전체끔대조。이전G2/0.5ms참고/일반채택0/커밋·푸시·main·태그·릴리스·ZIP0。정상quit/설치8바이트복원、실제화면조건확인。현재근거없는완료판반복0。

## 이전 이어받기 — 2026-10-24 round46/47 전곡·FG 동시 검증 중

- 최신 상세는 followup 문서의 round42~44절이다. round42 실제270초 원본/GPU0/4953600픽셀·16논리채널최대오차0, GPU23545/CPU native fallback2. global-time/table추정으로 존재하지 않는 tween을 만든 활성화 fallback을 제거했다. 새 활성화 undo/CPU 잔류, native/shader변경0·일반Player연구제외. critical16파일 SHA동결.
- round43 활성화실패 첫Rec/전체capture/첫dispatch3조건과 off/reset/세대변경3조건: 각384메타데이터/12프레임 논리값722~1342비교·불일치0·렌더러미복원0. native entries0은증명범위아님. round44 실제270초native2 identity/Elapsed비트/재생상태불일치0,11152/11152CPU복원·1384비교0/진행1006/다음이벤트교체50. native미래궤적/전곡모든픽셀은미확인. 두판정상quit/복원8/8·명령실패0.
- round45a88초 GPU반복240픽셀/max1로엄격assert실패·다음3판중단·복원8、PNG는옛도구finally삭제로未保存。원인분리45e同DLL 필터켬/끔6반복대조0、원본/GPU170/max2·82/max1、새PNG10SHA/복원8。옛실패원인未해결·재현0을해결로하지않는다。이어45b/c/d262/270/276초原本GPU10/max2·0·0、각noise/mask/all/반복0·PNG각5SHA·SF자원0·복원8。같은runtime core16critical파일round43-candidate凍結。
- 현재 owned runner round46-song-full부터 확인한다。후속 chain은45완료/복원/厳격대조성공을확인뒤46自然전곡、47FG2/4동시(258→276)순서。47은FG끔뒤실제GPU재생후handoff PNG4도얻되독립CPUoracle아님。대기chain48/49/50은47정상완료/복원표식뒤moduleShutdown재활성/현core한번의cleanABBA/전곡preparedOFF대조로준비했다。각출력manifest/원시실패우선확인、실패때재시도0。시험중빌드/DLL변경0。
- 이후 현core자연전곡/FG2·4동시/Unload·다른DLL/배속호환성 및 의미있는최종성능검증. 과거G2보존·0.5ms참고·미세반복0·일반채택0. 커밋/푸시/main/태그/릴리스/ZIP0. 기존dirty보존·정상quit·설치8바이트복원. 새증거없는완료판반복0.

## 이전 이어받기 — 2026-10-24 round42 진행 중

- `codex-gpudeco-followup-20261023.md` 최신절부터 읽는다. round37은265.047초effect예외→CPU11770/11770복귀/순서미관측0이라GPU전곡통과0. 원인후보동시시계2048상한을8192로늘린round38 실제Arche308.404초portal까지GPU유지/peak2246/native137525/137525오류0·끝곡/편집복귀2/2·설치8/8복원。平均445FPS/最悪34.736ms@220.388초/30ms초과1、끔대조0이라개선률/새끊김0주장0。
- 같은DLL round40 HELLO185.035초portal/native47278/47278오류0/안정성2/2/복원8/8、255FPS·最悪26ms。실제소유21개/peak5라Arche23547규모·이득대용아님。
- round39 동일DLL 88/262/270초 원본-GPU221/max1,79/max2,315562/max8픽셀。270은6.37036%로G1실패、3시점통과0。마스크/원본/각반복/noise모두0·PNG각5SHA保存·全판복원8/8。round38-candidate14파일SHA凍結。
- round41新DLL270초값검사: 회전58개>1e-5/最大562150.40625도、크기1e-5、나머지13채널0。master계산도같이틀렸고최대회전원래순서미관측。그림256047/max8로여전히실패。원인: 실제Rec없는채널을table곡시각으로추정하는활성화fallback이남음。
- round42는그추정을제거한다。실제CPU Rec진행값만GPU로、나머지현재CPU값유지。활성native/미지원Rec장식은독립renderer에서그대로CPU/native에남기고GPU대응renderer끔(혼합불가모드拒否)。GPU소유카운트에서除外。3빌드/LoadCheck/연구제외通過뒤270초같은원인검사중、결과전成功아님。新활성화undo·세대변경/명시off의CPU연속복귀는stage주입未검증。native/shader변경0/일반Player포함0。
- 현재owned manifest/log부터확인。과거G2보존/0.5ms참고/미세G2반복0。main/커밋/푸시/태그/릴리스/ZIP0。기존dirty파일보존、정상quit/설치8바이트복원。

## 이전 이어받기 — 2026-10-23 round36

- 연구 오류복귀 구현: 효과 계약/소스확보 preflight, 현재대상16채널/연결/순서/원점 undo, 렌더예외 CPU기록복원, 복귀뒤 명시prepare전 재활성화 차단. 기존FastMove catch는false반환이라원본전체재실행추정은정정한다. native/shader변경0·일반Player포함0.
- 실제Arche3440/144/D3D11/동기0,262초 오류주입4조건: CPU9960/9960복원×4,각384관측기록/12프레임846~1218논리비교불일치0. 이전순서미관측6제외·효과는대상1개직접시드라전대상루프/픽셀/장치오류증명아님. resume-preflight실패1조건은연속성DEGRADED/원본미복원0·contexts0·slices0·root없음으로만기록.
- 경계CPU Rec11227→1273/소유9954→0/소스時계1726/소유23547. 전체Transform/GPU비용0미측정. 정상quit/설치8/8복원·안정성자원/SF0·명령실패0,3빌드/LoadCheck/연구제외/互換302누락0。gate16tests·진단을cleanG2로인정0。ignored round36-candidate11SHA一致。
- round37은이새DLL의자연스러운0초전곡시험으로진행중. 완료전성공으로쓰지않는다. 이전좁은G2통과/이후rawCPU실패보존,0.5ms참고/微調整G2반복0。상세followup-20261023.md최신절만읽는다。

## 이전 이어받기 — 2026-10-23 round30~35

- 최신 사용자 지시: 이전 한정범위 G2 통과 보존, 향후0.5ms는참고값으로하고 소유장식CPU트윈/Transform 제거·그림/정렬·FPS/끊김·전곡/예외복귀를우선한다. 과거raw판정은변경0·미세한기준통과를위한추가G2반복0·CPU장식비용0은미측정이다.
- round33 clean ABBA123.011850→155.229466FPS(+26.190660%)·CPU0.509357ms·30ms초과0/0·안정성4/4, 원래기준CPU실패다. 단일후보 round34/35는 live kernel에서 읽지않는 slice fallback anchor 쓰기만 생략하고 master/구형경로는유지했다.
- round34 262초 mask/all·각반복·잡음0/4953600, 원본/GPU6픽셀/max2. 같은DLL round35 clean ABBA123.031607→156.765290FPS(+27.418713%)·CPU0.506031ms·30ms초과끔/켬1/0·안정성4/4/SF·native오류0다. 기존raw CPU실패를보존하고1묶음비교의미세차이를확정이득이라고하지않는다. 두판정상quit·설치8/8복원, PNG5장보존·일반채택0이다.
- 새연구후보의정식3시점/전곡/HELLO/FG동시/실제GPU·표시지연/예외복귀는미확인. 특히 LateUpdateException의syncState=false복귀와FastMove부분적용예외의원본재실행은오류주입전안전완료로하지않는다. 현재진행중게임0, 완료한판을새가설없이반복하지않는다. 검증기15테스트통과, legacy gate는과거고정기준보존용이다.

- 최신은 `codex-gpudeco-followup-20261023.md`. 히트음 첨부 코드 검토·적용, 패딩을 빠뜨린 업로드 예산 수정. 오프라인1178136샘플 최대오차1.1920928955078125e-7/가드비무음0. 실제615구간 PCM차이0/생성버퍼72해시차이0이나 청감/물리오디오출력미확인. round30 종료전 게임화면stop실패는통과제외·원시보존; round31 정지/끔뒤세션/풀0.
- 연구 Rec의 미관측 order 표식을 다음 GPU 왕복에 보존한다. 실제 기존6개를 관측했다고 바꾸지 않는다. 최종CPU12키144조건5328비교·GPU6키18조건666비교 불일치0/임시트윈0, 주입한미관측표식6조건보존. 시드된정지검사이며 원래native정렬/전곡/배속의완전동일성은미확인.
- 같은DLL262초 mask/all·각반복·잡음0/4953600, 원본/GPU2픽셀/max2. PNG5장보존, native54/54/오류0·안정성자원잔량0·설치8/8복원. 빌드3오류0·LoadCheck277/229/277불일치0·Player연구제외·게임참조302누락0. 후보소스/바이너리12파일SHA를 ignored round32-candidate에보존했다.
- round33~35는 위 새 결과까지 완료했다. 예전 후보 G2를 새 DLL에 붙이지 않는다. 일반채택/main/태그/릴리스/ZIP0.

## 이전 이어받기 — 2026-10-23 round23~29c

- 상세는 `codex-gpudeco-resume-result.md`, 이전 복사 비교는 `codex-gpudeco-song-result.md`·`codex-gpudeco-gemini-review-result.md`부터 읽는다. 커밋/푸시/main/태그/릴리스/ZIP0·일반 채택0이다.
- 전곡23547 합집합 준비 타이머는 성공 시 멈춘다. 기존 native8 완주308.4초/native25603/25603·오류0이나 평균83FPS다. 새 masked 전곡 통과가 아니다.
- 진단 없는 round26 ABBA+26.743351%/CPU0.505004ms, round28 하드웨어 복사+11.208226%/CPU0.531239ms로 둘 다 G2 실패다. 기준을 완화하거나 이전 round18b 진단 포함 결과로 대신하지 않는다.
- round27 명시적 비용 진단: advance0.138488/dirty+alpha0.038390/source업로드0.006146ms, 활성chunk평균8.624197·최대12/46, enabled전환12.739829회/프레임. advance를 순수 이징 비용으로 단정하지 않는다.
- round29c는 원래 start/target/change/duration/float Pos/ease·관측 생성순서로 진행 중 CPU Rec를 복원한다. Arche3440/144 즉시 왕복과 실제 GPU 뒤 각각384기록·12프레임·1218논리값 비교 불일치0이다. 원래 순서 미관측6개는 audit 제외, 키6종·전곡·새G2·HELLO·FG동시 미확인이다.
- paused 복원 경계11.0538/33.5471ms, mask/all·반복0/4953600픽셀이나 원본/GPU36픽셀·최대2다. 완전0/전환비용0/전체 동일성으로 확대하지 않는다. 설치8원본복원8/8·정상종료·SF오류0, 빌드3오류0·LoadCheck277/229/277불일치0·관리Player 연구제외다.
- Harness² 공식/CLI fork를 검토했으나 원본 설치/외부모델 호출/중첩 에이전트0이다. 자체 로컬 evidence gate를 집계에 연결해13검사 통과·round26/28 실패 유지. 모델 학습이나 Harness² 자체 도입으로 표현하지 않는다.

## codex-al-gg-2 후속 — 2026-10-22 (당시 최신, round9 보간 산술)

- run1이run6보다최신이다. 첫export183571.3017ms·bake514.6238ms,262초3768픽셀/최대9·A/A2=0으로픽셀최대조건실패.270초G0최대0.498907268px·초과4에서중단,276export없음.
- 독립랩에서장식index391위치Y의실제/CPU92842.6171875·GPU92842.625를재현.나눗셈보정만으로큰끝값보간은실패한근거를보존.최종Linear double잔차보정+보간mad로같은입력이일치.정답보정·소유/임계값변경0.
- 선형65537표본:기존근사나눗셈19567불일치·수정0.새56364B번들생성/재로드·이징101475초과0·회전8200각도·색36조건/6격자통과.실제두파일draw색26880/29968성분바이트불일치0.
- 새번들자체로최신262소유1680/2081·270소유1873/2270,정점최대/평균0.00401119562/0.00000596904px·0.00779542606/0.00000728350px·초과모두0.RGBA최대1.86264515e-9/3.72529030e-9·초과0.
- 빠른소스1568/1568·1771/1771이평균프레임9.216351/11.049486ms안.시작전체이력미측정.최신2파일은전체게이트미판정,이전run6완전한3파일회귀는G0_OFFLINE PASS·정점최대0.00817265734/0.00779688125/0px·초과0.
- 3종Release오류0/0/0(이번증분경고4/4/4)·LoadCheck277/229/277불일치0·일반Player제외.랩C#오류/경고0·SM5 5커널·데이터51/bake/live/DecoSim12/framecmp/ld2a/steps통과.
- 초기UPM IPC1/entitlement198·framecmp경로오기exit2보존.초기wrapper의옛verify복사본은STALE EXCLUDED로정정.클라이언트자연종료뒤직접Editor성공·로그인/강제종료0.
- 결과/랩결과/README/10줄/round9증거·manifest·새93단계JSON정리.게임실행·설치·커밋·푸시·config변경0.실시간소유/Transform쓰기생략/재생전1회준비·G2/GPUtimestamp미완료.
- 수정후게임G1미측정.이전262초최대9/255의원인이모두해결됐다고단정하지않는다.전역시계201후보·1000메시성능반복0.
- GPUDECO_G1=ERROR G0_LATEST_PARTIAL=PASS snapshots=2/3 game_pixels=UNMEASURED reason=post_fix_runtime_pixels_required prior_run1=G0_FAIL

---

## codex-al-gg-7 — 2026-10-22 (이전, 정지 그리기 색의 네이티브 UNorm8 변환)

- run6은 G0 PASS이나262초418684픽셀/최대7·270초136779/최대5·276보조0/0, A/A2모두0으로 PIXEL_FAIL. 원본PNG 히스토그램·ApplyColor 디컴파일/실제DLL IL 교차확인을 round8에 보존했다.
- 기존 SpriteRenderer 합성 대조는 정점색white라 비흰색 경로를 못 잡았다. 새 랩에서 Float32색18조건 중14조건 차이를 재현했다. 단순 Color32/HLSL round는 중간값에서 짝수 반올림으로 실패(로그 보존). 네이티브는 float 곱 후 half-up floor(c*255+.5)다.
- GPU Evaluate/논리색/G0는 그대로 두고 CopyRun 그리기색만 포화·half-up UNorm8로 변환.36B Float32레이아웃·원본재질/셰이더/정렬/소유/판정기준 유지. readback만 CPU기준과 비교하고 정점CPU업로드/정답보정0.
- Unity6000.3.10f1/RTX4060Ti/D3D11:6색×겹침1/32/128×CPU/GPU36조건 및256중간값/±1e-7 RGB·알파6격자 모두0픽셀/최대0. Sprites/Default 검증이며 게임Sprites/Tile 최종픽셀의 대체증거가 아니다.
- run6 실제v2 GPU그리기색27680/28368/272성분 바이트불일치0, 정규화float최대차5.96046448e-8. 새55748B번들 생성/재로드·이징101475초과0·회전·색검사통과.
- 최신실제G0:소유1730/2134(81.0684%)·1773/2217(79.9729%)·17/24보조, 정점최대/평균0.00817265734/0.00000472407939px·0.00779688125/0.00000439756416px·0/0, 초과모두0. RGBA최대7.45058060e-9/7.45058060e-9/0·초과0.
- 같은소유집합 곡시계정점최대46.2278860/63.6225510px·평균1.90658232/2.25441362px. 빠른앵커소스1628/1628·1719/1719가 평균프레임9.252877/9.399858ms안. 국소위상이며 시작전체이력 미측정. 전역시계탐색/1000메시반복0.
- 3종Release오류0/0/0·기존경고38/4/38, LoadCheck277/229/277불일치0·일반Player제외, 랩C#0/SM5·데이터51·bake/live·DecoSim12·framecmp·ld2a정책/geometry/delta·생성기통과. 초기IPC1/entitlement198·중간반올림실패·fixture경로오기보존.
- 새round8-g1-steps.json93단계·고유export폴더·새번들 준비만 했다. 첫export300초/timeout420/wait60/준비순서·판정기준유지. 게임실행·설치·로그인·강제종료·커밋·푸시·config변경0.
- 수정후게임G1/복원/실시간비용 미측정. 재생중소유·원본평가/Transform쓰기생략·재생전1회준비 미구현·G2/GPUtimestamp미측정. 전체실시간목표완료가 아니며 색이 이전게임차이 전부의 원인이라고 단정하지 않는다.
- GPUDECO_G0_OFFLINE=PASS measured=3/3 primary=2/2 auxiliary=PASS
- GPUDECO_G1=ERROR G0=PASS game_pixels=UNMEASURED reason=post_fix_runtime_pixels_required prior_run6=PIXEL_FAIL

---


## codex-al-gg-6 — 2026-10-22 (이전, 실제 v2 G0 통과·정지 G1 연결)

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

## 회차 1
- Codex 요약: 그리기 색 변환 차이를 찾아 GPU 복사 경로를 수정했습니다.

- 랩 색 검사 42조건: 픽셀 차이 0
- 실제 export 3개: G0 통과
- 3종 빌드·LoadCheck·관련 fixture 통과
- 수정 후 게임 G1은 미측정입니다.

게임 실행·설치·커밋은 하지 않았습니다.

[결과 문서](/E:/StutterFixWork/wt-gpu/docs/handoff/codex-gpudeco-1-result.md) · [10줄 요약](/E:/StutterFixWork/wt-gpu/docs/handoff/codex-gpudeco-1-result-o.txt)


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] prepare_begin budget_ms=300000 includes=open,bake,seek,export source_clock_capture=True
  - [StutterFix] [gpudeco] prepare_ms=183571.301700 phase=first_export includes=open,bake,seek,export bake_wall_ms=514.623800
  - [StutterFix] [gpudeco] prepare_ms=4529.894500 phase=export owned=1680 awake=2081 raw=262.01784002082422 tb=262.0138397693666 path=E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\exports\run-d9cfd5fbfec64e4
  - [StutterFix] [gpudeco] on=True scope=paused_G1_only owned=1680 awake=2081 runs=66 submeshes=1680 dispatches=67 prepare_ms=300.313200 draw_color=UNorm8_equivalent logical_color=Float32 prepare_scope=pa
  - [StutterFix] [gpudeco] prepare_ms=4574.180400 phase=export owned=1873 awake=2270 raw=270.01600896613672 tb=269.99983559093585 path=E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\exports\run-d9cfd5fbfec64e
  - [StutterFix] [gpudeco] GPUDECO_G1=G0_FAIL stage=paused-render reason=InvalidOperationException:gpudeco G1: G0_FAIL before renderer ownership
  - [StutterFix] [자동 시험] 단계 실패 'gpudeco on': gpudeco G1: G0_FAIL before renderer ownership

## 회차 2
- Codex 요약: 270초 보간 오류를 수정해 정점 최대 오차를 **0.4989→0.00780px**, 허용 초과를 **4→0개**로 줄였습니다.

- 최신 두 시점 G0·새 번들 재로드·3종 빌드·LoadCheck·회귀 검사 통과.
- 게임 실행·설치·커밋 없음.
- 수정 후 게임 G1은 미측정이므로 전체 통과는 미선언입니다.

[결과 문서](/E:/StutterFixWork/wt-gpu/docs/handoff/codex-gpudeco-1-result.md) · [10줄 요약](/E:/StutterFixWork/wt-gpu/docs/handoff/codex-gpudeco-1-result-o.txt)


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] prepare_begin budget_ms=300000 includes=open,bake,seek,export source_clock_capture=True
  - [StutterFix] [gpudeco] prepare_ms=183902.212900 phase=first_export includes=open,bake,seek,export bake_wall_ms=507.663900
  - [StutterFix] [gpudeco] prepare_ms=4691.539500 phase=export owned=1730 awake=2134 raw=262.01784002082422 tb=262.00886023814132 path=E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\exports\run-b87ceb35d55243
  - [StutterFix] [gpudeco] on=True scope=paused_G1_only owned=1730 awake=2134 runs=66 submeshes=1730 dispatches=67 prepare_ms=1432.001600 draw_color=UNorm8_equivalent logical_color=Float32 prepare_scope=p
  - [StutterFix] [gpudeco] prepare_ms=4672.821100 phase=export owned=1823 awake=2269 raw=270.01600896613672 tb=270.00543767655296 path=E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\exports\run-b87ceb35d55243
  - [StutterFix] [gpudeco] on=True scope=paused_G1_only owned=1823 awake=2269 runs=109 submeshes=1823 dispatches=110 prepare_ms=218.229600 draw_color=UNorm8_equivalent logical_color=Float32 prepare_scope=
  - [StutterFix] [gpudeco] prepare_ms=4443.114800 phase=export owned=17 awake=24 raw=276.005327813793 tb=276.00573587255229 path=E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\exports\run-b87ceb35d55243dc8981
  - [StutterFix] [gpudeco] on=True scope=paused_G1_only owned=17 awake=24 runs=4 submeshes=17 dispatches=5 prepare_ms=136.137200 draw_color=UNorm8_equivalent logical_color=Float32 prepare_scope=paused_dia
  - [StutterFix] [gpudeco] GPUDECO_G1=PIXEL_FAIL scope=paused_G1 exports=3 reason=PIXEL_FAIL live_animation=UNMEASURED G2=UNMEASURED


## 수동 이어받기 round11 — 2026-10-23

- 게임 G1:262초3120/max3,270초1233/max4,276초0/max0; PIXEL_FAIL. 제한 후보 제거 뒤 최종G1 재시험0.
- 같은262초 묶음/World정답2505/max4, 개별렌더러0. 장면재질ID순6/12·CRC순6/12·역순8/12라 정렬키 변경0.
- EvaluateLive_sourceE 번들 재로드, 실제export3장 G0 PASS. 잘못된Hitch.Playing 종료추정 대신 기존종료콜백 사용.
- G2 ABBA PASS: off125.766739 /on163.234243FPS, 증가29.791266%, CPU평균0.453591ms·구간최악0.887400ms.
- 측정구간30ms초과0, 안정성4/4·SF오류0·누락0·잔존버퍼/메시0/0. 실제2560×1440·120Hz 범위.
- 깨끗한3빌드오류0/0/0·LoadCheck277/229/277불일치0·DataCheck52PASS. 기존전체DiagnosticCheck목록 불일치와 연구타입격리3/3 검사를 구분.
- 소유 게임5판 정상종료·설치파일8항목 정확복원각8/8. 커밋/푸시/config/main/릴리스/ZIP변경0.
- 근거: `tools/gpudeco-lab/evidence/round11-summary.json`, 최신G1/G2 결과 문서. G1 미달로 두 시너지 단계 착수0.

## 수동 이어받기 round12/13 — 2026-10-23

- round12 동적 개별 렌더러:2560/120 G1 3시점 차이0·PASS. 동일 경로 G2 off123.773303/on84.502338FPS·−31.728138%·CPU1.523111ms·새30ms초과2로 실패.
- 정적 묶음은 실제 G1 max7/5로 실패. 작은 fixture12/12 통과를 실제 곡 통과로 확대0. 재질ID/CRC 정렬 추측 채택0.
- round13 미리 기록한 native 복사:실제3440/144 G1 3시점 차이0·PASS, G2 off128.191393/on106.886567FPS·−16.619545%·관리+native상한 CPU0.510085ms로 FPS_FAIL.
- round13 측정구간30ms초과0/0·안정성4/4·SF오류0·최종native등록0, 두판 모두 설치8파일 정확복원8/8. 일반 채택0·시너지 착수0.
- 독립8UAV 원본 복사 후보:1/128/1715버퍼×2패턴 차이0·상태오류0, 1715버퍼GPU0.883168~0.920832→0.344656~0.356400ms. 새로운 round14 후보로 분리하며 실제 게임 검증은 별도다.
- 근거 `tools/gpudeco-lab/evidence/round12-summary.json`, `round13-summary.json`, `round13-eight-uav.txt`. 커밋/푸시/main/태그/릴리스/ZIP0.

## 수동 이어받기 round14/15 — 2026-10-23

- Native8 첫 판은복사전G0 0.012286px/초과8에서중단·원본복원8/8. InCubic 공통몫 보정후 같은저장표본0.004095px/초과0, 랩Residual은게임과같은엄격정점판정을추가했다.
- 실제3440/144 새3시점 Native8 G1 픽셀차이0·최대0·G0 PASS·A/A2차이0·stableTrue. native/managed/번들SHA를결합해보존했다.
- 같은경로G2 off127.114144/on105.419011FPS·−17.067443%·CPU0.509647ms, 30ms초과0/0·안정성4/4·오류0. 일반채택0.
- 정적bounds 단독진단1판:키불일치0/1715인데복원뒤3249픽셀/max9. 전체G1/G2를통과했다고하지않으며가설배제다.
- 모든판정상종료·설치8파일정확복원8/8. 마지막3빌드오류0·LoadCheck277/229/277불일치0·일반Player연구타입0. 최종로그표기수정은게임을재실행하지않고빌드/검사했다.
- 30분FPS정체로중단. `codex-gpudeco-blocked-20261023.txt`3줄과`evidence/round14-summary.json`·`round15-summary.json`을다음이어받기에서먼저읽는다. 다음은CPU스레드별원인조사이며추가시너지/배포0이다.

## 2026-10-23 round16~18b — 사용자 재개, 같은 경로 G1/G2 통과

- 공식 Unity 배칭/ETW 사례와 Microsoft CPU 조회 API를 확인했다. 경계 진단은 메인7.649→7.701ms, 렌더3.503→5.944ms였고 콜백 약0.019ms만으로 증가 전체를 설명하지 못했다. 엔진 draw약1734→4248,262초 상수0알파2850/4580을 확인했다.
- 연구 기본false `zeroalpha`: 두 요인이 유한한 상수이고 하나가 정확히0인 대체 렌더러만 쉰다. 알파 변경 시 더티 목록, 렌더 직전 반영. 정렬·재질·기하·원래 렌더러/충돌체 변경0. 정책900조건 통과.
- 사전18a는 G1 PASS·라이브1/3 차이0 뒤 긴 정지 캡처의 연속 재생이 OnLandOnPortal을 받아 중단됐다.270초 대기 실패를 숨기지 않고 보존했다. 버퍼 finalizer 경고가 남아 준비 자원을 Application.quitting에서도 정리했다. 다음18b는 표본마다 seek/정지를 분리했다.
- 18b 동일 DLL/native/번들 G1:262/270/276초 픽셀차이0/4953600·max0·A/A2차이0·stableTrue·G0 PASS. 소유1715/2134·1765/2217·17/24. 추가 라이브 끔/켬/끔3점도 차이0, 렌더러4580→1730·4875→1925·931→18.
- 같은 경로 캡처/추가스레드 진단0이나 **fuse begin/end8개·NewRec 진단 포함**인 G2 ABBA:127.560613/172.427090/169.293337/126.820632FPS. 평균127.190623→170.860213(+34.333970%), CPU평균0.477584ms(관리+native경계상한). 새30ms초과0·안정성4/4·SF/native오류0·잔존버퍼/메시0/0. 2026-10-23 원시 로그 재확인으로 측정 범위를 정정했으며 진단 없는 일반 플레이 이득은 별도다.
- 실제3440x1440/144Hz/D3D11/동기0이며 다른GPU/전체곡/물리표시지연 미확인. 두 판 정상종료·설치8항목복원8/8·finalizer경고0. 준비 상태 종료의 native context0도 확인했다.
- 빌드3종오류0·LoadCheck277/229/277불일치0·DataCheck52 PASS·일반Player연구타입0. 일반채택·커밋·푸시·main·태그·릴리스·ZIP0. 결과를 먼저 기록했고 다음은 시너지1/2의 기존 코드 감사다.

## 2026-10-23 round19~22 — GPU 기준점 보존, CPU 시너지

- round18b 통과 DLL/native/번들/주요 소스13개를 비공개 accepted-round18b에 SHA 확인 후 보존했다. 이전 G2를 새 DLL에 재사용하지 않는다.
- 시너지1은 이미 LazySprite에 구현돼 있었다. 현재 Arche238→266초 이미지 지연+6644·적용+2091·대기3531을 확인했다. 인계의219.6초/타일3377 시간은 현재 맵에 그대로 적용하지 않았다. 중복 사전/FieldRef 추가0.
- 시너지2 연구 기본false: 투명한 일반 이미지의 회전/크기 논리 기록과 기존 큐, 유한한0이하 색/불투명도 논리 기록. 히트박스/마스크/타일붙음/활성사전검사/GPU소유/알 수 없는 패치는 기존 함수로 폴백한다. 색 전용 대상도 같은 큐에 넣어 off/종료/Unload 때 native 색을 반영한다.
- 사전 round20의 기존 MoveApply 패치 거부와 round20b fixture 대상0은 통과로 세지 않고 보존했다. 명시적 진단에서만 사전검사를 비운 마지막 round22는 실제32개·회전/크기224쌍·색160쌍 불일치0(그중 native색 큐적용128·재가시화32). 같은 DLL266초 원본 PNG3장 모두 SHA동일·전체4953600픽셀 차이0이다.
- round20c CPU ABBA 원시222.443071→224.897798FPS(+1.103531%)는 끔변동4.783705%보다 작다. on2의164.360ms는 사용자가 StarDesk 접속을 보고했다. 원시값은 보존하고 새 코드의 끊김 실패 및 깨끗한 성능 채택 근거에서 제외했다. 일반 채택0.
- 새 전곡 끔/켬은 실제3440/144/D3D11/동기0·포탈완주·재생시작연출뒤30ms초과0/0·최악19/20ms·SF집계예외0·설치복원8/8이다. 포탈처리24.927/37.627ms는 별도다. 켬 전곡은 회전/크기882586·색91230회 처리했다. 서로DLL/워밍업조건이 달라 원시438/432FPS로 인과적 성능을 주장하지 않는다.
- 마지막 빌드3종오류0·LoadCheck277/229/277불일치0·DataCheck52·Player 연구타입/HiddenFixture0. 게임정상종료·현재원본8바이트상태일치. 커밋/푸시/main/태그/릴리스/ZIP0. 새 CPU 후보와 GPU 동시 사용 G1/G2·다른GPU·전곡픽셀품질·물리표시지연은 미확인이다. 상세는 codex-gpudeco-phase-c-result.md와 evidence/phase-c-summary.json.
