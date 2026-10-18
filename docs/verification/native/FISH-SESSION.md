# ENV-03 수첩 조회의 로그인·캐릭터 경계

갱신: 2026-10-16. 대상: Plugin 0.1.3.0 / KR Dalamud 15.0.3.6. **로그아웃 후 이전 정보 폐기와 재접속 후 새 목록/비트 조회 확인**. 다른 캐릭터 전환은 미확인이다. 기존 DLL로 확인했으며 새 빌드나 언로드를 하지 않았다.

## 확인할 가설

로그아웃·준비 상실·캐릭터 변경을 관찰하면 이전 세션의 선택 추적, 대기 비트 요청, 공개 목록, 현재 비트 표본을 폐기한다. 정적 어종 시트 캐시는 재사용할 수 있지만 다른 캐릭터의 수집 정답은 재사용하지 않는다. 코드 검토를 실제 게임 검증 완료로 취급하지 않는다.

현재 `FishGuideDiagnostics.Poll`은 250ms마다 로그인/IsLoaded/ContentId를 관찰한다. 캐릭터 식별자가 달라지면 Session을 증가시키고 추적/대기 어종과 비트 표본을 비운다. `MainWindow`는 새 세션 또는 공개 목록 변경을 그릴 때 선택을 비운다. ContentId 자체는 보고서에 출력하지 않는다. 최근 관찰 이력은 경계 전후를 비교할 수 있도록 남는다.

## 같은 캐릭터 재접속

1. 로그인 갱신 옵션은 기본 꺼짐으로 둔다. 게임 어류도감을 열고 진단 탭에서 황금나비어를 선택해 원시 값 True를 확인한다. 이 정답은 기존 검증 캐릭터에서만 사용한다.
2. 플러그인을 언로드하지 않고 캐릭터 선택 화면으로 로그아웃한다. 언로드하면 기존 세션의 초기화 동작을 확인할 수 없다.
3. 같은 캐릭터에 재접속하고 게임 어류도감을 열기 전에 `/duckbillfisher`를 연다. 1초 이상 기다린 뒤 이전 어종 선택/비트가 사라졌는지 확인하고 수첩 진단을 복사한다.
4. 기대: 로그에 `[SessionCleared]`와 새로운 `[SessionReady]`, 새 Session이 나타난다. 수첩을 아직 열지 않았고 로그인 갱신이 꺼져 있으면 공개 목록 0행, `Selected`/`Raw caught bit` 없음이 기대된다. 기록에 남은 이전 `[BitReadLog]`는 현재 표본이 아니다.
5. 이어서 게임 어류도감을 열고 같은 어종을 다시 검색·선택한다. 새 `[BitReadLog]`가 생기고 실제 수첩 상태와 맞는지 확인한다.

로그인 과정에서 수첩이 이미 표시되었거나 로그인 갱신 옵션을 켰다면 목록 0행을 요구하지 않는다. 이때도 이전 선택/비트가 자동으로 복원되거나 이전 어종을 새 캐릭터에서 자동 조회해서는 안 된다.

## 다른 캐릭터 전환

가능하면 다른 캐릭터에서도 같은 순서를 수행한다. 게임 수첩을 열기 전 이전 선택/현재 비트가 없는지 확인한다. 새 어종을 선택할 때는 해당 캐릭터의 실제 게임 수첩 상태와 비교한다. 기존 황금나비어=True/홍룡=False 정답을 다른 캐릭터에 적용하지 않는다. 다른 캐릭터가 없으면 이 항목은 미실행으로 남긴다.

## 준비 상태와 결과 기록

- 로그인하지 않았거나 공개 플레이어 상태가 준비되지 않았으면 목록/비트는 비우고 기다림을 표시한다.
- 어류도감을 닫은 채 명시적으로 새 비트 조회를 요청하면 `[NativeGuideRequired]`의 Unknown이 기대된다. 닫기만 한 경우 표시된 마지막 표본은 새 조회 결과가 아니다.
- IsLoaded와 어류도감 표시만으로 전체 수첩 데이터 수신 완료를 보장하지 않는다. 이번 확인이 통과해도 전체 자동 필터나 로그인 직후 False의 정확성을 검증한 것으로 확대하지 않는다.
- 폴링 사이에 지나간 짧은 준비 상태는 로그에 없을 수 있다. 보고서에 없는 전이를 관찰했다고 기록하지 않는다.

결과는 보고서와 함께 선택/비트 초기화 여부, 재접속 후 새 조회 여부, 다른 캐릭터 수행 여부를 기록한다.

## 실제 한섭 결과: 재접속 전 정보 폐기

