# 0.5.4.0 배포 후보

2026-10-24. 사용자 ‘배포 진행하자. 노트 잘 써서’ 요청에 따라 개발본 검수 뒤 버전 업데이트/배포 단계로 진행한다. 이전 Debug 안내를 새 ZIP의 실기 확인으로 확대하지 않으며 **고정 Release 추출본 확인 뒤 같은 ZIP만 게시**한다. [영어 노트](../../releases/0.5.4.0.md).

## 변경과 검사

- 설정 JSON/ZIP 백업과 WAV 전용 복사본, 어종별 메모·도전 횟수·캐스팅 프리셋 연결, 확인 후 메모 비우기/프리셋 덮어쓰기, 출현 설정 탭·캐스팅 전용 채널·설정 UI 정리를 포함한다. [개인 기록](../../product/FISH-PERSONAL.md)·[백업](../../product/SETTINGS-BACKUP.md)·[알림 UI](../alerts/SPAWN-ALERTS.md#설정-화면-정리-2026-10-24).
- 앱0.5.4.0/API15·설정schema23·기록schema2다. SDK/의존성/lock·정적 자료·게임/DLL 조합/네이티브 가드는 변경하지 않는다. 테스트는 요청되지 않아 추가/실행하지 않는다.
- 설치 화면 Changelog·영어 릴리스 문서·내장 한영 기능별 노트를 함께 갱신했다. 내장 PublishedAtUtc는 배포 요청 무렵 **2026-10-24T09:14:50Z / 한국18:14:50**이며 실제 GitHub 공개 시각이라고 단정하지 않는다.
- 현재 참조의 별도 `.cache/release-notes-build/` → 정규 Debug 경고0/오류0·한영984키/형식 인자·문서114개/로컬 링크569개·diff 검사 통과. 최소 지원 참조 Release와 고정 ZIP 검사는 아래 단계에서 구분한다.
- 원격 사전 확인: 최신v0.5.3.0/Release421678993, 원격 main `c19cf84b89dc0a5db6429e81200f83f30dcf5adc`이며 현재 로컬은9커밋 앞/뒤0이다. 공개 저장소/push 권한을 확인했고 새v0.5.4.0 태그/Release는 모두404다. 인증 값은 메모리에서만 사용했다.

## 최초 고정본 대표 확인

- 소스 **622544e92fca75ad34921f3594692d21fa840d18**. 최소 지원 `.cache/compat-15.0.3.6/full/` Release 빌드 경고0/오류0·prepare-release.ps1 통과. ZIP 추출7파일과 빌드 출력 해시·프로젝트/manifest/후보 설치 JSON의 버전/API15·설명/Changelog·다운로드/태그 아이콘 URL이 일치한다.
- Plugin/한국어 위성 DLL0.5.4.0·Core 식별자, 내장 조건/등급/고지3자료의 원본 해시와 패키지 한영 각각984키/값의 .resx 일치를 확인했다. 결과는 `.cache/release-build/0.5.4.0-validation.json`이다. 자료/패키지 검사이며 게임 실행 검증은 아니다.
- 고정 ZIP: `artifacts/share/0.5.4.0/DuckbillFisher-0.5.4.0.zip`, **569,558바이트**, SHA256 **A424CD0DB7C1F759080F00F46B0D96727B08C151C8F75758D7B034D531C7D743**. 옆 체크섬/후보 설치 JSON도 고정했다.
- 추출 DLL: `DUCKBILL_ROOT/.cache/release-validation/0.5.4.0/DuckbillFisher.dll`, SHA256 **B9B17B314804C6D753D605714EEC7C76893F6E0CF783D64F77B90AAFD4B0C5F3**.
- 사용자에게 이 고정 추출본의 정상 로드·최신 노트·메모/횟수/프리셋 적용·백업 미리보기/설정·기존 도감/타이머/재로드 대표 확인을 요청했고 **‘확인 완료고 정상’** 응답을 받았다. 위 ZIP/DLL 해시에 연결한 대표 정상 확인이며 전수/다른 캐릭터/장애 확인으로 확대하지 않는다.
- 후속 요청은 업데이트 내역을 더 간결한 사용자 관점으로 고치고 향후 지침에도 반영하는 것이다. AGENTS/배포 문서에 작성 기준을 추가하며, 사용자 선택에 따라 앱 안 문구도 수정해 새 고정 후보 확인을 받는다.
- 게시 순서는 확인한 소스/태그 push → Release의 ZIP/체크섬 공개 → 익명 파일/해시·아이콘 확인 → 설치 JSON 갱신 → 실제 공유 raw URL 확인이다. 현재 공개판/실제 업데이트·전수/다른 캐릭터/장애의 확인 범위는 [STATUS](../../STATUS.md)를 따른다.

## 간결한 문구를 반영한 새 고정본

- 사용자 선택 **‘앱 안 문구도 수정 후 새 DLL 확인’**에 따라 내장 한영/설치 화면/GitHub 안내를 사용자 기능 중심으로 줄였다. 필요한 이전 백업 복원 주의사항은 유지하고 AGENTS/배포 문서에 같은 작성 기준을 추가했다. 최초 후보 이후 기능 코드는 바꾸지 않았다.
- 소스 **6cc17b89b25a886ec974e60660e2d05842200d8b**. 별도 `.cache/release-notes-short-build/` → 정규 Debug, 최소 지원 참조 Release 모두 경고0/오류0. 한영984키·문서114개/로컬 링크570개·diff 검사와 prepare-release.ps1 통과. 테스트 추가/실행 없음.
- 최종 후보 ZIP `artifacts/share/0.5.4.0-notes/DuckbillFisher-0.5.4.0.zip`, **568,894바이트**, SHA256 **981627A30C139F23CE4B3B2193F8247AEA12BE78E434D821469086E563CE50A4**.
- 추출 DLL `DUCKBILL_ROOT/.cache/release-validation/0.5.4.0-notes/DuckbillFisher.dll`, SHA256 **1A13387FDFC138506A943429CFBB8B7E2846C7FE7D3590FD82838354AB970597**.
- 추출7파일/빌드 해시·버전/API15/metadata/URL, 내장3자료·한영984키/값 일치를 재확인했다. 결과 `.cache/release-build/0.5.4.0-notes-validation.json`. 기존 후보/해시는 덮어쓰지 않았다.
- 새 고정본 정상 로드·간결한 노트 확인을 요청했다. 최초 후보의 기능 대표 정상 보고와 구분하며 새 고정본 응답 후 같은 ZIP을 게시한다.

## 첫 공개 범위에 맞춘 최종 문구 후보

- 사용자가 백업 기능의 첫 정식 배포이므로 이전 백업 관련 문구를 삭제하도록 요청했다. 내장 한영/설치 화면/GitHub 노트에서 개발용 백업의 개인 기록 삭제 안내만 제거했다. 파일 판독/복원 동작과 기능 코드는 유지했다. 공개 안내는 실제 배포 이력을 기준으로 작성하도록 배포 지침도 보완했다.
- 최종 소스 **b4193351acc3e31314fc97e79b17ad1e9405ae13**. 별도 `.cache/release-final-notes-build/` → 정규 Debug·최소 지원 Release 경고0/오류0, 한영984키·문서114개/로컬 링크570개·diff/prepare 검사 통과. 테스트 추가/실행 없음.
- 최종 ZIP `artifacts/share/0.5.4.0-final/DuckbillFisher-0.5.4.0.zip`, **568,830바이트**, SHA256 **7FAFEACE0C9042C78F3150B10802F28CA26EEF79BFD963BCF8CA20CE1CE40B8C**.
- 최종 추출 DLL `DUCKBILL_ROOT/.cache/release-validation/0.5.4.0-final/DuckbillFisher.dll`, SHA256 **A0524B48DD68FC9EE607433C0FA5698D4B5582BCF63B12E42534F4704A4F2032**.
- 추출7파일/빌드 해시·버전/API/metadata/URL·내장3자료·한영984키/값 일치를 다시 확인했다. 결과 `.cache/release-build/0.5.4.0-final-validation.json`. 최종 DLL의 정상 로드·해당 문구 제거 확인을 요청했으며 응답 후 같은 ZIP으로 승인된 게시를 이어간다. 이전 후보는 덮어쓰거나 게시하지 않았다.
