# 친구에게 URL로 공유하기

## 목표와 현재 상태

최소 배포 목표는 커스텀 저장소 URL을 등록한 친구가 DuckbillFisher를 설치하고 같은 경로로 업데이트하는 것이다. 공식 Dalamud 등록은 장기 목표다.

현재 `repo/pluginmaster.json`은 빈 배열이다. 시작 창 빌드와 배포 준비 도구만 있으며, 아직 원격 게시·인게임 검증·친구 설치 테스트를 하지 않았다.

예정 주소(아직 사용할 수 있는 배포 URL이 아님):

```text
https://raw.githubusercontent.com/PlatyDuckbill/DuckbillFisher/main/repo/pluginmaster.json
```

GitHub 저장소/파일이 공개되어 있어야 이 주소를 익명으로 읽을 수 있다. 소스를 비공개로 유지한다면 공개 배포 전용 저장소 또는 별도 정적 호스팅을 사용하고 주소를 바꾼다. 커스텀 저장소는 인증을 지원하지 않으므로 '친구에게 공유'는 접근 제한을 뜻하지 않는다. [공식 커스텀 저장소 규격](https://dalamud.dev/plugin-publishing/custom-repositories/).

## 구성

- `pluginmaster.json`: 이름·버전·API level·ZIP 설치/업데이트 URL을 담은 배열.
- GitHub Release의 `DuckbillFisher-<version>.zip`: 해당 버전의 실제 DLL과 manifest.
- 체크섬 파일: 게시 파일 확인용 SHA256. Dalamud가 이를 자동 검증한다고 가정하지 않는다.
- 설치/업데이트 통신은 Dalamud가 수행한다. DuckbillFisher 런타임에는 외부 네트워크 코드를 추가하지 않는다.

## 로컬 배포 준비

PowerShell 7에서 실행한다. 먼저 해당 Windows 한섭 환경에서 기능·언로드·재로드를 확인한다.

```powershell
pwsh -File scripts/build.ps1 -DalamudPath "실제 Dalamud.dll 폴더" -Configuration Release

pwsh -File scripts/prepare-release.ps1 `
  -PackagePath src/DuckbillFisher.Plugin/bin/Release/DuckbillFisher/latest.zip `
  -AssetBaseUrl https://github.com/PlatyDuckbill/DuckbillFisher/releases/download/v0.1.0.0 `
  -OutputDirectory artifacts/share/0.1.0.0
```

버전은 Plugin csproj의 Version에서 관리한다. 예시의 release tag/출력 경로도 실제 버전에 맞춘다. 스크립트는 ZIP의 필수 파일, 허용 목록, API, DLL/manifest 버전 일치를 확인한 후 버전별 ZIP·SHA256·pluginmaster.json을 로컬에 만든다. 기존 결과가 있으면 덮어쓰지 않는다. 업로드하거나 repo/pluginmaster.json을 자동 변경하지 않는다.

## 게시 순서

1. 테스트한 커밋, 지원하는 KR 게임/Dalamud 버전, 변경 사항을 기록한다. 코드/도입 자료 라이선스를 정리한다.
2. GitHub에 해당 버전의 release와 ZIP/체크섬을 게시한다. 태그와 링크가 가리키는 빌드는 검증한 빌드와 같아야 한다.
3. 로그아웃 상태에서도 ZIP 다운로드가 되는지 확인한다.
4. 생성한 pluginmaster.json을 repo/pluginmaster.json에 반영하여 게시한다. ZIP 게시보다 앞서 목록을 바꾸지 않는다.
5. 친구 PC에서 커스텀 저장소 추가 → 설치 → 명령으로 창 열기 → 재시작 확인.
6. 다음 버전의 AssemblyVersion을 증가시키고 같은 순서로 게시하여 업데이트를 검증한다.

현재는 사용자에게 유용한 기능을 만든 뒤 첫 친구 배포를 진행한다. 아직 공개용 release나 tag를 생성하지 않았다.

## 업데이트와 복구

기존 태그의 ZIP을 덮어쓰지 않는다. 문제가 있으면 수정하거나 이전 코드를 복원한 뒤 **더 높은 버전**으로 다시 배포한다. 단순히 목록의 버전을 낮추는 것으로 이미 설치된 버전이 자동 다운그레이드된다고 가정하지 않는다.

공식 등록 시에는 그 시점의 제출 요구사항과 한섭 지원 가능성을 별도로 확인한다. 최소 공유 목표 때문에 자동화 금지나 오프라인 런타임 원칙을 바꾸지 않는다.
