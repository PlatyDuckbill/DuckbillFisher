# KR Dalamud 15.0.3.7 호환성: 0.2.5.0

기록일: 2026-10-17. 게임/오프셋/SDK/의존성 갱신이 아닌, 정확한 KR 런타임 조합 추가 작업이다.

## 실제 증상과 원인

- 첫 배포 뒤 사용자는 설치 절차 완료를 알렸고, 친구 PC에서 도감 직접 열기·도감을 수동으로 연 뒤 수집 갱신·캐스팅 타이머가 모두 동작하지 않는다고 보고했다.
- 친구 진단: Plugin0.2.4.0 / Dalamud15.0.3.7 / 게임2026.10.06.0000.0000 / 로그인·IsLoaded=True / FishGuide2 열림과 2512행 로드 관찰됨 / native compatibility=NativeDllMismatch. 게임 도감 UI 감지 실패가 아니라 공통 네이티브 호환성 차단임을 확인했다.
- 기존 코드는 개발 PC의15.0.3.6 파일3개의 SHA256/MVID만 허용했다. NativeFishCollectionReader·NativeFishingActivityReader·FishGuideOpenService·효과음은 이 판정을 공유한다. 원본 상태를 그대로 유지하면서 UI 열기만 반복해도 해결되지 않는 문제였다.

## 확보한 KR 배포와 출처

