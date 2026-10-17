# 미끼 준비 · 0.3.0.0 개발 확인

기록: 2026-10-18. 사용자 요청은 미끼 준비·찾기 구현과 이번 배포까지다. 수량 일치와0.2.10.1/v3 수집·복사는 사용자 대표 확인을 받았다. 새 기능의 최종 버전은 사용자 요청으로0.3.0.0이다. 미끼 찾기 후보를 정규 DLL에 반영했으며 실제 Item 연결·강조는 인게임 검증 대기다. Release 패키지/원격 게시 완료가 아니다.

## 0.3.0.0 · 명시적 미끼 찾기 후보

- 실제 addon 이름은 `Bait`, KR ULD 경로는 `ui/uld/fishingbait.uld`다. v3의 숫자13은 Item ID로 사용하지 않는다. KR ItemUICategory33의188개 유효 미끼 중 아이콘 중복 그룹45개를 확인했으므로 아이콘 이미지로 대상을 추정하지 않는다. 준비 목록의 실제 첫 미끼는93개였다.
- `NativeBaitUiReader`를 Plugin에 격리했다. 공개 GetUIModule/GetAddonByName으로 현재 Framework의 포인터를 받고 KR 도구 설명의 node→Item 계약만 조사한다. 부모 addon ID·Item 도구 설명·DetailKind.Item/ItemId·정확한 Item ID가 일치하는 단일 그리드만 찾는다. InventoryItem 종류의 같은 union 값은 컨테이너이므로 Item ID로 해석하지 않는다. 아직 실제 Bait UI가 이 계약을 사용하는지는 표본으로 확인하지 않았다.
- ULD의17~41번은1005 버튼/44×48이고 내부 노드1~5에 이미지·아이콘·충돌 영역이 있다. 풀 번호를 미끼 순서로 사용하지 않는다. 노드 수/종류/크기·부모·표시/좌표·회전/범위와 map 크기를 제한한다. 빌려온 포인터는 해당 Framework 호출에서만 읽고 managed 좌표와 상태만 전달한다.
- 게임 UI에 쓰거나 선택 함수를 호출하지 않는다. `BaitFinder`는 준비 목록/상세 첫 미끼의 명시적 클릭 하나를 유지하고, 확인된 영역에 ImGui 전경 테두리만 그린다. 오래된 좌표150ms는 표시하지 않는다. 취소/새 요청/60초 만료/세션 변경/로그아웃/지원 실패/한 번 열린 UI 닫기·재생성/검증된 현재 미끼 일치 때 해제한다. 준비 목록 소지 수량을 확인할 수 있고1개 이상일 때만 요청한다. 선택되지 않은 미끼로 확인해야 강조를 볼 수 있다.
- 바인딩 부재·스크롤/숨김·예상 밖 구조는 강조만 중단/유보한다. 기존 지원 프로필과 도감/타이머 가드를 변경하지 않는다. 아이템 링크 클릭과 실제 게임에서의 사용자 미끼 선택을 유지한다. 프로필 밖 환경이나 가방 조회 불가에서 찾기는 비활성이다.
- v4 진단은 `Bait`를 직접 조회하고 초기 표시250ms 뒤 노드의 제한된 도구 설명 연결만 복사한다. 항상 복사 가능한v3 정책을 유지한다. UI 본문·주소·캐릭터 ID는 출력하지 않는다. 연결이 없다면 실제 자료를 받아 이 어댑터만 보완하며 순서/아이콘 추정을 추가하지 않는다.

### 추가 읽기 근거와 실행

