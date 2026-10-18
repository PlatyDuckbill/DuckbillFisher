# 새 Windows 개발 PC 준비

일반 사용자의 설치는 [DISTRIBUTION](DISTRIBUTION.md#사용자-설치)만 필요하다. 이 문서는 소스를 편집·빌드하는 새 PC용이며, 기존 PC 재개는 [HANDOFF](SESSION-HANDOFF.md)를 따른다.

## 도구와 소스

1. Windows x64에서 global.json에 고정한 .NET SDK, Git for Windows, PowerShell 7을 준비한다. Runtime만 설치하면 빌드할 수 없다.
2. PowerShell에서 git --version, dotnet --list-sdks, pwsh --version으로 설치를 확인한다.
3. 공개 저장소를 clone한다. SDK·게임 DLL·다른 PC의 캐시/자격 증명은 저장소에 복사하지 않는다.

```powershell
git clone https://github.com/PlatyDuckbill/DuckbillFisher.git
cd DuckbillFisher
pwsh -NoProfile -File scripts/inspect-environment.ps1
```

설치 안내: [.NET](https://dotnet.microsoft.com/en-us/download/dotnet), [Git](https://git-scm.com/install/windows), [PowerShell](https://learn.microsoft.com/en-us/powershell/scripting/install/install-powershell-on-windows). 설치할 버전은 저장소 고정값을 따른다.

## KR 참조와 복원

환경 스크립트의 후보 중 현재 실행하는 KR Dalamud 경로를 확인한다. 숫자 버전이 같아도 배포판/DLL이 다를 수 있다. 다른 PC의 지원 결과를 그대로 적용하지 않는다.

```powershell
# 실제 확인한 Dalamud.dll 폴더로 지정한다.
$duckbillRefs = 'C:/실제/KR/Dalamud/경로'
dotnet restore src/DuckbillFisher.Plugin/DuckbillFisher.Plugin.csproj --locked-mode "-p:DalamudLibPath=$duckbillRefs/"
```

복원이 끝나면 [WORKFLOW의 직접 빌드](WORKFLOW.md#windows-빌드)를 따른다. 일반 준비에서 테스트를 자동 실행하지 않는다. PowerShell 파일 차단이 있으면 해당 파일을 검토한 후 그 파일만 차단 해제하며 시스템 전체 실행 정책을 바꾸지 않는다.

## 개발 플러그인 로드

1. 출력 DuckbillFisher.dll의 절대 경로를 Dalamud의 Dev Plugin Locations에 등록한다. 이전 개발/배포 플러그인과 중복 로드하지 않는다.
2. 개발 플러그인 목록을 새로 검색해 로드하고 /duckbillfisher를 직접 입력한다.
3. 닫기/재열기·설정 UI·언로드/재로드와 이번 변경 기능을 확인한다. 결과는 빌드 성공과 별도로 기록한다.
4. 실패하면 짧은 로그와 실제 게임/배포판·버전·DLL 경로를 확인한다. 호환성 가드를 우회하지 않는다.

원격 소스 갱신은 로컬 수정 보존 후 git pull --ff-only를 사용한다. 자동 복사 도구나 소스 ZIP 전달을 기본 작업으로 만들지 않는다. 이 PC에서 확인한 경로/지원 근거는 HANDOFF에 갱신한다.
