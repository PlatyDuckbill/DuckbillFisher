# 커스텀 저장소로 설치·업데이트하기

## 이번 배포 상태

- 현재 Release는 **[0.3.0.0/API15](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.3.0.0)**다. 사용자 ZIP 추출본 대표 확인 후 같은 ZIP을 게시했다. 익명 ZIP/체크섬/아이콘 HTTP200·해시 일치, 기존 공유 저장소 JSON의0.3.0.0/API15/1항목과 설치·업데이트·아이콘 HTTP200까지 확인했다. 저장소 URL은 그대로다. 실제 Dalamud 업데이트와 친구 새 기능 확인은 별도다. 아래0.2.4/0.2.5 설명은 배포 이력이다. [현재 배포/확인 범위](validation/RELEASE-0.3.0.0.md), [0.2.6.0 친구 호환성 확인](validation/NATIVE-RUNTIME-DAL4KR.md).
- 첫 배포 버전은 **0.2.4.0 / Dalamud API 15**다. 사용자가 Release 실행·재로드의 대표 확인을 완료했다. 미확인 기능 전체의 보증으로 확대하지 않는다.
- 공개 GitHub 저장소와 게시 권한 확인, 영어 소개·512×512 원본 아이콘 연결, Release 빌드·패키지 검사 완료. 설명만 LF/글머리표로 개선했으며 검증한 실행 파일은 동일하다.
- [GitHub Release 0.2.4.0](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.2.4.0)와 태그를 게시했다. ZIP·체크섬·아이콘 익명 다운로드/해시 확인 통과 후 저장소 JSON을 게시했다. 아래 실제 URL의 HTTP200/1항목/0.2.4.0/API15/설명15줄/ZIP manifest 일치, 설치·업데이트·아이콘200 확인 완료. 실제 Dalamud 저장소 설치와 다음 버전 업데이트는 별도 확인 대기다.
- 자세한 산출물·확인 범위는 [첫 배포 기록](validation/RELEASE-0.2.4.0.md)을 따른다.
- 현재 업데이트는 **[0.2.5.0](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.2.5.0)**이다. 친구15.0.3.7의 공통 DLL 가드 차단을 정확한 런타임 조합 추가로 수정했다. 사용자 대표 확인 후 ZIP/체크섬/아이콘 익명 다운로드·해시 대조를 거쳐 기존 저장소 JSON을 갱신했다. **저장소 URL은 그대로**이며 설치 목록 새로고침→업데이트로 받는다. 친구 PC의 실제 결과는 별도 확인한다. [기록](validation/NATIVE-RUNTIME-15.0.3.7.md). 아래0.2.4.0 명령은 첫 배포 이력의 예시이며 후속 배포는 버전과 출력 경로를 바꾼다.

```text
https://raw.githubusercontent.com/PlatyDuckbill/DuckbillFisher/main/repo/pluginmaster.json
```

## 배포가 작동하는 구조

| 구성 | 역할 | 관리 위치 |
|---|---|---|
| 저장소 JSON | 이름·버전·아이콘·설치/업데이트 주소를 알려 준다 | `repo/pluginmaster.json` |
| 설치 ZIP | Plugin/Core/한국어 리소스/manifest 등 실행 파일을 담는다 | GitHub Release의 `DuckbillFisher-0.2.4.0.zip` |
| 아이콘 | 설치 목록의 대표 이미지다 | 버전 태그의 `assets/duckbillfisher-icon.png` |
| SHA256 | 게시 전후 ZIP이 같은 파일인지 개발자가 확인한다 | ZIP 옆 `.sha256` 파일 |

Dalamud가 JSON을 읽고 ZIP을 내려받아 설치하며 이후 더 높은 버전을 업데이트한다. 친구는 저장소 URL을 한 번 추가하면 된다. 소스 빌드·ZIP 수동 압축 해제·개발 DLL 등록이 필요하지 않다. 플러그인 실행 중 외부 HTTP는 추가하지 않는다.

