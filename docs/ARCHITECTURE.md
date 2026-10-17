# 최소 구조

아래 구조로 시작 프로젝트를 생성했다. 프로젝트명·표시명·InternalName은 `DuckbillFisher`다.

```text
src/
  DuckbillFisher.Core/       # 수집 상태/알림 대상 판정, 관찰 이력; 추후 시간·날씨·타이머
  DuckbillFisher.Plugin/     # 진입점·관찰 UI·공개 API·검증한 KR 수집 비트 어댑터
    Localization/           # .resx·타입화된 메시지·UI 언어 선택
tests/
  DuckbillFisher.Core.Tests/
data/                       # 검증된 원본/정제 JSON, 출처와 라이선스
scripts/                    # 환경/리소스 확인·빌드·공유 패키지 계약·배포 준비/검사
repo/                       # 친구 공유용 설치 목록, 현재 빈 배열
docs/
```

참조 방향: **Plugin → Core ← Tests**. Core에서 Dalamud, Lumina, ImGui, 게임 DLL을 참조하지 않는다. 처음부터 DI 프레임워크·DB·범용 이벤트 버스·다수의 프로젝트를 추가하지 않는다.

Plugin의 `AssemblyName`은 `DuckbillFisher`다. 주 DLL은 `DuckbillFisher.dll`, 생성 manifest는 `DuckbillFisher.json`이다. 최신 샘플 방식에 따라 manifest 입력 필드는 csproj에 두고 SDK가 JSON을 생성한다.

초기 도구: .NET SDK 10.0.402, Core net10.0, Plugin net10.0-windows / x64, Dalamud.NET.Sdk 15.0.0. .NET SDK와 NuGet lock 파일을 고정했다. 빌드 근거는 REFERENCES, 실제 검증은 STATUS를 따른다.

## 경계

| 부분 | 책임 |
|---|---|
| Core | UTC↔ET, 날씨 창 교집합, 미수집 필터, 알림 중복 방지, 타이머 상태 전이 |
| Game adapter | 로그인/지역/시전/수집 상태를 관찰하여 관리 객체의 snapshot/event로 전달 |
| UI | snapshot을 표시하고 사용자 선택을 전달 |
| Storage | JSON 설정 로드/검증/저장, 버전별 migration, 캐릭터별 상태 분리 |
| Notifications/Map | 허용된 로컬 출력, 사용자 클릭에 따른 지도/어류도감 열기 |

인터페이스는 실제 테스트와 어댑터 경계에 필요한 것만 만든다. 공개 API 유무는 ENV-03에서 확인하며 `CastStarted` 같은 개념적 이벤트 이름을 실제 Dalamud API로 오인하지 않는다.

MVP-02/F07·F13의 [화면/보관 합의와 KR 조사](CASTING-TIMER-HISTORY.md)를 따른다. 0.2.0.0 첫 단계의 FishingActivityProbe는 개발자 도구에서 명시적으로 시작한 관찰만 담당한다. NativeFishingActivityReader는 기존 정확한 KR/DLL 가드를 공유해 Framework에서 값만 복사하고, FishingLogContracts는 검수한 25개 KR 로그 macro 해시로 이벤트를 제한한다. 공개 로그·플래그·네이티브 상태를 같은 단조 시간축에 기록하지만 성공/실패/정확한 낚시터를 아직 확정하지 않는다. 기존 수집/알림 서비스와 격리한다. [수명/실기 대기](validation/MVP-02-CASTING-PROBE.md).

0.2.1.0은 위 KR 실제 표본을 근거로 FishingTracker → Core FishingAttempts에 확인한 전이를 전달한다. 별도 FishingHistoryStore가 단일 작업자로 캐릭터별 파일을 읽고 최신 snapshot을 저장한다. CastingTimerWindow/FishingHistoryPanel은 같은 관리 기록을 표시하고 낚시터 팝업은 FishRowRenderer를 재사용한다. 실제 미검증 결과는 Unknown으로 두며 타이머 관찰을 기존 도감/알림 판정에 주입하지 않는다. [저장·수명·성능 경계](validation/MVP-02-TIMER-HISTORY.md).

