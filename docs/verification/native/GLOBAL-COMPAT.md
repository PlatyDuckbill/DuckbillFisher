# 글로벌·공식 저장소 호환성 조사

기록일: 2026-11-04. [글로벌 지원·공식 저장소 계획](../../product/GLOBAL-AND-OFFICIAL-REPO.md)의 P0·P1 근거다. 모든 조사는 오프라인 메타데이터·시트 읽기와 공개 목록 1회 조회만 했다. 게임 실행, 플러그인 로드, 코드·허용 목록 변경은 없다. 산출물은 `.cache/global-compat-2026-11-04/`(Git 제외)에 있다.

## 대상 환경

| 항목 | KR | 글로벌 |
|---|---|---|
| 게임 빌드 | 2026.10.06.0000.0000 | 2026.10.16.0000.0000 |
| Dalamud | wasdcxz 15.0.3.9 (`0bbbf4f5`) | goatcorp 15.0.3.9 (`30509c3b`) |
| FFXIVClientStructs | 7.57.0.9415 | 7.57.0.9414 |

dal4kr 15.0.3.9는 DuckbillTweak 조사 캐시의 고정 Release.zip 추출본을 사용했다. 이 Dalamud.dll의 SHA256 `0E5FBEF4…EBA6`은 현재 `15.0.3.9-dal4kr` 프로필과 같다.

## P0: KR 배포판의 공식 저장소 사용

- 세 배포판(글로벌, wasdcxz, dal4kr)의 `PluginRepository.CreateMainRepo`는 모두 `https://kamori.goats.dev/Plugin/PluginMaster`를 사용한다. KR 배포판도 공식 저장소를 그대로 받는다.
- Mono.Cecil로 다음 IL을 비교했다. 세 배포판 모두 동일하다.
  - `PluginManager.IsManifestEligible`
  - `PluginManager.DetectAvailablePluginUpdates`
  - `LocalPlugin.get_IsOutdated`
  - `ExposedPlugin.get_IsOutdated`
  - `get_EffectiveVersion` 2종
- 따라서 KR 사용자는 글로벌 사용자와 같은 기준으로 공식 플러그인을 받는다. 다른 공식 플러그인이 KR에서 별도 대응 없이 동작한다는 사용자 관찰과 일치한다.
- 판정 규칙(소스 [PluginManager.cs](https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Plugin/Internal/PluginManager.cs)와 IL로 확인):
  - 목록 노출: 매니페스트의 API가 현재 API−1 이상이고 `ApplicableVersion`이 게임 버전보다 낮지 않을 때.
  - 업데이트 제공: 후보 API가 현재 API와 같을 때만.
  - 로드 차단(구버전 판정): 설치본 API가 현재 API보다 낮을 때.
- 공식 PluginMaster 조회 결과(2026-11-04, SHA256 `758D6D25…3EB9`):
  - 500개 항목, InternalName 500개가 모두 고유하다. 플러그인마다 stable 항목 하나와 선택적인 testing 필드만 있고, 지역별·API별 사본은 없다.
  - API 분포: 15=412, 14=21, 13=11, 12=12, 11=3, 10=6, 9=18, 8=13, 7=4.
  - `ApplicableVersion`은 500개 모두 `any`다.

### 결론과 영향

1. **공식 빌드 하나가 KR과 글로벌을 함께 담당한다.** 두 환경에서 로드되는 단일 산출물이 필요하며, 지역별 빌드나 분기는 둘 수 없다.
2. **API 전환기에는 한쪽이 멈춘다.** 글로벌이 API를 올리고 KR 배포판이 아직 따라오지 않았을 때 새 API로 배포하면, KR에서는 업데이트가 제공되지 않는다. 이미 설치된 이전 버전은 KR의 API와 같으므로 계속 로드된다. 이는 모든 공식 플러그인이 KR에서 겪는 같은 제약이며, 현재 KR과 글로벌은 모두 API 15다.
3. **참조 멤버가 양쪽에 모두 있어야 한다.** 공식 CI는 글로벌 최신 참조로 빌드한다. 글로벌 ClientStructs에만 있는 멤버를 쓰면 KR에서 해당 메서드 실행 시 실패한다. KR 참조 빌드를 개발 검사로 유지해야 한다.
4. **게임 자료는 지역 간 패치 차이를 견뎌야 한다.** 실행 중인 클라이언트 시트에 없는 행은 숨기는 방식이 필요하다(P2).

