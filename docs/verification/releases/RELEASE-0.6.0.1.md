# 0.6.0.1 배포 기록

2026-10-31. 사용자 **‘기존 작업 방식에 맞게 진행하여 배포까지 진행. 단순 빌드 대응이니 별도 테스트 없이 배포까지 해도 무방’** 요청으로 dal4kr 15.0.3.9 런타임 지원 배포를 진행했다. **동일 ZIP의 Release 공개/익명 자산·설치 JSON 확인 및 DuckbillRepo 공용 저장소 동기화 완료**이며 실제 Dalamud 업데이트는 별도 대기다. [영어 안내](../../releases/0.6.0.1.md).

## 변경과 자체 점검

- dal4kr 15.0.3.9 배포 갱신(2026-10-29T14:01:21Z)에 맞춰 `NativeRuntimeProfiles`에 해당 MVID/SHA256 프로필을 추가했다. 모듈 해시는 DuckbillTweak 0.2.0.4 및 NATIVE-RUNTIME-DAL4KR.md 검증 기록과 동일하다.
- 앱0.6.0.1/API15. SDK/의존성/lock·정적 자료·네이티브 가드/게임 DLL 조합은 이번 배포 준비에서 바꾸지 않았다. 자동 게임 조작·런타임 HTTP는 추가하지 않았다.
- 사용자 지시에 따라 별도 테스트는 추가/실행하지 않았다.
- 내장 한영/설치 화면/영어 안내의 기능 범위를 대조했다. PublishedAtUtc는 배포 요청 무렵 **2026-10-31T13:40:00Z / 한국22:40:00**이며 실제 GitHub 공개 시각이 아니다.
- 현재 KR 참조 Debug와 최소 지원 참조 Release 모두 경고0/오류0이다. 한영1,257키/형식 인자·문서/diff·prepare-release.ps1 검사를 통과했다.
- 원격 사전 조회에서 저장소 공개/push 권한과 태그 v0.6.0.1 사전 push를 확인했다.

## 고정 후보

- 소스 **4e5663cd36a56c42be4de42f0211d6373b21d6b3**. 최소 지원 참조 `.cache/compat-15.0.3.6/full/`로 직접 Release 빌드한 뒤 prepare-release.ps1을 사용했다.
- ZIP: `artifacts/share/0.6.0.1/DuckbillFisher-0.6.0.1.zip`, **629,145바이트**, SHA256 **1A5E380AA26D40C032A21856CF3A57BE3FC6F9D75B101A0C977385618E408B1C**.
- 추출 DLL: `DUCKBILL_ROOT/.cache/release-validation/0.6.0.1/DuckbillFisher.dll`, SHA256 **2EB184824969BBFAEC7EB37190C68F0FBB3A7400499743DE58E3897382721F85**.
- ZIP의 7개 파일과 빌드 출력 해시, Plugin/한국어 위성 DLL 0.6.0.1·Core 식별자, 프로젝트/manifest/후보 설치 JSON의 버전/API15·Changelog/설명·다운로드/태그 아이콘 URL을 대조했다. 내장 조건/등급/고지 3자료의 원본 해시와 한영 각각 1,257키/값이 일치한다.

## 원격 게시와 설치 목록

- 소스 **4e5663cd36a56c42be4de42f0211d6373b21d6b3**를 가리키는 새 v0.6.0.1 태그와 승인 범위 main을 정상 push했다. [공개 Release](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.6.0.1)는 ID **429963828**, 실제 공개 **2026-10-31T13:46:12Z / 한국22:46:12**다. 기존 버전/태그/자산은 덮어쓰지 않았다.
- 초안 ZIP/체크섬의 서버 digest·크기를 확인한 뒤 공개했다. 익명 정식 ZIP·체크섬·태그 아이콘 HTTP200/원본 SHA256 일치와 공개 본문/영어 안내 일치를 확인했다. 결과 `.cache/release-remote-check/0.6.0.1/`의 publish-state.json/download-check.json.
- 익명 자산 확인 후 후보 설치 JSON을 커밋 `487568ba9b175d7a02be653a0c2708aa4a60ecd6`으로 push했다.
- DuckbillRepo 공용 저장소의 `sync.yml` 워크플로우 `41207260470` 실행 성공 및 커밋 `a0767e16946dc1fd3f536d9268215bbd71db6626`으로 공용 `pluginmaster.json`이 0.6.0.1로 동기화 완료되었다.