0.2.2.0은 사용자 승인으로100묶음×최근2000회/schema2 묶음별 원자 파일로 확장한다. Core는 묶음 총 횟수/보관 행을 구분하고, Store는 요약 캐시/선택 상세 지연 읽기/이전schema1 가져오기/종료 기록 정리·삭제를 담당한다. FishingHistoryBrowser가 메인의 요약→상세·확인 삭제를 맡고 타이머는 현재 묶음만 표시한다. UI 요청은 캐릭터 세션과 함께 Framework에서 검증한다. 대표 경과는 낚아채기 관찰에서 멈추고 결과 수신 경과는 별도 보존한다. [수명·실패·확인](validation/MVP-02-HISTORY-MANAGEMENT.md).

0.2.3.0은 본인1121의 Item 후보를2초만 보관하고 시전 시작/생미끼 플래그/Phase1·12와 대조해 시작 미끼를 확정한다. 실패는 KR 계약이 일치한 본인 로그와 활성 시전·Phase2·4가 함께 있을 때만 Failed로 기록한다. 원시 Phase2만으로 실패를 추정하지 않는다. Native 주소/읽기 범위는 바꾸지 않았으며 과거 미확인 기록은 보정하지 않는다. [실제 표본·정적 계약·확인 경계](validation/MVP-02-MOOCH-FAILURE.md).

0.1.33.0의 FishGuideOpenService는 연동 안내의 명시적 클릭을 Framework에서 한 번 처리하고 검증한 KR Agent API로 낚시 탭을 연다. 세션/캐릭터/요청 만료·DLL 가드·구독 해제를 적용하며 도감 준비 판정은 기존 관찰기만 담당한다. [게임 접근 근거](validation/MVP-01-GUIDE-OPEN.md).

사용자 문구는 상태/게임 접근과 분리한다. 상태와 이력은 번역 문자열 대신 UiMessage의 키/인자를 보관하고 UI/진단 출력에서 각각 표현한다. 추가 언어와 패키징은 [다국어 지침](LOCALIZATION.md)을 따른다.

0.1.4.0의 `FishCatalog`는 Framework.Update에서 게임 시트의 이름·아이콘·ID·IsInLog를 읽어 정적 목록을 캐시한다. `FishListPanel`과 `FishGuideDiagnostics`가 이를 공유한다. 목록 탭의 로드/재조회는 요청만 큐에 넣으며 Draw에서 시트를 읽지 않는다. 정적 목록은 로그아웃 후에도 유지할 수 있지만 캐릭터의 비트 표본/선택 추적은 기존 수첩 진단에서 따로 폐기한다. 목록 탭의 행 선택은 정적 어종 선택이며 캐릭터 수집 상태를 포함하지 않는다.

목록의 검색/필터/정렬은 입력이나 원본 목록 변경 때만 갱신한다. 고정 높이 행에 설치본의 ImGuiListClipper를 적용해 보이는 행만 그린다. 아이콘은 설치본 ITextureProvider의 공유 즉시 텍스처로 받아 소유권을 가져오거나 borrowed wrap을 Dispose하지 않는다. 목록 정렬은 게임 데이터 언어인 한국어 비교를 사용하고 동률은 FishParameter ID로 고정해 UI 언어 변경에 따라 순서가 흔들리지 않게 한다.

## 시간과 성능

0.1.36.0의 CatalogPresetsPanel은 팝업 표시/선택만, CatalogPreset은 상한·이름·복사, FishPreferences(schema v11)는 기존 Framework 편집 큐/설정 저장을 담당한다. 이전 v1~v10과 기록을 유지하고 잘못된 선택적 프리셋만 제외한다. presets는 필터+정렬 복사이며 검색·진단은 포함하지 않는다. [저장 경계/인게임 확인](validation/MVP-01-FILTER-PRESETS.md).

- 0.1.7.0의 FishCollectionObserver는 FishPreferences의 현재 세션을 따르는 수집됨 관찰이다. NativeFishCollectionReader를 수첩 진단과 공유하고 기존 가드 안에서 FishGuide2 표시 중 1초마다 True만 수집한다. IChatGui.LogMessage의 검증한 KR 도감 등록 ID/숫자 Item 매개변수도 제한된 큐를 거쳐 Framework에서 적용한다. False는 Unknown이며 수동 override가 자동 관찰보다 우선한다. 자동 관찰은 세션 메모리에만 보관한다. API 근거·등록 입력 계약과 남은 검증은 [자동 수집 검증](validation/MVP-04-COLLECTION.md)을 따른다.

