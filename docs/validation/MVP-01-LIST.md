# MVP-01 첫 어종 목록

작성: 2026-10-16. Plugin 0.1.4.0 / KR Dalamud 15.0.3.6. **인게임 검증 대기**. MVP-01 전체 완료가 아니라 목록 표시의 첫 작업이다.

## 구현 범위

- 첫 탭 **어종 목록**: 아이콘·이름·ID·수집 상태 미확인 표시, 행 선택 강조.
- 게임 FishParameter 전체 행을 담는 스크롤 목록. 기본은 IsInLog 포함 어종만 표시하고 체크를 해제하면 전체 행을 표시한다. 미확인/이름 없는 행도 전체 행 모드에서는 숨기지 않는다.
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
2. 게임 도감을 닫아 둔 채 `/duckbillfisher` → **어종 목록**을 연다. 목록이 표시되고 기본 필터에서 1,518종 / 전체 2,512행인지 확인한다(기존 설치본의 기대값).
3. 목록의 중간·맨 아래까지 스크롤한다. 행 누락/겹침/빈 공간/아이콘 이상 여부를 확인한다.
4. 황금나비어 또는 194를 검색한다. 검색을 비우고 ID순으로 바꾼 뒤, 수첩 포함 체크를 해제하면 2,512행이 나타나는지 확인한다.
5. 행을 선택한 뒤 한국어/English 전환으로 입력·선택·정렬이 유지되는지 확인한다. 어종 이름은 게임 시트 언어를 유지한다.
6. 기존 수첩 연동 확인 탭에서 게임 도감을 열고 황금나비어를 재선택해 True와 새 보고서가 나오는지 확인한다. 목록 로드만으로 진단의 수집 표본이 생겨서는 안 된다.

언로드/재로드 후 중복 창·이벤트나 오류가 있으면 해당 동작과 Dalamud 오류 전문을 기록한다. 정적 어종 목록 유지와 캐릭터별 수집 표본 초기화는 서로 다른 동작이다.
