# ENV-03 로컬 검증

## 실행 환경

- 2026-10-15, macOS arm64.
- 저장소 내부 .NET SDK 10.0.402 / PowerShell 7.6.7.
- build/dalamud-reference.json에 고정한 upstream Dalamud 15.0.3.6 참조.

## 실행 명령

```sh
PATH="$PWD/.dotnet:$PATH" \
DOTNET_CLI_HOME="$PWD/.cache/dotnet" \
NUGET_PACKAGES="$PWD/.cache/nuget" \
DOTNET_CLI_TELEMETRY_OPTOUT=1 \
/private/tmp/duckbill-pwsh/pwsh -NoProfile -File scripts/build.ps1 \
  -DalamudPath "$PWD/.cache/dalamud/15.0.3.6"
```

## 결과

- 첫 샌드박스 실행에서는 VSTest 로컬 소켓이 Permission denied로 차단되어 테스트 실행이 중단됐다. 테스트 실패와 구분한다.
- 같은 명령을 샌드박스 밖에서 다시 실행해 종료 코드 0을 확인했다.
- locked restore 성공. Core 테스트 합계 15 / 성공 15 / 실패 0 / 건너뜀 0.
- 추가한 6개 테스트는 관찰 중에만 기록, 시작/중지 중복, 200행 제한과 제외 수, 재시작 초기화, 발행한 snapshot의 독립성, 같은 시각/중복 callback 보존을 확인했다.
- Plugin Debug 빌드 성공. 경고 0 / 오류 0. manifest 검사 성공.
- 이후 UI의 긴 행에 가로 스크롤을 추가하고 같은 환경 변수/참조 경로로 `dotnet build src/DuckbillFisher.Plugin/DuckbillFisher.Plugin.csproj --no-restore -c Debug -p:DalamudLibPath=<고정 참조 폴더>/`를 실행했다. 경고 0 / 오류 0. Core 로직은 변경하지 않았다.

위 Mac 검증 시점에는 Windows의 새 빌드 출력과 인게임 결과가 없었다. 이후 수신한 한섭 관찰 및 기본 동작 검증은 [Windows 결과](ENV-03-WIN-RESULTS.md)에 별도로 기록했다. 수첩 읽기·정밀 시전 시작·입질 감지 호환성은 위 자동 검증만으로 판단하지 않는다.
