# 0.1.30.0 설정 UI 콜백 등록

갱신: 2026-10-17 · 0.1.31.0 정규 DLL에 포함 · 인게임 확인 대기

- 사용자 화면에 설정 UI 콜백 미등록 검증 경고가 표시됐다. Plugin은 OpenMainUi만 등록하고 OpenConfigUi는 등록하지 않은 상태였다.
- 설치 Dalamud 15.0.3.6의 IUiBuilder.OpenConfigUi 문서에서 설정 UI 열기 요청 이벤트임을 확인했다.
- Plugin 로드 시 OpenConfigUi에 MainWindow.OpenSettings를 등록하고 Dispose에서 같은 핸들러를 해제한다.
- OpenSettings는 메인을 열고 기존 설정 탭을 선택한다. 이미 열려 있어도 닫지 않으며, 즐겨찾기의 설정 버튼도 같은 경로를 사용한다. 빈 콜백으로 경고만 숨기지 않는다.
- 새 화면/문구/게임 접근/설정 스키마 변경 없음. 기존 탭 고정 ID와 설정값 유지.
- 별도 빌드: `dotnet build src/DuckbillFisher.Plugin/DuckbillFisher.Plugin.csproj --no-restore -c Debug -p:DalamudLibPath=C:/Users/hyogu/AppData/Roaming/XIVLauncherKR/addon/Hooks/dev/ -p:OutputPath=C:/Users/hyogu/Downloads/DuckbillFisher/.cache/config-ui-build/` → 경고 0/오류 0.
- `pwsh -NoLogo -NoProfile -File scripts/check-localization.ps1` → 한영 448키/형식 인자 일치. 테스트 추가/실행 없음.
- 0.1.30.0은 별도 빌드만 수행했고 수정은 0.1.31.0 정규 DLL에 포함했다. 사용자 새 언로드 확인 후 정규 빌드 경고·오류 0. 인게임에서는 경고 해제와 Dalamud 설정 버튼 → 메인 설정 탭, 기존 화면이 열렸을 때도 설정 탭으로 이동하는지 확인한다. [통합 빌드 기록](MVP-01-UI-REFINEMENT.md).
- 사용자 tmp_note.md는 읽기/수정/스테이징하지 않았다. 원격 게시/릴리스 없음.
