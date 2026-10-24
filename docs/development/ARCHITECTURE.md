# 구조와 책임 경계

참조 방향은 **Plugin → Core ← Tests**다. Core는 게임/Dalamud/Lumina/ImGui에 의존하지 않는다. 아래는 현재 구조이며 버전별 도입 경위는 [검증 색인](../verification/README.md)에 있다.

## 코드 탐색

| 책임 | 주요 구현 | 경계 |
|---|---|---|
| 시간/날씨·정렬 | Core FishingAvailability, FishingWindowOrdering | UTC/조건/확률 입력, 게임 API 없음 |
| 수집·지역·등급 판정 | Core CollectionStatus, FishLocationFilter, FishClassification | Unknown/수동 우선·ID 기반 조건 |
| 알림·기록 상태 | Core SpawnAlertScheduler, FishingAttempts | 시각 입력·중복 방지·시전/묶음 모델 |
| 게임 시트·내장 자료 | Plugin FishCatalog, FishingDetails, FishClassificationData | Framework에서 읽고 관리 snapshot 게시 |
| 조건·등급 자료 검증 | Core FishingDatasetReader, Plugin FishingData | 오프라인 도구/런타임 공통 계약·필수값/중복/참조/해시 검사 |
| 캐릭터 설정·필터 | FishPreferences, CatalogSettings, CatalogPreset, CatalogFilter, CatalogQuery | UI 요청 큐/세션 확인/저장·결과 캐시 |
| 수집 관찰 | FishCollectionObserver, NativeFishCollectionReader | 도감 전체 대조와 검증된 등록 로그, 수동 상태와 분리 |
| 수집 현황 | CollectionProgress, CollectionProgressPanel, CollectionProgressFishWindow | Framework 집계·선택한 확장팩/지역/먼바다 배열만 준비, 행·수집 revision·수동 상태·세션 변경 시 재계산. 목록1개 재사용·보이는 행만 렌더링·범위 교체/닫기 때 캐시 해제. Draw는 snapshot 표시 |
| 출현 일정표 | Core FishingForecast.Enumerate/EnumerateCalendar, Plugin SpawnSchedule, SpawnScheduleCalendar, SpawnSchedulePanel | 일반30일/달력32일 분리. Framework 대상 필터링·선택 날짜 회차 캐시, 관리 입력만 취소 가능한 작업에서 계산. 월 데이터는 압축 UTC 구간 배열, UI는 보이는 행/날짜 칸만 표시 |
| 시전 관찰 | FishingTracker, NativeFishingActivityReader, FishingLogContracts | 본인 로그·시전 단계·시간 상관관계, 미검증 결과 Unknown |
| 시전 알림 | Core CastTimerAlerts, CastingTimerNotifications, CastingTimerSettingsWindow | 단조 시간 교차·시전별 알림ID 중복 방지. Framework에서 기존 추적기만 소비하고 선택SE/로컬 채팅 재사용, 화면 닫힘과 독립 |
| 낚시 효과 | FishingBuffObserver | 타이머가 열렸을 때 Framework에서 공개 LocalPlayer.StatusList의850/568·루어3972/3973의Param을200ms 주기로 복사. 루어는 같은 시전의 새1~3중첩만 시작 시각을 알 수 있다. 기존 KR 조합 가드·로그인/캐릭터·어부/이동 확인, 단조 만료·1초 신선도. Draw는 관리 값만 표시 |
| 기록 저장 | FishingHistoryStore, FishingHistoryBrowser | 묶음 파일·원자 교체·요약/상세 지연 읽기·삭제/재시도. schema2 선택 필드로 단조 총 시간/검증된 생미끼 연결 ID 저장 |
| 미끼 준비/찾기 | BaitPreparation, BaitStockObserver, BaitFinder, NativeBaitUiReader, NativeBaitPageReader | 첫 미끼/공개 소지품 조회와 네이티브 UI 읽기 분리 |
| 공통 표시 | FishRowRenderer, FishDetailPanel, FishingConditionPanel, UiWidgets | 표시·선택 전달, 게임 조회/예측을 가져오지 않음 |
| UI 배율 | UiScale, MainWindow, FishPreferences | WindowSystem 그리기에만 글자/스타일 배율 적용 후 finally로 원복. 게임 미끼 UI overlay는 범위 밖 |
| 로컬 출력 | SpawnAlerts, SpawnAlertOutput, FishingMapService, FishingItemLinkService, FishGuideOpenService | 채팅/SE와 명시적 클릭 처리, 서버 송신 없음 |
| 커스텀 WAV | CustomAlertSound, SpawnAlertsPanel | 전용 스레드의 Windows MCI alias/임시 복사본, 관리 요청·세대·상태만 공유 |
| 어종별 개인 기록 | FishPersonalRecord, FishPersonalPanel, FishPreferences | 어종 ID별 공통 메모/수동 횟수/프리셋ID.64개 초안·명시적 저장/적용, 세션/복원 세대·필드별 요청·충돌/실패 보존. 백업 포함 |
| 설정 백업·전용 WAV | CommonSettingsBackup, SettingsBackupFile, SettingsBackup, ManagedSoundFiles, ManagedSoundLibrary | 공통 설정만 버전별 JSON/ZIP 변환·제한/해시 검증. 관리 작업자 파일 처리, Framework 설정 적용·세션/취소·영구 음원 원자 교체/실패 복구. [계약](../product/SETTINGS-BACKUP.md) |
| 업데이트 내역 | ChangelogWindow, MainWindow, FishPreferences | 메인 OnOpen에서 미확인 버전 안내, 내장 한영 릴리스 요약·최대 최신10개 표시, 공통 확인 버전을 기존 Framework 설정 큐로 저장. 원격 조회 없음 |
| 진단 | FishingConditionObserver, FishingActivityProbe, FishGuideDiagnostics, BaitUiProbe | 원시 관찰과 제품 판정 구분, 개발자 도구 |

