# ENV-03 Windows 관찰 결과

판정: **낚시 자체 중단 3회·정상 어획 3회 및 관찰 기능 기본 수명 주기 확인 — 타이머 후보 확보, 정밀 시점과 추가 경계는 미확인**.

## 관찰 1: 찌 던지기 후 낚시 자체 중단, 3회

- 근거: 사용자가 복사한 보고서와 행동 설명. 직접 원격 수집한 결과가 아니다.
- 실제 행동: 찌를 던진 직후 **낚시 자체를 중단**하는 같은 패턴을 3회 반복했다. 물고기는 낚지 않았다. 낚싯대만 회수하고 낚시 자세를 유지한 실험이 아니다.
- Plugin assembly 0.1.0.0 / Dalamud assembly 15.0.3.6. 보고서 형식 ENV-03/public-conditions/v1.
- 관찰 시작 UTC: 2026-10-15T10:54:11.1124826+00:00.
- 사용한 소스 ZIP/커밋 SHA와 KR 게임 빌드는 별도 대조하지 않았다. Windows 새 빌드의 테스트 통과 수/경고·오류 출력도 이번 보고서에는 없다.
- START, 대상 CHANGE, STOP, 기록 복사를 실제 한섭 환경에서 확인했다. 전체 20행, 제외 0행. START와 STOP 사이 17.016초.

### 직접 확인한 패턴

| 반복 | Fishing=True 시각 | Fishing=False 시각 | ExecutingGatheringAction=True 시각 | Casting |
|---|---|---|---|---|
| 1 | +1.008초 | +5.675초 | +1.008, +3.141초 | 기록 내내 False |
| 2 | +6.208초 | +10.641초 | +6.208, +8.225초 | 기록 내내 False |
| 3 | +11.375초 | +16.175초 | +11.375, +13.792초 | 기록 내내 False |

- 각 반복에서 Fishing이 켜졌다 꺼졌으며 ExecutingGatheringAction은 두 번 켜졌다 꺼졌다. Casting 변경 이벤트는 없었다.
- 이 조건에서 Casting=True를 시전 감지 규칙으로 사용할 근거는 없다.
- ExecutingGatheringAction=True가 같은 반복 안에서 두 번 발생했으므로, 모든 True 이벤트를 새 시전으로 처리할 수 없다. 첫 번째와 두 번째 이벤트가 각각 정확히 어떤 동작 경계인지는 시각이 표시된 행동 대조 없이 확정하지 않는다.
- Fishing의 False는 이번 낚시 자체 중단을 포함한 주기에서 관찰됐다. 낚시 자세를 유지한 연속 시전에서 매번 변하는지, 일반 낚아채기나 입질 때 어떤 변화가 생기는지는 아직 알 수 없다.
- 위 결과를 바탕으로 타이머 시작/종료 규칙을 구현하지 않는다. 입질 감지·일반 낚아채기·회수 후 자세 유지·생미끼는 미검증이다.
- 중지 뒤 게임 행동에서 행 수가 유지되는지, 창 닫기·언로드·재로드·지역 이동·로그아웃 시 동작은 별도 검증 대기다.

### 원본 보고서

