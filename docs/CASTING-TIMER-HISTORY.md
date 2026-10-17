# 캐스팅 타이머와 낚시 히스토리

2026-10-17 · MVP-02/F07·F13 · 정규0.2.1.0의 실제 사용 피드백을 받아0.2.2.0 기록 관리 개선 중. 최신 합의는 캐릭터별100묶음/묶음별2000회이며 이전1000회 정책을 대체한다. [변경·확인](validation/MVP-02-HISTORY-MANAGEMENT.md).

## 요구사항

- 현재 **낚시터**와 현재 미끼, 개별 캐스팅 관찰 시각의 ET 및 단조 시계 경과를 표시한다. 지역 이름만으로 정확한 낚시터를 추측하지 않는다.
- 첫 캐스팅부터 낚싯대를 완전히 접기까지를 낚시 기록 한 묶음으로 구분한다. 개별 회수/휴식은 전체 종료와 다르다. 한국어 UI에 ‘연속 낚시’는 사용하지 않는다.
- 타이머 아래에는 현재 낚시 기록만 스크롤로 제공한다. 완전히 접으면 비우고 메인의 ‘낚시 기록’에만 남긴다.
- 캐스팅 한 번을 기록 하나로 묶는다. 미끼/생미끼 종류와 Item ID는 시작 시 복사하고 이후 미끼 변경으로 과거 기록을 바꾸지 않는다.
- 시작 UTC/ET, 결과 관찰까지 경과, 성공/실패/취소/중단/결과 미확인, 어획 Item ID/수량을 구분한다. 실패 시 어종이 알려지는 경우도 성공으로 바꾸지 않는다.
- 입질/낚아채기/결과 관찰 시각은 서로 다르다. 타이머와 기록의 대표 경과는 낚아채기 관찰에서 멈추며 결과 수신 경과는 별도 보존한다. 키 입력 자체나 서버 판정 시각으로 표현하지 않는다. 직접 회수 등 낚아채기 없는 기록은 종료 관찰 경과를 표시한다. 입질 초 수 통계/예측은 이번 범위가 아니다.
- 낚시터 이름 오른쪽 버튼으로 그 낚시터 어종을 연다. 기존 FishRowRenderer를 재사용하고, 메인의 검색/시간제/등급/지역/즐겨찾기/진단 필터는 적용하지 않는다. 수집 여부 버튼 하나와 기존 수동 우선 판정만 사용한다.
- 로그아웃/캐릭터 전환/지역 전환/재로드/관찰 공백 시 미완료 기록을 확정 성공/실패로 바꾸지 않는다. 이미 진행 중인 캐스팅을 늦게 본 경우 시작 시각을 만들어내지 않는다.
- 현재 설치본 밖에서는 새 네이티브 읽기를 비활성화한다. 자동 조작/명령/런타임 HTTP를 추가하지 않고 기존 수집/프리셋/알림 정책을 유지한다.

## 현재 코드와 검증 범위

