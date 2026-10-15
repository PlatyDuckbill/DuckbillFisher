# Windows 세션 인수인계

갱신: 2026-10-15. 다음 세션부터 **FFXIV가 실행되는 Windows PC에서 직접 개발**한다. 이 문서와 STATUS가 재개 기준이며 이전 대화 전체를 전제로 하지 않는다.

## 새 세션에서 먼저 읽기

1. `AGENTS.md`: 구현 경계와 검증/인수인계 규칙.
2. `docs/STATUS.md`: 현재 완료 범위와 다음 한 작업.
3. `docs/WORKFLOW.md`: 같은 Windows PC에서 편집 → 언로드 → 빌드 → 로드 → 사람이 게임 확인.
4. `docs/validation/ENV-03-API-NOTES.md`, `ENV-03-WIN-RESULTS.md`: API 후보와 실제 한섭 관찰 근거.

## 현재 동작과 한계

- `/duckbillfisher` 창에서 관찰을 시작/중지하고 보고서를 복사할 수 있다. 기본 관찰 꺼짐, 메모리 최근 200행, 수동 메모, Stopwatch 경과 초를 사용한다. 런타임 외부 통신·자동 게임 입력·네이티브 접근은 없다.
- 한섭 실제 관찰: 찌 던지기 후 낚시 자체 중단 3회, 자세 유지한 정상 어획 3회. Fishing이 각 주기 켜졌다 꺼져 경과 타이머의 후보가 됐다. Gathering은 한 주기에 여러 번 켜졌고 Casting은 계속 False였다.
- 창을 닫아도 기록 유지, 중지 후 행 수 고정, 새 관찰 시작 시 초기화, 언로드/재로드 직후 관찰 꺼짐·기록 0을 사용자에게 확인했다. 재로드 후 낚시 이벤트 중복·도중 시작·지역 이동/로그아웃·생미끼는 미확인이다.
- 로그가 자동 감지한 것은 세 플래그뿐이다. 미끼·실제 어종·어획 성공/실패·정확한 입질/낚아채기 시각은 기록하지 않는다. 정상 어획 여부는 사용자가 별도로 확인한 사실이다. Fishing=False를 성공으로, True 유지 시간을 입질 시간으로 해석하지 않는다.
- Mac에서 Core 테스트 15개와 고정 upstream 참조 Debug 빌드(경고/오류 0)를 확인했다. 초기 Windows 빌드 로그는 Core 테스트 9개 통과를 확인했다. 이후 새 관찰 기능의 실제 한섭 실행은 확인했지만 Windows의 15개 테스트 빌드 출력은 별도로 수집하지 않았다. 검증 근거를 합쳐서 다른 실행 결과로 표현하지 않는다.
- 타이머·수첩 읽기·실제 어종 데이터는 미구현이다. ENV-03 전체는 완료하지 않았다.

## 확인한 Windows 환경

| 항목 | 확인 값 |
|---|---|
| OS/아키텍처 | Microsoft Windows 10.0.26300 / X64 |
| PowerShell | 7.6.7 |
| .NET SDK | 10.0.402 (`global.json`, rollForward disable) |
| KR Dalamud | 15.0.3.6 / API 15 |
| KR 참조 폴더 | `%APPDATA%\XIVLauncherKR\addon\Hooks\dev` |
| 별도 글로벌 설치 | `%APPDATA%\XIVLauncher\addon\Hooks\dev`의 15.0.3.0 — KR 빌드 참조로 선택하지 않는다 |
| Git | 이전 환경 확인 시 미설치/PATH 없음. 전환 시 현재 설치 여부 확인 |
| 게임 클라이언트 빌드 | 아직 미기록. 다음 API 조사 시 실제 값 확인 |

이 버전들은 이전 확인 값이다. 환경 출력과 현재 설치가 다르면 변경 내용을 기록하고 호환성을 검토한다. 자동으로 최신 SDK/API로 올리지 않는다. Mac의 `.dotnet`, `.cache`, `/private/tmp` 도구 경로를 Windows에 적용하지 않는다.

## Windows로 이번 소스와 이력 가져오기

권장 작업 폴더 예시는 `C:\dev\DuckbillFisher`다. 실제 작업 루트는 Windows에서 정한다. 기존 Downloads의 ZIP 해제 폴더는 이전 테스트 소스이며, 이 세션의 문서가 들어 있지 않을 수 있다. 이전 폴더의 수정을 먼저 확인하고 보존한다.

이번 전환용 파일은 커밋 후 로컬 `artifacts`에 생성한다. 이 파일들은 Git 추적 대상이 아니며 원격 저장소에 자동 게시되지 않는다.

- `DuckbillFisher-Windows-handoff.bundle`: main 커밋 이력을 담는 Git bundle. Git 설치 후 오프라인 clone 가능.
- `DuckbillFisher-Windows-source.zip`: 같은 커밋의 소스/문서, `DuckbillFisher/` 하위에 포함. `.git`, SDK, 캐시, 빌드 결과는 포함하지 않는다.
- 각 파일의 `.sha256`: Windows의 `Get-FileHash <파일> -Algorithm SHA256`으로 대조.

### Git 이력을 가져오는 방법

