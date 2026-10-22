# 문서 지도

**매번 읽기:** [AGENTS](../AGENTS.md) → [STATUS](STATUS.md). 새 세션/PC만 [HANDOFF](SESSION-HANDOFF.md)를 추가한다. 나머지는 이번 작업 주제에 맞춰 읽는다.

## 폴더별 역할

```text
docs/
  README.md · STATUS.md · SESSION-HANDOFF.md
  product/       현재 기능·화면·로드맵·후속 아이디어
  development/   구조·빌드·배포·번역·데이터 출처·템플릿
  verification/  기능별 검증 근거·데이터 잔여 보고서
  releases/      공개 버전별 릴리스 요약
  archive/       초기 조사·UI 수정 원문·과거 상태
```

## 현재 동작과 작업 절차

| 필요한 정보 | 문서 |
|---|---|
| 현재 배포·대표 확인·미확인·다음 한 작업 | [STATUS](STATUS.md) |
| 현재 PC 경로·참조·재개 | [SESSION-HANDOFF](SESSION-HANDOFF.md) |
| 기능/기본값·수집/필터/알림/기록 계약 | [PRODUCT](product/PRODUCT.md) |
| 구현 책임·스레드·상태·저장 경계 | [ARCHITECTURE](development/ARCHITECTURE.md) |
| 기능별 코드 배치·관리 코드 테스트 범위/한계 | [구조와 테스트 검증](verification/ARCHITECTURE-TESTS.md) |
| 남은 후보와 사용자 보류 | [ROADMAP](product/ROADMAP.md) |
| 빌드·커밋·문서 관리 규칙 | [WORKFLOW](development/WORKFLOW.md) |
| 친구 설치·Release 준비/게시/복구 | [DISTRIBUTION](development/DISTRIBUTION.md) |
| 새 Windows 개발 PC | [WINDOWS-SETUP](development/WINDOWS-SETUP.md) |
| 문자열·번역·리소스 검사 | [LOCALIZATION](development/LOCALIZATION.md) |

## 필요한 주제만 읽기

| 주제 | 설계/자료 | 실제 근거 |
|---|---|---|
| 미끼 준비·게임 UI 찾기 | [BAIT-PREPARATION](product/BAIT-PREPARATION.md) | [단계별 관찰/확인](verification/bait/BAIT-PREPARATION.md) |
| 타이머·낚시 기록 | [CASTING-TIMER](product/CASTING-TIMER.md) | [관련 기록 색인](verification/README.md#타이머기록) |
| 남은 시간 정렬 | [LIST-SORTING](product/LIST-SORTING.md) | [정렬 확인](verification/catalog/REMAINING-TIME-SORT.md) |
| UI 구성·UX 조사 | [UI-UX-REDESIGN](product/UI-UX-REDESIGN.md) | [UI 확인 요약](verification/catalog/UI.md) |
| 데이터 출처/판정 | [DATA-SOURCES](development/DATA-SOURCES.md), [data/README](../data/README.md) | [보완 검사](verification/data/DATA-COMPLETION-2026-10-18.md), [잔여 목록](verification/data/reports/fishing-data-2026-10-17/remaining.md) |
| 터주작 아이디어 | [TROPHY-FISHING-PROPOSALS](product/TROPHY-FISHING-PROPOSALS.md) | 후보 기록은 착수 승인과 다름 |
| 출현 일정표 | [동작·구조](product/SPAWN-SCHEDULE.md), [가상 HTML 시안](product/mockups/spawn-schedule.html) | 공개0.5.0.0 시간축/월 달력·고정 Release 대표 확인 완료. [확인 순서](verification/catalog/UI.md#0500-r7-옵션-제거와-단일-대상-전환-2026-10-21) |
| 기존 외부 조사 | [REFERENCES](development/REFERENCES.md) | 조사일 기준. 새 정책/API 작업 때 재확인 |

## 이력과 스킬

- [검증 색인](verification/README.md): 보존된 관찰·구조·빌드·실기·배포 근거. 과거 문서의 대기/다음 작업은 당시 상태다.
- [릴리스 요약](releases): 버전별 공개 안내.
- [과거 기록 색인](archive/README.md): 초기 환경/타이머 조사·UI 수정 원문·이전 상태 사본. 현재 정책과 분리한다.
- [검증 기록 템플릿](development/templates/VALIDATION.md): 독립 근거가 필요할 때 관련 항목만 사용.
- [duckbill-release](../.agents/skills/duckbill-release/SKILL.md): 빌드·패키지·커스텀 저장소 배포 작업.
- [duckbill-kr-native](../.agents/skills/duckbill-kr-native/SKILL.md): KR API/모듈/메모리 계약 조사 작업.

2026-10-18 정리에서는 핵심 문서의 버전별 회고를 제거하고 현재 계약으로 통합했다. 이번에는 제품/개발/검증/과거 기록으로 실제 경로를 나누고 UI 기록 12개를 원문 구역 단위로 통합했다. 저장소 내부 링크는 새 경로로 갱신했다. 이전 공개 커밋/태그의 경로는 당시 이력에서 조회한다. 정리 전 원문은 Git b33f4eb2faa50503dd514a76c3a1e2a23d2e2df8에 있다. 파일 생성·갱신 기준은 [문서 관리](development/WORKFLOW.md#문서-관리)를 따른다.
