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
- 정규 DLL 0.1.12.0의 일반 903종/예측 대상 872종/먼바다 259종·작살 보존 203건(115건 미확인) 런타임 수치와 사용자 ‘문제 없음’으로 대표 확인 통과. 다음 교체에는 새 언로드 확인 필요. [이번 결과/남은 검증](validation/MVP-01-ALL-SOURCE-DATA.md)을 따른다.
- 0.1.13.0 목록 요약은 사용자 ‘정상임’으로 대표 확인 통과. [결과](validation/MVP-01-LIST-SUMMARY.md).
- 0.1.14.0 현재 지역 필터는 사용자 ‘정상’으로 대표 확인 통과. [결과](validation/MVP-01-CURRENT-REGION.md).
- 현재 지역은 일반 낚시터의 Territory ID 기준이며 먼바다·위치 미확인 어종을 제외한다. 다른 필터 선택은 유지한다. 먼바다 방문 시간대를 어종별 제한으로 해석하지 않는다.
- 0.1.15.0의 분류 335/왕 36/시간제 443과 터주왕+수집됨 결과 7종 확인. 한영/모든 선택 화면은 별도 미확인이다. [결과](validation/MVP-01-CLASSIFICATION-FILTERS.md).
- 사용자 선택은 시간제 체크 + 전체/터주(왕 포함)/터주왕 단일 선택이다. 다른 필터와 AND, 등급 전체는 다른 선택을 유지한다. 검수 ID 자료는 터주 335/왕 36종이다. 미확인 분류를 특정 등급/시간제로 추측하지 않는다.
- 수집 UI의 ‘미수집만’을 제거했고 네 항목을 명시적으로 Core enum에 매핑한다. 기존 수집 숨김/내부 Unknown/자동 관찰/수동 우선순위는 유지한다.
- 0.1.16.0 필터 기능은 대표 확인 통과, 후보 64/6·모순 선택 빈 결과 확인. UI 문제는 별도로 개선했다. [결과](validation/MVP-01-GEOGRAPHY-FILTERS.md).
- 지역/확장팩은 복수 선택, 확장팩은 낚시터 소속 기준이다. 그룹 안 OR/같은 낚시터에서 그룹 간 AND를 적용하며 현재 지역만과도 함께 적용한다. ExVersion 0은 신생이며 미확인은 null로 구분한다. 선택 후보는 로컬 자료에서 64지역/6확장팩이다.
- 0.1.17.0은 별도/정규 빌드 경고·오류 0, 한영 244키 검사와 사용자 ‘정상’으로 확장팩 체크/지역 선택 UI 대표 확인 통과. [결과](validation/MVP-01-GEOGRAPHY-PICKER-UI.md).
- 0.1.18.0 남은 시간순은 사용자 ‘정렬은 잘 됨’으로 대표 확인 통과. 열린 창 종료순→예정 창 시작순→미확인→상시, 동률 이름/ID이며 지역은 기존 필터로만 적용한다. [근거](validation/MVP-01-REMAINING-TIME-SORT.md).
- 0.1.19.0은 별도/정규 빌드 경고·오류 0/한영 245키 검사와 사용자 ‘배경 로드 문제 없음’으로 대표 확인 통과. summary/v5 선택 예측 시각과 같은 시점 Teamcraft 활성 응답도 확보했다.
- Teamcraft/바다구렁이 차이는 사용자 요청으로 미해결 후속 조사에 보류했다. 다음 작업에서 조사/로그 요청을 재개하지 않고 기존 예측을 임의 수정하지 않는다. 당시 자료와 제외한 가설은 [조사 기록](validation/MVP-03-OPEN-WINDOW-DIAGNOSTICS.md)에 보존했다.
- 현재 정규 DLL은 0.1.20.0이다. 사용자 언로드 확인 후 별도/정규 빌드 경고·오류 0/한영 265키 검사와 주 DLL/Core/ko/manifest 확인 완료. 다음은 로드 후 알림 대표 확인이며 추가 교체는 새 언로드 확인 필요. [예약/저장 정책·검사·인게임 순서](validation/MVP-03-SPAWN-ALERTS.md)를 따른다.
- 알림 탭: 기본 꺼짐/5분, 실제 1~60분 전, 첫 채널 본인 로컬 채팅. Unknown 제외/수동 우선/목록 표시 필터와 독립이다. 캐릭터별 발송 창 시작을 저장 후 출력하며 설정 v1의 즐겨찾기/수동 상태를 v2로 보존한다. Toast/소리는 후속. 실제 출력·재로드 중복·절전 복귀는 인게임 검증 대기다.
- PowerShell 5.1 읽기는 `-Encoding UTF8`; pwsh는 현재 샌드박스 밖 승인 실행 필요.

## 일반 구현 빌드

테스트는 사용자 요청 때만 추가/실행한다. 아래 명령은 테스트를 실행하지 않는다.

```powershell
dotnet build src/DuckbillFisher.Plugin/DuckbillFisher.Plugin.csproj --no-restore -c Debug -p:DalamudLibPath=C:/Users/hyogu/AppData/Roaming/XIVLauncherKR/addon/Hooks/dev/
pwsh -NoLogo -NoProfile -File scripts/check-localization.ps1
```

- 1차 빌드는 `-p:OutputPath=C:/Users/hyogu/Downloads/DuckbillFisher/.cache/spawn-alert-build/` 추가.
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
- 별도 fish-classifications.kr.v1.json은 KR 설명을 개발 중 검수한 숫자 ID 자료다. 재생성은 같은 도구의 --classifications 모드, 출력/조건 해시는 LF로 정규화한다. UI 표시 문자열을 실행 중 판정에 사용하지 않는다.
- 새 대화는 이 문서와 STATUS로 재개한다. 호스트가 지원하면 수동 `/compact` 사용 가능. 에이전트가 압축 주기를 직접 바꾸지는 못한다.
- [정리 전 인수인계](history/SESSION-HANDOFF-before-context-cleanup-2026-10-16.md)는 필요할 때만 조회한다.
