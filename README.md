# DuckbillFisher

한국 서버 FFXIV용 터주 정보·알림 Dalamud 플러그인. 프로젝트명·표시명·InternalName은 **DuckbillFisher**다.

현재는 `/duckbillfisher`로 여닫는 시작 창과 개발·테스트 기반을 구현했다. 실제 어종 데이터, 캐스팅 타이머, 게임 수첩 연동은 아직 없다. **한섭 인게임 검증 대기** 상태다.

최소 배포 목표는 친구가 커스텀 저장소 URL로 설치·업데이트하는 것이고, 장기 목표는 공식 등록이다. 게임 입력 대행과 플러그인 런타임 외부 통신은 구현하지 않는다.

## 지금 시작하기

- Windows 사용자는 [Windows 준비 및 첫 실행](docs/WINDOWS-SETUP.md)을 따른다.
- 현재 상태와 다음 한 작업은 [STATUS](docs/STATUS.md)에 있다.
- 친구 공유 방식은 [배포 안내](docs/DISTRIBUTION.md)에 있다. 아직 활성화된 배포 URL은 없다.

## 개발 도구

| 도구/대상 | 고정 버전 |
|---|---|
| .NET SDK | 10.0.402 (`global.json`) |
| Dalamud.NET.Sdk | 15.0.0 |
| Core / Plugin | net10.0 / net10.0-windows, Plugin x64 |
| 컴파일 확인용 Dalamud | upstream 15.0.3.6, revision/SHA256 고정 |
| 빌드 스크립트 | PowerShell 7 이상 |

게임과 독립된 테스트:

```sh
dotnet test tests/DuckbillFisher.Core.Tests/DuckbillFisher.Core.Tests.csproj -c Release
```

이 Mac에는 저장소 내부 `.dotnet`에 SDK를 설치했다. 현재 셸에서 사용하려면 저장소 루트에서 `export PATH="$PWD/.dotnet:$PATH"`를 실행한다. 시스템 전역 설치/PATH는 변경하지 않았다.

Plugin 빌드(PowerShell 7):

```powershell
pwsh -File scripts/build.ps1 -DalamudPath "Dalamud.dll이 있는 실제 폴더" -Configuration Release
```

출력 DLL은 `src/DuckbillFisher.Plugin/bin/Release/DuckbillFisher.dll`, ZIP은 같은 폴더의 `DuckbillFisher/latest.zip`이다. upstream 컴파일 성공은 한섭 실행 성공과 구분한다.

## 문서

- [AGENTS.md](AGENTS.md): AI 개발 규칙.
- [워크플로우](docs/WORKFLOW.md): Mac–Windows 반복, 모델/스킬 운용.
- [제품 범위](docs/PRODUCT.md), [구조](docs/ARCHITECTURE.md), [로드맵](docs/ROADMAP.md).
- [검증 기록 템플릿](docs/templates/VALIDATION.md), [실행한 검증](docs/validation/ENV-02-local.md).
- [참고 자료](docs/REFERENCES.md): 버전 근거와 제출 정책.