### 디렉터리와 의존성

- Core는 Alerts/Catalog/Collection/Data/Fishing/Forecast로 나눈다. 게임 API·파일 저장·번역·ImGui를 가져오지 않는다.
- Plugin의 Features는 Catalog/Collection/Fishing/Bait/SpawnAlerts/SpawnSchedule 단위로 화면과 해당 처리를 가까이 둔다. 공통 표시만 UI, 게임 읽기/실행 어댑터는 Game/Native·Contracts·Services, 개발 관찰은 Diagnostics에 둔다.
- Configuration의 FishPreferencesConfiguration은 기존 직렬화 타입, PreferencesValidation은 입력 검증, PreferencesMigration은 파일 접근 없는 표시 설정 변환, PreferencesNormalization은 일반 로딩/백업의 공통 정규화, FishPreferences는 Framework 요청·세션·저장·snapshot 발행을 맡는다. 백업 DTO는 캐릭터/발송/안내 확인 상태를 포함하지 않는다.
- 디렉터리 이동은 namespace/AssemblyName/InternalName과 설정·리소스 이름을 바꾸지 않는다. Localization은 리소스 이름/검사 스크립트의 고정 경로를 유지한다.
- 별도 DI 프레임워크·DB·이벤트 버스를 추가하지 않는다. Plugin은 Plugin.cs에서 직접 조립한다. SDK/버전은 global.json·csproj·lock 파일과 공통 build/DalamudReferences.targets를 기준으로 하며 의존성 업그레이드는 별도 작업이다.

### 상수의 소유권

- Game/Contracts/FishingGameIds는 이미 사용하던 KR 직업·상태·액션 시트 ID만 관리한다. 이름 부여는 지원 게임/DLL 조합 확대가 아니며 네이티브 오프셋·해시·로그 지문은 각 검증 계약에 남긴다.
- 예측 기간/직감 가정/기록 상한은 해당 Core 기능, 설정 허용 범위는 해당 설정 타입, 표본 주기/유효기간은 관찰기에 둔다. UI 배율 선택/정규화는 Configuration/UiScaleSettings를 UI/UiScale과 공유한다.
- 숫자가 같아도 의미가 다르면 합치지 않는다. ET 시간 환산의175초와 미확인 직감 지속 시간 가정175초는 각각 유지한다. 화면 전용 색/간격을 전역 상수로 옮기지 않는다.

## 스레드·시각·수명