- 고정 [UIModule](https://github.com/aers/FFXIVClientStructs/blob/5ecda407c82597890b41f704e087381fc7dfb912/FFXIVClientStructs/FFXIV/Client/UI/UIModule.cs)의 embedded RaptureAtkModule offset `0xD2690`과 [AtkTooltipManager](https://github.com/aers/FFXIVClientStructs/blob/5ecda407c82597890b41f704e087381fc7dfb912/FFXIVClientStructs/FFXIV/Component/GUI/AtkTooltipManager.cs)의 map/ItemArgs 계약을 확인했다. MIT 참조 자료이며 구현을 복제하지 않았다. 내부 필드는 공개 C# 접근이 불가하여 세 정확한 KR DLL에서 확인한 offset 한 곳으로 읽는다. virtual getter/시그니처/새 네이티브 함수 호출은 추가하지 않았다.
- `.cache/bait-preparation/highlight-layouts.json`: UIModule/RaptureAtkModule/AtkModule/AtkStage/tooltip와 중첩 args/component/matrix/map/tree/pair/pointer16종의 모든 인스턴스 필드 offset/형식·크기가 현재/최저/친구 DLL에서 일치했다. 기본 UI/가방6종은 앞선 대조를 유지한다. 공개 listener의 이름 지정 overload4개와 PostSetup=1/PreFinalize=6도 세 조합에서 확인했다. 실제 게임 Item 바인딩이나 친구 실기를 확인한 것은 아니다.
- 최초 컴파일은 wrapper의 내부 Struct 접근, 다음 컴파일은 내부 embedded 필드 접근으로 각각 실패했다. 공개 Address와 위에서 직접 대조한 offset 경계로 수정한 뒤 현재/최저/친구 참조 및 정규 Debug 빌드 모두 경고0/오류0. 한영602키/형식 인자 검사 통과. 후보/정규 Plugin/Core/ko/manifest 해시4/4 일치, manifest0.3.0.0/API15. 테스트 추가·실행 없음.
- 현재 확인 순서: 아직 선택하지 않은 보유 미끼의 ‘미끼 찾기’→사용자가 게임 미끼 UI 열기→정확한 한 칸 테두리/직접 선택 후 해제/닫고 다시 열면 해제 확인. 연결 안내가 나오거나 테두리가 없으면 개발자 진단v4의 BindingState/TooltipBindings 줄만 받는다. 그 뒤 Release 패키지와 추출본 실기 확인을 거쳐 이번 배포를 진행한다.

## 0.2.10.1 · 복사 불가 피드백 대응

- 사용자는 v2 자료 복사 버튼이 활성화되지 않는다고 보고했다. 당시 화면/경과 시간/Framework 처리 상태를 받지 않았으므로 정확한 런타임 원인은 아직 미확정이다. 코드의 `Report is null || IsWaiting` 조건 때문에 대기 상태 자체도 복사할 수 없던 설계는 확인했다.
- v3는 복사를 항상 허용한다. 수집 전/대기/실패/취소에도 Plugin 버전·ProbeBuild·진행 코드·Framework 처리 횟수·경과 시간·관찰된 UI 이름을 복사한다. 실제 숫자 자료가 없는데 수집 성공으로 표시하지 않는다.
- 대기 중 시작 버튼은 중복 재시작을 막고 취소 버튼·남은 초를 제공한다. Framework와 화면 모두에서 관리 시계만으로20초 만료를 적용해 Framework 처리가 멈춰도 대기 종료/진단 복사가 가능하다. UI 표시·복사에서 게임 메모리를 읽지 않는다.
- 공개 IAddonLifecycle의 PostSetup/PostRefresh에서 요청 대기 중인 UI 이름만 제한해 기록한다. 이름에 Bait가 있는 관찰 대상은 현재 Framework에서 다시 조회하고 주소를 저장하지 않는다. 관찰 해제/Dispose와 캐릭터 전환 폐기를 구현했다. 기존 네이티브 가드·도감·타이머·수집/알림 정책 변경 없음.
- 세 참조에서 Register/Unregister 시그니처와 이벤트 enum 상수는 같았다(`.cache/bait-preparation/lifecycle-contracts.json`). AddonName getter 구현은 현재 참조의 저장 문자열 반환과 최저/친구의 수명 안에서 지연 조회하는 방식으로 달랐다. 전체 IL 동일 주장은 철회하고 공개 계약/유효한 PostSetup·PostRefresh 수명으로 사용한다. 처음의 전체 IL 비교는 이 차이로 실패했고 원인을 확인한 뒤 시그니처/상수 대조와 세 참조 빌드를 통과했다. private 필드나 다른 배포의 오프셋을 복제하지 않았다.
- 현재/최저/친구 참조 및 정규 Debug 빌드 경고0/오류0, 한영591키/형식 인자 검사 통과. 정규0.2.10.1의 인게임 복사 동작은 확인 대기다. 테스트·Release 게시 없음.
- 요청한 표본은 같은 주 DLL 재로드→자료 수집→게임 미끼 UI 열기→언제든 ‘미끼 UI 자료 복사’다. `bait-ui/v3`/Plugin0.2.10.1/ProbeBuild로 이전 파일과 구분한다. 버튼이 여전히 비활성이면 버전과 해당 UI 사진만 받아 입력/실제 로드본을 구분한다.

### 받은 실제 v3 표본

사용자0.2.10.1/ProbeBuild9e8d3e1a/Session1: Status=Visible, **Addon=Bait**, Values=8, Nodes=52, Scale=1, Numeric=2:i:13, NumericOmitted=0. 노드17~41이 모두1005/44×48로 관찰됐고 FrameworkPolls=73/Progress=Captured/Pending=False/ObservedAddonNames=Bait였다. v3 수집·복사 대표 확인을 받았다. 앞서 두 이름으로 못 찾던 실제 UI는Bait이며 ULD 이름FishingBait와 구분한다. 숫자 자료에 Item ID가 없으므로13이나 그리드 순서에서 미끼를 추정하지 않는다. 사용자가 새 기능의 배포 버전을0.3으로 올리도록 요청해 다음 구현/최종 배포는0.3.0.0으로 진행한다. 강조는 아직 미구현이다.

## 0.2.10.0/1의 준비 목록·진단 구현

- 메인 ‘어종 목록’ 필터 옆 ‘미끼 준비’ 버튼으로 목록을 연다. 준비 대상은 기본 즐겨찾기 전체/현재 메인 목록의 검색·필터 결과 전체다. 화면에 보이는 행 수로 제한하지 않는다.
- `CatalogQuery`는 기존 메인 검색/진단/일반 필터 판정을 공유한다. UI는 불변 쿼리를 게시하고 준비 서비스가 Framework에서 변경 때만 집계한다. 수집/수동 우선·현재 지역·지역/확장팩·등급 등 기존 의미를 유지하며 알림 정책을 바꾸지 않는다.
- 각 검증된 미끼 경로의 첫 실제 미끼를 ID별로 모으고 관련 목표를 펼쳐 본다. 첫 단계의 대체 후보는 모두 표시하되 모두 필수라고 표현하지 않는다. 첫 미끼가 어종으로 남은 경로/자료 누락/먼바다는 준비 완료로 계산하지 않고 미확인 목표 목록에 둔다. 중간 생미끼를 가방 부족으로 표시하지 않는다.
- `BaitStockObserver`는 공개 IGameInventory로 Inventory1~4만500ms 간격으로 복사한다. 빈 span/잘못된 슬롯·컨테이너/미로그인/지역 전환/캐릭터 불일치/미지원은 Unknown이다. 조회가 완료된 집합에서 없는 Item만0개다. 집사·초코보 가방·충분 수량 기준은 제외한다.
- 소지 수량은 세션 메모리이며 캐릭터 변경/로그아웃에 비우고 초기 준비 유예 후 다시 조회한다. 게임 주소/borrowed span을 보관하지 않는다. 세션 일치 상태만 UI에 표시한다.
- 상세에서는 첫 미끼 이름 옆에 소지 수량/—만 추가한다. 기존 미끼 아이콘·이름의 아이템 링크와 생미끼 경로 안내는 유지한다. 준비 목록에서 ‘없는 미끼만’을 선택할 수 있으며 조회 불가 상태를0으로 필터링하지 않는다.
- 개발자 도구에서만 명시적으로 `BaitUiProbe`를 요청한다.20초간 ‘FishingBait’/‘FishBait’와 공개 이벤트에서 관찰한 Bait UI의 표시를 기다렸다가 Framework에서 숫자 AtkValues와 그리드 노드 형태를 제한해 한 번 복사한다. v3는 대기 중에도 진행 상태를 복사하며 세션 불일치/로그아웃/언로드 때 요청을 폐기한다. UI 본문 문자열/캐릭터 식별자/게임 주소는 보고하지 않는다. 선택·게임 상태/UI 색상을 쓰거나 명령/미끼 변경 함수를 호출하지 않는다.

## KR 설치본과 지원 조합 조사

- 게임 `2026.10.06.0000.0000`, 정확한 wasdcxz15.0.3.6/15.0.3.7 및 dal4kr15.0.3.7 모듈 가드는 변경하지 않았다. 새 기능의 문제를 기존 NativeRuntimeCompatibility에 추가하지 않는다.
- 공개 [IGameInventory](https://github.com/goatcorp/Dalamud/blob/f3eeb5e18bd982d1ae928cbf6dd0653997d3a916/Dalamud/Plugin/Services/IGameInventory.cs), [구현](https://github.com/goatcorp/Dalamud/blob/f3eeb5e18bd982d1ae928cbf6dd0653997d3a916/Dalamud/Game/Inventory/GameInventory.cs), [값 구조](https://github.com/goatcorp/Dalamud/blob/f3eeb5e18bd982d1ae928cbf6dd0653997d3a916/Dalamud/Game/Inventory/GameInventoryItem.cs)를 고정 소스로 확인했다. AGPL3 참조 조사이며 구현 코드를 복제하지 않았다. 공개 서비스도 내부적으로 게임 자료에 접근하므로 정확한 환경·준비 상태를 무시하지 않는다.
- `.cache/preparation-api.json`: 세 DLL의 GetInventoryItems 형태, 일반 가방 enum4개와 GameInventoryItem 크기72, 사용한 값 getter/GetReadOnlySpan의 IL/시그니처 동일 확인.
- `.cache/bait-preparation/layouts.json`: AtkUnitBase/AtkUldManager/AtkResNode/AtkValue/InventoryItem/InventoryContainer 6개 타입의 크기와 인스턴스 필드 이름·타입·오프셋이 세 조합에서 동일했다. Mono.Cecil로 디스크 메타데이터만 읽었다. 이것은 실제 동작 표본을 대체하지 않는다.
- 실제 한섭 `ui/uld/fishingbait.uld`: 컴포넌트11/위젯 노드52, SHA256 `8C804260B75C1CDCBA2FAE03695C032AD8F67AA60BAC52CA26B71E4CB9B1EED7`. 그리드 노드17~41은 ULD에서 확인한 구성이다. 게임 실행 파일의 ASCII 이름에도FishingBait가 존재한다. 단, 첫 사용자 보고서NotOpen의 원인과 런타임 Item ID/항목 연결은 아직 확인하지 않았다. 글로벌 행 순서를 가져와 강조하지 않는다.
- API/ULD 조사 중 UldFile 전체 직렬화가 DataSpan 때문에 실패했으나 필요한 필드만 별도로 직렬화해 조사 완료했다. 리소스 추가 스크립트의 따옴표 구문 오류도 수정했고 최종 빌드는 아래와 같이 통과했다.

## 실행 결과

- 별도 Debug(현재 참조), 최저 참조, 친구 provider-refs, 정규 Debug 빌드: 각각 경고0/오류0.
- `scripts/check-localization.ps1`: 한영591키/형식 인자 일치. 고정 ImGui ID 사용.
- 별도/정규 Plugin·Core·ko·manifest의 파일 해시4/4 일치.0.2.10.1/API15. 데이터/SDK/의존성/기존 설정 스키마 변경 없음.
- 테스트 추가/실행, Release 패키징, push/태그/릴리스/저장소 JSON 변경은 하지 않았다. 공개 JSON의0.2.6.0/API15를 HTTP로 재확인했다.

## 요청한 실제 확인과 다음 단계

정규 주 DLL: `C:\Users\hyogu\Downloads\DuckbillFisher\src\DuckbillFisher.Plugin\bin\Debug\DuckbillFisher.dll`.

1. 일반 소지품에 있는 미끼의 실제 수량과 준비 목록/상세 수량을 비교한다. 즐겨찾기 기본/현재 필터 전체 전환, 스크롤 밖 목표 포함·검색/수집 필터·첫 미끼/대체 후보·없는 미끼만을 대표 확인한다.
2. 사용자 ‘수량 일치’와v3 수집·복사는 대표 확인으로 기록했다. v1의NotOpen을 미끼 UI가 실제로 닫혀 있었다는 증거로 확대하지 않는다. 반복 전체 로그를 요구하지 않는다.
3. 0.3.0.0의 보유한 다른 미끼로 단일 강조·선택/닫기/취소·60초 만료를 확인한다. 새 후보 구현을 실제 동작 확인으로 확대하지 않는다. 실패하면v4의BindingState/TooltipBindings 줄로 실제 Item 계약을 확인하고 해당 어댑터만 보완한다.
4. 구현·자료 대조/리소스 검사·커밋 후 최저 지원 참조로 Release 빌드와 `scripts/prepare-release.ps1`을 수행한다. ZIP 추출본의 대표 인게임 확인을 받은 뒤 정상 push/새 태그/Release/익명 다운로드·해시 확인→설치 JSON 순으로 게시한다. 이미 검증한 Debug를 Release ZIP 실행 확인으로 대신하지 않는다.

배포 목표는 이번에 승인됐지만 미검증 산출물 게시 금지는 유지한다. 남은 일반 데이터0.2.9.0의 대표 화면 확인도 이 새 작업 시작으로 완료 처리하지 않는다.
