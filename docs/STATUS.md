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
| 해당 설치의 Plugin 실행 호환성 | 시작 창의 로드·언로드·재로드 및 UI 동작 통과; 낚시 연동은 후속 검증 |
| 개발 플러그인 등록 위치 | 빌드 출력 DLL 등록 후 로드 성공; 인게임 등록 UI의 세부 명칭은 별도 기록 없음 |
| 코드 라이선스 / 어종 데이터 출처·라이선스 | 정식 배포 전 선정·검토 |

## 다음 한 작업: ENV-03

목표: 한섭에서 캐스팅 상태와 수첩 수집 상태를 읽을 수 있는지 작은 실험으로 검증한다. 첫 API 조사 결과는 docs/validation/ENV-03-API-NOTES.md에 기록했다.

1. 공개 ConditionChange 이벤트로 낚시 관련 상태 전이를 관찰하는 진단을 준비한다. 이것을 정밀 캐스팅 시작 시점이라고 단정하지 않는다.
2. 시전·입질·낚아채기·취소를 직접 수행하면서 관찰 상태와 실제 동작을 대조한다.
3. 수첩 읽기는 대상 버전의 API/구조와 ID 매핑을 확인한 뒤 읽기 전용 어댑터로 별도 검증한다. 글로벌 구조를 한섭에 곧바로 적용하지 않는다.
4. 수집 완료/미완료가 알려진 어종과 대조하고, 미확인 상태는 Unknown으로 유지한다.

정밀 시전 감지와 수첩 읽기의 한섭 런타임 호환성은 아직 검증하지 않았다. 이번 시작 창의 성공을 전체 낚시 기능의 호환성으로 확대 해석하지 않는다.

## 후속 세션 입력 예시

> AGENTS.md와 docs/STATUS.md를 읽고 ENV-03을 진행해 줘. 먼저 공개 API 기반 낚시 상태 관찰을 준비하고, Windows에서 검증할 동작과 기대 로그를 제공해 줘. 검증되지 않은 메모리 구조나 이벤트를 확정하지 마.
