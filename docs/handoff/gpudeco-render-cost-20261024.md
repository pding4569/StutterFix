# Arche 400FPS 병목 분리 — 2026-10-24

## 범위

실제 3440×1440/144Hz/D3D11/동기0, 곡260~270초. 맵을0초부터 완주한 표본이 아니라248초 부근으로 seek한 뒤256초부터 GPU23547개를 소유한 구간이다. FX/해상도 자동 변경0·FG끔·기존원본/정렬/충돌체코드변경0. source DLL/native SHA와 각 상태의 ScreenState는 raw와 summary에 보존한다. 아래 GPU는 장식 트레이스이며 프레임 생성이 아니다.

## round78~85 완료

|판|조건|진짜 FPS|메인 CPU ms/프레임|렌더 CPU ms/프레임|
|---|---|---:|---:|---:|
|78|정상 on8masked|170.162|확인 안 됨|확인 안 됨|
|79|목적지 복사만 생략|182.388|확인 안 됨|확인 안 됨|
|80|대체 렌더러 그리기만 생략|224.203|확인 안 됨|확인 안 됨|
|81|목적지 복사+그리기 생략|247.038|확인 안 됨|확인 안 됨|
|82|뒤 정상 on8masked 대조|167.879|확인 안 됨|확인 안 됨|
|83|정상 on8masked + 경계 계측|169.211|5.556583|3.485577|
|84|목적지 복사 생략+대체 root 비활성|246.755|3.555465|1.333299|
|85|정상 CopySubresourceRegion masked|149.372|5.355047|3.458249|

78~82는 동일DLL이고83~85는 경계 계측/root 명령만 추가한 동일DLL이다. native/shader는8판 모두 같은 바이트다. main/render는 GetThreadTimes 누적 CPU의 경계 상한이며 병렬로 겹치므로 합산하지 않는다. GPU 시간/OS 대기 시간과 다르다. 83의 엔진카운터는 DrawCalls2269.285799/SetPass2142.447337/Batches2266.649112/Vertices21163.426036 평균(1690표본)이다.

79는 옛 그림을 쓰고80/81/84는 그림을 숨긴 진단이다. 실제 최적화 성과·그림 동일성·400FPS 달성으로 세지 않는다. 이 FPS 차이는 파이프라인 상호작용/그림 변화까지 포함하므로 독립적인 복사/그리기 비용의 정확한 가산 분해가 아니다. 각조건1표본이고 새ABBA통과가 아니다. 원래 그림을 유지한85는 더 느려 채택하지 않는다. 원래 on8masked 유지.

8판 명령/native오류0·원래순서미관측0·복귀개수일치·끝자원잔량0·정상quit·설치9상태 복원9/9다. 각 manifest의 early_recoveries1은 명시적 live_off이며 예기치 않은 복귀로 집계하지 않는다. 전체시험 worst_frame에는 준비·seek·전환이 포함되어 곡260~270초 최악 프레임으로 인용하지 않는다. 추가시각/전곡/실제표시FPS는 확인 안 됨.

## 계측 구현과 다음 판

- GpuDecoAblation은 AUTOTEST만. 원본 SpriteRenderer가 아닌 대체 renderer의 forceRenderingOff/root.activeSelf만 저장·복원한다. off/Stop/Dispose/Unload 경로에서 복구한다. 일반Player제외·기본진단0.
- 복사 생략은 기존 mask queue에0을 넣어 소비/정규화/Evaluate/소스시계·이벤트를 유지한다. 네이티브 콜백을 우회하거나 다른 스레드에서 장치를 쓰지 않는다.
- micro opt-in threadcpu는 시작·끝에서만 핸들·CPU 누적을 읽는다. 추가 매프레임 타이머/패치/워크0. 핸들/ProfilerRecorder는 end/Stop/Dispose에서 정리한다. normalPlayer제외.
- 새 phasewindow는 기존 PlayerLoop 마커만 명시적으로 설치해 구간 차이를 읽는다. 장식별 타이머를 추가하지 않는다. 시간은 대기를 포함한 엔진 단계 경과이며 CPU 실행시간이 아니다. 기존 Main.Unload의 PhaseWatch.Uninstall로 복원한다.
- 사용자 요청대로 round86은 맵1회 open/같은 게임에서 정상·rootcopy 두 구간을 연속 측정한다. 같은DLL 재로드0. 각구간GPU유지/실제원본복귀/패킷검사·최종9복원을 먼저 확인한다. 재생성/준비 실패면 그 구간을 GPU성능판으로 쓰지 않는다. 이 결과로 남은 메인 작업을 선택한다.

자료: tools/optimization/out/round78-82-ablation-summary.json, round83~85 각 manifest/Player.log. 400FPS 통과0. 커밋/푸시/main/태그/릴리스/ZIP/예약0.

## round86~91 후속

