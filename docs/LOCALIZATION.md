# 문자열과 다국어 유지보수

적용: 0.1.3.0. 작업 규칙은 AGENTS.md의 '다국어와 문자열', '유지보수', '작업 완료와 커밋'을 따른다.

## 리소스와 표시

- `src/DuckbillFisher.Plugin/Localization/Strings.resx`: 기본 영어 리소스.
- `Strings.ko.resx`: 한국어 번역. 현재 각 파일에 84개 키가 있다.
- `TextKey`: 코드에서 사용하는 키를 제한하는 enum.
- `UiText`: ResourceManager 조회·형식 인자·언어 선택·고정 ImGui ID를 담당한다.
- `UiMessage`: 번역된 문자열 대신 키와 인자를 보관한다. 캐시된 상태/실패 이유와 로그는 표시 시 번역한다.
- `LocalizedLogEntry`: 경과 시각과 메시지를 보관한다. UI는 현재 언어, 보고서는 invariant 영어와 메시지 키로 출력한다.

UI는 한국어로 시작하며 상단에서 English/한국어를 선택할 수 있다. 선택은 이번 Plugin 세션의 메모리에만 유지한다. 호스트의 CurrentCulture/CurrentUICulture는 변경하지 않는다. ResourceManager의 문화권 fallback을 사용하므로 ko-KR은 ko로, 미지원 언어 또는 누락 번역은 기본 영어로 돌아간다.

```csharp
UiText.Get(TextKey.GuideRows, total, inLog, maxId);
new UiMessage(TextKey.RowsReadError, exceptionType);
UiText.Label(TextKey.ReloadFish, "ReloadFish");
```

문장 전체와 자리 표시자를 리소스에 둔다. 위젯의 `###` 뒤 고정 ID는 번역하지 않는다. 어종 이름은 게임 시트에서 읽은 실제 이름을 유지한다. UI 언어 변경은 게임 데이터 언어나 지원 서버를 변경하지 않는다. Plugin 이름·게임 API 식별자·원시 상태 코드도 번역 대상이 아니다. 설치 manifest의 설명은 런타임 UI 리소스와 별개의 빌드 메타데이터다.

## 메시지와 진단

상태/실패 이유에 UiMessage를 보관해 언어 전환이 관찰 기록·선택·수집 비트 표본을 지우지 않게 한다. 번역 문자열을 읽어 조건 분기하지 않는다. 중복 오류 로그는 메시지 키/인자를 비교한다.

수첩 보고서는 `ENV-03/fish-guide-native/v2`다. 필드명과 True/False/Unknown은 고정이며, 메시지는 기본 영어로, 경과 초는 invariant 숫자로 출력한다. 이력에는 `[BitReadLog]` 같은 키를 함께 남긴다. 실제 어종 이름은 시트의 언어를 유지한다. 낚시 플래그 보고서의 수동 메모는 `Marker=MarkCast`처럼 언어와 독립된 코드를 기록한다. 이전 v1 원문은 기존 검증 문서에 보존한다.

## 문구 또는 언어 추가

1. TextKey와 기본 영어 리소스에 새 키를 추가하고 지원 번역 파일에도 같은 키/인자를 추가한다. 표시 코드에서는 UiText 또는 UiMessage를 사용한다.
2. 언어를 추가할 때 `Strings.<culture>.resx`를 만들고 UiLanguage/UiText의 문화권 선택과 MainWindow의 언어 메뉴를 함께 확장한다.
3. `scripts/check-localization.ps1`로 키 누락/중복, 복합 형식 문법, 번역 간 형식 인자 번호를 확인한다. 이 검사는 build.ps1에도 포함된다.
4. 실제 빌드 결과와 인게임에서 언어 변경·선택/기록 유지·길어진 문구의 표시를 확인한다. 기본 언어와 번역의 문장을 바꿔도 게임 접근이나 상태 전이 로직을 바꾸지 않는다.

번역 DLL은 .NET의 satellite assembly다. 한국어는 출력의 `ko/DuckbillFisher.resources.dll`에 담긴다. `scripts/package-files.ps1`가 리소스 파일에서 필요한 언어 DLL 경로를 계산하고, 개발 복사와 배포 준비가 같은 목록을 사용한다. ZIP의 역슬래시는 허용 목록 검사 전에 정규화하고 정규화 후 중복도 거부한다. 리소스 DLL이 빠진 ZIP은 배포 준비를 통과하지 못한다.

## 이번 검증

- Debug build.ps1: 84개 typed key와 한국어 번역의 키/형식 일치, Core 15개 통과, 한섭 참조 Plugin 경고 0 / 오류 0, manifest 0.1.3.0 성공.
- 빌드 DLL을 게임 밖에서 로드해 한국어/영어 문자열 조회, ko-KR/fr-FR fallback, 같은 캐시 메시지의 언어 전환, 진단 문자열 불변, 호스트 문화권 불변을 확인했다. 네이티브 어댑터를 실행한 검사가 아니다.
- Release Plugin 빌드 경고 0 / 오류 0. ZIP에서 한국어 리소스 DLL을 확인했다.
- prepare-release.ps1로 `.cache/localization-release-0.1.3.0`에 로컬 배포 준비 성공. 게시하지 않았다.
- test-release.ps1: 기존 정상/덮어쓰기/HTTPS/런타임 DLL/API/버전/중복 검사와 추가한 번역 DLL 누락·경로 정규화 후 중복 거부 검사 통과.
- 사용자에게 언어 전환 후 선택/표본/기록 유지와 수첩 재개방을 요청했고 0.1.3.0 v2 보고서를 받았다. 보고서에서 황금나비어 선택과 True 표본/기록이 유지됐고, 어류도감 재개방 시 자동 재조회가 확인됐다. 화면 번역/배치에 대한 별도 설명은 받지 않았다.
- 로그인/캐릭터 전환 경계는 미확인이다.

### 0.1.3.0 한섭 보고서 핵심

```text
DuckbillFisher ENV-03/fish-guide-native/v2
Plugin: 0.1.3.0; Dalamud: 15.0.3.6
Session: 1; sampled: True; IsLoggedIn=True; IsLoaded=True
Selected: FishParameter=194; Item=7685; IsInLog=True; name=황금나비어
Raw caught bit: True; [NativeRawSample] Raw caught-bit sample; compare it with the game fish log. Automatic filters do not use this value.
+62.650s [BitReadLog] Caught-bit sample: FishParameter=194; raw=True; Raw caught-bit sample; compare it with the game fish log. Automatic filters do not use this value.
+79.300s [WindowStateLog] FishGuide2: closed
+80.083s [WindowStateLog] FishGuide2: open
+80.083s [RowsRefreshLog] Refresh #4: Fish log opened: FishGuide2; all rows 2512 / in log 1518
+80.083s [BitReadLog] Caught-bit sample: FishParameter=194; raw=True; Raw caught-bit sample; compare it with the game fish log. Automatic filters do not use this value.
```

복사 시에는 두 수첩 모두 닫힌 상태였고 표시된 True는 마지막 표본이다. 닫힌 상태에서 새 비트를 읽었다는 근거로 해석하지 않는다. 보고서는 고정 영어 필드·메시지 키를 사용하며 어종 이름만 실제 게임 시트 언어를 유지했다.

설계 근거: [.NET 리소스와 지역화](https://learn.microsoft.com/en-us/dotnet/core/extensions/localization), [ResourceManager](https://learn.microsoft.com/en-us/dotnet/api/system.resources.resourcemanager?view=net-10.0). 추가 번역 패키지나 외부 코드를 도입하지 않았다.
