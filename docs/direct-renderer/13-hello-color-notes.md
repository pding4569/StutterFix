# HELLO 50초 배경색 불일치 — Claude 조사 메모 (2026-11-15 밤)

- M4d~M4g 모두 HELLO on/off 64~68% 다름(잡음 d0/d00 0). `drfull grab unity`(BlendModes 를 Unity 경로)에서도 64.7% → BlendModes 원인 아님.
- 같은 세션 off 에서 숨겨 본 것(off 숨김 그림 vs 같은 세션 on 그림): `hidetex space-art`(8개) → 여전히 65%, `hidetex pt2_lights`(158개) → 66%. 원인 아님.
- 영역 평균(캡처 위아래 뒤집어 읽음, 채널 순서는 캡처 그대로): 오른쪽 (2000..2300, 300..700) off (11,26,115) / on (16,19,29); 왼쪽 off (36,57,72) / on (40,52,31). **on 에서 세 번째 채널이 크게 빠진다 = 원본에 있는 색 층이 우리 그림에 없다.**
- 다른 세션 off 에서 `hidepath circleblur`(534 렌더러 — 경로 일치)·`hidepath particles`(ParticleSystemRenderer 2,188개 전부): 오른쪽 세 번째 채널 139 → 72 (on 은 29). 즉 **파티클이 일부(약 절반)를 차지**하고 나머지는 다른 것. 파티클은 우리 차례에서 `cb.DrawRenderer` 로 넘기는데(unityExplicit) Unity 의 CommandBuffer.DrawRenderer 는 ParticleSystemRenderer 에 대해 그 프레임 입자 기하가 준비되지 않으면 아무것도 안 그릴 수 있다 — 확인 필요. 세션 간 같은 seek 시점도 그림이 꽤 다르므로(off d0 끼리 오른쪽 115 vs 139) 반드시 **같은 세션 안에서** off/on/숨김을 비교할 것.
- 새 AutoTest 명령 `hidepath <경로 일부>`(Claude 추가, `particles` 는 모든 ParticleSystemRenderer): forceRenderingOff. 이분 탐색 도구로 쓰면 된다.
- 할 일: 같은 세션에서 on 그림과, off 에서 후보 묶음을 숨긴 그림을 비교하는 이분 탐색으로 "우리 그림에 빠진 렌더러"를 특정(우리가 native/explicit 으로 그렸다고 기록한 것 중 실제 화면에 안 나온 것). 파티클 DrawRenderer 가 안 그려지면 파티클은 보조 카메라 합성(M4d 캔버스 방식)으로 옮기거나 Unity 원래 경로에 남기는 방법.