86 같은게임 연속 두 구간 정상167.475/rootcopy246.404FPS. GPU23547유지/9복원. 정상 단계경과 Render2.26/Update1.62/Late1.24ms, rootcopy0.86/1.27/1.05ms.87은남은436CPU이미지장식/히트박스0·managerLate0.48ms/애니메이션0.51ms/효과발동0.22ms를확인했다. 모두대조원시자료보존, 신중히범위를구분한다.

공유static저장88은489263픽셀/maxRGB5/9/6,같은Data/채널/시계를유지한90은490772픽셀/maxRGB5/7/6으로실패했다. oldbackend재활성화89는픽셀0·CPU왕복0이다.88/90성능시험은assertzero에서중단했고모두정상quit/9복원이다. 공유저장은미채택,원인미확인,원래on8masked유지. source값이나공개정렬key의일치만으로화면일치를추론하지않는다.

91 MCP요약은새게임없이raw를재분석한다. micro_windows와병렬thread_cpu_boundary_ms를반환하고잘못된값·다른창계측·사적경로를거부한다(15검사통과). 자세한최신상태는gg-autoloop-ledger 첫절이다. 현재400FPS미달/일반채택0.
# round95 완료: 공유 저장 보류

- 실제2560×1440, GPU 대상23547개의 정렬 버킷507개마다 별도 static Combine/507개 버퍼로 나눴다. 공개 key mismatch0이어도 원본C/후보B는4075/3686400픽셀·maxRGBA5,7,2,0으로 실패했다. 같은 판 기존I도 원본C대비295픽셀·max3,2,2,0이므로 이 실패를 후보에 전부 귀속하지 않는다. 저장한 PNG의 I/B 직접 비교는3780픽셀·max5,7,2,0이다. 단순 수치 빼기로 추가 오류를 계산한 것이 아니다.
- C/C·I/I·B/B 반복 각각0. 원본과 후보가 같다는 gate는 실패했고 성능96은 자동 시작하지 않았다(0판). assertion 실패 후에도 exit0/게임false/9상태9복원이다. `out/round95-sort-bucket-summary.json`에 원시 비교를 보존했다.
- 공유 저장의 작은 우회를 더 쌓지 않고 중단한다. 기본 원래 native 렌더 경로 유지, 정식 채택0. 픽셀 gate와 실제 정렬의 미확인 부분을 허용 오차 변경으로 넘기지 않는다. 다음은 이미지 선택자 표/실제 이벤트 일치 검사이며 별도 `gpudeco-image-trace-20261024.md`에 기록했다.

# round92~95 후속: 미지원 이미지/공유 저장 원인 구분

- 92/93은 FPS판이 아니라 명시적 경계 census다. 92의 260/265/270 요청에서 CPU잔류304/378/437개, 93의 실제260.008989923168/265.006792657543/270.01433049933985초에서304/378/434개였다. 순간별 Rec 수를 고정값이나 전곡 최대값으로 부르지 않는다.
- 93의 잔류 중 이미지 관련296/370/413개는 수집한 예약 이벤트의 imageFilenameUsed만 있었다(비트1). originalSize/smoothing/crop 비트는0이다. 이는 곡 전체 중 한 번이라도 이미지 지정이 있었다는 뜻이지, 이 세 순간에 실제 그림이 바뀌었다거나 같은 그림 재지정이라는 뜻은 아니다. 해당 이미지 이벤트의 실제 동일성/지원은 미확인. table/이벤트/구운 수식/원본 그림 변경0, AUTOTEST 스냅샷 메타데이터와 경계 집계만 추가. 두 판 exit0·9복원·GPU23547유지/native오류0.
- 94는 실제2560×1440으로 바뀐 화면에서 수행했다. 이전3440 FPS와 합산0. 같은 그림·시계의 원본C/독립I/공유 비반올림R/공유 반올림Q를 저장한 뒤 비교했으며, 각 방식 자체 반복 C/C·I/I·R/R·Q/Q는0/3686400픽셀이다. C/I도0. I/R과C/R은712164픽셀/maxRGBA7,6,9,0; C/Q는425359픽셀/maxRGBA4,6,5,0이다. 반올림 제거로 문제 해결0, 시간 변화로 원인 확정0. assertion 실패로 정상 종료·exit0/9복원. 성능판0.
- native raw copy 앞의 `GpuDecoCopy`에는 이미 UNorm8 정규화가 있다. 함수 내부의 raw Store만 보고 기존 draw가 비반올림이라고 판단한 것은 철회했다. 네이티브/셰이더 수정0.
- 95는 마지막 공유 저장 가설: 서로 다른 layer/order/queue/distance의 원본 정렬 버킷이 하나의 static batch를 공유하지 않도록 분리한다. 같은 버킷의 내부 tie는 여전히 미확인이므로 픽셀 gate를 요구한다. prepared/paused 연구 명령 `storage sharedbuckets`만, 기본끔/일반Player제외. 반복 mesh 검색은 준비 단계에서만 dictionary로 묶고, 정렬/그리기/소유 채널 자체를 생략하지 않는다. 현재 시험 완료 전 채택0. 결과는 실제 manifest/PNG를 먼저 확인한다. 실패하면 공유 저장 방식의 추가 우회는 중단한다.

