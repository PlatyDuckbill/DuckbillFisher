# 0.3.11.0 배포

2026-10-20. 사용자가 개발0.3.11.0의 ‘기능 전부 잘 동작했음’을 보고했다. 이후 노트의 표시 방식/설정 문구 변경 설명을 삭제하고 이력 상한을10개로 늘려 배포하라고 지시했다. 추가 테스트 없이 진행하라는 지시에 따라 새 인게임 확인/자동 테스트/별도 Debug 재빌드는 하지 않았다. 최종 Release 패키지의 파일/리소스/자료 검사는 별도로 완료했다. 개발 DLL의 대표 확인과 최종 Release ZIP의 자료 검사를 구분한다.

## 범위와 검사

- UI 배율80~200%/100% 복원, 간이 목록 전용 즐겨찾기만 보기, 메인 상세의 같은 줄 별도 화면 버튼, 수집 상태 배지, 새 시전의 이전 위치 무효화/미지 표시/본인 어획과 발견 상관 갱신, 어종 보기 토글을 포함한다. [영어 릴리스 요약](../../releases/0.3.11.0.md), [UI 근거](../catalog/UI.md#03110-목록상세-조작-개선-2026-10-20), [낚시터 근거](../fishing/TIMER-HISTORY.md#03110-미지의-낚시터와-이전-위치-분리-2026-10-20).
- 노트는 실제 기능6항목만 유지하며 표시 방식/설정 문구 수정 설명2항목은 삭제했다. 최대10개 이력을 표시하고 현재는 신규 포함8개다. 확인된 날짜는 한국 시간의 시 단위까지 표시하며0.3.11.0 자체 게시 시각은 사전에 추측하지 않았다.
- 설정schema15/타이머 진단timer/v4이며 SDK/의존성·KR 네이티브 조합 가드는 유지한다. 사용자 확인 개발 소스는af95a7e이며 마지막 수정은 노트 내용/이력 상한뿐이다.
- 최소 지원 참조 `.cache/compat-15.0.3.6/full/`로 직접 Release 빌드 경고0/오류0. 한영687키/형식 인자 검사 통과. `scripts/prepare-release.ps1`의 ZIP 허용 목록·필수 파일·manifest/API/버전 검사를 통과했다.
- ZIP7파일을 새 디렉터리에 추출했고 모두 Release 출력과 SHA256이 일치했다. Plugin/한국어 위성 DLL0.3.11.0/API15, csproj/manifest/후보 JSON의 Changelog 일치, 내장 조건·등급·THIRD-PARTY-NOTICES와 원본 해시 일치를 확인했다. 참조는Dalamud15.0.3.6/FFXIVClientStructs7.57.0.9376/Interop1.0.0.0이다. 결과는 `.cache/release-build/0.3.11.0-validation.json`에 있다.
- 준비 시 공개 저장소 쓰기 권한과 원격 태그/Release 부재를 확인했다. Release 익명 검사가 끝날 때까지 설치 JSON은0.3.10.0으로 유지했다.

## 고정 산출물

- ZIP: `artifacts/share/0.3.11.0/DuckbillFisher-0.3.11.0.zip`, **413,772바이트**.
- ZIP SHA256: **BE23229FA4B2A2DBFE9C7A2C1993717DB82CAD99852D30ECA2ABE96DF007C677**.
- 추출 DLL: `${env:DUCKBILL_ROOT}/.cache/release-validation/0.3.11.0/DuckbillFisher.dll`.
- DLL SHA256: **AF46F14ACBEAB335356C015B8271406336FA1A84072496C16F0B7C55DCC0CFD9**.

## 게시 상태와 남은 범위

- 소스c2a115d8ad7b5e1108dbc02d5ff2fdfaa460909b를 push하고 같은 커밋에 새 태그v0.3.11.0을 만들었다. GitHub [Release](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.3.11.0), ID418137946을2026-10-20T07:59:10Z에 공개했다. 기존 태그/자산을 덮어쓰거나 고정 ZIP을 재빌드하지 않았다.
- 2026-10-20T07:59:16Z ZIP/체크섬/태그 아이콘의 인증 없는 다운로드가 모두 HTTP200이며 로컬 고정본과 SHA256이 일치했다. 결과는 `.cache/release-remote-check/0.3.11.0/download-check.json`과 `publish-state.json`에 있다. 인증 값은 메모리에서만 사용하고 파일/로그/Git에 남기지 않았다.
- 이후 설치 JSON을8c72e1a로 커밋/push했다. 실제 공유 raw URL이 잠시 이전0.3.10.0 캐시를 반환했으나2026-10-20T08:04:37Z에 쿼리를 추가하지 않은 [공유 URL](https://raw.githubusercontent.com/PlatyDuckbill/DuckbillFisher/main/repo/pluginmaster.json)의 HTTP200·0.3.11.0/API15/Changelog·설치/업데이트/아이콘 URL·설명/LastUpdate 일치를 확인했다. 결과는 `.cache/release-remote-check/0.3.11.0/public-index-check.json`에 있다.
- 최종 Release 추출 DLL의 추가 실기는 사용자 지시로 생략했다. 실제 Dalamud 저장소 업데이트/친구 PC·모든 배율/언어/테마·다른 캐릭터·장애/원시 발견 이벤트의 전수 확인은 별도 미확인이다.
