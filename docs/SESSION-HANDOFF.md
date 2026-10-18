# 세션 인수인계

갱신: 2026-10-18 · 문서 정비 · 공개 0.3.0.0 유지

## 재개 순서

1. AGENTS.md, [STATUS](STATUS.md), git status --short를 읽는다. tmp_note.md는 읽기/수정/스테이징하지 않는다.
2. 이번 사용자 요청과 STATUS의 다음 작업을 맞춘다. 옛 validation/history의 ‘다음 작업’은 자동 실행하지 않는다.
3. [문서 지도](README.md)에서 필요한 설계/검증만 선택한다. 현재 정책은 PRODUCT/ARCHITECTURE, 명령은 WORKFLOW/DISTRIBUTION을 따른다.
4. 프로젝트 스킬은 .agents/skills에 Git으로 관리한다. 빌드/패키지/배포는 duckbill-release, KR 계약 조사에는 duckbill-kr-native를 적용한다. 새 세션에서 자동 목록에 없으면 AGENTS의 링크로 직접 읽는다.
5. 구현 후 명시한 파일만 커밋하고 실제 게임 결과/공개 게시와 구분한다. 사용자 언로드는 기본 상태이며 다시 묻지 않는다. 파일 잠금 실패를 우회하지 않는다.

## 환경과 경로

| 항목 | 현재 PC의 값 |
|---|---|
| 작업 폴더 | C:/Users/hyogu/Downloads/DuckbillFisher |
| 게임 | C:/Program Files (x86)/FINAL FANTASY XIV - KOREA/game |
| KR 게임 빌드 | 2026.10.06.0000.0000 |
| 현재 Dalamud 참조 | C:/Users/hyogu/AppData/Roaming/XIVLauncherKR/addon/Hooks/dev/ |
| 최소 지원 참조 | .cache/compat-15.0.3.6/full/ |
| 친구 배포판 참조 | .cache/friend-runtime/provider-refs/ |
| SDK/대상 | global.json의 10.0.402, SDK15.0.0, x64/net10.0-windows |
| 정규 Debug DLL | C:/Users/hyogu/Downloads/DuckbillFisher/src/DuckbillFisher.Plugin/bin/Debug/DuckbillFisher.dll |
| 확인한 Release 추출 DLL | C:/Users/hyogu/Downloads/DuckbillFisher/.cache/release-validation/0.3.0.0/DuckbillFisher.dll |

캐시/참조 DLL은 Git에 없다. 다른 PC에서는 실제 경로와 정확한 지원 배포판을 다시 확인한다. 최소 참조가 없다고 현재 참조를 Release에 조용히 대체하지 않는다. 일반 빌드 명령은 [WORKFLOW](WORKFLOW.md#windows-빌드)에 한 곳만 유지한다.

## 배포 산출물과 로컬 상태

- 공개 0.3.0.0의 소스 태그 커밋은 7269627, 설치 JSON 게시 커밋은 fc300d5, 게시 확인 기록은 b33f4eb다.
- 사용자 확인 ZIP: artifacts/share/0.3.0.0/DuckbillFisher-0.3.0.0.zip. 크기/전체 SHA256/익명 다운로드 근거는 [Release 기록](validation/RELEASE-0.3.0.0.md)에 있다.
- Release 빌드 출력은 .cache/bait-preparation-release/, 일반 참조별 출력은 .cache/bait-preparation-build/·bait-preparation-oldrefs/·bait-preparation-friendrefs/였다.
- 확인된 ZIP을 다시 빌드해 바꾸거나 기존 태그/Release를 덮어쓰지 않는다. 제품 수정은 새 버전으로 준비한다.
- 개발·배포 플러그인의 같은 InternalName을 동시에 로드하지 않는다. 사용자가 현재 등록한 경로를 추측해 설정을 수정하지 않는다.
- 현재 PC는 gh CLI 없이 Git credential manager+GitHub API로 게시한 이력이 있다. 토큰은 메모리에서만 사용하며 출력/파일/Git에 남기지 않는다. 캐시의 버전별 게시 스크립트를 확인 없이 재실행하지 않는다.

## 이번 정비와 후속

- 핵심 문서를 현재 정책 중심으로 정리하고 문서/검증 색인과 프로젝트 스킬을 추가했다. 제품 코드·DLL 변경은 없다.
- 문서/스킬 형식 검사는 제품 빌드/테스트나 인게임 확인이 아니다. 과거 확인 범위는 [STATUS](STATUS.md)에 유지한다.
- 다음 게임 확인은 실제 저장소 업데이트/친구 새 기능이다. 미끼 페이지 연결 문제가 생기면 bait-ui/v7의 Pages 한 줄부터 요청한다.
- 일반 데이터 잔여 이름/ID/이유는 [보고서](reports/fishing-data-2026-10-17/remaining.md), 재생성 순서는 [data/README](../data/README.md)를 따른다. 완성 제품을 보완 단계 baseline으로 다시 넣지 않는다.
- Teamcraft 재조사·입질 통계·복수 어획 보류를 유지한다. 신규 후보는 [ROADMAP](ROADMAP.md)에서 사용자와 선택한다.
- 정리 전 핵심 문서는 b33f4eb2faa50503dd514a76c3a1e2a23d2e2df8의 Git 이력에 있다. 별도 전체 사본을 추가하지 않았다.
