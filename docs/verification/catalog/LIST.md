# MVP-01 첫 어종 목록

갱신: 2026-10-16. Plugin 0.1.4.0 및 수정본 0.1.4.1 / KR Dalamud 15.0.3.6. **검색·도감 없이 정적 목록 로드·확인 불가 행 집계 확인**. 화면/스크롤/언어 전환의 세부 검증은 남아 있다. MVP-01 전체 완료가 아니라 목록 표시의 첫 작업이다.

## 구현 범위

- 첫 탭 **어종 목록**: 아이콘·이름·ID·수집 상태 미확인 표시, 행 선택 강조.
- 게임 FishParameter 전체 행을 담는 스크롤 목록. 기본은 IsInLog 포함 어종만 표시하고 체크를 해제하면 전체 어종 범위로 넓힌다. 0.1.4.1부터 ID 0과 이름/참조 확인 불가 행은 일반 목록에서 제외하고 진단 옵션에서만 표시한다. 원본 데이터와 수첩 진단의 행 수는 유지한다.
- 이름 부분 검색 또는 FishParameter ID 정확 검색, 한국어 이름순/ID순 정렬. 동률은 ID로 정렬한다.
- 자료 변경·검색 입력·필터/정렬 변경 때 결과를 갱신하고, 매 프레임 검색하지 않는다. ImGuiListClipper로 보이는 행만 그리며 스크롤만으로 시트를 다시 읽지 않는다.
- 목록 탭을 처음 열면 Framework.Update에 정적 목록 로드를 요청한다. 게임 도감을 열 필요는 없다. 정적 목록과 캐릭터 수집 표본을 분리했으며 기존 수첩 진단은 도감 열림/로그인 경계를 유지한다.
- 한국어/영어 11개 리소스 키 추가. 언어 변경 시 입력·선택·정렬을 초기화하지 않는다.

낚시터·미끼·출현 시간·즐겨찾기·수동 수집 저장·지역/확장팩 필터는 후속 작업이다. 이 목록의 수집 상태는 Unknown이며 원시 비트를 자동 필터에 연결하지 않았다. 조건 데이터나 어종별 수집 정답을 추측해 추가하지 않았다.

## API 근거

현재 KR 설치의 Mono.Cecil 메타데이터에서 직접 확인했다. 새 외부 코드나 의존성을 도입하지 않았다.

- `Lumina.Excel.Sheets.Item.Icon`과 `EventItem.Icon`: UInt16. 기존 RowRef의 실제 참조 타입을 확인해 읽는다.
- `ITextureProvider.TryGetFromGameIcon(in GameIconLookup, out ISharedImmediateTexture)` 및 `TryGetWrap(out IDalamudTextureWrap, out Exception)`.
- `IDalamudTextureWrap.Handle`: 설치된 ImGui의 ImTextureID.
- `ImGui.ImGuiListClipper()` 생성, `Begin(count, height)`/`Step()`/DisplayStart/DisplayEnd/`Destroy()` 사용. 래퍼는 프레임 안에서 생성/해제하며 게임 포인터를 저장하지 않는다.

## 빌드와 검사

로드된 기존 Debug DLL을 건드리지 않도록 먼저 별도 출력 경로에서 빌드했다.

```powershell
dotnet build src/DuckbillFisher.Plugin/DuckbillFisher.Plugin.csproj --no-restore -c Debug `
  -p:DalamudLibPath=C:/Users/hyogu/AppData/Roaming/XIVLauncherKR/addon/Hooks/dev/ `
  -p:OutputPath=C:/Users/hyogu/Downloads/DuckbillFisher/.cache/mvp01-list-build/
pwsh -NoLogo -NoProfile -File scripts/check-localization.ps1
```

- 별도 출력의 한섭 참조 Plugin/Core 빌드: 경고 0 / 오류 0.
- 리소스 95개 키와 한국어 번역의 키/형식 인자 일치.
- 생성 manifest의 버전 0.1.4.0/API 15, 한국어 satellite DLL 존재 확인.
- 사용자 언로드 확인 후 같은 명령에서 OutputPath 지정만 제거해 기존 Debug DLL 경로에 빌드했다. 경고 0 / 오류 0. 기존 경로 재로드 후 목록/스크롤/검색/정렬/필터와 언어·기존 진단 동작의 확인을 요청한 상태다.
- 이 작업에서는 테스트를 새로 추가하거나 실행하지 않았다. 기존 Core 15개 결과는 0.1.3.0의 이력이다. 빌드 성공을 게임 렌더링/스크롤 성능 검증으로 표현하지 않는다.

## 게임 확인 절차

1. 사용자 언로드 후 기존 Debug DLL 경로에 0.1.4.0을 빌드하고 같은 경로로 재로드한다.
2. 게임 도감을 닫아 둔 채 `/duckbillfisher` → **어종 목록**을 연다. 원본은 IsInLog 1,518행 / 전체 2,512행이었다. 0.1.4.1의 일반 표시 수는 확인 불가 행을 제외하므로 이보다 적다. 원본 행 수와 표시 수를 구분한다.
3. 목록의 중간·맨 아래까지 스크롤한다. 행 누락/겹침/빈 공간/아이콘 이상 여부를 확인한다.
4. 황금나비어 또는 194를 검색하고 ID순 정렬을 확인한다. 0.1.4.1에서는 수첩 포함 체크를 풀어도 이름 확인 불가 행이 일반 목록에 나타나지 않아야 한다. 진단용 확인 불가 행 표시까지 켜면 원본 2,512행을 모두 볼 수 있어야 한다.
5. 행을 선택한 뒤 한국어/English 전환으로 입력·선택·정렬이 유지되는지 확인한다. 어종 이름은 게임 시트 언어를 유지한다.
6. 기존 수첩 연동 확인 탭에서 게임 도감을 열고 황금나비어를 재선택해 True와 새 보고서가 나오는지 확인한다. 목록 로드만으로 진단의 수집 표본이 생겨서는 안 된다.

