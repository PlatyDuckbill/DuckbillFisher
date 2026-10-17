# 세션 인수인계

갱신: 2026-10-17 · 정규0.1.36.0 / 별도0.2.0.0 관찰 빌드 완료 · 새 언로드 확인 대기

## 재개 순서

1. AGENTS.md/STATUS.md와 `git status --short`부터 확인한다.
2. UI 작업은 [합의된 구조](UI-UX-REDESIGN.md), [분석·구현·확인 순서](validation/MVP-01-UI-UX-REDESIGN.md)를 읽는다.
3. 큰 디자인 변경은 선택지/차이를 질문하고 답변 후 구현한다. 이미 승인된 최대 세 창 구성을 재승인받을 필요는 없다.
4. 사용자 `tmp_note.md`는 읽기·수정·스테이징하지 않는다. 전체 로그/과거 이력은 기본 요청하지 않는다.
5. 현재 [캐스팅/히스토리](CASTING-TIMER-HISTORY.md)는 별도 타이머+팝업+메인 탭/전체↔수집 숨김/캐릭터별1000회 저장을 합의했다. [0.2.0.0 첫 관찰](validation/MVP-02-CASTING-PROBE.md)의 별도 빌드 경고0/오류0·한영470키·출력 검사 통과. 새 언로드 확인 후 정규 빌드하고 제한된 KR 동작을 대조한다. 타이머/영속 이력은 아직 없으며 이전 확인 대기는 유지한다.

## 환경과 DLL

- 작업 폴더: `C:\Users\hyogu\Downloads\DuckbillFisher`.
- 게임: `C:\Program Files (x86)\FINAL FANTASY XIV - KOREA\game`, KR 빌드 2026.10.06.0000.0000.
- 참조: `C:\Users\hyogu\AppData\Roaming\XIVLauncherKR\addon\Hooks\dev`, Dalamud 15.0.3.6/API15.
- 개발 DLL: `C:\Users\hyogu\Downloads\DuckbillFisher\src\DuckbillFisher.Plugin\bin\Debug\DuckbillFisher.dll`.
- 현재 정규 0.1.36.0: 별도/정규 빌드 경고·오류 0, 한영 468키/형식 인자, Plugin/Core/ko/manifest 버전 확인. 테스트 미실행.
- 이번 수정의 새 언로드 완료 확인 후 정규 빌드를 완료했다. 이후 새 기능 교체는 해당 작업의 로드 상태/승인을 따른다.
- 0.1.26.0 사용자 `다 잘됨` 대표 확인 유지. 0.1.27.0 사용 피드백을 반영했으며 0.1.28.0 사용 피드백을 반영했고 0.1.29.0 결과는 아직 없다. 커밋은 로컬 보존이며 푸시/릴리스하지 않았다.

## 이번 UI 구조와 정책

- 메인: 어종 목록/즐겨찾기 관리/설정. 목록/즐겨찾기의 클릭은 오른쪽 상세 패널을 갱신한다.
- 간략: 메인 버튼 또는 `/duckbillfisher compact`로 열기. 현재 출현 종료순→예정 시작순/기본 30분 이내·1~180분 설정. 0.1.28.0은 메인 필터를 공유하고 독립 즐겨찾기 체크를 제거했다.
- 독립 상세: 간략 창 클릭 시 하나를 재사용한다. 메인을 열지 않으며 세 창은 각각 닫을 수 있다.
- 0.1.28.0 사용자 결정: 간략과 메인은 일반 필터 전체 공유, 검색어/예정 분만 별도. CatalogFilter 공통 판정이며 알림 대상은 독립이다. 상시/먼바다/예측 미확인은 간략에서 제외한다.
- 개발자 도구는 설정에서 켠다. 진단 표시만 제어하고 수집/예측/알림 서비스는 그대로 작동한다. 끌 때 진단용 두 필터를 해제한다.
- 수집 상태는 메뉴 하나에서 수동 지정하고 수동일 때만 복귀 버튼 표시. 자동 접미사 없음/수동 접미사 유지. Unknown은 미수집이 아니다.
- 현재 소스 schema v11: v1~v10 허용, 일반 필터/예정 분/즐겨찾기/수동/알림 설정 보존. preferences/v9에 프리셋 개수/오류 코드 추가. 간략은 SharedCatalog를 유지한다.
- `FishDetailPanel` 공통 상세, `UiWidgets` 아이콘/배너, `CompactFishObserver` Framework 1초 필터/수명 관리. Draw에서 전체 검색/장기 예측/파일 읽기 없음.
- 0.1.29.0은 목록 직접 핀/이름·수집 및 위치·시간 정렬/직감 배지, 상세 Patch 우측/작은 물고기의 눈/옆 조건/시간 한 줄을 적용했다. 새 언로드 확인 후 정규 빌드·한영 448키 통과. 다음은 [배치/상호작용 확인](validation/MVP-01-UI-ALIGNMENT.md)이다.