## P1: 낚시 로그 계약의 언어 의존성

대상은 `FishingLogContracts`의 LogMessage 28개다. 임시 도구(`.cache/global-compat-2026-11-04/logaudit/`)로 KR 한국어와 글로벌 영어·일본어·독일어·프랑스어 시트의 `ToMacroString()`을 읽었다.

| 언어 | 현재 SHA256 지문 일치 |
|---|---|
| 한국어 (KR) | 28/28 |
| 영어 | 0/28 |
| 일본어 | 0/28 |
| 독일어 | 0/28 |
| 프랑스어 | 0/28 |

- 현재 방식대로라면 **글로벌에서는 모든 로그 계약이 거부되어 타이머·기록·생미끼·낚시터 판정이 동작하지 않는다.**
- 매크로에서 매개변수와 시트의 결합(`sheet`/`ennoun`/`denoun`/`frnoun(시트, …, lnumN)`)만 뽑아 비교했다.
  - **27/28은 5개 언어에서 결합이 같다.** 예: 1110·1115·1130·3513은 `PlaceName:lnum1`, 3512는 `Item:lnum1`과 수치 `lnum2·lnum3`, 5557은 `Item:lnum1+Item:lnum2`.
  - 1121(생미끼 재투척)은 영어 문장만 물고기 이름을 표시하지 않아 결합이 비어 있다. 나머지 4개 언어는 `Item:lnum1`이다.
  - 로그 매개변수는 서버가 보내는 값이며, 문장이 그 값을 표시하지 않아도 매개변수는 그대로 존재한다. 따라서 결합이 비어 있는 것은 충돌이 아니다.
- 결합이 없는 13개 ID는 문장으로 의미를 확인할 근거가 없다. 언어 무관 방식에서는 ID 자체를 계약으로 삼게 된다.
  - 해당 ID: 1111·1112·1113·1116·1117·1119·1120·1127·1129·3511·3527·5559·5584.
  - 현재 KR 지문보다 검증이 약해지는 지점이며, 재설계안에서 판단한다.

### 재설계 후보 (미승인)

- 정확한 문장 지문 대신 ID별 **기대 결합 집합**을 둔다.
- 현재 클라이언트 언어 행에서 발견한 결합이 기대 집합의 부분집합이고, 같은 `lnumN`이 다른 시트에 결합되지 않으면 허용한다.
- 행이 없거나 결합이 충돌하면 해당 ID만 거부한다.
- 문장 수정·번역 차이로는 차단되지 않는다. 반대로 결합 없는 ID의 의미 변경은 문장으로 감지할 수 없다.

## P1 구현 1단계: 로그 결합 계약 (2026-11-04)

사용자가 13개 ID에 대해 B안(실행 중에는 ID만 확인하고, 패치 때 개발 점검)을 승인했다. 이에 따라 KR 문장 SHA256 지문을 결합 계약으로 교체했다.

- **코드**
  - 판정: `Core/Fishing/FishingLogBindings.cs`에 기대 결합 28개, 결합 추출, 판정(Valid/Missing/Conflict)을 두었다.
  - 적용: Plugin `FishingLogContracts.Validate`는 실행 중인 클라이언트 언어 시트로 판정하고, 거부 요약을 반환한다.
  - 진단: 타이머 진단을 `timer/v6`으로 올리고 `ContractMode=ParameterBindings; RejectedContracts=<id:상태,…|none>`을 추가했다.
  - 바꾸지 않은 것: 판정 순서, 본인·Phase 조건, 네이티브 가드, 설정·기록 형식.
