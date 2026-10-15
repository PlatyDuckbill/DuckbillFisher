# ENV-03 선택 어종 수집 비트 읽기

확인일: 2026-10-15. 판정: **0.1.2.0 한섭 빌드 및 알려진 수집/미수집 각 1종 대조 통과**. 로그인 직후 준비 시점, 캐릭터 전환, 전체 자동 필터는 미검증이다.

## 비교 기준

0.1.1.0의 실제 KR 시트와 사용자의 게임 수첩 확인으로 다음 정답을 확보했다. 이름으로 식별하지 않고 FishParameter ID로 읽는다. 이 값은 이번 검증 캐릭터의 정답이며 다른 캐릭터의 수집 상태로 재사용하지 않는다.

| 어종 | FishParameter ID | Item ID | 사용자 확인 상태 | 0.1.2.0 원시 비트 |
|---|---|---|---|---|
| 황금나비어 | 194 | 7685 | 수집됨 | True |
| 홍룡 | 696 | 24993 | 미수집 | False |

0.1.1.0 보고서의 두 행은 IsInLog=True이고 Item 참조였다. 황금나비어의 수집 상태는 사용자 후속 답변으로, 홍룡은 미수집 어종 요청에 대한 응답으로 확인했다. 기준 어종 ID/상태를 제품 코드에 하드코딩하지 않았다.

## 네이티브 접근 근거와 제한

- 접근은 `NativeFishCollectionReader.Read` 한 메서드에 격리했다. Plugin의 unsafe 허용은 이 메서드를 컴파일하기 위해 켰으며 Core는 게임 라이브러리에 의존하지 않는다.
- 현재 게임 프로세스의 `ffxivgame.ver`가 **2026.10.06.0000.0000**인지 확인한다. 설치된 Dalamud/FFXIVClientStructs/InteropGenerator.Runtime 파일의 SHA256과 로드된 모듈의 MVID를 모두 고정 값과 비교한다. 파일·버전·모듈 확인이 실패하면 포인터를 읽기 전에 Unknown으로 비활성화한다. 확인은 생성 시 한 번 수행하고 렌더링 중 파일을 읽지 않는다.
- 해시와 소스 revision은 [설치본 조사](ENV-03-FISH-API.md)를 따른다. 추가로 설치 Mono.Cecil 메타데이터에서 확인한 MVID는 다음과 같다.

| 모듈 | MVID |
|---|---|
| Dalamud | `d2fe7e59-9fb6-480d-9e6d-00ece6410e34` |
| FFXIVClientStructs | `f13b1f1f-fd4a-441a-bdac-b3c5abb3675f` |
| InteropGenerator.Runtime | `f8f9f456-aaf5-4b57-9551-9fae48c2eb8b` |

- 설치 메타데이터는 `PlayerState.StaticAddressPointers.pInstance`가 공개 생성 getter이고, FFXIVClientStructs가 이미 해석한 정적 주소를 반환함을 확인했다. 직접 시그니처를 스캔하거나 글로벌 오프셋을 코드에 복사하지 않는다.
- 조회는 Framework.Update 스레드에서만 수행한다. 공개 로그인/준비 상태·세션 ID가 확인되고 어류도감 FishGuide2가 열려 있어야 한다. 현재 시트의 IsInLog=True·Item 참조 행, 1~1522의 FishParameter ID로 한정한다.
- 포인터는 호출 안에서만 얻고 저장하지 않는다. null 주소를 거부한 뒤 네이티브 IsLoaded/ContentId를 다시 확인해 같은 캐릭터인지 대조한다. 최종적으로 설치본의 `IsFishCaught(rowId)`를 호출한다. 마지막 ID 1522는 실제 KR 시트와 설치 Get의 포함 경계에 맞췄으며 TryGet으로 대체하지 않는다.
- 실제 포인터 역참조를 try/catch로 보호할 수 있다고 가정하지 않는다. 어댑터에서 잡는 예외는 호환성 파일/메타데이터 검사 중의 관리 예외뿐이다. 메모리 쓰기·게임 명령·입력 대행·추가 네이티브 후크는 없다.
- 캐릭터 변경/로그아웃/준비 상실 시 선택 추적·대기 요청·현재 비트 표본을 비운다. ContentId는 비교에만 사용하며 보고서에 출력하지 않는다.

