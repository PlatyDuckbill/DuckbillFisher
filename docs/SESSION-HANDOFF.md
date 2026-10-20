# 세션 인수인계

갱신: 2026-10-20 · 공개0.3.10.0 · 개발0.3.11.0 UI 개선

## 재개 순서

1. AGENTS.md, [STATUS](STATUS.md), git status --short를 읽는다. tmp_note.md는 읽기/수정/스테이징하지 않는다.
2. 이번 사용자 요청과 STATUS의 다음 작업을 맞춘다. 옛 verification/archive의 ‘다음 작업’은 자동 실행하지 않는다.
3. [문서 지도](README.md)에서 필요한 설계/검증만 선택한다. 현재 정책은 PRODUCT/ARCHITECTURE, 명령은 WORKFLOW/DISTRIBUTION을 따른다.
4. 프로젝트 스킬은 .agents/skills에 Git으로 관리한다. 빌드/패키지/배포는 duckbill-release, KR 계약 조사에는 duckbill-kr-native를 적용한다. 새 세션에서 자동 목록에 없으면 AGENTS의 링크로 직접 읽는다.
5. 구현 후 명시한 파일만 커밋하고 실제 게임 결과/공개 게시와 구분한다. 사용자 언로드는 기본 상태이며 다시 묻지 않는다. 파일 잠금 실패를 우회하지 않는다.

## 환경과 경로

