# 커스텀 저장소 설치와 배포

현재 공개 버전과 실제 확인 범위는 [STATUS](../STATUS.md), 버전별 ZIP·해시·게시 결과는 [검증 색인](../verification/README.md#배포와-호환성)을 따른다. 이 문서는 반복 가능한 절차만 관리한다.

## 사용자 설치

```text
https://raw.githubusercontent.com/PlatyDuckbill/DuckbillFisher/main/repo/pluginmaster.json
```

1. Dalamud 설정의 커스텀 플러그인 저장소에 위 URL을 입력하고 같은 줄 오른쪽 **＋**를 누른다. 추가된 행을 활성화하고 설정을 저장한다. 버튼이 가려지면 폭을 넓힌다.
2. 플러그인 설치 목록을 새로고침하고 DuckbillFisher를 검색해 설치한다.
3. /duckbillfisher로 연다. 도감 연동 안내가 있으면 어류도감 열기를 누르고 낚시 탭 조회를 기다린다.
4. 이후 같은 설치 화면에서 업데이트한다. 저장소 URL을 다시 등록하거나 ZIP을 직접 풀 필요는 없다.

개발자는 설치 확인 시 개발 DLL 등록을 비활성화해 배포 플러그인과 중복 로드하지 않는다. 설정/기록 파일 삭제는 필요 없다. 호환성 차단이면 도감 수동 열기만으로 해결되지 않으므로 짧은 환경 진단부터 확인한다.

## 배포 구성

| 구성 | 역할 |
|---|---|
| repo/pluginmaster.json | 이름·API·버전·아이콘·설치/업데이트 URL |
| GitHub Release의 버전 ZIP | Plugin/Core/위성 리소스/manifest. Source code ZIP과 다름 |
| 태그의 assets/duckbillfisher-icon.png | 사용자가 제공한 원본 대표 이미지 |
| ZIP 옆 .sha256 | 확인한 패키지와 게시 파일 동일성 확인 |
| docs/releases/버전.md | 영어 릴리스 요약 |

설치/업데이트 HTTP는 Dalamud가 담당한다. 플러그인 런타임 HTTP는 없다. 이 방식은 공식 저장소 등록과 별개다.

## 개발자 배포 절차

### 1. 로컬 후보 준비

- 게시 요청/기존 권한, Git 상태/원격 공개 여부/인증을 확인한다. 소스 수정과 원격 게시를 구분한다.
- Plugin csproj Version을 기준으로 DLL·위성 리소스·manifest·ZIP·태그·설치 JSON을 맞춘다. 이미 게시한 버전은 덮어쓰지 않는다.
- Release는 HANDOFF의 **검증한 최소 지원 참조**로 빌드한다. 정확한 참조가 없으면 대체하지 말고 확보/검토한다.
- 잠금 파일/기존 원본 이미지/라이선스를 유지한다. 설명은 영어, 실제 줄바꿈을 사용하고 지원 범위와 제한을 명시한다.

PowerShell 7, 저장소 루트에서 실행한다. 이 명령은 로컬 준비만 한다. 각 단계 실패 시 멈춘다.

```powershell
$duckbillRoot = (Get-Location).Path
$duckbillProject = 'src/DuckbillFisher.Plugin/DuckbillFisher.Plugin.csproj'
[xml]$duckbillProjectXml = Get-Content -LiteralPath $duckbillProject -Raw -Encoding UTF8
$duckbillVersion = [string](@($duckbillProjectXml.Project.PropertyGroup.Version | Where-Object { $_ })[0])
if ($duckbillVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'Invalid project version' }
$duckbillRefs = Join-Path $duckbillRoot '.cache/compat-15.0.3.6/full'
if (!(Test-Path -LiteralPath (Join-Path $duckbillRefs 'Dalamud.dll'))) { throw 'Minimum supported references missing' }
$duckbillOutput = Join-Path $duckbillRoot ".cache/release-build/$duckbillVersion"
dotnet build $duckbillProject --no-restore -c Release "-p:DalamudLibPath=$duckbillRefs/" "-p:OutputPath=$duckbillOutput/"
if ($LASTEXITCODE -ne 0) { throw 'Release build failed' }
pwsh -NoLogo -NoProfile -File scripts/check-localization.ps1
if ($LASTEXITCODE -ne 0) { throw 'Resource check failed' }
pwsh -NoLogo -NoProfile -File scripts/prepare-release.ps1 `
  -PackagePath "$duckbillOutput/DuckbillFisher/latest.zip" `
  -AssetBaseUrl "https://github.com/PlatyDuckbill/DuckbillFisher/releases/download/v$duckbillVersion" `
  -OutputDirectory "artifacts/share/$duckbillVersion"
if ($LASTEXITCODE -ne 0) { throw 'Package preparation failed' }
```

prepare-release.ps1은 필수 파일·허용 목록·API·DLL/manifest 버전을 검사하고 ZIP·SHA256·후보 JSON을 만든다. 기존 산출물을 덮어쓰지 않으므로 새 후보는 별도 출력 경로를 사용한다.

필수 파일은 scripts/package-files.ps1이 관리하며 한국어 ko/DuckbillFisher.resources.dll을 포함한다. SDK/게임 DLL·캐시·자격 증명을 ZIP에 넣지 않는다. 내장 데이터/고지가 소스와 같은지 확인한다. 아이콘은 원본을 유지하며 태그의 공개 URL에 연결한다.

### 2. 확인할 ZIP 고정

1. 후보 ZIP을 .cache/release-validation/버전 아래에 추출하고 빌드 출력과 파일 해시·DLL/manifest 버전·API·한국어 리소스를 대조한다.
2. 추출 DLL의 절대 경로를 안내한다. 사용자 상시 언로드 지시를 적용하되 같은 InternalName의 중복 로드는 피한다.
3. 변경 기능과 목록/도감/간이 목록/타이머·기록/한영/재로드 중 관련 동작을 구체적으로 요청한다. 확인한 동작과 미확인을 해당 Release 기록에 남긴다.
4. 실제 결과와 ZIP SHA256을 묶어 기록한다. 확인 뒤 실행 파일을 다시 빌드했다면 새 후보로 취급한다. 미확인 ZIP은 설치 가능한 릴리스로 게시하지 않는다.

### 3. Release 먼저, 설치 목록은 이후

1. 승인 범위의 코드/문서만 커밋·정상 push한다. 테스트 미요청 작업의 커밋은 [skip ci]를 사용한다.
2. 확인한 소스에 새 버전 태그를 만들고 push한다. 태그/Release/자산이 이미 있으면 현재 상태를 조사하고 덮어쓰지 않는다.
3. GitHub Release 초안에 영어 요약과 **확인한 ZIP·체크섬**을 업로드한다. 업로드 크기/해시·태그를 확인한 후 공개한다.
4. 인증 없는 요청으로 ZIP·체크섬·태그 아이콘을 내려받아 HTTP 성공과 원본 해시를 확인한다.
5. 그 다음에만 후보 JSON을 repo/pluginmaster.json에 반영해 커밋·push한다.
6. 사용자가 등록한 **실제 공유 raw URL**에서 이름·버전·API·설치/업데이트/아이콘 URL을 다시 확인한다. 캐시 우회 URL만 성공한 것을 완료로 보고하지 않는다.
7. HTTP/해시 확인과 실제 Dalamud 설치·업데이트 결과를 구분해 STATUS에 기록한다.

GitHub 웹 또는 현재 사용 가능한 인증 도구로 게시한다. 토큰은 메모리에서만 사용하고 출력/파일/Git에 남기지 않는다. 실패 시 이미 공개된 단계와 남은 단계를 기록하고, 검증되지 않은 URL을 가리키도록 설치 목록을 바꾸지 않는다.

## 업데이트와 복구

### 변경 내역

- 설치 화면은 커스텀 저장소 항목의 `Changelog` 문자열을 사용한다. GitHub Release 본문을 자동 조회하지 않는다. csproj의 `<Changelog>`에 영어 요약을 넣으면 SDK manifest와 prepare-release.ps1의 후보 설치 JSON에 보존된다. 매 버전 요약·DLL/manifest·후보 JSON의 일치를 확인한다.
- 이미 공개한 버전의 안내만 보완할 때는 설치 JSON의 `Changelog`만 추가할 수 있다. 버전/다운로드 링크/LastUpdate와 고정 ZIP·태그를 바꾸지 않으며 실제 공유 raw URL에서 새 문구를 확인한다. ZIP 안 manifest와 설치 JSON의 이 차이는 배포 기록에 남긴다.
- 플러그인 자체 업데이트 안내는 내장 한영 리소스와 ChangelogWindow.PublishedReleases를 사용한다. 최근 공개 릴리스5개를 최신순으로 표시하며 개발 버전은 제외한다. 새 Release 후보를 준비할 때 docs/releases의 공개 요약에 맞춰 해당 버전을 맨 앞에 추가하고 한영 TextKey/리소스를 함께 갱신한 뒤 그 후보 ZIP을 확인받아 게시한다. 실패한 후보/중간 개발 버전을 배포 이력으로 남기지 않는다. 원격 Release 조회는 하지 않는다. 확인 버전은 공통 설정에 저장하고 설정에서 다시 열 수 있다.
- 규격 확인: 2026-10-19 [manifest Changelog](https://dalamud.dev/plugin-development/plugin-metadata/#changelogs), [커스텀 저장소 항목](https://dalamud.dev/plugin-publishing/custom-repositories/#store-entry-keys).

### 업데이트 확인과 복구

raw 캐시가 잠시 이전 JSON을 제공할 수 있다. 실제 공유 URL이 새 버전을 반환하는지 확인하고, 사용자는 설치 목록을 새로고침한다. 같은 저장소를 삭제/재등록할 필요는 없다.

수정 또는 이전 코드 복원도 **더 높은 버전**으로 새 ZIP·태그를 만든다. JSON 버전만 낮춰 자동 다운그레이드된다고 가정하지 않는다. 공식 제출은 대상/최신 정책을 별도 확인하고 사람 작성이 필요한 제출문은 사람이 작성한다.

규격의 기존 조사 출처: [Dalamud 커스텀 저장소](https://dalamud.dev/plugin-publishing/custom-repositories/), [이미지/제출 안내](https://dalamud.dev/plugin-publishing/submission/), [GitHub CI 생략](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/skip-workflow-runs). 이 문서 정리는 해당 외부 정책의 최신 재검증을 의미하지 않는다.
