# 현재 상태

갱신: 2026-10-16

## 확인한 환경과 결정

- 프로젝트명/표시명/InternalName: DuckbillFisher.
- 최소 배포 목표: 친구가 커스텀 URL로 설치·업데이트. 장기 목표: 공식 등록.
- 수첩 갱신은 사용자가 수첩을 열 때 수행하는 방식을 우선한다. 로그인 후 갱신은 대안이며 이번 진단에서는 선택 옵션(기본 꺼짐)으로 제공한다.
- 개발 방식 전환: 다음 세션부터 게임이 실행되는 Windows PC에서 직접 편집·Git 관리·빌드·인게임 확인한다. Mac 왕복은 기본 흐름에서 제외했다. 재개 순서와 소스/이력 이동은 docs/SESSION-HANDOFF.md.
- 대상 Windows 런타임: 한국 서버용 Dalamud D15.0.3.6. 기존 플러그인 실행 환경을 기준으로 개발한다.
- Mac: arm64. 저장소 `.dotnet/`에 SDK 10.0.402를 설치하고 공식 SHA512를 검증했다. 시스템 PATH는 변경하지 않았다.
- SDK: Dalamud.NET.Sdk 15.0.0 → .NET 10 / C# 14 / Plugin x64. Core는 net10.0.
- 컴파일 참조: upstream D15.0.3.6, revision `3e8e6eb456c928401febd1d9452c4b8d4cad0eb5`. URL/SHA256은 build/dalamud-reference.json.
- 원격 origin: PlatyDuckbill/DuckbillFisher. 원격 게시와 접근 검증은 별도로 진행한다.
- 현재 Windows 작업 루트는 `C:\Users\hyogu\Downloads\DuckbillFisher`이며 Git 2.56.0.windows.2와 .NET SDK 10.0.402, KR Dalamud 15.0.3.6을 재확인했다. PowerShell 7.6.7은 정상 설치되어 있다. 현재 도구의 샌드박스 안에서는 Store 실행 별칭과 실제 설치 파일 모두 실행이 차단되며, 승인된 샌드박스 밖 실행에서는 두 경로 모두 성공했다. 이후 `pwsh` 명령은 필요한 승인을 거쳐 샌드박스 밖에서 실행한다. 이번 확인에서는 빌드·Core 테스트를 실행하지 않았다.

## 구현 및 검증

