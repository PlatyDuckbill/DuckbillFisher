# ENV-03 API 조사 메모

## 조사 범위

고정한 upstream Dalamud 15.0.3.6 배포의 Dalamud.xml / FFXIVClientStructs.xml과 공개 소스를 조사했다. 아래는 구현 후보이며 한섭에서 낚시 데이터를 읽은 실행 결과가 아니다.

2026-10-15 Windows 설치본의 후속 조사와 고정 소스 대조는 [한섭 수첩 API 조사](ENV-03-FISH-API.md)에 기록했다. 실제 게임 빌드, DLL 해시/revision, IsFishCaught 구현, 비제네릭 Item 참조, 준비 상태의 미확인 범위를 포함한다. 아래 초기 후보와 인게임 수첩 읽기 성공을 구분한다.

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

공개 API 기반 관찰 코드를 구현했다. `FishingConditionObserver`는 `ICondition`만 사용하여 위 세 플래그의 변경을 구독하며, UI에서 명시적으로 시작한 동안에만 기록한다. Core의 `FishingObservation`은 단조 증가 시각 입력으로 경과 시간을 계산하고 최근 200행을 보관한다. 게임 동작을 추론하지 않는다.

시작/중지/메모와 대상 변경 이벤트에서만 플래그를 읽는다. 렌더링은 캐시된 관리 객체를 표시하며 게임 상태를 매 프레임 읽지 않는다. Dispose 시 이벤트를 해제한다. 네이티브 접근·수첩 읽기·자동 게임 입력은 추가하지 않았다.

구현 시 고정 배포의 `ICondition` indexer와 `ConditionChange` XML을 재확인했고, [공개 ICondition 문서](https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/ICondition/)도 대조했다. 신규 외부 코드/데이터를 복사해 넣지 않았다.

[한섭 관찰 결과](ENV-03-WIN-RESULTS.md) 2건을 수신했다. 낚시 자체 중단 3회와 자세를 유지한 정상 어획 3회 모두 Fishing이 각 주기 켜졌다 꺼졌다. Casting은 계속 False, Gathering은 각 주기 두 번 켜졌으며 Fishing=False에서도 켜졌다. Fishing 전이를 경과 타이머 후보로 삼을 근거는 확보했다. 정확한 입력/착수 지연과 입질 경계는 미검증이며 True 구간 길이를 입질 시간으로 쓰지 않는다. 나머지는 [Windows 절차](ENV-03-WIN.md)에 따라 검증한다.

## 정밀 타이머 전에 남은 관찰 정보

현재 로그는 세 플래그와 수동 메모만 기록한다. 미끼/생미끼 단계·어획 어종·성공/실패/취소·정확한 입질/낚아채기 시각은 자동 수집하지 않는다. 정상 어획 3회는 사용자가 확인한 사실이다.

정밀 타이머 단계에서는 위 동작·조건·결과를 어떤 대상 API에서 관찰할 수 있는지 별도로 조사한다. 확인되지 않은 값은 미확인으로 두고 플래그만으로 추정하지 않는다. 이번 결과를 입질 감지나 어획 성공 자동 판정의 검증으로 확대하지 않는다.
