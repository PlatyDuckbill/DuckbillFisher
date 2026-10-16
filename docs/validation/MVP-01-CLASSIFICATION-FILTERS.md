# MVP-01 분류 필터와 수집 선택 정리

날짜: 2026-10-16 · Plugin 0.1.15.0 · 인게임 검증 대기

## 사용자 선택과 계약

- 사용자는 시간제 체크/등급 선택 분리와 터주에 터주왕 포함을 선택했다. 기본은 ‘시간제만’ 해제 + 등급 전체다. 등급 선택은 전체/터주(터주왕 포함)/터주왕만이다.
- 시간제는 확인된 ET/현재 날씨/직전 날씨 제약 중 하나 이상이다. 현재 창 열림이나 개인 준비 완료와 구분한다. 등급과 시간제, 기존 검색/지역/수집/즐겨찾기/먼바다 포함/상세 자료 사이에는 AND를 적용한다. 터주 선택 내부는 터주 또는 터주왕이다.
- 등급 전체는 해당 제한만 해제하고 다른 선택을 유지한다. 분류 미확인은 전체에서 유지하며 특정 등급/시간제에서는 제외한다. 먼바다를 일반 터주/시간제로 분류하지 않는다.
- 수집 필터는 전체/수집한 어종 숨기기/수집됨만/미확인만이다. 미수집만 UI를 제거하고 명시적 enum 매핑으로 인덱스 이동 오류를 피한다. Core의 기존 UncaughtOnly 값은 호환성 차원에서 보존하되 UI에 노출하지 않는다. 내부 Unknown/미수집과 수동 우선순위/알림 조건은 바꾸지 않았다.

## 분류 근거와 실패 동작

- 일반 KR 도감 1258종의 Item.Description을 개발 중 검수했다. 명시적 터주왕/터주 문구를 순서대로 읽고 해당 빌드의 FishingNoteInfo.SpecialConditions=2 집합과 대조했다. 양성 335종 중 터주왕 36종이며 원본 903종의 bigFish와 불일치 0이다.
- 런타임의 터주왕은 별도 내장 ID 자료/지원 빌드/FishParameter·Item 쌍 일치가 필요하다. 설명/번역 UI 문구/패치 번호로 추정하지 않는다. 양성 목록 밖이라는 사실만으로 일반 어종임을 확정하지 않는다. 기존 원본에 명시된 bigFish=false는 별도 일반 분류 근거다.
- 시간 제약은 conditionsKnown의 실제 ET/날씨 교집합 자료를 우선하고, 미확인일 때는 검수한 KR FishingNoteInfo 양성 플래그 1만 보완한다. 131개 양성 표본이 원본 조건과 일치했다. raw 0/2를 무제한으로 바꾸지 않는다. 현재 자료의 제약 양성은 443종이다.
- 지원 빌드 불일치/참조 불일치/근거 부재는 Unknown을 유지한다. 분류는 정적 목록 로드 때 계산하고 Draw에서는 Core의 타입 기반 필터를 사용한다. 시트/파일 읽기·장기 예측·네이티브 접근·이벤트 구독을 렌더링에 추가하지 않았다.
- 내장 파일은 [재생성 명령/해시](../../data/README.md)를 따른다. 1차 자료는 설치된 KR 시트, 교차 대조 자료는 기존 MIT 고지/고정 revision의 조건 파일이다. 새 외부 코드/런타임 HTTP는 없다.

## 실행한 확인

- `tools/FishingDataImport --classifications` 모드 실행: Inspected=1258, SourceCompared=903, PositiveTimingCompared=131, BigIncludingLegendary=335, Legendary=36, Failed=0. 출력과 제품 복사본 해시 일치 확인. 출력/입력 해시는 LF로 고정했다.
- 별도 빌드: `dotnet build src/DuckbillFisher.Plugin/DuckbillFisher.Plugin.csproj --no-restore -c Debug -p:DalamudLibPath=C:/Users/hyogu/AppData/Roaming/XIVLauncherKR/addon/Hooks/dev/ -p:OutputPath=C:/Users/hyogu/Downloads/DuckbillFisher/.cache/classification-build/` 성공, 경고·오류 0.
- `pwsh -NoLogo -NoProfile -File scripts/check-localization.ps1`: 한영 230키와 형식 인자 일치 확인.
- 사용자 ‘언로드완료’ 후 같은 build 명령의 OutputPath 없이 정규 빌드 성공, 경고·오류 0. LF 정규화 반영 후 주 DLL 893952바이트/0.1.15.0, Core 30208바이트/1.0.0.0, ko 26112바이트/0.1.15.0, manifest InternalName DuckbillFisher/0.1.15.0/API 15 확인.
- 실제 DLL에서 내장 JSON을 읽어 분류 335/터주왕 36/지원 빌드 일치를 확인했다. `git diff --check` 확인. 테스트는 추가/실행하지 않았다. 정량 성능/게임 조합 검증과 구분한다.
- 진단은 summary/v4·preferences/v4·catalog/v5다. 분류 버전/양성 수와 Grade/Timing, TimeRestrictedOnly/GradeFilter를 추가했다. 전체 상세 표본 제한은 20종으로 유지한다.

## 인게임 확인 순서

1. 기존 개발 DLL로 0.1.15.0 로드. 검색 비우기, 수집=전체, 현재 지역만/즐겨찾기/상세 자료만/시간제만 해제, 수첩 포함만 켜기로 시작한다. 등급 터주(왕 포함)는 335종, 터주왕만은 36종이 기대값이다.
2. 등급 전체에서 시간제만 켜면 443종이 기대값이다. 터주/터주왕과 함께 켤 수 있으며 ‘시간제’가 현재 출현 중만을 뜻하지 않아야 한다. 다른 필터가 남으면 수는 줄어든다.
3. 황금나비어(194)는 터주, 홍룡(696)/넵토드래곤(276)은 터주왕 표지를 확인한다. 등급/시간제 선택 뒤 한영을 바꾸어 선택·검색·수집·지역 필터가 유지되는지 확인한다.
4. 수집 선택이 네 항목인지 확인한다. 수집됨만은 실제 수집/수동 수집 어종, 미확인만은 Unknown 어종으로 좁혀져야 한다. 수집 숨김은 기존처럼 Unknown을 남긴다. 자동 관찰 전 Unknown은 정상이다.
5. 결과는 짧은 진단 한 개와 화면 이상 여부로 받는다. 기존 도감/좌표/전체 데이터 연결 확인은 반복하지 않는다.
