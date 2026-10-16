# 세션 인수인계

갱신: 2026-10-16

## 재개 순서

1. AGENTS.md와 STATUS.md를 읽고 `git status --short`로 기존 수정 확인.
2. WORKFLOW/validation은 관련 절차/근거가 필요할 때만 해당 구역 조회.
3. 과거 이력·긴 로그를 매번 전부 읽거나 대화에 붙이지 않는다.
4. 통과한 황금나비어 숨김·가라루파 등록·지도 핀 검증은 반복 요청하지 않는다.
5. 사용자 파일 `tmp_note.md`는 읽기·수정·스테이징하지 않는다.

## 환경과 DLL

- 작업 폴더: `C:\Users\hyogu\Downloads\DuckbillFisher`.
- 게임: `C:\Program Files (x86)\FINAL FANTASY XIV - KOREA\game`.
- 참조: `C:\Users\hyogu\AppData\Roaming\XIVLauncherKR\addon\Hooks\dev`.
- 개발 DLL: `C:\Users\hyogu\Downloads\DuckbillFisher\src\DuckbillFisher.Plugin\bin\Debug\DuckbillFisher.dll`.
- 0.1.9.0 전체 도감 대조/홍룡 미수집 필터/도감 닫힘 후 유지와 동일 캐릭터 재접속 초기화·복구 확인. 다른 캐릭터는 없어 격리 실기 확인 보류. 다음 교체는 새 언로드 확인 필요.
- 정규 DLL 0.1.11.0의 상세 140종 연결/목록·사용자 대표 확인 통과. 다음 교체에는 새 언로드 확인 필요. [확대 결과](validation/MVP-01-DATA-EXPANSION.md).
- 정규 DLL 0.1.12.0의 일반 903종/예측 대상 872종/먼바다 259종·작살 보존 203건(115건 미확인) 런타임 수치와 사용자 ‘문제 없음’으로 대표 확인 통과. 다음 교체에는 새 언로드 확인 필요. [이번 결과/남은 검증](validation/MVP-01-ALL-SOURCE-DATA.md)을 따른다.
- 사용자 후속 UI 요구: 현재 지역만 필터와 목록 행의 간단한 미끼/시간/조건 요약. 지역 ID와 조건 메타데이터를 유지했다. 먼바다 방문 시간대를 어종별 제한으로 해석하지 않는다.
- 추가 분류 필터 F12는 시간제(ET/날씨) / 터주 / 터주왕 / 모두 보기다. 수집 여부와 독립되며 포함 관계·단일/복수 선택·필터 간 결합 규칙은 PRODUCT의 미정 항목부터 논의한다. 아직 구현하지 않았다.
- PowerShell 5.1 읽기는 `-Encoding UTF8`; pwsh는 현재 샌드박스 밖 승인 실행 필요.

## 일반 구현 빌드

테스트는 사용자 요청 때만 추가/실행한다. 아래 명령은 테스트를 실행하지 않는다.

```powershell
dotnet build src/DuckbillFisher.Plugin/DuckbillFisher.Plugin.csproj --no-restore -c Debug -p:DalamudLibPath=C:/Users/hyogu/AppData/Roaming/XIVLauncherKR/addon/Hooks/dev/
pwsh -NoLogo -NoProfile -File scripts/check-localization.ps1
```

- 1차 빌드는 `-p:OutputPath=C:/Users/hyogu/Downloads/DuckbillFisher/.cache/all-details-build/` 추가.
- 성공 후 언로드 요청 → 사용자 확인 → 정규 Debug 빌드로 교체.
- 실제 주 DLL/버전·Core DLL·ko 리소스 DLL·manifest 확인.
- `scripts/build.ps1`은 테스트도 실행하므로 일반 구현 때 사용하지 않는다.
- 파일을 명시해 커밋한다. 현재 Git 메타데이터 쓰기는 별도 실행 승인 필요. 푸시는 하지 않는다.

## 유지할 사실

- 최신 기능·검증 수치·보류는 [STATUS](STATUS.md)를 따른다.
- 자동 수집은 True 관찰/등록 이벤트를 사용한다. 자동 미수집은 0.1.9.0의 게임 도감 전체 목록 대조 후만 적용한다. False·IsLoaded·창 표시만으로 확정하지 않는다. [근거와 다음 확인](validation/MVP-04-GUIDE-SNAPSHOT.md)을 따른다.
- 네이티브 접근은 조사한 KR 빌드와 DLL SHA/MVID 일치 조건 유지.
- 짧은 진단을 기본 요청한다. 전체 진단은 관련 오류 조사에 한정하고 가능하면 파일로 받는다.
- 조건 데이터 재생성은 `data/README.md`를 따른다. 현재 제품 입력은 schema v2의 fishing.kr.v2.json이며 v1은 과거 이력이다. JS/YAML 해시는 data/fishing-source.json에 고정했다.
- 새 대화는 이 문서와 STATUS로 재개한다. 호스트가 지원하면 수동 `/compact` 사용 가능. 에이전트가 압축 주기를 직접 바꾸지는 못한다.
- [정리 전 인수인계](history/SESSION-HANDOFF-before-context-cleanup-2026-10-16.md)는 필요할 때만 조회한다.