## 사용자 동작과 표시

1. 게임의 **어류도감**을 열어 둔다. 낚시터 수첩 FishingNote만 열린 상태에서는 비트 조회를 허용하지 않는다.
2. `/duckbillfisher` → **수첩 연동 확인**에서 어종을 검색하고 이름을 클릭한다.
3. 다음 Framework 표본에서 읽어 **마지막 수집 비트**와 관찰 로그에 남긴다. 선택한 어종이 있으면 어류도감을 다시 열 때도 재조회한다. 버튼으로 같은 어종을 다시 읽을 수도 있다.
4. **수첩 진단 복사**로 선택 정보와 `Raw caught bit`를 복사한다. 다른 선택/세션의 표본을 선택 결과로 출력하지 않는다.

비트 True/False는 원시 관찰값이다. Collection은 계속 Unknown으로 두고 자동 미수집 판정/필터/알림에 사용하지 않는다. 일반 준비 플래그와 어류도감 표시만으로 로그인 직후 전체 수첩 비트 수신 완료를 확정하지 않는다. 닫은 뒤 표시된 값은 마지막 표본이며 지속 감시 결과가 아니다.

## 빌드

사용자의 언로드 확인 후 실행했다.

```powershell
pwsh -NoLogo -NoProfile -File scripts/build.ps1 `
  -DalamudPath "$env:APPDATA\XIVLauncherKR\addon\Hooks\dev"
```

- Windows PowerShell 7.6.7 / SDK 10.0.402 / KR Dalamud 15.0.3.6.
- Core 테스트 15개 통과, 실패/건너뜀 0. 네이티브 읽기를 이 Core 테스트로 검증했다고 표현하지 않는다.
- Plugin 0.1.2.0 Debug 빌드: 경고 0 / 오류 0, manifest 검사 성공.
- DLL: `C:\Users\hyogu\Downloads\DuckbillFisher\src\DuckbillFisher.Plugin\bin\Debug\DuckbillFisher.dll`.

## 실제 한섭 결과

사용자가 제공한 두 보고서는 Plugin 0.1.2.0 / Dalamud 15.0.3.6, Session 1, IsLoggedIn=True / IsLoaded=True, FishGuide2=True였다. 게임 빌드와 모듈/파일 호환성 검사가 통과했다. 정적 시트는 전체 2512 / IsInLog 1518 / 최대 ID 1522로 이전과 같았다.

선택과 원시 결과는 다음과 같다. 나머지 공통 헤더와 창 개폐 이력은 중복해 복제하지 않는다.

```text
Selected: FishParameter=194; Item=7685; IsInLog=True; name=황금나비어
Raw caught bit: True; 원시 수집 비트 표본입니다. 실제 수첩과 대조 전이며 자동 필터에는 사용하지 않습니다.
+38.529s 수집 비트 조회: FishParameter=194; raw=True; 원시 수집 비트 표본입니다. 실제 수첩과 대조 전이며 자동 필터에는 사용하지 않습니다.

Selected: FishParameter=696; Item=24993; IsInLog=True; name=홍룡
Raw caught bit: False; 원시 수집 비트 표본입니다. 실제 수첩과 대조 전이며 자동 필터에는 사용하지 않습니다.
+67.661s 수집 비트 조회: FishParameter=696; raw=False; 원시 수집 비트 표본입니다. 실제 수첩과 대조 전이며 자동 필터에는 사용하지 않습니다.
```

두 값 모두 사용자 정답과 일치했다. 이미 로그인한 한 캐릭터에서 어류도감을 연 상태의 기본 읽기를 확인한 결과다. 다른 캐릭터나 로그인 직후의 False가 항상 미수집을 뜻한다고 일반화하지 않는다.

## 다음 한 작업

선택 어종을 유지한 수첩 재개방 자동 재조회와 로그아웃/로그인 시 현재 표본 폐기를 확인한다. 로그인 준비 여부가 불확실한 경우 Unknown을 유지할 조건을 정리한 다음, 수집/미수집 표시와 전체 목록 필터를 연결한다. 다른 캐릭터에서 황금나비어/홍룡의 정답을 그대로 적용하지 않는다. 전체 1518종 수집 상태 스냅샷과 저장/수동 override 통합은 아직 구현하지 않았다.
