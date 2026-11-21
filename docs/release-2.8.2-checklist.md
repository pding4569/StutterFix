# 2.8.2 올릴 때 체크리스트 (사용자가 "올려"라고 한 뒤에만)

상태(2026-11-22): 브랜치 `release/2.8.2`, 버전 파일(`Info.json`·`repository.json`)·README·패치노트·공지문은 모두 2.8.2 로 준비됨. main·태그·GitHub 릴리스는 **아직 없음**.
수정 목록과 확인 근거는 `docs/release-2.8.2-fixes.md`, 사용자에게 보일 글은 `docs/patch-notes-2.8.2.md`(릴리스 노트), `docs/announcement-2.8.2.md`(디스코드).

## 올리기 직전 확인 (각 1분)

1. `git status` 가 깨끗하고 `git log -1` 이 `release/2.8.2` 의 마지막 커밋.
2. `StutterFix.cs` 의 `MeasureBuild` 가 `false` (`grep -n "MeasureBuild =" StutterFix.cs`).
3. `dist/StutterFix-2.8.2-player.zip` 이 현재 소스로 만든 것: 소스를 고쳤다면 `./pack.sh` 를 다시 돌린다(LoadCheck·CompatCheck 포함). 정식 ZIP 해시는 `tools/ReleaseCheck/out/full-game-2.8.2.json` 의 `normal_player_zip_sha256` 와 같아야 한다(다르면 전곡 검증을 `python tools/ReleaseCheck/out/full_game_282.py` 로 다시).
4. 게임 폴더가 정식 상태: `Mods/StutterFix/StutterFix.dll` 이 플레이어용, `Plugins/x86_64/steam_api64.dll` 존재, 임시 플래그 파일 없음.

## 올리기

```bash
cd /e/StutterFixWork/wt-282
git fetch origin
git checkout main && git pull --ff-only origin main          # main = v2.8.1 d6fb971
git merge --ff-only release/2.8.2                             # 빨리감기만 가능해야 정상
git push origin main
git tag v2.8.2 && git push origin v2.8.2
gh release create v2.8.2 dist/StutterFix-2.8.2-player.zip dist/StutterFix-2.8.2-developer.zip \
  --title "StutterFix 2.8.2" --notes-file docs/patch-notes-2.8.2.md --latest
```
- 프리릴리스로 올리지 않는다(자동 업데이트는 latest 만 본다). 파일 이름이 정확해야 한다(`StutterFix-2.8.2-player.zip`).
- `git merge --ff-only` 가 실패하면 main 에 새 커밋이 생긴 것이다: 멈추고 사용자에게 알린다.

## 올린 뒤

1. 이전 버전(2.8.1)을 깐 상태에서 게임을 켜 자동 업데이트가 2.8.2 를 받는지 확인(재시작 안내가 뜬다).
2. 디스코드에 `docs/announcement-2.8.2.md` 내용을 올린다(링크 마지막 줄의 릴리스 주소 포함).
3. 문제가 생기면 고친 판을 **더 높은 버전**(2.8.3)으로 낸다.

## 사용자가 알아야 할 동작 변화 (공지 보강용)

- 타이머 0.5ms 요청이 기본 켬이고, 2.8.1 에서 올린 사람도 처음 한 번만 켜진 상태로 바뀐다(설정창 `플레이`에서 끌 수 있음).
- `화면 밖 파티클 멈추기`는 한 번만 꺼진 상태로 바뀐다(`저사양` 페이지에서 다시 켤 수 있음). Arche 곡 262~277초의 약 +4.7%는 포기한 대가.
- `메모리 정리 미루기`를 꺼 둔 사람도 이제 곡 시작·끝 처리(셰이더 미리 준비, 파티클 건너뛰기 등)가 돈다. 화면은 Arche·HELLO·Windflower 한 시점씩 켬/끔을 비교해 같았다(곡 시각 정렬, `docs/release-2.8.2-fixes.md` #4).

## 아직 확인 못 한 것 (공지에 과장하지 않기)

- 설정창에서 `자세한 로그`를 눌러 저장·재시작하는 흐름을 실제 게임 창으로는 안 해 봤다(설정 파일로 켠 경우만 확인).
- 리눅스·맥(원래 유니티) 실기 시험은 못 했다.
- 독점 전체 화면에서 CPU 멀티코어를 못 쓴다는 제보는 재현 못 했다(제보자 CPU 모델·본 화면이 필요).
- 타이머 0.5ms 의 실제 체감 크기는 이 PC(ISLC 가 이미 0.5ms 로 고정)에서 측정하지 못했다.
