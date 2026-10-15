# 낚시 상세 데이터의 출처와 조회 계획

조사일: 2026-10-16. **출처 후보와 구현 계획**이다. 외부 어종 데이터를 제품에 도입하거나 조회 API의 동작/이용 권한을 검증한 상태는 아니다. 플러그인의 런타임 외부 HTTP 금지 원칙은 유지한다.

## 제공할 정보와 단계

| 정보 | 표현과 구현 시점 |
|---|---|
| 미끼·생미끼 | 권장/대체 미끼와 생미끼 순서를 구분해 MVP-01 상세 정보에 표시 |
| 낚아채기 | 섬세/강력한 낚아채기와 적용 조건, 입질 강도를 서로 다른 필드로 표시. MVP-01 |
| 입질 시간 | 사용한 미끼·낚시터·측정 기준·스킬 조건을 포함한 초 단위 관측 범위/통계. MVP-01은 안내값, MVP-02는 관찰 경과 타이머와 함께 표시 |
| 출현 조건 | ET 시간대·현재/직전 날씨·직감 대상과 마릿수·전승록 등 검증된 조건. MVP-01에 정적 안내 |
| 출현까지 남은 시간 | 조건 데이터와 시간/날씨 계산으로 다음 창 시작/종료·남은 실제 시간을 MVP-03에서 계산. 시간순 정렬과 연결 |
| 지도 위치 | 낚시터별 지도 ID/지역/좌표를 확인한 뒤 사용자 클릭으로 지도 열기와 핀 표시. MVP-01 |

입질 시간은 모든 시도에 적용되는 확정 초 수로 표시하지 않는다. 관측 최소/최대·평균·분포/분위수는 서로 다른 값이며 실제로 얻은 통계만 사용한다. 표본 수와 단위, 시전/생미끼 기준, 버프·스킬·미끼 조건이 확인되지 않으면 미확인으로 둔다. 관측상 어획이 없던 시간/날씨만으로 출현 불가 조건을 확정하지 않는다.

다음 시간/날씨 창을 예측할 수 있어도 캐릭터의 직감·장비·GP·선행 어획 등 개인 조건까지 충족했다고 표현하지 않는다. 미지원 조건이 있으면 해당 조건을 함께 표시한다. 정해진 탐색 범위에서 다음 창을 못 찾으면 검색 범위를 표시하고 영구 출현 불가로 해석하지 않는다.

## 조사한 소스

### Teamcraft

- 저장소: https://github.com/ffxiv-teamcraft/ffxiv-teamcraft
- 고정 커밋: `8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7`.
- [fish-data.service.ts](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/apps/client/src/app/pages/db/service/fish-data.service.ts)에서 미끼/생미끼, 낚아채기/입질 강도, 미끼별 입질 시간, 시간·날씨 집계 조회 경로를 확인했다.
- [fish-data.gql.ts](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/apps/client/src/app/pages/db/service/fish-data.gql.ts)의 GraphQL은 itemId/spot/baitId와 flooredBiteTime/occurences를 조회한다. 낚시터별/미끼별 시간 버킷과 횟수 형태이며, 실제 조회 응답·서비스 가용성·단위/측정 기준은 아직 검증하지 않았다. 일부 쿼리는 최소 관측 횟수 필터를 적용하므로 전체 관측 원본과 같다고 가정하지 않는다.
- [입질 시간 화면](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/apps/client/src/app/pages/db/fish/fish-bite-times/fish-bite-times.component.ts)은 시간 버킷의 최소/최대와 횟수 가중 평균을 계산한다. 그대로 정확한 확정 구간으로 취급하지 않는다.
- [Hookset enum](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/libs/types/src/lib/data/hookset.ts)은 NORMAL/POWERFUL/PRECISION을 구분한다. 데이터 원본의 값 매핑은 고정 커밋 기준으로 검증해 변환한다.
- [저장소 라이선스](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/LICENSE)는 MIT, 저작권 표기는 Flavien Normand(2017)다. 이 사실만으로 호스팅 통계 API의 외부 이용/대량 조회/재배포 조건까지 확인했다고 해석하지 않는다. 통계 데이터를 내장하기 전 해당 조건과 출처 표기를 별도로 확인한다.

