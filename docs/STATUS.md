# 현재 상태

갱신: 2026-10-15

## 확인한 환경과 결정

- 프로젝트명/표시명/InternalName: DuckbillFisher.
- 최소 배포 목표: 친구가 커스텀 URL로 설치·업데이트. 장기 목표: 공식 등록.
- 개발 방식 전환: 다음 세션부터 게임이 실행되는 Windows PC에서 직접 편집·Git 관리·빌드·인게임 확인한다. Mac 왕복은 기본 흐름에서 제외했다. 재개 순서와 소스/이력 이동은 docs/SESSION-HANDOFF.md.
- 대상 Windows 런타임: 한국 서버용 Dalamud D15.0.3.6. 기존 플러그인 실행 환경을 기준으로 개발한다.
- Mac: arm64. 저장소 `.dotnet/`에 SDK 10.0.402를 설치하고 공식 SHA512를 검증했다. 시스템 PATH는 변경하지 않았다.
- SDK: Dalamud.NET.Sdk 15.0.0 → .NET 10 / C# 14 / Plugin x64. Core는 net10.0.
- 컴파일 참조: upstream D15.0.3.6, revision `3e8e6eb456c928401febd1d9452c4b8d4cad0eb5`. URL/SHA256은 build/dalamud-reference.json.
- 원격 origin: PlatyDuckbill/DuckbillFisher. 원격 게시와 접근 검증은 별도로 진행한다.

## 구현 및 검증

- Plugin: `/duckbillfisher` 명령과 설치 관리자 Main UI 버튼으로 시작 창 열기, 이벤트/명령 해제.
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
| KR 게임 클라이언트 빌드 | 미확인 |
| 대상 환경의 Dalamud.dll 경로 | `%APPDATA%\XIVLauncherKR\addon\Hooks\dev`에서 15.0.3.6 확인 |
| 해당 설치의 Plugin 실행 호환성 | 기존 시작 창과 ENV-03 관찰 기능 기본 검증 통과; 정밀 시점·추가 경계·수첩 읽기는 미확인 |
| 개발 플러그인 등록 위치 | 빌드 출력 DLL 등록 후 로드 성공; 인게임 등록 UI의 세부 명칭은 별도 기록 없음 |
| 코드 라이선스 / 어종 데이터 출처·라이선스 | 정식 배포 전 선정·검토 |

## 다음 한 작업: ENV-03 수첩 읽기 근거 조사

공개 플래그 관찰의 기본 구현과 한섭 검증을 마쳤다. 구현 근거는 docs/validation/ENV-03-API-NOTES.md, 실제 결과는 docs/validation/ENV-03-WIN-RESULTS.md에 있다. ENV-03 전체와 수첩 검증은 아직 완료하지 않았다.

새 Windows 세션에서는 먼저 docs/SESSION-HANDOFF.md에 따라 이번 커밋의 작업 폴더/이력을 받고 KR 환경을 확인한다. 기존 Downloads 소스가 최신 문서를 포함한다고 가정하지 않는다. 원격 푸시/접근은 미검증이므로 로컬 전달용 bundle/소스 ZIP 경로를 함께 제공한다.

1. 수첩 읽기의 공개 API 유무와 실제 KR 대상 버전의 구조/메서드 근거를 확인한다. 글로벌 최신 구조를 곧바로 적용하지 않는다.
2. FishParameter ID와 Item ID의 매핑, 수첩 준비 상태, 로그인/로그아웃 시 접근 가능 조건을 정리한다. 근거가 부족하면 어댑터 구현을 보류하고 Unknown/수동 필터 대안을 유지한다.
3. 근거를 확보한 범위만 읽기 전용 어댑터로 검증하고, 수집/미수집이 알려진 어종과 대조한다. 읽기 실패는 Unknown으로 유지한다.

타이머는 아직 미구현이다. 후속 타이머 실험에서는 Fishing 전이를 시작/구간 종료 후보로 삼되 입질 감지로 표현하지 않는다. 도중 활성화 시 과거 시작 시각은 Unknown으로 처리해야 한다. 회수 후 자세 유지·생미끼·재로드 후 이벤트 중복·지역 이동/로그아웃은 그 작업의 추가 검증으로 남긴다.

정밀 시전 감지와 수첩 읽기의 한섭 런타임 호환성은 아직 검증하지 않았다. 이번 시작 창의 성공을 전체 낚시 기능의 호환성으로 확대 해석하지 않는다.

## 후속 세션 입력 예시

> AGENTS.md, docs/SESSION-HANDOFF.md, docs/STATUS.md를 읽고 이어서 진행해 줘. 이제 이 Windows PC에서 직접 편집·빌드·게임 검증한다. 먼저 작업 루트와 현재 KR 환경을 확인하고 ENV-03 수첩 읽기의 API/버전 근거부터 조사해 줘. 기존 한섭 관찰 결과와 미확인 항목을 구분하고 검증되지 않은 네이티브 구조는 적용하지 마.
