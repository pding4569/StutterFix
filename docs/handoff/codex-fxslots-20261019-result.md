# 쉐이더 내 프리셋 슬롯 작업 결과 — 2026-10-19

코드 구현과 오프라인 검증 완료. **게임 실행·Mods 설치·커밋은 하지 않았다.** 실제 화면 캡처, 게임에서의 키 입력, UMM 저장 후 재시작 확인은 아래 순서로 이어서 확인해야 한다.

## 변경 내용

- `SettingsWindow.cs`: 쉐이더 → **켜짐**(`ui 8.0`)의 기존 켜진 효과 목록 아래에 **내 프리셋**을 추가했다. 이름 입력, 지금 설정 저장, 각 항목의 적용·덮어쓰기·삭제를 제공한다. 이름 변경은 기존 항목을 적용하고 새 이름으로 저장한 뒤 기존 항목을 삭제하는 방식이다. 12개가 모두 찼다면 적용 후 기존 항목을 삭제하고 새 이름으로 저장한다.
- `SubHeading`·`BeginGroup/EndGroup`·`Btn`·`P`를 재사용했다. 이름과 작업 단추를 두 줄로 분리하고 단추는 가변 폭으로 배치했다. 긴 이름 때문에 작업 단추가 창 밖으로 밀리지 않도록 했다. 한국어·영어 문자열을 함께 넣었다.
- **모두 끄기**는 현재 코드를 `FxLastOn`에 저장한 뒤 모든 효과 bool과 `FxPreset`을 끈다. 효과가 0개이고 복원 코드가 있으면 같은 자리에 **다시 켜기**가 나타난다. 복원 후 다시 끄면 그 시점 설정을 새로 기억한다. 이미 꺼진 상태에서는 기억한 코드를 덮어쓰지 않는다. 위쪽 기존 모양 선택기의 ‘꺼짐’은 기존 동작을 유지하며 이 복원 저장 기능과 별개다.
- 적용·복원 성공 시 `ScreenEffects.Apply()`와 기존 설정 저장 경로를 거친다. 슬롯 적용 메시지에는 켜진 쉐이더 수를 표시한다. 저장·이름·손상 코드 오류는 인라인 메시지로 표시한다.
- `FxSlots.cs`: 공통 저장·적용·덮어쓰기·삭제·전체 끄기·복원 로직과 공개 직렬화 타입 `FxSlot`을 추가했다. 버튼과 자동 시험 명령이 같은 로직을 쓴다.
- `StutterFix.cs`: `Settings.FxSlots = new List<FxSlot>()`, `Settings.FxLastOn = ""`를 추가했다. 기존 효과 기본값은 바꾸지 않았다. 새 기능은 버튼이나 명령을 실행해야 값을 바꾼다.
- `StutterFix.csproj`: `FxSlots.cs`를 모든 에디션에 포함했다. `AutoTest.cs`의 `fxslot` 분기와 자체 시험 코드는 `DEV || AUTOTEST`에만 포함된다.

## 저장 형식과 호환

UMM의 기존 `Settings.xml`에 다음 필드를 저장한다. 새 필드가 없는 옛 XML은 빈 목록·빈 문자열로 읽는다. 별도 마이그레이션이나 자동 적용은 없다.

```xml
<FxSlots>
  <FxSlot>
    <Name>내 이름</Name>
    <Code>SFX1:...</Code>
  </FxSlot>
</FxSlots>
<FxLastOn>SFX1:...</FxLastOn>
```

