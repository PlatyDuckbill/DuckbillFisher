# 게임 도감 목록과 대조한 자동 미수집

날짜: 2026-10-16 · Plugin 0.1.9.0 · 전체 목록 대조/홍룡 자동 미수집 필터 확인

## 조사 결과와 판정 기준

- 별도의 캐릭터 수첩 수신 완료 플래그는 확인하지 못했다. FishingNote.LoadStates의 Ready는 ExcelSheetWaiter 로딩 상태이므로 수신 완료 근거로 사용하지 않는다.
- 대안은 실제 게임 도감의 표시 상태를 읽는 것이다. AgentFishGuide.Data.Entries는 FilteredEntries와 분리된 목록이며 각 행에 FishParameter ID/Item ID/IsCaught가 있다.
- 현재 한섭 설치 DLL의 구조와 고정 소스를 대조했다. [AgentFishGuide 원본](https://github.com/aers/FFXIVClientStructs/blob/cae2c3755ef02f9f2728082ae8043669ac9fbfe3/FFXIVClientStructs/FFXIV/Client/UI/Agent/AgentFishGuide.cs), [FishingNote 원본](https://github.com/aers/FFXIVClientStructs/blob/cae2c3755ef02f9f2728082ae8043669ac9fbfe3/FFXIVClientStructs/FFXIV/Client/Game/UI/FishingNote.cs). 각 원본은 조사 캐시에만 보관했다. 구현은 확인한 필드 계약을 사용하며 원본 코드 전문은 포함하지 않는다.
- 소스 revision은 cae2c3755ef02f9f2728082ae8043669ac9fbfe3이며 설치본의 dirty 표기를 포함한 SHA/MVID 가드는 기존 [네이티브 근거](ENV-03-FISH-NATIVE.md)를 그대로 유지한다.
- `Complete`는 아래 조건을 통과한 게임 도감 표시 상태다. 서버 패킷 수신 완료를 직접 관찰했다는 뜻은 아니다.

## 적용 조건과 실패 동작

1. 정확한 게임 빌드/DLL SHA/MVID, Framework 스레드, 준비된 같은 캐릭터, 표시 중인 FishGuide2가 필요하다.
2. 공개 IGameGui.FindAgentInterface(addon)에서 해당 창의 에이전트 주소를 얻는다. 별도의 Agent.Instance 호출·후크·시그니처 탐색·게임 입력은 없다.
3. Mode=0(낚시), Data의 부모 에이전트 일치, IsFilteredEntriesLoaded=True를 요구한다. 창 표시나 이 플래그 하나만으로 미수집을 확정하지 않는다.
4. vector의 시작/끝/용량 정렬과 최대 4096행 경계를 확인한다. 대상 1517종의 ID·아이템 연결이 누락/중복 없이 모두 있어야 한다.
5. 모든 대상 행의 IsCaught와 기존 PlayerState.IsFishCaught 결과가 일치해야 한다. 같은 세션에서 이전에 확인한 수집됨이 미수집으로 되돌아가는 경우도 거부한다.
6. 대조 실패는 미수집으로 처리하지 않는다. 자동 미수집 집합을 비워 Unknown으로 되돌리고 기존 수집됨과 수동 기록은 유지한다.
7. 정상 표본은 포인터를 버리고 복사한 ID 집합만 세션 메모리에 보관한다. 도감을 닫아도 마지막 정상 결과를 유지한다. 재개방/열린 동안 1초마다 다시 대조한다.
8. 신규 등록은 Caught에 추가하고 Uncaught에서 제거한다. 로그아웃/준비 상실/캐릭터 변경은 두 자동 집합을 비운다. 수동 지정이 계속 우선한다.

네이티브 포인터 오류를 try/catch로 복구할 수 있다고 가정하지 않는다. 데이터 정합성 검사는 버전·객체 수명 가드를 대체하지 않는다.

## 구현과 진단

- 기존 어댑터에 ReadGuideSnapshot을 격리했다. UI/설정은 복사된 상태만 사용하며 자동 관찰은 저장하지 않는다. Core/설정 스키마는 변경하지 않았다.
- 미수집만 보기에도 자동 미수집이 적용되며 미확인은 계속 구분한다. 한영 자동 미수집/대조 안내를 추가했다.
- 짧은 진단을 유지하며 collection/v2에 Uncaught·GuideState·GuideEntries·Matched·Mismatches를 추가했다. AutomaticUncaught=MatchedGameGuideSnapshot은 적용 정책이고 GuideState가 현재 표본 상태다.
- 정상 기대: GuideState=Complete, Matched=1517, Mismatches=0. 다른 결과이면 짧은 진단으로 원인을 조사하며 미수집을 강제로 확정하지 않는다.

## 실행 결과

- 별도 출력 경로 Debug 빌드: 경고·오류 0. 한영 리소스 174키/형식 인자 검사 통과.
- `scripts/inspect-fish-guide-snapshot.ps1`로 설치 DLL의 AgentFishGuide/FishGuideDataBase/FishGuideListEntry 3개 레이아웃을 대조했다. 게임 메모리는 읽지 않았다. 재현 예:

```powershell
pwsh -NoLogo -NoProfile -File scripts/inspect-fish-guide-snapshot.ps1 -DalamudPath C:/Users/hyogu/AppData/Roaming/XIVLauncherKR/addon/Hooks/dev -OutputPath .cache/guide-snapshot-metadata.json
```

- 사용자 언로드 확인 후 정규 Debug 빌드도 경고·오류 0. 실제 주 DLL/manifest의 0.1.9.0, Core DLL과 한국어 리소스 DLL을 확인했다.
- 전체 1517행의 표본 성립과 홍룡 자동 미수집은 아래 사용자 결과로 확인했다. 신규 등록 시 미수집 제거·낚시/작살 전환·재접속·캐릭터 격리·한영 전환 조합은 추가 확인 대기다.

## 사용자 결과

- 사용자 ‘정상 동작함’과 summary/v1 보고서로 확인했다. 수동 Caught/Uncaught는 모두 0건이다.
- GuideState=Complete, GuideEntries=1517, Matched=1517, Mismatches=0. 자동 Caught=739와 Uncaught=778의 합이 대상 1517종과 일치한다.
- SelectedFish=696/Item=24993/Collection=Uncaught, CollectionFilter=UncaughtOnly/Displayed=1로 홍룡의 자동 미수집과 필터 적용을 확인했다. 먼바다 제외와 상세 조건 필터 해제 상태다.
- GuideVisible=False에서도 마지막 정상 조회의 미수집 집합과 홍룡 선택이 유지됐다. 이번 보고서의 등록 이벤트는 0건이므로 0.1.9.0의 등록 후 미수집 제거를 검증한 것은 아니다.
- 이번 결과 반영은 문서만 변경했으며 DLL·빌드·테스트를 변경/실행하지 않았다. 이미 확인한 기본 판정의 반복 로그는 요청하지 않는다.

## 기본 판정 확인 절차 (위 결과로 통과)

1. 기존 DLL 경로로 0.1.9.0 재로드 → 게임 어류도감의 낚시 탭을 열고 2초 대기.
2. 플러그인에서 수동 기록이 없는 홍룡(696)을 검색해 ‘미수집 (자동)’인지 확인. ‘상세 조건 있는 어종만’은 끈다. ‘미수집만’에서도 남아야 한다.
3. 가능하면 미수집만 필터에서 기존 수집 어종 황금나비어가 제외되는지 확인한다. 전체/이름 검색 필터 조건을 함께 고려한다.
4. ‘짧은 진단 복사’ 결과 하나와 홍룡의 표시 결과를 보낸다. 미확인이어도 같은 짧은 진단을 보내며 전체 로그는 필요 없다.

## 다음 확인: 세션 초기화와 재조회

- 0.1.9.0을 로드한 채 로그아웃 후 같은 캐릭터로 재접속한다. 플러그인 재로드는 별도 초기화를 일으키므로 이번 절차에서는 게임 로그인만 전환한다.
- 도감 조회 전 짧은 진단: Session은 직전과 달라지고 자동 Caught=0/Uncaught=0/Scans=0/GuideState=NotSampled여야 한다. 이전 선택은 비워지고 정적 Rows=2512는 유지된다. 미수집만 필터의 빈 목록은 정상이다.
- 이어 낚시 탭을 열고 2초 후 짧은 진단: GuideState=Complete/Matched=1517/Mismatches=0으로 새 대조가 이뤄져야 한다. 어획 변화가 없으면 기존 739/778종으로 복구되는 것이 기대 결과다.
- 코드 검토: FishPreferences가 로그인·준비 상태와 ContentId 변경을 먼저 처리하고 Session을 갱신한다. FishCollectionObserver는 Session 불일치 시 두 자동 집합과 관찰 카운터를 초기화한다. UI도 세션 불일치의 관찰을 사용하지 않으며 이전 선택을 비운다. 이는 코드 확인이며 실제 경계 통과를 뜻하지 않는다.
- 같은 캐릭터 결과를 받은 뒤, 다른 캐릭터가 가능하면 동일 절차로 이전 관찰이 섞이지 않는지 확인한다. 이름/ContentId는 보고서에 필요 없다. 캐릭터별 결과의 수가 같다는 이유만으로 누출/격리를 판정하지 않는다.
- 현재 상태: 사용자 결과 대기. DLL·설정·게임 동작을 변경하거나 빌드·테스트를 실행하지 않았다.
