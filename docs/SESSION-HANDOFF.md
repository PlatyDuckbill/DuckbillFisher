# 세션 인수인계

갱신: 2026-11-04. 현재 사실/확인 대기는 [STATUS](STATUS.md), 버전별 근거는 [검증 색인](verification/README.md)에 둔다.

## 재개 순서

1. AGENTS.md·STATUS와 git status --short를 확인한다. tmp_note.md는 읽기·수정·스테이징하지 않는다.
2. 현재 사용자 요청을 우선하고 [문서 지도](README.md)에서 필요한 문서만 고른다. 과거 verification/archive의 대기/다음 작업은 새 지시가 아니다.
3. 코드 책임은 [ARCHITECTURE](development/ARCHITECTURE.md), 명령은 [WORKFLOW](development/WORKFLOW.md), 패키지/게시 절차는 [DISTRIBUTION](development/DISTRIBUTION.md)을 따른다.
4. [duckbill-release](../.agents/skills/duckbill-release/SKILL.md)는 빌드/패키지, [duckbill-native](../.agents/skills/duckbill-native/SKILL.md)는 API·네이티브 실행 계약 조사 때만 적용한다.
5. 언로드는 상시 기본 상태다. 다시 묻지 않으며 파일 잠금 실패를 우회/강제 종료하지 않는다. 같은 InternalName의 개발/배포본을 동시에 로드하지 않는다.
6. 테스트는 명시 요청 범위에서만 추가·실행한다. 이번 요청의 테스트 허용을 이후 일반 작업의 상시 실행 지시로 확대하지 않는다.
7. 명시한 작업 파일만 커밋하며 push/배포는 별도 권한이다. 공개 ZIP/태그를 개발본으로 덮어쓰지 않는다.

## 환경과 경로

