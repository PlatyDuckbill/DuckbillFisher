# 낚시 상세 데이터의 출처와 조회 계획

갱신: 2026-11-06. 일반 도감1,258종 모두 상세에 연결했다. 최신 개발 자료는 시간·날씨1,257종 확인/부분1종, 핵심 미확인2종(성 파스릭의 석두 직감 시간·이크에바지 가위 낚아채기·시간/날씨)이다. 작살203건/미확인115건은 보존했다. 빌드·자료 대조와 인게임 확인 대기를 구분한다. [재현](../../data/README.md), [초기 보완 근거](../verification/data/DATA-COMPLETION-2026-10-18.md), [당시 잔여 목록](../verification/data/reports/fishing-data-2026-10-17/remaining.md). 입질 통계 API와 런타임 HTTP는 도입하지 않는다.

## 필요한 어종 보완과 먼바다 조건 (2026-11-06)

- 조건 자료는 `kr-regular-2026-11-06`으로 갱신하고 등급 자료의 조건 해시를 함께 갱신했다. `reviewedDetailObservations`에 URL·확인일·스냅샷 SHA256·수치·가공 범위를 기록했다. 기존 Tracker/Teamcraft revision은 원래 기반으로 보존한다. 새 자료가 전체 기반을 대체했다고 표현하지 않는다.
- [Fruity Snacks Dawntrail 가이드](https://docs.google.com/document/d/1HB2R8e2ZS6kW3v-7xSYuBbTEA3AZSLj4j2jrA2YcPFs/edit)의 명시적 직감 시간으로 대왕큰뱀머리고기350초·제파 보폴360초·채광 하트가오리900초·세 날 범고래600초를 채웠다. [Uzel Qestir의 기록](https://jp.finalfantasyxiv.com/lodestone/character/29265719/blog/5636744)으로 정강이 메기/희망잉어 각300초, Razor Edge의 [심혈 망령](https://jp.finalfantasyxiv.com/lodestone/character/22067410/blog/5681892)300초·[번개구체](https://na.finalfantasyxiv.com/lodestone/character/22067410/blog/5723521/)90초를 보완했다. 출현 시간 길이를 직감 시간으로 사용하지 않았다.
- 양쪽 시트의 이크에바지/요에콰 `FishingNoteInfo` 제한 플래그는0이지만,0만으로 제한 없음을 확정할 수 없다는 기존 계약을 유지한다. 요에콰만 [실측 기록 운영자의 명시적 무제한/Powerful 안내](https://eorzea-weather.com/en/fish/yowekwa-inkhead)를 채택했다. 이크에바지는 낚아채기와 시간/날씨 완전성 미확인을 유지한다. 성 파스릭은 실제 어획자의 공개 기록끼리120초/180초가 충돌해 null을 유지한다([180초](https://jp.finalfantasyxiv.com/lodestone/character/1632871/blog/5329881)·[120초](https://jp.finalfantasyxiv.com/lodestone/character/18733847/blog/5308295)).
- 같은 Dawntrail 가이드로 기사 망둥이/발목 메기의 이중2를 추가했다. Golden Day 이중2이상·삼중3이상, Scarlet Queen/Copper Shark 이중2이상은 해당 기술로 준비 수량에 도달하는 원문의 절차가 입증하는 하한만 기록한다. 최대값·다른 기술·획득력 컷은 환산하지 않는다. 현재 일반26종/이중26·삼중11, 직감 선행43종 중20종/이중20·삼중8이다. 먼바다259종/양쪽259는 유지한다.
- 현재 Teamcraft 경로 최신 고정 revision `ca634a20561a1178fea3eb948be82e8fcccb8e2e`도 기존 최소 획득력350종과 값 충돌0, 우선 대상에 새 양수 값0이었다. 더 보완할 근거가 없는 최소 획득력은 미확인으로 유지한다. 일반 전수 완성·획득력별 보장 수량은 범위 밖이다.
- [ocean-conditions.v1.json](../../data/ocean-conditions.v1.json)은 같은 원저자 Indigo/Ruby CSV의 시간 Yes/No·날씨 Yes/No/???·직감 필요 여부만 추린260행이다. URL·날짜·SHA256·권리 범위를 파일에 기록했다. 표/본문/이미지/코드는 배포하지 않는다. `???` 두 행은 미확인으로 남긴다. 빈 시간 열=일반, 빈 날씨 열=환해류 구분은 원저자 `displayStops.js`의 분류와 두 클라이언트 낚시터 슬롯으로 대조했다. Junior Jinbei는 양쪽 해역에 속하므로 Item 단독 키로 합치지 않는다.
- 후보 판정은 Core의 순수 조건 비교다. 방문 시간대는 항로 시트, 현재 날씨는 검증된 ClientStructs 접근자, 직감 부재는 공개 StatusList 관찰이다. 관찰/자료 미확인 및 원본에 없는 날씨는 제외 근거가 아니다. 직감 활성은 대상까지 식별하지 못하므로 직감 필요 어종을 ‘조건 미확인’으로 남긴다. 장착 미끼/장비·특수 어획표·업적·점수 최적화는 판정하지 않는다. [실행 계약·실기 경계](../verification/native/GLOBAL-COMPAT.md#먼바다-조건-후보-개발본-2026-11-06).

## 일반 낚시 이중·삼중 수량 안내 (2026-11-05)

- 별도 자료 [hook-yields.v1.json](../../data/hook-yields.v1.json)는 현재 일반26종/이중26·삼중11종이다. 최초12종의 영상 조사와 통합 가이드 추가9종에 위 Dawntrail5종을 보완한 결과다. 일반 직감 선행43종 중20종의 이중,8종의 삼중만 포함하므로 나머지는 여전히 미확인이다.
- 영상ID와 설명 SHA256, 문서의2026-11-05 UTF-8 텍스트 내보내기 스냅샷 SHA256(4CD82A004E2B11BAD2442FD07FCFD066EE9DC13FE38701DF26CBC2A9751486A0)을 기록한다. 문서 URL은 내용이 바뀔 수 있어 고정 revision으로 취급하지 않는다. 형식1의 source는 기존descriptionSha256과 추가contentSha256 중 유효한 해시를 요구한다. 기존 FishParameter↔Item 짝과 연결하며 원본에 없는 이름/ID/수량은 채우지 않는다.
- 보강한 수량(이중/삼중, 빈 기술은 미확인): Titanic Sawfish4/7, Granite Crab3~4/미확인, Sculptor2/미확인, Eighteyes Eel1/미확인, Amber Monkfish1/미확인, Ponderer2/미확인, Thavnairian Caiman2/3, Thavnairian Calamari2/3, Jhinga2/3. 기존 Sand Egg의 삼중3, Astacus Aetherius의 이중2이상도 보완했다. 가이드의 해당 어종·직감 준비/재준비 절을 읽어 개별 기술의 숫자만 옮겼다. Green Prismfish의 복합 준비 순서 등 모호한 설명은 수량으로 환산하지 않았다.
- 출처의 수량은 캐릭터 현재 획득력에서의 보장 수량이 아니다. ‘단일 어획+이중으로 직감 조건 충족’처럼 최소량만 확인되는 설명은 하한으로만 기록하고 상한은 null이다. 3~4처럼 명시한 범위와 구분한다. 삼중 설명이 없으면 null이며 이중 수량으로 변환하지 않는다. 일반 어종 전수·획득력별 임계값·소장품 실제 수령 계약은 미확인이다.
- 공개 재사용 라이선스는 확인하지 못했다. 저작권은 원저작자에게 있으며 소수의 숫자 사실만 기록한다. 원문 문장·코드·이미지·영상은 도입하지 않고 MIT/Apache 자료로 표시하지 않는다. URL/고정ID/해시/가공 범위는 JSON과 THIRD-PARTY-NOTICES에 남긴다. 개발 중 수집과 DLL 내장은 런타임 통신과 구분한다.

## 최소 획득력·납품 구간·수량 확장 (2026-11-06)

- 최소 획득력은 [minimum-gathering.v1.json](../../data/minimum-gathering.v1.json)에350종을 내장한다. 기존 Teamcraft MIT 원본 revision `8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7`의 `fishing-sources.json`에서 양수 `minGathering`만 추렸다. SHA256 `701438120D5DB489985972A833777F7B31DCF7CA2E76A756136DD1677B957B74`가 기존 고정 manifest와 일치하며 어종별 양수 충돌0건·KR/글로벌 Item↔FishParameter 짝 불일치0건이다. 원본0/누락은 미확인이다. 일반 조건 자료와 연결321종·상세 조건이 없는29종이며 후자도 상세의 독립 획득력 표시를 사용한다. 복수 낚아채기의 획득력별 보장 임계값으로 바꾸지 않는다.
- 소장품은 외부 표를 복사하지 않고 Framework의 카탈로그 로드에서 실행 클라이언트 `CollectablesShop → CollectablesShopItem → CollectablesShopRefine`의 세 구간을 읽는다. `RewardType=1`의 납품원만 포함하고 장비 개량용 부재/거래 자본 등 `RewardType=2` 교환14종은 일반 납품원으로 분류하지 않는다. 단골거래는 `SatisfactionNpc.SatisfactionNpcParams.SupplyIndex → SatisfactionSupply`로 Item/구간을 연결하고 이름은 `SatisfactionNpc.Npc → ENpcResident.Singular`를 사용한다. 0번 공급/빈 Item과 같은 NPC·Item·구간의 중복을 제거한다.
- 설치 KR `2026.10.06`·글로벌 `2026.10.16`의 현재 카탈로그 대상에 납품원77종·단골거래12명/54개 고유 NPC-어종 연결, 총131개 구간이 일치했다. `.cache/detail-supplement-audit/KR.json`·`Global.json`과 조회 코드에 로컬 근거를 둔다. 조건/해금 단계·이번 주 요청을 읽은 것이 아니며 다른 특수 납품과 작살 전용 품목을 전수 지원한다고 표현하지 않는다. 수치/이름은 런타임 시트를 따르고 Draw에서는 시트를 다시 읽지 않는다. 실패는 확인 불가로, 소장품인데 연결이 없으면 미확인으로 표시한다.
- [hook-yields.v1.json](../../data/hook-yields.v1.json)은 기존 일반21종/이중21·삼중10에 먼바다259종의 이중/삼중을 추가해 총280종이다. 원저자 Tyo'to Tayuun의 Indigo/Ruby CSV `DH`/`TH`에 명시된 단일 수량과 `3 - 4`/`5 - 7` 범위만 가져왔다. 고정 CSV260행은 설치 양쪽 클라이언트의259종에 연결되고 겹치는 어종의 수량 충돌0건이다. 원본의 범위와 미확인을 보존하고 기술 간 환산·입질 초·점수·전략은 도입하지 않는다. 각 CSV URL/SHA256/확인일을 수량 파일에도 기록했으며 해시는 아래 Beta의 원본과 같다. 원저작권/가공 범위는 THIRD-PARTY-NOTICES에 추가했다. DistantSeas의 AGPL 자료/코드를 도입하지 않았다.
- 일반 수량은 종합 가이드 스냅샷의 Double/Triple Hook 관련60개 단락을 다시 확인했다. 직접 숫자를 말하는 일반 어종은 기존21종과 겹쳤으며, 준비 순서/필요 마릿수에서 새 수량을 역산하지 않았다. 일반 전수·획득력별 보장 수량·소장품 실제 수령량은 확인되지 않은 범위다. 2026-11-06 TODO 정리에서 전수 완성/보장 수량은 현재 목표에서 제외했으며 필요한 어종의 근거 있는 보완만 남긴다. 개발 전용 재생성 명령은 [data/README](../../data/README.md#최소-획득력납품수량-보강-2026-11-06)를 따른다.

## 먼바다 Beta 기본 안내와 일정 (2026-11-06)

- [ocean-guidance.v1.json](../../data/ocean-guidance.v1.json)은13해역의 일반 환해류 유발 대상13행과 밤/낮/저녁별 환해류 기본 대상39행이다. Tyo'to Tayuun의 [Indigo](https://ffxiv.oceanfishing.boats/indigo/)·[Ruby](https://ffxiv.oceanfishing.boats/ruby/) 원저자 사이트의 CSV에서 개별 최적 미끼/입질 강도/이중·삼중 수량/시간대 사실만 추렸다. URL은 가변이므로 각 CSV/편성표 조사본의 날짜와 SHA256을 JSON에 고정한다. 사이트의 본문·코드·이미지·전체 어종표·입질 초는 도입하지 않았다. 공개 재사용 라이선스는 확인되지 않았으며 THIRD-PARTY-NOTICES에 원저작권/가공 범위를 기록했다.
- 일반 해역은 환해류 유발 대상의 미끼다. 환해류는 직감/생미끼/별도 특수 미끼가 없는 직접 미끼 대상 중, 해당 시간대에 유효한 명시적 이중4마리 후보를 먼저 고르고 동률은 원저자 표 순서를 유지했다. 해당 후보가 없으면 표의 이중2/3마리 대상을 사용한다. 점수×수량·기대값·GP별 최적화는 계산하지 않는다. ‘지금 추천’은 이 기본 방침이며 장착 미끼나 걸린 어종의 판정이 아니다. Galadion의 Krill 등 다른 공략과 차이가 있을 수 있는 항목은 이번 고정 CSV의 사실을 사용하며 독립 검증이 끝났다고 표현하지 않는다.
- 52행 모두 이름을 개발 중 글로벌 Item/FishParameter의 유일한 ID로 대응한 뒤 KR에도 같은 Item→FishingSpot 연결이 있는지 대조했다. 일반/환해류는 각각 IKDSpot.SpotMain/SpotSub와 교차 일치시켰다. 어종/미끼 이름과 아이콘은 실행 클라이언트 시트에서 읽으며, 누락/불일치한 행은 안내 자료 미수집으로 남긴다. 안내 전용 자료이며 일반 어종 수량 안내나 먼바다259종 상세를 전수 보완하지 않는다.
- 일정은 실행 클라이언트의 IKDRoute21행·IKDSpot13행·IKDRouteTable144행을 사용한다. 안정 스키마의 IKDRouteTable.Unknown0는 두 클라이언트와 같은 DLL의 실험 스키마 RubyRoute 접근자 배치를 대조해 두 번째 항로 ID임을 확인했다. 원시 오프셋이나 실험 스키마 의존성은 제품에 넣지 않았다. KR/글로벌의144쌍 순환표는 같다. 시간대 값1=밤/2=낮/3=저녁을 원저자 항로 순서와 대응했다.
- UTC Unix2시간 구간 번호를 `n`이라 할 때 표 인덱스는 `(n+40) mod 144`다. 원저자의 Indigo144회와 Ruby144회 전체 순환을 각각 게임 표로 대응했다(원저자 배열→시트의 회전량100/96, 각 원저자 UTC 기준 위상84/88이므로 모두40). 접수는 UTC 짝수 정각부터15분까지이며 로컬 시각 LT로 표시한다. [원저자의 접수 설명](https://ffxiv.oceanfishing.boats/achievements/)도 대조했다. 각 접수 시각의 양쪽 항로를 함께 표시하며, 실제 승선편을 시각으로 추론하지 않는다. KR/글로벌 인게임 NPC 편성표의 실제 기준 시각 대조는 대기다.
- 개발 조사본은 `.cache/ocean-beta-research/`의 고정 CSV·편성표·시트 조회 JSON과 가공 스크립트다. 배포물은52개 사실 행과 출처 메타데이터만 내장한다. 런타임 HTTP·크롤링·원격 갱신은 없다.

## 제공할 정보와 단계

| 정보 | 표현과 구현 시점 |
|---|---|
| 미끼·생미끼 | 권장/대체 미끼와 생미끼 순서를 구분해 MVP-01 상세 정보에 표시 |
| 낚아채기 | 섬세/강력한 낚아채기와 적용 조건, 입질 강도를 서로 다른 필드로 표시. MVP-01 |
| 입질 시간 | 사용자가 후순위로 지정. 통계 API 이용/재배포·측정 기준 확인 뒤 별도 구현하며 현재 10종 데이터에 포함하지 않음 |
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
- 0.3.3.0의 직감 예측은 같은 revision의 [fishwatcher.js](https://github.com/icykoneko/ff14-fish-tracker-app/blob/d0e1ab5bba62c2b57356e690a73676cd4751b64d/js/app/fishwatcher.js)와 [fish.js](https://github.com/icykoneko/ff14-fish-tracker-app/blob/d0e1ab5bba62c2b57356e690a73676cd4751b64d/js/app/fish.js)의 구간 연결을 C#·실제 UTC 초·30일 검색으로 옮겼다. MIT 고지에 파일/가공 내역을 추가했다. 원본의 미확인 지속 시간 기본값은 도입하지 않았으며 실제 캐릭터 준비 상태는 계산하지 않는다. 정적 조건 데이터/의존성 revision 변경은 없다. [해시·비교](../verification/forecast/FORECAST.md#0330-전체-일반-어종의-직감-예측-2026-10-19).
- 0.3.4.0은 같은 원본의 물고기의 눈 지원 플래그로 대상/선행 어종의 시간 제한을 해제하는 선택 예측을 추가했다. 내장 자료는 true529/false374/null355이며 미확인 지원을 추측하지 않는다. 설치 KR Action4105/Icon1112를 조회해 버튼에 사용했다. 현재 웹에서 지속 시간 null인2종과 ET1시간(실제175초) 기본값을 다시 확인했으며 우리 자료/정책은 유지한다. [재확인·전수 계산·웹 비교](../verification/forecast/FORECAST.md#0340-물고기의-눈과-상세-탐색-2026-10-19).
- 0.3.5.0에서는 사용자 명시 요청으로 직감 지속 시간 null에175초 기본값을 적용했다. Source null을 바꾸지 않고 Breakdown에 계산값/DurationAssumed를 보관하며 상세에 경고한다. 원본 사실이 보완된 것으로 집계하지 않는다. 제파 보폴은 선행 어종이 상시여서 기존에도 예측 가능했고, 세 날 범고래/채광 하트가오리는 제한 있는 선행 어종 때문에 보류했으나 이제 가정 예측을 제공한다. [정책·자료 대조](../verification/forecast/FORECAST.md#0350-직감-기본값과-계산-구역-개선-2026-10-19).
- [fishData.yaml](https://github.com/icykoneko/ff14-fish-tracker-app/blob/d0e1ab5bba62c2b57356e690a73676cd4751b64d/private/fishData.yaml)에서 startHour/endHour, previousWeatherSet/weatherSet, bestCatchPath, predators, tug, hookset, folklore, fishEyes, patch 필드를 확인했다. 구조화된 출현 조건의 후보이며, 전체 어종의 완전한 입질 시간 통계까지 제공한다고 확인한 것은 아니다.
- [데이터 템플릿](https://github.com/icykoneko/ff14-fish-tracker-app/blob/d0e1ab5bba62c2b57356e690a73676cd4751b64d/private/fishDataTemplate.yaml)에는 dataMissing 상태가 있다. 미입력과 제한 없음을 같은 의미로 취급하지 않는다.
- [저장소 라이선스](https://github.com/icykoneko/ff14-fish-tracker-app/blob/d0e1ab5bba62c2b57356e690a73676cd4751b64d/LICENSE)는 MIT, 저작권 표기는 icykoneko(2019)다. 제품 도입 시 원본 URL/커밋/가공 내역과 필요한 라이선스 고지를 포함한다. 게임 추출물/이미지/하위 모듈의 권리까지 동일하다고 확대하지 않는다. 이미지는 현행 한섭 클라이언트에서 읽는 방식을 유지한다.

### XIVAPI v2와 한섭 클라이언트

- [공식 문서](https://v2.xivapi.com/docs/welcome/)에 따르면 XIVAPI는 게임 클라이언트 파일에 있는 데이터를 제공한다. 낚시 커뮤니티의 관측 통계를 전부 대체하는 소스로 가정하지 않는다.
- [에디션/언어 설명](https://v2.xivapi.com/docs/guides/)은 제공 데이터가 글로벌 클라이언트 기준임을 명시한다. 한섭 이름·지원 패치·행의 존재는 설치된 한섭 클라이언트의 시트를 우선한다.
- [로컬 파일 접근 안내](https://v2.xivapi.com/docs/software/)도 게임 설치에 접근할 수 있다면 로컬 데이터 라이브러리 사용을 권한다. 현행 IDataManager/Lumina 경로를 유지하고 불필요한 런타임 웹 조회를 추가하지 않는다.

## 초기 도입 순서와 유지할 경계

1. 한섭 시트로 FishParameter ID ↔ Item ID, 낚시터 ↔ 지도/좌표를 확인한다. 글로벌 소스의 이름을 제품 식별자로 쓰지 않고 ID를 대조한다.
2. Fish Tracker의 검증된 출현 조건/미끼/낚아채기를 가공하는 내장 데이터 구조를 준비한다. 조건 데이터가 없는 어종은 목록에 계속 표시한다.
3. Teamcraft 입질 통계의 외부 조회/재배포 조건과 실제 응답·단위·필터 확인은 사용자의 지시에 따라 후속으로 미룬다. 현재 입질 초 수를 데이터에 채우지 않는다.
4. 초기 10종→2.x 140종→고정 원본 903종→일반 1,258종 연결로 확대했다. 이후에도 미지원 조건·누락 상태를 먼저 분류하며 현재 잔여 범위는 상단 보고서를 따른다.
5. 검수한 스냅샷을 내장/캐시하고 시간/날씨는 Core에서 계산한다. 게임 입력 자동화와 원격 백엔드는 추가하지 않는다.

최초 조사는 저장소 원문과 공식 문서의 읽기 전용 확인이었다. 이후 0.1.5.0에서 10종의 부분집합과 C# 날씨 계산을 도입하고 원본/변경/라이선스를 루트 THIRD-PARTY-NOTICES.txt에 기록해 DLL에 내장했다. 전체 다운로드 원문은 추적 제외 `.cache/`에만 보관한다. 0.2.9.0은 아래 정적 자료만 추가했으며 외부 이미지·호스팅 통계 API는 사용하지 않는다.

## 0.2.9.0 일반 낚시 보완

- 같은 Teamcraft 고정 revision의 [정적 JSON](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/tree/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/libs/data/src/lib/json) 5개를 입력으로 사용했다. `fishing-sources.json`은 장소·미끼·낚아채기·입질·시간/날씨·선행 어획, `fish-parameter.json`은 ID 대조, `item-patch.json`/`patch-names.json`은 접미사를 포함한 도입 패치, `items.json`은 감사용 영문 이름만 제공한다. 전체 이름 사전은 배포하지 않는다.
- [Hookset](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/libs/types/src/lib/data/hookset.ts)의 0/1/2=NORMAL/POWERFUL/PRECISION, [Tug](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/libs/types/src/lib/data/tug.ts)의 0/1/2=MEDIUM/BIG/LIGHT를 별도로 변환한다. spawn/duration은 ET 시간 단위이며 생미끼 경로는 같은 낚시터에서만 연결한다. 조건 누락과 제한 없음의 의미는 고정 소스 모델/추출기를 확인하고 KR 양성 제한 플래그와 함께 대조했다.
- 추가 353종과 기존 낚아채기 80종·입질 4종·위치 31종을 보완했다. 기존 비어 있지 않은 조건은 덮어쓰지 않았다. 위치 보완은 기존 시간/날씨/선행 어획 조건과 정확히 일치하는 후보만 채택했다.
- 정적 원본에 없는 이크에바지 가위(Item52014)와 요에콰 먹물머리(Item52015)는 공식 Lodestone의 [첫 어종](https://na.finalfantasyxiv.com/lodestone/playguide/db/item/12a7666ee04/), [둘째 어종](https://eu.finalfantasyxiv.com/lodestone/playguide/db/item/4712953a751/)에 게시된 Slay Tanic의 개별 어획 사실에서 금속 스피너/입질 강도만 기록했다. KR 위치는 클라이언트에서 대조했다. 댓글 문장·이미지·입질 초 수를 복제하지 않으며 그 사실만으로 시간/날씨 완전성이나 낚아채기를 확정하지 않는다. manifest의 URL/확인일/관찰자/범위와 별도 고지를 유지한다.
- 채택한 두 정적 원본에 없는 직감 지속 시간 9종과 위 부분 확인 2종은 미확인이다. 다른 공개 자료가 전혀 없다는 뜻은 아니며 검증하지 않은 값을 추측해 채우지 않았다. 추가 355종의 물고기의 눈 지원 여부는 null로 구분하고 현재 UI 미표시 정책을 유지한다.
- 기존 작살 데이터와 예측 계산·수집·필터·알림 정책은 변경하지 않았다. 먼바다 259종과 작살 미확인 115건은 자료의 부재와 전용 표시/연동 후속을 구분해 이름/ID/이유 CSV를 제공한다.

## 0.4.0.0 거주구 확장팩과 먼바다 분류 점검

- 최종 요청에 따라 먼바다는 등급 대신 해역별 현황을 제공한다. FishParameter.FishingSpot을 기존 IKDSpot의 SpotMain/SpotSub로 연결하고 같은 PlaceName.RowId를 해역 키로 사용한다. 일반/환해류를 같은 해역으로 합치며 번역된 이름으로 그룹을 판정하지 않는다. 동일 KR 빌드의259종이13해역에 연결되고 미연결0종이다. 해역 키는3444/3445/3446/3447/3641/3642/3643/4363/4364/4365/4366/5531/5532이며 앞12해역은20종,5532 사베네어는19종이다. 오프라인 조회 코드/결과는 `.cache/ocean-area-review/Program.cs`와 `audit.json`에 보존했다. 이름은 런타임 클라이언트 시트를 따른다.

- 2026-10-20 설치 KR 빌드 `2026.10.06.0000.0000`의 TerritoryType/Map/ExVersion/FishParameter/Item.Description을 오프라인으로 조회했다. 시로가네는 Territory641/Map364/ExVersion0, 지고천 거리는 Territory979/Map679/ExVersion0이다. 반면 ExVersion2는 홍련,4는 효월이다. [공식4.0 패치 노트](https://na.finalfantasyxiv.com/lodestone/topics/detail/296b070f41c0e0dedae24de50ae7c51abfa95055)의 신규 시로가네와 [공식6.0 패치 노트](https://jp.finalfantasyxiv.com/lodestone/topics/detail/66a742165e9415790247c1a1f7d909abf5becb5c/)의 신규 Empyreum을 대조했다. 해당 빌드에서641→2/979→4만 보정하고 다른 행/지원 빌드 밖은 시트값을 유지한다. 어종 도입 패치로 지역 소속을 추정하지 않는다. 보정된 낚시터 소속은 메인 필터와 수집 현황이 함께 사용한다.
- 같은 KR 도감의 먼바다259종에서 Item.Description의 명시적 `터주`/`터주왕` 양성은0종이다. OceanStars 분포는1성39/2성39/3성25/4성143/5성13종이지만 이를 터주/터주왕 등급으로 바꾸지 않는다. 내장 등급 자료에도 이 범위의 양성이 없어 현재 제품은 전체 먼바다와 시트 해역별 현황으로 구분한다. 양성 자료 부재를 일반 등급이나 확정0종으로 변환하지 않는다.
- 조회 결과는 `.cache/collection-review/audit.json`, 사용한 오프라인 조회 코드는 같은 폴더의 Program.cs다. 먼바다 설명 근거의 `fishId/itemId:description` LF 결합 SHA256은 `BD28AA4C08CD0313BE5E42932E257A46ACE78DE3F35B31FAB73D75DA841B5AAB`이다. 외부 코드·본문·에셋은 도입하지 않고 클라이언트 행 ID와 고정 패치4.0/6.0의 기능 사실만 사용했다. 런타임 HTTP/네이티브 접근은 추가하지 않았다.

## 0.1.15.0 등급 분류 근거

- 1차 자료는 설치된 KR `2026.10.06.0000.0000`의 FishParameter/Item.Description/FishingNoteInfo다. 일반 도감 1258종에서 설명의 명시적 ‘터주’/‘터주왕’을 개발 중 대조해 숫자 ID 양성 목록을 생성했다. 실행 중 번역된 화면 문자열을 판정에 쓰지 않는다.
- 현재 조건 원본 903종의 bigFish와 대조해 불일치 0, 전체 양성 목록은 335종/터주왕 36종이다. 원본의 bigFish 하나나 도입 패치 번호만으로 터주왕을 나누지 않는다. 새 외부 코드/라이선스는 도입하지 않았으며 기존 고정 원본과 MIT 고지를 유지한다.
- 별도 분류 파일은 지원 빌드/조건 파일/설명 근거 해시를 기록한다. 원문 설명/이름/이미지는 배포하지 않는다. [재생성 명령과 해시](../../data/README.md)의 개발 도구로 재현하며 글로벌 클라이언트 호환성을 보장하지 않는다.
