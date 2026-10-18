# GPU 분해 진단 (DEV / AUTOTEST 전용)

`powershell -ExecutionPolicy Bypass -File tools/gpuprobe/build.ps1`로 독립 `gpuprobe.dll`과 `check.exe`를 만든다. sfnative 변경·내장·자동 설치는 없다. `check.exe`는 별도 WARP D3D11 장치로 검사하며 게임을 실행하지 않는다. 테스트에서만 Flush/짧은 대기를 쓴다. query.cpp의 게임 콜백은 Flush·동기 대기·픽셀 읽기를 하지 않는다.

실제 게임 측정 담당자는 별도 설치가 허용된 환경에서 gpuprobe.dll을 StutterFix.dll 옆에 제공해야 한다. 재배포 대상이 아니다. D3D11만 지원한다. PlayerAuto 또는 DEV managed DLL이 필요하다.

- `gpuprobe on`: 기존 세션 해제 후 쿼리·카메라 버퍼·현재 장면 카메라의 OnRenderImage 패치를 준비한다. **맵을 연 뒤, press 전에 실행**한다. 비활성 필터도 나중의 활성화에 대비해 패치한다. 켜는 순간의 비용을 성능으로 쓰지 않는다.
- `gpuprobe reset`: 패치와 쿼리를 유지하고 측정 창을 비동기로 비운다. 회수 전 표본은 epoch로 구분하여 버린다. 곡 중 특정 구간을 측정할 때 사용한다.
- `gpuprobe state`: 회수 완료 표본의 카메라·효과별 프레임 평균/활성 프레임 평균/프레임 최대/호출 수와 전체-카메라 합 차이를 출력한다. 프레임마다 로그를 쓰지 않는다. snapshot busy면 다시 state 한다.
- `gpuprobe off`: 카메라 구독·명령 버퍼·Harmony 패치·코루틴을 제거하고, 렌더 스레드에 native 쿼리 해제를 예약한다. Main.Unload에서도 같은 해제를 호출한다. off는 자동 report하지 않으므로 state부터 실행한다.
- `gpucensus`: 일회성 조사. `%LOCALAPPDATA%/StutterFix/gpu-census-*.tsv`에 전체 그룹을 쓰고 로그에 카메라별 요약을 남긴다. Mods에는 쓰지 않는다. **타이밍 창을 닫은 뒤 별도 판에서 실행**한다.

32 카메라, 477 OnRenderImage 인스턴스를 등록할 수 있다. 새로 생성된 카메라·컴포넌트는 장면이 안정된 후 다시 on으로 등록한다. 기존 카메라의 disabled 컴포넌트가 켜지는 것은 추가 설치 없이 측정한다. 현재 상한을 넘으면 설치를 정리하고 오류를 낸다.

8프레임 중 하나에만 timestamp를 넣는다. 8개 순환 슬롯, 표본당 최대 96 카메라/효과 호출, 3프레임 이상 뒤에 가장 오래된 슬롯 하나만 DONOTFLUSH로 조회한다. 미완료면 그 프레임에서 조회를 끝낸다. 슬롯이 차면 표본을 건너뛴다. 조회용 쿼리는 on에서 모두 만든다. 한 표본당 195개(프레임 disjoint/시작/끝 + 96쌍), 전체 1,560개다.

카메라는 onPreRender부터 AfterEverything까지, 전체는 첫 onPreCull부터 WaitForEndOfFrame까지다. 필터는 Harmony prefix/finalizer 사이의 GPU 제출 구간이다. 카메라 시간 안에 필터 시간이 있으므로 더하지 않는다. `effect_outside_camera`가 있으면 이 포함 가정을 그 판에 적용하지 않는다. 수동/nested Camera.Render는 카메라 합이 겹칠 수 있다. GPU 경과 시간에는 제출 사이 GPU 유휴·스케줄링도 섞일 수 있어 FrameTiming/PresentMon의 GPU busy와 같지 않다. 샘플 최대는 전체 프레임의 최악값이 아니다.

census의 화면 덮임은 near/far와 viewport로 자른 **월드 AABB**의 면적 합이다. 실제 픽셀 채움률/오클루전 결과/배치 수가 아니다. renderer enabled/forceRenderingOff/activeInHierarchy/cullingMask/프러스텀/shadows-only를 반영한다. LODGroup, SortingGroup, 스프라이트 sort point, 오클루전, 스텐실·깊이·알파 테스트, 셰이더 pass와 동적 배치는 재현하지 않는다. disabled 카메라도 수동 Render 후보로 표시하므로 실제 호출 여부는 gpuprobe와 함께 판별한다. 표의 property block 수가 0이어도 스킨/인스턴스 셰이더의 모든 값이 복원된 뜻은 아니다. 알파 0은 색 프로퍼티 후보일 뿐 안전한 생략 증명이 아니다.

텍스처마다 Unity 속성과 D3D11 Texture2D GetDesc의 실제 GPU 크기·DXGI·mip/array/MSAA를 함께 기록한다. Texture3D 등은 안전하게 미지원으로 처리한다. DLL이 없으면 GPUdesc=unavailable이다. GetNativeTexturePtr는 렌더 스레드 동기화를 일으킬 수 있어 고유 텍스처당 한 번만 호출하고, 이 명령의 캐시는 종료 때 버린다. census는 반드시 타이밍 판과 분리한다.

사용 명령과 후보 판단은 `docs/handoff/codex-gpu-20261018-result.md` 참고.
