# 한섭 낚시 조건 데이터

`fishing.kr.v1.json`은 0.1.11.0에서 사용하는 신생 에오르제아(원본 patch 2.x) **140종**의 상세 조건이다. 스키마는 v1을 유지한다. 입질 초 수 통계는 포함하지 않는다. 이름·아이콘·지역·낚시터·지도·날씨 확률은 설치된 한섭 클라이언트에서 읽는다.

## 출처와 가공

- 원본: https://github.com/icykoneko/ff14-fish-tracker-app
- 고정 revision: `d0e1ab5bba62c2b57356e690a73676cd4751b64d`.
- 조건과 이름을 검토한 파일: `private/fishData.yaml`.
- 숫자 ID와 가공된 조건을 대조한 파일: `js/app/data.js`.
- 2.x 원본 155행 중 추천 낚시터가 없는 생미끼 보조 행 15개를 제외하고 140종을 선별했다. 제외 어종은 일반 목록/아이템 링크에 남지만 상세 낚시터를 추측하지 않는다. 모든 2.x 어종의 완전한 조건 목록을 뜻하지 않는다. sourceName은 대조용이며 UI 이름은 한섭 시트를 사용한다.
- 원본 SHA-256·KR 빌드·대상 패치 범위는 [fishing-source.json](fishing-source.json)에 고정한다. 재다운로드한 고정 원본과 기존 캐시의 해시가 같음을 확인했다.
- 시작/종료 ET, 현재/직전 날씨 ID, 미끼→생미끼 ID 순서, 낚아채기, 입질 강도, 직감 선행 어종·마릿수/지속 시간, 전승록/물고기의 눈 정보를 추출했다. 실제 캐릭터의 직감/스킬 상태를 이 데이터에서 추론하지 않는다.
- 원본의 추천 경로를 모든 가능한 미끼의 완전한 목록으로 표시하지 않는다. dataMissing·작살·유인 기술 조건·중첩 대체 미끼는 거부한다. snagging의 null/false는 요구 없음이며 true는 미지원으로 거부한다. 미입력 조건을 제한 없음으로 바꾸지 않는다.
- fishEyes는 생성된 현대 스킬 지원 플래그다. 오래된 YAML의 동일 이름 필드를 현재 스킬 요구로 복사하지 않는다. 기본 시간/날씨 예측에 개인 준비 상태나 물고기의 눈은 반영하지 않는다.
- MIT 고지는 루트 `THIRD-PARTY-NOTICES.txt`에 보존하고, JSON과 함께 주 DLL에 내장한다. UI의 출처/라이선스에서 고지를 복사할 수 있다. 외부 이미지나 전체 원본 데이터셋은 재배포하지 않는다.

## 한섭 대조 범위

`2026.10.06.0000.0000` 설치본에서 140종의 FishParameter↔Item 유일 대응·수첩 포함/먼바다 제외·추천 낚시터 연결·지도/지역·좌표 오차 0.00001 미만·미끼/직감 아이템 이름과 아이콘·날씨 이름과 아이콘·날씨 확률 합·전승록 대응·시간과 직감 형태를 대조했다. 이는 정적 대조이며 실제 어획 조건의 인게임 검증 완료가 아니다.

개발용 [FishingDataImport](../tools/FishingDataImport/Program.cs)는 로컬 파일/설치 Lumina·Excel/기존 Core를 사용하며 새 NuGet 패키지·네트워크·게임 프로세스 접근이 없다. 입력 JS를 실행하지 않고 테이블 키를 따옴표로 감싼 뒤 JSON으로 읽는다. 후보 대조 실패 시 출력하지 않는다. 런타임에도 지원 빌드와 개별 ID/참조가 다르면 상세 조건을 연결하지 않는다.

고정 revision의 `js/app/data.js`를 로컬에 확보한 뒤 저장소 루트에서 실행한다. 실제 데이터 교체 전에는 `.cache` 출력으로 차이를 검토한다.

```powershell
dotnet run --project tools/FishingDataImport -p:DalamudLibPath=C:/Users/hyogu/AppData/Roaming/XIVLauncherKR/addon/Hooks/dev/ -- data/fishing-source.json .cache/fishing-sources/data.js "C:/Program Files (x86)/FINAL FANTASY XIV - KOREA/game" .cache/fishing-expanded.json .cache/fishing-expanded-audit.json
```

## 데이터 구조

- dataset: schemaVersion, dataVersion, region, gameBuild, sourceRepository/sourceRevision/sourceSha256/sourceFiles, fish.
- fish: itemId, fishParameterId, fishingSpotId, sourceName, startHour/endHour, weatherIds/previousWeatherIds, baitPath, hookset/tug, predators, intuitionSeconds, folkloreRequired, fishEyesSupported.
- 빈 날씨 배열은 이번 대조된 원본에서 제한 없음이다. 임의의 미입력 자료를 빈 배열로 변환하지 않는다.
- baitPath는 단일 추천 경로다. 대체 미끼의 중첩 배열 등 다른 원본 형태를 도입할 때는 스키마를 명시적으로 확장한다.

이번 실행 결과와 게임 확인은 [확대 검증 기록](../docs/validation/MVP-01-DATA-EXPANSION.md), 초기 도입은 [상세 정보 검증](../docs/validation/MVP-01-DETAILS.md)을 따른다.