언로드/재로드 후 중복 창·이벤트나 오류가 있으면 해당 동작과 Dalamud 오류 전문을 기록한다. 정적 어종 목록 유지와 캐릭터별 수집 표본 초기화는 서로 다른 동작이다.

## 0.1.4.0 사용자 결과와 0.1.4.1 수정

사용자가 검색 정상, 수집 여부 문제, 수첩 포함 필터 해제 시 다수의 이름 확인 불가 행을 보고했다. 필터를 켜도 남는 한 행의 ID는 후속 답변으로 **0**임을 확인했다. 수집 문제가 목록의 미확인 표시인지 기존 수첩 진단의 조회 실패인지는 답변이 없었다. 기존 네이티브 조회의 회귀나 스크롤/언어 전환까지 검증 완료로 단정하지 않는다.

- `FishRowResolution`으로 ID 0, 빈 아이템 참조, 미지원 참조 타입, 참조 행 부재, 빈 이름을 구분한다. 일반 목록에는 이름/참조가 확인된 행만 포함한다. 빈 아이콘이나 미검증 상세 조건만으로 정상 어종을 제외하지 않는다.
- 제외 수를 필터 범위별로 안내한다. 원본 행은 보존하고 **확인 불가 행 표시 (진단용)**에서 확인할 수 있다.
- **목록 진단 복사**: 고정 `MVP-01/catalog/v1` 형식으로 전체/표시 가능/확인 불가/수첩 포함 확인 불가 행 수와 사유별 집계, ID순 최대 80행의 참조/IsInLog/사유를 출력한다. 캐릭터 수집 정보는 포함하지 않는다.
- 목록의 수집 문구를 **수집 연동 예정**으로 바꿨다. 목록은 아직 수집 조회에 연결되지 않았으며 ‘수첩 포함’은 도감 등록 대상 여부다. 기존 네이티브 수집 비트 읽기는 변경하지 않았다.

0.1.4.1은 위 빌드 명령의 OutputPath를 `.cache/mvp01-list-fix-build/`로 바꾸어 별도 빌드했다. 경고 0 / 오류 0. 리소스 99개 키/인자 일치. 테스트를 새로 추가하거나 실행하지 않았다. 사용자 언로드 확인 후 OutputPath 지정을 제거해 기존 Debug DLL 경로에 빌드했다(경고/오류 0). 재로드 후 기본 목록의 ID 0/확인 불가 행 제외와 목록 진단 결과를 요청했으며 아직 대기다.

### 개발 DLL 누락과 복구

사용자가 Core DLL만 보인다고 보고했고 실제 Debug 폴더에서 주 DLL 부재를 확인했다. obj/Debug와 별도 출력에는 컴파일된 DLL이 남아 있었다. 원인은 확정하지 않았다. 정규 Debug 경로로 다시 빌드해 경고/오류 0으로 복구하고, 출력 파일의 실제 존재·크기 82,944바이트·어셈블리 버전 0.1.4.1·한국어 satellite DLL 존재를 직접 확인했다. 빌드 성공 메시지나 manifest만으로 주 DLL 존재를 확인했다고 취급하지 않는다. 테스트나 게임 로드를 새로 수행한 결과는 아니다.

## 0.1.4.1 실제 목록 진단

사용자가 0.1.4.1을 로드한 뒤 다음 보고서를 제공했다. **어류도감을 한 번도 열지 않았다**고 명시했다.

```text
DuckbillFisher MVP-01/catalog/v1
Plugin: 0.1.4.1
Rows: 2512; displayable: 2465; unresolved: 47; unresolved in log: 1
Status: [CatalogLoaded] Fish list loaded from the game client.
Resolution: ZeroRow; count: 1
Resolution: EmptyItemReference; count: 44
Resolution: EmptyName; count: 2
FishParameter=0; Item=0; IsInLog=True; resolution=ZeroRow
FishParameter=15026; EventItem=2002390; IsInLog=False; resolution=EmptyName
FishParameter=15058; EventItem=2002869; IsInLog=False; resolution=EmptyName
```

- 원본 2,512행 중 일반 목록에 사용할 수 있는 행은 2,465행이다. 제외 사유 1+44+2=47과 일치하며 참조 행 부재/미지원 참조 타입은 보고되지 않았다.
- 수첩 포함 확인 불가 행은 ID 0 한 행뿐이다. 기존 IsInLog 1,518행에서 이를 제외하면 일반 목록의 수첩 대상은 1,517행으로 계산된다. 이는 수집 완료 어종 수가 아니다.
- 44행은 아이템 참조 ID가 0이고 모두 IsInLog=False였다. 이름이 빈 2행도 IsInLog=False이며 위 EventItem 참조다. 이를 사용되지 않는 데이터라고 단정하거나 이름을 추측해 채우지 않는다.
- 도감을 열지 않아도 정적 목록 로드와 진단 복사가 가능함을 확인했다. 캐릭터 수집 비트 읽기나 수집 데이터 준비 완료를 확인한 결과는 아니다.
- 일반 화면에서 확인 불가 행이 사라졌다는 별도 설명, 수정 후 검색 재확인, 스크롤/정렬/언어 전환 결과는 받지 않았다. 보고서 집계 확인과 화면의 모든 동작 완료를 구분한다.

이번 결과 반영은 문서 변경이다. 새 빌드나 테스트를 실행하지 않았다. 후속 MVP-01 작업은 검증된 5~10종의 상세 데이터 구조·출처·낚시터/미끼부터 준비하며 화면 검증의 미확인 항목도 유지한다.
