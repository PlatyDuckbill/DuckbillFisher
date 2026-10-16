# 한섭 낚시 조건 데이터

0.1.12.0의 내장 데이터는 **`fishing.kr.v2.json`**이다. 일반 낚시 원본 903종과 별도 작살 자료 203건을 포함한다. 기존 `fishing.kr.v1.json`은 0.1.11.0/과거 예측 검증 입력의 이력이며 현재 제품은 읽지 않는다. 입질 초 수 통계는 포함하지 않는다.

## 출처와 재현

- 원본: https://github.com/icykoneko/ff14-fish-tracker-app
- 고정 revision: `d0e1ab5bba62c2b57356e690a73676cd4751b64d`, MIT 고지는 루트 `THIRD-PARTY-NOTICES.txt`에 보존한다.
- `private/fishData.yaml`의 미확인 표시와 `js/app/data.js`의 숫자 ID/가공 조건을 함께 읽는다. 두 입력 해시·지원 빌드·dataset 버전은 `fishing-source.json`에 고정한다.
- kr은 한섭 ID/빌드 검수 대상이다. 이름/아이콘/날씨 확률/지도/스킬/전승록은 실행 시 클라이언트 시트를 사용하며 sourceName 영문을 UI 이름으로 쓰지 않는다. 글로벌 호환성 보장은 아니다.
- 개발 도구 `tools/FishingDataImport`는 로컬 파일/설치 Lumina·Excel/기존 Core만 사용한다. 입력 JS를 실행하지 않으며 네트워크/게임 프로세스/새 패키지에 의존하지 않는다.

고정 원본을 확보한 뒤 저장소 루트에서 실행한다. `.cache` 출력을 검토한 후 제품 데이터로 반영한다.

```powershell
dotnet run --project tools/FishingDataImport -p:DalamudLibPath=C:/Users/hyogu/AppData/Roaming/XIVLauncherKR/addon/Hooks/dev/ -- data/fishing-source.json .cache/fishing-sources/data.js .cache/fishing-sources/tracker-fishData.yaml "C:/Program Files (x86)/FINAL FANTASY XIV - KOREA/game" .cache/fishing-all.json .cache/fishing-all-audit.json
```

## 범위와 의미

- 전체 원본 1110건에서 일반 도감 903종/작살 203건 대응, 일반 도감과 대응하지 않는 4건 제외. 제외 사유와 좌표 차이 4곳은 감사 JSON에 기록한다. 일반 참조 대조 실패를 제한 없음으로 변환하지 않는다.
- 일반 903종 중 추천 낚시터가 있는 872종은 기본 시간/날씨 예측 대상이다. 나머지 31종도 알려진 미끼/메타데이터를 표시하되 예측하지 않는다. 원본에 없는 일반 355종의 상세를 추측하지 않는다.
- baitSteps의 바깥 배열은 미끼→생미끼 순서, 각 내부 배열은 같은 단계의 대체 후보다. 가능한 모든 미끼의 완전한 목록을 뜻하지 않는다.
- hookset/tug의 빈 문자열과 intuitionSeconds=null은 해당 필드 미확인이다. 빈 날씨 배열은 조건을 확인한 원본에서만 제한 없음이다. conditionsKnown=false이면 시간/날씨를 제한 없음으로 안내하지 않는다.
- snaggingRequired는 KR 갈고리 낚시, lure는 Modest/Ambitious 요구다. fishEyesSupported는 현대 물고기의 눈 지원 여부이며 기본 예측에는 적용하지 않는다. 전승록은 실제 KR GatheringSubCategory.Item으로 연결한다.
- 작살 gig=UNKNOWN/원본 dataMissing 115건을 보존한다. predators를 일반 직감이나 특수 지점 출현으로 임의 확정하지 않는다. 작살 UI/수집 연동은 후속이다.
- 먼바다 259종의 해역·일반/환해류 장소·항로 방문 시간은 클라이언트 IKD 시트에서 읽는다. 어종별 미끼/시간 제한과 동일하지 않으며 현재 외부 원본 상세에는 없다.
- 낚시터 ID/지역/Item을 대조하고 핀 좌표는 한섭 시트로 계산한다. 외부 좌표와 다른 294/295/296/327번 낚시터는 인게임 확인 대기로 남긴다.

예측은 기본 시간/날씨만 계산하며 개인의 직감·장비·스킬 상태와 모든 선행 어종 조건을 추론하지 않는다. [이번 검증과 게임 절차](../docs/validation/MVP-01-ALL-SOURCE-DATA.md)를 따른다.

## 터주 등급 자료 (0.1.15.0)

- `fish-classifications.kr.v1.json`은 조건 원본을 바꾸지 않는 별도 양성 분류 자료다. KR 빌드 `2026.10.06.0000.0000`의 일반 도감 1258종을 조사해 터주 335종(터주왕 36종 포함)의 FishParameter/Item ID 쌍을 보관한다. 이름·설명 전문·아이콘은 내보내지 않는다.
- 개발 단계에서 KR Item.Description의 명시적 ‘터주왕’/‘터주’ 문구를 순서대로 확인하고 FishingNoteInfo.SpecialConditions=2 집합과 대조했다. 기존 고정 원본의 903종 bigFish 값과 불일치 0이다. SpecialConditions=2의 의미를 다른 빌드에 일반화하지 않는다.
- 실행 중 UI 문자열/아이템 설명/패치 번호로 등급을 추정하지 않는다. 지원 빌드와 FishParameter/Item 쌍이 일치하는 ID 자료를 연결한다. 자료가 없는 어종을 터주왕이 아니라고 확정하는 용도로 쓰지 않는다.
- 출력과 조건 입력 해시는 LF로 정규화해 OS별 줄바꿈에 의존하지 않는다. conditionDataSha256은 검수에 쓴 조건 JSON, descriptionSha256은 ID 순으로 읽은 설명 근거의 해시다. 분류 파일 SHA256은 `0C407F8A04CBFCAB53EC0A5BF8EA4B0C6DC9812C73E339E54A8BDF892ADAA31C`다.
- 시간제는 등급과 별도다. 확인된 조건의 ET/현재·직전 날씨 제약이나 검수한 KR FishingNoteInfo의 양성 제한 플래그(1)를 사용한다. raw 0/2만으로 제한 없음으로 바꾸지 않는다.

```powershell
dotnet run --project tools/FishingDataImport -p:DalamudLibPath=C:/Users/hyogu/AppData/Roaming/XIVLauncherKR/addon/Hooks/dev/ -- --classifications "C:/Program Files (x86)/FINAL FANTASY XIV - KOREA/game" data/fishing.kr.v2.json .cache/fish-classifications.json .cache/fish-classifications-audit.json
```

결과: Inspected=1258 / SourceCompared=903 / PositiveTimingCompared=131 / BigIncludingLegendary=335 / Legendary=36 / Failed=0. 출력은 검토 후 제품 자료에 복사한다. 설명 문구/시트/빌드가 달라지면 다시 조사한다.
