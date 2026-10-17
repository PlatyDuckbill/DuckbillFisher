# 일반 낚시 미확인 자료 보완 · 0.2.9.0

조사 2026-10-17 / 완료 2026-10-18. KR `2026.10.06.0000.0000`. 정규 DLL 반영, 인게임 검증 대기, 공개 배포는 사용자 요청으로 생략했다.

## 범위와 결과

| 항목 | 이전 → 반영 후 |
|---|---|
| 일반 도감 상세 | 903/1258 → 1258/1258 |
| 추가 상세 | Teamcraft 정적 자료 353종 + 개별 어획 사실 2종 |
| 기존 빈 필드 보완 | 낚아채기 80종 / 입질 강도 4종 / 예측 위치·조건 확인 31종 |
| 변경 어종 | 중복을 제외한 466종 |
| 시간·날씨 조건 확인 | 872 → 1256종. 부분 확인 2종은 예측/알림 대상에서 제외 |
| 핵심 안내 필드 미확인 | 직감 지속 시간 9종 + 낚아채기·시간/날씨 완전성 2종 |
| 선택적 메타데이터 | 추가 355종의 물고기의 눈 지원 여부 null. 현재 UI 미표시 |
| 별도 범위 | 먼바다 259종의 어종별 조건 연동 / 작살 203건 중 미확인 115건 |

전체 이름·ID·누락 필드·이유는 [보고서와 CSV](../reports/fishing-data-2026-10-17/remaining.md)에 제공한다. 수집 상태의 Unknown, 조건 자료 누락, 7일 이내 출현 예측 없음은 서로 다른 상태다.

## 출처·가공 결정

