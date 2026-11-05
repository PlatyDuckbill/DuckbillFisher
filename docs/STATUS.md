# 현재 상태

갱신: 2026-11-05 · 공개0.8.0.0/API15·schema25 · KR 게임2026.10.06 · 글로벌 게임2026.10.16

## 현재 배포

- **공개0.8.0.0**: 직감 준비·터주 회차 기록·필터 외 미끼·어종 상세 접근·복수 어획 수량 안내/기록을 추가했다. 소스dc3731f, Plogon run42029321554 성공, 최소 지원 참조 ZIP7파일·한영1,034키 검사 후 Release435190222를 게시했다. 익명 ZIP/체크섬/태그 아이콘 HTTP200·해시 일치. 설치 JSON·공용 목록 동기화와 실제 공유 raw URL의0.8.0.0/API·주소·내역 일치를 확인했다. 사용자 지시로 **실기는 배포 후 확인 예정**이며 새 기능·실제 업데이트 확인은 대기다. [근거](verification/releases/RELEASE-0.8.0.0.md).
- **이전 공개0.7.0.0**은 글로벌 클라이언트 지원판이다. 태그 소스5b45e2b, Plogon 점검 run41855137474 성공, 최소 지원 참조 Release 경고0/오류0, 추출7파일/한영1,013키 검사 후 같은 ZIP을 Release434089212에 게시했다. 익명 자산 SHA256 일치, 설치 JSON 반영. DuckbillRepo 공용 `pluginmaster.json` 동기화와 실제 공유 raw URL 확인 완료. [근거](verification/releases/RELEASE-0.7.0.0.md). 후보 ZIP의 인게임 확인 없이 사용자 지시로 게시했다(개발본 KR·글로벌 대표 확인은 완료). **KR은 실제 Dalamud 업데이트·업데이트 내역 표시 정상(2026-11-05 사용자 보고), 글로벌은 확인 대기**다.
- 이전 공개판 **0.6.0.1**은 dal4kr 15.0.3.9 런타임 지원 배포다. 실제 Dalamud 업데이트 확인은 하지 않은 채 0.7.0.0으로 넘어갔다. [근거](verification/releases/RELEASE-0.6.0.1.md).
- 기능 기준판 **0.6.0.0**은 고정 Release DLL 대표 정상 확인 뒤 Release423892077로 게시했고 실제 공유 raw URL 일치까지 확인했다. [근거](verification/releases/RELEASE-0.6.0.0.md).
- 실제 Dalamud 저장소 업데이트 확인 완료는 0.5.3.0·0.4.1.0이다. 그 밖의 버전별 소스/해시/게시 단계는 [검증 색인](verification/README.md#배포와-호환성)을 따른다.
- 설치 URL: https://raw.githubusercontent.com/PlatyDuckbill/DuckbillRepo/main/pluginmaster.json (DuckbillFisher·DuckbillTweak 공용). 명령은 /dbfs·/duckbillfisher, cl/compact·ct/timer다.

## 현재 기능 요약

- **0.8.0.0 추가 기능**: 터주 회차 자동 집계/수동 보정, 필터 대상 외 소지 미끼·직감 선행 미끼, 이름 명령·물고기 전용 우클릭 상세, 일반21종의 이중/삼중 안내, 타이머 직감 진행도다. KR·글로벌 빌드/자료·리소스/계약 검사와 실기를 구분하며 **실기 확인 대기**다. [회차](verification/configuration/FISH-PERSONAL.md#터주-회차-자동-집계-개발본-2026-11-05)·[직감/수량](verification/fishing/TIMER-HISTORY.md#직감-준비복수-어획-수량-개발본-2026-11-05).

기능·기본값·저장 정책은 [PRODUCT](product/PRODUCT.md), 구조는 [ARCHITECTURE](development/ARCHITECTURE.md)가 담당한다.

- 메인 메뉴: 어종 목록/즐겨찾기 관리/추천/출현 일정표/수집 현황/낚시 기록/설정. 모든 메인 탭에 공통 도감 미대조 경고를 표시한다.
- 추천(0.6.0.0): **지금 노릴 어종 / 밑작업 / 출현 드문 순**, 기간1~24시간·수동 갱신. 준비 완료·어획 가능 여부와 준비 시간·점수·보관 최적화는 범위 밖이다. [계약](product/NEXT-FISH-RECOMMENDATION.md#0600-구현-계약)·[검사](verification/forecast/RECOMMENDATIONS.md).
- 현재 낚시터 알림(0.6.0.0): 기본ON, 첫 투척 뒤 출현 중 대상 포함, 같은 회차 중복 방지·즐겨찾기0분과 중복 방지. 세 경로의 로컬 채팅 형식을 통일했다. [계약](verification/alerts/SPAWN-ALERTS.md#현재-낚시터-출현-알림-0600-2026-10-25).
- 개인 기록·백업(0.5.4.0): 어종별 메모·도전 횟수·프리셋 연결, JSON/ZIP 공통 설정 백업·전용 WAV. [개인 기록](product/FISH-PERSONAL.md)·[백업](product/SETTINGS-BACKUP.md).
- 타이머(0.5.1~0.5.3): 복수 시전 알림·이름 있는 프리셋·인내/직감/루어 경과, 입질·터주·터주왕 개별 SE. [CASTING-TIMER](product/CASTING-TIMER.md).
- 출현 일정표(0.5.0.0): 시간축·월 달력. [SPAWN-SCHEDULE](product/SPAWN-SCHEDULE.md).

## 대표 확인과 기능 경계

| 영역 | 확인한 범위 | 근거 |
|---|---|---|
| 목록·필터·UI | 검색/정렬/즐겨찾기·수동 상태 저장, 지역/확장팩·등급/시간제 조합, 남은 시간순/출현 중 배경 | [UI](verification/catalog/UI.md), [필터](verification/README.md#목록필터화면) |
| 수집 | 도감1,517종 전체 대조/불일치0, 재접속 후 재조회 복구, 도감 닫힌 상태의 새 등록/자동 숨김 | [대조](verification/collection/GUIDE-SNAPSHOT.md), [등록](verification/collection/COLLECTION.md) |
| 알림 | 로컬 채팅·선택SE, 전체/미수집만·개별 시간·신규 등록 자동 해제 | [알림](verification/README.md#예측알림) |
| 타이머·기록 | 낚아채기 정지·완전 접기 후 현재 기록 비움, 요약/상세/삭제·복원·최신순·재등장 방지 | [저장](verification/fishing/HISTORY-STABILITY.md), [최근 타이머](verification/fishing/TIMER-HISTORY.md) |
| 미끼 | 일반 생미끼·실패/빈 결과, 보관 생미끼 첫 칸, 준비/수량·찾기 표식/강조 | [생미끼](verification/fishing/MOOCH-FAILURE.md), [보관](verification/fishing/SWIMBAIT.md), [준비](verification/bait/BAIT-PREPARATION.md) |
| 추천·개인 기록·백업 | 고정 Release 대표 정상 보고 | [추천](verification/forecast/RECOMMENDATIONS.md), [개인 기록](verification/configuration/FISH-PERSONAL.md), [백업](verification/configuration/SETTINGS-BACKUP.md) |
| KR·글로벌 지원 | 0.7.0.0부터 전역 DLL 가드 없이 기능별 실행 계약을 쓴다. 개발본으로 글로벌 대표 확인과 KR 회귀 대표 확인을 마쳤다(2026-11-04). 0.6.0.1 이하는 정확한 wasdcxz/dal4kr 모듈 조합 가드다 | [확인 기록](verification/native/GLOBAL-COMPAT.md#글로벌-인게임-대표-확인-2026-11-04-사용자-보고), [이전 가드](verification/native/NATIVE-RUNTIME-DAL4KR.md) |

대표 정상 보고를 모든 어종/테마/언어·폭/캐릭터/장애·재로드·저장 실패 조합의 전수 확인으로 확대하지 않는다. 관리 코드 테스트는 게임·네이티브·UI 실기를 대신하지 않는다. 공개 API 우선·네이티브 최소화·기능별 실행 계약 방침은 [ARCHITECTURE](development/ARCHITECTURE.md#호환성과-실패)를 따른다.

## 데이터와 예측 현황

- 정적 목록2,512행/표시 가능2,465/확인 불가47. dataset kr-regular-2026-10-17은 일반 상세1,258/1,258 연결, 시간/날씨 전체 확인1,256·부분 확인2종이다. 상세 연결은 모든 필드 완성을 뜻하지 않는다.
- 핵심 잔여11종: 직감 지속 시간9종(175초 가정·원본null 보존), 이크에바지 가위/요에콰 먹물머리의 낚아채기·시간/날씨 완전성. 부분 확인2종은 Unknown이다.
- 물고기의 눈 true529/false374/null355. null355종에는 자체ET 제한이 없어 ON으로 Unknown이 추가되지 않는다. [예측/원본 비교 한계](verification/forecast/FORECAST.md#0350-직감-기본값과-계산-구역-개선-2026-10-19).
- 조건1,258종/등급335종의 고정 원본·설치 클라이언트 재생성·내장 해시 대조 완료. 자료 판독 테스트 통과는 모든 어종 예측값의 독립 대조가 아니다. 먼바다259종 조건 연동·작살 미확인115건은 별도 후속이다. [검사/한계](verification/data/DATA-COMPLETION-2026-10-18.md), [잔여 이름/ID](verification/data/reports/fishing-data-2026-10-17/remaining.md), [재생성](../data/README.md).

## 다음 한 작업과 미확인

다음 한 작업: **Dalamud에서0.8.0.0 업데이트 후 직감 선행 어종의 일반/소장품 수령·선택 대기 중 진행 유지·종료 후 재집계를 확인한다.** 터주 회차 중복 방지·타이머 폭과 공개0.7.0.0 글로벌 업데이트 확인 대기는 유지한다.

배포 전 코드 재점검에서 소장품 선택 단계의 어획 누락·일시적 낚시터 식별 대기/무관한 소장품 수령 실패의 직감 초기화·수령 대기 중 터주 횟수 증가·목록 로드 전 검색 무반응을 보완했다. [정적 검토·실기 경계](verification/fishing/TIMER-HISTORY.md#배포-전-기능로직-재점검-개발본-2026-11-05).

이름 명령 검색·채팅 물고기 링크 상세 메뉴는 사용자 대표 정상 확인(2026-11-05). 다른 아이템/플레이어 제외 등 경계는 [점검](verification/catalog/UI.md#채팅-물고기-링크-우클릭-누락-보완-2026-11-05)에 남는다. 일반 수량은 재조사 후21종/이중21·삼중10으로 보강했고 직감 선행43종 중15종만 포함한다. [출처·한계](development/DATA-SOURCES.md#일반-낚시-이중삼중-수량-안내-2026-11-05). 소장품 가능 어종도 본인 어획+실제 소지품 수령량/시점으로 집계하며 [실기 대기](verification/fishing/TIMER-HISTORY.md#소장품-가능-어종의-직감-집계-제외-수정-개발본-2026-11-05)다.

진행 중 장기 작업: KR·글로벌 범용 지원과 공식 저장소 등록. 공식 저장소 제출 준비는 사용자 결정으로 후순위다(2026-11-04). 우리 작업용 문서 정리는 186d209로 마쳤고, 구조 문서는 큰 문제가 없어 그대로 둔다. 단계와 진행도는 [계획](product/GLOBAL-AND-OFFICIAL-REPO.md)에서만 관리한다.

1. 0.6.0.0·0.5.4.0·0.5.2.0·0.5.1.0·0.5.0.0·0.3.9.0의 실제 저장소 업데이트 확인은 대기다. 새 버전 확인으로 이전 버전 확인을 대체하지 않는다.
2. 추천 활용·첫 투척/반복·다음 회차/0분 중복, 알림 채널/링크/음원 조합, 백업·개인 기록의 장애/경계는 실기 대기다. [UI](verification/catalog/UI.md)·[알림](verification/alerts/SPAWN-ALERTS.md#0600-알림-경계-점검-2026-10-26)·[백업](verification/configuration/SETTINGS-BACKUP.md).
3. 친구 PC·다른 캐릭터 격리는 미확인이다. 다른 캐릭터는 사용할 캐릭터가 없어 보류했다. 글로벌은 개발본 대표 확인만 했고, 공개판 0.7.0.0 설치본의 확인은 아직이다.
4. 모든 테마·보관 생미끼 다른 칸·좁은 폭/한영·저장 장애/절전/상한, 데이터 대표 화면과 외부 좌표 차이4곳은 별도 확인 대상이다. 커스텀 WAV 범위는 [기록](verification/alerts/CUSTOM-SOUND.md)을 따른다.
5. 사용자 보류: 입질 초 통계. 0.8.0.0 복수 어획·직감 진행도 실기, 소장품 수령 시점·일반 어종 마릿수 전수 자료, 직감 반영 구간의 실제 어획·나미타로 자체 날씨 조건은 근거가 필요하다. 후속 후보는 [ROADMAP](product/ROADMAP.md)을 따른다.