공식 저장소 제출·승인은 이번 범위가 아니다. 커스텀 저장소는 인증 없는 HTTP GET으로 접근할 수 있어야 하므로 공유 URL은 공개 주소다. [공식 규격](https://dalamud.dev/plugin-publishing/custom-repositories/).

## 사용자 설치 절차

1. XIVLauncherKR로 게임을 실행하고 Dalamud 설정을 연다.
2. 커스텀 플러그인 저장소의 마지막 입력란에 위 JSON URL을 붙여넣고 **같은 줄 오른쪽 `＋` 버튼**을 누른다. URL 입력만으로는 등록되지 않는다. 추가된 행이 활성화되었는지 확인하고 설정의 **저장/저장 후 닫기**를 누른다. 오른쪽 버튼이 가려졌다면 설정 화면 폭을 넓힌다. 메뉴 이름은 사용하는 Dalamud 언어/버전에 따라 다를 수 있다.
3. 플러그인 설치 목록을 새로고침하고 **DuckbillFisher**를 검색해 설치한다.
4. `/duckbillfisher`로 화면을 연다. 수집 연동 안내가 나오면 **어류도감 열기**를 누르고 낚시 탭의 조회를 기다린다.
5. 이후 업데이트는 Dalamud 플러그인 설치 화면에서 제공한다. 저장소 URL을 다시 추가하지 않는다.

개발자는 배포 설치를 확인하기 전에 기존 개발 플러그인을 언로드하고 개발 DLL 등록을 비활성화한다. 같은 InternalName의 개발·배포 플러그인을 동시에 로드하지 않는다. 설정이나 기록 파일을 삭제할 필요는 없다.

## 개발자 준비: 게시 전까지 로컬 작업

현재 검증 환경은 KR 게임 `2026.10.06.0000.0000`, Dalamud `15.0.3.6/API15`다. 친구의 게임/Dalamud가 다르면 네이티브 기능의 호환성을 먼저 확인한다. UI 언어 선택이 글로벌 클라이언트 지원을 뜻하지 않는다.

버전은 Plugin csproj의 `Version` 한 곳에서 관리한다. 첫 배포는 미게시 버전 `0.2.4.0`을 유지한다. 태그·ZIP 이름·manifest·저장소 JSON이 같은 버전을 가리켜야 한다.

PowerShell 7에서 다음을 실행한다. 테스트를 실행하는 `scripts/build.ps1`은 이번 절차에서 사용하지 않는다.

```powershell
dotnet build src/DuckbillFisher.Plugin/DuckbillFisher.Plugin.csproj --no-restore -c Release -p:DalamudLibPath=C:/Users/hyogu/AppData/Roaming/XIVLauncherKR/addon/Hooks/dev/
pwsh -NoLogo -NoProfile -File scripts/check-localization.ps1

pwsh -NoLogo -NoProfile -File scripts/prepare-release.ps1 `
  -PackagePath src/DuckbillFisher.Plugin/bin/Release/DuckbillFisher/latest.zip `
  -AssetBaseUrl https://github.com/PlatyDuckbill/DuckbillFisher/releases/download/v0.2.4.0 `
  -OutputDirectory artifacts/share/0.2.4.0
```

| 생성물 | 용도 |
|---|---|
| `artifacts/share/0.2.4.0/DuckbillFisher-0.2.4.0.zip` | 실제 업로드할 ZIP |
| `DuckbillFisher-0.2.4.0.zip.sha256` | ZIP의 해시 |
| `pluginmaster.json` | 확인 후 repo에 반영할 후보 |

`prepare-release.ps1`은 필수 파일·허용 목록·API·버전을 확인한 뒤 로컬에 ZIP·체크섬·JSON을 만든다. 원격 게시나 실제 `repo/pluginmaster.json` 변경은 하지 않으며 기존 결과를 덮어쓰지 않는다. 재생성할 때는 새 출력 경로를 쓰고 확인한 ZIP의 해시가 바뀌면 실행 확인도 다시 구분한다.

이번 최종 산출물은 `artifacts/share/0.2.4.0-final/`이다. 실행 확인 뒤 설명 가독성만 수정했으며 직접 재컴파일하지 않고 `dotnet msbuild ... -t:DefaultDalamudPackagerRelease -p:Configuration=Release`로 manifest/ZIP만 재생성했다. 원래 ZIP과 비교해 Description 외 5파일 및 다른 manifest 필드가 동일함을 확인했다. 향후 기능·실행 파일이 달라지는 수정의 실행 확인을 생략하는 절차는 아니다.

한국어 위성 리소스 `ko/DuckbillFisher.resources.dll`이 반드시 포함되어야 한다. 요구 파일은 `scripts/package-files.ps1`을 따른다. 아이콘은 사용자가 제공한 원본을 바이트 변경 없이 이동했으며 `IconUrl`로 연결했다. 미리보기 스크린샷이 아니므로 `ImageUrls`에는 중복 등록하지 않았다. 아이콘 1:1·64~512픽셀 기준은 [공식 이미지 안내](https://dalamud.dev/plugin-publishing/submission/)를 참고했다.

## Release ZIP의 실행 확인

개발자가 로컬에서 할 확인이며 친구의 설치 절차가 아니다.

1. 배포 ZIP을 `.cache/release-validation/0.2.4.0/`에 추출하고 빌드 출력과 각 파일 SHA256이 같은지 확인한다.
2. 기존 개발 플러그인을 언로드한다. 개발 DLL 경로를 추출한 `DuckbillFisher.dll`로 바꿔 로드한다. 기존 Debug 파일은 덮어쓰지 않는다.
3. 목록·간이 목록·타이머·한영 전환·어류도감 연동을 확인하고 낚시 1회의 기록을 확인한다.
4. 언로드→재로드 후 설정과 낚시 기록이 유지되는지 확인한다. 확인한 ZIP의 SHA256과 결과를 기록한다.

모든 기능의 전수 확인과 이 최소 패키지 확인은 구분한다. 문제가 있으면 설치 가능한 릴리스·저장소 항목은 게시하지 않는다.

## 게시 순서

1. Release 실행 확인과 문서 갱신 후 승인된 파일만 커밋하고 기존 `origin/main`에 정상 push한다. 무관한 사용자 파일이나 자격 증명은 포함하지 않는다.
2. 확인한 소스 커밋에 `v0.2.4.0` 태그를 만들고 push한다. 이미 존재하는 태그·릴리스는 덮어쓰지 않는다.
3. GitHub의 **Releases → Draft a new release**에서 해당 태그를 고르고 [영어 릴리스 요약](releases/0.2.4.0.md)과 ZIP·`.sha256`을 첨부한다. 자동으로 딸려 오는 Source code ZIP은 설치 파일이 아니다.
4. ZIP 업로드를 끝낸 뒤 Release를 공개한다. 이번 배포는 커스텀 저장소 일반 설치 항목이며 Dalamud의 테스트 빌드 옵션을 요구하지 않는다. 초기 배포의 확인 범위와 제한은 릴리스 요약에 명시한다.
5. 로그인 없는 별도 요청으로 ZIP 다운로드·SHA256, 태그의 아이콘 URL 접근을 확인한다.
6. 그 다음에만 생성한 `pluginmaster.json`을 `repo/pluginmaster.json`에 반영하고 커밋·push한다.
7. 공개 JSON의 이름·API·버전·설치/업데이트 URL과 모든 링크를 익명 요청으로 다시 확인한다. 이 시점부터 친구에게 URL을 전달한다.
8. 실제 Dalamud 저장소 설치 확인은 HTTP 확인과 별도로 기록한다. 첫 업데이트 경로는 더 높은 버전의 다음 배포에서 확인한다.

현재 환경에는 `gh` CLI가 없지만 Git credential manager의 인증으로 GitHub API 게시가 가능하다. 토큰은 메모리에서만 사용하며 출력·파일·Git에 남기지 않는다. 직접 게시할 때는 위 GitHub 웹 절차로 같은 결과를 만들 수 있다.

이번 요청은 테스트 실행을 제외하므로 기존 테스트 CI를 유발하지 않도록 게시용 커밋에 `[skip ci]`를 넣는다. 워크플로우 자체는 변경하지 않는다. [GitHub의 push 생략 지침](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/skip-workflow-runs).

## 업데이트와 복구

게시 직후 GitHub raw 주소의 캐시가 이전 JSON을 잠시 제공할 수 있다. 0.2.5.0 게시 때 원본 커밋/Contents API/refs/heads/main은 새 버전인데 기존 공유 URL만 이전 버전을 반환하는 것을 관찰했다. 개발자는 실제 공유 URL의 버전을 확인한 뒤 완료를 알리고, 사용자는 잠시 후 설치 목록을 다시 새로고침하면 된다. 같은 저장소 URL을 삭제하거나 다시 등록할 필요는 없다.

다음 버전은 csproj의 Version을 증가시키고 새 태그·새 ZIP을 만든다. 기존 태그/ZIP을 수정하지 않는다. 새 ZIP 확인·게시·익명 다운로드 확인 후 같은 저장소 JSON URL의 버전과 링크만 갱신한다.

문제가 있으면 수정하거나 이전 코드를 복원해 **더 높은 버전**으로 다시 배포한다. 목록의 버전을 낮추는 것만으로 설치된 버전이 자동 다운그레이드된다고 가정하지 않는다. 설치 목록·ZIP·아이콘의 현재 주소, 원격 커밋과 확인 범위는 STATUS에 남긴다.
