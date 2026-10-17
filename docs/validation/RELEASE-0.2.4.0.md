# 첫 커스텀 저장소 배포: 0.2.4.0

기록일: 2026-10-17. 목표는 친구의 Dalamud 저장소 URL 등록 후 설치·업데이트다. 공식 등록은 범위 밖이다.

## 확인한 준비 상태

- `origin`: `https://github.com/PlatyDuckbill/DuckbillFisher.git`, 기본 브랜치 main, 공개 저장소. GitHub API로 소유 계정/게시 권한 확인. 인증은 기존 Git credential manager를 사용하며 비밀 값은 출력·저장하지 않았다.
- 시작 시 원격 fetch 후 main은 origin/main보다 8커밋 앞섰다. 기존 태그·공개 릴리스 없음. 사용자 `tmp_note.md`는 읽기·수정·스테이징하지 않았다.
- 배포 후보 버전은 미게시 `0.2.4.0` 유지. SDK·의존성·기능·네이티브 접근·예측·저장 정책 변경 없음. 정규 Debug DLL도 교체하지 않았다.
- 영어 Punchline/Description/README를 현재 구현 범위로 정리했다. 슬로건은 `Let Duckbill guide your next catch.`이며 지원 범위는 KR로 명시했다.
- 사용자 이미지 `image-s.png`를 `assets/duckbillfisher-icon.png`로 이동했다. PNG 512×512, SHA256 `7cec514fec1d88a842ef0c82071bb708c98e717b81ab8a494c6aa3bcb5a195fa`; 원본과 해시가 같아 디자인 변경 없음. `IconUrl`은 v0.2.4.0 태그의 공개 raw 주소다. 스크린샷 `ImageUrls`는 추가하지 않았다.

## 실행한 빌드·패키지 검사

```powershell
dotnet build src/DuckbillFisher.Plugin/DuckbillFisher.Plugin.csproj --no-restore -c Release -p:DalamudLibPath=C:/Users/hyogu/AppData/Roaming/XIVLauncherKR/addon/Hooks/dev/
pwsh -NoLogo -NoProfile -File scripts/check-localization.ps1
pwsh -NoLogo -NoProfile -File scripts/prepare-release.ps1 -PackagePath src/DuckbillFisher.Plugin/bin/Release/DuckbillFisher/latest.zip -AssetBaseUrl https://github.com/PlatyDuckbill/DuckbillFisher/releases/download/v0.2.4.0 -OutputDirectory artifacts/share/0.2.4.0
```

- 빌드 경고0/오류0, 한영555키/형식 인자 일치. 테스트 추가·실행 없음. 기존 테스트 CI도 유발하지 않도록 게시 커밋은 `[skip ci]`를 사용한다.
- ZIP 318,332바이트. SHA256: `055f6652ddf46c01b3e8b8edb9aa072df01904d7a42d8c455e14b516f0fd996b`.
- 패키징 허용 목록·필수 파일·Assembly/manifest 0.2.4.0/API15 검사 통과. 아래 ZIP 파일을 `.cache/release-validation/0.2.4.0/`에 추출해 각 Release 빌드 출력과 SHA256 동일 확인.

| ZIP 항목 | 바이트 | AssemblyVersion |
|---|---:|---|
| DuckbillFisher.dll | 1,144,832 | 0.2.4.0 |
| DuckbillFisher.Core.dll | 54,784 | 1.0.0.0 |
| ko/DuckbillFisher.resources.dll | 53,248 | 0.2.4.0 |
| DuckbillFisher.json | 1,065 | manifest 0.2.4.0 |
| DuckbillFisher.deps.json | 980 | 해당 없음 |
| DuckbillFisher.Core.pdb | 21,216 | 해당 없음 |

- Core는 기존 독립 버전 1.0.0.0을 유지하며 Plugin 버전과 혼동하지 않는다. Dalamud/게임 DLL은 배포하지 않는다.
- PE 메타데이터/리소스 바이트를 읽어 내장 FishingData 648,727바이트, FishClassifications 29,775바이트, ThirdPartyNotices 1,844바이트가 각 소스 SHA256과 동일함을 확인했다. 플러그인 코드를 게임 밖에서 실행한 검사가 아니다.
- `inspection.json`에 파일별 해시를 로컬 보관했다. `artifacts/`와 추출본은 Git에 포함하지 않는다.

## 인게임 확인과 게시 경계

- 기존 Debug 0.2.4.0의 탐색 줄/간이 목록과 0.2.3.0 생미끼/실패 표시는 사용자 대표 확인 범위다. Release ZIP 실행 확인으로 확대하지 않는다.
- 현재: **Release ZIP 인게임 확인 대기**. 로드·목록/간이 목록/타이머·한영·도감 연동·낚시1회·재로드 후 설정/기록 확인을 요청했다. 확인 결과는 이 구역에 이어 기록한다.
- GitHub Release/태그/원격 push/실제 설치 목록 반영은 아직 하지 않았다. `repo/pluginmaster.json`은 빈 배열이다.
- 이후: 사용자 Release 확인 → 소스/태그 push → ZIP/체크섬 Release 게시 → 익명 다운로드 해시/아이콘 확인 → 설치 JSON 게시 → 익명 JSON/링크 확인 → Dalamud 설치 확인. 더 높은 버전의 실제 업데이트는 첫 배포만으로 검증했다고 하지 않는다.

## 공식 근거와 적용

- [커스텀 저장소](https://dalamud.dev/plugin-publishing/custom-repositories/): 인증 없는 배열 JSON, 설치/업데이트 링크, IconUrl/ImageUrls 역할. SDK15 manifest의 나머지 필드는 그대로 보존했다.
- [이미지 규격](https://dalamud.dev/plugin-publishing/submission/): 아이콘 1:1, 64~512픽셀. 원본512 PNG를 그대로 사용했다. 공식 저장소 제출 절차를 수행한 것은 아니다.
- [메타데이터](https://dalamud.dev/plugin-development/plugin-metadata/): Punchline/Description/IconUrl. 실제 설치된 DalamudPackager15 targets에서도 IconUrl 전달을 확인했다.
- [GitHub CI 생략](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/skip-workflow-runs): 기존 push 테스트 워크플로우는 수정하지 않고 이번 테스트 미실행 요청을 유지한다.