- Framework에서 게임 API/포인터를 해당 수명 안에서 읽고 값만 복사한다. 백그라운드 계산/저장은 관리 값만 받는다. 네이티브 포인터를 프레임 밖에 보관하지 않는다.
- Draw는 snapshot 표시와 요청 전달만 담당한다. 파일 읽기·전체 시트 검색·장기 예측을 수행하지 않는다. 목록은 보이는 행 위주 렌더링, 입력/자료 revision 변경 때 검색·필터·정렬 갱신이다.
- 예측/기록 시각은 UTC, 경과/요청 만료는 단조 시계다. ET는 표시/조건 계산을 위한 변환값이다.
- 출현 검색은 호출 시각부터 실제 30일로 제한한다. 경계의 부분 날씨 구간도 포함하되 범위 끝에서 이어지는 출현은 종료 미확인으로 유지한다. 계산/안내/진단은 SearchDays를 공유하며, 기존 절대 시각 캐시와 NotFound의 분 단위 재검색을 유지한다. 날씨 배열은 OR 조건이라 통상 확률표에 없는 대체 날씨 하나가 다른 유효 대안을 막지 않는다.
- UI 요청은 적용 직전 캐릭터 세션을 확인한다. 로그아웃/전환/Dispose에서 관찰·요청·선택·알림 예약·구독을 해당 수명에 맞춰 정리한다.
- 공유 텍스처의 소유권을 가져오거나 borrowed wrap을 Dispose하지 않는다.

## 호환성과 실패

