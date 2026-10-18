---
name: duckbill-kr-native
description: Investigate DuckbillFisher KR game APIs, native observations and distribution-specific DLL compatibility. Use for native feature work, NativeDllMismatch or validating new KR contracts; not for routine UI, localization or static data edits.
---

# KR API·네이티브 계약 조사

저장소 루트는 이 파일에서 `../../..`이다. [AGENTS](../../../AGENTS.md)의 게임 경계와 [ARCHITECTURE](../../../docs/development/ARCHITECTURE.md#호환성과-실패)를 유지한다. 게임 실행/자동 조작이나 가드 완화를 승인하는 스킬이 아니다.

## 먼저 범위 좁히기

1. [STATUS](../../../docs/STATUS.md)에서 확인 범위를, [HANDOFF](../../../docs/SESSION-HANDOFF.md#환경과-경로)에서 현재 참조를 읽는다. 증상/원하는 관찰 한 가지를 정한다.
2. [검증 색인](../../../docs/verification/README.md)에서 해당 계약만 읽는다. 런타임 차이는 NATIVE-RUNTIME 문서, 미끼는 BAIT-PREPARATION, 시전은 verification/fishing의 기능별 기록을 따른다.
3. 공개 API로 얻을 수 있는지 먼저 확인한다. 실패 시 실제 KR 설치 DLL·고정 소스의 구조/필드/IL과 필요한 게임 실행 표본을 대조한다. 글로벌 최신 소스만으로 접근을 확정하지 않는다.

## 조사 결과에 필요한 사실

| 항목 | 확인할 것 |
|---|---|
| 환경 | 게임 빌드, 배포판, 실제 로드한 모듈의 버전/MVID/SHA256 |
| 계약 | 필요한 타입/필드 크기·오프셋·호출/로그 인자와 고정 소스 근거 |
| 수명 | 실행 스레드, 객체 생성/폐기, 복사 시점, 세션 전환/종료 |
| 의미 | 원시 값, 사람의 행동 설명, 제품 판정이 각각 어디까지인지 |
| 실패 | Unknown/지원 불가/마지막 표본의 구분과 영향받는 기능 |

- 같은 버전 번호도 배포판별 모듈이 다를 수 있다. NativeRuntimeCompatibility의 **조합 단위** 판정을 유지한다. 해시 제거·개별 모듈 혼합·버전만 허용으로 문제를 숨기지 않는다.
- 본인 여부가 불명확한 로그, Fishing 플래그/Phase 하나, UI 위치/아이콘만으로 어종·미끼·성공을 추론하지 않는다. 기존 제품 연결과 원시 진단을 구분한다.
- Framework의 유효 수명 안에서 읽어 관리 값으로 전달한다. 포인터 보관/Draw 읽기/try-catch에 의존한 무효 접근은 추가하지 않는다.
- 추가 자료는 실패를 가르는 최소 필드부터 요청한다. 기존 자료가 충분하면 다시 전체 로그를 받지 않는다. 원문/주소/캐릭터 식별자/게임 DLL을 Git에 넣지 않는다.
- 구조/컴파일 대조와 실제 게임 확인은 별개다. 근거 부족이면 해당 관찰을 Unknown/비활성으로 남기고 나머지 검증 기능에 불필요한 차단을 전파하지 않는다.

기존 주제 검증 문서에 근거·판정·실기 대기를 추가한다. 빌드가 필요하면 WORKFLOW를 따른다. 새 게임 표본 요청은 구체적인 수동 동작과 필요한 짧은 출력만 제시한다.
