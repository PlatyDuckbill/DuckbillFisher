# 0.3.8.0 배포

2026-10-19. 사용자가 커스텀 WAV 대표 확인 뒤 게시를 요청했고 고정 ZIP 추출본에도 ‘Release 추출본도 정상’으로 응답했다. 확인한 ZIP을 재빌드하지 않고 게시했으며 설치 JSON의 실제 공유 URL 조회까지 완료했다.

## 범위와 확인

- 공개0.3.0.0 이후 일반 어종 데이터 검증/30일 검색/직감 예측/물고기의 눈·비교 상세/알림 결함 수정/반복 데이터 재사용/커스텀 WAV를 포함한다. [영어 요약](../../releases/0.3.8.0.md).
- 패키지 실행 소스는77352ee다. 이후 후보 문서만 추가했고 실행 파일·SDK·의존성·lock·데이터를 바꾸지 않았다. 실제 태그는 추출본 확인 뒤 해당 확인 기록 커밋에 연결한다.
- 사용자 응답은 개발0.3.8.0 커스텀 WAV 대표 확인으로 기록한다. 실패 주입·출력 장치 변경·세션/종료 경계나 이전 개발 작업의 미확인 항목까지 통과한 것으로 확대하지 않는다.

## 실행한 검사

1. 최소 지원 참조 `.cache/compat-15.0.3.6/full/`로 직접 Release 빌드했다. 출력은 `.cache/release-build/0.3.8.0/`, 경고0/오류0. 테스트 추가/실행 없음.
2. 한영649키/형식 인자 검사와 prepare-release.ps1의 허용 파일/manifest/API/버전 검사를 통과했다. 후보 JSON만 생성하고 공개 repo/pluginmaster.json은 유지했다.
3. ZIP7항목을 `.cache/release-validation/0.3.8.0/`에 새로 추출했다. 모든 추출 파일은 Release 출력과 SHA256이 같다. Plugin/ko0.3.8.0·Core1.0.0.0/API15다. SDK/게임 DLL·개인 음원은 포함하지 않는다.
4. Mono.Cecil로 참조 Dalamud15.0.3.6/FFXIVClientStructs7.57.0.9376/Interop1.0.0.0을 확인했다. 내장 조건/등급/THIRD-PARTY-NOTICES는 각각 저장소 원본 해시와 일치했다.
5. 후보 JSON1항목의 설치/업데이트는v0.3.8.0의 같은 ZIP, 아이콘은같은태그다. 원격 공개 최신v0.3.0.0과v0.3.8.0 태그 부재를 조회했다. 미래 URL 다운로드 성공이나 게시로 표현하지 않는다.

## 고정 산출물

- ZIP: `artifacts/share/0.3.8.0/DuckbillFisher-0.3.8.0.zip`, **400,131바이트**.
- ZIP SHA256: **CED3E8CFB3D2E5F714388B1514382DD49FE958ED333208A1690C350905A2CE2E**.
- 체크섬: 같은 디렉터리의 `DuckbillFisher-0.3.8.0.zip.sha256`.
- 추출 DLL: `${env:DUCKBILL_ROOT}/.cache/release-validation/0.3.8.0/DuckbillFisher.dll`.
- DLL SHA256: **99F05A4DD45DF9E115DB6B05E6FF93443A4883B9B99C3C86E1B61E9AEA049C7D**.

## 추출본 확인과 게시 순서

- 위 추출 DLL로 WAV 미리 듣기/중지·목록/도감·타이머/기록·설정 복원의 대표 확인을 요청했고 사용자가 ‘Release 추출본도 정상’으로 응답했다. 이 요청 범위의 대표 확인이며 다른 장애/경계/전수 확인으로 확대하지 않는다.
- 확인 뒤 같은 ZIP을 재빌드 없이 게시한다. 소스/확인 기록 commit·push→새v0.3.8.0 태그→Release/ZIP/체크섬→익명 다운로드/해시/아이콘→후보 설치 JSON commit·push→실제 공유 raw URL 확인 순서다.
- 다른 캐릭터·친구 PC·전수 데이터·전체 테마·장애/성능 측정과 실제 Dalamud 저장소 업데이트는 별도 대기다. 배포 권한과 고정 패키지 대표 확인을 받았다.

## 실제 게시

- 확인 기록 커밋899f567을main에 정상push하고 같은 커밋에새v0.3.8.0 태그를 게시했다. 기존 태그/Release/ZIP은 덮어쓰지 않았다. 게시 커밋의 [skip ci]로 테스트 CI를 유발하지 않도록 했다.
- GitHub [Release](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.3.8.0), ID416649615, 게시 시각2026-10-18T19:20:50Z에 고정 ZIP/체크섬2개를 게시했다. 업로드 크기와 GitHub digest가 로컬 해시와 일치했다. 인증은 Git credential에서 메모리로만 읽고 파일/로그/Git에 저장하지 않았다.
- 2026-10-18T19:21:10Z에 인증 없는 ZIP/체크섬/태그 아이콘 다운로드가 모두 HTTP200이었다. ZIP SHA256·체크섬 내용·원본 아이콘 해시가 일치했다. 결과는 `.cache/release-remote-check/0.3.8.0/download-check.json`에 보관한다.
- 위 확인 뒤에만 후보 JSON을repo/pluginmaster.json에 복사하고 커밋c18de68b17eff67520a9fd0b270deb95f98b13e2로 정상push했다. 설치/업데이트가 같은0.3.8.0 ZIP, 아이콘이 같은태그,1항목/API15다.
- 2026-10-18T19:23:54Z에 인증 없는 실제 [공유 raw URL](https://raw.githubusercontent.com/PlatyDuckbill/DuckbillFisher/main/repo/pluginmaster.json)을 쿼리 추가 없이 조회했다. HTTP200·DuckbillFisher1항목·0.3.8.0/API15·설치/업데이트/아이콘 URL·설명을 로컬 설치 JSON 및 확인한 manifest와 대조했다. 결과는 `.cache/release-remote-check/0.3.8.0/public-index-check.json`에 보관한다.
- 원격 게시와 HTTP/해시 확인은 완료했다. 실제 Dalamud 저장소 업데이트와 친구 PC 대표 확인은 별도 대기이며, 추출 DLL 대표 확인을 전수·장애 확인으로 확대하지 않는다. 이번 작업에 테스트를 추가하거나 실행하지 않았다.
