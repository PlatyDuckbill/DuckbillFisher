# 문서 지도

**매번 읽기:** [AGENTS](../AGENTS.md) → [STATUS](STATUS.md). 새 세션/PC만 [HANDOFF](SESSION-HANDOFF.md)를 추가한다. 나머지는 이번 작업 주제에 맞춰 읽는다.

## 현재 동작과 작업 절차

| 필요한 정보 | 문서 |
|---|---|
| 현재 배포·대표 확인·미확인·다음 한 작업 | [STATUS](STATUS.md) |
| 현재 PC 경로·참조·재개 | [SESSION-HANDOFF](SESSION-HANDOFF.md) |
| 기능/기본값·수집/필터/알림/기록 계약 | [PRODUCT](PRODUCT.md) |
| 구현 책임·스레드·상태·저장 경계 | [ARCHITECTURE](ARCHITECTURE.md) |
| 남은 후보와 사용자 보류 | [ROADMAP](ROADMAP.md) |
| 빌드·커밋·문서 관리 규칙 | [WORKFLOW](WORKFLOW.md) |
| 친구 설치·Release 준비/게시/복구 | [DISTRIBUTION](DISTRIBUTION.md) |
| 새 Windows 개발 PC | [WINDOWS-SETUP](WINDOWS-SETUP.md) |
| 문자열·번역·리소스 검사 | [LOCALIZATION](LOCALIZATION.md) |

## 필요한 주제만 읽기

| 주제 | 설계/자료 | 실제 근거 |
|---|---|---|
| 미끼 준비·게임 UI 찾기 | [BAIT-PREPARATION](BAIT-PREPARATION.md) | [단계별 관찰/확인](validation/BAIT-PREPARATION.md) |
| 타이머·낚시 기록 | [CASTING-TIMER-HISTORY](CASTING-TIMER-HISTORY.md) | [관련 기록 색인](validation/README.md#타이머기록) |
| 남은 시간 정렬 | [LIST-SORTING](LIST-SORTING.md) | [정렬 확인](validation/MVP-01-REMAINING-TIME-SORT.md) |
| UI 구성·UX 조사 | [UI-UX-REDESIGN](UI-UX-REDESIGN.md) | [화면/필터 기록](validation/README.md#목록필터화면) |
| 데이터 출처/판정 | [DATA-SOURCES](DATA-SOURCES.md), [data/README](../data/README.md) | [보완 검사](validation/DATA-COMPLETION-2026-10-18.md), [잔여 목록](reports/fishing-data-2026-10-17/remaining.md) |
| 터주작 아이디어 | [TROPHY-FISHING-PROPOSALS](TROPHY-FISHING-PROPOSALS.md) | 후보 기록은 착수 승인과 다름 |
| 기존 외부 조사 | [REFERENCES](REFERENCES.md) | 조사일 기준. 새 정책/API 작업 때 재확인 |

## 이력과 스킬

- [검증 색인](validation/README.md): 보존된 관찰·구조·빌드·실기·배포 근거. 과거 문서의 대기/다음 작업은 당시 상태다.
- [릴리스 요약](releases/): 버전별 공개 안내.
- [과거 상태 사본](history/): 기존 이력 보존. 새 사본을 매번 추가하지 않고 Git 이력을 사용한다.
- [검증 기록 템플릿](templates/VALIDATION.md): 독립 근거가 필요할 때 관련 항목만 사용.
- [duckbill-release](../.agents/skills/duckbill-release/SKILL.md): 빌드·패키지·커스텀 저장소 배포 작업.
- [duckbill-kr-native](../.agents/skills/duckbill-kr-native/SKILL.md): KR API/모듈/메모리 계약 조사 작업.

2026-10-18 정리에서는 핵심 문서의 버전별 회고를 제거하고 현재 계약으로 통합했다. 기존 검증 파일명/외부 인용 경로는 유지한다. 정리 전 원문은 Git b33f4eb2faa50503dd514a76c3a1e2a23d2e2df8에 있다. 파일 생성·갱신 기준은 [문서 관리](WORKFLOW.md#문서-관리)를 따른다.
