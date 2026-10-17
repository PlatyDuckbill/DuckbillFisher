# 한섭 낚시 조건 데이터

0.2.9.0은 **`fishing.kr.v2.json` / `kr-regular-2026-10-17`**을 내장한다. 일반 도감 1,258종 모두 상세에 연결했으며 시간·날씨 조건을 확인한 1,256종과 부분 확인 2종을 구분한다. 직감 지속 시간 9종을 포함한 핵심 미확인 11종과 별도 범위의 전체 목록은 [잔여 보고서](../docs/reports/fishing-data-2026-10-17/remaining.md)에 있다. 작살 자료 203건은 보존했다. `fishing.kr.v1.json`은 과거 검증 이력이며 현재 제품은 읽지 않는다. 입질 초 수 통계는 포함하지 않는다.

## 출처와 경계

- 기본 원본: [Fish Tracker](https://github.com/icykoneko/ff14-fish-tracker-app/tree/d0e1ab5bba62c2b57356e690a73676cd4751b64d), revision `d0e1ab5bba62c2b57356e690a73676cd4751b64d`. JS 숫자 조건과 YAML 미확인 표시를 함께 읽는다. 입력 해시는 `fishing-source.json`에 고정한다.
- 보완 원본: [Teamcraft 정적 JSON](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/tree/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/libs/data/src/lib/json), revision `8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7`. 입력 5개 파일의 해시·기본 자료 해시·지원 빌드·출력 버전은 `fishing-supplement-source.json`에 고정한다. 호스팅 통계 API를 이용한 것이 아니다.
- 두 원본의 MIT 고지는 루트 `THIRD-PARTY-NOTICES.txt`에 보존한다. 추가 2종의 개별 어획 사실은 manifest에 별도 URL/관찰자/확인일/범위를 기록했다. 해당 댓글을 MIT 자료로 취급하거나 표현·이미지·입질 초 수를 복제하지 않는다.
- `kr`은 한섭 ID/빌드 검수 대상이라는 뜻이다. 이름·아이콘·날씨 확률·지도·스킬·전승록은 실행 중 클라이언트 시트에서 읽고 영문 `sourceName`을 UI 이름으로 쓰지 않는다. 글로벌 빌드 호환성 보장이 아니다.
- `tools/FishingDataImport`는 로컬 파일·설치된 Lumina/Excel·Core만 사용한다. JS 실행, HTTP, 게임 프로세스 접근, 새 패키지는 추가하지 않았다. 다운로드는 개발 중에만 수행한다.

## 재생성

저장소 루트에서 실행한다. `.cache` 출력과 감사 JSON을 검토한 뒤 제품 자료로 복사한다. 1단계 결과는 보완 입력의 고정 기준이므로 완성된 제품 파일로 대체하지 않는다.

### 1. 기존 원본 903종과 작살 203건

```powershell
New-Item -ItemType Directory -Force .cache/fishing-sources | Out-Null
$primary = Get-Content data/fishing-source.json -Raw | ConvertFrom-Json
$baseUrl = "https://raw.githubusercontent.com/icykoneko/ff14-fish-tracker-app/$($primary.revision)"
Invoke-WebRequest "$baseUrl/js/app/data.js" -OutFile .cache/fishing-sources/data.js
Invoke-WebRequest "$baseUrl/private/fishData.yaml" -OutFile .cache/fishing-sources/tracker-fishData.yaml
dotnet run --project tools/FishingDataImport -p:DalamudLibPath=C:/Users/hyogu/AppData/Roaming/XIVLauncherKR/addon/Hooks/dev/ -- data/fishing-source.json .cache/fishing-sources/data.js .cache/fishing-sources/tracker-fishData.yaml "C:/Program Files (x86)/FINAL FANTASY XIV - KOREA/game" .cache/fishing-all.json .cache/fishing-all-audit.json
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
dotnet run --project tools/FishingDataImport -p:DalamudLibPath=C:/Users/hyogu/AppData/Roaming/XIVLauncherKR/addon/Hooks/dev/ -- --complete "C:/Program Files (x86)/FINAL FANTASY XIV - KOREA/game" .cache/fishing-all.json data/fishing-supplement-source.json .cache/data-completion .cache/data-completion/completed.json .cache/data-completion/report
```

결과는 일반 903→1,258종, 변경 466종, 핵심 잔여 11종이다. 새 상세 355종(정적 자료 353종/개별 사실 2종), 기존 낚아채기 80종·입질 강도 4종·예측 위치 31종을 보완했다. 이 수치는 어종 간 중복을 포함한다. 기존 알려진 조건·미끼·예측 공식과 작살 자료는 변경하지 않았다. 변환은 KR 어종/Item/낚시터/미끼/날씨 ID, 시간 범위, 생미끼 순환, 기존 조건과 위치의 일치를 검사한다.

### 3. 터주 등급 자료 재생성

검토한 `completed.json`을 `data/fishing.kr.v2.json`에 복사한 뒤 실행한다.

```powershell
dotnet run --project tools/FishingDataImport -p:DalamudLibPath=C:/Users/hyogu/AppData/Roaming/XIVLauncherKR/addon/Hooks/dev/ -- --classifications "C:/Program Files (x86)/FINAL FANTASY XIV - KOREA/game" data/fishing.kr.v2.json .cache/data-completion/classifications.json .cache/data-completion/classifications-audit.json
```

- 출력 `fish-classifications.kr.v1.json`: KR 일반 도감 1,258종의 명시적 터주왕/터주 설명과 FishingNoteInfo.SpecialConditions=2 양성 집합을 개발 중 대조한 ID 자료다. 설명 전문·아이콘을 내보내지 않는다.
- 결과: Inspected=1258 / SourceCompared=1258 / PositiveTimingCompared=131 / BigIncludingLegendary=335 / Legendary=36 / Failed=0. 등급 ID 양성 집합은 이전과 동일하다. 다른 빌드에 플래그 의미를 일반화하지 않는다.
- 조건 입력 해시는 LF 정규화하며 분류 버전은 조건 버전에서 파생한다. 분류 파일 SHA256: `756D46948B20A44F5695465CDBEC42E63EAC355F1BD9B372D9AEADDC98048DF2`. 검토 후 출력 파일을 제품 경로에 복사한다.

## 필드 의미와 미확인

- `baitSteps` 바깥 배열은 미끼→생미끼 순서, 내부 배열은 같은 단계 대체 후보다. 추가 자료는 검증된 한 경로를 선택하며 가능한 모든 미끼를 보장하지 않는다.
- `hookset`/`tug` 빈 문자열, `intuitionSeconds=null`은 해당 필드 미확인이다. 낚아채기를 입질 강도에서 추정하거나 직감 지속 시간을 출현 시간 길이로 대체하지 않는다.
- 빈 날씨 배열은 확인된 원본에서만 제한 없음이다. `conditionsKnown=false`인 2종은 미끼·입질 강도·위치만 안내하며 상시 예측/알림으로 바꾸지 않는다.
- `fishEyesSupported`는 nullable이다. 기존 903종의 bool은 유지하고 추가 자료에 없는 355종은 null로 둔다. 사용자 결정으로 현재 UI에는 표시하지 않는다. 개인 스킬을 기본 예측에 적용하지 않는다.
- `patchVersion`은 3.55b 같은 접미사를 보존한다. 기존 숫자 `patch`도 유지하며 기존 903종은 숫자 표시를 사용한다. 전승록은 KR GatheringSubCategory.Item으로 연결한다.
- `snaggingRequired`는 갈고리 낚시, `lure`는 Modest/Ambitious 요구다. 직감 선행 어종/마릿수는 지속 시간 누락과 별개로 안내한다.
- 먼바다 259종의 기본 해역·일반/환해류 장소·항로 방문 시간은 IKD 시트에서 제공한다. 어종별 미끼·특수 조건은 별도 연동이 남았다. 작살 203건 중 `conditionsKnown=false` 115건과 전용 UI·수집 연동도 후속이다. [전체 ID/이름/이유](../docs/reports/fishing-data-2026-10-17/remaining.md)에 분리했다.
- 모든 선택 낚시터는 한섭 지도 시트와 대응한다. 외부 좌표와 다른 294/295/296/327번은 한섭 좌표를 쓰며 실기 차이 확인은 대기다.

빌드/자료 검사와 인게임 확인은 구분한다. [이번 실행 근거와 남은 확인](../docs/validation/DATA-COMPLETION-2026-10-18.md)을 따른다.
