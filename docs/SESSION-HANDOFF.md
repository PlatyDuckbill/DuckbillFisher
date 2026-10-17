# 세션 인수인계

갱신: 2026-10-17 · 0.1.31.0 정규 반영 완료 · 인게임 확인 대기

## 재개 순서

1. AGENTS.md/STATUS.md와 `git status --short`부터 확인한다.
2. UI 작업은 [합의된 구조](UI-UX-REDESIGN.md), [분석·구현·확인 순서](validation/MVP-01-UI-UX-REDESIGN.md)를 읽는다.
3. 큰 디자인 변경은 선택지/차이를 질문하고 답변 후 구현한다. 이미 승인된 최대 세 창 구성을 재승인받을 필요는 없다.
4. 사용자 `tmp_note.md`는 읽기·수정·스테이징하지 않는다. 전체 로그/과거 이력은 기본 요청하지 않는다.

## 환경과 DLL

- 작업 폴더: `C:\Users\hyogu\Downloads\DuckbillFisher`.
- 게임: `C:\Program Files (x86)\FINAL FANTASY XIV - KOREA\game`, KR 빌드 2026.10.06.0000.0000.
- 참조: `C:\Users\hyogu\AppData\Roaming\XIVLauncherKR\addon\Hooks\dev`, Dalamud 15.0.3.6/API15.
- 개발 DLL: `C:\Users\hyogu\Downloads\DuckbillFisher\src\DuckbillFisher.Plugin\bin\Debug\DuckbillFisher.dll`.
- 현재 정규 0.1.31.0: 별도/정규 빌드 경고·오류 0, 한영 450키/형식 인자, Plugin/Core/ko/manifest 버전 확인. 테스트 미실행.
- 사용자 최신 언로드 완료 확인 후 글자 없는 지도 아이콘/명확한 안내 문장 수정과 정규 빌드 완료. 이후 새 기능 교체는 해당 작업의 로드 상태/승인을 따른다.
- 0.1.26.0 사용자 `다 잘됨` 대표 확인 유지. 0.1.27.0 사용 피드백을 반영했으며 0.1.28.0 사용 피드백을 반영했고 0.1.29.0 결과는 아직 없다. 커밋은 로컬 보존이며 푸시/릴리스하지 않았다.

## 이번 UI 구조와 정책

- 메인: 어종 목록/즐겨찾기 관리/설정. 목록/즐겨찾기의 클릭은 오른쪽 상세 패널을 갱신한다.
- 간략: 메인 버튼 또는 `/duckbillfisher compact`로 열기. 현재 출현 종료순→예정 시작순/기본 30분 이내·1~180분 설정. 0.1.28.0은 메인 필터를 공유하고 독립 즐겨찾기 체크를 제거했다.
- 독립 상세: 간략 창 클릭 시 하나를 재사용한다. 메인을 열지 않으며 세 창은 각각 닫을 수 있다.
- 0.1.28.0 사용자 결정: 간략과 메인은 일반 필터 전체 공유, 검색어/예정 분만 별도. CatalogFilter 공통 판정이며 알림 대상은 독립이다. 상시/먼바다/예측 미확인은 간략에서 제외한다.
- 개발자 도구는 설정에서 켠다. 진단 표시만 제어하고 수집/예측/알림 서비스는 그대로 작동한다. 끌 때 진단용 두 필터를 해제한다.
- 수집 상태는 메뉴 하나에서 수동 지정하고 수동일 때만 복귀 버튼 표시. 자동 접미사 없음/수동 접미사 유지. Unknown은 미수집이 아니다.
- 수정본 schema v10: v1~v9 허용, 기존 일반 필터/예정 분 보존, 과거 간략 FavoritesOnly 무시. preferences/v8에 CompactFilters=SharedCatalog 표시. 수집/알림/자동 해제 선택은 그대로다.
- `FishDetailPanel` 공통 상세, `UiWidgets` 아이콘/배너, `CompactFishObserver` Framework 1초 필터/수명 관리. Draw에서 전체 검색/장기 예측/파일 읽기 없음.
- 0.1.29.0은 목록 직접 핀/이름·수집 및 위치·시간 정렬/직감 배지, 상세 Patch 우측/작은 물고기의 눈/옆 조건/시간 한 줄을 적용했다. 새 언로드 확인 후 정규 빌드·한영 448키 통과. 다음은 [배치/상호작용 확인](validation/MVP-01-UI-ALIGNMENT.md)이다.

- 0.1.31.0은 물고기의 눈 표시 제거/등급·별·수집 선택 한 줄/명시적 수동 지정/직감 배지 종횡비·배경 정리/글자 없는 지도 아이콘/낚시터 연결 누락 안내를 포함한다. 데이터/예측/수집/알림 정책은 그대로다. [변경과 다음 확인](validation/MVP-01-UI-REFINEMENT.md).
- 0.1.30.0 OpenConfigUi 누락 경고 수정도 정규 0.1.31.0에 포함했다. MainWindow.OpenSettings를 로드/해제 시 구독/해제하고 실제 설정 탭으로 이동한다. [확인 순서](validation/CONFIG-UI-CALLBACK.md).

## 일반 구현 빌드

테스트는 명시 요청 때만 추가/실행한다. `scripts/build.ps1`은 테스트를 실행하므로 사용하지 않는다.

```powershell
dotnet build src/DuckbillFisher.Plugin/DuckbillFisher.Plugin.csproj --no-restore -c Debug -p:DalamudLibPath=C:/Users/hyogu/AppData/Roaming/XIVLauncherKR/addon/Hooks/dev/
pwsh -NoLogo -NoProfile -File scripts/check-localization.ps1
```

- 별도 출력은 `-p:OutputPath=C:/Users/hyogu/Downloads/DuckbillFisher/.cache/ui-refine-build/` 추가.
- PowerShell 5.1 파일 읽기는 `-Encoding UTF8`. 이번 환경에서 pwsh/정규 빌드/git 쓰기 모두 실행됐다.
- 실제 주 DLL·Core·ko 리소스 DLL·manifest 확인 후 파일을 명시해 스테이징/커밋한다. 빌드 결과/캐시/사용자 파일 제외.

## 유지할 경계와 보류

- 자동 수집은 True/검증된 등록 이벤트, 자동 미수집은 전체 도감 1517종 대조 뒤만 사용한다. 수동 우선, 캐릭터 세션 격리 유지. [근거](validation/MVP-04-GUIDE-SNAPSHOT.md).
- 새 등록 자동 즐겨찾기 해제 기본 켜짐. 도감 조회/수동 지정/반복 어획은 해제 트리거가 아니다. 로컬 채팅/SE/아이템·지도 링크, 예약/중복 방지 유지.
- Teamcraft/바다구렁이 차이는 사용자 요청으로 미해결 후속 조사다. 재조사/로그 요청/예측 공식 임의 수정 금지. Toast는 제거 상태 유지.
- 다른 캐릭터 격리는 사용 가능한 캐릭터가 없어 실기 보류. 입질 초 수 통계/작살 UI/외부 좌표 차이 확인은 후속이다.
- 정적 수치/검증된 과거 범위는 STATUS를 따른다. UI 언어와 게임 데이터 언어는 다르며 글로벌 빌드 지원으로 표현하지 않는다.
- 데이터 재생성은 data/README.md와 고정 원본 해시를 따른다. SDK/네이티브 오프셋/데이터 갱신은 별도 작업이다.
