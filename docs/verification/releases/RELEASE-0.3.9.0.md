# 0.3.9.0 배포

2026-10-19. 사용자가 고정 Release 추출본 대표 확인 요청에 ‘이상 없음 배포 진행’으로 응답했다. 확인한 ZIP을 재빌드하지 않고 게시했고 익명 다운로드를 확인한 뒤 설치 목록 최종 조회를 진행한다.

## 범위와 검사

- 설치 화면 Changelog, 내장 한영 최근 릴리스5개 이력, 버전별 확인 저장(schema13), 설정에서 다시 열기. [영어 요약](../../releases/0.3.9.0.md), [동작·인게임 순서](../catalog/UI.md#0390-업데이트-안내-2026-10-19).
- 배포 후보에0.3.9.0 자체 내역을 추가했다. 표시할5개는0.3.9.0/0.3.8.0/0.3.0.0/0.2.6.0/0.2.5.0이고 중간 개발 버전은 제외한다. 배포 전인0.3.9.0은 후보 확인용이며 원격 게시가 끝난 것으로 표현하지 않는다.
- 별도→정규 Debug와 최소 지원 참조 `.cache/compat-15.0.3.6/full/`의 직접 Release 빌드 모두 경고0/오류0. Release 출력은 `.cache/release-build/0.3.9.0/`다. SDK/의존성/데이터 변경 없음. 테스트 추가/실행 없음.
- 한영669키/형식 인자 검사, prepare-release.ps1의 허용 파일·manifest/API/버전 검사를 통과했다. 생성 manifest와 후보 설치 JSON의 Changelog가 csproj 문구와 일치한다.
- ZIP7항목을 `.cache/release-validation/0.3.9.0/`에 새로 추출했다. 모든 추출 파일의 SHA256이 Release 출력과 같다. Plugin/ko0.3.9.0·Core1.0.0.0/API15다. SDK/게임 DLL·개인 음원은 포함하지 않는다.
- Mono.Cecil로 참조 Dalamud15.0.3.6/FFXIVClientStructs7.57.0.9376/Interop1.0.0.0을 확인했다. 내장 조건/등급/THIRD-PARTY-NOTICES 해시는 각각 저장소 원본과 같다. 상세 검사 결과는 `.cache/release-build/0.3.9.0-validation.json`에 있다.
- 후보 JSON은1항목, 설치/업데이트는v0.3.9.0의 같은 ZIP, 아이콘은같은태그다. 게시 전 원격main은6e034d1이고v0.3.9.0 태그·공개 Release가 없음을 조회했다. 미래 URL의 다운로드 성공을 주장하지 않는다.

## 고정 산출물

- ZIP: `artifacts/share/0.3.9.0/DuckbillFisher-0.3.9.0.zip`, **405,616바이트**.
- ZIP SHA256: **FDFEA7AE5BE0E77D9B3F7BCD26964CEA55B383B3E2A67056D7C30BA411805C0F**.
- 체크섬: 같은 디렉터리의 `DuckbillFisher-0.3.9.0.zip.sha256`.
- 추출 DLL: `${env:DUCKBILL_ROOT}/.cache/release-validation/0.3.9.0/DuckbillFisher.dll`.
- DLL SHA256: **B6DA24ECC0DE84DF3221B1F9210DACE9B832D2B7BFBA7748147BCFD71C31A6A4**.

## 추출본 확인과 게시

- 위 고정 추출 DLL로 업데이트 내역5개/접기·펼치기, 닫기→재로드 후 자동 생략, 설정에서 다시 열기/한영 전환, 기존 목록/타이머·설정 복원을 요청했고 사용자가 ‘이상 없음 배포 진행’으로 응답했다. 이 요청 범위의 대표 확인이며 전수·장애 확인으로 확대하지 않는다. 이전 개발 DLL 확인으로 대체하지 않았다.
- 사용자 대표 확인과 게시 권한을 받았다. 같은 ZIP을 재빌드하지 않고 소스/확인 기록push→새태그/Release→익명 ZIP/체크섬/아이콘→설치 JSON→실제 공유 raw URL 순서로 진행한다.
- 다른 캐릭터/친구 PC/전수 테마·장애/성능 측정·실제 Dalamud 업데이트는 별도 대기다.0.3.8.0 이전 확인 대기를 이번 준비 완료로 바꾸지 않는다.

## 실제 게시

- 실행 소스는6702b62, 사용자 확인 기록을 포함한 태그 커밋은8ada789ec407715fe950c4ad1984811d2a7b75d0이다. main과새v0.3.9.0 태그를 정상push했다. 확인 이후 실행 파일을 빌드하거나 기존 태그/Release/ZIP을 덮어쓰지 않았다. 커밋은 [skip ci]를 사용했다.
- GitHub [Release](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.3.9.0), ID416680193, 게시 시각2026-10-18T20:07:42Z. ZIP/체크섬2개를 초안에 업로드해 크기와 GitHub digest를 대조하고 공개했다. 인증은 Git credential에서 메모리로만 읽고 파일/로그/Git에 남기지 않았다.
- 2026-10-18T20:08:06Z에 인증 없는 ZIP/체크섬/태그 아이콘 요청이 모두 HTTP200이었다. ZIP/체크섬 해시와 내용·원본 아이콘 해시가 일치했다. 결과는 `.cache/release-remote-check/0.3.9.0/download-check.json`에 보관한다.
- 위 확인 뒤 후보 설치 JSON을repo/pluginmaster.json에 복사했다. 설치/업데이트는 같은0.3.9.0 ZIP, 아이콘은같은태그이며1항목/API15/Changelog다. 설치 JSON push와 실제 공유 raw URL의 최종 조회는 이어 확인한다.
