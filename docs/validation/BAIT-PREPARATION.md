# 미끼 준비 · 0.2.10.0 개발 확인

기록: 2026-10-18. 사용자 요청은 미끼 준비·찾기 구현과 이번 배포까지다. 현재 준비 목록/수량 표시와 읽기 전용 미끼 UI 진단을 정규 DLL에 반영했다. 사용자가 소지 수량 일치를 대표 확인했다. 첫 UI 보고서는NotOpen이었고20초 대기 수집v2로 바꿔 표본을 다시 요청했다. 강조는 미구현이다. 기능 전체 완료/Release 검증/원격 게시로 해석하지 않는다.

## 구현한 범위

- 메인 ‘어종 목록’ 필터 옆 ‘미끼 준비’ 버튼으로 목록을 연다. 준비 대상은 기본 즐겨찾기 전체/현재 메인 목록의 검색·필터 결과 전체다. 화면에 보이는 행 수로 제한하지 않는다.
- `CatalogQuery`는 기존 메인 검색/진단/일반 필터 판정을 공유한다. UI는 불변 쿼리를 게시하고 준비 서비스가 Framework에서 변경 때만 집계한다. 수집/수동 우선·현재 지역·지역/확장팩·등급 등 기존 의미를 유지하며 알림 정책을 바꾸지 않는다.
- 각 검증된 미끼 경로의 첫 실제 미끼를 ID별로 모으고 관련 목표를 펼쳐 본다. 첫 단계의 대체 후보는 모두 표시하되 모두 필수라고 표현하지 않는다. 첫 미끼가 어종으로 남은 경로/자료 누락/먼바다는 준비 완료로 계산하지 않고 미확인 목표 목록에 둔다. 중간 생미끼를 가방 부족으로 표시하지 않는다.
- `BaitStockObserver`는 공개 IGameInventory로 Inventory1~4만500ms 간격으로 복사한다. 빈 span/잘못된 슬롯·컨테이너/미로그인/지역 전환/캐릭터 불일치/미지원은 Unknown이다. 조회가 완료된 집합에서 없는 Item만0개다. 집사·초코보 가방·충분 수량 기준은 제외한다.
- 소지 수량은 세션 메모리이며 캐릭터 변경/로그아웃에 비우고 초기 준비 유예 후 다시 조회한다. 게임 주소/borrowed span을 보관하지 않는다. 세션 일치 상태만 UI에 표시한다.
- 상세에서는 첫 미끼 이름 옆에 소지 수량/—만 추가한다. 기존 미끼 아이콘·이름의 아이템 링크와 생미끼 경로 안내는 유지한다. 준비 목록에서 ‘없는 미끼만’을 선택할 수 있으며 조회 불가 상태를0으로 필터링하지 않는다.
- 개발자 도구에서만 명시적으로 `BaitUiProbe`를 요청한다. v2는20초간 ‘FishingBait’/‘FishBait’의 표시를 기다렸다가 Framework에서 숫자 AtkValues와 그리드 노드 형태를 제한해 한 번 복사한다. 대기 중에는 복사를 막고 세션 불일치/로그아웃/언로드 때 폐기한다. 문자열/캐릭터 식별자/게임 주소는 보고하지 않는다. 선택·게임 상태/UI 색상을 쓰거나 명령/미끼 변경 함수를 호출하지 않는다.

## KR 설치본과 지원 조합 조사

