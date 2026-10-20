# tilebake 이벤트 파일 지원률 추정

읽기 전용. 게임 실행/설치/파일 수정 없음.

## Ludicin-Lost-Requiem.adofai

파일: `D:\얼불춤 맵 파일\Lost Requiem\Ludicin-Lost-Requiem.adofai`

SHA256: `ee9275397a1cfa522b6d000c59945cf21b5b921f72cb0d08ba24be2b8ba606bd`

타일 15,001개 (geometry 길이+시작 타일), normal custom / High / remakeFloors 가정.

| 구분 | 사유 | 고유 타일 | 전체 비율 |
|---|---|---:|---:|
| 제외 추정 | writer:ffxFloorDisappearPlus | 11,400 | 75.995% |
| 제외 추정 | writer:ffxFloorAppearPlus | 10,785 | 71.895% |
| 미정 | runtime:initial/tweens/scene/foreign-patches/clock | 15,001 | 100.000% |
| 미정 | scale-cross-axis-frame-dependent-skip | 11,032 | 73.542% |
| 미정 | rotation-wrap/origin-getter | 8,790 | 58.596% |
| 미정 | hitbox/manual-tag-binding | 411 | 2.740% |
| 기능/위험 노출 | scale-supported-channel | 11,032 | 73.542% |
| 제외 추정 | conditional/manual/hit | 0 | 0.000% |
| 제외 추정 | visual-setting-condition | 0 | 0.000% |
| 제외 추정 | unknown-ease | 0 | 0.000% |
| 제외 추정 | duration-invalid | 0 | 0.000% |
| 파일상 유무 | 자유 이동 이벤트 | 0 | 0.000% |
| 합집합 | 파일에서 제외 추정 | 11,837 | 78.908% |
| 후보 상한 | 알려진 제외가 없는 타일 | 3,164 | 21.092% |
| 확정 지원 | 런타임 미정 때문에 확정 불가 | 미정 | 미정 |

사유는 중복 가능. 후보는 실제 지원률이 아닌 상한이며 회전·크기 근접 비교는 초기값/시간순서가 필요하다.

| 큰 전체 되돌리기 형태 (위치0/회전0/크기100%) | 범위 | 타일 | duration 비트 | opacity % | 후보 |
|---|---|---:|---:|---:|---:|
| actions(active) #18883, floor 14926 | 6830..15000 | 8,171 | 32 | 245 | 2,202/8,171 |
| actions(active) #555, floor 464 | 510..664 | 155 | 0 | 100 | 0/155 |
| actions(active) #15403, floor 13695 | 13730..13755 | 26 | 32 | 245 | 26/26 |
| actions(active) #12822, floor 12799 | 12834..12834 | 1 | 16 | 100 | 1/1 |
| actions(active) #12844, floor 12800 | 12835..12835 | 1 | 16 | 245 | 1/1 |

다음에 풀 사유 (다른 제외를 고정했을 때 단독으로 되살아나는 수 / 현재 노출 수):
- writer:ffxFloorDisappearPlus: 단독 +1,052, 노출 11,400; FloorBake.ExcludeWriter/Copy, ffxChangeTrack.PrepFloor 근거.
- writer:ffxFloorAppearPlus: 단독 +437, 노출 10,785; FloorBake.ExcludeWriter/Copy, ffxChangeTrack.PrepFloor 근거.
- 미정 해소 scale-cross-axis-frame-dependent-skip: 11,032타일; 지원 증가량 미정 (FloorBake.Snapshot/Collect, FloorBakeCore.Compile).
- 미정 해소 rotation-wrap/origin-getter: 8,790타일; 지원 증가량 미정 (FloorBake.Snapshot/Collect, FloorBakeCore.Compile).
- 미정 해소 hitbox/manual-tag-binding: 411타일; 지원 증가량 미정 (FloorBake.Snapshot/Collect, FloorBakeCore.Compile).

531.8초는 인계의 런타임 시각 식별자다. 이 도구는 tempo/entryTime을 복원하지 않아 시각·초 단위 길이를 확정하지 않는다.

## level.adofai

파일: `D:\얼불춤 맵 파일\HELLO (BPM) 2026\level.adofai`

SHA256: `11945eca2d39e1760a3d4b80325fd2497f6bc1c95666e4a8d2ec14d0e12b4bd8`

타일 14,721개 (geometry 길이+시작 타일), normal custom / High / remakeFloors 가정.

| 구분 | 사유 | 고유 타일 | 전체 비율 |
|---|---|---:|---:|
| 제외 추정 | writer:ffxFloorAppearPlus | 13,455 | 91.400% |
| 제외 추정 | writer:ffxFloorDisappearPlus | 5,913 | 40.167% |
| 미정 | runtime:initial/tweens/scene/foreign-patches/clock | 14,721 | 100.000% |
| 미정 | hitbox/manual-tag-binding | 5,428 | 36.872% |
| 미정 | scale-cross-axis-frame-dependent-skip | 5,423 | 36.839% |
| 미정 | rotation-wrap/origin-getter | 239 | 1.624% |
| 기능/위험 노출 | scale-supported-channel | 6,128 | 41.628% |
| 기능/위험 노출 | rotation-offset-outside-one-turn | 239 | 1.624% |
| 제외 추정 | conditional/manual/hit | 0 | 0.000% |
| 제외 추정 | visual-setting-condition | 0 | 0.000% |
| 제외 추정 | unknown-ease | 0 | 0.000% |
| 제외 추정 | duration-invalid | 0 | 0.000% |
| 파일상 유무 | 자유 이동 이벤트 | 0 | 0.000% |
| 합집합 | 파일에서 제외 추정 | 13,483 | 91.590% |
| 후보 상한 | 알려진 제외가 없는 타일 | 1,238 | 8.410% |
| 확정 지원 | 런타임 미정 때문에 확정 불가 | 미정 | 미정 |

사유는 중복 가능. 후보는 실제 지원률이 아닌 상한이며 회전·크기 근접 비교는 초기값/시간순서가 필요하다.

| 큰 전체 되돌리기 형태 (위치0/회전0/크기100%) | 범위 | 타일 | duration 비트 | opacity % | 후보 |
|---|---|---:|---:|---:|---:|
| 해당 형태 없음 | - | 0 | - | - | - |

다음에 풀 사유 (다른 제외를 고정했을 때 단독으로 되살아나는 수 / 현재 노출 수):
- writer:ffxFloorAppearPlus: 단독 +7,570, 노출 13,455; FloorBake.ExcludeWriter/Copy, ffxChangeTrack.PrepFloor 근거.
- writer:ffxFloorDisappearPlus: 단독 +28, 노출 5,913; FloorBake.ExcludeWriter/Copy, ffxChangeTrack.PrepFloor 근거.
- 미정 해소 hitbox/manual-tag-binding: 5,428타일; 지원 증가량 미정 (FloorBake.Snapshot/Collect, FloorBakeCore.Compile).
- 미정 해소 scale-cross-axis-frame-dependent-skip: 5,423타일; 지원 증가량 미정 (FloorBake.Snapshot/Collect, FloorBakeCore.Compile).
- 미정 해소 rotation-wrap/origin-getter: 239타일; 지원 증가량 미정 (FloorBake.Snapshot/Collect, FloorBakeCore.Compile).

531.8초는 인계의 런타임 시각 식별자다. 이 도구는 tempo/entryTime을 복원하지 않아 시각·초 단위 길이를 확정하지 않는다.