- 기존 설치 `version.json`의 DownloadUrl이 가리키는 배포 저장소: [wasdcxz/dalamud.assets](https://github.com/wasdcxz/dalamud.assets).
- 고정 revision **a96111f929280b45c84eb7fdf689830177627779**.
- [VersionInfo_dalamud.json](https://github.com/wasdcxz/dalamud.assets/blob/a96111f929280b45c84eb7fdf689830177627779/VersionInfo_dalamud.json): AssemblyVersion15.0.3.7 / SupportedGameVer2026.10.06.0000.0000 / Runtime10.0.2.
- [7.57-15.0.3.7.zip](https://raw.githubusercontent.com/wasdcxz/dalamud.assets/a96111f929280b45c84eb7fdf689830177627779/7.57-15.0.3.7.zip), 60,790,060바이트, SHA256 **AEEC1DAD79B8DA74F2D31E6A428DDCD3A03CFBFFCC5607D9381491470D40EA80**. 캐시에만 내려받아 읽었고 설치된 Dalamud 파일은 변경하지 않았다.
- Dalamud ProductVersion의 소스 revision: [f3eeb5e18bd982d1ae928cbf6dd0653997d3a916](https://github.com/goatcorp/Dalamud/tree/f3eeb5e18bd982d1ae928cbf6dd0653997d3a916), 기존 기록896d83190 대비18커밋. 관련 PlayerState/GameGui/공개 서비스/Chat.LogMessage 파일 변경 없음. upstream은 AGPL3이며 코드 전문은 도입하지 않았다.
- ClientStructs ProductVersion: [d6397a76b3a63c2b247025dc296653182b718ca6](https://github.com/aers/FFXIVClientStructs/tree/d6397a76b3a63c2b247025dc296653182b718ca6), MIT. 기존 cae2c3755 대비10커밋의 소스 변경은 Skeleton/지도/분석 자료이며 플러그인의 낚시 접근 대상 소스는 변하지 않았다. KR 패치 빌드 차이는 아래 실제 DLL 메타데이터/IL 대조로 별도 확인했다.
- 소스·DLL은 조사 캐시에만 두며 배포 ZIP에 포함하지 않는다. 런타임 다운로드·시그니처 스캔·주소 쓰기 없음.

| 모듈 | AssemblyVersion | MVID | SHA256 |
|---|---|---|---|
| Dalamud | 15.0.3.7 | 8e662101-af12-485e-90bf-ddc19df3f357 | CFEE2C278B0AF6619503546B78F57B62539399F9BC60B089006F0408A595A275 |
| FFXIVClientStructs | 7.57.0.9386 | 768c4b04-4ac2-4dae-a5ef-263d2fffbdaa | B4DC56FF088217013871CECF4630AE596407A083D5F78DCC30FE4AE95E3A4BD1 |
| InteropGenerator.Runtime | 1.0.0.0 | 7d2ee25e-a51e-41b3-8097-2ad986272d0b | B158F0A863B4AABB0855EDE5CA824C1FA7B9694DBADB3C65D489246F861A6028 |

## 네이티브 접근 근거 대조

설치된 Mono.Cecil로 디스크 DLL만 읽었다. 타입/중첩 타입의 크기·정렬·필드 타입/오프셋/상수·속성 자료·메서드 시그니처/구현 속성·이름으로 해석한 IL·로컬 타입을 JSON으로 대조했다. 플러그인/게임 코드를 실행하거나 게임 메모리를 읽는 검사가 아니다.

| 타입 | 크기 | 15.0.3.6 DLL과 비교 |
|---|---:|---|
| PlayerState | 2344 | 메타데이터/IL 동일 |
| AgentFishGuide 및 중첩 자료·함수 포인터 | 424 | 메타데이터/IL 동일 |
| EventFramework 및 중첩 주소 | 18432 | 메타데이터/IL 동일 |
| EventHandlerModule | 192 | 메타데이터/IL 동일 |
| FishingEventHandler 및 중첩 주소 | 560 | 메타데이터/IL 동일 |
| UIGlobals 및 효과음 함수 포인터 | 1 | 메타데이터/IL 동일 |
| StdVector 제네릭 | 제네릭 | 메타데이터/IL 동일 |
| Interop BitArray | 제네릭/관리 형식 | 메타데이터/IL 동일 |

기존 KR 실행 파일에서 확인한 PlayerState/EventFramework 주소 시그니처·도감 열기 함수 근거는 [캐스팅 조사](MVP-02-CASTING-PROBE.md), [도감 열기](MVP-01-GUIDE-OPEN.md)를 따른다. 이번 접근 대상 구현이 같고 게임 버전도 같지만, 새 런타임에서의 실제 실행 성공과 같다고 표현하지 않는다.

## 구현과 유지 경계

- NativeRuntimeCompatibility에 **15.0.3.6과15.0.3.7 두 정확한 조합**을 기록한다. 버전·로드된 MVID·디스크 SHA256 모두를 확인하며 서로 다른 조합의 DLL을 섞어 허용하지 않는다. 검증되지 않은 새 버전이나 패치 파일은 계속 차단한다.
- NativeFishCollectionReader는 기존 읽기·스레드·캐릭터·수명/행 범위 검사를 유지하고 공통 호환성 결과만 재사용한다. 타이머·도감 열기·효과음의 기존 경계도 유지한다. 동적으로 파일을 허용하거나 가드를 우회하지 않는다.
- 차단된 환경에서 도감을 열라는 안내 대신 공통 차단 사유를 표시한다. Unknown을 미수집으로 바꾸거나 기존 수동 지정/필터/알림/저장 정책을 변경하지 않는다.
- 짧은 진단 summary/v6, collection/v3와 수첩 진단 native/v3에 NativeRuntime/NativeProfile/NativeStatus/MismatchedModules 한 줄을 추가했다. 필드·버전·상태 코드는 언어와 무관하다. UI는 한영 리소스2키 추가다.

## 빌드·패키지와 실행 확인

- SDK15.0.0/lock 파일 유지. 기존15.0.3.6 참조의 별도 Debug 빌드와 확보한15.0.3.7 참조의 별도 Debug 빌드 모두 경고0/오류0.
- 기존15.0.3.6 참조로 별도 Release 빌드 경고0/오류0. 리소스 검사557키/한영 형식 인자 일치. 테스트 추가·실행 없음.
- prepare-release.ps1로 `artifacts/share/0.2.5.0/` ZIP/체크섬/후보 JSON 생성. ZIP SHA256 **5105B7B50292CB018032A2B3F6984B1672E94AD1093C05440BD401BD52EE0B4D**. `.cache/release-validation/0.2.5.0/` 추출본 전 항목이 ZIP과 해시 동일하며 Plugin/ko0.2.5.0·Core1.0.0.0·manifest/API15 일치다.
- 사용자는 위 추출본을 사용한 도감 연동·타이머/기록 확인 요청에 **‘잘 됨’**으로 대표 확인을 알렸다. 제공된 결과만으로 실행 PC의 런타임이15.0.3.7이었다고 가정하지 않는다. 친구15.0.3.7의 업데이트 후 세 기능 확인은 별도다.
- 확인한 ZIP은 재빌드/변경하지 않고 같은 SHA256으로 게시한다. 정규 개발 DLL·현재 로드 파일을 직접 교체하지 않았으며 기존0.2.4.0 태그/릴리스는 유지한다. 공개 ZIP→익명 다운로드 해시→저장소 JSON 업데이트 순서를 따른다.
