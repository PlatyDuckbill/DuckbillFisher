# 세션 인수인계

갱신: 2026-10-18 · 정규0.2.10.0 미끼 준비 첫 단계 / KR UI 표본 대기 / 배포 승인·공개0.2.6.0 유지

## 재개 순서

1. AGENTS.md/STATUS.md와 `git status --short`를 확인한다. 사용자 `tmp_note.md`는 읽기·수정·스테이징하지 않는다.
2. 다음 한 작업은 [0.2.10.0 미끼 UI v2 표본/강조 구현](validation/BAIT-PREPARATION.md)이다. 사용자는 소지 수량 일치를 대표 확인했고 UI v1은NotOpen이었다. 정규의20초 대기 수집v2로 ‘수집 먼저→게임 미끼 UI 열기→복사’ 표본을 재요청했다. 실제 숫자/항목 연결 없이 강조하지 않는다. 데이터0.2.9.0 대표 확인도 남아 있다.
3. [잔여 보고서](reports/fishing-data-2026-10-17/remaining.md)에 일반 핵심11종·선택 메타데이터355종·먼바다259종·작살115건의 전체 이름/ID/이유가 있다. 일반 상세1258/1258과 모든 필드 완성은 구분한다.
4. 사용자 상시 지시로 언로드는 기본 상태다. 별도→정규 빌드 후 주 DLL 절대 경로를 제공하고 다시 언로드를 묻지 않는다. 실제 파일 잠금 실패 시 원인만 보고한다.
5. 공개0.2.6.0은 친구 업데이트/도감/수집/타이머 대표 확인 완료. 이번0.2.10.0은 배포까지 승인됐다. 강조 구현 뒤 최저 지원 참조 Release/패키지/추출본 인게임 확인을 받고 Release/다운로드 확인→저장소 JSON 순으로 게시한다. 미검증 ZIP은 게시하지 않는다.
6. 큰 UI 구조 변경은 [합의](UI-UX-REDESIGN.md)와 AGENTS에 따라 사용자에게 질의한다. 현재 작업은 데이터·Patch·출처 표시만 보완했다.

현재 관심은 터주작이다. 미끼 준비는 메인 필터 옆 버튼에서 열고 즐겨찾기 기본/현재 필터 전체를 전환한다. 첫 미끼 집계/상세 소지 수량 구현, 강조 미구현·실기 대기. 미끼 변경 함수 호출 없이 UI 단일 강조 흐름으로 이어간다. 새 강조 실패는 기존 도감/타이머 지원을 막지 않게 분리한다. 먼바다 상세는 후속이다.

## 환경과 산출물

- 작업: `C:\Users\hyogu\Downloads\DuckbillFisher`.
- 게임: `C:\Program Files (x86)\FINAL FANTASY XIV - KOREA\game`, KR2026.10.06.0000.0000.
- 현재 참조: `C:\Users\hyogu\AppData\Roaming\XIVLauncherKR\addon\Hooks\dev`, wasdcxz15.0.3.7/API15.
- 최저 지원 참조: `.cache/compat-15.0.3.6/full/` 고정본. 런타임은 정확한 wasdcxz/dal4kr 조합 가드 유지.
- SDK10.0.402 / Dalamud.NET.Sdk15.0.0 / x64/net10.0-windows. 의존성 버전은 변경하지 않았다.
- 정규 주 DLL: `C:\Users\hyogu\Downloads\DuckbillFisher\src\DuckbillFisher.Plugin\bin\Debug\DuckbillFisher.dll`.
- 후보 `.cache/data-completion-build/`, 최저 참조 `.cache/data-completion-oldrefs/`, 정규 빌드 모두 경고0/오류0. 한영559키/형식 인자 통과, Plugin/ko/manifest0.2.9.0·Core1.0.0.0·API15.
- 정규0.2.10.0 후보 `.cache/bait-preparation-build/`, 최저 `.cache/bait-preparation-oldrefs/`, 친구 `.cache/bait-preparation-friendrefs/`와 정규 빌드 경고0/오류0·한영584키. 정규/후보 Plugin/Core/ko/manifest 해시4/4 일치. 수량 일치만 대표 확인, 미끼 UI 표본/강조·패키지준비·원격 게시 대기.

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