- 0.1.6.0의 FishPreferences는 공개 IPlayerState.ContentId/IsLoaded와 IClientState.IsLoggedIn을 Framework.Update에서 읽는다. 즐겨찾기는 공통, 수동 수집 bool은 캐릭터/FishParameter ID별로 보관한다. UI는 변경 요청만 큐에 넣고 서비스가 적용 직전 세션을 확인해 다른 캐릭터로 넘어간 수동 요청을 폐기한다. 로그인 전/조회 실패에는 수동 상태를 비우고 변경을 막는다. 불변 snapshot의 revision이 바뀔 때만 목록 필터를 다시 계산한다.
- 저장은 공개 IDalamudPluginInterface.GetPluginConfig/SavePluginConfig를 사용한다. 스키마 버전 1이며 손상/미지원 기존 설정은 덮어쓰지 않고 편집을 비활성화한다. 저장 실패 시 직전 메모리 상태를 유지하고 재시도 안내를 표시한다. Draw에서 파일을 읽거나 저장하지 않는다. ContentId는 로컬 설정 키로만 사용하고 복사 보고서에는 넣지 않는다. Core의 CollectionFilter가 수동 우선순위와 표시 필터 의미를 담당하며 원시 수집 비트를 자동 판정에 사용하지 않는다.

- 0.1.5.0의 FishingAvailability는 UTC/조건/날씨 확률만 받는 Core 계산이다. Framework에서 초기 10종을 초당 한 번 예측해 snapshot에 보관하고 Draw에서는 시각 차이만 표시한다. FishCatalog가 낚시터·지도·날씨 확률·내장 조건을 연결하고 FishingMapService는 명시적 클릭 요청만 Framework에서 처리한다. 외부 데이터 JSON과 MIT 고지는 주 DLL 리소스에 내장해 파일/네트워크 읽기를 렌더링에 넣지 않는다. 구조/제약은 [상세 검증](validation/MVP-01-DETAILS.md)을 따른다.
- 0.1.4.1부터 이름/참조의 해석 상태를 FishRowResolution으로 보관한다. 일반 목록은 Resolved 행만 표시한다. ID 0, 빈 참조/이름, 참조 해석 실패는 개수와 진단 보고서로 노출하고 원본 행은 보존한다. 상세 낚시 조건의 유무, 아이콘 로드 여부, 캐릭터 수집 상태는 이 분류의 기준이 아니다.
- 예측은 UTC 기반 절대 시간, 입질 경과는 Stopwatch 같은 단조 증가 시간으로 처리한다.
- ET/날씨 계산을 렌더링 루프 밖에서 수행하고 지역·날씨 구간·데이터 revision별로 캐시한다.
- 로그인/로그아웃/지역 전환/선택 변경 시 필요한 상태만 갱신한다.
- 적절한 이벤트가 없다면 필요한 상태만 제한된 주기로 관찰하고 성능을 측정한다. 이벤트 API가 있다고 추측하지 않는다.
- UI는 프레임마다 그리더라도 전체 데이터 재계산을 하지 않는다. 긴 예측은 계산 예산과 취소를 지원한다.
- 게임 API와 포인터는 해당 API가 요구하는 스레드/수명 안에서만 사용한다. 백그라운드 계산에는 복사된 값만 넘긴다.

## 캐릭터 상태와 실패 처리

0.2.5.0의 `NativeRuntimeCompatibility`는 검증한 KR 게임 버전과 런타임별 Dalamud/ClientStructs/Interop의 MVID·SHA256 조합을 한 곳에서 판정한다. 모듈을 개별 허용 목록으로 섞지 않으며 생성 시 파일 검사만 수행한다. 수집/도감 열기/타이머/효과음은 같은 판정을 재사용하고 미지원 환경의 UI에는 실제 사유를 표시한다. [새 배포의 구조/IL 근거](validation/NATIVE-RUNTIME-15.0.3.7.md)와 인게임 확인은 구분한다.