저장소 루트의 현재 PowerShell 세션에서 . ./scripts/enter-dev.ps1을 실행한다. 경로 선택은 [WINDOWS-SETUP](development/WINDOWS-SETUP.md#개발-환경-변수)을 따른다.

| 항목 | 환경 변수 또는 저장소 상대 경로 |
|---|---|
| 작업 폴더 | DUCKBILL_ROOT |
| 게임 | FFXIV_GAME_PATH · 이 PC 기본 C:/Program Files (x86)/FINAL FANTASY XIV - KOREA/game |
| KR 게임 빌드 | 2026.10.06.0000.0000 |
| 현재 KR 참조 | DALAMUD_HOME · 이 PC APPDATA/XIVLauncherKR/addon/Hooks/dev · wasdcxz15.0.3.9 |
| 글로벌 게임·참조 | 이 PC C:/Program Files (x86)/SquareEnix/FINAL FANTASY XIV - A Realm Reborn/game (2026.10.16) · APPDATA/XIVLauncher/addon/Hooks/dev (goatcorp 15.0.3.9). 빌드 검사에는 `-p:DalamudLibPath`로 지정 |
| 최소 지원 참조 | .cache/compat-15.0.3.6/full/ |
| 친구 배포판 참조 | .cache/friend-runtime/provider-refs/ |
| upstream 컴파일 참조 | .cache/upstream-15.0.3.6/refs/ · build/dalamud-reference.json 고정 해시 |
| SDK/대상 | global.json의10.0.402 · Dalamud SDK15.0.0 · x64/net10.0-windows |
| 정규 개발 DLL | DUCKBILL_ROOT/src/DuckbillFisher.Plugin/bin/Debug/DuckbillFisher.dll |
| 최근 별도 Debug | .cache/recommendation-build/DuckbillFisher.dll |
| 0.6.0.1 고정 Release 추출본 | .cache/release-validation/0.6.0.1/DuckbillFisher.dll · Release429963828 게시 |
| 0.6.0.1 고정 ZIP | artifacts/share/0.6.0.1/DuckbillFisher-0.6.0.1.zip · SHA256 1A5E380AA26D40C032A21856CF3A57BE3FC6F9D75B101A0C977385618E408B1C |
| 0.6.0.0 고정 Release 추출본 | .cache/release-validation/0.6.0.0/DuckbillFisher.dll · 대표 정상 응답 완료 |
| 0.6.0.0 고정 ZIP | artifacts/share/0.6.0.0/DuckbillFisher-0.6.0.0.zip |
| KR 15.0.3.9 대조 보고서 | .cache/compat-15.0.3.9/metadata.json |

0.5.4.0 이전 버전의 추출 DLL/ZIP/해시 경로는 각 [Release 기록](verification/README.md#배포와-호환성)에 있다. 참조/캐시 DLL은 Git에 없다. 다른 PC에서는 정확한 지원 배포판과 실제 경로를 확인한다. 최소 참조가 없다고 현재 참조로 Release를 조용히 대체하지 않는다. 같은 프로젝트의 참조별 빌드는 순서대로 하며 명령은 WORKFLOW에 한 곳만 유지한다.

## 개발본과 게시본 구분

- 현재 공개판은 0.6.0.1(소스4e5663c)이다. 게시 단계와 해시는 [0.6.0.1 기록](verification/releases/RELEASE-0.6.0.1.md)을 따른다. 기능 기준판 0.6.0.0 확인에는 위 고정 추출본을 사용하고 재빌드해 교체하지 않는다.
- 정규/별도 Debug와 고정 Release 추출본은 구분한다. 정규/별도 Debug를 게시본 확인에 쓰지 않으며 확인 뒤 실행 파일을 다시 빌드하지 않는다. 새 후보에 과거 대표 확인을 자동 적용하지 않는다.
- 이전 버전 확인에는 해당 Release 기록의 추출 DLL을 사용한다. 과거 테스트 허용(추천31개·추천22/알림20/설정32개 등)은 각각 당시 승인 범위였다.
- 설치 목록의 공용 배포는 DuckbillRepo의 `pluginmaster.json`이며 이 저장소의 repo/release.json이 원본이다. 동기화 절차는 [DISTRIBUTION](development/DISTRIBUTION.md)을 따른다.
- 현재 PC의 게시 이력은 gh CLI 없이 Git credential manager+GitHub API를 사용했다. 토큰은 메모리에서만 쓰고 출력/파일/Git에 남기지 않는다. 캐시의 과거 게시 스크립트는 확인 없이 재실행하지 않는다.

## 자료와 진단 재개

- 일반 자료의 남은 이름/ID/이유는 [잔여 보고서](verification/data/reports/fishing-data-2026-10-17/remaining.md), 재생성은 [data/README](../data/README.md)를 따른다. 완성 자료를 보완 단계 baseline으로 다시 넣지 않는다.
- 초기 예측 고정 기대값은10종이며 보존 v1 입력은140종이다. [fixture 설명](../tests/DuckbillFisher.Core.Tests/Fixtures/README.md)을 확인하고 자동 덮어쓰지 않는다. 제품은 v2 조건/v1 등급을 내장한다.
- 진단은 필요한 짧은 구역부터 요청한다. 자료는 catalog/v10의 DataValidation/IntuitionForecast, 예측은 summary/v11의 SelectedForecast/ForecastComparison/SelectedConditions/SelectedIntuition, 음원은 alerts/v8의 CustomSoundState/Code, 미끼는 bait-ui/v7의 Pages를 사용한다.
- 타이머 상태 API의 정적 근거는 .cache/timer-status-api.json, 시트 도우미는 .cache/timer-status-audit/다. 기존 공개판 근거는 [타이머 기록](verification/fishing/TIMER-HISTORY.md)에 있다.
- 예측 정적 자료 도우미/UTC·CSV와 비교 한계는 [FORECAST](verification/forecast/FORECAST.md)에, 과거 소스/문서 경로는 Git 이력에 보존된다. 문서 전체 사본이나 새로운 재개 상태 사본을 반복 생성하지 않는다.
