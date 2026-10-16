# 열린 창 배경과 바다구렁이 예측 차이 조사

날짜: 2026-10-16 · Plugin 0.1.19.0 · 별도/정규 빌드 통과 · 인게임 확인 대기

## 사용자 보고와 범위

- 0.1.18.0 남은 시간 정렬은 사용자 ‘정렬은 잘 됨’으로 대표 확인 통과했다. 예측 차이를 정렬 실패로 해석하지 않는다.
- 바다구렁이(Item 24215/FishParameter 670, Argonautica)의 Teamcraft 화면은 파란 활성 표시/16:07, Plugin은 시작까지 약 6시간 28분/현지 10-16 20:46을 표시했다. 같은 대화에서 양쪽 화면을 받았으나 캡처의 정확한 UTC 시각은 없다.
- Teamcraft를 새로고침한 뒤에도 활성으로 표시된다는 응답을 받았다. 단순 새로고침으로 해결됐다고 기록하지 않는다.
- 사용자는 현재 열린 어종의 배경 구분도 요청했다. 이번 버전은 표시와 짧은 진단을 추가하며 예측 공식/조건 데이터는 변경하지 않는다.

## 확인한 조건과 계산

- 양쪽 조건은 ET 종일, 현재 바람(5), 직전 쾌청/맑음(1/2), 오노코로 섬 근해(162)/홍옥해(613)/Map 371이다. Plugin은 직전 날씨를 현재 시각 이전의 날씨 구간에서 계산한다. 과거 플레이/플러그인 로드 기록을 필요로 하지 않는다.
- 로컬 KR 시트 덤프, 고정 Tracker, 고정 Teamcraft의 Map 371→WeatherRate 83 확률이 일치한다. Weather ID별 개별 확률은 9:10/5:10/3:15/2:40/1:25다. 원본 누적 확률과 개별 확률을 구분했다.
- 조사 시각 `2026-10-16T05:16:31.287Z`에 고정 Tracker 목표값 함수로 계산한 구간은 다음과 같다. 화면 캡처와 밀리초까지 같은 시각을 대조한 결과는 아니다.

| 구간 시작 UTC | 홍옥해 날씨 ID |
|---|---|
| 04:23:20 | 1 (쾌청) |
| 04:46:40 | 5 (바람) |
| 05:10:00 | 5 (바람) |
| 05:33:20 | 1 (쾌청) |

- 직전 8 ET 시간 구간을 검사하면 조사 시각은 바람→바람이므로 제한 창이 닫힌 상태다. 다음 맑음/쾌청→바람은 `11:46:40Z~12:10:00Z`(KST 20:46:40~21:10:00)로 계산된다. 직전 날씨 데이터 누락으로 계산하지 못한 상태와 구분한다.
- 고정 Teamcraft의 `WeatherService.getNextWeatherTransition`과 `AlarmStatusService.findWeatherSpawnCombination` 함수 본문을 개발 도구의 JavaScript에서 실행했다. 같은 시각/Map/조건에서 시작 `11:46:40.016Z`, 종료 `12:10:00.016Z`를 반환했다. 날짜 덧셈/뺄셈 도우미와 상수/확률표만 주입했으며 UI·Angular·실제 배포 사이트 전체를 실행하지 않았다. 함수 추출의 첫 실행은 호출 위치를 잘못 추출해 ReferenceError로 실패했고, 선언 위치 기준으로 수정한 뒤 재실행했다.
- 두 소스의 조사 계산은 다음 창 시각에 일치하지만 실제 Teamcraft 화면과의 차이 원인은 확정하지 못했다. Teamcraft 배포 버전/실제 알람 입력·캐시와 게임의 직전 날씨 처리에 대한 동시 대조가 남아 있다. Teamcraft나 Plugin 어느 쪽이 게임과 맞는지 이번 자료만으로 확정하지 않는다.

## 출처와 도입 경계