- 0.1.31.0은 물고기의 눈 표시 제거/등급·별·수집 선택 한 줄/명시적 수동 지정/직감 배지 종횡비·배경 정리/글자 없는 지도 아이콘/낚시터 연결 누락 안내를 포함한다. 데이터/예측/수집/알림 정책은 그대로다. [변경과 다음 확인](validation/MVP-01-UI-REFINEMENT.md).
- 0.1.30.0 OpenConfigUi 누락 경고 수정도 정규 0.1.31.0에 포함했다. MainWindow.OpenSettings를 로드/해제 시 구독/해제하고 실제 설정 탭으로 이동한다. [확인 순서](validation/CONFIG-UI-CALLBACK.md).
- 0.1.32.0은 상세 조작 줄을 이름 바로 아래/아이콘 오른쪽으로 이동, 작은 예측 없음 표시/원인 툴팁, 테마 지도 아이콘, 간략 한 줄 도구 모음으로 수정했다. 별도/정규 빌드 경고·오류 0/한영 451키 검사 통과. 다음은 정규 DLL 재로드 후 사용자 대표 확인이다. [근거/확인 순서](validation/MVP-01-UI-HEADER-POLISH.md).
- 이후 실제 화면에 사용자 ‘좋다’로 시각적 대표 확인을 받았다. [추가 UI 제안](UI-UX-SCREENSHOT-REVIEW.md) 1~7번도 승인 후 정규 0.1.34.0에 반영했다. [구현/다음 확인](validation/MVP-01-UI-SCREENSHOT-POLISH.md).
- 0.1.33.0 안내의 ‘어류도감 열기’도 정규 0.1.34.0에 포함했다. Framework에서 한 번 처리하며 정확한 KR 가드/캐릭터/2초 만료/주소 확인 뒤 낚시 탭을 연다. 준비 완료는 기존 관찰기가 판단한다. [근거/확인 순서](validation/MVP-01-GUIDE-OPEN.md).
- 0.1.35.0 별도/정규 빌드 경고·오류 0/한영 456키 통과. 좁은 목록도 조건 4줄/… 툴팁, 전승록 상세 한정, 터주 이상 신규/복원 기본값, 아이콘 세로 중앙, 한 줄 전환 28→23배와 실측 배분, 7일 예측 없음 목록 한 줄을 반영했다. 기존 저장 등급/스키마/수집/알림/예측 계산은 유지한다. 다음은 정규 DLL 재로드 후 사용자 대표 확인이다. [기록/확인 순서](validation/MVP-01-RESPONSIVE-ROWS.md).
- 0.1.36.0 별도/정규 빌드·한영 468키 통과. 전체 보기(검색·일반 필터 해제/정렬 유지), 최대 20개 이름 프리셋/확인 삭제, 예약 상태 문구 제거, 낚시터 연결 기술 문구 제거를 포함한다. 31종은 원본 조사 후에도 미확인을 유지한다. 새 언로드 확인 후 정규 반영했으며 다음은 사용자 대표 확인이다. [근거/확인](validation/MVP-01-FILTER-PRESETS.md).

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