```text
DuckbillFisher ENV-03/public-conditions/v1
Plugin: 0.1.0.0; Dalamud: 15.0.3.6
Started UTC: 2026-10-15T10:54:11.1124826+00:00
Recording: False; retained: 20; dropped: 0
Times are monotonic seconds since START. MARK timestamps are user clicks, not action detection.
F=Fishing; G=ExecutingGatheringAction; C=Casting. Values are raw API flags, not fish collection status.
#0001 +0.000s START (snapshot) | F=False G=False C=False
#0002 +1.008s CHANGE ExecutingGatheringAction=True | F=True G=True C=False
#0003 +1.008s CHANGE Fishing=True | F=True G=True C=False
#0004 +2.424s CHANGE ExecutingGatheringAction=False | F=True G=False C=False
#0005 +3.141s CHANGE ExecutingGatheringAction=True | F=True G=True C=False
#0006 +3.624s CHANGE ExecutingGatheringAction=False | F=True G=False C=False
#0007 +5.675s CHANGE Fishing=False | F=False G=False C=False
#0008 +6.208s CHANGE ExecutingGatheringAction=True | F=True G=True C=False
#0009 +6.208s CHANGE Fishing=True | F=True G=True C=False
#0010 +7.574s CHANGE ExecutingGatheringAction=False | F=True G=False C=False
#0011 +8.225s CHANGE ExecutingGatheringAction=True | F=True G=True C=False
#0012 +8.608s CHANGE ExecutingGatheringAction=False | F=True G=False C=False
#0013 +10.641s CHANGE Fishing=False | F=False G=False C=False
#0014 +11.375s CHANGE ExecutingGatheringAction=True | F=True G=True C=False
#0015 +11.375s CHANGE Fishing=True | F=True G=True C=False
#0016 +12.791s CHANGE ExecutingGatheringAction=False | F=True G=False C=False
#0017 +13.792s CHANGE ExecutingGatheringAction=True | F=True G=True C=False
#0018 +14.158s CHANGE ExecutingGatheringAction=False | F=True G=False C=False
#0019 +16.175s CHANGE Fishing=False | F=False G=False C=False
#0020 +17.016s STOP (snapshot) | F=False G=False C=False
```

## 관찰 2: 낚시 자세를 유지한 정상 어획, 3회

- 근거: 사용자 보고서와 후속 행동 확인. 세 번 모두 실제 어획에 성공했고 회차 사이에는 낚시 자세를 유지했다. 세 번째 어획 후에만 낚시 자체를 중단했다.
- Plugin assembly 0.1.0.0 / Dalamud assembly 15.0.3.6. 시작 UTC 2026-10-15T10:57:40.7454161+00:00.
- 전체 22행, 제외 0행. START와 STOP 사이 80.698초. 구체적 입질/낚아채기 버튼 시각의 수동 메모는 없다.

| 반복 | Fishing=True | Fishing=False | Fishing=True 유지 구간 | Gathering=True 시각 |
|---|---|---|---|---|
| 1 | +1.342초 | +24.092초 | 22.750초 | +1.342, +13.776초 |
| 2 | +26.558초 | +49.925초 | 23.367초 | +26.558, +40.475초 |
| 3 | +51.009초 | +77.641초 | 26.632초 | +51.009, +68.158초 |

### 판단

- 관찰 1만으로는 알 수 없었던 점을 확인했다. **낚시 자세를 유지한 정상 어획에서도 Fishing이 매 주기 켜졌다 꺼진다.** 이 표본에서는 낚시 자세 유지 전체를 나타내는 플래그로 해석할 수 없다.
- Fishing False→True를 경과 타이머 시작, True→False를 해당 구간 종료 후보로 삼을 근거를 확보했다. 정확한 사용자 입력/찌 착수와의 지연은 측정하지 않았다. 모든 낚시 행동에 대한 호환성이나 정밀 입질 타이머가 입증된 것은 아니다.
- 위 유지 시간은 원시 플래그 구간 길이다. 입질까지 걸린 시간이나 어종별 입질 기대 구간으로 사용하지 않는다. 실제 낚아채기를 수행한 기록이고 입질/낚아채기 시각 메모가 없어 해당 경계를 분리할 수 없다.
- Gathering은 각 주기에서 두 번 켜졌고, +78.308초에는 Fishing=False인 상태에서도 켜졌다. 사용자 확인상 세 번째 어획 후 낚시 자체 중단이 마지막 행동이지만 이 이벤트를 중단 전용 이벤트로 일반화하지 않는다. Gathering=True만으로 시전을 판정하지 않는다.
- Casting은 전 구간 False다. 두 관찰에서 Casting을 낚시 시전 시작 기준으로 쓸 근거는 얻지 못했다.
- 생미끼·회수 후 자세 유지·입질 놓침·도중 활성화·수명 주기는 별도 검증이다. 수첩 데이터 읽기는 미구현이다.

