# Windows 준비와 첫 실행

목표: 대상 한섭 Dalamud 환경에서 `/duckbillfisher`로 시작 창을 열고 재로드까지 확인한다.

기존 Windows 게임 PC의 PowerShell/SDK 설치와 관찰 기능 기본 검증은 완료했다. 앞으로 이 PC에서 직접 개발한다. **이번 세션에서 이어갈 때는 [세션 인수인계](SESSION-HANDOFF.md)**의 소스/이력 이동과 실제 KR 경로부터 확인한다. 아래 도구 설치는 새 PC 또는 누락 도구를 위한 절차이며, 이미 설치한 도구를 다시 설치할 필요는 없다.

## 1. 개발 도구 설치

Windows x64 PC 기준이다. Windows 설정 → 시스템 → 정보에서 시스템 종류를 확인하고 ARM 기반 PC는 설치 아키텍처를 별도로 맞춘다.

1. [.NET SDK 10.0.402 x64 설치 파일](https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.402/dotnet-sdk-10.0.402-win-x64.exe)을 실행한다. Runtime이 아닌 SDK다. 버전은 global.json과 동일해야 한다.
2. [Git for Windows](https://git-scm.com/install/windows)를 설치한다.
3. [PowerShell 7](https://learn.microsoft.com/en-us/powershell/scripting/install/install-powershell-on-windows)을 설치한다. Windows에 기본 포함된 PowerShell 5.1과 별개다. winget이 있다면 아래 명령으로 설치할 수 있다.

```powershell
winget install --id Microsoft.PowerShell --source winget
```

새 터미널을 열고 확인한다.

```powershell
git --version
dotnet --list-sdks
pwsh --version
```

`10.0.402`와 PowerShell `7.x`가 보이면 된다. IDE는 나중에 추가해도 빌드할 수 있다.

## 2. Windows에 소스 받기

소스 ZIP을 사용하는 경우 `DuckbillFisher-source.zip`을 Windows의 `C:\dev\DuckbillFisher` 같은 폴더에 푼다. 압축 안에 SDK·캐시·게임 DLL은 포함하지 않는다.

GitHub에 소스가 올라간 이후에는 Git을 사용한다.

```powershell
git clone https://github.com/PlatyDuckbill/DuckbillFisher.git
cd DuckbillFisher
```

비공개 저장소라면 GitHub 인증이 필요하다. Mac의 SSH alias를 Windows에 복사할 필요는 없다. 이후 작업에서는 두 PC가 같은 브랜치/커밋을 사용하는지 확인한다.

## 3. 실제 Dalamud 경로 확인

저장소 루트(README.md와 global.json이 있는 폴더)에서 실행한다.

```powershell
pwsh -NoProfile -File scripts/inspect-environment.ps1
```

이 스크립트는 도구 버전과 일반적인 Dalamud 위치를 읽기만 한다. `Dalamud: 15.0.3.6 at ...`처럼 출력되면 `at` 뒤 경로를 다음 명령에 사용한다. 여러 경로가 나오면 현재 사용하는 설치의 경로를 선택한다. 후보에서 못 찾으면 나온 결과를 공유하여 경로를 추가 확인한다. API를 추측하거나 게임 파일을 수정하지 않는다.

인터넷에서 받은 스크립트라 실행이 차단되면 해당 프로젝트의 스크립트 내용을 확인한 후 다음과 같이 파일 차단을 해제할 수 있다. 시스템 전체 실행 정책 변경은 필요하지 않다.

```powershell
Get-ChildItem scripts/*.ps1 | Unblock-File
```

## 4. 테스트와 빌드

아래의 경로 문자열을 앞에서 확인한 값으로 바꾼다.

```powershell
pwsh -NoProfile -File scripts/build.ps1 -DalamudPath "실제 Dalamud.dll이 있는 폴더"
```

첫 실행은 NuGet 의존성을 인터넷에서 받는다. 이후 잠금 파일 기준으로 같은 의존성을 사용한다. 스크립트는 테스트가 실패하면 Plugin 빌드/복사를 진행하지 않는다.

성공 시 개발용 DLL:

```text
src/DuckbillFisher.Plugin/bin/Debug/DuckbillFisher.dll
```

Dalamud 참조가 없는 환경에서 Core만 확인하려면:

```powershell
pwsh -File scripts/build.ps1 -CoreOnly
```

## 5. 게임에서 로드

1. 현재 Dalamud 설정의 개발 옵션에서 **Dev Plugin Locations**(개발 플러그인 경로)를 찾는다. 번역·배포판에 따라 메뉴 이름은 다를 수 있다.
2. 위에서 생성한 `DuckbillFisher.dll`의 **절대 경로**를 추가한다.
3. 개발 플러그인 목록을 새로 검색하고 DuckbillFisher를 로드한다. 나타나지 않으면 설정과 `/xllog`의 오류를 확인한다.
4. 채팅 입력에 `/duckbillfisher`를 직접 입력한다. 창이 하나 열려야 한다.
5. 닫기 버튼, 명령으로 다시 열기, 설치 관리자의 UI 열기를 확인한다.
6. 플러그인을 언로드하고 다시 로드한다. 명령이 중복 등록되지 않고 창이 하나만 떠야 한다.
7. 결과를 검증 기록에 남긴다. 로드 오류가 나면 오류 전문과 실제 Dalamud 경로/버전을 기록한다.

[upstream 개발 플러그인 안내](https://dalamud.dev/faq/getting-started/). 실제 메뉴와 로드는 대상 한섭 환경에서 확인한다.

## 선택: 전용 개발 폴더에 자동 복사

빌드 출력 DLL을 직접 등록했다면 복사는 필요 없다. 고정 경로가 필요하면 플러그인을 먼저 언로드한 후 다음처럼 실행한다.

```powershell
pwsh -File scripts/build.ps1 `
  -DalamudPath "실제 Dalamud.dll이 있는 폴더" `
  -DeployDirectory "C:\devPlugins\DuckbillFisher"
```

목적 폴더 이름은 반드시 DuckbillFisher여야 한다. 테스트/빌드 성공 후 자체 DLL 2개와 manifest만 복사한다. 경로 잠금 등 복사 오류가 나면 언로드 상태를 확인하고 다시 실행한다. 이 경우 개발 설정에 등록할 DLL은 `C:\devPlugins\DuckbillFisher\DuckbillFisher.dll`이다.

## 완료 기준

실제 한섭 환경에서 빌드·로드·한글 창 표시·언로드·재로드가 통과해야 한다. 기존 시작 창의 [ENV-02 Windows 검증](validation/ENV-02-WIN.md)은 완료했다. 새 낚시 상태 관찰 기능은 [ENV-03 Windows 절차](validation/ENV-03-WIN.md)를 따른다.
