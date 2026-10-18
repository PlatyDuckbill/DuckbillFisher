# ENV-03 수첩 열림·어종 매핑 진단

판정: **0.1.1.0 한섭 참조 빌드·기본 수첩 감지·정적 시트 읽기·2종 ID 대조 통과. 로그인/재로드 경계는 대기**.

후속 0.1.2.0의 실제 수집 비트 읽기와 두 어종 대조 결과는 [수집 비트 검증](FISH-NATIVE.md)에 있다. 아래 구현 설명과 원본 보고서는 0.1.1.0 기준이다.

## 이번 구현

- 사용자가 어류 수첩을 열었을 때 갱신하는 흐름을 우선한다. 공개 IGameGui의 관리 래퍼로 `FishGuide2`와 `FishingNote`의 표시 여부를 Framework.Update에서 250ms 간격으로 확인한다. 창 이름과 래퍼의 IsNull/IsVisible은 설치 DLL 메타데이터에서 확인했다. 아주 짧게 열었다 닫는 동작은 표본 사이에서 놓칠 수 있다.
- 닫힘→열림 또는 Plugin 로드 시 이미 표시된 첫 표본에서 정적 FishParameter 목록을 준비한다. 캐릭터 준비가 늦으면 준비 후 갱신한다. 기존 캐시가 있으면 재사용하며 **어종 목록 새로 읽기**는 실제 시트를 다시 읽는다.
- 로그인 시점 갱신은 기본 꺼짐이다. **로그인 준비 후에도 갱신**을 켜면 현재 준비된 세션과 이후 로그인 준비 시점에 갱신한다. 옵션은 이번 진단 세션의 메모리에만 보관한다.
- 공개 IsLoggedIn/IsLoaded와 어종 행 수, IsInLog 행 수/최대 ID, 어종 검색과 선택한 행의 FishParameter ID·참조 타입·아이템 ID·이름을 표시한다. Item/EventItem/참조 불명 상태를 구분한다.
- 게임 API와 시트 읽기는 Framework.Update에서 수행한다. UI는 요청을 전달하고 관리 객체를 표시한다. 전체 어종 검색은 입력/목록/세션 변경 시에만 수행한다. 표시 목록은 30종으로 제한한다.
- 수집 여부는 모두 **Unknown**이다. 네이티브 수첩 비트는 읽지 않으며 IsLoaded가 수첩 수신 완료를 의미한다고 해석하지 않는다. unsafe는 계속 꺼져 있다.
- 로그인 해제·준비 상태 상실·캐릭터 변경 때 이전 표시/갱신 계기를 비운다. 정적 캐시는 재사용할 수 있다. ContentId는 변경 감지에만 사용하고 UI/보고서/파일에 출력하지 않는다.
- 최근 80행의 상태/표시 전이만 메모리에 유지한다. Dispose에서 Framework.Update를 해제한다. 파일·네트워크·채팅으로 자동 출력하지 않는다.

## 실행한 빌드

사용자가 기존 Plugin의 언로드를 확인한 후 Windows PowerShell 7.6.7에서 실행했다.

```powershell
pwsh -NoLogo -NoProfile -File scripts/build.ps1 `
  -DalamudPath "$env:APPDATA\XIVLauncherKR\addon\Hooks\dev"
