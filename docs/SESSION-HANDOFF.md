# 세션 인수인계

갱신: 2026-10-18 · 0.3.0.0 Release 추출본 대표 확인 완료 · 동일 ZIP 게시 진행

## 재개 순서

1. AGENTS.md/STATUS.md와 `git status --short`를 확인한다. 사용자 `tmp_note.md`는 읽기·수정·스테이징하지 않는다.
2. 다음 한 작업은 [페이지 표식/테마 실기 확인](validation/BAIT-PREPARATION.md)이다. Kind0 수정 뒤 기본 강조는 대표 확인했다. 정확한 KR 실행 파일의 전체 미끼 자료/25칸 페이지 구조를 확인하고, 현재 표시 칸의 Item ID 전체 대조 후 목적지 번호에 작은 표식/번호 안내를 구현했다. 게임의 선택 페이지 글자 색상과 반대 명암 테두리를 사용한다. 페이지 이동/선택은 사용자 직접 동작이며 UI 쓰기/함수 호출은 없다. 재로드 후 다른 페이지→직접 이동→미끼 테두리/종료와 다른 게임 테마를 확인받는다. 실패는v7의Pages 한 줄만 받는다.
3. [잔여 보고서](reports/fishing-data-2026-10-17/remaining.md)에 일반 핵심11종·선택 메타데이터355종·먼바다259종·작살115건의 전체 이름/ID/이유가 있다. 일반 상세1258/1258과 모든 필드 완성은 구분한다.
4. 사용자 상시 지시로 언로드는 기본 상태다. 별도→정규 빌드 후 주 DLL 절대 경로를 제공하고 다시 언로드를 묻지 않는다. 실제 파일 잠금 실패 시 원인만 보고한다.
5. 사용자 ‘Release 확인 완료’로 [고정 ZIP](validation/RELEASE-0.3.0.0.md) 추출본의 대표 확인을 받았다. 재빌드 없이 같은 ZIP을push/태그/Release/익명 다운로드 확인→저장소 JSON 순서로 게시한다. ZIP은370,524바이트/SHA256 시작4EC80617이다. 공개JSON은 다운로드 검증 후 갱신한다. 다른 캐릭터/전체 테마/친구 새 기능 전수 확인으로 확대하지 않는다.
6. 큰 UI 구조 변경은 [합의](UI-UX-REDESIGN.md)와 AGENTS에 따라 사용자에게 질의한다. 현재 독립 준비 화면과 페이지 번호 표식은 사용자가 승인한 범위다.

현재 관심은 터주작이다. 미끼 준비는 독립 보조 화면에서 즐겨찾기 기본/현재 필터 전체를 전환한다. 준비 목록의 찾기는 제거했고 상세 제목의 찾기는 유지한다. 대체 미끼 반복 목록도 후속 피드백으로 제거하고 집계 아래 ‘같은 어종이 여러 미끼에 포함될 수 있습니다.’만 안내한다. 상단 미확인 개수/하단 목록은 유지한다. 찾기 테두리/페이지 표식은 굵게 하고2.4초 주기의 외곽 강조를 더했다. 실기 대기다. 미끼 변경 함수 호출은 없다. 새 강조 실패는 기존 도감/타이머를 막지 않는다. 먼바다 상세는 후속이다.

## 환경과 산출물

- 작업: `C:\Users\hyogu\Downloads\DuckbillFisher`.
- 게임: `C:\Program Files (x86)\FINAL FANTASY XIV - KOREA\game`, KR2026.10.06.0000.0000.
- 현재 참조: `C:\Users\hyogu\AppData\Roaming\XIVLauncherKR\addon\Hooks\dev`, wasdcxz15.0.3.7/API15.
- 최저 지원 참조: `.cache/compat-15.0.3.6/full/` 고정본. 런타임은 정확한 wasdcxz/dal4kr 조합 가드 유지.
- SDK10.0.402 / Dalamud.NET.Sdk15.0.0 / x64/net10.0-windows. 의존성 버전은 변경하지 않았다.
- 정규 주 DLL: `C:\Users\hyogu\Downloads\DuckbillFisher\src\DuckbillFisher.Plugin\bin\Debug\DuckbillFisher.dll`.
- 후보 `.cache/data-completion-build/`, 최저 참조 `.cache/data-completion-oldrefs/`, 정규 빌드 모두 경고0/오류0. 한영559키/형식 인자 통과, Plugin/ko/manifest0.2.9.0·Core1.0.0.0·API15.
- 정규0.3.0.0 후보 `.cache/bait-preparation-build/`, 최저 `.cache/bait-preparation-oldrefs/`, 친구 `.cache/bait-preparation-friendrefs/`와 정규 빌드 경고0/오류0·한영606키. 수량/v3 복사/기본 강조는 대표 확인, 새 표시/패키지·원격 게시 대기. 페이지 추가23종 구조와5getter/5enum 대조 근거는validation을 따른다.

## 이번 데이터 구현

