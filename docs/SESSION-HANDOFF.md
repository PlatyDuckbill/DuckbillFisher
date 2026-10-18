# 세션 인수인계

갱신: 2026-10-18 · 개발 0.3.1.0 환경/호환성 정비 · 공개 0.3.0.0 유지

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
| 확인한 Release 추출 DLL | ${env:DUCKBILL_ROOT}/.cache/release-validation/0.3.0.0/DuckbillFisher.dll |

캐시/참조 DLL은 Git에 없다. 다른 PC에서는 실제 경로와 정확한 지원 배포판을 다시 확인한다. 최소 참조가 없다고 현재 참조를 Release에 조용히 대체하지 않는다. 일반 빌드 명령은 [WORKFLOW](development/WORKFLOW.md#windows-빌드)에 한 곳만 유지한다.

## 배포 산출물과 로컬 상태

- 공개 0.3.0.0의 소스 태그 커밋은 7269627, 설치 JSON 게시 커밋은 fc300d5, 게시 확인 기록은 b33f4eb다.
- 사용자 확인 ZIP: artifacts/share/0.3.0.0/DuckbillFisher-0.3.0.0.zip. 크기/전체 SHA256/익명 다운로드 근거는 [Release 기록](verification/releases/RELEASE-0.3.0.0.md)에 있다.
- 공개 Release 빌드 출력은 .cache/bait-preparation-release/다. 개발 0.3.1.0 별도 출력은 .cache/maintenance-build/·maintenance-minimum/·maintenance-friend/·maintenance-upstream/·maintenance-import/다. 정규 Debug는 개발 0.3.1.0이며 실기 대기다.
- 확인된 ZIP을 다시 빌드해 바꾸거나 기존 태그/Release를 덮어쓰지 않는다. 제품 수정은 새 버전으로 준비한다.
- 개발·배포 플러그인의 같은 InternalName을 동시에 로드하지 않는다. 사용자가 현재 등록한 경로를 추측해 설정을 수정하지 않는다.
- 현재 PC는 gh CLI 없이 Git credential manager+GitHub API로 게시한 이력이 있다. 토큰은 메모리에서만 사용하며 출력/파일/Git에 남기지 않는다. 캐시의 버전별 게시 스크립트를 확인 없이 재실행하지 않는다.

## 이번 정비와 후속

- 개발 0.3.1.0은 환경 변수/공통 참조 검사/게임별 네이티브 프로필 분리를 적용했다. 최소 KR·현재 KR·친구 KR·고정 upstream 빌드는 실제 게임 확인과 구분한다. [구조](development/ARCHITECTURE.md#호환성과-실패), [검사와 재현](verification/native/NATIVE-RUNTIME-DAL4KR.md#0310-개발-정비-2026-10-18).
- v1 조건 자료는 data/archive로 이동했으며 기존 예측 테스트에서만 참조한다. 제품은 v2 조건과 v1 등급 자료를 내장한다. 테스트는 요청 없이는 실행하지 않는다.
- 다음 게임 확인은 개발 DLL 재로드 후 도감/수집/타이머 대표 동작이다. 공개 0.3.0.0의 실제 저장소 업데이트/친구 새 기능 확인 대기는 별도로 유지한다. 미끼 페이지 연결 문제는 bait-ui/v7의 Pages 한 줄부터 요청한다.
- 일반 데이터 잔여 이름/ID/이유는 [보고서](verification/data/reports/fishing-data-2026-10-17/remaining.md), 재생성 순서는 [data/README](../data/README.md)를 따른다. 완성 제품을 보완 단계 baseline으로 다시 넣지 않는다.
- Teamcraft 재조사·입질 통계·복수 어획 보류를 유지한다. 신규 후보는 [ROADMAP](product/ROADMAP.md)에서 사용자와 선택한다.
- 폴더 재구성 전 경로는 Git 699db5b에 보존됐다. UI 원문 12개는 archive/ui/CHANGES.md의 문서별 앵커로 합쳤으며 새 전체 상태 사본은 만들지 않았다. 문서 검사기는 새 폴더/색인 규칙을 검사한다.
