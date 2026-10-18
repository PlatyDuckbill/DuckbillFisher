# 현재 상태

갱신: 2026-10-19 · 공개 0.3.0.0 · 개발 0.3.2.0 데이터 안정성·30일 예측

## 현재 배포와 작업

- 공개 버전은 **0.3.0.0 / API15**다. Release 추출본 사용자 대표 확인 후 같은 ZIP을 게시하고 익명 다운로드·해시·설치 JSON을 확인했다. [배포 근거](verification/releases/RELEASE-0.3.0.0.md).
- 커스텀 저장소: https://raw.githubusercontent.com/PlatyDuckbill/DuckbillFisher/main/repo/pluginmaster.json
- 0.2.6.0에서 본인·친구 PC의 설치/업데이트·도감·수집·타이머 대표 확인 완료. **0.3.0.0 실제 저장소 업데이트/친구 새 기능 확인은 별도 대기**다.
- 개발 **0.3.2.0**: 조건/등급 공통 검증과 오류 격리, 조건 해시·등급 연결, 날씨 확률표 불일치 Unknown 처리를 보강했다. 조건/등급 원본은 그대로이며 잘못된 값을 기본값으로 보완하지 않는다. [근거·재현](verification/data/DATA-COMPLETION-2026-10-18.md#0320-데이터-안정성-보강).
- 출현 검색은 실제 30일로 확대하고 목록/상세에 물고기의 눈 미사용 안내를 추가했다. 실제1258종 계산에서 7일 밖의 6종을 찾았고 Unknown은 원래 부분 확인2종뿐이다. [계산·남은 실기](verification/forecast/FORECAST.md#0320-검색-범위-확대-2026-10-18). 바다구렁이 재조사는 같은 입력의 양쪽 계산이 일치하며 당시 Teamcraft 화면 차이 원인은 미확정이다. [근거](verification/forecast/OPEN-WINDOW-DIAGNOSTICS.md#0320-재조사-2026-10-18).
- 10-19 현재 웹 실제 표시와 Core DLL을 대조했다. Tracker의 KR 공통446종 중 441종은 일치/0.048초 이내, 5종은 직감 선행 조건 반영 범위 차이다. Teamcraft 12종 표본에서는 나미타로의 날씨 조건 차이가 확인됐다. 현재 바다구렁이·장기 대기 표본은 일치한다. [UTC·CSV·비교 한계](verification/forecast/FORECAST.md#현재-웹-표시-대조-2026-10-19). 제품 코드는 변경하지 않았다.
- 실제 자료 검사·고정 원본/설치 클라이언트 재생성 대조에서 조건1258종/등급335종과 원본의 LF 정규화 일치를 확인했다. 직접 빌드·문서/리소스 검사는 완료, 테스트·인게임·패키징·게시 없음. 이전 환경/호환성 정비의 실기와 글로벌 런타임도 미확인이다. [이전 범위](verification/native/NATIVE-RUNTIME-DAL4KR.md#0310-개발-정비-2026-10-18).
- 환경/등록 DLL/참조 경로는 [HANDOFF](SESSION-HANDOFF.md), 작업 절차는 [WORKFLOW](development/WORKFLOW.md), 전체 탐색은 [문서 지도](README.md)를 따른다.

## 대표 확인과 기능 경계

| 영역 | 확인한 범위 | 근거 |
|---|---|---|
| 목록·필터·UI | 검색/정렬/즐겨찾기·수동 상태 저장, 지역/확장팩·등급/시간제 조합, 남은 시간순/출현 중 배경 대표 확인 | [UI 확인 요약](verification/catalog/UI.md), [필터 근거](verification/README.md#목록필터화면) |
| 수집 | 도감 1,517종 전체 대조/불일치 0, 재접속 초기화→도감 재조회 복구, 도감 닫힌 상태 새 등록 반영/자동 숨김 | [수집 판정](verification/collection/GUIDE-SNAPSHOT.md), [등록](verification/collection/COLLECTION.md) |
| 알림 | 로컬 채팅·선택 SE, 전체/미수집만·개별 시간·신규 등록 자동 해제 정책 | [알림 기록](verification/README.md#예측알림) |
| 타이머·기록 | 낚아채기 경과 정지·완전 접기 후 현재 기록 비움, 요약/상세/삭제, 저장 복원·최신순·재등장 방지 대표 확인 | [기록 안정성](verification/fishing/HISTORY-STABILITY.md) |
| 미끼 관찰 | 일반 생미끼 Item·명시적 실패·빈 결과 아이콘, 보관 생미끼 첫 칸 Item 기록 대표 확인 | [생미끼/실패](verification/fishing/MOOCH-FAILURE.md), [보관 생미끼](verification/fishing/SWIMBAIT.md) |
| 미끼 준비·찾기 | 첫 미끼/수량·독립 준비 화면·미확인 목록·게임 UI 칸/페이지 표식·강조의 Release 대표 확인 | [기능 근거](verification/bait/BAIT-PREPARATION.md), [Release](verification/releases/RELEASE-0.3.0.0.md) |
| KR 지원 | 정확한 wasdcxz/dal4kr 모듈 조합 가드. 동일 숫자 버전도 배포판별 대조 | [친구 환경](verification/native/NATIVE-RUNTIME-DAL4KR.md) |

확인 범위는 표본이다. 모든 어종/테마/언어·폭/캐릭터/장애 조합의 전수 확인으로 확대하지 않는다. 현재 사용자 동작·기본값·수집/알림/저장 정책은 [PRODUCT](product/PRODUCT.md)에 있다.

## 데이터 현황

- 정적 목록 2,512행 / 표시 가능 2,465 / 확인 불가 47.
- dataset kr-regular-2026-10-17: 일반 상세 **1,258/1,258** 연결, 시간/날씨 전체 확인 1,256종·부분 확인 2종. 상세 연결은 모든 필드 완성을 뜻하지 않는다.
- 핵심 잔여 11종: 직감 지속 시간 9종, 이크에바지 가위/요에콰 먹물머리의 낚아채기·시간/날씨 완전성. 부분 확인 어종은 상시 예측하지 않는다.
- 추가 355종 물고기의 눈 지원은 근거 없어 null이며 UI 미표시. 먼바다 259종 조건 연동·작살 미확인 115건은 별도 후속이다.
- 데이터 재생성/기존 알려진 값 보존·내장 해시·KR 지도 연결 대조는 완료했다. 0.2.9.0 데이터 보완의 별도 대표 화면 확인 대기는 유지한다.
- [검사/한계](verification/data/DATA-COMPLETION-2026-10-18.md), [전체 잔여 이름·ID·이유](verification/data/reports/fishing-data-2026-10-17/remaining.md), [재생성](../data/README.md).

## 다음 한 작업과 미확인

1. 개발 0.3.2.0 DLL 재로드 후 목록/상세의 미사용 안내·장기 출현 날짜·한영 전환·출처 및 기존 수동 상태 보존 확인. 이전 도감/수집/타이머 대표 확인과 공개 0.3.0.0 실제 업데이트/친구 새 기능 확인도 별도 대기다.
2. 다른 캐릭터 격리는 사용할 캐릭터가 없어 실기 보류. 모든 테마·보관 생미끼 다른 칸·신규 등록/작살 전환·좁은 폭/한영·장애/절전/상한은 별도 확인 대상이다.
3. 데이터 대표 화면과 외부 좌표 차이 4곳도 미확인이다. 정확한 근거가 생기면 잔여 데이터를 보완한다.
4. 사용자 보류: 복수 어획·수량, 입질 초 통계. 과거 Teamcraft 화면 차이 추적은 사실 자료 부족으로 중단하고 현재 웹 비교로 전환했다. 확인된 직감 범위/나미타로 조건 차이는 후속 정책·게임 근거 검토 대상이다.
5. 터주작 후보·먼바다/작살·장기 목표는 [ROADMAP](product/ROADMAP.md)을 따른다. 새 글로벌 런타임 프로필은 실제 게임/DLL 계약과 데이터 검증 후 별도 도입한다.