- `fishing.kr.v2.json` / `kr-regular-2026-10-17`: 기존903종 +353종 Teamcraft 정적 자료 +2종 부분 어획 사실 =1258종.
- 빈 낚아채기80·입질4·예측 위치31종 보완, 중복 제외466종 변경. 알려진 조건/미끼/직감·메타데이터와 예측 공식은 보존했다.
- 시간·날씨 확인1256종. 이크에바지 가위/요에콰 먹물머리는 금속 스피너·입질 강도·KR 위치만 확인하고 낚아채기/시간·날씨 완전성은Unknown 유지.
- 직감 지속 시간9종은 채택한 원본에 필드가 없어 남겼다. 다른 공개 자료의 값은 아직 검증하지 않았으며 출현 시간 길이로 대체하지 않는다.
- 물고기의 눈 지원 정보는 기존bool 유지/새355종null, UI 미표시. `patchVersion`은3.55b 접미사 보존, 기존 숫자patch 유지.
- 원본/보완 source revision·파일 해시·개별 사실 URL/작성자/확인일·MIT 고지를 manifest/notices에 기록했다. 런타임 외부 조회 없음.
- [재현 절차](../data/README.md): 기존 기본 원본→`.cache/fishing-all.json`→`--complete`→검토/제품 복사→`--classifications`. 완성된 제품을 보완 단계의baseline으로 넣지 않는다.
- 보완 manifest는 `data/fishing-supplement-source.json`, 개발 입력은 `.cache/data-completion/`. 도구는 해시/빌드/KR참조/시간/날씨/생미끼 순환을 검사한다. 전체 다운로드 원문은 커밋하지 않는다.
- 등급 재생성1258종 대조/실패0, 터주335(왕36) 양성 집합 유지. 기존 작살203건 동일/미확인115건. 지도 연결 불일치0. [실행 근거](validation/DATA-COMPLETION-2026-10-18.md).

## 일반 빌드

테스트는 명시 요청 때만 추가/실행한다. 테스트를 실행하는 `scripts/build.ps1`은 사용하지 않는다.

```powershell
dotnet build src/DuckbillFisher.Plugin/DuckbillFisher.Plugin.csproj --no-restore -c Debug -p:DalamudLibPath=C:/Users/hyogu/AppData/Roaming/XIVLauncherKR/addon/Hooks/dev/
pwsh -NoLogo -NoProfile -File scripts/check-localization.ps1
```

- 별도 출력은 `-p:OutputPath=C:/Users/hyogu/Downloads/DuckbillFisher/.cache/data-completion-build/` 추가. 같은 프로젝트의 참조별 빌드는 순서대로 수행한다.
- PowerShell 파일 읽기는 `-Encoding UTF8`. JSON은 파싱 후 필요한 필드만 출력하며 대용량 원문/전체 로그를 대화에 붙이지 않는다.
- 명시한 파일만 스테이징하고 커밋한다. 캐시/산출물/SDK/사용자 파일은 제외한다. 새 배포는 Release 최저 참조/패키지/인게임 확인을 별도로 진행한다.

## 유지할 정책과 확인 범위

- Core는 게임 독립, 네이티브는 Plugin 경계/정확한 KR가드/Framework 수명 유지. 읽기 실패를 미수집으로 바꾸지 않는다.
- 수집은 수동 우선, 도감1517종 전체 대조 후 자동 미수집 확정. 재접속 초기화/재조회는 확인, 다른 캐릭터는 사용할 수 없어 실기 보류.
- 메인/간이 일반 필터 공유·검색/예정 분 별도, 프리셋20개와 저장 정책 유지. 새 등록 자동 즐겨찾기 해제는 기본 켜짐/도감 조회·수동 지정은 트리거 아님. 알림 로컬22채널/SE, Toast 제거.
- 타이머/기록은 낚아채기에서 경과 정지, 완전 접기 후 현재 기록 비움. 캐릭터별100묶음×최근2000회/schema2, 저장 재시도/순번/요청 세대 보호 유지.
- 0.2.7.0 저장/복원/최신순/삭제와0.2.8.0 첫 칸 보관 생미끼 Item 기록은 사용자 대표 확인 완료. [저장](validation/MVP-02-HISTORY-STABILITY.md), [생미끼](validation/MVP-02-SWIMBAIT.md). 모든 장애/칸/조합의 확인으로 확대하지 않는다.
- 복수 어획·수량은 사용자 요청으로 후속. Teamcraft/바다구렁이 불일치와 입질 초 수 통계는 보류하며 재조사/로그 요청/공식 변경하지 않는다.
- 공개0.2.6.0 태그6fc2fc1/Release415773597/설치 JSON7179f43. [정확한 런타임/게시](validation/NATIVE-RUNTIME-DAL4KR.md), [설치 안내](DISTRIBUTION.md).
- 과거 버전별 실제 확인은 [정리 전 STATUS](history/STATUS-before-data-completion-2026-10-18.md), [정리 전 HANDOFF](history/HANDOFF-before-data-completion-2026-10-18.md)와 각 validation에 보존했다.
