# 0.4.1.0 배포 후보

2026-10-20. 사용자 요청의 기록/타이머/진행 막대 개선을 구현했다. 코드·자료 검사는 완료했고 고정 추출 DLL의 새 동작 실기와0.4.1 게시 전이다. 이전0.4.0의 최종 추가 실기 생략 지시를 새 기록/타이머 동작의 확인 완료로 확대하지 않는다.

## 범위와 검사

- 기존 검증한 발견/본인 어획 상관 계약으로 이미 종료된 성공 시전과 비어 있는 묶음 위치를 갱신한다. 임의의 다른 과거 미확인 기록은 추정 보정하지 않는다.
- 기록 목록/상세의 너비 조절, 첫 관측 시전부터 종료까지의 총 시간, 날짜/시작 분리, 실제 어종/미끼 아이콘과 결과 색/결과 경과를 추가했다. 확인된 생미끼와 직전 성공 어획이 일치할 때만 연결 표식을 표시한다.
- 사용자 응답대로 중단 후 지역 이동에도 최근 타이머를5분 유지한다. 새 낚시/로그아웃/캐릭터 전환/재로드/삭제/만료 시 미리 보기를 교체하거나 비운다. 저장 기록을 만료로 삭제하지 않는다. 그래프 톤은 청색·청록·녹색 계열이다. [판정·재현](../fishing/TIMER-HISTORY.md#0410-기록-위치시간최근-타이머-2026-10-20), [영어 요약](../../releases/0.4.1.0.md).
- 직접 별도 `.cache/history-041-build/`→정규 Debug와 최소 지원 참조 `.cache/compat-15.0.3.6/full/` Release를 빌드했다. 모두 경고0/오류0, 한영748키·형식 인자 검사 통과. 테스트 추가/실행 없음. SDK/lock 파일·네이티브 계약·설정schema15는 유지한다. 기록schema2의 DurationSeconds/MoochSourceId는 기본null의 선택 필드다. 진단timer/v5.
- `scripts/prepare-release.ps1`의 ZIP 허용 목록/필수 파일/API/버전 검사를 통과했다. 추출7파일 모두 Release 출력과 SHA256 일치, Plugin/한국어 DLL0.4.1.0·API15·csproj/manifest/후보 JSON의 Changelog 일치, 내장 조건/등급/THIRD-PARTY-NOTICES의 원본 해시 일치. 결과 `.cache/release-build/0.4.1.0-validation.json`.

## 고정 산출물

- ZIP: `artifacts/share/0.4.1.0/DuckbillFisher-0.4.1.0.zip`, **440,437바이트**.
- ZIP SHA256: **96BBF7300D5B72225E758CB4B051EB5DBE62DE629E24F7AF6028BDFC5E68D8D5**.
- 추출 DLL: `${env:DUCKBILL_ROOT}/.cache/release-validation/0.4.1.0/DuckbillFisher.dll`.
- DLL SHA256: **B8EFF82D2CC6C9DEEF16A34FC045C5793BDA3827127218A0BAA322940AF3E2E6**.

## 게시 상태와 남은 범위

- 0.4.1.0 태그/Release/설치 JSON 게시는 하지 않았다. 확인 순서는 위 재현 문서이며 고정 DLL의 새 동작 결과를 확인한 뒤 같은 ZIP을 게시한다. 기존 공개/설치 목록은0.4.0.0을 유지한다.
- 별도 선행 작업으로 사용자의 임시101faff를 요청대로dffe297로 amend하고 원격101faff를 조건으로 `--force-with-lease` push했다.0.4.0 설치 JSON의 Changelog/LastUpdate를 고정 후보와 일치시켰으며, 실제 공유 URL에서2026-10-20T11:50:53Z HTTP200/메타데이터 일치를 확인했다.0.4.0 소스 태그64c71c0/Release 자산은 바꾸지 않았다.
- 최종 DLL 인게임·실제 저장소 업데이트/친구 PC·다른 캐릭터·모든 테마/배율/폭·5분 경계/절전·저장 장애/상한 검사는 미확인이다.
