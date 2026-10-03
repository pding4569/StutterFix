#!/usr/bin/env bash
# 플레이어용과 개발자용을 빌드해 UMM 에 바로 넣을 수 있는 zip 두 개를 dist/ 에 만든다.
#   dist/StutterFix-<버전>-player.zip    일반 배포용 (수정만, 측정/로그/단축키 없음)
#   dist/StutterFix-<버전>-developer.zip 원인 추적용 (끊김 기록, Ctrl+F5, F6~F9)
#   ./pack.sh test <번호>                테스터용 (아래)
# zip 안은 StutterFix/Info.json + StutterFix.dll 이다(UMM "Install Mod" 로 설치).
set -e
cd "$(dirname "$0")"
ver=$(grep -oE '"Version": *"[^"]+"' Info.json | grep -oE '[0-9][0-9.]*')

# 테스터용: ./pack.sh test <번호>  ->  dist/StutterFix-<마지막 릴리스>.<번호>-tester.zip
#   DLL 은 플레이어용 그대로, Info.json 의 이름과 버전만 바꾼다(UMM 목록에 "테스터 x.y.z.n" 으로 보임).
#   버전을 마지막 릴리스(repository.json) 뒤에 번호를 붙인 것으로 해서, 다음 정식 릴리스가 나오면 자동 업데이트로 넘어가고
#   그 전에는 지금 정식판으로 되돌아가지 않는다(Updater.Newer: 2.4.7 < 2.4.7.1 < 2.4.8).
if [ "$1" = "test" ]; then
  n="${2:?테스터 번호를 준다: ./pack.sh test 1}"
  base=$(grep -oE '"Version": *"[^"]+"' repository.json | head -1 | grep -oE '[0-9][0-9.]*')
  tver="$base.$n"
  dotnet build -v q --nologo -c Debug -p:Edition=Player
  dotnet build tools/LoadCheck -v q --nologo -c Release -o tools/LoadCheck/bin
  tools/LoadCheck/bin/LoadCheck.exe bin/Player/StutterFix.dll
  rm -rf dist/tester && mkdir -p dist/tester/StutterFix
  cp bin/Player/StutterFix.dll dist/tester/StutterFix/
  sed -e "s/\"Version\": *\"[^\"]*\"/\"Version\": \"$tver\"/" \
      -e "s/\"DisplayName\": *\"Stutter Fix\"/\"DisplayName\": \"Stutter Fix (테스터 $tver)\"/" Info.json > dist/tester/StutterFix/Info.json
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File zip.ps1 -Version "$tver" -Editions tester
  ls -la dist/*-tester.zip
  exit 0
fi

dotnet build -v q --nologo -c Debug
dotnet build -v q --nologo -c Debug -p:Edition=Player

# 불러오기 미리 확인: 게임 필드를 잘못 적은 FieldRefAccess 가 있으면 모드 전체가 안 켜지므로 여기서 멈춘다
dotnet build tools/LoadCheck -v q --nologo -c Release -o tools/LoadCheck/bin
tools/LoadCheck/bin/LoadCheck.exe bin/Debug/StutterFix.dll
tools/LoadCheck/bin/LoadCheck.exe bin/Player/StutterFix.dll

rm -rf dist && mkdir -p dist/player/StutterFix dist/developer/StutterFix
cp bin/Player/StutterFix.dll Info.json dist/player/StutterFix/
cp bin/Debug/StutterFix.dll dist/developer/StutterFix/
sed 's/"DisplayName": *"Stutter Fix"/"DisplayName": "Stutter Fix (개발자용)"/' Info.json > dist/developer/StutterFix/Info.json

powershell.exe -NoProfile -ExecutionPolicy Bypass -File zip.ps1 -Version "$ver"
ls -la dist/*.zip