- 게임 `2026.10.06.0000.0000`, 정확한 wasdcxz15.0.3.6/15.0.3.7 및 dal4kr15.0.3.7 모듈 가드는 변경하지 않았다. 새 기능의 문제를 기존 NativeRuntimeCompatibility에 추가하지 않는다.
- 공개 [IGameInventory](https://github.com/goatcorp/Dalamud/blob/f3eeb5e18bd982d1ae928cbf6dd0653997d3a916/Dalamud/Plugin/Services/IGameInventory.cs), [구현](https://github.com/goatcorp/Dalamud/blob/f3eeb5e18bd982d1ae928cbf6dd0653997d3a916/Dalamud/Game/Inventory/GameInventory.cs), [값 구조](https://github.com/goatcorp/Dalamud/blob/f3eeb5e18bd982d1ae928cbf6dd0653997d3a916/Dalamud/Game/Inventory/GameInventoryItem.cs)를 고정 소스로 확인했다. AGPL3 참조 조사이며 구현 코드를 복제하지 않았다. 공개 서비스도 내부적으로 게임 자료에 접근하므로 정확한 환경·준비 상태를 무시하지 않는다.
- `.cache/preparation-api.json`: 세 DLL의 GetInventoryItems 형태, 일반 가방 enum4개와 GameInventoryItem 크기72, 사용한 값 getter/GetReadOnlySpan의 IL/시그니처 동일 확인.
- `.cache/bait-preparation/layouts.json`: AtkUnitBase/AtkUldManager/AtkResNode/AtkValue/InventoryItem/InventoryContainer 6개 타입의 크기와 인스턴스 필드 이름·타입·오프셋이 세 조합에서 동일했다. Mono.Cecil로 디스크 메타데이터만 읽었다. 이것은 실제 동작 표본을 대체하지 않는다.
- 실제 한섭 `ui/uld/fishingbait.uld`: 컴포넌트11/위젯 노드52, SHA256 `8C804260B75C1CDCBA2FAE03695C032AD8F67AA60BAC52CA26B71E4CB9B1EED7`. 그리드 노드17~41은 ULD에서 확인한 구성이다. 게임 실행 파일의 ASCII 이름에도FishingBait가 존재한다. 단, 첫 사용자 보고서NotOpen의 원인과 런타임 Item ID/항목 연결은 아직 확인하지 않았다. 글로벌 행 순서를 가져와 강조하지 않는다.
- API/ULD 조사 중 UldFile 전체 직렬화가 DataSpan 때문에 실패했으나 필요한 필드만 별도로 직렬화해 조사 완료했다. 리소스 추가 스크립트의 따옴표 구문 오류도 수정했고 최종 빌드는 아래와 같이 통과했다.

## 실행 결과

- 별도 Debug(현재 참조), 최저 참조, 친구 provider-refs, 정규 Debug 빌드: 각각 경고0/오류0.
- `scripts/check-localization.ps1`: 한영584키/형식 인자 일치. 고정 ImGui ID 사용.
- 별도/정규 Plugin·Core·ko·manifest의 파일 해시4/4 일치.0.2.10.0/API15. 데이터/SDK/의존성/기존 설정 스키마 변경 없음.
- 테스트 추가/실행, Release 패키징, push/태그/릴리스/저장소 JSON 변경은 하지 않았다. 공개 JSON의0.2.6.0/API15를 HTTP로 재확인했다.

## 요청한 실제 확인과 다음 단계

정규 주 DLL: `C:\Users\hyogu\Downloads\DuckbillFisher\src\DuckbillFisher.Plugin\bin\Debug\DuckbillFisher.dll`.

1. 일반 소지품에 있는 미끼의 실제 수량과 준비 목록/상세 수량을 비교한다. 즐겨찾기 기본/현재 필터 전체 전환, 스크롤 밖 목표 포함·검색/수집 필터·첫 미끼/대체 후보·없는 미끼만을 대표 확인한다.
2. 사용자 ‘수량 일치’는 대표 확인으로 기록했다. v1의NotOpen을 미끼 UI가 실제로 닫혀 있었다는 증거로 확대하지 않는다. v2는 설정→개발자 도구를 켜고 어종 목록→미끼 준비에서 ‘열린 미끼 UI 자료 수집’을 먼저 누른 뒤20초 안에 게임 미끼 선택 UI를 열어 자동 수집한다. 1초 뒤 ‘미끼 UI 자료 복사’ 결과와 가능하면 UI 사진을 받는다. 개인정보/반복 전체 로그를 요구하지 않는다.
3. 실제 숫자 자료/항목 연결을 확인한 뒤에만 단일 미끼 강조·대기/닫기/취소/만료/캐릭터 격리를 구현한다. 현재 ‘미끼 찾기’가 동작하는 것으로 표현하지 않는다.
4. 구현·자료 대조/리소스 검사·커밋 후 최저 지원 참조로 Release 빌드와 `scripts/prepare-release.ps1`을 수행한다. ZIP 추출본의 대표 인게임 확인을 받은 뒤 정상 push/새 태그/Release/익명 다운로드·해시 확인→설치 JSON 순으로 게시한다. 이미 검증한 Debug를 Release ZIP 실행 확인으로 대신하지 않는다.

배포 목표는 이번에 승인됐지만 미검증 산출물 게시 금지는 유지한다. 남은 일반 데이터0.2.9.0의 대표 화면 확인도 이 새 작업 시작으로 완료 처리하지 않는다.
