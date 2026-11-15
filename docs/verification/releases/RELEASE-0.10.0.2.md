# 0.10.0.2 · 수량 보강과 추정 표식

게시: 2026-11-15T13:04:24Z / 2026-11-15 22:04:24 KST. 사용자가 최종 문구를 승인하고 재검수 후 배포를 요청했다. 고정 ZIP의 인게임 확인은 하지 않았으며 `InGameVerified=false`다.

| 항목 | 값 |
|---|---|
| [공개 Release](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.10.0.2) | ID446066338 · draft=false/prerelease=false |
| 빌드·태그 소스 | `28487c53a2b121b7f1448f2af613d8fa109b649c` |
| 고정 후보 | `artifacts/share/0.10.0.2` |
| ZIP | `DuckbillFisher-0.10.0.2.zip` · 733,345바이트/7파일 |
| ZIP SHA256 | `e1878cf77bc422816170d44fbd0f37fe920bcd7580c3845782684e5d3eacc75e` |
| Plugin DLL SHA256 | `05e01747d0ad7d662753a19da0c9afb9cd758cca26c1e9aefa16f32d9c2940bc` |
| 태그 아이콘 SHA256 | `8ff74ceb96779df1630d2c5b88b0f7aee13e3f75094e4abbe1de207fe2951081` |
| 설치 원본 커밋 | `09825bf7df57b3ec7a84635440bd474ab4f08925` · repo/release.json |
| [Plogon](https://github.com/PlatyDuckbill/DuckbillFisher/actions/runs/43716925276) | 빌드·태그 소스와 같은 SHA · success |
| [공용 동기화](https://github.com/PlatyDuckbill/DuckbillRepo/actions/runs/43717112477) | success · 공용 `45f0d44346a32f2979858ef0b83216877cb52fac` |

## 변경과 검수

- 일반16종의 이중·삼중 수량을 추가하고 화석아로와나의 이중 범위를 보완했다. 일반42종/이중39·삼중20, 먼바다259종/양쪽259다. 원본·권리·누락/한계는 [수량 근거](../../development/DATA-SOURCES.md#수량-빈칸-전수-점검과-보완-2026-11-15)에 있다. 모든 어종의 수량을 완성하거나 현재 획득력의 보장 수량으로 바꾼 것이 아니다.
- 사용자가 화면을 확인한 뒤 요청한 최종 표현을 반영했다. 직접 명시된 수량은 별도 괄호 없이, 준비 절차에서 얻은 하한14개에만 `(추정)`을 붙인다. 미확인은 그대로 유지한다. 툴팁은 공개 자료/실제 수량의 차이와 추정의 뜻만 두 줄로 설명한다. 내장 영문은 `(inferred)`다.
- 표시/파싱 경로·명시적 근거 코드·ID 연결·수량/출처 보존·누락 유지와 좁은 폭 분리 경로를 재검토했다. KR/글로벌 시트2,512행의 ID·종류·도감/먼바다 구분 불일치0, 수량301행 검사와 전수 CSV 재현을 확인했다. 기존 조건1,258종/부분1·작살203행·등급335행 자료 검사도 정상이다. 정적/자료 검수는 실제 게임 화면이나 어획량 재현이 아니다.
- KR·글로벌·검증한 최소 지원15.0.3.6 참조로 직접 Release 빌드 경고0/오류0. 게시 ZIP은 최소 지원 참조 산출물로 고정했다. 추출7파일의 빌드 출력 해시·DLL/위성 리소스/manifest/API15·설치 후보·원본 내장 자료/고지7개·한영1,145키 값 전체가 일치한다. 내장/manifest/설치/GitHub 노트2문장이 일치하며 이전11개 내장 안내는 유지했다. 테스트는 추가/실행하지 않았다.
- 내장 배포 시각2026-11-15T12:58:15Z는 최초 배포 요청 무렵의 값이며 실제 GitHub 공개 시각과 구분한다. 확인 뒤 ZIP/DLL을 재빌드하거나 기존 공개 태그/ZIP을 덮어쓰지 않았다.
- GitHub 자산 digest/크기·태그 소스를 확인하고 공개한 뒤, 익명 ZIP·체크섬·태그 아이콘 HTTP200/원본 해시 일치를 확인했다. 그 다음 후보 설치 객체22필드를 repo/release.json에 반영했다.

## 확인 대기와 재개

- 공용 저장소 고정 커밋과 사용자가 등록한 실제 공유 `main/pluginmaster.json` 모두 HTTP200/후보22필드 일치다. Tweak0.3.0.1·Calendar0.5.0.0의 모든 기존 필드와 전체3개 항목을 보존했으며 캐시 우회 URL만으로 완료 처리하지 않았다.
- 실제 Dalamud 설치/업데이트·새 ZIP 로드, 검은나비조개4/7의 표식 생략, 얇은보자기문어2이상/3이상의 `(추정)`, 미확인/범위·짧은 툴팁·한영/좁은 폭·재로드 확인은 대기다. 실제 어획량과 이전 버전의 미확인은 별도로 유지한다.
- 고정 추출 DLL: `$env:DUCKBILL_ROOT/.cache/release-validation/0.10.0.2/DuckbillFisher.dll`. 설치본과 개발 DLL을 동시에 로드하지 않는다.
- 재개용 검증/게시 영수증은 `.cache/release-0.10.0.2/`, 고정 자료 대조 보고서는 `.cache/release-build/0.10.0.2-validation.json`에 있다.
