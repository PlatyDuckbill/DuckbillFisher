# 현재 상태

갱신: 2026-10-23 · 공개0.5.2.0/API15 · 실제 Dalamud 저장소 업데이트 확인 대기

## 현재 배포와 개발본

- 공개0.5.2.0은 복수 시전 알림·이름 있는 프리셋·루어 경과·캐릭터별 수집 백업, 구조 정비와 KR15.0.3.9 지원을 포함한다. 고정 추출본 대표 정상 확인 후 동일 ZIP 게시·익명 해시/아이콘·설치 JSON3871a41 확인. **실제 공유 raw URL0.5.2.0 일치·Dalamud 저장소 업데이트 별도 대기**다. 최소 참조 Release 경고0/오류0·한영884키·추출/내장 자료 검사 통과, 테스트 추가/실행 없음. [소스02061f4·Release ID421639798·고정 해시](verification/releases/RELEASE-0.5.2.0.md).
- 공개0.5.1.0은 인내·직감 타이머와 잔여/예정 시간 필터·빈칸 해제를 제공한다(schema19). 고정 추출 DLL 사용자 ‘정상’ 확인 뒤 같은 ZIP을 게시했다. 소스6e672e8/Release ID419790510·설치 JSONa8d5e89, 익명 ZIP/체크섬/아이콘과 실제 공유 raw URL의 버전/메타데이터 일치 확인 완료. **실제 Dalamud 저장소 업데이트는 확인 대기**다. [배포 근거](verification/releases/RELEASE-0.5.1.0.md).
- 사용자 합의로 공개 API 우선·네이티브 최소화·정확한 KR 조합 검사/업데이트별 대조 방침을 확정했다. 지원 참조의 핵심 네이티브 기능에 동등한 공개 대체는 찾지 못했으며, 이미 공개 API인 미끼 수량/버프의 검사 경계는 후속 후보로 남겼다. [조사/범위](verification/native/NATIVE-RUNTIME-DAL4KR.md#공개-api-우선-방침과-대체-조사-2026-10-23). 이번 반영은 문서만이며 기존 실기 대기는 유지한다.
- 공개0.5.0.0 일정표는 고정 추출본 대표 확인·원격 게시/실제 raw URL 대조 완료지만 **실제 Dalamud 저장소 업데이트는 별도 대기**다. [배포](verification/releases/RELEASE-0.5.0.0.md)·[동작](product/SPAWN-SCHEDULE.md).
- 공개0.4.1.0은 고정 추출본·실제 저장소 업데이트 대표 확인 완료다. [근거](verification/releases/RELEASE-0.4.1.0.md). 그 밖의 버전별 빌드/추출/게시 SHA와 확인 범위는 [검증 색인](verification/README.md#배포와-호환성)을 따른다.
- 커스텀 저장소: https://raw.githubusercontent.com/PlatyDuckbill/DuckbillFisher/main/repo/pluginmaster.json
- 명령은 /dbfs·/duckbillfisher, cl/compact·ct/timer다. 현재 기능·기본값·저장 정책은 [PRODUCT](product/PRODUCT.md), 구조/테스트 절차는 [ARCHITECTURE](development/ARCHITECTURE.md)·[WORKFLOW](development/WORKFLOW.md)에 둔다.

## 대표 확인과 기능 경계

| 영역 | 확인한 범위 | 근거 |
|---|---|---|
| 목록·필터·UI | 검색/정렬/즐겨찾기·수동 상태 저장, 지역/확장팩·등급/시간제 조합, 남은 시간순/출현 중 배경 대표 확인 | [UI](verification/catalog/UI.md), [필터](verification/README.md#목록필터화면) |
| 수집 | 도감1,517종 전체 대조/불일치0, 재접속 후 재조회 복구, 도감 닫힌 상태의 새 등록/자동 숨김 | [대조](verification/collection/GUIDE-SNAPSHOT.md), [등록](verification/collection/COLLECTION.md) |
| 알림 | 로컬 채팅·선택SE, 전체/미수집만·개별 시간·신규 등록 자동 해제 대표 확인 | [알림](verification/README.md#예측알림) |
| 타이머·기록 | 낚아채기 정지·완전 접기 후 현재 기록 비움, 요약/상세/삭제·복원·최신순·재등장 방지 대표 확인 | [저장](verification/fishing/HISTORY-STABILITY.md), [최근 타이머](verification/fishing/TIMER-HISTORY.md) |
| 미끼 | 일반 생미끼·실패/빈 결과, 보관 생미끼 첫 칸, 준비/수량·찾기 표식/강조 대표 확인 | [생미끼](verification/fishing/MOOCH-FAILURE.md), [보관](verification/fishing/SWIMBAIT.md), [준비](verification/bait/BAIT-PREPARATION.md) |
| KR 지원 | 정확한 wasdcxz/dal4kr 모듈 조합 가드, 같은 숫자 버전도 배포판별 대조 | [친구 환경](verification/native/NATIVE-RUNTIME-DAL4KR.md) |

위 표는 기존 공개판의 확인 범위다. 이번 개발본·모든 어종/테마/언어·폭/캐릭터/장애 조합으로 확대하지 않는다. 관리 코드 테스트는 게임·네이티브·UI 실기를 대신하지 않는다.

## 데이터와 예측 현황

- 정적 목록2,512행/표시 가능2,465/확인 불가47. dataset kr-regular-2026-10-17은 일반 상세1,258/1,258 연결, 시간/날씨 전체 확인1,256·부분 확인2종이다. 상세 연결은 모든 필드 완성을 뜻하지 않는다.
- 핵심 잔여11종: 직감 지속 시간9종(175초 가정·원본null 보존), 이크에바지 가위/요에콰 먹물머리의 낚아채기·시간/날씨 완전성. 부분 확인2종은 Unknown이다.
- 물고기의 눈 true529/false374/null355. 현재 null355종에는 자체ET 제한이 없어 ON으로 Unknown이 추가되지 않는다. 동일UTC 전체 계산 OFF/ON 모두 Unknown2종이며 변경은 세 날 범고래·채광 하트가오리다. [예측/원본 비교 한계](verification/forecast/FORECAST.md#0350-직감-기본값과-계산-구역-개선-2026-10-19).
- 조건1,258종/등급335종의 고정 원본·설치 클라이언트 재생성·내장 해시 대조 완료. 현재 자료 판독 테스트 통과는 모든 어종 예측값의 독립 대조가 아니다. 먼바다259종 조건 연동·작살 미확인115건은 별도 후속이다.
- 데이터 재생성·기존 알려진 값 보존·KR 지도 연결 대조 완료, 0.2.9.0 보완 자료의 별도 대표 화면 확인은 대기다. [검사/한계](verification/data/DATA-COMPLETION-2026-10-18.md), [잔여 이름/ID](verification/data/reports/fishing-data-2026-10-17/remaining.md), [재생성](../data/README.md).

## 다음 한 작업과 미확인

1. **공개0.5.2.0의 실제 Dalamud 저장소 업데이트를 확인한다. 실제 공유 raw URL0.5.2.0 일치는 확인했다.** 고정 추출본의 알림/프리셋·아이콘·루어·수집 백업 대표 확인은 완료했고 전수/장애·다른 캐릭터 확인으로 확대하지 않는다. 위 배포 기록을 따른다. 기존15.0.3.9 [호환성·도감/수집·시전/기록·미끼/SE](verification/native/NATIVE-RUNTIME-DAL4KR.md#15039-대응과-반복-검사-2026-10-23)와 구조 정비의 기존 설정·프리셋·한영·기록 상세/삭제/재로드도 계속 대기다. [기대 결과](verification/ARCHITECTURE-TESTS.md#한계와-대표-재확인).
2. 공개0.5.1.0/0.5.0.0의 실제 저장소 업데이트, 이전0.3.9.0 실제 업데이트/친구 새 기능 확인 대기는 유지한다. 새 개발본/고정 추출본의 확인으로 대체하지 않는다.
3. 친구 PC·이전 예측/필터/미끼/성능/저장 장애·다른 캐릭터 격리·글로벌 런타임은 미확인이다. 다른 캐릭터는 사용할 캐릭터가 없어 실기 보류다. 저장 자동 테스트의 임시 캐릭터 키 분리는 실제 캐릭터 전환 확인이 아니다.
4. 모든 테마·보관 생미끼 다른 칸·신규 등록/작살 전환·좁은 폭/한영·장애/절전/상한, 데이터 대표 화면과 외부 좌표 차이4곳은 별도 확인 대상이다. 커스텀 WAV는 대표 확인과 장애/실제 가청 범위를 [기록](verification/alerts/CUSTOM-SOUND.md)에서 구분한다.
5. 예측 비동기화·설정/기록 저장 정책·요약 렌더링은 변경하지 않았다. snapshot 재사용/알림 재계획의 성능·실기 대기는 [UI](verification/catalog/UI.md#0370-변경-없는-데이터의-재사용-2026-10-19)·[알림](verification/alerts/SPAWN-ALERTS.md#0360-알림-예약-결함-수정-2026-10-19)을 따른다.
6. 사용자 보류는 복수 어획·수량과 입질 초 통계다. 과거 Teamcraft 화면 차이 추적은 사실 자료 부족으로 중단했다. 직감 반영 구간의 실제 어획·나미타로 자체 날씨 조건은 게임 근거가 남아 있다.
7. 터주작 후보·먼바다/작살·장기 목표는 [ROADMAP](product/ROADMAP.md)을 따른다. 새 글로벌 프로필은 정확한 게임/DLL 계약·자료·실기 검증 후 별도 도입한다.
