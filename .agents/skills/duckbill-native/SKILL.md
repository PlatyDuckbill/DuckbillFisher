---
name: duckbill-native
description: Investigate DuckbillFisher game APIs, native observations and per-feature runtime contracts on KR and global clients. Use for native feature work, a feature reporting unavailable, log contract review after a patch, or validating new contracts; not for routine UI, localization or static data edits.
---

# API·네이티브 실행 계약 조사

저장소 루트는 이 파일에서 `../../..`이다. [AGENTS](../../../AGENTS.md)의 게임 경계와 [ARCHITECTURE](../../../docs/development/ARCHITECTURE.md#호환성과-실패)를 유지한다. 이 스킬은 게임 실행이나 자동 조작을 승인하지 않는다.

## 먼저 범위 좁히기

1. 상태와 환경을 확인하고, 증상이나 원하는 관찰을 한 가지로 정한다.
   - 확인 범위: [STATUS](../../../docs/STATUS.md)
   - 현재 참조: [HANDOFF](../../../docs/SESSION-HANDOFF.md#환경과-경로)
2. [검증 색인](../../../docs/verification/README.md)에서 해당 계약 기록만 읽는다.
   - 지역 공통 계약과 로그: [GLOBAL-COMPAT](../../../docs/verification/native/GLOBAL-COMPAT.md)
   - 미끼: BAIT-PREPARATION
   - 시전: verification/fishing의 기능별 기록
3. 공개 API로 얻을 수 있는지 먼저 확인한다. 안 되면 ClientStructs 공개 멤버(정적 주소, 함수 포인터, 구조체 필드)로 해결한다.
   - KR과 글로벌 설치 DLL **양쪽**의 메타데이터·IL을 대조한다.
   - 하드코딩 오프셋이나 자체 시그니처는 도입하지 않는다.

## 조사 결과에 필요한 사실

| 항목 | 확인할 것 |
|---|---|
| 환경 | 게임 빌드, 클라이언트 언어, 진단의 NativeEnvironment(모듈 버전/MVID) |
| 계약 | 사용하는 ClientStructs 멤버가 KR·글로벌 양쪽에 같은 시그니처로 있는지, 로그 매개변수 결합 |
| 검증 | 실행 중 검사(주소 해석 여부, 값 범위, UI 형태, 교차 일치)로 실패를 잡을 수 있는지 |
| 수명 | 실행 스레드, 객체 생성/폐기, 복사 시점, 세션 전환/종료 |
| 의미 | 원시 값, 사람의 행동 설명, 제품 판정이 각각 어디까지인지 |
| 실패 | 그 기능만 Unknown/사용 불가가 되는지, 다른 기능으로 차단이 번지지 않는지 |

- DLL 해시나 게임 빌드 일치를 판정 조건으로 다시 넣지 않는다. 문제는 해당 기능의 실행 계약 검사로 해결한다.
- 본인 여부가 불명확한 로그, Fishing 플래그나 Phase 하나, UI 위치나 아이콘만으로 어종·미끼·성공을 추론하지 않는다. 번역 문장으로 판정하지 않는다.
- 게임 패치 뒤에는 WORKFLOW의 로그 계약 점검(`FishingDataImport --log-contracts`)을 실행한다. 결합이 없는 행의 문장 변경은 의미를 사람이 확인한다.
- Framework의 유효 수명 안에서 읽어 관리 값으로 전달한다. 포인터 보관, Draw 중 읽기, try-catch에 의존한 무효 접근은 추가하지 않는다.
- 추가 자료는 실패를 가르는 최소 필드부터 요청한다. 기존 자료가 충분하면 전체 로그를 다시 받지 않는다. 원문, 주소, 캐릭터 식별자, 게임 DLL을 Git에 넣지 않는다.
- 구조·컴파일 대조와 실제 게임 확인은 별개다. 근거가 부족하면 해당 관찰을 Unknown이나 비활성으로 남긴다.

근거, 판정, 실기 대기는 기존 주제 검증 문서에 추가한다. 빌드가 필요하면 WORKFLOW를 따르며, KR과 글로벌 참조 양쪽에서 빌드한다. 새 게임 표본을 요청할 때는 구체적인 수동 동작과 필요한 짧은 출력만 제시한다.