사용자 보고서에서 다음을 확인했다. 같은 캐릭터 재접속 절차에 대한 응답이며 보고서에는 캐릭터 식별자를 넣지 않으므로 다른 캐릭터 전환까지 확인했다고 해석하지 않는다.

```text
Plugin: 0.1.3.0; Dalamud: 15.0.3.6
Session: 3; sampled: True; IsLoggedIn=True; IsLoaded=True
FishGuide2=False; FishingNote=False; loginFallback=False
Open samples: 0; refreshes: 0; trigger: none
FishParameter rows: 0; IsInLog rows: 0; max IsInLog RowId: 0

+17.982s [BitReadLog] Caught-bit sample: FishParameter=194; raw=Unknown; Open the in-game Fish Guide and select the fish again.
+29.082s [WindowStateLog] FishGuide2: open
+29.082s [RowsRefreshLog] Refresh #3: Fish log opened: FishGuide2; all rows 2512 / in log 1518
+29.082s [BitReadLog] Caught-bit sample: FishParameter=194; raw=True; Raw caught-bit sample; compare it with the game fish log. Automatic filters do not use this value.
+72.365s [BitReadLog] Caught-bit sample: FishParameter=194; raw=True; Raw caught-bit sample; compare it with the game fish log. Automatic filters do not use this value.
+80.898s [SessionCleared] Session cleared: previous display discarded
+80.898s [PlayerStateChanged] State: IsLoggedIn=False, IsLoaded=not sampled
+135.081s [SessionReady] Player data ready: new session
+135.081s [PlayerStateChanged] State: IsLoggedIn=True, IsLoaded=True
```

- 로그아웃 전 황금나비어 True가 기록되어 있고, 로그아웃/재접속 뒤 Session 3에서 공개 목록·개방/갱신 횟수·마지막 트리거가 초기화됐다. 현재 `Selected`와 `Raw caught bit` 행은 없다. 과거 이력은 유지됐다.
- 사용자는 재접속 후 도감을 열기 전 황금나비어가 검색되지 않는다고 설명했다. 이는 공개 목록 0행과 일치하는 현재 진단 동작이다. 정적 어종의 존재 여부나 데이터 소실을 뜻하지 않는다.
- 재접속 전 +17.982초에 도감이 닫힌 상태의 명시적 비트 요청이 Unknown으로 처리되고, +29.082초에 도감을 연 뒤 새 조회가 True가 된 것도 확인했다. 이 결과를 재접속 후 새 조회로 해석하지 않는다.
- 첫 보고서에서 재접속 이후 새 비트 조회는 없었다. 이어서 도감 열기→황금나비어 검색/선택을 요청했고 아래 두 번째 보고서를 받았다. 다른 캐릭터 전환, 순간적인 대기 요청 폐기, 재로드 후 이벤트 중복은 추가 미확인이다.
- 같은 보고서의 ID 298 raw=True는 게임 수첩 정답을 별도로 받지 않았으므로 검증된 수집 어종 기준으로 추가하지 않는다.

## 실제 한섭 결과: 재접속 후 새 조회

```text
Session: 3; sampled: True; IsLoggedIn=True; IsLoaded=True
FishGuide2=True; FishingNote=False; loginFallback=False
Open samples: 3; refreshes: 3; trigger: Fish log opened: FishGuide2
FishParameter rows: 2512; IsInLog rows: 1518; max IsInLog RowId: 1522
Selected: FishParameter=194; Item=7685; IsInLog=True; name=황금나비어
Raw caught bit: True; [NativeRawSample] Raw caught-bit sample; compare it with the game fish log. Automatic filters do not use this value.
+219.116s [WindowStateLog] FishingNote: open
+219.116s [RowsRefreshLog] Refresh #1: Fish log opened: FishingNote; all rows 2512 / in log 1518
+227.915s [WindowStateLog] FishGuide2: open
+227.915s [RowsRefreshLog] Refresh #3: Fish log opened: FishGuide2; all rows 2512 / in log 1518
+229.482s [BitReadLog] Caught-bit sample: FishParameter=194; raw=True; Raw caught-bit sample; compare it with the game fish log. Automatic filters do not use this value.
```

Session 3에서 신규 목록 로드와 사용자 재선택 후 새 비트 조회를 확인했다. 이전 세션의 표본을 단순 재표시한 결과가 아니다. 기존 기준 캐릭터의 황금나비어=True와 일치한다. 이 결과는 같은 캐릭터의 재접속 한 경로를 검증하며, 로그인 직후 전체 비트의 준비 완료나 모든 캐릭터의 정확성을 보장하지 않는다.

이번 검증 기록 갱신에서는 Plugin 코드·DLL을 바꾸거나 빌드·테스트를 새로 실행하지 않았다.
