# Codex 임무 framecmp: 게임 실제 카메라 체인에서 "같은 프레임 픽셀 비교" 도구 (극한 최적화 로드맵 1단계)

2026-10-21. 작업 폴더 `E:\StutterFixWork\wt-framecmp` (브랜치 `codex/night-framecmp`, `codex/night-archegpu` 위). 게임 실행·설치·커밋 금지(Claude 가 한다). 결과 `docs/handoff/codex-framecmp-20261021-result.md` + `-o`(10줄).

## 왜 필요한가
Arche 262→276초는 원본 약 125 FPS인데 **장식 0개 사본은 약 546 FPS**다(`docs/optimization-36.md` 끝: 프레임 8.0ms 중 켜지고 움직이는 장식 약 2,000개와 그 처리가 5.7ms). 이걸 줄이려면 활성 장식을 Unity 오브젝트 없이 그리는 대체 렌더러(소수의 인스턴싱 draw, 우리가 정한 그리기 순서, 굽기 표 기반 GPU 애니메이션)가 필요하고, 그 모든 단계는 **"원본과 화면이 같다"를 자동으로 증명**할 수 있어야 채택 가능하다. 지금은 그 도구가 없다: 기존 `pixeldiff`(`GpuSplitPixelDiff.cs`)는 본 카메라 `clear=Nothing` 때문에 "Nothing clear mode requires depth preservation; unsupported" 로 전부 실패했다. 이 임무는 그 도구를 만드는 것이다.

## 알고 있는 것
- 장면 카메라(`archegpu on` 로그): `BGStaticCam`(clear SolidColor, mask 2048), `BGMovingCam`(depth −1.11, clear Depth, mask 1024), 본 `Camera`(**clear Nothing**, mask 652183), `OverlayCam`(depth −1.63~ clear SolidColor ...), 모두 `target=backbuffer` 로 보고되지만 게임은 `scrCamera.SetupRTCam` 으로 공유 RT(camRT)에 BG/본 카메라를 그리고 최종 quad/Overlay 로 화면에 낸다(`docs/handoff/codex-gpu-20261018-camera-il.txt` 참고). 카메라 순서에 의존하는 누적 렌더라 한 카메라만 따로 `Render()` 하면 깨진다.
- 필터(`CameraFilterPackLegacy_*`, VideoBloom 등)는 시간 의존(필름 그레인, 비 등)이라 연속 프레임이 서로 다르다 → 비교 전에 필터를 끄거나 비교에서 격리해야 한다.
- 이전 Tight 메시 시험은 990조건 중 832조건 픽셀 불일치였다(그 뒤 폐기). 비교 도구는 이런 미세 차이도 잡아야 한다.
- 정렬 규칙(`SortProbe`): 같은 키 동률은 재질 인스턴스 ID 오름차순이 뒤→앞.

## 만들 것 (`DEV || AUTOTEST`, 기본 꺼짐, Player 제외, `Main.Unload` 복구)
1. **정지 상태 만들기**: AutoTest 명령 `pause`/`resume`(게임 UI 일시정지가 아니라 장면 상태를 멈추는 안전한 방법: 코드로 확인 — `scrController` 의 pause, `DOTween` 타임스케일, `AudioListener.pause`, 모드의 `DecoAnim`/`FloorAnim` 갱신이 멈추는지). 정지 중 장식·타일 상태가 프레임마다 **완전히 같아야** 한다는 것을 검증(아래 3번의 잡음 바닥).
2. **최종 화면 캡처**: 게임의 실제 카메라 체인을 그대로 둔 채, **마지막 카메라 렌더 끝(`CameraEvent.AfterEverything` 또는 `Camera.onPostRender`/`WaitForEndOfFrame`)에서 화면에 나가는 최종 이미지를 RT 로 복사**(CommandBuffer `Blit(BuiltinRenderTextureType.CameraTarget or CurrentActive, rt)` 또는 `ScreenCapture`/백버퍼 접근 중 안전한 것)해 `AsyncGPUReadback` 으로 비동기 회수. 추가 `Camera.Render()` 호출 금지(누적 렌더가 깨지므로). 해상도는 화면 그대로(2560×1440, 3440×1440 모두). UI/HUD 를 포함할지 제외할지 옵션(UI 카메라 레이어 마스크 이용), 기본은 UI 제외 가능하면 제외.
3. **비교 명령** `framecmp`:
   - `framecmp filters off|on` (캡처 동안 필터 컴포넌트 `enabled=false`/복원; 시간 의존 필터 격리),
   - `framecmp grab <이름>`: 다음 프레임 최종 이미지를 메모리에 저장(완료까지 기다림, 시간 초과 시 실패),
   - `framecmp diff <A> <B> [임계=0]`: RGBA8 채널별 최대 차이, 다른 픽셀 수, 전체 대비 비율, PSNR, 차이가 큰 상위 영역 좌표 5곳(타일 32px 격자), A/B 이미지 PNG 저장 옵션(개발 폴더),
   - `framecmp noise <이름A> <이름B>`: 같은 상태 연속 두 캡처의 차이 = **잡음 바닥**(0 이어야 정상; 0 이 아니면 이유 분석: 필터, 시간 의존 셰이더, MSAA, 비동기 업로드 등).
4. **양성 대조군(필수)**: 순서에 민감하지 않은 "차이 0" 은 증거가 못 된다. `framecmp control order`: 화면에 겹쳐 보이는 장식 한 쌍을 찾아 (실제 장면에서) 재질 ID 순서를 바꾸는 식으로 그리기 순서를 **일부러 뒤집고** 차이가 검출되는지(>0) 확인하고 복원. `framecmp control hide`: 화면에 보이는 장식 하나 `forceRenderingOff` → 검출되어야 함. 음성 대조군: 변경 없음 → 0.
5. **사용 예 steps**(내가 돌릴 것): Arche `open → seek 262 8 → waitsongpos 264 → pause → filters off → grab A → (변경 적용: 예 `matshare on` 또는 `gpusplit hide deco-image:small=1` 같은 기존 진단) → 몇 프레임 대기 → grab B → (복원) → grab A2 → diff A A2(=0) → diff A B → control order/hide → resume`. 이를 `tools/optimization/framecmp-steps.py` 로 생성.
6. 빌드 3종(`-c Release`; `-p:Edition=Player`; `-p:Edition=Player -p:AutoTestBuild=1`) 오류 0 + LoadCheck. Player 제외. 새 Harmony 패치는 이름+인자 수+호출 규약으로 정확히 하나(가능하면 CommandBuffer/이벤트만으로), `Module.ResolveSignature` 금지.

## 규칙
- 헤르츠 맵 이름·비공개 프로젝트 언급 금지, 100만 타일 맵 열기 금지.
- 이 도구는 성능 측정 창과 분리(캡처·readback 비용 큼). 성능 판에서 켜지 않는다.
- 막히면 **이유를 구체적으로**(어떤 카메라 설정·RT 형식 때문에 안 되는지) 적고, 대안(예: 스크린샷 API, 외부 캡처) 후보와 한계를 문서에. 거짓 낙관 금지.
