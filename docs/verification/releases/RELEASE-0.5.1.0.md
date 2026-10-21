# 0.5.1.0 배포 기록

2026-10-22 한국 시간. 타이머 인내·직감과 출현 시간 필터를 고정 Release로 준비했다. **고정 추출 DLL 사용자 대표 확인 완료·원격 게시 진행 중**이다. [영어 요약](../../releases/0.5.1.0.md).

## 변경과 검사

- 타이머는 인내/어부의 직감 아이콘·실제 남은 분:초를 표시하며 미적용/미확인과 구분한다. [공개 API·KR3배포판 정적 대조](../fishing/TIMER-HISTORY.md#타이머-인내직감-남은-시간-2026-10-22)를 유지한다.
- 출현 중 최소 잔여 시간과 출현까지 최소/최대 시간을 공통 필터/간이/현재 목록 대상 미끼 준비·일정표에 적용한다. 최소 잔여는 빈칸/0 모두 해제, 예정 경계는 각각 빈칸 해제다. nullable 예정 경계·schema19·프리셋/이전 최대0 변환·입력 오류/역전 범위 안내를 제공한다. [범위·경계](../catalog/UI.md#출현까지-최소최대-범위와-빈칸-2026-10-22).
- 별도 `.cache/remaining-empty-build/`→정규 Debug·최소 지원 `.cache/compat-15.0.3.6/full/` Release 경고0/오류0. 한영854키/형식 인자 검사 통과. 테스트는 명시 요청 없어 추가/실행하지 않았다. SDK/lock·지원 프로필/네이티브 가드는 변경하지 않았다.
- `scripts/prepare-release.ps1` 통과. ZIP 추출7파일 모두 빌드 출력 해시 일치, Plugin/한국어 DLL0.5.1.0·API15·manifest/프로젝트/후보 JSON Changelog/Description 일치, 내장 조건/등급/고지3자료 원본 해시 일치다. 검사 결과 `.cache/release-build/0.5.1.0-validation.json`.

## 고정 산출물과 시각

- ZIP `artifacts/share/0.5.1.0/DuckbillFisher-0.5.1.0.zip`: **491,843바이트**, SHA256 **206B67D262E02B4581565D0068D50F3465B69BAD3450C1C9F00B3DAA3603F56C**.
- 추출 DLL `${env:DUCKBILL_ROOT}/.cache/release-validation/0.5.1.0/DuckbillFisher.dll`: SHA256 **DCB20161271D774B009BAB2BF69B9804B27A7922BF1A561AFB809346D5463BD9**.
- Release 출력 `.cache/release-build/0.5.1.0/`; ZIP 옆 체크섬/후보 JSON을 함께 고정한다. 사용자 확인 뒤 실행 파일을 재빌드하지 않는다.
- 내장 PublishedAtUtc/후보 LastUpdate는 요청 무렵 **2026-10-21T16:22:12Z / 한국2026-10-22 01:22:12**, Unix **1792599732**다. 실제 GitHub 공개 시각과 구분한다.

## 확인과 게시

- 사용자가 위 고정 추출 DLL의 최소 잔여 시간 전체 삭제→해제, 예정 최소/최대 한쪽 빈칸, 인내·직감 표시 요청에 **‘정상’**으로 응답했다. 위 ZIP/DLL 해시의 대표 확인 결과로 연결한다. 전수/친구 PC/캐릭터/장애·모든 언어/폭 확인으로 확대하지 않는다.
- 게시 준비 시 origin/main은0040b44이고 로컬은 원격 전용0/로컬2커밋 앞이었다. 새v0.5.1.0 태그/Release 없음·공개 저장소 push 권한을 확인했다. 토큰은 메모리에서만 사용한다.
- 소스 push/태그 → Release ZIP·체크섬 게시 → 익명 다운로드/아이콘 해시 → 설치 JSON 게시 → 실제 공유 raw URL 확인 순으로 진행한다. 실제 Dalamud 저장소 업데이트는 별도 확인 대상이다.
