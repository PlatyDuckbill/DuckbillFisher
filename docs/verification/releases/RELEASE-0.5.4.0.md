# 0.5.4.0 배포 후보

2026-10-24. 사용자 ‘배포 진행하자. 노트 잘 써서’ 요청에 따라 개발본 검수 뒤 버전 업데이트/배포 단계로 진행한다. 이전 Debug 안내를 새 ZIP의 실기 확인으로 확대하지 않으며 **고정 Release 추출본 확인 뒤 같은 ZIP만 게시**한다. [영어 노트](../../releases/0.5.4.0.md).

## 변경과 검사

- 설정 JSON/ZIP 백업과 WAV 전용 복사본, 어종별 메모·도전 횟수·캐스팅 프리셋 연결, 확인 후 메모 비우기/프리셋 덮어쓰기, 출현 설정 탭·캐스팅 전용 채널·설정 UI 정리를 포함한다. [개인 기록](../../product/FISH-PERSONAL.md)·[백업](../../product/SETTINGS-BACKUP.md)·[알림 UI](../alerts/SPAWN-ALERTS.md#설정-화면-정리-2026-10-24).
- 앱0.5.4.0/API15·설정schema23·기록schema2다. SDK/의존성/lock·정적 자료·게임/DLL 조합/네이티브 가드는 변경하지 않는다. 테스트는 요청되지 않아 추가/실행하지 않는다.
- 설치 화면 Changelog·영어 릴리스 문서·내장 한영 기능별 노트를 함께 갱신했다. 내장 PublishedAtUtc는 배포 요청 무렵 **2026-10-24T09:14:50Z / 한국18:14:50**이며 실제 GitHub 공개 시각이라고 단정하지 않는다.
- 현재 참조의 별도 `.cache/release-notes-build/` → 정규 Debug 경고0/오류0·한영984키/형식 인자·문서114개/로컬 링크569개·diff 검사 통과. 최소 지원 참조 Release와 고정 ZIP 검사는 아래 단계에서 구분한다.
- 원격 사전 확인: 최신v0.5.3.0/Release421678993, 원격 main `c19cf84b89dc0a5db6429e81200f83f30dcf5adc`이며 현재 로컬은9커밋 앞/뒤0이다. 공개 저장소/push 권한을 확인했고 새v0.5.4.0 태그/Release는 모두404다. 인증 값은 메모리에서만 사용했다.

## 고정 산출물과 남은 단계

- 소스 **622544e92fca75ad34921f3594692d21fa840d18**. 최소 지원 `.cache/compat-15.0.3.6/full/` Release 빌드 경고0/오류0·prepare-release.ps1 통과. ZIP 추출7파일과 빌드 출력 해시·프로젝트/manifest/후보 설치 JSON의 버전/API15·설명/Changelog·다운로드/태그 아이콘 URL이 일치한다.
- Plugin/한국어 위성 DLL0.5.4.0·Core 식별자, 내장 조건/등급/고지3자료의 원본 해시와 패키지 한영 각각984키/값의 .resx 일치를 확인했다. 결과는 `.cache/release-build/0.5.4.0-validation.json`이다. 자료/패키지 검사이며 게임 실행 검증은 아니다.
- 고정 ZIP: `artifacts/share/0.5.4.0/DuckbillFisher-0.5.4.0.zip`, **569,558바이트**, SHA256 **A424CD0DB7C1F759080F00F46B0D96727B08C151C8F75758D7B034D531C7D743**. 옆 체크섬/후보 설치 JSON도 고정했다.
- 추출 DLL: `DUCKBILL_ROOT/.cache/release-validation/0.5.4.0/DuckbillFisher.dll`, SHA256 **B9B17B314804C6D753D605714EEC7C76893F6E0CF783D64F77B90AAFD4B0C5F3**.
- 고정 추출본의 최신 노트/백업·개인 기록·설정 UI·기존 도감/타이머/한영/재로드 대표 확인이 남아 있다. 확인 뒤에는 실행 파일을 재빌드하지 않는다.
- 게시 순서는 확인한 소스/태그 push → Release의 ZIP/체크섬 공개 → 익명 파일/해시·아이콘 확인 → 설치 JSON 갱신 → 실제 공유 raw URL 확인이다. 현재 공개판/실제 업데이트·전수/다른 캐릭터/장애의 확인 범위는 [STATUS](../../STATUS.md)를 따른다.