- **점검 도구**: `FishingDataImport --log-contracts`. 같은 Core 판정으로 여러 클라이언트와 언어를 검사하고, 해시 기준선과 비교해 문장이 바뀐 행을 `Review`로 표시한다.
- **도구 실행 결과**: 5개 언어 모두 **Valid 28/28**이다. 기준선 자기 비교는 140행 중 변경 0이다.

  | 클라이언트 | 언어 | 결과 |
  |---|---|---|
  | KR 2026.10.06 | 한국어 | 28/28 |
  | 글로벌 2026.10.16 | 영어 | 28/28 |
  | 글로벌 2026.10.16 | 일본어 | 28/28 |
  | 글로벌 2026.10.16 | 독일어 | 28/28 |
  | 글로벌 2026.10.16 | 프랑스어 | 28/28 |

- **기준선**: [`reports/log-contracts-2026-11-04.json`](reports/log-contracts-2026-11-04.json). 빌드, 언어, ID, 상태, 결합, 매크로 SHA256만 담았고 게임 문장 원문은 넣지 않았다.
- **빌드**: 모두 경고 0, 오류 0이다.
  - 플러그인: KR 참조 별도 빌드 → 글로벌 참조 별도 빌드 → KR 참조 정규 Debug 순서로 빌드했다.
  - 데이터 도구도 같은 결과다.
  - 한영 1,257키 검사를 통과했다.
  - 테스트 추가·실행은 하지 않았다.
- **미확인**
  - 인게임 확인: KR에서 기존 타이머·기록이 그대로인지, 진단이 `Contracts=28/28; RejectedContracts=none`인지 확인해야 한다.
  - 글로벌에서는 네이티브 전역 가드(3단계 전)가 아직 남아 있어 타이머가 동작하지 않는다.

## P1 구현 2·3단계: 오프셋·상한 제거와 전역 가드 제거 (2026-11-04)

- **미끼 화면**: `UIModule + 0xD2690`(RaptureAtkModule) 하드코딩 오프셋을 ClientStructs의 `AtkStage.Instance()`로 바꿨다.
  - KR·글로벌 FFCS 모두 같은 `StaticAddress` 시그니처(`48 8B 05 ?? ?? ?? ?? 4C 8B 40 18 45 8B 40 18`)를 갖는다.
  - 해석에 실패해 null이 되면 Unavailable로 처리한다.
- **수집 비트**: 상한 상수 `1522`를 실행 중 값으로 바꿨다.
  - 상한은 로드된 ClientStructs의 `CaughtFishBitArray.BitCount`(1522)와 `CaughtFish` 버퍼(191바이트, 마지막 비트 1527) 중 작은 값이다.
  - 비트는 버퍼 span에서 직접 읽는다. 순서는 `BitArray.Get` IL(`byte[id/8] & 1<<(id%8)`)과 같다.
  - `Get`은 `index == BitCount`를 허용하므로, 기존 1522 포함 범위를 유지한다.
- **전역 가드 제거**: `NativeRuntimeProfiles`(해시 5조합)와 `NativeRuntimeCompatibility`를 삭제했다. 보고 전용 `NativeEnvironment`를 추가했다.
- **기능별 독립 판정**: 각 기능은 다음 조건만 본다. 미끼 수량과 버프는 네이티브 상태와 분리됐다.

  | 기능 | 판정 조건 |
  |---|---|
  | 수집 | PlayerState 주소 |
  | 도감 열기 | 수집 주소 + `OpenForItemId` 함수 포인터 |
  | 낚시 상태 | 주소와 값 범위 |
  | 미끼 UI·찾기 | 형태 검사 |
  | SE | `PlaySoundEffect` 함수 포인터 |
  | 미끼 수량·버프 | 공개 API |

- **안내와 진단**
  - 쓰지 않게 된 문구 7개를 삭제했다: `NativeCompatible`, `NativeWrongProcess`, `NativeGameMismatch`, `NativeDllMismatch`, `NativeCheckFailed`, `NativeRuntimeUnsupported`, `BaitStockUnsupported`. 한영 키는 1,250개가 됐다.
  - 도감 배너 문구(`NativeFeaturesBlocked`)는 "수집 연동 또는 도감 바로 열기 불가 + 사유"로 좁혔다.
  - 진단 형식을 올렸다: `fish-guide-native/v4`, `collection/v5`. `NativeEnvironment: Game=…; ClientLanguage=…; Dalamud=버전/MVID; FFXIVClientStructs=…; InteropGenerator.Runtime=…; NativeGate=None; FishBitLimit=…`를 기록한다.