- `FxSlot`은 public 타입이며 public 기본 생성자와 public 문자열 필드를 갖는다.
- 최대 12개. 이름은 앞뒤 공백을 제거한 뒤 1~32 UTF-16 단위이며 제어 문자를 거부한다. 대소문자를 구분하지 않는 동일 이름은 새 저장 시 거부하고 명시적인 덮어쓰기에서만 갱신한다. 12개일 때도 기존 항목 덮어쓰기는 가능하다.
- 코드는 기존 **`FxShare.Export(c)`와 동일한 SFX1 형식**이다. 공유 코드의 버전이나 필드 선택·파서는 변경하지 않았다. `Fx*`의 bool·int·float 88개를 포함한다. 새 목록과 복원 문자열은 타입이 달라 공유 코드에 포함되지 않으므로 코드가 중첩되지 않는다.
- `FxLutPath`와 LUT 파일 자체는 포함하지 않는다. LUT 켜짐 여부는 저장하지만 적용할 때는 **현재 PC에서 선택한 경로**를 그대로 사용한다. 경로가 달라진 뒤 예전 슬롯을 적용해도 예전 경로로 돌아가지 않는다.
- 해상도·프레임 생성·`LowSharpen` 같은 `Fx*` 밖의 설정도 포함하지 않는다. `FxRcasSharpness`는 포함한다.
- 슬롯 코드는 정확한 SFX1 접두어와 정규 Base64 형식인지 먼저 검사하고, 값은 기존 `FxShare.Import`의 검증·일괄 적용을 사용한다. 손상 코드와 잘못된 숫자에서 설정을 부분 변경하지 않는 시험을 통과했다. 기존 공유 코드 정책에 따라 알 수 없는 필드는 무시한다. 구버전 코드에 빠진 새 필드는 현재 값을 유지한다.
- 맵별 화면 설정 저장이 켜져 있어도 슬롯 목록·복원 문자열은 전역 XML에 남는다. `RenderProfileValues`는 bool·int·float만 캡처하므로 두 저장 필드는 맵별 값에 섞이지 않는다. 슬롯을 적용한 화면 값은 기존 맵별 저장 정책을 따른다.

## 입력 처리와 수명주기

- 이름 입력에 `GUI.SetNextControlName("fxslot-name")`를 사용한다. 숫자 입력의 `fxvalue-*`와 함께 `GUIUtility.keyboardControl`·포커스 이름으로 입력 중인지 판단한다.
- 이름 입력의 Enter·Escape는 입력 포커스를 해제하고 해당 IMGUI 이벤트를 소비한다. 탭·페이지 전환, 패널 접기, 창 닫기·비활성화 시에도 입력 상태를 해제한다.
- 입력 중에는 `Hotkey.Down`을 통한 모드 단축키와 개발자 Ctrl+F5, 창 Escape 접기를 막는다.
- 게임 DLL을 IL로 확인한 결과 게임은 `GUIUtility.keyboardControl`을 직접 검사하지 않았다. 에디터 단축키 진입점 `scnEditor.HandleKeyboardActions`에 입력 중에만 건너뛰는 Prefix 하나를 기존 `StutterFix` Harmony ID로 추가했다. Prefix는 bool 조회만 하며 반사·할당·타이머를 실행하지 않는다. `Main.Unload` → 기존 `SettingsWindow.Destroy`와 `HarmonyIds`의 `UnpatchAll("StutterFix")`로 상태와 패치가 정리된다.
- 실제 IME 조합, 입력 프레임 순서, 플레이 화면에서의 판정·일시정지 입력과 다른 모드의 단축키까지 충돌하지 않는지는 게임에서 확인하지 않았다. 에디터와 모드 단축키 보호 구현만 정적으로 확인했다. 성능 측정도 하지 않았다.

## 수행한 검증

| 검사 | 결과 |
|---|---|
| `dotnet build StutterFix.csproj -v q --nologo` | 오류 0 / 경고 35 |
| 위 명령에 `-p:Edition=Player` | 오류 0 / 경고 33 |
| 위 명령에 `-p:Edition=Player -p:AutoTestBuild=1` | 오류 0 / 경고 35 |
| `py -3 tools/check-fxslots.py` | PASS |
| `py -3 tools/check-ui-encoding.py` | `SFENC001: clean` |
| 변경한 추적 파일의 `git diff --check` | 공백 오류 없음 |

빌드 경고에는 참조 버전 충돌·사용 중단 API·사용하지 않는 필드 등이 있다. `FxSlots.cs` 경고는 없었다. 빌드 산출물은 저장소 `bin`·`obj`에만 만들었다. 설치 훅은 실행하지 않았다.