0.1.20.0 `SpawnAlertScheduler`는 기존 예측의 미래 창과 사전 알림 시각 통과를 Core에서 판정한다. Plugin `SpawnAlerts`는 Framework에서 최종 수집 상태/즐겨찾기 snapshot을 연결하고, `FishPreferences`의 캐릭터별 마지막 창 시작을 저장한 뒤 로컬 채팅 큐에 넣는다. 로그인·설정 변경·긴 중단·시계 변경은 소급 발송하지 않는다. Draw에서는 설정 요청/불변 view 표시만 처리한다. [상세 정책과 실패 동작](validation/MVP-03-SPAWN-ALERTS.md)을 따른다.

0.1.21.0은 어종별 적용 사전 분을 Core 예약 키에 포함하여 해당 어종만 다시 예약한다. `SpawnAlertOutput`은 로컬 XivChatEntry/지도 링크와 KR 게이트를 공유한 정적 SE 호출을 격리한다. 설정 schema v3의 공통 개별 시간/채널/SE, 캐릭터별 영속 중복 방지 기록, 세션 200건 UI 발송 이력은 수명과 책임을 구분한다. [이번 결정](validation/MVP-03-ALERT-MANAGEMENT.md)을 따른다.

0.1.22.0의 `FishRowRenderer`는 목록/즐겨찾기의 동일 행 표시와 요약 캐시만 담당한다. 게임 데이터 조회/예측/필터는 가져오지 않는다. `ChatChannelChoices`는 설치 SDK enum 전체와 번역을 연결하며 schema v4에서 과거 세 채널 선택을 실제 타입 ID로 한 번 변환한다. [이번 결정](validation/MVP-03-ALERT-CATALOG-VIEW.md)을 따른다.

0.1.23.0의 Core `SpawnAlertAudience`는 전체/미수집만을 구분한다. 기본 전체는 Unknown도 대상으로 삼되 수집 상태 자체를 바꾸지 않는다. 새 등록 자동 해제는 이벤트 수신 당시 선택과 처리 당시 설정/세션을 함께 확인하여 기존 검증된 KR 등록 경로에서만 Framework의 preferences 저장/게시를 호출한다. 도감 스캔과 수동 지정은 이 경로를 호출하지 않는다. [정책/실패 동작](validation/MVP-03-FAVORITE-POLICY.md)을 따른다.

0.1.25.0은 사용자 요청으로 0.1.24.0의 IToastGui 의존성과 팝업 설정/출력/발송 기록 표시를 제거한다. `SpawnAlertOutput`은 로컬 채팅과 선택 SE만 담당하며 기존 예약/영속 중복 방지에는 변경이 없다. schema v7은 v1~v6 설정을 허용하고 제거된 팝업 속성을 사용하지 않는다. 채널 선택은 22개이며 기존 알려진 제외 채널은 시스템으로 대체한다. [제거 결정/확인 상태](validation/MVP-03-TOAST.md)를 따른다.

0.1.26.0의 `CatalogSettings`는 안정적인 enum/지역 ID와 초기값을 보관한다. UI는 한 번 복원하고 변경 요청만 기존 preferences 큐에 넣는다. Framework에서 다른 요청과 병합해 저장하고 복사한 설정을 view에 게시한다. schema v8은 자동 해제 켜짐을 이전 버전에 적용하며 이후 사용자 선택은 보존한다. 도감 안내는 기존 세션/GuideState.Complete 판정을 그대로 사용하며 추가 조회나 수집 추론을 하지 않는다. [정책/실패 처리](validation/MVP-01-CATALOG-SETTINGS.md)를 따른다.

- 수집 상태는 Caught / Uncaught / Unknown. 값 없음은 false가 아니다.
- 수동 override는 자동 판정과 따로 저장하고 '자동 상태 사용'으로 복귀할 수 있게 한다.
- 캐릭터 식별자는 로컬 설정 격리에만 사용하고 로그·공개 테스트 자료에 넣지 않는다.
- 로그인/캐릭터 전환 때 이전 캐릭터의 snapshot과 알림 예약을 폐기한다.
- 호환되지 않는 상태 읽기는 비활성화하고 설명을 표시한다. 수동 체크와 검증된 정적 안내는 계속 제공할 수 있게 한다.
- 이벤트/후크와 타이머는 Dispose 때 해제한다. 종료 중 파일 쓰기/비동기 작업도 정리한다.
- 설정은 스키마 버전, migration, 손상 시 원본 보존, 임시 파일 후 교체 방식의 저장을 고려한다.

