# 구조와 책임 경계

참조 방향은 **Plugin → Core ← Tests**다. Core는 게임/Dalamud/Lumina/ImGui에 의존하지 않는다. 아래는 현재 구조이며 버전별 도입 경위는 [검증 색인](../verification/README.md)에 있다.

## 코드 탐색

| 책임 | 주요 구현 | 경계 |
|---|---|---|
| 시간/날씨·정렬 | Core FishingAvailability, FishingWindowOrdering | UTC/조건/확률 입력, 게임 API 없음 |
| 수집·지역·등급 판정 | Core CollectionStatus, FishLocationFilter, FishClassification | Unknown/수동 우선·ID 기반 조건 |
| 알림·기록 상태 | Core SpawnAlertScheduler, FishingAttempts | 시각 입력·중복 방지·시전/묶음 모델 |
| 게임 시트·내장 자료 | Plugin FishCatalog, FishingDetails, FishClassificationData | Framework에서 읽고 관리 snapshot 게시 |
| 캐릭터 설정·필터 | FishPreferences, CatalogSettings, CatalogPreset, CatalogFilter, CatalogQuery | UI 요청 큐/세션 확인/저장·결과 캐시 |
| 수집 관찰 | FishCollectionObserver, NativeFishCollectionReader | 도감 전체 대조와 검증된 등록 로그, 수동 상태와 분리 |
| 시전 관찰 | FishingTracker, NativeFishingActivityReader, FishingLogContracts | 본인 로그·시전 단계·시간 상관관계, 미검증 결과 Unknown |
| 기록 저장 | FishingHistoryStore, FishingHistoryBrowser | 묶음 파일·원자 교체·요약/상세 지연 읽기·삭제/재시도 |
| 미끼 준비/찾기 | BaitPreparation, BaitStockObserver, BaitFinder, NativeBaitUiReader, NativeBaitPageReader | 첫 미끼/공개 소지품 조회와 네이티브 UI 읽기 분리 |
| 공통 표시 | FishRowRenderer, FishDetailPanel, FishingConditionPanel, UiWidgets | 표시·선택 전달, 게임 조회/예측을 가져오지 않음 |
| 로컬 출력 | SpawnAlerts, SpawnAlertOutput, FishingMapService, FishingItemLinkService, FishGuideOpenService | 채팅/SE와 명시적 클릭 처리, 서버 송신 없음 |
| 진단 | FishingConditionObserver, FishingActivityProbe, FishGuideDiagnostics, BaitUiProbe | 원시 관찰과 제품 판정 구분, 개발자 도구 |

파일은 src/DuckbillFisher.Core 또는 src/DuckbillFisher.Plugin에 있다. 별도 DI 프레임워크·DB·이벤트 버스를 추가하지 않는다. SDK/버전은 global.json·csproj·lock 파일을 기준으로 한다.

## 스레드·시각·수명

- Framework에서 게임 API/포인터를 해당 수명 안에서 읽고 값만 복사한다. 백그라운드 계산/저장은 관리 값만 받는다. 네이티브 포인터를 프레임 밖에 보관하지 않는다.
- Draw는 snapshot 표시와 요청 전달만 담당한다. 파일 읽기·전체 시트 검색·장기 예측을 수행하지 않는다. 목록은 보이는 행 위주 렌더링, 입력/자료 revision 변경 때 검색·필터·정렬 갱신이다.
- 예측/기록 시각은 UTC, 경과/요청 만료는 단조 시계다. ET는 표시/조건 계산을 위한 변환값이다.
- UI 요청은 적용 직전 캐릭터 세션을 확인한다. 로그아웃/전환/Dispose에서 관찰·요청·선택·알림 예약·구독을 해당 수명에 맞춰 정리한다.
- 공유 텍스처의 소유권을 가져오거나 borrowed wrap을 Dispose하지 않는다.

## 호환성과 실패