- 사용자 합의(2026-10-23): 공개 API를 우선하고 네이티브 접근을 필요한 범위로 줄인다. 현재의 정확한 KR 게임/DLL 조합 검사를 유지하며, 업데이트마다 사용 계약을 정적으로 대조한 뒤 지원 조합을 추가하고 별도 실기 확인을 받는다. 런타임 계약 지문에 따른 새 DLL 자동 허용은 도입하지 않는다. 반복 대조 도구는 개발용 근거 수집에만 사용한다. [공개 API 대체 조사](../verification/native/NATIVE-RUNTIME-DAL4KR.md#공개-api-우선-방침과-대체-조사-2026-10-23).
- NativeRuntimeProfiles는 검증된 **게임 빌드별·배포판별 세 모듈 조합**의 MVID/SHA256을 관리하고 NativeRuntimeCompatibility는 환경 수집과 판정을 담당한다. 각 DLL은 초기화 때 한 번만 해시를 읽어 후보 조합과 비교한다. 같은 숫자 버전의 다른 배포판을 동일시하거나 개별 허용 모듈을 섞지 않는다.
- 빌드 참조는 `DALAMUD_HOME` 또는 명시한 `DalamudLibPath`로 선택하며 공통 `build/DalamudReferences.targets`가 누락 DLL/API 세대 차이를 검출한다. 컴파일 가능한 SDK/API와 실제 네이티브 지원 계약은 별개다. 새 지역/게임 프로필은 관련 자료·구조·스레드·수명·실기 확인을 마친 뒤 추가하며 현재는 KR 조합만 등록돼 있다.
- 수집/도감 열기/타이머/SE는 검증된 지원 계약을 재사용한다. 미끼 페이지 연결 실패는 페이지 표식만 생략하며 다른 검증 기능을 막지 않는다.
- 가드 실패는 실제 사유를 표시하고 네이티브 읽기를 막는다. 정적 자료·수동 상태를 미수집으로 바꾸지 않는다. [런타임 근거](../verification/native/NATIVE-RUNTIME-DAL4KR.md).
- 단순 Fishing 플래그 관찰기는 어획의 증거가 아니다. FishingTracker는 따로 검증한 본인 로그/단계만 적용한다. Phase 값 하나 또는 Actor=NoSource만으로 성공/실패/본인을 추정하지 않는다.
- 낚시터는 NotObserved/AwaitingIdentification/Unknown/Known/LastKnown으로 구분한다. 새 시전에는 이전 위치를 지우며 Unknown은 본인1110의 KR PlaceName950일 때만 확정한다. 발견1115/1130/3513의 NoSource는 후보일 뿐이며 같은 시전의 검증된 Local3512·2초 상관·Phase2/4·유일한 Territory+PlaceName 대응이 모두 필요하다. 새로운 네이티브 읽기/가드 완화는 없으며 [정적 근거·실기 대기](../verification/fishing/TIMER-HISTORY.md#03110-미지의-낚시터와-이전-위치-분리-2026-10-20)를 따른다.

## 저장·수집·알림

- FishPreferences schema23: 공통 즐겨찾기/필터/언어/UI 배율/간이 목록 전용 즐겨찾기 필터/알림 설정·커스텀 WAV 원본 경로·일정표/업데이트 내역 확인 버전(LastSeenChangelogVersion)·시전 알림/이름 있는 프리셋·입질/터주/터주왕 효과음·낚시터 목록 수집 필터, 캐릭터별 수동 수집/자동 관찰 백업(UTC·상태)/알림 중복 방지 기록. schema1~22 변환과 손상/미지원 원본 보존을 유지한다. 이전 설정은 새 효과음 없음·최초 습득만 켜짐·낚시터 수집 숨김을 기본 적용하고 기존 알림/프리셋을 보존한다. 이전 설정은 간이 목록의 즐겨찾기 선택을 당시 메인 필터에서 한 번 이어받는다. 선택적인 확인 버전만 잘못되면 확인 전으로, 배율이 누락/선택 범위 밖이면100%로 정규화하며 다른 설정을 버리지 않는다.
- PersonalRecords는 최대4,096종의 불변 값이며 필드별 요청을 Framework에서 현재 저장본에 적용한다. 메모/횟수 편집만으로 CatalogRevision을 올리지 않고 같은 snapshot 맵을 재사용한다. 타이머 프리셋 삭제는 연결만 정리하고 명시적 적용은 기존 알림ID/채널/효과음 보존 계약을 따른다. CommonSettingsBackup의 선택 필드로 JSON/ZIP에 포함하며 미저장 초안은 제외한다.
- 캐스팅 알림의 로컬 채팅 채널은 Timer.AlertChannelId로 출현 알림 채널과 분리한다.21 이하 로딩/백업은 종전 채널을 한 번 이어받고 이후 독립 저장한다. 즐겨찾기 관리의 가운데 출현 알림 설정 탭이 기존 메인 설정의 옵션을 담당한다.
- 업데이트 내역을 표시한 뒤 사용자가 닫으면 확인 버전을 저장한다. 저장 실패 시 이전 상태를 유지하고 재로드 때 다시 표시할 수 있다. 캐릭터/언어 전환은 확인 버전을 초기화하지 않고, 더 최신 확인값을 이전 DLL로 낮추지 않는다. 내역 저장만으로 CatalogRevision을 올리지 않는다.
- 전체 Revision과 메모리 전용 CatalogRevision을 구분한다. 목록·타이머 낚시터 필터·미끼 준비는 캐릭터/즐겨찾기/수동 수집/목록 설정/언어 변경에 반응하며 알림 예약 저장만으로 재계산하지 않는다. 같은 즐겨찾기/수동 상태의 불변 집합은 재사용한다.
- 현재 자동 관찰은 세션 메모리이며 로컬 백업과 구분한다. 도감 전체 snapshot 대조가 완료되어야 자동 Uncaught를 확정한다. FishCollectionObserver는 변화한 검증 결과를 FishPreferences에 전달해 전체 일치 시 전체 상태, 부분 관찰 시 수집됨만 보완한다. 실패/빈 조회는 기존 백업을 지우지 않는다. 저장 실패는 마지막 성공본을 유지하고5초 후 재시도한다. 판정은 수동→현재 자동 관찰→저장 백업→Unknown이며 백업 변화도 CatalogRevision/수집 집계를 무효화한다. ContentId는 로컬 키로만 쓰고 진단에 넣지 않는다. [대조·저장 계약](../verification/collection/GUIDE-SNAPSHOT.md).
- 기록 schema 2는 캐릭터별 100묶음×최근 2,000행이다. 총 시전 수/보관 행 수를 구분하고 진행 중 묶음 보호·이전 형식 원본 보존 가져오기를 한다.
- Store는 단일 작업자로 실패 snapshot/삭제 재시도·읽기/쓰기 오류·요약/본문 검증·순번·상세 요청 세대를 관리한다. Draw에서 파일을 열지 않는다. 종료 미처리 건수의 로그 출력만 Plugin.cs에서 주입해 게임 SDK 없이 실제 저장 코드를 검증한다. [저장 경계](../verification/fishing/HISTORY-STABILITY.md).
- 알림은 기존 예측과 최종 수집 판정을 사용한다. 캐릭터별 발송 대상 출현 시작 기록을 저장한 뒤 로컬 출력한다. 로그인/설정 변경/긴 중단/시계 변경은 소급 발송하지 않는다. [예약](../verification/alerts/SPAWN-ALERTS.md), [대상 정책](../verification/alerts/FAVORITE-POLICY.md).
- 알림 계획과 발송은 Scheduler의 같은 재기준화 판정을 공유한다. 시각/세션/활성 상태가 끊기면 새 후보 계산 전에 미래 계획·재시도·순회 위치를 비운다. 새 계산은 최대8종씩 순환하고, 상시는 예산에서 제외하며 기존 예약의 마감은 매번 확인한다.
- 알림 즐겨찾기 표시는 매초 상태를 확인하되 구성/카탈로그 행 변경 때만 이름 정렬하고, 값이 같은 항목/목록은 재사용한다. 소지품도 기존 주기로 검증하되 수량/상태/세션이 같으면 snapshot을 유지하며 재사용 집계 버퍼를 외부에 게시하지 않는다.
- 커스텀 WAV는 Draw/Framework에서 파일을 열거나 재생하지 않는다. 전용 스레드가 세대별 파일 검증/소유 임시 복사본/Windows alias를 관리한다. 요청1개·단조2초 만료·세션 폐기·종료 join을 유지하며, 실패해도 로컬 채팅을 막지 않는다. alerts/v7의 커스텀 접수와 실제 가청 결과를 구분한다. [API/수명/실기 대기](../verification/alerts/CUSTOM-SOUND.md).
- FishingSoundNotifications는 Framework에서 FishingTracker의 현재 입질/확정 어획과 FishCollectionObserver의 검증된 신규 등록 이벤트만 소비한다. 등급은 카탈로그 인덱스에서 조회하고 최초 습득 옵션에 따라 등록/어획 중 한 출처를 사용한다. 세 효과음의 설정/Windows alias는 서로 독립이며 기존 예측 알림 재생기를 변경하지 않는다. 대기 요청은 최대64개·단조2초 만료·세션 일치만 처리하고 종료 때 구독/대기열/작업자를 정리한다. 새 네이티브 읽기·로그 계약·수집 판정은 추가하지 않는다.

## UI·자료 경계

- 설정schema19는 CatalogSettings의 MinimumRemainingMinutes(0해제), nullable MinimumUpcomingMinutes/MaximumUpcomingMinutes(null은 해당 경계 해제)를 공통 필터·프리셋에 저장한다. 이전18이하 최대0만 null로 옮기고 양수·기타 상태는 보존한다. UI의 빈 문자열은 예정 경계null/최소 잔여0으로 즉시 전달하며 복원한 잔여0은 빈칸으로 표시한다. 경계별 범위는 검증하되 최소>최대는 표시 가능한 사용자 충돌 상태로 보존해 설정 전체를 손상 처리하지 않는다. CatalogWindowFilter는 선택 예측/UTC로 Open 종료 최소·Upcoming 시작 최소/최대를 독립 적용하고 미확인은 유지한다. 메인·현재 목록 미끼 준비/일정표는 양쪽 UTC 경계·예측 revision을 추적하며 간이는 기존1초 주기를 사용한다. 즐겨찾기 대상/알림/낚시터 독립 목록은 대상 정책을 유지한다.

- 메인/간이 목록은 CatalogFilter의 일반 조건을 공유한다. 검색/진단/정렬·간이 예정 범위는 각 화면 정책을 따른다. 알림 대상을 목록 필터로 바꾸지 않는다.
- 상세 패널은 메인·독립 상세·비교 상세에서 재사용한다. 비교는1개/뒤로32단계를 유지하며 세션 전환 시 이력을 비운다. 생미끼/직감 어종 탐색은 로드 시 만든 Item/FishParameter 인덱스로 연결하고 Draw에서 전체 재검색하지 않는다. 타이머의 낚시터 목록은 독립 수집 필터와 공통 행을 쓴다. 미끼 준비는 독립 보조 화면이다. [현재 동작](../product/PRODUCT.md#화면과-상호작용).
- 미끼 찾기는 명시적 요청 1개와 유효 기한을 가진다. 전체 Item 배열과 현재 보이는 칸이 모두 일치할 때만 페이지 표식을 만든다. 게임 UI 쓰기/페이지 이동/미끼 변경 호출은 없다. [수명·KR 근거](../verification/bait/BAIT-PREPARATION.md).
- 조건 JSON·분류·고지는 DLL 내장 리소스다. conditionsKnown=false는 예측/알림 제외이며 수집 Unknown과 별개다. Item/FishParameter/Territory/Map/Spot ID를 혼동하지 않는다.
- 조건/등급 DTO와 검증은 Core에 두고 파일/내장 리소스 읽기는 도구/Plugin에서 담당한다. 조건 JSON의 명시적 필수값과 nullable 미확인을 구분한다. 등급은 조건 해시/빌드/버전/양성 목록과 대조한다. 조건 검증 실패는 관련 상세/예측을 중단하고, 등급만 실패하면 등급만 Unknown으로 유지한다. 실패를 빈 정상 자료로 표시하지 않고 타입/코드/경로와 번역된 안내를 제공한다.
- 예측 조건의 날씨 ID가 지역 확률표에 없거나 확률표가 유효하지 않으면 Unknown이다. 유효 조건을 정해진 기간 탐색한 NotFound와 구분한다.
- FishingAvailability는 기본 시간/날씨 구간을 계산하고 FishingForecast는 전체 일반 어종의 관리 값 인덱스에서 직감 선행 조건을 적용한다. 출처 순서의 교집합·인접 연결·지속 시간 연장을 사용한다. 선행 자료 누락/유효성 실패·미지원 중첩은 타입 코드로 Unknown이다. 지속 시간 null은 사용자 요청에 따라175초를 가정하며, 원본은 그대로 둔다. 모든 선행 어종이 상시이면 지속 시간 없이 기본 구간을 사용한다. 현재 자료에는 중첩 직감이 없다.
- FishCatalog가 로드 시 예측 입력을 만들고 Framework.Update에서 일반/물고기의 눈 UTC 구간을 각각 캐시한다. 선택 전환은 캐시 참조와 revision만 바꾸며 예측을 Draw에서 수행하지 않는다. 알림 후속 검색은 같은 FishingForecast의 일반 기준으로 고정한다. 시간제 분류는 원본 기준을 유지한다. 물고기의 눈은 명시적 true인 어종의 ET만 해제하고 날씨·직감 제약과 원본 유효성 검증을 유지한다. [근거](../verification/forecast/FORECAST.md#0340-물고기의-눈과-상세-탐색-2026-10-19).
- 예측 맵은 첫 값 변경 때 한 번 복사하고 변화가 없으면 기존 맵을 재사용한다. ForecastAt은 맵 참조 변경과 무관하게 갱신한다. 카탈로그 재로드는 빈 맵에서 시작하여 이전 키가 남지 않는다. 미끼 준비의 어종 ID 집합도 카탈로그 행 수명에 맞춰 유지한다. [반복 생성 개선](../verification/catalog/UI.md#0370-변경-없는-데이터의-재사용-2026-10-19).
- FishingWindow의 Basis/Issue와 직감 Breakdown은 관리 값이다. 대상/선행 어종의 절대 구간·계산 지속 시간·DurationAssumed를 보관하고 표시 시 번역한다. 기본 접힘 표는 이 값만 렌더링한다. 진단 catalog/v9·summary/v10은 원본/계산 지속 시간과 가정 여부를 구분한다. 기존 실패 코드와 물고기의 눈 지원 미확인 코드는 유지한다.
- 조건 자료의 원본 revision/해시·라이선스·가공은 [data/README](../../data/README.md), 표시와 번역은 [LOCALIZATION](LOCALIZATION.md)을 따른다.

테스트는 명시 요청 때 실행한다. Core.Tests는 Core 프로젝트를 참조하고, 게임 의존성이 없는 Plugin 정책/저장 파일만 소스 링크로 함께 컴파일한다. 실제 구현을 검사하며 가짜 Dalamud 타입이나 구현 복사본을 만들지 않는다. 네이티브·게임 관찰·렌더링과 Dalamud 설정 직렬화는 별도 빌드/실기로 확인한다. [범위와 결과](../verification/ARCHITECTURE-TESTS.md)·[실행 절차](WORKFLOW.md#요청한-테스트-실행)를 따른다.