### FFX|V Fish Tracker / Carbuncle Plushy

- 저장소: https://github.com/icykoneko/ff14-fish-tracker-app
- 고정 커밋: `d0e1ab5bba62c2b57356e690a73676cd4751b64d`.
- [fishData.yaml](https://github.com/icykoneko/ff14-fish-tracker-app/blob/d0e1ab5bba62c2b57356e690a73676cd4751b64d/private/fishData.yaml)에서 startHour/endHour, previousWeatherSet/weatherSet, bestCatchPath, predators, tug, hookset, folklore, fishEyes, patch 필드를 확인했다. 구조화된 출현 조건의 후보이며, 전체 어종의 완전한 입질 시간 통계까지 제공한다고 확인한 것은 아니다.
- [데이터 템플릿](https://github.com/icykoneko/ff14-fish-tracker-app/blob/d0e1ab5bba62c2b57356e690a73676cd4751b64d/private/fishDataTemplate.yaml)에는 dataMissing 상태가 있다. 미입력과 제한 없음을 같은 의미로 취급하지 않는다.
- [저장소 라이선스](https://github.com/icykoneko/ff14-fish-tracker-app/blob/d0e1ab5bba62c2b57356e690a73676cd4751b64d/LICENSE)는 MIT, 저작권 표기는 icykoneko(2019)다. 제품 도입 시 원본 URL/커밋/가공 내역과 필요한 라이선스 고지를 포함한다. 게임 추출물/이미지/하위 모듈의 권리까지 동일하다고 확대하지 않는다. 이미지는 현행 한섭 클라이언트에서 읽는 방식을 유지한다.

### XIVAPI v2와 한섭 클라이언트

- [공식 문서](https://v2.xivapi.com/docs/welcome/)에 따르면 XIVAPI는 게임 클라이언트 파일에 있는 데이터를 제공한다. 낚시 커뮤니티의 관측 통계를 전부 대체하는 소스로 가정하지 않는다.
- [에디션/언어 설명](https://v2.xivapi.com/docs/guides/)은 제공 데이터가 글로벌 클라이언트 기준임을 명시한다. 한섭 이름·지원 패치·행의 존재는 설치된 한섭 클라이언트의 시트를 우선한다.
- [로컬 파일 접근 안내](https://v2.xivapi.com/docs/software/)도 게임 설치에 접근할 수 있다면 로컬 데이터 라이브러리 사용을 권한다. 현행 IDataManager/Lumina 경로를 유지하고 불필요한 런타임 웹 조회를 추가하지 않는다.

## 권장 구성과 다음 작업

1. 한섭 시트로 FishParameter ID ↔ Item ID, 낚시터 ↔ 지도/좌표를 확인한다. 글로벌 소스의 이름을 제품 식별자로 쓰지 않고 ID를 대조한다.
2. Fish Tracker의 검증된 출현 조건/미끼/낚아채기를 가공하는 내장 데이터 구조를 준비한다. 조건 데이터가 없는 어종은 목록에 계속 표시한다.
3. Teamcraft 입질 통계의 외부 조회/재배포 조건과 실제 응답·단위·필터를 확인한다. 확인되기 전 임의 통계나 초 수를 데이터에 채우지 않는다.
4. 1~2종의 한섭 ID·낚시터·미끼·조건을 대조한 뒤 5~10종으로 확대한다. 출처는 필드별로 추적할 수 있게 한다.
5. 검증된 내장 스냅샷을 로드/캐시하고, 시간/날씨 예측은 추후 Core에서 계산한다. 게임 입력 자동화와 원격 백엔드는 추가하지 않는다.

이번 조사는 저장소 원문과 공식 문서의 읽기 전용 확인이다. 다운로드 원문은 추적 제외 `.cache/fishing-sources/`에만 보관했다. 제품 데이터/코드를 가져오거나 Plugin을 빌드·변경하지 않았으며 테스트를 실행하지 않았다.