- NativeRuntimeProfiles는 검증된 **게임 빌드별·배포판별 세 모듈 조합**의 MVID/SHA256을 관리하고 NativeRuntimeCompatibility는 환경 수집과 판정을 담당한다. 각 DLL은 초기화 때 한 번만 해시를 읽어 후보 조합과 비교한다. 같은 숫자 버전의 다른 배포판을 동일시하거나 개별 허용 모듈을 섞지 않는다.
- 빌드 참조는 `DALAMUD_HOME` 또는 명시한 `DalamudLibPath`로 선택하며 공통 `build/DalamudReferences.targets`가 누락 DLL/API 세대 차이를 검출한다. 컴파일 가능한 SDK/API와 실제 네이티브 지원 계약은 별개다. 새 지역/게임 프로필은 관련 자료·구조·스레드·수명·실기 확인을 마친 뒤 추가하며 현재는 KR 조합만 등록돼 있다.
- 수집/도감 열기/타이머/SE는 검증된 지원 계약을 재사용한다. 미끼 페이지 연결 실패는 페이지 표식만 생략하며 다른 검증 기능을 막지 않는다.
- 가드 실패는 실제 사유를 표시하고 네이티브 읽기를 막는다. 정적 자료·수동 상태를 미수집으로 바꾸지 않는다. [런타임 근거](../verification/native/NATIVE-RUNTIME-DAL4KR.md).
- 단순 Fishing 플래그 관찰기는 어획의 증거가 아니다. FishingTracker는 따로 검증한 본인 로그/단계만 적용한다. Phase 값 하나 또는 Actor=NoSource만으로 성공/실패/본인을 추정하지 않는다.

## 저장·수집·알림

- FishPreferences schema 11: 공통 즐겨찾기/필터/언어/알림 설정, 캐릭터별 수동 수집/알림 중복 방지 기록. 기존 스키마 변환과 손상/미지원 원본 보존을 유지한다.
- 자동 관찰은 세션 메모리다. 도감 전체 snapshot 대조가 완료되어야 자동 Uncaught를 확정한다. 수동 override가 항상 우선이며 ContentId는 로컬 키로만 쓰고 진단에 넣지 않는다. [대조 계약](../verification/collection/GUIDE-SNAPSHOT.md).
- 기록 schema 2는 캐릭터별 100묶음×최근 2,000행이다. 총 시전 수/보관 행 수를 구분하고 진행 중 묶음 보호·이전 형식 원본 보존 가져오기를 한다.
- Store는 단일 작업자로 실패 snapshot/삭제 재시도·읽기/쓰기 오류·요약/본문 검증·순번·상세 요청 세대를 관리한다. Draw에서 파일을 열지 않는다. [저장 경계](../verification/fishing/HISTORY-STABILITY.md).
- 알림은 기존 예측과 최종 수집 판정을 사용한다. 캐릭터별 발송 대상 출현 시작 기록을 저장한 뒤 로컬 출력한다. 로그인/설정 변경/긴 중단/시계 변경은 소급 발송하지 않는다. [예약](../verification/alerts/SPAWN-ALERTS.md), [대상 정책](../verification/alerts/FAVORITE-POLICY.md).

## UI·자료 경계

- 메인/간이 목록은 CatalogFilter의 일반 조건을 공유한다. 검색/진단/정렬·간이 예정 범위는 각 화면 정책을 따른다. 알림 대상을 목록 필터로 바꾸지 않는다.
- 상세 패널은 메인과 독립 상세에서 재사용한다. 타이머의 낚시터 목록은 독립 수집 필터와 공통 행을 쓴다. 미끼 준비는 독립 보조 화면이다. [현재 동작](../product/PRODUCT.md#화면과-상호작용).
- 미끼 찾기는 명시적 요청 1개와 유효 기한을 가진다. 전체 Item 배열과 현재 보이는 칸이 모두 일치할 때만 페이지 표식을 만든다. 게임 UI 쓰기/페이지 이동/미끼 변경 호출은 없다. [수명·KR 근거](../verification/bait/BAIT-PREPARATION.md).
- 조건 JSON·분류·고지는 DLL 내장 리소스다. conditionsKnown=false는 예측/알림 제외이며 수집 Unknown과 별개다. Item/FishParameter/Territory/Map/Spot ID를 혼동하지 않는다.
- 조건 자료의 원본 revision/해시·라이선스·가공은 [data/README](../../data/README.md), 표시와 번역은 [LOCALIZATION](LOCALIZATION.md)을 따른다.

사용자가 테스트를 요청하면 시간/날씨 경계·중복 알림·상태 전이·캐릭터 격리·저장 실패처럼 실패 비용이 큰 계약에 집중한다. 일반 작업의 빌드/검사와 인게임 확인 절차는 [WORKFLOW](WORKFLOW.md)에 있다.