- 기존 Tracker revision `d0e1ab5bba62c2b57356e690a73676cd4751b64d`: [조건 YAML](https://github.com/icykoneko/ff14-fish-tracker-app/blob/d0e1ab5bba62c2b57356e690a73676cd4751b64d/private/fishData.yaml), [날씨 계산](https://github.com/icykoneko/ff14-fish-tracker-app/blob/d0e1ab5bba62c2b57356e690a73676cd4751b64d/js/app/weather.js).
- 기존 Teamcraft 조사 revision `8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7`: [날씨 계산](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/apps/client/src/app/core/eorzea/weather.service.ts), [출현 계산](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/apps/client/src/app/core/alarms/alarm-status.service.ts), [Map 연결](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/apps/client/src/app/core/data/sources/map-ids.ts), [날씨 확률](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/apps/client/src/app/core/data/sources/weather-index.ts), [어종 조건/알람](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/libs/data/src/lib/json/fishing-log-tracker-page-data.json).
- 두 저장소는 기존 조사한 MIT 출처다([라이선스/범위](../DATA-SOURCES.md)). 다운로드는 개발 중 읽기 전용 조사이며 `.cache/fishing-sources`에 보관한다. Teamcraft 코드/데이터를 제품에 복사하지 않았고 새 의존성/데이터 버전/런타임 HTTP는 없다.

## 이번 구현

- 목록의 Open 창에 연한 녹색 배경을 그린다. 시작 전/종료 후 표본, Upcoming/Always/미확인/먼바다는 강조하지 않는다. 선택/마우스 오버는 기존 테마의 Selectable을 위에 그려 유지한다. 아이콘/텍스트/클리핑/선택 ID는 유지한다.
- 색상과 별개로 기존 행의 ‘창 열림/종료까지’ 문구를 유지하고 한영 목록 안내에 배경의 의미를 적었다. 모든 개인 조건 충족이나 낚시 성공 표시가 아니다.
- 짧은 보고서만 summary/v4→v5: 선택 어종이 있으면 `SelectedForecast/ForecastAt/Start/End/CopiedAt` 한 줄을 추가한다. 시각은 UTC 오프셋이 있는 O 형식이며 전체 진단/반복 어종 로그는 추가하지 않는다. preferences/v5·collection/v2·catalog/v5 형식은 유지한다.

## 실행 결과와 다음 확인

- 별도 빌드: `dotnet build src/DuckbillFisher.Plugin/DuckbillFisher.Plugin.csproj --no-restore -c Debug -p:DalamudLibPath=C:/Users/hyogu/AppData/Roaming/XIVLauncherKR/addon/Hooks/dev/ -p:OutputPath=C:/Users/hyogu/Downloads/DuckbillFisher/.cache/open-window-build/` 성공, 경고·오류 0.
- `pwsh -NoLogo -NoProfile -File scripts/check-localization.ps1`: 한영 245키/형식 인자 통과. `git diff --check` 통과. 테스트 추가/실행 없음. 위 소스 실행은 실제 보고 시각의 조사 계산이다.
- 사용자 ‘언로드 완료’ 후 OutputPath 없이 정규 Debug 빌드 성공, 경고·오류 0. 주 DLL 914432바이트/0.1.19.0, Core 32256바이트/1.0.0.0, ko 리소스 27648바이트/0.1.19.0, manifest DuckbillFisher/0.1.19.0/API 15 확인.
- 인게임: 0.1.19.0을 기존 경로로 로드하고 열린 제한 창의 녹색 배경, 선택/호버 구분과 상시/예정 어종의 비강조를 확인한다. 한영/모든 테마·화면 폭 조합은 미확인이다.
- 바다구렁이를 선택하고 ‘짧은 진단’의 SelectedForecast 한 줄과 같은 시점의 Teamcraft 표시를 비교한다. 전체 진단은 요구하지 않는다. 화면 차이가 지속되면 실제 게임 ET/홍옥해 현재·직전 날씨 또는 실제 해당 어종 어획 증거를 더 확보해 원인을 구분한다.