- 목록 화면 범위 확정: MVP-01에서 전체 어종을 담는 행 목록·스크롤·검색·기본 정렬·수동 필터를 구현하고, 상세 조건/미끼/지도는 검증된 5~10종부터 채운다. 출현 시간 정렬은 MVP-03, 자동 수집 필터는 MVP-04다. docs/ROADMAP.md와 docs/PRODUCT.md에 반영했으며 목록 UI는 아직 구현 전이다.
- 0.1.3.0 로그인/캐릭터 경계의 코드 경로를 검토하고 기존 DLL로 수행할 절차를 docs/validation/ENV-03-FISH-SESSION.md에 정리했다. 사용자에게 재접속 전후 선택/비트 폐기 보고서를 요청했다. 결과 대기이며 이번 문서 작업에서 새 빌드·테스트는 실행하지 않았다.
- Plugin: `/duckbillfisher` 명령과 설치 관리자 Main UI 버튼으로 시작 창 열기, 이벤트/명령 해제.
- 개발 지침 추가: UI 리소스/고정 ImGui ID/언어와 독립된 진단, UI·상태·게임 접근의 책임 분리, 완료 시 관련 검사·문서 갱신 후 작업 단위 커밋. 기존 수첩 진단은 `28b7404`로 커밋했다. Git 작성자는 사용자 제공 값으로 이 저장소 로컬에 설정했다. 푸시하지 않았다.
- 0.1.3.0 다국어 기반: 기본 영어/한국어 .resx 84개 키, TextKey/UiText, 캐시된 메시지·이력의 키/인자 보관, 상단 언어 선택을 구현했다. 언어와 독립된 수첩 보고서 v2 및 수동 메모 코드를 사용한다. 어종 이름은 실제 게임 시트 언어를 유지한다. 세부 지침·구조는 docs/LOCALIZATION.md.
- 0.1.3.0 자동 검증: 리소스 키/인자 검사·Core 15개·Debug/Release 한섭 참조 빌드(각 경고/오류 0)·manifest 성공. 실제 리소스 로드/fallback/캐시 메시지 전환/진단 불변/호스트 문화권 불변을 게임 밖에서 확인했다. 한국어 satellite DLL을 포함한 ZIP·로컬 배포 준비·누락 번역 DLL/정규화 중복 경로 거부를 포함한 test-release.ps1도 통과했다.
- 0.1.3.0 한섭 보고서 수신: 언어 전환 절차 후 황금나비어 선택/True 표본/기록이 남았고 +80.083초에 어류도감 재개방→목록 갱신→비트 재조회가 확인됐다. 고정 영어 필드와 메시지 키의 v2 보고서다. 화면 번역/배치에 대한 별도 설명은 없으며 로그인/캐릭터 전환 경계는 미확인이다. 근거는 docs/LOCALIZATION.md.
- 0.1.2.0 수집 비트 진단: 선택한 일반 어종을 어류도감이 열린 상태에서 읽는 NativeFishCollectionReader를 추가했다. 조사한 게임 빌드와 DLL 해시/로드 모듈 MVID가 일치할 때만 동작하며 Framework 스레드·현재 캐릭터·IsInLog·ID 경계를 확인한다. 어종 클릭과 수첩 재개방에서 표본을 읽고 로그/보고서에 남긴다. unsafe는 이 어댑터 메서드에 격리했다. 수집 상태 자동 분류/필터는 아직 연결하지 않았다.
- 0.1.2.0 검증: 사용자 언로드 확인 후 build.ps1 성공, Core 15개 통과, 한섭 참조 Plugin 경고 0 / 오류 0, manifest 성공. 실제 보고서에서 황금나비어(ID 194/Item 7685)는 True, 홍룡(ID 696/Item 24993)은 False였고 사용자 확인 수집/미수집 상태와 각각 일치했다. 이미 로그인한 한 캐릭터의 기본 읽기 검증이며 로그인 직후 준비·캐릭터 전환·자동 필터는 미확인이다. 근거는 docs/validation/ENV-03-FISH-NATIVE.md.
- 0.1.1.0 수첩 진단 구현: `FishGuide2`/`FishingNote`의 표시를 공개 IGameGui 래퍼로 250ms마다 관찰해 열림 때 정적 FishParameter 목록을 준비한다. 수동 재조회, 이름/ID 검색, Item/EventItem 참조 확인, 로그인 갱신 옵션, 최근 80행 보고서를 추가했다. IsLoggedIn/IsLoaded/ContentId 변경 감지는 Framework.Update에서 수행하고 이전 캐릭터 표시를 비운다. ContentId는 보고서에 출력하지 않는다. 수집 여부는 Unknown이며 네이티브 수첩 비트는 읽지 않는다.
- 이번 Windows 빌드: 사용자 언로드 확인 후 한섭 15.0.3.6 참조로 build.ps1 성공. Core 15개 통과, Plugin 경고 0 / 오류 0, manifest 0.1.1.0/API 15 검사 성공. 절차/인게임 결과는 docs/validation/ENV-03-FISH-WIN.md.
- 0.1.1.0 한섭 기본 검증 통과: 사용자 보고서에서 FishingNote/FishGuide2 각각 3회 표시와 총 6회 갱신을 확인했다. 실제 FishParameter 2,512행 / IsInLog 1,518행 / 최대 IsInLog ID 1,522. 로그인·준비 상태는 둘 다 True인 기존 세션이며 개별 어종 선택과 로그인/재로드 경계는 미확인이다. 수집 비트는 아직 읽지 않았다. 원본은 docs/validation/ENV-03-FISH-WIN.md.
- ENV-03 수첩 API 조사: 실제 KR 게임 빌드 `2026.10.06.0000.0000`과 설치 DLL의 SHA256/revision을 기록했다. 설치 메타데이터와 고정 소스에서 `PlayerState.IsFishCaught(uint fishParameterId)`·IsInLog 호출 조건·비트 배열 구현을 대조했다. 공개 수집 서비스는 확보하지 못했으며 `IsLoaded`의 수첩 준비 보장도 미확인이다. `FishParameter.Item`은 EventItem/Item을 지원하는 비제네릭 RowRef다. 재현용 `scripts/inspect-fish-api.ps1` 실행 성공. 상세 근거와 다음 검증은 docs/validation/ENV-03-FISH-API.md. Plugin/Core 변경·빌드·테스트·인게임 수첩 읽기는 하지 않았다.
- Core: Unknown/Uncaught/Caught와 수동 override, 즐겨찾기 알림 대상 판정. 기존 테스트 9개 통과.
- ENV-03 공개 API 관찰: `ICondition.ConditionChange`의 Fishing/ExecutingGatheringAction/Casting을 사용자가 켠 동안 기록한다. 시작/중지 표본, 수동 메모, 단조 증가 경과 초, 최근 200행, 보고서 복사를 구현했다. 창 닫기와 별개로 관찰하며 Dispose 때 이벤트를 해제한다. 정밀 시전·입질을 추론하거나 수첩을 읽지 않는다.
- 이번 Mac 검증: build.ps1로 Core 테스트 15개(기존 9 + 관찰 6) 통과, 고정 upstream 15.0.3.6 참조 Debug Plugin 컴파일 경고 0 / 오류 0 및 manifest 검사 성공. 명령/결과는 docs/validation/ENV-03-local.md.
- ENV-03 한섭 관찰 2건 수신: 낚시 자체 중단 3회(20행)와 낚시 자세를 유지한 정상 어획 3회(22행), 모두 제외 0행. 실제 행동은 사용자에게 확인했다. 두 경우 모두 Fishing이 매 주기 켜졌다 꺼졌다. 정상 어획의 True 유지 구간은 22.750/23.367/26.632초로, 입질 시간으로 해석하지 않는다. Gathering은 각 주기 두 번 켜지고 Fishing=False에서도 켜졌으며 Casting은 계속 False였다. Fishing 전이를 경과 타이머 후보로 삼을 근거는 확보했다. 정밀 시점과 수첩 검증은 남아 있다. 원문과 판단은 docs/validation/ENV-03-WIN-RESULTS.md.
- ENV-03 관찰 기능 기본 검증 통과: 창 닫기/다시 열기에도 기록 유지, 중지 후 행 수 유지, 새 관찰 시작 시 이전 기록 초기화, 언로드/재로드 후 기본 중지·빈 기록을 사용자에게 확인했다. 재로드 후 이벤트 중복·로그아웃 등 추가 경계는 별도 미확인이다.
- 현재 로그에는 미끼·어종·어획 성공/실패가 없다. 어획 성공은 사용자 확인을 근거로 했으며 플래그에서 자동 판정하지 않았다. 정밀 타이머 전에는 행동 시점·조건·결과의 관찰 경로를 추가 조사한다.
- .NET SDK/의존성 lock 파일, Core/Plugin/Tests solution, GitHub Actions 구성.
- scripts/inspect-environment.ps1: 환경/참조 후보 경로 확인.
- scripts/build.ps1: locked restore → Core 테스트 → Plugin 빌드 → manifest 확인 → 선택적 전용 폴더 복사.
- scripts/prepare-release.ps1: ZIP/manifest 검사 → 버전별 ZIP·SHA256·설치 목록 로컬 생성.
- scripts/test-release.ps1: 정상 생성, 덮어쓰기/HTTP/필수 파일 누락/런타임 DLL 포함/API·버전 불일치/중복 항목 거부 확인.
- Mac에서 upstream 참조로 Release 컴파일: 경고 0 / 오류 0.
- Mac PowerShell 7.6.7에서 전체 빌드·테스트·복사 스크립트 통과. 배포 도구 테스트 통과.
- Windows x64 / PowerShell 7.6.7 / SDK 10.0.402에서 환경 확인과 build.ps1 실행 통과. 한섭 설치의 Dalamud 15.0.3.6을 참조해 Debug 빌드 성공, Core 테스트 9개 통과. 근거는 docs/validation/ENV-02-WIN.md.
- 한섭 인게임에서 첫 로드, 시작 창 열기·닫기, 언로드·재로드, 재로드 후 창/명령 중복 없음, 한글 표시 확인을 완료했다. ENV-02-WIN 통과.
- GitHub Actions 실행, URL 게시/친구 설치는 검증 대기.
- repo/pluginmaster.json은 빈 배열이다. 공개 배포 항목은 없다.