```

- SDK 10.0.402, KR Dalamud 15.0.3.6 참조.
- Core 테스트 15개 통과, 실패/건너뜀 0.
- Debug Plugin 컴파일 경고 0 / 오류 0, manifest 검사 성공.
- manifest InternalName DuckbillFisher, API 15, AssemblyVersion **0.1.1.0**.
- 출력: `${env:DUCKBILL_ROOT}\src\DuckbillFisher.Plugin\bin\Debug\DuckbillFisher.dll`.
- 빌드·테스트 성공은 수첩 인게임 동작의 검증 결과가 아니다. 기존 Core 테스트에 새 UI/어댑터 검증이 포함됐다고 해석하지 않는다. 실제 수첩 읽기·정밀 타이머는 미구현이다.

## 인게임 재현 순서

1. 위 경로의 새 DLL을 개발 플러그인으로 로드하고 `/duckbillfisher`를 연다. **수첩 연동 확인** 탭이 보여야 한다. 기존 관찰 기능은 **낚시 상태 관찰** 탭에 있다.
2. 게임의 어류도감을 열고 1초 정도 유지한다. 표시가 열림으로 바뀌고 갱신 계기에 `FishGuide2`가 들어가는지 확인한다. 게임의 낚시 수첩도 따로 열어 `FishingNote`를 확인한다. 실제 메뉴/이름이 다르면 그대로 기록한다.
3. 두 창을 닫았다 다시 열어 갱신 횟수가 늘어나는지 확인한다. 플러그인 창만 닫았다 다시 열 때는 수첩 갱신이 추가되지 않아야 한다.
4. 수집된 어종 1종과 미수집 어종 1종을 게임 수첩에서 직접 확인한다. 이 탭에서 각각 이름으로 검색·선택 후 **수첩 진단 복사**를 누르고 보고서와 실제 수집 여부를 함께 제공한다. 보고서 자체의 Collection은 계속 Unknown이어야 한다.
5. **어종 목록 새로 읽기**로 재조회한다. 시트 실패 시 Unknown과 오류 종류가 표시되고 이전 성공 목록을 현재 값처럼 남기지 않아야 한다.
6. 가능한 경우 로그아웃·다른 캐릭터 로그인 후 이전 선택과 표시가 사라지는지 확인한다. 기본 옵션에서는 수첩을 다시 열 때 갱신한다. 로그인 갱신 옵션을 켠 경우에는 준비 후 갱신되는지 따로 확인한다.
7. 언로드/재로드 후 관찰·갱신이 중복되지 않는지 확인한다. 이미 열린 수첩의 첫 표본은 `로드 시 이미 표시됨`으로 기록할 수 있다.

보고서 형식은 `ENV-03/fish-guide-public/v1`이며 Plugin **0.1.1.0**이 포함돼야 한다. 캐릭터명/ContentId는 담지 않는다. 새 탭이 없거나 버전이 다르면 이전 DLL 경로를 확인한다. 오류가 있으면 `/xllog`의 DuckbillFisher 관련 오류를 함께 확인한다.

## 결과

- 사용자 보고서: Plugin 0.1.1.0 / Dalamud 15.0.3.6, Session 1, IsLoggedIn=True / IsLoaded=True, 로그인 갱신 옵션 False.
- `FishingNote` 3회와 `FishGuide2` 3회 표시를 감지했고 각 표시 표본마다 갱신이 1회씩 기록됐다. 총 열림 표본 6 / 갱신 6. 이 보고서에서 중복 갱신은 보이지 않는다. 재로드 후 중복 여부의 검증은 아니다.
- 실제 KR FishParameter **2,512행**, IsInLog **1,518행**, 최대 IsInLog RowId **1,522**. 설치 수집 비트 getter에 전달되는 경계 상수 1,522와 대조할 근거를 확보했다. Get/TryGet의 경계 차이는 [API 조사](FISH-API.md)를 따른다.
- 최초 보고서에는 Selected 줄이 없었다. 후속 보고서로 황금나비어 FishParameter 194 / Item 7685 / IsInLog=True, 홍룡 696 / Item 24993 / IsInLog=True를 확인했다. 사용자는 전자를 수집, 후자를 미수집으로 확인했다. 이는 기준 정답이며 0.1.1.0이 수집 상태를 자동 판정했다는 뜻은 아니다.
- Plugin이 이미 로그인·준비된 상태에서 로드됐다. 로그인 직전/직후 준비 시점, 캐릭터 전환, 수동 재조회 실패, 로그인 옵션, 언로드/재로드는 이번 보고서로 확인하지 않았다.
- 로그인·수첩 전후의 수집 비트 준비 상태: 이번 진단만으로 확정할 수 없음. 후속 네이티브 어댑터 검증에서 별도 확인.

### 사용자 제공 원본 보고서

```text
DuckbillFisher ENV-03/fish-guide-public/v1
Plugin: 0.1.1.0; Dalamud: 15.0.3.6
Session: 1; sampled: True; IsLoggedIn=True; IsLoaded=True
FishGuide2=False; FishingNote=False; loginFallback=False
Open samples: 6; refreshes: 6; trigger: 수첩 열림: FishGuide2
FishParameter rows: 2512; IsInLog rows: 1518; max IsInLog RowId: 1522
어종 목록 읽기 완료 · 수집 여부 Unknown (확인 전)
Collection=Unknown: caught-fish bits were not read. IsLoaded does not establish fish-log readiness.
Visibility sampled every 250ms via public APIs. Times are monotonic seconds since plugin load.
+0.007s 캐릭터 준비: 새 세션
+0.007s 상태: IsLoggedIn=True, IsLoaded=True
+0.007s FishGuide2: 닫힘 (첫 표본)
+0.007s FishingNote: 닫힘 (첫 표본)
+22.514s FishingNote: 표시
+22.517s 갱신 #1: 수첩 열림: FishingNote; 전체 2512 / IsInLog 1518
+29.247s FishGuide2: 표시
+29.247s 갱신 #2: 수첩 열림: FishGuide2; 전체 2512 / IsInLog 1518
+30.014s FishGuide2: 닫힘
+30.797s FishingNote: 닫힘
+32.914s FishingNote: 표시
+32.914s 갱신 #3: 수첩 열림: FishingNote; 전체 2512 / IsInLog 1518
+33.697s FishGuide2: 표시
+33.697s 갱신 #4: 수첩 열림: FishGuide2; 전체 2512 / IsInLog 1518
+34.981s FishGuide2: 닫힘
+36.281s FishingNote: 닫힘
+37.064s FishingNote: 표시
+37.065s 갱신 #5: 수첩 열림: FishingNote; 전체 2512 / IsInLog 1518
+38.097s FishGuide2: 표시
+38.098s 갱신 #6: 수첩 열림: FishGuide2; 전체 2512 / IsInLog 1518
+39.898s FishGuide2: 닫힘
+40.414s FishingNote: 닫힘
```

API 계약과 고정 소스 근거는 [수첩 API 조사](FISH-API.md)에 있다. 표시 감지에 사용한 공개 서비스는 [IGameGui](https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IGameGui/)이며, 최신 웹 문서만으로 한섭 메서드 존재를 판단하지 않고 설치 DLL 시그니처와 컴파일로 대조했다.
