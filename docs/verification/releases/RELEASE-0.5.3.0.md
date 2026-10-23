# 0.5.3.0 배포 기록

2026-10-23 한국 시간. 고정 추출 Release에 사용자 **‘문제 없음. 배포 진행’** 확인을 받은 뒤 같은 ZIP을 게시했다. **Release·익명 자산/해시·설치 JSON·실제 공유 raw URL·실제 Dalamud 저장소 업데이트 확인 완료**다. [영어 요약](../../releases/0.5.3.0.md).

## 변경과 검사

- 입질·터주·터주왕의 개별SE/커스텀WAV, 실제 어류도감 최초 등록만 기본 적용하는 터주/터주왕 옵션, 낚시터 수집 필터의 재시작 저장을 포함한다. 타이머 설정은 왼쪽 캐스팅 알림/효과음 두 탭·우측 공통 출력 설정·아이콘/불릿 설명으로 정리했다. [계약·대표 확인 순서](../fishing/TIMER-HISTORY.md#입질터주터주왕-효과음과-낚시터-필터-저장-2026-10-23).
- 소스 **6dd27eba123087242815fea3fa96daa5600e1a0c**. 버전/내장 업데이트 내역/영어 릴리스 요약을 맞췄으며 설정schema21·기록schema2를 사용한다. SDK/의존성/lock/정적 자료·네이티브 가드는 변경하지 않았다. 테스트 추가/실행 없음.
- 검증한 최소 지원 `.cache/compat-15.0.3.6/full/`로 Release `.cache/release-build/0.5.3.0/`를 빌드해 경고0/오류0. 한영911키/형식 인자·문서 검사와 `scripts/prepare-release.ps1` 통과. 프로젝트/manifest/후보 설치 JSON의 버전/API15·설명/Changelog·아이콘/다운로드 경로 일치다.
- ZIP 추출7파일 모두 빌드 출력 SHA256 일치, Plugin/한국어 위성 리소스 DLL0.5.3.0, 내장 조건/등급/고지3자료의 원본 해시 일치, 패키지에 든 영어/한국어 문자열 각각911키·값의 .resx 일치를 확인했다. 결과 `.cache/release-build/0.5.3.0-validation.json`이며 게임 동작/청취 확인을 뜻하지 않는다.

## 고정 산출물

- ZIP `artifacts/share/0.5.3.0/DuckbillFisher-0.5.3.0.zip`: **519,128바이트**, SHA256 **041FC48576E17C9BE6C87EFD756AD0591FDE4AA7611C9664D333CBD0B78C56B3**.
- 추출 DLL `${env:DUCKBILL_ROOT}/.cache/release-validation/0.5.3.0/DuckbillFisher.dll`: SHA256 **7D227E08E414AA866F93BDD55168785D29D63A1AF19A39168A1F0B803502AF5A**.
- ZIP 옆 체크섬/후보 pluginmaster.json을 고정했다. 확인 뒤 실행 파일을 다시 빌드하지 않으며, 수정이 필요하면 별도 후보로 취급한다. 정규/별도 Debug는0.5.2.0 개발본이므로 이번 후보 확인에는 위 추출 DLL을 사용한다.
- 내장 PublishedAtUtc는 배포 요청 무렵 **2026-10-23T09:41:18Z / 한국2026-10-23 18:41:18**이며 실제 GitHub 공개 시각이 아니다.

## 실기와 게시

- 준비 시 최신 공개 Release는v0.5.2.0/ID421639798, 원격 main은2ea34cc84ac479ce729b9e83ba11bc1daf6810cf였다. 공개 저장소 push 권한과 새v0.5.3.0 태그/Release 부재를 확인했다. 인증 값은 메모리에서만 사용하고 출력/파일에 기록하지 않았다.
- 사용자에게 고정 추출본의 설정 배치·효과음/최초 습득 옵션/개별WAV·필터와 설정의 재로드 복원을 요청했고 ‘문제 없음. 배포 진행’ 응답을 받았다. 위 ZIP/DLL 해시에 연결한 대표 정상 확인이며, 모든 어종/언어/폭/배율·다른 캐릭터·장애/동시 출력까지 확인한 것으로 확대하지 않는다. 게시 직전 ZIP/추출7파일의 해시와 후보 이후 런타임/포장 소스 변경 없음을 재확인했다. 실행 파일 재빌드 없음. 이전 실기 대기는 STATUS대로 유지한다.
- 소스6dd27eba123087242815fea3fa96daa5600e1a0c를 가리키는 새v0.5.3.0 태그와 승인 범위 main을 정상 push했다. [공개 Release](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.5.3.0)는 ID**421678993**, 실제 공개 **2026-10-23T09:50:56Z / 한국18:50:56**다. 초안에서 ZIP/체크섬 크기와 서버digest를 확인한 뒤 공개했고 정식 다운로드 URL을 다시 조회했다.
- 인증 없는 정식 ZIP·체크섬·태그 아이콘 HTTP200·원본 SHA256 일치를 확인한 뒤 설치 JSON을 **c4161174268e2b251d90e521c6e289fe3a39c96e**로 커밋·push했다. 공개 ZIP은 사용자 확인본과 동일하며 실행 파일 재빌드/기존 자산 덮어쓰기 없음. 결과는 `.cache/release-remote-check/0.5.3.0/`의 publish-state.json/download-check.json/index-push-state.json이다.
- 쿼리 없는 실제 공유 raw URL 첫 확인 **2026-10-23T09:52:11.8309786Z / 한국18:52:11**에는 캐시의0.5.2.0을 반환했다. **2026-10-23T09:54:06.6285183Z / 한국18:54:06**에 같은 URL의 HTTP200·새0.5.3.0/API15·이름/설명/업데이트 내역·설치/업데이트/아이콘 URL·LastUpdate의 로컬 일치를 확인했다. public-index-attempts.json/public-index-check.json/public-pluginmaster.json에 남겼으며 캐시 우회 URL로 대체하지 않았다.
- 2026-10-23 실제 Dalamud 커스텀 저장소 업데이트·버전/정상 로드·기존 설정 복원을 확인하는 다음 작업 추천1번에 사용자 **‘1 확인 잘됐음’** 응답을 받았다. 공개0.5.3.0의 실제 업데이트 경로 대표 정상 확인으로 기록한다. 전수/다른 캐릭터/장애 조합이나 이전 버전별 확인 대기로 확대하지 않는다.