## 데이터 모델 검토 항목

정식 스키마는 첫 실제 어종을 검수한 뒤 확정한다.

- dataset schemaVersion, dataVersion, 대상 region/KR patch, source URL/revision/license, 검증일.
- fish item ID와 fish guide row ID의 명시적 구분·매핑. zone/territory/map/fishing spot ID도 혼동하지 않는다.
- 지역·낚시터·좌표, 확장팩, KR 표시명. 이름은 식별자로 사용하지 않는다.
- ET 구간(자정 통과 포함), 현재 날씨 목록, 직전 날씨 목록, AND/OR 결합 의미.
- 미끼와 생미끼 연쇄, 직감 등 추가 조건, 지원/미지원/미확인 상태.
- 훅킹 권장과 적용 조건, 입질 구간/측정 기준/변동 조건/출처. 데이터가 없으면 unknown.

검수는 중복/미존재 ID, 생미끼 연쇄 순환, 잘못된 시간 구간, 잘못된 min/max, 누락 출처를 검사한다. 기존 커뮤니티 자료는 라이선스와 재배포 범위를 확인한 뒤 개발 시점에 정제하여 내장한다.

## 의미 있는 테스트

- ET 자정·날씨 경계 직전/정각/직후, 현재+직전 날씨 교집합, 시간대 변경과 독립성.
- 알려진 UTC/지역/날씨 기대값. 구현을 복사한 계산식을 테스트 정답으로 쓰지 않는다.
- 창 중간 로그인, 이미 지난 사전 알림, 절전 복귀 시 알림 폭주 방지, 같은 창 중복 방지.
- 수집 성공/실패/지연, 수동 override 우선순위, 캐릭터 전환 격리.
- 시전 시작/중복/취소/완료/생미끼/로그아웃 상태 전이, OS 시각 변경에도 경과 타이머 유지.
- Plugin은 Windows에서 로드/언로드/재로드, 채널별 알림 1회, 지도 좌표를 직접 확인한다.

## 0.1.27.0 UI 책임과 수명

`FishDetailPanel`은 메인 목록/즐겨찾기의 오른쪽과 단일 `FishDetailWindow`에서 공유한다. 화면은 기존 큐를 통해서만 수집 지정/지도/링크를 요청한다. 독립 상세는 세션 변경 시 선택을 비운다. `CompactFishObserver`는 창이 열릴 때만 Framework에서 단조 시계 1초 간격으로 기존 예측을 필터링하며, `CompactFishWindow`는 게시된 배열의 보이는 행만 그린다. 닫기/재오픈/Dispose에서 갱신 중지/표본 초기화/이벤트 해제를 수행한다. 예측 알고리즘과 알림 예약은 가져오지 않는다.

schema v9의 언어·개발자 옵션·간략 설정은 기존 preferences 저장 큐에 합쳐 저장하며 v1~v8을 허용한다. v8에서 저장한 자동 즐겨찾기 해제 선택은 유지한다. 공통 아이콘/배너는 `UiWidgets`, 조건 정렬은 `FishingConditionPanel`, 목록의 아이콘 요약 캐시는 `FishRowRenderer`가 담당한다. 상세 근거는 [이번 구현 기록](validation/MVP-01-UI-UX-REDESIGN.md)에 있다.

0.1.28.0의 `CatalogFilter`는 메인/간략의 일반 조건을 공유하며 검색·진단·정렬과 간략 예정 범위는 각 화면에 남긴다. 캐릭터 수집 상태는 기존 세션/수동 우선 판정으로 전달한다. `FishingWindowPanel`은 날짜/색상/문구만 담당한다. 미끼 단계는 RelatedDetails의 짧은 사실만 표시하고 전체 상세를 재귀 렌더링하지 않는다. schema v10/preferences/v8과 기존 설정 보존은 [피드백 구현](validation/MVP-01-UI-FEEDBACK.md)을 따른다.
