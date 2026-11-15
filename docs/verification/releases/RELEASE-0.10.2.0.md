# 0.10.2.0 · 먼바다 목표와 준비

게시: 2026-11-15T15:41:01Z / 2026-11-16 00:41:01 KST. 사용자가 일반 사용·업적 활용·현재 화면의3회 개선 뒤 배포를 요청했다. 고정 ZIP의 실제 게임 확인은 하지 않았으며 `InGameVerified=false`다.

| 항목 | 값 |
|---|---|
| [공개 Release](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.10.2.0) | ID446164726 · draft=false/prerelease=false |
| 빌드·태그 소스 | `eb56fef90d0aed92e6060e7f5766f119961aa129` |
| 고정 후보 | `artifacts/share/0.10.2.0` |
| ZIP | `DuckbillFisher-0.10.2.0.zip` · 757,432바이트/7파일 |
| ZIP SHA256 | `45ba1ce12933f5df7faeb3d7887f6422b93e83ac57b7efa85fc7d7bdb2e88e06` |
| Plugin DLL SHA256 | `5da4b2dedbcc310cb0e0582e5eea1bb3d500b149c1ab74796f1d5786c3493f49` |
| 태그 아이콘 SHA256 | `8ff74ceb96779df1630d2c5b88b0f7aee13e3f75094e4abbe1de207fe2951081` |
| [Plogon](https://github.com/PlatyDuckbill/DuckbillFisher/actions/runs/43731127381) | 빌드·태그 소스와 같은 SHA · success |
| 설치 원본 커밋 | `91b7f05400949c0eb85c9d6078566872debcb641` · repo/release.json |
| [공용 동기화](https://github.com/PlatyDuckbill/DuckbillRepo/actions/runs/43731280499) | success · 공용 `e260c4303ee3dde56858d2052c741dce34f46696` |

## 변경과 검수

- 기존 추천 영역에서 기본 또는12개 어종군 목표를 고르고 출항편의 대상 어종/미끼 경로를 미리 본다. 기본 미끼3종과 선택 직감 미끼를 분리하고 미소지·소지 미확인 및 좁은 폭 세로 배치를 제공한다. 목표가 없는 조건에서는 없다고 표시하고 캐릭터 전환에는 기본 추천으로 초기화한다. [제품 계약](../../product/UI-UX-REDESIGN.md#먼바다-일반-사용어종군-목표화면-3회-개선-개발본-2026-11-16).
- 원본 Species의 명시 분류78행/12군만 도입하고 빈182행은 미분류로 유지한다. 원본 CSV2개 재다운로드 해시·양쪽 시트 연결/재생성·기존52행/260행 사실 보존을 확인했다. 업적 ID·필요 수량·완료 상태·파티 진행도는 판정하지 않으며 원저자 관찰 자료의 권리/정확도 한계는 [출처 기록](../../development/DATA-SOURCES.md#먼바다-어종군과-준비-미끼-개발본-2026-11-16)을 따른다.
- KR 별도→정규/글로벌 Debug 및 한영1,184키/내장 자료 대조·실제 표시 함수의 한영/좁은 폭/확대/스모크·펄 대표 렌더를 마쳤다. 가상 자료·대체 텍스처/찾기 비활성의 배치 렌더이며 실제 게임 입력이나 승선 확인을 대신하지 않는다. [3회 검수](../catalog/UI.md#먼바다-3회-개선-개발본-2026-11-16).
- 게시 후보는 KR·글로벌·최소 지원15.0.3.6 Release 빌드 경고0·오류0. 최소 지원 참조 산출물을 고정했고 추출7파일/빌드 출력 해시·DLL/위성 리소스/manifest의0.10.2.0/API15·내장 자료/고지7개·한영1,189키 전체 값이 일치한다. 내장/manifest/설치 후보/GitHub 노트4문장이 일치하고 기존13개 내장 안내를 보존했다. 테스트는 추가/실행하지 않았다.
- 내장 시각2026-11-15T15:38:12Z는 배포 요청 무렵의 값이며 실제 GitHub 공개 시각과 구분한다. 확인한 고정 ZIP/추출 DLL은 다시 빌드하지 않는다.
- GitHub 자산 digest/크기·태그 소스를 확인한 뒤 공개했다. 익명 ZIP·체크섬·태그 아이콘 HTTP200/원본 해시 일치 확인 후에만 후보 설치 객체22필드를 repo/release.json에 반영했다.

## 확인 대기와 재개

- 공용 저장소 고정 커밋과 사용자가 등록한 실제 공유 `main/pluginmaster.json` 모두 HTTP200/후보22필드 일치다. 첫 실제 URL 조회는 캐시된0.10.1.0을 반환했으며 캐시 갱신 뒤 우회 인자 없는 일반 요청으로0.10.2.0과 모든 필드를 다시 확인했다. Tweak0.3.0.1·Calendar0.5.0.0의 기존 전체 필드와3개 항목을 보존했다.

- 실제 Dalamud 설치/업데이트·새 ZIP 로드, 기본/어종군 목표 전환과 후보 없음, 출항 전 준비/선택 직감 미끼, 승선 후 일반/환해류/기항지 전환, 캐릭터 전환 초기화, 한영/좁은 폭/확대·실제 미끼 찾기·재로드 확인은 대기다. [재현·기대 결과](../catalog/UI.md#먼바다-3회-개선-개발본-2026-11-16). 새 게시로 이전 버전의 미확인을 완료 처리하지 않는다.
- 고정 추출 DLL: `$env:DUCKBILL_ROOT/.cache/release-validation/0.10.2.0/DuckbillFisher.dll`. 설치본과 개발 DLL을 동시에 로드하지 않는다.
- 재개 영수증은 `.cache/release-0.10.2.0/`, 내장 자료/파일 대조는 `.cache/release-build/0.10.2.0-validation.json`에 있다.
