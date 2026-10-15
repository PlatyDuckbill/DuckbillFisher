# ENV-03 API 조사 메모

## 조사 범위

고정한 upstream Dalamud 15.0.3.6 배포의 Dalamud.xml / FFXIVClientStructs.xml과 공개 소스를 조사했다. 아래는 구현 후보이며 한섭에서 낚시 데이터를 읽은 실행 결과가 아니다.

## 낚시 상태 관찰 후보

- `ICondition.ConditionChange` 이벤트와 `ConditionFlag.Fishing`, `ExecutingGatheringAction`, `Casting` 상태가 조사 대상이다.
- Fishing은 낚시 중이라는 상태이며 개별 찌 던지기의 시작 시점과 같다고 가정하지 않는다. 낚시 전·시전·대기·입질·낚아채기·취소를 직접 수행하며 전이를 대조해야 한다.
- 우선 공개 API로 상태 변화를 관찰하고 시점의 의미를 확인한다. 정밀한 시전 시작을 구분할 수 없으면 추가 API를 별도 검토한다.
- 근거: [D15 ConditionFlag 문서](https://dalamud.dev/api/Dalamud.Game.ClientState.Conditions/Enums/ConditionFlag/), 고정 배포 XML의 `ICondition.ConditionChange` 항목.

## 수첩 수집 상태 후보

- 고정 배포의 FFXIVClientStructs.xml에서 `PlayerState.IsFishCaught(uint)`와 `CaughtFish` 관련 항목을 확인했다.
- 이는 네이티브 구조 접근 후보이며 공개 Dalamud 서비스로 안전하게 감싼 수집 API를 확보한 상태는 아니다. 현재 Plugin은 네이티브 접근을 하지 않으며 unsafe도 비활성화돼 있다.
- [현재 upstream PlayerState 소스](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/UI/PlayerState.cs)는 FishParameter ID를 인자로 사용하고 IsInLog인 행에만 호출하도록 설명한다. 현재 main은 실제 한섭 설치의 구조 호환성을 입증하지 않는다.
- 구현 전에 실제 KR 설치의 구조/메서드 근거, 로그인 후 데이터 준비 여부, FishParameter ID와 Item ID 매핑을 확인한다. 임의의 글로벌 오프셋은 사용하지 않는다.
- 검증 시 수집/미수집이 알려진 어종을 대조하고, 수첩 로드·캐릭터 전환·로그아웃 조건도 확인한다. 읽기 준비가 확인되지 않으면 Unknown을 유지한다.

## 상태

초기 조사 완료. 진단 코드 작성과 실제 한섭 낚시 동작 대조는 다음 작업이며 아직 수행하지 않았다.
