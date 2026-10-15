# 현재 상태

갱신: 2026-10-15

## 확인한 환경과 결정

- 프로젝트명/표시명/InternalName: DuckbillFisher.
- 최소 배포 목표: 친구가 커스텀 URL로 설치·업데이트. 장기 목표: 공식 등록.
- 대상 Windows 런타임: 한국 서버용 Dalamud D15.0.3.6. 기존 플러그인 실행 환경을 기준으로 개발한다.
- Mac: arm64. 저장소 `.dotnet/`에 SDK 10.0.402를 설치하고 공식 SHA512를 검증했다. 시스템 PATH는 변경하지 않았다.
- SDK: Dalamud.NET.Sdk 15.0.0 → .NET 10 / C# 14 / Plugin x64. Core는 net10.0.
- 컴파일 참조: upstream D15.0.3.6, revision `3e8e6eb456c928401febd1d9452c4b8d4cad0eb5`. URL/SHA256은 build/dalamud-reference.json.
- 원격 origin: PlatyDuckbill/DuckbillFisher. 원격 게시와 접근 검증은 별도로 진행한다.

## 구현 및 검증

- Plugin: `/duckbillfisher` 명령과 설치 관리자 Main UI 버튼으로 시작 창 열기, 이벤트/명령 해제.
- Core: Unknown/Uncaught/Caught와 수동 override, 즐겨찾기 알림 대상 판정. 테스트 9개 통과.
- .NET SDK/의존성 lock 파일, Core/Plugin/Tests solution, GitHub Actions 구성.
- scripts/inspect-environment.ps1: 환경/참조 후보 경로 확인.
- scripts/build.ps1: locked restore → Core 테스트 → Plugin 빌드 → manifest 확인 → 선택적 전용 폴더 복사.
- scripts/prepare-release.ps1: ZIP/manifest 검사 → 버전별 ZIP·SHA256·설치 목록 로컬 생성.
- scripts/test-release.ps1: 정상 생성, 덮어쓰기/HTTP/필수 파일 누락/런타임 DLL 포함/API·버전 불일치/중복 항목 거부 확인.
- Mac에서 upstream 참조로 Release 컴파일: 경고 0 / 오류 0.
- Mac PowerShell 7.6.7에서 전체 빌드·테스트·복사 스크립트 통과. 배포 도구 테스트 통과.
- 실제 Windows 실행, 게임 창/한글 표시, 로드·언로드·재로드, GitHub Actions 실행, URL 게시/친구 설치는 **미실행**.
- repo/pluginmaster.json은 빈 배열이다. 공개 배포 항목은 없다.

## 환경 미확인 항목

| 항목 | 상태 |
|---|---|
| Windows 버전/CPU | 미확인; 기본 설치 안내는 x64 기준 |
| KR 게임 클라이언트 빌드 | 미확인 |
| 대상 환경의 Dalamud.dll 경로 | 환경 확인 스크립트로 수집 예정 |
| 해당 설치의 Plugin 실행 호환성 | Windows 첫 로드 대기 |
| 개발 플러그인 등록 위치 | 해당 설치 설정에서 확인 예정 |
| 코드 라이선스 / 어종 데이터 출처·라이선스 | 정식 배포 전 선정·검토 |

## 다음 한 작업: ENV-02-WIN

목표: Windows에 개발 도구를 준비하고 시작 창의 실제 동작을 확인한다.

1. docs/WINDOWS-SETUP.md에 따라 .NET SDK 10.0.402, PowerShell 7, Git을 설치한다.
2. 소스 ZIP 또는 첫 푸시 이후 Git clone으로 Windows에 소스를 받는다.
3. 환경 확인 스크립트로 실제 Dalamud.dll 경로를 확인한다.
4. 해당 참조로 Debug 빌드하고 개발 플러그인 DLL을 등록한다.
5. 명령/버튼으로 창 열기, 한글 표시, 닫기, 언로드, 재로드 후 중복 없음 확인.
6. docs/templates/VALIDATION.md에 실제 게임 빌드와 결과를 기록한다.

이를 통과하면 ENV-03(캐스팅/수첩 관찰 가능성 검증)으로 진행한다. Mac 컴파일 결과만으로 ENV-02-WIN을 완료 처리하지 않는다.

## 후속 세션 입력 예시

> AGENTS.md와 docs/STATUS.md를 읽고 ENV-02-WIN을 진행해 줘. Windows의 실제 경로와 오류를 확인하며 한 단계씩 안내해 줘. upstream 컴파일과 한섭 실행 검증을 구분해 줘.