- FishingConditionObserver는 ICondition의 Fishing/ExecutingGatheringAction/Casting 변경만 사용자가 기록을 켠 동안 보관한다. 자동 캐스팅/입질/어획 감지는 없다.
- [기존 실제 결과](validation/ENV-03-WIN-RESULTS.md): 중단 3회/정상 어획 3회의 플래그 표본. 정상 연속 어획에서도 Fishing은 각 주기 변하며 Casting은 계속 False였다. 전체 세션을 Fishing True 구간과 같다고 볼 수 없다.
- FishingRegionObserver는 공개 IClientState.TerritoryType을 읽는다. 낚시터 ID가 아니다.
- FishCollectionObserver의 LogMessage 1114는 새 도감 등록이다. 반복 어획/실패 기록에 재사용하지 않는다. 알림 자동 즐겨찾기 해제 정책도 바꾸지 않는다.
- FishCatalog에 FishingSpot별 ID/이름/좌표 및 어종 연결이 있으나 현위치 판정은 없다. 어종 목록 팝업은 정확한 Spot ID를 얻은 후 연결한다.
- 기존 정규 0.1.36.0의 전체 보기/프리셋과 일부 UI는 인게임 확인 대기다. 이번 조사로 이전 검증 상태가 바뀌지 않는다.
- 이후 [0.2.0.0 실제 KR 표본](validation/MVP-02-CASTING-PROBE.md#실제-kr-표본--사용자-제공-2026-10-17)으로 만능 루어/쪽빛청어/안갯빛 마을 및 일반 시전·입질·낚아채기·성공·직접 회수·완전 종료를 대조했다. [0.2.1.0 구현/남은 확인](validation/MVP-02-TIMER-HISTORY.md)은 이 표본의 범위 안에서 자동 기록을 시작하며 기존 관찰기는 개발자 진단으로 유지한다.

## 항목별 출처와 실패 동작

아래는 설치 파일/고정 소스/시트에서 확인한 후보이며 **새 런타임 이벤트의 정확도는 아직 확인하지 않았다**.

| 항목 | 우선 출처/관찰 후보 | 정적 근거와 정확도 한계 | 실패 동작 |
|---|---|---|---|
| 현재 지역 | 공개 IClientState.TerritoryType | 기존 실제 지역 필터 확인. 낚시터보다 넓은 구역 | 전환 중/미접속은 없음 |
| 정확한 낚시터 | 공개 IChatGui.LogMessage 1110의 lnum1=PlaceName + 당시 TerritoryType | KR FishingSpot 유효 333행에서 Territory+PlaceName 중복 0. 본인 메시지/발생 시점의 실기 확인 필요. 좌표 최근접이나 수첩의 선택 낚시터로 대체하지 않음 | 본인/매핑이 불명확하면 낚시터 미확인, 목록 버튼 비활성 |
| 캐스팅 시작/경과 | 검증 후 EventFramework.EventHandlerModule.FishingEventHandler.State의 CastingOut 전이 후보 | 설치 enum/구조/정적 주소 시그니처 확인. Framework 표본 시각이며 실제 키 입력/착수와 차이 있음. 매 프레임 읽어도 프레임 사이 전이는 놓칠 수 있음 | 최초 표본이 이미 진행 중이면 시작 미확인. 상태 누락/관찰 공백은 중단/미확인 |
| 연속 낚시 세션 | State의 PoleReady/Quitting/None 및 본인 시작1110/종료1111/회수5584 대조 | Fishing 플래그 하나로 세션을 묶지 않음. 5584는 ‘거두기’ 메시지로 전체 접기와 동일하다고 가정하지 않음 | 늦게 관찰한 세션은 시작 미확인, 종료 이유 별도 보존 |
| 현재 장착 미끼 | Native PlayerState.FishingBait | 설치 필드 0x4E4, uint Item ID. 공개 서비스의 해당 필드는 찾지 못함. 로컬 캐릭터/IsLoaded/호환성 확인 필요 | 미확인. 현재 장착 미끼를 생미끼의 실제 미끼로 대체하지 않음 |
| 캐스팅 생미끼 | CurrentCastBaitFlags/Mooch, 본인1121 Item, Swimbait 인덱스/저장 Item | 설치 구조에 flags와 3개 저장 미끼/선택 인덱스가 존재. 이벤트 순서와 어느 시점에 값이 고정되는지는 실기 확인 필요 | 실제 사용 Item이 불명확하면 종류만 표시하고 Item은 미확인 |
| 던진 UTC/ET | 시작을 관찰한 UTC + 단조 시계, 기존 Core의 UTC/ET 기준 | ET 1시간=실제175초. 사용자 입력 시각이 아닌 관찰 시각. UTC 변경은 경과에 영향 없음 | 시작 미확인 시 ET/경과를 생성하지 않음 |
| 어획 성공/어종 | 공개 LogMessage 3512 Item=lnum1; 복수3559 Item=lnum1/수량=lnum4 | 설치 KR macro 확인. SourceEntity로 본인 식별 후 진행 기록 하나에 연결해야 함. 주변인의 동일 로그/작살 결과/지연 중복을 제외해야 함 | 출처/진행 기록이 불명확하면 결과 미확인. 현재 기록에 임의 배정 금지 |
| 실패/취소/중단 | 공개 KR 로그1117/1118/1119/1120/1127/1129/3511/3515/3527 등, 상태 대조 | 각 메시지는 서로 다른 이유. 1118은 놓친 Item을 알려줌. 실패 시 성공 어종처럼 표시하지 않음. 3511 등 취소의 실제 발생 경계 확인 필요 | 해당 근거 없이 Fishing False/시간 초과를 실패로 바꾸지 않음 |
| 보관 실패/방생 | 3525=낚았으나 소지 불가, 3528/5528=놓아주기 | 어획 성공과 인벤토리 획득/나중 방생은 다름. 소장품 확인과 복수 어획도 분리 필요 | ‘얻은 어종’과 ‘소지품 획득’ 혼동 금지. 미검증 수량은 null |
| 낚시터 어종 | 게임 FishingSpot.Item → FishCatalog의 FishParameter 매핑 | 전체 지역 필터가 아닌 정확한 Spot ID. 미발견 어종을 임의 제거하지 않음. 먼바다/특수 낚시터 지원은 해당 로그/매핑 확인 후 | 위치 미확인 시 주변 지역 어종을 현재 낚시터처럼 표시하지 않음 |

### 공개 로그 이벤트의 수명/본인 판정

- ILogMessage/Parameters/SourceEntity는 콜백 안에서만 읽고 숫자/필요한 문자열 비교 결과만 복사한다. 네이티브 주소나 빌린 객체를 큐에 보관하지 않는다.
- 공개 SourceEntity에는 IsPlayer/Name/HomeWorldId가 있으나 공개 EntityId/IsLocalPlayer는 확인되지 않았다. 로컬 플레이어의 이름+HomeWorld를 콜백에서 대조하는 후보를 검토한다. 이름은 저장 파일/복사 진단에 포함하지 않는다.
- SourceEntity가 없는 메시지는 null이라는 이유만으로 본인으로 확정하지 않는다. KR 로컬 전용 계약과 활성 캐스팅/세션 대조를 실기에서 확인한다.
- LogMessage.FormatLogMessageForDebugging은 부작용 가능성이 문서화돼 있어 호출하지 않는다. 메시지의 표시 문장 파싱 대신 검수한 ID/매개변수 형식을 사용한다.
- 등록 로그1114와 이번 어획 로그는 별도 경로이며, 신규 등록/즐겨찾기 정책에 영향 주지 않는다.

## KR 설치본과 네이티브 후보 근거

- 게임 ffxivgame.ver=2026.10.06.0000.0000, Dalamud 15.0.3.6/API15. 실행 중 프로세스에 붙거나 새 네이티브 호출을 실행하지 않았다.
- Dalamud MVID `d2fe7e59-9fb6-480d-9e6d-00ece6410e34`; FFXIVClientStructs MVID `f13b1f1f-fd4a-441a-bdac-b3c5abb3675f`. 기존 NativeFishCollectionReader의 DLL SHA256 가드와 일치하는 파일을 검사했다.
- 설치 PDB SourceLink: Dalamud `896d83190a34e7ff92deef967ba635ebb7dbdbae`, ClientStructs `cae2c3755ef02f9f2728082ae8043669ac9fbfe3`.
- 설치 metadata와 고정 소스의 EventFramework 크기0x4800/HandlerModule0x00/낚시 핸들러 포인터0x70/핸들러 크기0x230/State0x1C8/BaitFlags0x1D8/Swimbait선택0x1DC/저장3개0x1E0을 대조했다. PlayerState.FishingBait는0x4E4다. 소스의 미해석 필드에 새 의미를 부여하지 않는다.
- EventFramework.Instance 시그니처 `48 8B 05 ?? ?? ?? ?? 48 85 C0 74 ?? 83 B8 ?? ?? ?? ?? ?? 7C`: KR 실행 파일에서 1회, 파일 오프셋0x601389.
- PlayerState.Instance 시그니처 `48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 75 06 F6 43 18 02`: 1회, 파일 오프셋0x955CE2.
- 위 오프셋은 **정적 파일 근거**이며 실행 주소가 아니다. 구현 시 새 하드코딩 오프셋/서명 스캔 대신 설치 생성형 StaticAddressPointers를 사용하고 Framework/로컬 플레이어/로그인/전환/IsLoaded/ContentId/호환성/null을 확인한다. 포인터는 같은 업데이트 안에서만 빌리고 타입화된 관리 표본을 게시한다.
- 시그니처 일치가 필드의 실시간 의미/수명 검증을 대신하지 않는다. 진단에서 상태값/시점만 확인한 뒤 사용자 기능에 연결한다. 전역 핸들러는 접속/낚시 밖에도 존재할 수 있으므로 포인터 존재를 낚시 중으로 해석하지 않는다.
- State.PullingPoleIn은 성공 직후/놓침/입질 없음/휴식에 공통이므로 결과 판정에 사용하지 않는다. Casting/State 플래그와 실제 결과 이벤트를 별도로 모델링한다.

## 합의한 화면/저장 정책

1. 별도 ‘캐스팅 타이머’ 화면: 낚시터+‘어종 보기’ 글자 버튼, 현재 미끼, 캐스팅 ET/경과, 이번 낚시 시작/경과를 상단 고정. 아래 현재 묶음 최근2000회 스크롤/최신순, 완전히 접으면 비운다. 사용자가 메인에서 열고 닫는다. 기록은 표시 여부와 분리한다.
2. 낚시터 어종은 타이머에 붙은 팝업 하나. 사용자 선택으로 수집 버튼은 `전체 ↔ 수집한 어종 숨기기` 두 가지로만 전환하고 기본 숨기기다. Unknown은 숨기기에서 유지한다. 클릭 상세는 기존 독립 상세 하나를 재사용한다. 추가 검색/일반 필터를 두지 않는다.
3. 메인 ‘낚시 기록’ 탭: 어종 목록/즐겨찾기를 먼저 유지하고 그 뒤에 둔다. 첫 화면은 지역·낚시터/날짜·기록 시작 시각/총 시전·성공/진행 상태 요약이다. 클릭하면 같은 탭에서 해당 묶음 상세를 보고 ‘목록으로’ 돌아온다. 묶음별 삭제/종료 기록 전체 삭제는 확인 후 실행하고 진행 중 기록은 보호한다. 캐릭터별 최신100묶음, 묶음별 최근2000회이며 초과 시 총 횟수는 유지한다.
4. 미완료 기록은 reload/logout/dispose/observation gap 원인을 보존하고 재개하지 않는다. 복원 시 이미 저장된 캐스팅은 다시 생성하지 않는다. 세션 시작을 못 본 경우 시간 미확인으로 남긴다.
5. 보관 파일은 일반 설정과 분리한schema2의 캐릭터/묶음별 로컬 파일이다. 현재 묶음만 원자적으로 교체하며 이전 기록 전체를 다시 쓰지 않는다. 요약만 먼저 캐시하고 선택한 묶음의 상세만 비동기로 읽는다. schema1은 원본을 남겨 가져오고 완료 마커로 삭제한 기록의 재등장을 방지한다. 캐릭터 키는 로컬 격리에만 쓰며 번역 문장은 저장하지 않는다. 손상 원본/확인되지 않은 시작 시각은 보존하고 읽기 실패 시 저장·삭제를 막는다. 용량 초과 정리에서도 진행 중 기록을 보호한다.
6. 이번 큰 기능 묶음은 사용자 동의로 0.2.0.0을 사용한다. 관찰 단계의 버전 상승을 전체 타이머/히스토리 구현·실기 완료로 표현하지 않는다. [첫 관찰 구현/빌드/확인 순서](validation/MVP-02-CASTING-PROBE.md)를 따른다.

## 구현 순서와 실기 완료 기준

1. 화면/수집 버튼/보관 범위는 합의 완료. 설치 API/시트 정적 조사를 바탕으로 먼저 관찰을 구현한다.
2. 첫 작은 작업은 명시적으로 켜는 KR 수동 낚시 관찰: 기존 플래그+네이티브 상태/현재 미끼+허용 ID의 공개 로그를 짧은 숫자 진단으로 함께 확인한다. 본인/외부 구분 결과와 세션 토큰만 남기고 채팅 본문/이름/ContentId는 보고서에 넣지 않는다. 성공/실패를 아직 자동 확정하지 않는다.
3. 일반 던지기→성공→재시전, 직접 회수/완전 종료, 알려진 실패 한 가지, 미끼 변경과 생미끼를 사용자 동작과 대조한다. 수량/소장품/보관 생미끼/먼바다는 확인한 것만 지원하고 나머지는 미확인으로 둔다. 전체 로그 대신 최근 동작의 제한된 요약을 받는다.
4. 확인한 전이를 Core 상태 모델/Plugin 어댑터에 연결하고 타이머/최근 기록/히스토리/낚시터 팝업을 순서대로 만든다. 캐릭터 전환/로그아웃/일시 중단/재로드와 중복 이벤트 방어를 포함한다.
5. 빌드/한영 리소스 검사/문서/커밋. 사용자가 요청하지 않은 테스트는 추가하거나 실행하지 않는다. 정규 DLL 교체 전 이 작업의 새 언로드 확인을 받는다. 다른 캐릭터 실기는 보유 캐릭터가 없어 보류 상태를 유지한다.

## 초기 정적 조사에서 실행한 것

- PowerShell/Mono.Cecil로 설치 DLL metadata/PDB SourceLink/IL/필드/enum 및 공개 서비스 후보를 읽었다. 네이티브 코드를 실행하지 않았다.
- 무시된 `.cache/casting-audit/casting-audit.csproj`를 `dotnet run`으로 실행해 **디스크의 KR sqpack 시트만** 읽었다. LogMessage macro/Action/FishingSpot 중복을 조사한 도구이며 단위 테스트/인게임 실행이 아니다.
- 무시된 `.cache/inspect-casting-addresses.ps1`로 설치 실행 파일 바이트에서 선언 시그니처를 대조했다. 프로세스 메모리 접근/후크/주소 실행 없음.
- 요구사항/조사 단계에서 Plugin 소스/버전/정규 DLL은 변경하지 않았다. Plugin 빌드/리소스 검사/테스트/인게임 검증은 이번 단계에서 실행하지 않았다.

## 고정 원본

라이선스: Dalamud AGPL-3.0, FFXIVClientStructs MIT. 이번에는 근거 열람만 했으며 외부 구현/후크 코드를 복사하거나 새 의존성을 도입하지 않았다.

- [공개 IChatGui와 콜백 수명](https://github.com/goatcorp/Dalamud/blob/896d83190a34e7ff92deef967ba635ebb7dbdbae/Dalamud/Plugin/Services/IChatGui.cs)
- [ILogMessage/숫자 매개변수](https://github.com/goatcorp/Dalamud/blob/896d83190a34e7ff92deef967ba635ebb7dbdbae/Dalamud/Game/Chat/LogMessage.cs), [SourceEntity의 공개 항목](https://github.com/goatcorp/Dalamud/blob/896d83190a34e7ff92deef967ba635ebb7dbdbae/Dalamud/Game/Chat/LogMessageEntity.cs)
- [낚시 상태/미끼 종류](https://github.com/aers/FFXIVClientStructs/blob/cae2c3755ef02f9f2728082ae8043669ac9fbfe3/FFXIVClientStructs/FFXIV/Client/Game/Event/FishingEventHandler.cs)
- [전역 접근](https://github.com/aers/FFXIVClientStructs/blob/cae2c3755ef02f9f2728082ae8043669ac9fbfe3/FFXIVClientStructs/FFXIV/Client/Game/Event/EventFramework.cs), [핸들러 수명 경계](https://github.com/aers/FFXIVClientStructs/blob/cae2c3755ef02f9f2728082ae8043669ac9fbfe3/FFXIVClientStructs/FFXIV/Client/Game/Event/EventHandlerModule.cs)
- [장착 미끼 필드](https://github.com/aers/FFXIVClientStructs/blob/cae2c3755ef02f9f2728082ae8043669ac9fbfe3/FFXIVClientStructs/FFXIV/Client/Game/UI/PlayerState.cs)