Git이 없다면 [Git for Windows](https://git-scm.com/install/windows)를 설치하고 새 터미널에서 `git --version`을 확인한다. 빈 목적 폴더에 bundle을 clone한다. 파일 경로는 받은 실제 위치로 바꾼다.

```powershell
git clone "C:\받은폴더\DuckbillFisher-Windows-handoff.bundle" "C:\dev\DuckbillFisher"
cd C:\dev\DuckbillFisher
git status --short --branch
git log -1 --oneline
```

마지막 커밋은 `feat: add fishing condition diagnostics and Windows handoff` 제목이며, 전달 시 안내한 커밋 SHA와 대조한다. 작업 트리는 깨끗해야 한다. 이미 존재하는 작업 폴더에 강제로 clone하거나 기존 수정을 덮어쓰지 않는다.

bundle clone의 origin은 로컬 bundle 파일을 가리킨다. GitHub 동기화를 사용할 때 아래처럼 원격 주소를 바꾸고 접근을 별도로 확인한다. **이번 작업은 로컬 커밋이며 원격 푸시/접근은 검증하지 않았다.**

```powershell
git remote set-url origin https://github.com/PlatyDuckbill/DuckbillFisher.git
git remote -v
```

개인 PC의 Git 작성자 설정이 없으면 사용하려는 이름/이메일을 설정한다. Mac의 SSH alias나 자격 증명을 그대로 복사하지 않는다. 원격 게시 여부와 무관하게 bundle에서 받은 이력 위에 로컬 개발을 진행할 수 있다.

### 소스 ZIP으로 먼저 시작하는 방법

ZIP을 별도 폴더에 풀고 안쪽 `DuckbillFisher`를 작업 루트로 연다. 빌드와 편집은 가능하지만 Git 이력은 포함하지 않는다. Git을 준비한 뒤 bundle 작업 폴더로 옮길 경우 수정 파일을 먼저 비교하고 보존한다. 원격 저장소에 이 커밋이 있다고 가정하고 오래된 소스를 clone하지 않는다.

## 첫 빌드와 개발 DLL 등록

PowerShell 7에서 실제 작업 루트로 이동한다. 이미 확인한 SDK/PowerShell을 반복 설치하지 않는다.

```powershell
$PSVersionTable.PSVersion
dotnet --version
.\scripts\inspect-environment.ps1
```

다운로드한 스크립트가 서명 오류로 차단될 때는 내용을 확인한 후 프로젝트 스크립트만 차단 해제한다.

```powershell
Get-ChildItem .\scripts\*.ps1 | Unblock-File
```

게임의 Dalamud에서 기존 DuckbillFisher를 직접 언로드한 뒤 실행한다.

```powershell
.\scripts\build.ps1 -DalamudPath "$env:APPDATA\XIVLauncherKR\addon\Hooks\dev"
```

현재 기준 기대 결과는 Core 테스트 15개 통과, Plugin 빌드와 manifest 검사 성공이다. 실패하면 오류 전문을 확인하고 기존 DLL을 새 빌드로 오인하지 않는다.

빌드 성공 출력의 **Dev plugin DLL** 경로를 사용한다. 권장 예시 루트라면:

```text
C:\dev\DuckbillFisher\src\DuckbillFisher.Plugin\bin\Debug\DuckbillFisher.dll
```

Dev Plugin Locations의 이전 Downloads DLL 경로를 새 경로로 교체한다. 같은 InternalName의 두 DLL을 중복 등록하지 않는다. 이후 같은 작업 폴더를 유지하면 경로 교체나 ZIP 전달을 반복할 필요가 없다. 빌드 결과를 로드한 뒤 `/duckbillfisher`로 확인한다. 게임 입력은 사람이 직접 수행한다.

## 다음 한 작업

**ENV-03 수첩 읽기의 API/버전 근거 조사**부터 시작한다. 바로 unsafe를 켜거나 글로벌 오프셋을 적용하지 않는다.

1. 실제 KR 게임 빌드와 현재 설치 DLL/XML/가능한 대응 소스의 버전 근거를 기록한다. 공개 서비스에 수첩 상태 API가 있는지 먼저 확인한다.
2. 후보 `PlayerState.IsFishCaught(uint)`의 대상 버전 호환성, FishParameter ID와 Item ID 구분, IsInLog 조건, 로그인 후 준비 여부를 조사한다. 현재 upstream main 소스만으로 KR 구조를 확정하지 않는다.
3. 근거가 확보되면 작은 읽기 전용 어댑터로 알려진 수집/미수집 어종을 대조한다. 준비되지 않거나 읽기 실패한 값은 Unknown으로 유지한다. 근거가 없으면 수동 필터 대안을 유지한다.

정밀 타이머 개발 전에는 시전/입질/낚아채기/종료와 미끼·어종·성공/실패의 관찰 경로도 따로 조사해야 한다. 각 필드의 값 출처와 미확인 상태를 구분하고, 기존 플래그 로그에서 추측해 채우지 않는다. 관련 없는 범위까지 한 번에 구현하지 않는다.

새 세션 입력 예시:

> AGENTS.md, docs/SESSION-HANDOFF.md, docs/STATUS.md를 읽고 이어서 진행해 줘. 이제 이 Windows PC에서 직접 편집·빌드·게임 검증한다. 먼저 작업 루트와 현재 KR 환경을 확인하고 ENV-03 수첩 읽기의 API/버전 근거부터 조사해 줘. 기존 한섭 관찰 결과와 미확인 항목을 구분하고 검증되지 않은 네이티브 구조는 적용하지 마.

세션 종료 전 STATUS의 실제 검증 결과와 다음 한 작업을 갱신한다. 새 API/의존성 결정과 환경 변경은 이 문서에도 반영한다. 커밋 SHA는 `git log -1`에서 확인하고, 푸시·배포·검증 여부를 따로 보고한다.
