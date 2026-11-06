# 한섭 낚시 조건 데이터

## 일반 낚시 이중·삼중 수량 안내 (개발본 2026-11-05)

`hook-yields.v1.json`은 조건/등급과 별도로 내장하는 형식1 자료다. 일반26종(이중26·삼중11)과 먼바다259종(양쪽 기술259)을 안내하며, 직감 선행43종 중20종(이중20·삼중8)을 포함한다. 한 기술이 null이면 미확인, maximum=null이면 원본이 하한만 설명한 경우다. 이중에서 삼중을 환산하거나 획득력 임계값을 추정하지 않는다. 일반 어종의 전수 자료는 아니다.

일반 수량의 원본은 Fruity Snacks의10개 영상 설명과 종합 가이드와 Dawntrail 가이드다. 고정 영상ID·URL·확인일·UTF-8 SHA256·가공/권리 범위를 파일에 기록했다. 영상은descriptionSha256, 문서 내보내기 스냅샷은contentSha256으로 검증하며 문서 URL 자체를 고정 revision으로 간주하지 않는다. 이름/ID는 기존 조건 자료와 대조하고 게임 표시 이름은 클라이언트를 따른다. 원문·영상·이미지·외부 코드는 배포하지 않는다. 자세한 판정은 [DATA-SOURCES](../docs/development/DATA-SOURCES.md#일반-낚시-이중삼중-수량-안내-2026-11-05)를 따른다. 런타임 HTTP는 없다.

현재 제품은 **`fishing.kr.v2.json` / `kr-regular-2026-11-06`**를 내장한다. 일반 도감1,258종 모두 상세에 연결했고 시간·날씨는1,257종 확인/부분1종이다. 핵심 잔여는 성 파스릭의 직감 시간·이크에바지의 낚아채기/시간·날씨 완전성2종이다. 최신 수치·출처/충돌·양쪽 시트 근거는 [이번 보완](../docs/development/DATA-SOURCES.md#필요한-어종-보완과-먼바다-조건-2026-11-06), 이전11종 목록은 [당시 보고서](../docs/verification/data/reports/fishing-data-2026-10-17/remaining.md)에 보존한다. 작살203건/미확인115건은 유지한다. 입질 통계는 포함하지 않는다.

- [archive/fishing.kr.v1.json](archive/fishing.kr.v1.json)은 기존 예측 테스트의 고정 입력으로 보존한다. 제품 로딩/패키징 대상이 아니며 v2와 병합하지 않는다. 테스트 출력의 파일명은 그대로 유지한다.
- `fish-classifications.kr.v1.json`은 별도 **등급** 자료이며 현재 제품에서 사용하므로 현 위치에 유지한다. 파일명의 v1/v2는 서로 다른 자료의 형식 버전이다.

## 최소 획득력·납품·수량 보강 (2026-11-06)

최신 조건 보완은 JSON의 `reviewedDetailObservations`에 원문 스냅샷 해시·출처별 수치·범위를 기록했다. 초기 재생성 출력으로 이 관측 보완을 덮어쓰지 않는다. 새 수치 적용 뒤 조건 자료의 LF 정규화 해시와 등급 자료의 버전/연결 해시를 함께 갱신해 `--validate`로 검사한다. 기존 소스 revision·과거 미확인 보고서는 초기 기반의 이력으로 보존한다.

- `minimum-gathering.v1.json`: 고정 Teamcraft MIT 원본의 양수 최소 획득력350종이다. source URL/revision/SHA256/가공 내역과 Item/FishParameter를 기록한다. 0/누락과 복수 수량의 보장 임계값을 추정하지 않는다. 상세 조건 없는29종도 최소 획득력을 표시하며 유효하지 않은 자료는 해당 정보만 확인 불가로 처리한다.
- `hook-yields.v1.json`: 일반26종/이중26·삼중11에 먼바다259종/이중259·삼중259를 더했다. `ocean`은 생략/false면 기존 일반 조건 자료와 ID를 검증하고 true면 개발 중 양쪽 클라이언트 대조와 실제 표시 어종 ID로 확인한다. 범위·하한·미확인 의미는 유지한다. 원저자 CSV 두 파일의 숫자 사실만 사용하고 URL/고정 해시/확인일과 권리 범위를 기록한다.
- 납품원77종·단골거래12명/54개 NPC-어종 구간은 별도 정적 파일을 만들지 않고 클라이언트 시트를 카탈로그 로드 시 관리 값으로 복사한다. 런타임 HTTP는 없다. 범위/대조 근거는 [DATA-SOURCES](../docs/development/DATA-SOURCES.md#최소-획득력납품-구간수량-확장-2026-11-06)를 따른다.

개발 전용 재생성(테스트/게임 프로세스 접근 없음):

```powershell
. ./scripts/enter-dev.ps1
dotnet run --project tools/FishingDataImport -- --detail-supplement .cache/data-completion/fishing-sources.json data/fishing-supplement-source.json 'C:/Program Files (x86)/FINAL FANTASY XIV - KOREA/game' 'C:/Program Files (x86)/SquareEnix/FINAL FANTASY XIV - A Realm Reborn/game' .cache/minimum-gathering.v1.json
Copy-Item -LiteralPath data/hook-yields.v1.json -Destination .cache/hook-yields.v1.json
dotnet run --project tools/FishingDataImport -- --ocean-hook-yields .cache/ocean-beta-research/indigo-EN.csv .cache/ocean-beta-research/ruby-EN.csv data/ocean-guidance.v1.json 'C:/Program Files (x86)/FINAL FANTASY XIV - KOREA/game' 'C:/Program Files (x86)/SquareEnix/FINAL FANTASY XIV - A Realm Reborn/game' .cache/hook-yields.v1.json
```

두 번째 명령은 기존 수량 파일의 복사본을 출력 경로로 사용한다. 입력의 고정 해시와 출력 차이를 검토한 뒤 제품 자료에 반영한다.

## 출처와 경계

- 기본 원본: [Fish Tracker](https://github.com/icykoneko/ff14-fish-tracker-app/tree/d0e1ab5bba62c2b57356e690a73676cd4751b64d), revision `d0e1ab5bba62c2b57356e690a73676cd4751b64d`. JS 숫자 조건과 YAML 미확인 표시를 함께 읽는다. 입력 해시는 `fishing-source.json`에 고정한다.
- 보완 원본: [Teamcraft 정적 JSON](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/tree/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/libs/data/src/lib/json), revision `8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7`. 입력 5개 파일의 해시·기본 자료 해시·지원 빌드·출력 버전은 `fishing-supplement-source.json`에 고정한다. 호스팅 통계 API를 이용한 것이 아니다.
- 두 원본의 MIT 고지는 루트 `THIRD-PARTY-NOTICES.txt`에 보존한다. 추가 2종의 개별 어획 사실은 manifest에 별도 URL/관찰자/확인일/범위를 기록했다. 해당 댓글을 MIT 자료로 취급하거나 표현·이미지·입질 초 수를 복제하지 않는다.
- `kr`은 한섭 ID/빌드 검수 대상이라는 뜻이다. 이름·아이콘·날씨 확률·지도·스킬·전승록은 실행 중 클라이언트 시트에서 읽고 영문 `sourceName`을 UI 이름으로 쓰지 않는다. 글로벌 빌드 호환성 보장이 아니다.
- `tools/FishingDataImport`는 로컬 파일·설치된 Lumina/Excel·Core만 사용한다. JS 실행, HTTP, 게임 프로세스 접근, 새 패키지는 추가하지 않았다. 다운로드는 개발 중에만 수행한다.

## 재생성

저장소 루트에서 실행한다. `.cache` 출력과 감사 JSON을 검토한 뒤 제품 자료로 복사한다. 1단계 결과는 보완 입력의 고정 기준이므로 완성된 제품 파일로 대체하지 않는다.

먼저 [개발 환경 변수](../docs/development/WINDOWS-SETUP.md#개발-환경-변수)를 설정한다. 데이터 도구는 `DALAMUD_HOME`에서 Lumina를 참조하며 `FFXIV_GAME_PATH`는 실제 game 디렉터리다.

```powershell
. ./scripts/enter-dev.ps1 -GamePath 'C:/Program Files (x86)/FINAL FANTASY XIV - KOREA/game'
```

### 1. 기존 원본 903종과 작살 203건

```powershell
New-Item -ItemType Directory -Force .cache/fishing-sources | Out-Null
$primary = Get-Content data/fishing-source.json -Raw | ConvertFrom-Json
$baseUrl = "https://raw.githubusercontent.com/icykoneko/ff14-fish-tracker-app/$($primary.revision)"
Invoke-WebRequest "$baseUrl/js/app/data.js" -OutFile .cache/fishing-sources/data.js
Invoke-WebRequest "$baseUrl/private/fishData.yaml" -OutFile .cache/fishing-sources/tracker-fishData.yaml
dotnet run --project tools/FishingDataImport -- data/fishing-source.json .cache/fishing-sources/data.js .cache/fishing-sources/tracker-fishData.yaml "$env:FFXIV_GAME_PATH" .cache/fishing-all.json .cache/fishing-all-audit.json
```

기본 JSON의 LF 정규화 SHA256: `27811E206378FC768B086D44BCA21A25CB2D94FD31E24EC1D0303C94D04CAA1C`.

### 2. 일반 낚시 보완과 잔여 목록

```powershell
New-Item -ItemType Directory -Force .cache/data-completion | Out-Null
$supplement = Get-Content data/fishing-supplement-source.json -Raw | ConvertFrom-Json
$staticUrl = "https://raw.githubusercontent.com/ffxiv-teamcraft/ffxiv-teamcraft/$($supplement.revision)/libs/data/src/lib/json"
foreach ($file in $supplement.files.PSObject.Properties.Name) {
    Invoke-WebRequest "$staticUrl/$file" -OutFile ".cache/data-completion/$file"
}
dotnet run --project tools/FishingDataImport -- --complete "$env:FFXIV_GAME_PATH" .cache/fishing-all.json data/fishing-supplement-source.json .cache/data-completion .cache/data-completion/completed.json .cache/data-completion/report
```

결과는 일반 903→1,258종, 변경 466종, 핵심 잔여 11종이다. 새 상세 355종(정적 자료 353종/개별 사실 2종), 기존 낚아채기 80종·입질 강도 4종·예측 위치 31종을 보완했다. 이 수치는 어종 간 중복을 포함한다. 기존 알려진 조건·미끼·예측 공식과 작살 자료는 변경하지 않았다. 변환은 KR 어종/Item/낚시터/미끼/날씨 ID, 시간 범위, 생미끼 순환, 기존 조건과 위치의 일치를 검사한다.

### 3. 터주 등급 자료 재생성

검토한 `completed.json`을 `data/fishing.kr.v2.json`에 복사한 뒤 실행한다.

```powershell
dotnet run --project tools/FishingDataImport -- --classifications "$env:FFXIV_GAME_PATH" data/fishing.kr.v2.json .cache/data-completion/classifications.json .cache/data-completion/classifications-audit.json
```

- 출력 `fish-classifications.kr.v1.json`: KR 일반 도감 1,258종의 명시적 터주왕/터주 설명과 FishingNoteInfo.SpecialConditions=2 양성 집합을 개발 중 대조한 ID 자료다. 설명 전문·아이콘을 내보내지 않는다.
- 결과: Inspected=1258 / SourceCompared=1258 / PositiveTimingCompared=131 / BigIncludingLegendary=335 / Legendary=36 / Failed=0. 등급 ID 양성 집합은 이전과 동일하다. 다른 빌드에 플래그 의미를 일반화하지 않는다.
- 조건 입력 해시는 LF 정규화하며 분류 버전은 조건 버전에서 파생한다. 초기2026-10-17 분류 파일 SHA256: `756D46948B20A44F5695465CDBEC42E63EAC355F1BD9B372D9AEADDC98048DF2`. 검토 후 출력 파일을 제품 경로에 복사한다.

## 먼바다 조건 후보 자료 (2026-11-06)

`ocean-conditions.v1.json`은260개 어종-낚시터 소속 행이다. 일반/환해류 양쪽 소속 어종은 서로 다른 행으로 보존한다. `timeMask`는 게임 방문 시간 값1/2/3의 비트, 날씨는 allowed/excluded/unknown의 명시적 집합이며 목록 밖의 날씨는 미확인이다. 빈 집합은 그 해역 구분에서 적용하지 않는 조건이다. `requiresIntuition`은 선행 어종 진행도가 아닌 필요 여부만 나타낸다. 같은 원저자 CSV의 Yes/No/???만 사용하고 파일에 URL·SHA256·가공/권리 범위를 기록했다.

```powershell
$duckbillKrGame = 'C:/Program Files (x86)/FINAL FANTASY XIV - KOREA/game'
$duckbillGlobalGame = 'C:/Program Files (x86)/SquareEnix/FINAL FANTASY XIV - A Realm Reborn/game'
dotnet run --no-restore --project tools/FishingDataImport -- --ocean-conditions `
  .cache/ocean-beta-research/indigo-EN.csv .cache/ocean-beta-research/ruby-EN.csv `
  data/ocean-guidance.v1.json $duckbillKrGame $duckbillGlobalGame `
  .cache/ocean-conditions.generated.json
```

게임 경로는 현재 PC의 설치 예시다. 다른 환경에서는 각 설치본의 실제 `game` 디렉터리를 지정한다. 생성 시 양쪽 Item/FishParameter·낚시수첩 슬롯 소속을 대조하며, 런타임은 관찰/자료 실패를 후보 유지로 처리한다. 직감 활성의 대상·미끼/장비·특수 어획표를 확정하지 않는다. [실행 계약](../docs/verification/native/GLOBAL-COMPAT.md#먼바다-조건-후보-개발본-2026-11-06).

## 필드 의미와 미확인

조건/등급 JSON은 Core의 `FishingDatasetReader`로 공통 검증한다. 가져오기 도구는 출력 저장 전에, Plugin은 초기화 때 한 번 실행한다. `conditionsKnown`·시간·날씨·미끼·선행 어획 등 필수 필드 누락, 잘못된 null/형식/범위, 중복 속성/ID는 거부하며 기본값으로 보완하지 않는다. `intuitionSeconds`·`fishEyesSupported`의 명시적 null은 미확인으로 보존한다. 기존 빈 `baitPath`는 허용하고, 값이 있으면 `baitSteps`와 연결을 대조한다.

제품0.3.5.0부터 사용자 요청에 따라 직감 지속 시간 null은 예측 계산에서만175초를 가정한다. 데이터 파일의 null은 유지하고 계산값/가정 여부를 별도 보관해 상세에 표시한다. 데이터 검증·미확인 집계나 물고기의 눈 지원 값을 채우는 동작은 아니다.

등급 자료는 조건 JSON의 LF 정규화 SHA256·게임 빌드·자료 버전, 겹치는 어종의 Item/FishParameter/터주 판정, 조건 자료의 터주 양성 항목 누락을 검사한다. 파일을 바꿀 때는 조건과 등급을 함께 재생성·검토한다. 해시 일치는 두 파일의 연결을 검증하며 외부 원본의 낚시 사실 자체를 증명하지 않는다.

```powershell
# 개발 환경 변수 설정 후 실행. 로컬 JSON 검사이며 게임/네트워크 접근은 없다.
dotnet run --no-restore --project tools/FishingDataImport -- --validate data/fishing.kr.v2.json data/fish-classifications.kr.v1.json
```

실패하면 `DataInvalid: code=...; path=...`와 종료 코드 1을 반환한다. 조건 자료 검증 실패 시 Plugin은 조건 상세/예측/예측 알림/등급을 제공하지 않고 기본 시트 목록·수동 수집 설정을 유지한다. 등급 자료만 실패하면 등급은 Unknown이며 정상 조건/예측은 유지한다. 현재 언어로 오류를 표시하고 catalog/v6의 `DataValidation`에 언어 독립 코드와 항목 경로를 기록한다. 이 검사는 테스트 실행이나 인게임 확인이 아니다.

- `baitSteps` 바깥 배열은 미끼→생미끼 순서, 내부 배열은 같은 단계 대체 후보다. 추가 자료는 검증된 한 경로를 선택하며 가능한 모든 미끼를 보장하지 않는다.
- `hookset`/`tug` 빈 문자열, `intuitionSeconds=null`은 해당 필드 미확인이다. 낚아채기를 입질 강도에서 추정하거나 직감 지속 시간을 출현 시간 길이로 대체하지 않는다.
- 빈 날씨 배열은 확인된 원본에서만 제한 없음이다. `conditionsKnown=false`인 어종은 미끼·입질 강도·위치만 안내하며 상시 예측/알림으로 바꾸지 않는다.
- `fishEyesSupported`는 nullable이다. 기존 903종의 bool은 유지하고 추가 자료에 없는 355종은 null로 둔다. 지원 여부 자체는 UI에 표시하지 않는다. 목록/상세의 미사용 기준 안내와 별개이며 개인 스킬을 기본 예측에 적용하지 않는다.
- `patchVersion`은 3.55b 같은 접미사를 보존한다. 기존 숫자 `patch`도 유지하며 기존 903종은 숫자 표시를 사용한다. 전승록은 KR GatheringSubCategory.Item으로 연결한다.
- `snaggingRequired`는 갈고리 낚시, `lure`는 Modest/Ambitious 요구다. 직감 선행 어종/마릿수는 지속 시간 누락과 별개로 안내한다.
- 먼바다 259종의 기본 해역·일반/환해류 장소·항로 방문 시간은 IKD 시트에서 제공한다. 어종별 미끼·특수 조건은 별도 연동이 남았다. 작살 203건 중 `conditionsKnown=false` 115건과 전용 UI·수집 연동도 후속이다. [전체 ID/이름/이유](../docs/verification/data/reports/fishing-data-2026-10-17/remaining.md)에 분리했다.
- 개발본 먼바다 Beta의 기본 미끼는 [ocean-guidance.v1.json](ocean-guidance.v1.json)의 별도52행을 사용한다.13해역×일반1/환해류 시간대3이며 일반 상세 자료를 덮어쓰지 않는다. 원저자 CSV의 개별 사실→유일한 게임 Item ID→KR/글로벌 FishParameter 낚시터로 대조했다. 갱신 시 JSON의 URL/스냅샷 SHA256과 [가공 기준](../docs/development/DATA-SOURCES.md#먼바다-beta-기본-안내와-일정-2026-11-06)에 따라52조합의 미끼·시간대·입질·수량·장소를 다시 확인한다. 실행 중 이름 매칭이나 HTTP를 하지 않는다.
- 모든 선택 낚시터는 한섭 지도 시트와 대응한다. 외부 좌표와 다른 294/295/296/327번은 한섭 좌표를 쓰며 실기 차이 확인은 대기다.

빌드/자료 검사와 인게임 확인은 구분한다. [이번 실행 근거와 남은 확인](../docs/verification/data/DATA-COMPLETION-2026-10-18.md)을 따른다.
