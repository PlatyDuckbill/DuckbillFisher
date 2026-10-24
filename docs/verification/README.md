# 기능별 검증 근거

현재 대표 확인/미확인은 [STATUS](../STATUS.md)를 따른다. 필요한 기능 구역만 읽고 초기 환경·UI 수정 원문은 [과거 기록](../archive/README.md)에서 찾는다. 아래 파일은 당시의 근거이며 대기/다음 작업을 새 실행 지시로 해석하지 않는다.

## 배포와 호환성

- [0.5.3.0 입질·터주 효과음/필터 저장·설정 정리 배포](releases/RELEASE-0.5.3.0.md)
- [0.5.2.0 복수 시전 알림·루어 경과·수집 백업 배포](releases/RELEASE-0.5.2.0.md)
- [코드 구조·상수·설정 분리와 핵심 동작 테스트](ARCHITECTURE-TESTS.md)
- [설정 JSON/ZIP 백업·영어 고정 이름의 전용 WAV 개발본](configuration/SETTINGS-BACKUP.md)
- [어종별 메모·수동 도전 횟수·타이머 프리셋 연결/백업 검수본](configuration/FISH-PERSONAL.md)

- [0.5.1.0 타이머 효과·시간 필터 배포](releases/RELEASE-0.5.1.0.md)

- [첫 커스텀 저장소 배포: 0.2.4.0](releases/RELEASE-0.2.4.0.md)
- [0.3.0.0 배포](releases/RELEASE-0.3.0.0.md)
- [0.3.8.0 배포](releases/RELEASE-0.3.8.0.md)
- [0.3.9.0 배포](releases/RELEASE-0.3.9.0.md)
- [0.3.10.0 배포](releases/RELEASE-0.3.10.0.md)
- [0.3.11.0 배포](releases/RELEASE-0.3.11.0.md)
- [0.4.0.0 · 수집 현황](releases/RELEASE-0.4.0.0.md)
- [0.4.1.0 · 낚시 기록/최근 타이머](releases/RELEASE-0.4.1.0.md)
- [0.5.0.0 · 출현 일정표](releases/RELEASE-0.5.0.0.md)
- [ENV-03 한섭 수첩 API 조사](native/FISH-API.md)
- [ENV-03 선택 어종 수집 비트 읽기](native/FISH-NATIVE.md)
- [ENV-03 수첩 조회의 로그인·캐릭터 경계](native/FISH-SESSION.md)
- [ENV-03 수첩 열림·어종 매핑 진단](native/FISH-WIN.md)
- [KR Dalamud 15.0.3.7 호환성: 0.2.5.0](native/NATIVE-RUNTIME-15.0.3.7.md)
- [0.2.6.0: 동일 버전의 dal4kr 런타임 지원](native/NATIVE-RUNTIME-DAL4KR.md)

## 미끼 준비와 데이터

- [미끼 준비 · 0.3.0.0 개발 확인](bait/BAIT-PREPARATION.md)
- [전체 원본 도입과 일부 조건 표시](data/ALL-SOURCE-DATA.md)
- [일반 낚시 미확인 자료 보완 · 0.2.9.0](data/DATA-COMPLETION-2026-10-18.md)
- [전체 데이터 확대 전 누락 점검](data/DATA-COVERAGE-REVIEW.md)
- [신생 에오르제아 상세 조건 확대](data/DATA-EXPANSION.md)
- [MVP-01 상세 정보와 지도 핀 / 초기 출현 예측](data/DETAILS.md)
- [일반 낚시 데이터 보완 후 미확인 목록](data/reports/fishing-data-2026-10-17/remaining.md)

## 타이머·기록

- [MVP-02 KR 캐스팅 관찰 첫 구현](fishing/CASTING-PROBE.md)
- [MVP-02 낚시 기록 관리 개선](fishing/HISTORY-MANAGEMENT.md)
- [낚시 기록 저장·복구 안정성: 0.2.7.0](fishing/HISTORY-STABILITY.md)
- [MVP-02 생미끼·실패 기록과 메인 바로가기](fishing/MOOCH-FAILURE.md)
- [보관 생미끼 실제 사용 어종: 0.2.8.0](fishing/SWIMBAIT.md)
- [MVP-02 캐스팅 타이머·공유 히스토리 첫 구현](fishing/TIMER-HISTORY.md)

## 수집 판정

- [수집됨 자동 반영: 수첩 조회와 새 어종 등록](collection/COLLECTION.md)
- [게임 도감 목록과 대조한 자동 미수집](collection/GUIDE-SNAPSHOT.md)

## 예측·알림

- [시간·날씨 계산과 현재 웹 표시 대조](forecast/FORECAST.md)
- [열린 창 배경과 바다구렁이 예측 차이 조사](forecast/OPEN-WINDOW-DIAGNOSTICS.md)
- [MVP-03 즐겨찾기 뷰·아이템 링크·전체 채널·한 줄 기록](alerts/ALERT-CATALOG-VIEW.md)
- [MVP-03 알림 관리와 즐겨찾기 스크롤 수정](alerts/ALERT-MANAGEMENT.md)
- [커스텀 WAV 알림 소리: 0.3.8.0](alerts/CUSTOM-SOUND.md)
- [MVP-03 알림 채널과 시간 표시 간소화](alerts/ALERT-SIMPLIFICATION.md)
- [MVP-03 즐겨찾기 관리·알림 대상과 목록 기본값](alerts/FAVORITE-POLICY.md)
- [MVP-03/F05 출현 사전 알림 첫 구현](alerts/SPAWN-ALERTS.md)

## 목록·필터·화면

- [목록 설정 저장·기본값 복원·도감 안내](catalog/CATALOG-SETTINGS.md)
- [MVP-01 분류 필터와 수집 선택 정리](catalog/CLASSIFICATION-FILTERS.md)
- [MVP-01 현재 지역 필터](catalog/CURRENT-REGION.md)
- [짧은 진단과 먼바다 필터](catalog/DIAGNOSTICS.md)
- [0.1.36.0 전체 보기·사용자 필터 프리셋](catalog/FILTER-PRESETS.md)
- [MVP-01 지역·확장팩 복수 선택](catalog/GEOGRAPHY-FILTERS.md)
- [MVP-01 지역 선택 UI 개선](catalog/GEOGRAPHY-PICKER-UI.md)
- [0.1.33.0 연동 안내의 어류도감 열기](catalog/GUIDE-OPEN.md)
- [MVP-01 첫 어종 목록](catalog/LIST.md)
- [즐겨찾기·수동 수집 상태와 필터](catalog/PREFERENCES.md)
- [MVP-01 남은 시간 정렬](catalog/REMAINING-TIME-SORT.md)
- [목록·상세·화면 검증 요약](catalog/UI.md)