## 환경 미확인 항목

| 항목 | 상태 |
|---|---|
| Windows 버전/CPU | 환경 확인 출력: Microsoft Windows 10.0.26300 / X64 |
| KR 게임 클라이언트 빌드 | `C:\Program Files (x86)\FINAL FANTASY XIV - KOREA\game\ffxivgame.ver`: `2026.10.06.0000.0000` |
| 대상 환경의 Dalamud.dll 경로 | `%APPDATA%\XIVLauncherKR\addon\Hooks\dev`에서 15.0.3.6 확인 |
| 해당 설치의 Plugin 실행 호환성 | 시작 창·낚시 관찰·수첩 감지/시트 읽기 및 0.1.2.0 수집/미수집 각 1종 비트 대조 통과. 로그인 준비·추가 경계·전체 필터·정밀 시점은 미확인 |
| 개발 플러그인 등록 위치 | 빌드 출력 DLL 등록 후 로드 성공; 인게임 등록 UI의 세부 명칭은 별도 기록 없음 |
| 코드 라이선스 / 어종 데이터 출처·라이선스 | 정식 배포 전 선정·검토 |

## 다음 한 작업: ENV-03 로그인·캐릭터 전환 경계 확인

공개 플래그 관찰의 기본 구현과 한섭 검증을 마쳤다. 구현 근거는 docs/validation/ENV-03-API-NOTES.md, 실제 결과는 docs/validation/ENV-03-WIN-RESULTS.md에 있다. Windows 설치본의 수첩 API 조사는 docs/validation/ENV-03-FISH-API.md에 기록했다. ENV-03 전체와 실제 수첩 검증은 아직 완료하지 않았다.

