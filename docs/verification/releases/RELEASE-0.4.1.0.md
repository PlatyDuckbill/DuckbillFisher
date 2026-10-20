# 0.4.1.0 배포 기록

2026-10-20. 사용자 요청의 기록/타이머/진행 막대 개선을 구현했다. 코드·자료 검사를 마친 r3 고정 추출 DLL에 사용자 ‘잘 작동 중. 이제 배포 진행’ 응답을 받았고 같은 ZIP을 재빌드 없이 게시했다. 대표 확인과 전수 확인을 구분한다.

## 범위와 검사

- 기존 검증한 발견/본인 어획 상관 계약으로 이미 종료된 성공 시전과 비어 있는 묶음 위치를 갱신한다. 임의의 다른 과거 미확인 기록은 추정 보정하지 않는다.
- 기록 목록/상세의 너비 조절, 첫 관측 시전부터 종료까지의 총 시간, 날짜/시작 분리, 실제 어종/미끼 아이콘과 결과 색/결과 경과를 추가했다. 확인된 생미끼와 직전 성공 어획이 일치할 때만 위아래 행을 세로선·화살표로 잇는다. 긴 연결 문구는 행 툴팁에만 둔다. 새1110 없는 연속 생미끼의 검증된 연결에서는 직전 성공 어획의 확인된 낚시터를 타이머/목록·새 시전 기록에 이어받는다.
- 사용자 응답대로 중단 후 지역 이동에도 최근 타이머를5분 유지한다. 새 낚시/로그아웃/캐릭터 전환/재로드/삭제/만료 시 미리 보기를 교체하거나 비운다. 저장 기록을 만료로 삭제하지 않는다. 그래프 톤은 청색·청록·녹색 계열이다. [판정·재현](../fishing/TIMER-HISTORY.md#0410-기록-위치시간최근-타이머-2026-10-20), [영어 요약](../../releases/0.4.1.0.md).
- 직접 별도 `.cache/history-041-r3-build/`→정규 Debug와 최소 지원 참조 `.cache/compat-15.0.3.6/full/` Release를 빌드했다. 모두 경고0/오류0, 한영748키·형식 인자 검사 통과. 테스트 추가/실행 없음. SDK/lock 파일·네이티브 계약·설정schema15는 유지한다. 기록schema2의 DurationSeconds/MoochSourceId는 기본null의 선택 필드다. 진단timer/v5.
- `scripts/prepare-release.ps1`의 ZIP 허용 목록/필수 파일/API/버전 검사를 통과했다. 추출7파일 모두 Release 출력과 SHA256 일치, Plugin/한국어 DLL0.4.1.0·API15·csproj/manifest/후보 JSON의 Changelog 일치, 내장 조건/등급/THIRD-PARTY-NOTICES의 원본 해시 일치. 결과 `.cache/release-build/0.4.1.0-r3-validation.json`.

## 고정 산출물

- ZIP: `artifacts/share/0.4.1.0-r3/DuckbillFisher-0.4.1.0.zip`, **441,490바이트**.
- ZIP SHA256: **79E3BBE6188D38DC5254E9DDEA90C2721979E1D0D1B340F6DCE5B6C1DD20998D**.
- 추출 DLL: `${env:DUCKBILL_ROOT}/.cache/release-validation/0.4.1.0-r3/DuckbillFisher.dll`.
- DLL SHA256: **4BF68642EC08A12015A1CF168F37D90AA61C2A6315103487E581BAA10AE0E827**.
- 생미끼 위치 이어받기를 고친 r3를 그대로 게시했으며 이전 `0.4.1.0/`·`0.4.1.0-r2/` 산출물과 검사 결과는 보존했다.

## 게시 상태와 남은 범위

- 사용자 대표 확인: r3 추출 DLL 안내 뒤 ‘잘 작동 중. 이제 배포 진행’ 응답을 받았다(배포 작업 시작2026-10-20T12:14:34Z). 직전 보고의 생미끼 중 낚시터 소실 수정과 현재 후보 동작의 대표 확인으로 기록하고 위 ZIP/DLL SHA256에 연결한다. 모든 어종/이벤트/폭/장애 조합을 개별 확인한 것으로 확대하지 않는다.
- [공개 Release](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.4.1.0): ID418362051, 태그v0.4.1.0/소스cc92e8986ceb6e1a74ec855691612ec5fe1c5fd2, 공개2026-10-20T12:16:00Z. ZIP/체크섬 업로드 크기와 서버 digest를 대조한 뒤 공개했다.
- 익명 ZIP/체크섬/태그 아이콘 HTTP200·원본 SHA256 일치를2026-10-20T12:16:09Z 확인했다. `.cache/release-remote-check/0.4.1.0/publish-state.json`과 `download-check.json`에 결과를 보관했다.
- 설치 JSON은 익명 파일 확인 후59e9886a9063066bdf05914e15712201a8cef10a로 커밋·push했다. API15/버전/Changelog/다운로드·아이콘 URL을 후보에 맞췄고 LastUpdate만 이번 배포 요청 무렵2026-10-20T12:14:34Z(1792498474)로 기록했다. 실제 공유 raw URL(쿼리 없음)에서2026-10-20T12:17:43Z HTTP200·버전0.4.1.0/API15·이름/설명/Changelog/설치·업데이트·아이콘 URL/LastUpdate의 로컬 일치를 확인했다. 결과는 `.cache/release-remote-check/0.4.1.0/public-index-check.json`과 `public-pluginmaster.json`에 있다.
- 별도 선행 작업으로 사용자의 임시101faff를 요청대로dffe297로 amend하고 원격101faff를 조건으로 `--force-with-lease` push했다.0.4.0 설치 JSON의 Changelog/LastUpdate를 고정 후보와 일치시켰으며, 실제 공유 URL에서2026-10-20T11:50:53Z HTTP200/메타데이터 일치를 확인했다.0.4.0 소스 태그64c71c0/Release 자산은 바꾸지 않았다.
- 실제 저장소 업데이트/친구 PC·다른 캐릭터·모든 테마/배율/폭·5분 경계/절전·저장 장애/상한 검사는 미확인이다.
