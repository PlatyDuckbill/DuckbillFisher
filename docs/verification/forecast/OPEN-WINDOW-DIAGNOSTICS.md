# 열린 창 배경과 바다구렁이 예측 차이 조사

갱신: 2026-10-19 · 과거 화면 원인 미확정·추적 중단. 0.1.19.0 배경 사용자 대표 확인은 유지한다.

2026-10-16에 사용자 요청으로 보류했던 조사를 2026-10-18의 명시 요청으로 재개했다. 아래는 기존 관찰과 새 계산을 분리한 기록이다.

2026-10-19 사용자 방향 전환: 당시 Teamcraft의 개별 상세와 등록 알람 양쪽에 차이가 있었다는 응답을 받았으나, 과거 사실 자료가 부족하므로 추가 원인 추적을 중단했다. 이후 작업은 [현재 두 사이트의 실제 표시와 Core 계산 대조](FORECAST.md#현재-웹-표시-대조-2026-10-19)다. 현재 일치를 과거 문제의 해결로 해석하지 않으며 캐시 가설 추적을 자동 재개하지 않는다.

## 0.3.2.0 재조사 (2026-10-18)

- 기존 보고의 Item24215/FishParameter670 바다구렁이를 대상으로 했다. 사용자에게 다른 어종이면 이름을 알려 달라고 요청했으며, 다른 어종의 추가 시각/정보는 아직 없다.
- 고정 revision의 WeatherService/AlarmStatusService/AlarmsFacade를 원격 raw 파일과 다시 대조해 로컬 조사 사본의 LF 정규화 일치를 확인했다. 새 원본/의존성/조건 데이터는 제품에 도입하지 않았다.
- 설치 KR 시트의 실제 확률표와 현재 Core DLL로 아래 세 시각을 계산했다. Teamcraft는 고정 소스의 날씨 함수와 `getAlarmStatus` 경로까지 함수 본문을 추출해 별도 실행했다. ET 상수·Date 연산 도우미·같은 확률표를 주입했으며 실제 Angular/데스크톱 UI를 실행한 결과는 아니다. 시각은 UTC이며 표의 Teamcraft 시각은 초 단위로 표시한다(남는 ET 밀리초로 13~41ms 차이).

| 관찰 시각 | Plugin | Teamcraft 계산 | 현재/다음 출현 |
|---|---|---|---|
| 2026-10-16 04:50:00 | Open | spawned=true | 04:46:40~05:10:00 |
| 2026-10-16 05:32:05.689793 | Upcoming | spawned=false | 11:46:40~12:10:00 |
| 2026-10-16 05:34:00 | Upcoming | spawned=false | 11:46:40~12:10:00 |

- 첫 바람 구간은 양쪽 계산에 나타나며, 두 번째 바람 구간은 직전 날씨도 바람이라 제외된다. 05:32의 다음 출현은 같은 날 11:46이므로 기존 7일 제한이 이 불일치의 원인은 아니다. 바다구렁이는 ET 종일 조건이고 Teamcraft의 Fish Eyes 처리도 직전 날씨를 제거하지 않으므로 물고기의 눈 시간 제한 해제로도 이번 차이를 설명할 수 없다.
- Teamcraft의 일반 캐시 만료는 계산된 시작/종료 경계의 실제 3초 뒤다. 정상 입력의 캐시 지연만으로 05:10 종료 후 05:32까지의 활성 표시는 설명되지 않는다. 캐시 키에 weathersFrom/mapId/duration 등이 없는 것은 여전히 코드상 위험 후보지만, 당시 다른 입력과의 충돌/실제 캐시 값은 확보하지 못했다. 기존 등록 알람의 직전 조건 누락 가설은 사용자 확인으로 제외한 상태를 유지한다.
- 결론: 같은 입력에서 계산은 일치한다. **실제 Teamcraft 화면의 추가 활성 구간 원인은 미확정**이며 Plugin이 첫 구간을 누락했다는 근거도 없다. 두 번째 바람까지 임의로 연장하거나 조건 데이터를 바꾸지 않았다. 이후 재현 시에는 같은 시점의 Teamcraft 런타임 입력/표시와 게임 날씨 근거가 필요하다.
- 개선: summary/v6→v7. 기존 SelectedForecast는 보존하고 `SelectedConditions` 한 줄에 Spot/Territory/Map/Known/StartET/EndET/Weather/PreviousWeather/Rates/HorizonDays/FishEyes를 추가했다. 숫자는 invariant, Rates는 원래 확률 순서의 `ID:개별확률` 목록이다. 선택 어종의 입력과 ForecastAt으로 외부 차이를 재현할 수 있으며 표시 언어/개인 수집 정보에 의존하지 않는다. catalog/v6에도 horizonDays/fishEyes를 추가했다.
- 별도→정규 빌드와 리소스 검사는 [30일 확대 기록](FORECAST.md#0320-검색-범위-확대-2026-10-18)을 따른다. 이번 조사 계산은 당시 실제 자료/시각 재현이며 테스트 추가/실행·게임 실기·새 배포가 아니다.

## 0.1.19.0 당시 관찰

## 사용자 보고와 범위

- 0.1.18.0 남은 시간 정렬은 사용자 ‘정렬은 잘 됨’으로 대표 확인 통과했다. 예측 차이를 정렬 실패로 해석하지 않는다.
- 바다구렁이(Item 24215/FishParameter 670, Argonautica)의 Teamcraft 화면은 파란 활성 표시/16:07, Plugin은 시작까지 약 6시간 28분/현지 10-16 20:46을 표시했다. 같은 대화에서 양쪽 화면을 받았으나 캡처의 정확한 UTC 시각은 없다.
- Teamcraft를 새로고침한 뒤에도 활성으로 표시된다는 응답을 받았다. 단순 새로고침으로 해결됐다고 기록하지 않는다.
- 후속 진단은 `ForecastAt=2026-10-16T05:32:05.6897930Z`, `CopiedAt=05:32:05.9028015Z`, Upcoming, 창 `11:46:40Z~12:10:00Z`다. 계산/복사 간격은 약 0.213초이며 시작까지 약 6시간 14분이다. 사용자는 같은 14:32(KST) Teamcraft 활성/Plugin 대기를 명시적으로 확인했다. Plugin 보고서만으로 외부 사이트와 대조했다고 확대하지 않는다.
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
- 14:33 이후 실제 웹사이트의 main에서 참조한 배포 자산도 읽기 전용으로 조사했다. 웹 데이터의 24215는 spot 162/weathers [5]/weathersFrom [1,2]이며, 웹 날씨 Map 371→Rate 83과 누적 확률도 위 자료와 같다. 웹 출현 계산에도 직전 날씨 검사 경로가 있다. 데이터가 같다는 사실만으로 UI 실행 결과까지 검증했다고 표현하지 않는다.
- 사용자는 설치한 데스크톱 앱이라고 확인했다. 로컬 설치에 11.4.31/11.4.32가 있고, 11.4.32 package.json과 exe 11.4.32.49846을 확인했다. 실행 프로세스의 경로/버전은 접근 가능한 메타데이터에서 얻지 못했으므로 실행 버전을 단정하지 않는다. app.asar의 프로그램 코드/정적 데이터만 읽었으며 계정/사용자 저장소와 설치 파일은 수정하지 않았다.
- 11.4.32의 바다구렁이 조건/홍옥해 확률표/직전 날씨 계산은 위 자료와 같다. 11.4.31에서도 getStatus/findWeatherSpawnCombination 함수 본문이 11.4.32와 일치함을 확인했다. 단순 설치 버전 차이나 누락된 내장 직전 조건은 현재 근거로 확인되지 않는다.
- 조사한 원인 후보: Teamcraft getStatus 캐시 키는 itemId/bnpcName/zoneId/spawns/weathers만 포함하고 weathersFrom은 포함하지 않는다. 기존 알람의 직전 조건이 비었다면 상세 페이지와 캐시를 공유할 수 있지만, 사용자가 등록 알람의 직전 날씨도 맑음/쾌청이라고 확인했다. 따라서 이번 건의 ‘기존 알람 직전 조건 누락’ 가설은 제외한다. 캐시 키의 구성만으로 실제 화면 차이를 설명했다고 기록하지 않는다.
- 후속 응답에서 현재 Teamcraft의 대기 값은 Plugin과 같아졌으나 앞서 Teamcraft에만 활성 구간이 있었다고 재확인했다. 카운트다운이 정지했는지에 대한 명시적 답변은 없으므로 정지 원인으로 확정하지 않는다. 현재 일치를 과거 불일치의 해소/오인으로 해석하지 않는다.
- 재현 대상으로 좁힌 구간은 `04:46:40Z~05:10:00Z`의 첫 바람과 `05:10:00Z~05:33:20Z`의 두 번째 바람이다. 확보한 05:32 진단은 두 번째 구간에서의 차이를 증명한다. Plugin이 첫 구간까지 놓쳤다는 근거는 없다. 반복 날씨의 창 종료/갱신 경계와 Teamcraft 런타임 입력·캐시를 다음 조사 대상으로 두며, 직전의 다른 날씨를 찾아 조건을 연장하도록 공식을 추측 변경하지 않는다.

## 출처와 도입 경계

- 기존 Tracker revision `d0e1ab5bba62c2b57356e690a73676cd4751b64d`: [조건 YAML](https://github.com/icykoneko/ff14-fish-tracker-app/blob/d0e1ab5bba62c2b57356e690a73676cd4751b64d/private/fishData.yaml), [날씨 계산](https://github.com/icykoneko/ff14-fish-tracker-app/blob/d0e1ab5bba62c2b57356e690a73676cd4751b64d/js/app/weather.js).
- 기존 Teamcraft 조사 revision `8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7`: [날씨 계산](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/apps/client/src/app/core/eorzea/weather.service.ts), [출현 계산](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/apps/client/src/app/core/alarms/alarm-status.service.ts), [Map 연결](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/apps/client/src/app/core/data/sources/map-ids.ts), [날씨 확률](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/apps/client/src/app/core/data/sources/weather-index.ts), [어종 조건/알람](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/libs/data/src/lib/json/fishing-log-tracker-page-data.json).
- 두 저장소는 기존 조사한 MIT 출처다([라이선스/범위](../../development/DATA-SOURCES.md)). 다운로드는 개발 중 읽기 전용 조사이며 `.cache/fishing-sources`에 보관한다. Teamcraft 코드/데이터를 제품에 복사하지 않았고 새 의존성/데이터 버전/런타임 HTTP는 없다.

실제 웹 배포물의 조사 사본은 아래 URL과 SHA256으로 구분한다. 고정 Git revision과 동일 배포판이라고 추정하지 않는다.

| 자산 | SHA256 |
|---|---|
| [main-YCJECEOM.js](https://cdn.ffxivteamcraft.com/main-YCJECEOM.js) | C1018579F37E0F86C507CAA78131C48A1EE115EBCBC33F475919BA9F256B48A9 |
| [날씨/확률표 chunk-4WK6JWR7.js](https://cdn.ffxivteamcraft.com/chunk-4WK6JWR7.js) | 70A64BFEAEDB30A6AD84AAFC2D66A8970908B7E11C43887E3B11B54DAE5719D4 |
| [출현/알람 chunk-QSMN4NC3.js](https://cdn.ffxivteamcraft.com/chunk-QSMN4NC3.js) | 2BA28ACB9D5868289FDE6CC03DE781887533A47D279EC5BE4AA42E7B706ADAA9 |
| [fishing-sources.c3e3368eeb04801c006a7d076a286ab49492f077.json](https://cdn.ffxivteamcraft.com/assets/data/fishing-sources.c3e3368eeb04801c006a7d076a286ab49492f077.json) | E12D5AB9DA91BF62EF77B9987C84E288B750F77B861CE1F43EB28CD57A83CF4B |

설치 앱은 app-11.4.32/resources/app.asar에서 다음 가상 경로를 읽었다. 가공은 압축 해제 없이 ASAR 헤더/해당 파일 범위 읽기이며 사본만 개발 캐시에 저장했다. 프로그램 코드를 제품에 도입하지 않는다.

| 설치 리소스 경로 | SHA256 |
|---|---|
| dist/apps/client/chunk-R334MS6I.js (날씨) | E469ACE89D0133EBC02E03CD0AEF2789F01D43CAB2F98E02F03632853FCD8FEC |
| dist/apps/client/chunk-TWZ3TP6M.js (출현/캐시) | 4F1EC6AFB5F6247147E8AF2C4B576CF9AA22FB26C6BBF0319264AF670518C7B3 |

캐시 키의 고정 소스 근거는 [alarms.facade.ts](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/apps/client/src/app/core/alarms/+state/alarms.facade.ts), 등록 아이콘은 [alarm-button.component.html](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/apps/client/src/app/modules/alarm-button/alarm-button/alarm-button.component.html)이다. 설치 리소스에서도 해당 키 구성을 확인했다.

## 이번 구현

- 목록의 Open 창에 연한 녹색 배경을 그린다. 시작 전/종료 후 표본, Upcoming/Always/미확인/먼바다는 강조하지 않는다. 선택/마우스 오버는 기존 테마의 Selectable을 위에 그려 유지한다. 아이콘/텍스트/클리핑/선택 ID는 유지한다.
- 색상과 별개로 기존 행의 ‘창 열림/종료까지’ 문구를 유지하고 한영 목록 안내에 배경의 의미를 적었다. 모든 개인 조건 충족이나 낚시 성공 표시가 아니다.
- 짧은 보고서만 summary/v4→v5: 선택 어종이 있으면 `SelectedForecast/ForecastAt/Start/End/CopiedAt` 한 줄을 추가한다. 시각은 UTC 오프셋이 있는 O 형식이며 전체 진단/반복 어종 로그는 추가하지 않는다. preferences/v5·collection/v2·catalog/v5 형식은 유지한다.

## 실행 결과와 다음 확인

- 별도 빌드: `dotnet build src/DuckbillFisher.Plugin/DuckbillFisher.Plugin.csproj --no-restore -c Debug "-p:DalamudLibPath=${env:DALAMUD_HOME}/" "-p:OutputPath=${env:DUCKBILL_ROOT}/.cache/open-window-build/"` 성공, 경고·오류 0.
- `pwsh -NoLogo -NoProfile -File scripts/check-localization.ps1`: 한영 245키/형식 인자 통과. `git diff --check` 통과. 테스트 추가/실행 없음. 위 소스 실행은 실제 보고 시각의 조사 계산이다.
- 사용자 ‘언로드 완료’ 후 OutputPath 없이 정규 Debug 빌드 성공, 경고·오류 0. 주 DLL 914432바이트/0.1.19.0, Core 32256바이트/1.0.0.0, ko 리소스 27648바이트/0.1.19.0, manifest DuckbillFisher/0.1.19.0/API 15 확인.
- 사용자 ‘배경 로드 문제 없음’으로 로드/배경 표시 대표 확인 통과. 선택/호버·상시 비강조·한영/모든 테마·폭 조합의 개별 확인까지 확대하지 않는다. 이번 후속은 문서와 개발 중 웹/설치 리소스 조사이며 빌드/리소스 검사/테스트를 재실행하지 않았다.
- SelectedForecast 포함 진단과 같은 시점 Teamcraft 활성 응답, 설치 앱/등록 알람 직전 조건, 이후 두 대기 값 일치를 확보했다. 다음은 위 반복 날씨 구간의 창 종료/갱신 및 런타임 입력·캐시 차이 조사다. 추가 보고서/배경/알람 조건 확인은 반복 요청하지 않고 알람 삭제/설정 변경도 요구하지 않는다. 필요하면 다음 재현에서 실제 게임 근거를 별도로 확보한다. 예측 차이는 미해결로 유지한다.