- **빌드**: 모두 경고 0, 오류 0이다.
  - 플러그인: KR 참조 별도 → 글로벌 참조 별도 → 최소 15.0.3.6 참조 Release → KR 정규 Debug.
  - 데이터 도구, 한영 리소스 검사도 통과했다.
  - 테스트 추가·실행은 하지 않았다.
- **미확인**: KR·글로벌 인게임 동작 전체. 수집, 도감 열기, 타이머·기록, 미끼 찾기, SE, 버프·미끼 수량 순으로 확인한다.

### 도감 등록 메시지 1114 보완 (2026-11-04)

- **사용자 보고(글로벌):** 수첩 연동은 되지만, 새 어종을 낚아도 낚시 수첩을 다시 열어야 갱신됐다.
- **원인:** `FishCollectionObserver`가 도감 등록 메시지 1114를 **KR 문장과 완전히 같은지**로 판정하고 있었다. 1단계에서 타이머 로그 28개만 바꾸고 이 메시지를 놓쳤다. 글로벌에서는 계약이 거부돼, 실시간 등록 반영(`RegistrationContract`)이 꺼져 있었다.
- **수정:** 1114를 공용 결합 계약에 `Item:lnum1`로 추가했다. 수집 관찰기도 같은 판정을 쓴다.
  - 코드에 남은 한글 문자열 비교는 없다(전체 검색으로 확인).
- **대조:** 5개 언어 모두 Valid **29/29**이고, 1114는 모든 언어에서 `Item:lnum1`이다. KR 문장은 기존 하드코딩 문자열과 똑같아 KR 동작은 그대로다. 기준선을 145행으로 갱신했다(해시만).
- **빌드:** KR, 글로벌, 최소 Release, 정규 Debug 모두 경고 0, 오류 0이다.
- **미확인:** 글로벌에서 새 어종을 낚았을 때 수첩을 열지 않아도 수집됨으로 바뀌는지.

## P2 자료 적용 범위 (2026-11-04)

- **발견:** 사용자가 글로벌(영어 클라이언트)에서 처음 실기했을 때, 모든 어종이 "Bait/Time·weather: unconfirmed, No forecast"로 표시됐다.
  - 원인: `FishCatalog`가 **게임 빌드 문자열이 자료 빌드(2026.10.06)와 같을 때만** 조건 자료를 연결했다. 글로벌 2026.10.16에서는 자료 전체가 끊겼다.
  - 같은 조건 때문에 추천, 출현 알림, 현재 낚시터 알림, 주거 구역 확장팩 보정도 꺼졌다.
  - KR도 작은 핫픽스로 빌드 문자열만 바뀌면 같은 증상이 생기는 구조였다.
- **근거:** `FishingDataImport --data-references`로 `fishing.kr.v2.json`이 참조하는 모든 ID를 KR·글로벌 시트에서 비교했다.
  - **참조 3,398개 전부 일치, 실패 0**이다.

    | 대상 | 참조 수 | 비교한 사실 |
    |---|---|---|
    | 아이템 | 1,554 | 분류, 아이콘 |
    | FishParameter | 1,258 | 아이템, 도감 포함, 먼바다 |
    | 낚시터 | 302 | 지역, 지명, 좌표, 반경, 어종 목록, 날씨 확률표 ID |
    | 날씨 확률표 | 62 | 날씨와 확률 |
    | 날씨 | 19 | — |
    | 작살 | 203 | — |

  - 아이템↔FishParameter 짝 1,258개도 양쪽 모두 일치한다.
  - 원본 자료(FFX|V Fish Tracker)는 원래 글로벌 ID 기준이다.
