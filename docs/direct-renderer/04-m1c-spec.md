# 코덱스 작업: 직통 렌더러 M1c — 실게임 결과로 고치기 (2026-11-15)

작업 폴더 `E:\StutterFixWork\wt-v14prod`. 커밋·게임 실행·설치 DLL 교체 금지. 한국어 보고. 결과: `docs/direct-renderer/04-m1c-result.md`.
앞 단계: `03-m1b-result.md`. 로그: `E:\StutterFixWork\night-results\dp5-{hl50,wf,dd,sh}-drPix.log`.

## 실게임 결과 (drPix: 정상 모드, 일시정지 → verify → d1 → off → d0 → diff)
| 맵 | 결과 |
|---|---|
| HELLO 50초 | **켜지지 않음**: `정적 값 검증 실패: 부분 소유 대신 원래 경로` (owned 659 중 released 5) |
| Windflower 258초 | 같음 (owned 1513, released 2) |
| DD 50초 | 같음 (owned 452, released 1) |
| Exit 88초 | 켜짐. 활성화 로그 `맡은 장식 1039개` 가 **6번** 찍힘(재활성화 반복?), 일시정지 시점 state 는 `owned=44`. verify `max_world=0.0626 max_uv=6e-8 max_color=1/255`, **화면 차이 0**. 첫 commit 196ms(`commit_us=196083` 첫 프레임), 이후 평균 0.8ms 로 표시됨 |
- 앞 층(Default) 장식은 거의 전부 `순서: 앞쪽 남는 것 아래`로 거부. 끼울 수 없는 것 상위: `MeshRenderer:TextMeshPro/Distance Field Overlay 2800`(판정 글자 풀 등), `ParticleSystemRenderer:GUI/Text Shader 1400`, TrailRenderer, 파티클, ADOFAI/Frames(행성), Sprites/Default …
- 앞 층 묶음은 AfterForwardAlpha(=Unity 의 반투명 그리기 전부 뒤)에 그려지므로, 맡은 앞 장식보다 위에 있는 남는 것은 전부 차례에 끼워야 한다.

## 할 일
1. **부분 실패 처리**: 정적 값 검증에 실패한 장식 때문에 전체를 포기하지 말고, 그 장식을 "남는 렌더러"로 돌려 차례에 끼워(DrawRenderer) 그린다. 끼울 수 없으면 그 장식 기준으로만 경계를 다시 계산한다. 실패 이유(장식 이름·필드·값)를 상위 10개 로그로.
2. **재활성화 반복과 첫 프레임 196ms**: Exit 에서 활성화가 6번 반복된 이유를 찾고 한 번만 하게. 첫 commit 의 무거운 일(정적 값·텍스처 등록·도형·경계)은 재생 준비(PrepVfx 후)에 끝내고, 곡 중 첫 프레임에 몰리지 않게. 곡 중 경계 재계산도 스파이크 없게(점진 또는 미리 계산).
3. **앞 층 남는 것 끼우기**: TextMeshPro(판정 글자 등 MeshRenderer), 파티클(ParticleSystemRenderer), TrailRenderer, 행성 스프라이트(ADOFAI/Frames 등 단일 패스)는 CommandBuffer.DrawRenderer 로 차례에 끼울 수 있다(단일 패스·마스크 없음). 끼울 수 없다고 분류되는 이유를 분해해 로그하고 고쳐라. 남는 것이 수천 개여도(판정 글자 풀 2,800 등) 매 프레임 전수 확인하지 않게: 켜짐 변화는 후크(SetActive/enabled 를 바꾸는 게임 함수, 판정 글자 풀의 꺼내기/돌려넣기 등)나 Unity 의 `Renderer.isVisible`/컬링 결과를 싸게 쓰는 방법을 찾고, 안 되면 "보이는 것만 차례에 넣고 나머지는 숨김 층"처럼 비용을 제한해라.
4. state 의 owned 수가 활성화 로그와 다른 이유(1039 대 44)를 설명/수정하고 state 에 맡은 수·끼운 수·경계 사유를 일관되게.
5. 메인 스레드 직통 비용 < 50µs/프레임(안정 구간), 할당 0, 4종 빌드 오류 0, export 보존.
6. 결과 문서에 Claude 가 돌릴 bench 설정(drPix·drOn·ppGpuCam ABBA)과 기대 지표.
