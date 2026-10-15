# 수집됨 자동 반영: 수첩 조회와 새 어종 등록

갱신: 2026-10-16. Plugin 0.1.7.0 / KR Dalamud 15.0.3.6 / 게임 빌드 2026.10.06.0000.0000. **빌드·수첩 자동 조회/집계·황금나비어 숨김 확인 / 등록 이벤트·캐릭터 격리 검증 대기**.

## 범위

- 게임 어류도감 `FishGuide2`를 열면 검증 범위의 일반 어종을 조회한다. 열린 동안 1초 간격으로 갱신한다. True 관찰만 자동 수집됨으로 반영한다. False는 수첩 준비 완료를 보장하지 않으므로 Unknown을 유지한다. 자동 미수집 판정은 아직 구현하지 않았다.
- 도감을 닫아도 같은 세션의 수집됨 관찰을 유지한다. 새 어종의 도감 등록 로그를 받으면 수첩을 다시 열지 않고 갱신하도록 구현했다. 실제 이벤트 전달/매개변수와 화면 반영은 인게임 대조가 필요하다.
- 수동 지정이 자동 관찰보다 우선한다. 지정을 해제하면 자동 수집됨 또는 미확인으로 돌아간다. 자동 관찰과 수동 기록은 서로 덮어쓰지 않는다.
- 수집한 어종 숨기기·수집됨만 보기 등에 자동 관찰을 반영한다. 미수집만 보기는 현재 명시적인 수동 미수집에만 해당한다. 일반적인 False를 미수집으로 확정하지 않는다.
- 자동 관찰은 디스크에 저장하지 않는다. 로그아웃·준비 상실·캐릭터 변경 시 세션과 함께 비운다. 재로드 후 수첩을 열어 다시 조회한다. 즐겨찾기/수동 기록은 기존 설정 스키마 1을 유지한다.

## 한섭 근거

개발용 `.cache/collection-audit/` 도구로 현재 게임의 LogMessage 시트를 읽었다. 게임 파일이나 설정을 변경하지 않았다. 설치 Mono.Cecil로 공개 로그 API와 숫자 매개변수 타입을 확인했다.

| 대상 | 확인 내용 |
|---|---|
| `IChatGui.LogMessage` | `OnLogMessageDelegate(ILogMessage)` 공개 이벤트 |
| `ILogMessage` | LogMessageId, ParameterCount, Parameters 제공. 객체는 콜백 안에서만 유효 |
| `SeStringParameter` | IsString, UIntValue로 숫자 매개변수를 구분 가능 |
| KR LogMessage 1114 | 어류도감 등록 문구, `<sheet(Item,lnum1,0)>` 매크로. 첫 숫자 인자를 Item ID로 해석 |
| KR LogMessage 3545 | GatheringSubCategory 참조. 같은 어류도감 문구지만 일반 어종 등록으로 처리하지 않음 |

런타임에서도 1114행의 매크로 전체가 조사한 KR 원문과 같은지 확인한다. 문자열은 사용자 문구/번역 대상이 아닌 입력 계약이다. 맞지 않으면 등록 이벤트 반영만 비활성화하고 수첩 조회는 유지한다. 원시 숫자는 실제 FishParameter→Item의 유일한 매핑과 맞을 때만 반영하며 HQ/다른 ID를 추측해 변환하지 않는다. 이 매개변수 해석은 실제 이벤트에서도 대조해야 한다.

네이티브 수첩 조회는 기존 [어댑터 근거](ENV-03-FISH-NATIVE.md)의 정확한 빌드·DLL 해시/MVID·Framework 스레드·같은 ContentId·IsLoaded·FishGuide2 표시 가드를 유지한다. Resolved·Item 참조·IsInLog·ID 1~1522 조건을 공유한다. 포인터/오프셋·추가 후크·게임 입력은 도입하지 않았다. 수첩 표시/IsLoaded만으로 전체 데이터 준비를 확정한 것은 아니며 다른 캐릭터의 실제 상태 대조는 여전히 필요하다.

## 처리와 진단

- FishPreferences의 현재 세션을 기준으로 관찰을 격리한다. 목록은 서로 다른 세션의 수동/자동 snapshot을 섞지 않는다.
- 로그 콜백에서는 등록 로그의 숫자 Item ID와 세션/단조 시각만 복사한다. ILogMessage·매개변수 객체·메시지/캐릭터 이름은 보관하지 않는다. 최대 64건 큐를 두며 다른 세션 또는 5초 넘은 요청은 폐기한다. Framework에서 250ms 간격으로 처리한다.
- 시트 매핑은 행 변경 시만 갱신한다. 도감 네이티브 조회는 Framework에서 수행하고 Draw에는 넣지 않는다. 수집 집합이 바뀐 경우만 관찰 revision을 올려 필터를 다시 계산한다.
- 네이티브 포인터 오류를 관리 예외 처리로 복구할 수 있다고 가정하지 않는다. 공개 API/시트 조회의 관리 예외 안내는 별도다.
- 목록 진단의 수동 상태 구역은 `MVP-01/preferences/v2`, 새 자동 구역은 `MVP-04/collection/v1`이다. 자동 수집 수·대상 수·조회 횟수·마지막 조회의 False/Unknown 수·등록 이벤트/적용 수·마지막 Item/FishParameter ID·처리 시각/이유를 기록한다. False 개수는 마지막 수첩 조회 표본이며 자동 미수집 수가 아니다. ContentId/이름은 포함하지 않는다.

## 실행한 빌드와 검사

