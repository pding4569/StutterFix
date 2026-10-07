# 릴리스 순서 (Claude·Codex 누가 하든 같음)

**릴리스는 사용자가 "올려"라고 한 뒤에만.** 모드에 자동 업데이트가 있어서, GitHub 에 최신 릴리스를 올리는 순간 **모든 사용자에게 자동으로 퍼진다.** 되돌리기 어렵다.

## 0. 올리기 전

- 테스터 zip(`./pack.sh test <번호>`)을 테스터에게 먼저 줬고, 문제 보고가 없거나 고쳤다.
- 이번에 바뀐 기능은 측정 근거가 있다(CLAUDE.md 규칙). 원래 게임과 같은지 대조한 기능은 대조 결과가 있다.
- `StutterFix.cs` 의 `MeasureBuild` 가 `false` 다(측정용 빌드로 배포하지 않는다).

## 1. 버전 올리기

- `Info.json` 의 `Version` 을 새 버전으로 (예: 2.4.7 → 2.4.8).
- `repository.json` 의 `Version` 과 `DownloadUrl`(`.../releases/download/v<버전>/StutterFix-<버전>-player.zip`).
- `README.md` 에 "`<버전>` 에서 바뀐 것" 절을 맨 위에(측정값, 사용자 말로). 목차 줄도. 오래된 절은 `CHANGELOG.md` 로.
- 도움 준 사람이 있으면 README 에 이름(예: PR 을 준 사람).

## 2. 빌드와 확인

```bash
./pack.sh            # 플레이어용·개발자용 빌드 + LoadCheck + dist/*.zip
```
- LoadCheck 가 실패하면 멈춘다(모드가 아예 안 켜지는 실수를 잡는다).
- 플레이어용 zip 을 실제로 Mods 에 설치해 게임을 켜고: 로그에 패치 설치 오류가 없는지, 맵 하나를 끝까지 돌려 끊김 요약이 정상인지(sfmeasure `sf_run` 또는 사람이).

## 3. 올리기

1. main 에 합치고 푸시 (작업 브랜치 → main).
2. 태그 `v<버전>` 을 만들어 푸시.
3. GitHub 릴리스 `v<버전>` 을 만들고 `dist/StutterFix-<버전>-player.zip`, `dist/StutterFix-<버전>-developer.zip` 을 올린다.
   **파일 이름이 정확해야** 자동 업데이트가 받는다(`Updater.cs`: `releases/download/v<버전>/StutterFix-<버전>-player.zip`).
   릴리스 노트는 README 의 그 버전 절과 같은 내용.
4. 프리릴리스로 올리지 않는다(자동 업데이트는 latest 만 본다). 테스터용은 GitHub 에 올리지 않고 디스코드로.

## 4. 올린 뒤

- 다른 PC(또는 이전 버전을 깐 상태)에서 게임을 켜 자동 업데이트가 새 버전을 받는지 확인.
- 디스코드에 알림(바뀐 것 짧게).
- 문제가 생기면: 고친 버전을 바로 다시 올린다(자동 업데이트는 더 높은 버전만 받으므로 되돌린 내용도 **버전을 올려서** 낸다).

## 쓰면 안 되는 것

- 게임 DLL(Assembly-CSharp 등), 디컴파일한 게임 코드, 개인 시험 맵 이름, 사용자 이메일을 저장소·릴리스·노트 어디에도.
- 다른 모드 코드.