오프라인 검사는 실제 `FxShare.cs`·`FxSlots.cs`·`RenderProfileValues.cs`를 링크하고 실제 `Settings` 소스의 Fx 필드 선언으로 만든 임시 fixture를 사용한다. Unity 호출과 영구 저장 stub은 호출되면 실패한다. **실제 UMM 전체 Settings 타입을 실행한 시험은 아니다.**

자체 시험 출력:

```text
PASS fields=89 bitwise/restore/XML/legacy/names/limit/overwrite/atomic/delete
PASS per-map storage isolation
[fxslot] PASS fields=89 bitwise/restore/XML/legacy/names/limit/overwrite/atomic/delete
```

89개 비교 대상은 효과 설정 88개와 경로 `FxLutPath`이며, 새 저장 메타데이터 두 개는 제외한다. 저장→전체 끄기→적용 및 전체 끄기→복원, float 비트 비교, 한글 이름·코드·복원 문자열 XML 왕복, 옛 XML, 잘못된 이름·중복 이름·12개 제한·덮어쓰기 실제 값 갱신·삭제·null 목록, 손상 코드·NaN의 부분 변경 방지를 검사했다. 자체 시험은 별도 Settings 인스턴스만 사용하므로 실제 사용자 설정을 바꾸거나 저장하지 않는다.

## Claude 후속 확인 순서

아래 게임 확인은 **이번 작업에서 실행하지 않았다**. 게임 실행·설치가 허용된 후속 작업에서 진행할 것.

1. 위 세 빌드와 두 Python 검사를 확인하고, 후속 작업의 허용 범위에 맞게 새 DLL을 설치한다.
2. 개발자/PlayerAuto 자동 시험에서 `ui 8.0`, `wait 1`, `shot fxslots-ko-empty`로 초기 화면을 확인한다. 한국어·영어, 작은 화면 배율에서 이름 입력과 단추 폭을 확인한다. 기존 켜진 효과가 많으면 `uiscroll <값>`으로 아래 내 프리셋까지 내려 캡처한다.
3. `fxslot selftest`를 실행하고 `[fxslot] PASS fields=89 ...`를 확인한다. 이 단계가 실제 UMM Settings 타입의 XML 왕복 검증이다.
4. 효과 종류와 수치를 여러 개 바꾼 뒤 아래 명령을 순서대로 확인한다. 이름에는 공백을 쓸 수 있고 따옴표는 필요 없다. `save`는 중복 이름을 덮어쓰지 않는다.

   ```text
   fxslot save 내 테스트
   fxslot list
   fxslot apply 내 테스트
   ```

5. UI에서 모두 끄기→다시 켜기를 여러 번 누르고 켜진 개수·수치·현재 모양이 복원되는지 확인한다. 슬롯 적용·덮어쓰기·삭제, 빈 이름·중복·33자·13번째 저장, 손상 코드 메시지도 확인한다. 이름 변경은 새 이름으로 저장한 뒤 기존 항목을 삭제한다.
6. 이름 칸에서 한글 IME·공백·영문을 입력하고 Insert/설정된 모드 단축키·Ctrl+F5·에디터 단축키가 실행되지 않는지 확인한다. Enter/Escape, 탭 전환, 창 닫기 후 입력이 정상 복구되는지 확인한다. 플레이 화면에서도 판정·일시정지 키와의 상호작용을 별도로 확인한다.
7. 실제 UI로 슬롯을 저장한 후 게임을 정상 종료하고 새로 실행한다. `fxslot list`와 UI 목록에서 유지되는지, 이전 실행에서 모두 끄기로 저장한 복원도 유지되는지 확인한다. 적용 후 실제 화면을 캡처한다. 맵별 저장 켬/끔에서 슬롯 목록이 전역으로 유지되는지도 확인한다.
8. 필요하면 `fxslot delete 내 테스트`로 시험 슬롯을 지운다. 모드 다시 불러오기/언로드 뒤 입력 차단 패치가 남지 않는지도 확인한다.

문서는 `apply_patch`로 UTF-8 BOM을 포함해 작성했다. 작성 후 BOM 바이트 `EF BB BF`, 엄격 UTF-8 디코딩, 한글 제목·문구 보존, 대체 문자·연속 물음표 깨짐 없음 검사를 모두 통과했다.