- **변경:** 빌드 문자열 비교를 없앴다. 이제 조건 자료가 유효하면 모든 클라이언트에 적용한다.
  - 기존 행 단위 검사는 유지한다: FishParameter↔Item 일치, 낚시터 어종 목록 포함 또는 FishParameter 낚시터 일치.
  - 주거 구역 확장팩 보정(641→2, 979→4)은 시트 ExVersion이 0일 때만 적용한다. KR·글로벌 모두 0임을 확인했다.
  - 진단에서 `buildMatch`를 `datasetBuild/clientBuild/conditionsApplied`로 바꿨다(`catalog/v10`, `summary/v11`).
- **기본 UI 언어:** 첫 실행 기본값이 한국어로 고정돼 있었다(사용자 보고).
  - 이제 KR 클라이언트(실행 중 `ClientLanguage` 이름이 `Korean`)이거나 Dalamud UI 언어가 `ko`일 때만 한국어, 그 밖에는 영어다.
  - 글로벌 Dalamud에는 `ClientLanguage.Korean`이 없다(Japanese/English/German/French만 있음). KR 배포판은 값 4를 추가한다.
  - 이미 저장된 언어 선택은 바뀌지 않는다.
- **빌드:** 모두 경고 0, 오류 0이다. KR 별도 → 글로벌 → 최소 15.0.3.6 Release → KR 정규 Debug, 데이터 도구 순으로 빌드했다. 한영 1,250키 검사를 통과했다. 테스트 추가·실행은 하지 않았다.
- **미확인:** 글로벌에서 조건, 예측, 지도, 추천이 실제로 표시되는지. 새 설정으로 첫 실행했을 때 영어로 시작하는지.

## P2 사전 확인: 고정 ID와 시트 규모

| ID | KR | 글로벌(영어) |
|---|---|---|
| ClassJob 18 | 어부 | fisher |
| Status 850 / 568 | 월척 확률 증가 / 어부의 직감 | Angler's Fortune / Fisher's Intuition |
| Status 3972 / 3973 | 대형 / 소형 물고기 확률 증가 | Ambitious Lure / Modest Lure |
| Action 4105 / 37594 / 37595 | 물고기의 눈 / 거대한 루어 / 소박한 루어 | Fish Eyes / Ambitious Lure / Modest Lure |
| PlaceName 950 | 미지의 낚시터 | Undiscovered Fishing Hole |
| ItemUICategory 33 | 낚시용 미끼 | Fishing Tackle |

시트 행 수도 같다.

| 시트 | 행 수 |
|---|---|
| FishParameter | 2,512 (Item 연결 2,467) |
| FishingSpot | 576 |
| LogMessage | 11,701 |

같은 7.57 단계라는 정황이다. 다만 조건, 날씨 비율, 좌표의 전체 대조는 P2에서 별도로 한다.

## 함께 확인한 dal4kr 15.0.3.9 구조 대조 (0.6.0.1 근거 보완)

`compare-native-runtime.ps1`로 49개 타입을 비교했다. 이 구조 대조는 0.6.0.1 배포 당시 Fisher 기준으로는 기록되지 않았다.

| 비교 | 결과 |
|---|---|
| dal4kr 15.0.3.7 → dal4kr 15.0.3.9 | 48/49 동일. `ChatHandlers.PrintWelcomeMessage` IL만 다름 |
| wasdcxz 15.0.3.9 → dal4kr 15.0.3.9 | 46/49 동일. `PrintWelcomeMessage`, `CompiledUrlRegex`, `RaptureAtkModule.ChangeUiMode`, `AtkUnitBase.UnsubscribeAtkArrayData`의 IL·특성만 다름 |

필드, 오프셋, 크기, 시그니처는 모두 같다. 달라진 메서드는 Fisher 소스에서 호출하지 않는다.

## 미확인 범위

- 글로벌 인게임 동작: 플러그인을 글로벌에서 로드한 적이 없다.
- 최신 공식 CI(Plogon)의 빌드 결과.
- API 전환 시 KR 설치본의 실제 화면 표시.
- 결합 없는 13개 ID의 지역 간 의미 동일성은 문장으로 확인할 수 없다.
