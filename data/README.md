# 초기 한섭 낚시 조건 데이터

`fishing.kr.v1.json`은 0.1.5.0에서 사용하는 10종의 조건 부분집합이다. 입질 초 수 통계는 포함하지 않는다. 이름·아이콘·지역·낚시터·지도·날씨 확률은 내장하지 않고 설치된 한섭 클라이언트에서 읽는다.

## 출처와 가공

- 원본: https://github.com/icykoneko/ff14-fish-tracker-app
- 고정 revision: `d0e1ab5bba62c2b57356e690a73676cd4751b64d`.
- 조건과 이름을 검토한 파일: `private/fishData.yaml`.
- 숫자 ID와 가공된 조건을 대조한 파일: `js/app/data.js`.
- 10종만 선별해 schemaVersion/dataVersion/region/gameBuild/source 메타데이터를 붙였다. sourceName은 원본 대조용이며 UI 이름은 한섭 시트를 사용한다.
- 시작/종료 ET, 현재/직전 날씨 ID, 미끼→생미끼 ID 순서, 낚아채기, 입질 강도, 직감 선행 어종·마릿수/지속 시간, 전승록/물고기의 눈 정보를 추출했다. 실제 캐릭터의 직감/스킬 상태를 이 데이터에서 추론하지 않는다.
- 원본의 추천 경로를 모든 가능한 미끼의 완전한 목록으로 표시하지 않는다. 추가 조건 도입 시 미지원 필드의 의미와 실제 한섭 적용 여부를 검토한다.
- MIT 고지는 루트 `THIRD-PARTY-NOTICES.txt`에 보존하고, JSON과 함께 주 DLL에 내장한다. UI의 출처/라이선스에서 고지를 복사할 수 있다. 외부 이미지나 전체 원본 데이터셋은 재배포하지 않는다.

## 한섭 대조 범위

`2026.10.06.0000.0000` 설치본에서 10종 모두 FishParameter ID ↔ Item ID ↔ FishingSpot ID가 일치하고 미끼 Item ID/이름이 존재함을 확인했다. 지도 좌표는 한섭 FishingSpot X/Z와 Map.SizeFactor로 계산해 원본 map_coords와 0.00001 이내 일치함을 확인했다. 이는 정적 데이터 대조이며 실제 어획 조건의 인게임 검증 완료가 아니다.

수정 시 0이 아닌 ID, 중복 어종, 시작/종료 시간, 날씨 ID, 미끼 순서/직감 요구 수, KR 지원 빌드를 확인한다. 런타임에는 지원 빌드와 개별 ID/참조가 맞지 않으면 해당 상세 조건을 연결하지 않는다. 자료가 없는 어종은 일반 목록과 가능한 낚시터 안내에 남는다.

## 데이터 구조

- dataset: schemaVersion, dataVersion, region, gameBuild, sourceRepository/sourceRevision/sourceFiles, fish.
- fish: itemId, fishParameterId, fishingSpotId, sourceName, startHour/endHour, weatherIds/previousWeatherIds, baitPath, hookset/tug, predators, intuitionSeconds, folkloreRequired, fishEyesSupported.
- 빈 날씨 배열은 이번 대조된 원본에서 제한 없음이다. 임의의 미입력 자료를 빈 배열로 변환하지 않는다.
- baitPath는 선별한 10종의 단일 추천 경로다. 대체 미끼의 중첩 배열 등 다른 원본 형태를 도입할 때는 스키마를 명시적으로 확장한다.

재현 수치와 게임 확인 절차는 [상세 정보 검증](../docs/validation/MVP-01-DETAILS.md)을 따른다.