```powershell
dotnet run --project .cache/collection-audit/collection-audit.csproj
pwsh -NoLogo -NoProfile -File .cache/inspect-collection-events.ps1
dotnet build src/DuckbillFisher.Plugin/DuckbillFisher.Plugin.csproj --no-restore -c Debug `
  -p:DalamudLibPath=C:/Users/hyogu/AppData/Roaming/XIVLauncherKR/addon/Hooks/dev/ `
  -p:OutputPath=C:/Users/hyogu/Downloads/DuckbillFisher/.cache/mvp04-collection-build/
pwsh -NoLogo -NoProfile -File scripts/check-localization.ps1
```

- 별도 Debug 빌드와 사용자 언로드 후 OutputPath를 뺀 정규 Debug 빌드: 경고 0 / 오류 0.
- 실제 주 DLL 157696바이트·어셈블리 0.1.7.0, Core/한국어 DLL 존재와 manifest를 확인했다.
- 한국어/영어 리소스 168키와 형식 인자 일치.
- 테스트를 추가/실행하지 않았다. 과거 Core 601개 통과를 이번 자동 수집 동작의 검증으로 표시하지 않는다.
- 원격 게시/배포는 하지 않았다. 자동 수집 판정은 개발 버전의 인게임 검증 대기다.

## 게임 확인 순서

1. 같은 개발 DLL 경로의 0.1.7.0을 로드한다. 목록 필터는 전체, 즐겨찾기/상세 조건 제한은 필요에 따라 해제한다.
2. 황금나비어(194)와 홍룡(696)의 수동 지정을 해제한다. 게임 어류도감을 열고 1초 뒤 황금나비어의 게임 자동 확인이 수집됨인지 본다. 기존 정답과 같다면 홍룡은 미확인이다. 현재 실제 수집 여부가 달라졌다면 게임 수첩을 기준으로 대조한다.
3. 도감을 닫고 수집한 어종 숨기기로 바꾼다. 자동 수집된 황금나비어가 빠지고 미확인 어종은 남아야 한다. 목록 진단에서 Caught/Scans와 RegistrationContract를 확인한다.
4. 가능할 때 어류도감을 닫은 상태에서 아직 도감에 등록하지 않은 쉬운 어종을 실제로 낚는다. 게임의 도감 등록 메시지와 자동 수집 반영/숨김을 대조한다. 이미 잡은 어종의 반복 어획은 신규 등록 이벤트 검증이 아니다. 목록 진단의 RegistrationMessages/AppliedRegistrations/LastLogItem/LastLogFish를 받는다.
5. 수동 미수집 지정은 자동 결과와 별개로 남고, 수동 해제 시 자동 수집됨으로 돌아가는지 확인한다. 로그인/다른 캐릭터 전환 시 이전 자동 집합/등록 대기 요청이 섞이지 않아야 한다. 재로드 후 자동 관찰을 다시 준비하는지도 확인한다.

수첩 조회·수집 숨김·새 등록·세션 격리·언어 전환은 실제 결과를 받은 뒤 각각 판정한다. 습득 이벤트 전달이 다르면 복사한 진단의 ID/숫자만 근거로 보완하며 미수집으로 임의 처리하지 않는다.

## 실제 한섭 보고서: 수첩 자동 조회/집계

사용자가 0.1.7.0 재로드 후 보낸 목록 진단의 핵심 값이다. 긴 원본/미해결 행 목록과 이미 확인한 지도/조건 출력은 반복해서 보관하지 않는다.

```text
Plugin: 0.1.7.0
Ready=True; Editable=True; Session=1; Favorites=1
ManualCaught=0; ManualUncaught=0
CollectionFilter=All; FavoritesOnly=False; Sort=0; Displayed=10
StorageError=none
Session=1; Caught=738; Eligible=1517; GuideVisible=True
Scans=40; RawFalse=779; UnknownReads=0; RegistrationContract=True
RegistrationMessages=0; AppliedRegistrations=0; LastLogItem=0; LastLogFish=0
LastTrigger=GuideScan; SampledAtSeconds=65.472; Status=CollectionPositiveOnly
AutomaticUncaught=Unknown; ObservationsPersisted=False
```

- 수동 기록 0건 상태에서 자동 수집됨 집합 738종이 생성됐다. 738 + 779 = 1517로 조회 대상 전체의 표본 집계가 맞으며 Unknown 조회는 0건이다. False 779종을 자동 미수집으로 전환하지 않는 계약도 보고서에서 유지됐다.
- 도감 표시 상태에서 40회 갱신됐고 등록 로그의 설치본 매크로 계약도 통과했다. 설정 편집 가능/저장 오류 없음과 즐겨찾기 1건을 확인했다.
- 보고서의 필터는 All이었다. 이후 수동 지정을 해제한 황금나비어가 수집한 어종 숨기기에서 사라지는지 물었고 사용자가 ‘사라짐’이라고 응답했다. 이 대표 어종의 자동 수집 상태에 따른 숨김 동작은 통과로 기록한다. 전체 어종별 수집 정답을 대조한 것으로 확대하지 않는다.
- 등록 이벤트 0건은 오류를 뜻하지 않는다. 새 어종 습득·등록이 관찰되지 않은 상태여서 습득 순간 갱신은 아직 미검증이다. RegistrationContract=True를 실제 이벤트 수신 검증과 혼동하지 않는다.
- 다른 캐릭터/재접속·수동 우선순위·언어 전환도 이 보고서로 확정하지 않는다. 이번 반영은 문서 변경이며 빌드/테스트는 재실행하지 않았다.
