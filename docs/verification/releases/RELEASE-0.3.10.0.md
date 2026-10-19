# 0.3.10.0 배포

2026-10-19. 사용자가 단축 명령어·낚시터 어종 목록 유지 수정과 배포를 요청했고, 고정 Release 추출 DLL 대표 확인 요청에 ‘이상 없음’으로 응답했다. 확인한 ZIP을 재빌드하지 않고 게시했으며 익명 다운로드/해시·설치 목록의 실제 공유 URL 조회까지 완료했다. 실제 저장소 설치 확인은 별도다.

## 범위와 검사

- `/dbfs` 접두사와 `cl`(compact list)·`ct`(casting timer) 인자를 추가했다. 두 접두사에서 짧은/긴 인자를 모두 지원한다. 기존 `/duckbillfisher`·`compact`·`timer`는 유지한다. 단축 접두사 충돌 시 기존 명령어를 유지하고 로그에 남기며, 등록한 단축 명령어만 언로드 때 해제한다.
- 낚시터 어종 팝업을 `SpotFishWindow` 하나로 분리했다. 어종 상세 선택·바깥 클릭·타이머 닫기로 목록을 닫지 않으며 버튼 재클릭은 같은 목록을 연다. 현재/마지막 낚시터·수집 갱신과 공통 어종 행·전체/수집 숨김은 유지한다. Draw에서 파일 읽기나 전체 어종 재검색을 추가하지 않았다.
- [제품 계약](../../product/PRODUCT.md#화면과-상호작용), [영어 릴리스 요약](../../releases/0.3.10.0.md). 내장 안내 최신5개는0.3.10.0/0.3.9.0/0.3.8.0/0.3.0.0/0.2.6.0이다.
- 별도 `.cache/short-commands-build/`→정규 Debug와 최소 지원 참조 `.cache/compat-15.0.3.6/full/` 직접 Release 빌드 모두 경고0/오류0. 한영671키/형식 인자 검사 통과. SDK/의존성/데이터/저장 schema13/진단 형식 변경 없음. 테스트 추가·실행 없음.
- `scripts/prepare-release.ps1`의 허용 파일·manifest/API/버전 검사를 통과했다. ZIP7항목을 새 후보 디렉터리에 추출했고 모두 Release 출력과 SHA256이 일치한다. Plugin/ko0.3.10.0/API15이며 manifest Changelog/설명과 후보 설치 JSON이 일치한다.
- Mono.Cecil로 참조 Dalamud15.0.3.6/FFXIVClientStructs7.57.0.9376/Interop1.0.0.0과 내장 조건/등급/THIRD-PARTY-NOTICES의 원본 해시 일치를 확인했다. 검사 결과는 `.cache/release-build/0.3.10.0-validation.json`이다.
- 준비 때 원격 main75de53a와 v0.3.10.0 태그 부재를 조회했고 게시 직전 공개 저장소/인증 쓰기 권한과 태그·Release 부재를 다시 확인했다. 원격 Release 확인 전 설치 JSON은0.3.9.0을 유지했다.

## 고정 산출물

- ZIP: `artifacts/share/0.3.10.0/DuckbillFisher-0.3.10.0.zip`, **406,869바이트**.
- ZIP SHA256: **43225D14A27115046C372984B2ECD6274423707B1DDAF97784A23CB260E0AFD1**.
- 체크섬: 같은 디렉터리의 `DuckbillFisher-0.3.10.0.zip.sha256`.
- 추출 DLL: `${env:DUCKBILL_ROOT}/.cache/release-validation/0.3.10.0/DuckbillFisher.dll`.
- DLL SHA256: **11E15F955E411672D7856B39A87172703243D7AF3BDF62FB7E00D1B38D42364B**.

## 추출본 대표 확인

1. `/dbfs`로 메인, `/dbfs cl`로 간이 목록, `/dbfs ct`로 타이머 열기/닫기. 기존 `/duckbillfisher compact`·`timer`도 동작해야 한다. 두 접두사에서 `cl`/`ct`/`compact`/`timer`는 같은 기능이며 대소문자/앞뒤 공백을 허용한다.
2. 확인된 낚시터에서 타이머의 어종 보기 → 상세 선택 → 바깥 클릭 → 타이머 닫기 후에도 목록이 남아야 한다. 전체/수집 숨김·스크롤·지도 버튼·제목 닫기/다시 열기도 확인한다. 낚시터가 새로 확인되면 대상/제목이 갱신되어야 한다. 지역만으로 낚시터를 추측하지 않는다.
3. 한영 전환에서 화면 제목·명령 도움말·0.3.10.0 안내가 바뀌고, 기존 목록/상세/타이머·기록은 동작해야 한다. 언로드→재로드 후 두 접두사가 동작하고 이전 어종 목록은 자동 재열림하지 않아야 한다.
4. 다른 캐릭터/전수 테마·좁은 폭·장애/명령 충돌 주입·실제 저장소 업데이트·친구 PC 확인은 별도다. 이전 버전 미확인을 통과로 바꾸지 않는다.

- 위 고정 추출 DLL로 `/dbfs`·`cl`·`ct`와 기존 긴 명령어, 상세 선택·바깥 클릭·타이머 닫기 후 낚시터 목록 유지, 전체/수집 숨김·닫기/재열기, 한영·업데이트 내역·언로드/재로드 확인을 요청했고 사용자가 ‘이상 없음’으로 응답했다. 이 요청 범위의 대표 확인이며 모든 인자 조합/명령 충돌·낚시터 전환/지도·전수 테마/다른 캐릭터/실제 저장소 업데이트까지 확인한 것으로 확대하지 않는다.
- 구현 소스는95126eb다. 같은 ZIP을 재빌드하지 않고 소스push/새태그·Release→익명 ZIP/체크섬/아이콘→설치 JSON→실제 공유 raw URL 순으로 게시했다.

## 실제 게시

- 사용자 확인 기록을 포함한 소스 태그 커밋은77d5442661fa0b4cf681a49d7672be238c5e560d다. main과 새v0.3.10.0 태그를 정상push했다. 확인 후 실행 파일을 재빌드하거나 기존 태그/Release/ZIP을 덮어쓰지 않았다. 커밋은 [skip ci]를 사용했다.
- GitHub [Release](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.3.10.0), ID417042910, 게시 시각2026-10-19T08:05:59Z. ZIP/체크섬2개를 초안에 업로드해 크기와 GitHub digest를 대조하고 공개했다. 인증은 Git credential에서 메모리로만 읽었고 파일/로그/Git에 남기지 않았다.
- 2026-10-19T08:06:08Z 인증 없는 ZIP/체크섬/태그 아이콘 요청 모두 HTTP200이며 고정 원본 해시와 일치했다. 결과는 `.cache/release-remote-check/0.3.10.0/download-check.json`, 공개 Release/자산 상태는 같은 경로의 `publish-state.json`에 있다.
- 그 뒤 후보 설치 JSON을 `repo/pluginmaster.json`에 반영하고 커밋d21c9b5로 정상push했다. 설치/업데이트는 같은0.3.10.0 ZIP, 아이콘은 같은 태그,1항목/API15/Changelog다.
- 2026-10-19T08:07:51Z 인증 없는 실제 [공유 raw URL](https://raw.githubusercontent.com/PlatyDuckbill/DuckbillFisher/main/repo/pluginmaster.json)을 쿼리 추가 없이 조회했다. HTTP200·DuckbillFisher1항목·0.3.10.0/API15·Changelog·설치/업데이트/아이콘 URL·설명/LastUpdate가 로컬 설치 JSON과 일치한다. 결과는 `.cache/release-remote-check/0.3.10.0/public-index-check.json`이다.
- 원격 게시/HTTP·해시 확인은 완료했다. 실제 Dalamud 저장소 업데이트·설치 화면 렌더링·친구 PC/전수/장애 확인은 별도 대기다. 이전 버전 미확인을 통과로 바꾸지 않았고 테스트를 추가하거나 실행하지 않았다.