현재 작업 폴더는 `C:\Users\hyogu\Downloads\DuckbillFisher`이며 Git 이력과 최신 관찰 코드를 확인했다. 같은 폴더에서 계속 개발한다. 실제 원격 접근·게시 상태는 별도 미확인이다.

1. 수첩 재개방 자동 조회는 0.1.3.0 보고서로 확인했다. 기존 두 어종의 ID/정답은 docs/validation/ENV-03-FISH-NATIVE.md에 있으며 같은 확인을 다시 요청할 필요가 없다.
2. 로그아웃/로그인·캐릭터 변경 때 선택/대기 요청/현재 비트 표본 폐기와 준비 전 Unknown을 확인한다. 기존 0.1.3.0 DLL의 결과를 요청한 상태이며 절차/기대 결과는 docs/validation/ENV-03-FISH-SESSION.md를 따른다. 다른 캐릭터에는 기존 기준 어종의 수집 정답을 적용하지 않는다.
3. 기본 수집 비트 읽기는 통과했다. 준비 조건과 실패 동작을 확정한 뒤 수집/미수집 표시와 전체 목록 필터에 연결한다. 재로드 후 이벤트 중복도 추가 검증으로 남아 있다.

타이머는 아직 미구현이다. 후속 타이머 실험에서는 Fishing 전이를 시작/구간 종료 후보로 삼되 입질 감지로 표현하지 않는다. 도중 활성화 시 과거 시작 시각은 Unknown으로 처리해야 한다. 회수 후 자세 유지·생미끼·재로드 후 이벤트 중복·지역 이동/로그아웃은 그 작업의 추가 검증으로 남긴다.

정밀 시전 감지와 전체 수첩 상태/준비 시점의 호환성은 아직 검증하지 않았다. 수집 비트 각 1종 대조의 성공을 모든 어종·캐릭터·로그인 경계의 보장으로 확대 해석하지 않는다.

## 후속 세션 입력 예시

> AGENTS.md, docs/SESSION-HANDOFF.md, docs/STATUS.md와 docs/LOCALIZATION.md를 읽고 이어서 진행해 줘. 0.1.3.0의 다국어 기반과 수첩 재개방 자동 조회는 확인했다. 로그인/캐릭터 전환 시 표본 폐기와 Unknown 유지 조건을 확인한 뒤 수집 상태 표시/필터에 연결해 줘. 완료한 작업은 관련 검증과 문서 갱신 후 커밋해 줘.