- 기본 Fish Tracker revision `d0e1ab5bba62c2b57356e690a73676cd4751b64d`의 기존 알려진 조건/미끼/직감·메타데이터는 유지했다. 비어 있던 낚아채기·입질 강도·위치만 보완했다. 위치는 같은 시간·현재/직전 날씨·선행 어획 조건과 KR 어종/미끼 경로가 맞는 후보만 채택했다.
- 보완 자료는 [Teamcraft 고정 정적 파일](https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/tree/8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7/libs/data/src/lib/json), revision `8925c7b73a99e8ba4fb2aa10719a3b3f48109ea7`이다. MIT 고지, 파일 5개와 SHA256, baseline SHA256을 각각 notices/manifest에 기록했다. 호스팅 통계 API·외부 이미지·입질 초 수는 사용하지 않았다. 조사 당시 최신 fishing-sources revision `ca634a20561a1178fea3eb948be82e8fcccb8e2e`(2026-10-13)도 같은 파일 해시여서 2종 추가 정보는 없었다.
- Hookset 0/1/2=NORMAL/POWERFUL/PRECISION, Tug 0/1/2=MEDIUM/BIG/LIGHT를 원본 enum에서 확인했다. ET spawn/duration과 생미끼의 같은 장소 연결은 고정 모델/추출기의 의미를 확인했다. 입질 강도에서 낚아채기를 추정하지 않는다.
- 추가 조건은 KR 설치본의 어종·Item·낚시터·미끼·날씨·양성 제한 플래그와 대조했다. 경로는 검증된 한 가지를 선택하며 가능한 모든 대체 미끼 목록을 보장하지 않는다. 도입 패치의 `3.55b` 접미사는 새 `patchVersion`으로 보존하고 기존 숫자 필드는 유지했다.
- 이크에바지 가위(1514/52014), 요에콰 먹물머리(1515/52015)는 채택한 구조화 원본에 없다. [Lodestone 첫 어종](https://na.finalfantasyxiv.com/lodestone/playguide/db/item/12a7666ee04/), [둘째 어종](https://eu.finalfantasyxiv.com/lodestone/playguide/db/item/4712953a751/)의 Slay Tanic 관찰에서 금속 스피너(28634)·입질 강도만 개별 사실로 기록했다. KR 위치309/321과 ID를 대조했다. 댓글 문장/이미지/입질 초 수를 복제하거나 MIT로 표시하지 않았다. 시간·날씨 전체와 낚아채기는 계속 미확인이다.
- 직감 지속 시간 9종은 채택한 두 원본에서 해당 필드가 빠져 있다. 다른 공개 정보가 전혀 없다고 주장하지 않으며, 검증하지 않은 값을 출현 시간 길이 등으로 대신하지 않았다. 필요 선행 어종/마릿수는 계속 안내한다.
- `kr-regular-2026-10-17`은 조사 대상 KR 빌드와 일반 낚시 범위를 뜻한다. 클라이언트 언어 이름/아이콘 경로는 유지하며 글로벌 런타임 호환성을 새로 보장하지 않는다.

## 실제 실행한 검사

테스트 추가/실행 없이 다음 빌드·실제 자료·배포 파일 검사를 수행했다. 재현 명령은 [data/README](../../data/README.md)에 있다.

1. `dotnet run --project tools/FishingDataImport ... -- --complete ...`: 입력 해시/정확한 KR 빌드/ID/낚시터/시간 범위/날씨/생미끼 경로 검사. 최종 재생성 결과와 제품 JSON SHA256 일치.
2. `--classifications`: Inspected=1258, SourceCompared=1258, PositiveTimingCompared=131, BigIncludingLegendary=335, Legendary=36, Failed=0. 기존 양성 ID 집합 유지.
3. 기존 903종의 필드별 대조: 허용한 빈 필드 보완 외 차이 0. 기존 작살 203건의 직렬화 내용 일치. 선택된 일반 낚시터 1258곳의 런타임 지도/지역 연결 불일치 0.
4. 현재15.0.3.7 참조의 별도 `.cache/data-completion-build/`와 정규 Debug 빌드, 최저15.0.3.6 참조의 `.cache/data-completion-oldrefs/` 빌드: 각각 경고0/오류0. 참조 컴파일 성공을 추가 런타임 확인으로 표현하지 않는다.
5. `pwsh -NoLogo -NoProfile -File scripts/check-localization.ps1`: 한영559 typed keys/형식 인자 일치.
6. 내장 FishingData/FishClassifications/ThirdPartyNotices와 디스크 자료 해시 일치. 별도/정규 Plugin·Core·ko·manifest 4개 파일 해시 일치. Plugin/ko/manifest0.2.9.0, Core1.0.0.0, manifest API15.
7. CSV 이름 포함 건수: 핵심11 / 선택 메타데이터355 / 먼바다259 / 작살115. UTF-8 BOM, 작살 첫 열의 ID 종류 분리.

최종 SHA256:

| 파일 | SHA256 |
|---|---|
| 조건 JSON | `172BF562969B0F8D17199F0DA12085291E6D0C34DB38910EB7A235A10F8307A0` |
| 분류 JSON | `756D46948B20A44F5695465CDBEC42E63EAC355F1BD9B372D9AEADDC98048DF2` |
| 정규 Plugin DLL | `0D526B45F9CC87E31BAF592364D56214C44FC596DEB8531FC0B42CA5E9242182` |

변환기 최초 실행의 FishingNoteInfo 중복은 일반 도감 대상 Item으로 입력 범위를 제한해 해결했고 최종 재생성은 성공했다. 게임 메모리 접근·타이머·수집/수동 우선·필터 저장/프리셋·알림 정책과 예측 공식은 변경하지 않았다. Teamcraft/바다구렁이 불일치 조사를 재개하지 않았다.

## 인게임 확인 대기

기본 필터가 일반 어종을 숨길 수 있으므로 메인 ‘전체 보기’ 후 개발자 도구의 상세 조건 필터로 연결 수 1258/1258을 확인한다. 대표 확인 후보:

1. 항만청어/멜토르 망둥이 등 기존 예측 위치 누락 어종의 위치·시간 안내, 여덟눈이(698)/수정단검어(699)의 낚아채기 아이콘.
2. 황금나비어의 기존 ET09:00~14:00·섬세한 낚아채기, 지도 핀과 한영 UI 전환 유지.
3. 이크에바지 가위의 금속 스피너·부분 확인, 성 파스릭의 석두의 직감 필요 어획과 지속 시간 미확인 표시. 부분 확인 2종에 상시 예측이 생기지 않아야 한다.

정규 주 DLL: `C:\Users\hyogu\Downloads\DuckbillFisher\src\DuckbillFisher.Plugin\bin\Debug\DuckbillFisher.dll`. 사용자 상시 지시의 기본 언로드 상태로 반영했다. 공개 릴리스/태그/저장소 JSON/ZIP/push는 수행하지 않았으며 공개0.2.6.0을 유지한다.