### 원본 보고서

```text
DuckbillFisher ENV-03/public-conditions/v1
Plugin: 0.1.0.0; Dalamud: 15.0.3.6
Started UTC: 2026-10-15T10:57:40.7454161+00:00
Recording: False; retained: 22; dropped: 0
Times are monotonic seconds since START. MARK timestamps are user clicks, not action detection.
F=Fishing; G=ExecutingGatheringAction; C=Casting. Values are raw API flags, not fish collection status.
#0001 +0.000s START (snapshot) | F=False G=False C=False
#0002 +1.342s CHANGE ExecutingGatheringAction=True | F=True G=True C=False
#0003 +1.342s CHANGE Fishing=True | F=True G=True C=False
#0004 +2.758s CHANGE ExecutingGatheringAction=False | F=True G=False C=False
#0005 +13.776s CHANGE ExecutingGatheringAction=True | F=True G=True C=False
#0006 +23.759s CHANGE ExecutingGatheringAction=False | F=True G=False C=False
#0007 +24.092s CHANGE Fishing=False | F=False G=False C=False
#0008 +26.558s CHANGE ExecutingGatheringAction=True | F=True G=True C=False
#0009 +26.558s CHANGE Fishing=True | F=True G=True C=False
#0010 +28.275s CHANGE ExecutingGatheringAction=False | F=True G=False C=False
#0011 +40.475s CHANGE ExecutingGatheringAction=True | F=True G=True C=False
#0012 +49.575s CHANGE ExecutingGatheringAction=False | F=True G=False C=False
#0013 +49.925s CHANGE Fishing=False | F=False G=False C=False
#0014 +51.009s CHANGE ExecutingGatheringAction=True | F=True G=True C=False
#0015 +51.009s CHANGE Fishing=True | F=True G=True C=False
#0016 +52.408s CHANGE ExecutingGatheringAction=False | F=True G=False C=False
#0017 +68.158s CHANGE ExecutingGatheringAction=True | F=True G=True C=False
#0018 +77.341s CHANGE ExecutingGatheringAction=False | F=True G=False C=False
#0019 +77.641s CHANGE Fishing=False | F=False G=False C=False
#0020 +78.308s CHANGE ExecutingGatheringAction=True | F=False G=True C=False
#0021 +78.708s CHANGE ExecutingGatheringAction=False | F=False G=False C=False
#0022 +80.698s STOP (snapshot) | F=False G=False C=False
```

## 관찰 기능 후속 확인

- 사용자 확인: 관찰 중 창을 닫아도 기록 유지, 다시 열어 확인 가능.
- 사용자 확인: 관찰 중지 후 게임 행동을 해도 행 수 유지.
- 사용자 확인: 중지된 상태에서 새 관찰 시작 시 이전 기록 초기화. 시작하면 새 START 행이 생기므로 활성 관찰의 행 수가 0이라는 뜻은 아니다.
- 사용자 확인: DuckbillFisher 언로드/재로드 후 창을 열면 새 관찰 시작을 누르기 전 `중지됨`, `기록 0/200`으로 초기화된다. 언로드/재로드 절차를 다시 설명한 뒤 정상 동작 보고를 받았다.
- 재로드 후 실제 낚시 이벤트의 중복 여부는 별도 보고서가 없다. 도중 시작·지역 이동/로그아웃·생미끼 등도 미확인으로 유지한다.

## 다음 작업

공개 플래그 관찰 기능의 기본 검증은 통과했다. ENV-03에 남은 수첩 읽기의 대상 버전 API/구조·ID 매핑·준비 상태 근거를 조사한다. 추가 낚시 경계와 정밀 시점은 실제 타이머 구현 시 별도 완료 기준으로 검증한다. 이 결과로 ENV-03 전체나 정밀 입질 타이머가 완료됐다고 판정하지 않는다.