저장소 루트에서 `. ./scripts/enter-dev.ps1`을 실행하면 아래 환경 변수가 현재 셸에 설정된다. 참조/게임 경로 선택은 [WINDOWS-SETUP](development/WINDOWS-SETUP.md#개발-환경-변수)을 따른다. 이 PC의 기본 참조는 APPDATA 아래 XIVLauncherKR/addon/Hooks/dev다.

| 항목 | 값 또는 저장소 상대 경로 |
|---|---|
| 작업 폴더 | ${env:DUCKBILL_ROOT} |
| 게임 | FFXIV_GAME_PATH로 선택. 이 PC는 C:/Program Files (x86)/FINAL FANTASY XIV - KOREA/game |
| KR 게임 빌드 | 2026.10.06.0000.0000 |
| 현재 Dalamud 참조 | ${env:DALAMUD_HOME}/ |
| 최소 지원 참조 | .cache/compat-15.0.3.6/full/ |
| 친구 배포판 참조 | .cache/friend-runtime/provider-refs/ |
| upstream 컴파일 참조 | .cache/upstream-15.0.3.6/refs/ · build/dalamud-reference.json 고정 해시 |
| SDK/대상 | global.json의 10.0.402, SDK15.0.0, x64/net10.0-windows |
| 정규 Debug DLL | ${env:DUCKBILL_ROOT}/src/DuckbillFisher.Plugin/bin/Debug/DuckbillFisher.dll |
| 이전 Release 추출 DLL | ${env:DUCKBILL_ROOT}/.cache/release-validation/0.3.9.0/DuckbillFisher.dll |
| 현재 확인 Release 추출 DLL | ${env:DUCKBILL_ROOT}/.cache/release-validation/0.3.10.0/DuckbillFisher.dll |

캐시/참조 DLL은 Git에 없다. 다른 PC에서는 실제 경로와 정확한 지원 배포판을 다시 확인한다. 최소 참조가 없다고 현재 참조를 Release에 조용히 대체하지 않는다. 일반 빌드 명령은 [WORKFLOW](development/WORKFLOW.md#windows-빌드)에 한 곳만 유지한다.

## 배포 산출물과 로컬 상태

- 개발0.3.11.0 최신 별도 출력은 `.cache/catalog-options-build/`, 정규 Debug DLL도0.3.11.0이다. 설정schema15이며 게임 확인은 [목록/상세](verification/catalog/UI.md#03110-목록상세-조작-개선-2026-10-20)와 [배율](verification/catalog/UI.md#03110-ui-배율-2026-10-20)을 따른다. 이전 배율 출력은 `.cache/ui-scale-build/`에 남아 있다. 패키징/게시 전이다.
- 공개0.3.10.0 소스 태그 커밋은77d5442, 설치 JSON 커밋은d21c9b5다. Release ID417042910의 ZIP/체크섬·익명 다운로드/해시/아이콘 확인 뒤 설치 JSON을 게시했고 실제 공유 raw URL의0.3.10.0/API15/Changelog/링크 조회도 완료했다. [게시 근거](verification/releases/RELEASE-0.3.10.0.md).
- 고정 ZIP은 `artifacts/share/0.3.10.0/DuckbillFisher-0.3.10.0.zip`, Release 출력은 `.cache/release-build/0.3.10.0/`, 추출본은 위 표다. 검사 결과 `.cache/release-build/0.3.10.0-validation.json`과 `.cache/release-remote-check/0.3.10.0/`에 있다. 사용자 대표 확인 뒤 같은 ZIP을 게시했으며 재빌드/덮어쓰지 않는다. 이전0.3.9.0/0.3.8.0 산출물도 보존한다.
- 개발0.3.10.0 별도 출력은 `.cache/short-commands-build/`, 정규 Debug도0.3.10.0이다. 업데이트 안내는 schema13의 공통 LastSeenChangelogVersion을 사용한다. 이전 출력은 `.cache/changelog-build/`·custom-sound-build/·bait-preparation-release/·snapshot-reuse-build/·alert-stability-build/·forecast-refinement-build/·fish-eyes-build/·intuition-forecast-build/·data-integrity-build/·data-integrity-import/, 재생성 자료는 `.cache/data-integrity/`다. 이전 참조별 출력은 `.cache/maintenance-*`에 있다.
- 확인된 ZIP을 다시 빌드해 바꾸거나 기존 태그/Release를 덮어쓰지 않는다. 제품 수정은 새 버전으로 준비한다.
- 개발·배포 플러그인의 같은 InternalName을 동시에 로드하지 않는다. 사용자가 현재 등록한 경로를 추측해 설정을 수정하지 않는다.
- 현재 PC는 gh CLI 없이 Git credential manager+GitHub API로 게시한 이력이 있다. 토큰은 메모리에서만 사용하며 출력/파일/Git에 남기지 않는다. 캐시의 버전별 게시 스크립트를 확인 없이 재실행하지 않는다.

## 이번 정비와 후속

- Core 공통 자료 검증을 Plugin/도구에서 사용한다. 실제 자료 검사 명령은 [data/README](../data/README.md#필드-의미와-미확인), 결과와 재현은 [데이터 기록](verification/data/DATA-COMPLETION-2026-10-18.md#0320-데이터-안정성-보강)을 따른다. 오류 진단은 catalog/v9의 DataValidation/IntuitionForecast 한 줄부터 확인한다.
- 30일 검색·직감175초 가정·물고기의 눈 전환은 [예측 기록](verification/forecast/FORECAST.md#0350-직감-기본값과-계산-구역-개선-2026-10-19)을 따른다. summary/v10의 SelectedForecast/ForecastComparison/SelectedConditions/SelectedIntuition은 두 UTC·지원 여부·원본/계산 지속 시간·DurationAssumed를 구분한다. 실제 자료 도우미는 .cache/Inspect-ForecastRefinement.ps1·Compare-ForecastRefinement.ps1이며 테스트 스위트가 아니다.
- v1 조건 자료는 data/archive로 이동했으며 기존 예측 테스트에서만 참조한다. 제품은 v2 조건과 v1 등급 자료를 내장한다. 테스트는 요청 없이는 실행하지 않는다.
- 다음 실기 확인은 실제 저장소의0.3.10.0 업데이트와 설치 화면 Changelog·단축 명령어/목록 유지다. 고정 추출 DLL의 새 명령어·목록 유지·한영/재로드는 대표 확인을 받았다.0.3.9.0 이력 안내와 친구 PC/이전 예측·UI/장애/다른 캐릭터의 확인 범위는 STATUS와 해당 Release 기록을 따른다. 음원은 alerts/v7의 CustomSoundState/CustomSoundCode, 미끼 페이지는 bait-ui/v7의 Pages부터 요청한다.
- 일반 데이터 잔여 이름/ID/이유는 [보고서](verification/data/reports/fishing-data-2026-10-17/remaining.md), 재생성 순서는 [data/README](../data/README.md)를 따른다. 완성 제품을 보완 단계 baseline으로 다시 넣지 않는다.
- 입질 통계·복수 어획 보류를 유지한다. 과거 Teamcraft 화면 추적은 중단하고 [현재 웹 표시 대조](verification/forecast/FORECAST.md#현재-웹-표시-대조-2026-10-19)로 전환했다. UTC/CSV는 기록에, 임시 브라우저 관찰 자료는 .cache/forecast-browser-2026-10-19에 있다. 사용자 계정/브라우저와 분리한 익명 세션을 사용했다. 후속은 [ROADMAP](product/ROADMAP.md)을 따른다.
- 폴더 재구성 전 경로는 Git 699db5b에 보존됐다. UI 원문 12개는 archive/ui/CHANGES.md의 문서별 앵커로 합쳤으며 새 전체 상태 사본은 만들지 않았다. 문서 검사기는 새 폴더/색인 규칙을 검사한다.
