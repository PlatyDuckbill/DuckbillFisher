# 0.10.1.0 · 먼바다 미끼 추천

게시: 2026-11-15T14:47:50Z / 2026-11-15 23:47:50 KST. 사용자가 최종 무결성·정합성·출처 점검 뒤 배포를 요청했다. 고정 ZIP의 인게임 확인은 하지 않았으며 `InGameVerified=false`다.

| 항목 | 값 |
|---|---|
| [공개 Release](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.10.1.0) | ID446130476 · draft=false/prerelease=false |
| 빌드·태그 소스 | `e9665cc50958d025950cb2e0198558328427d8c3` |
| 고정 후보 | `artifacts/share/0.10.1.0` |
| ZIP | `DuckbillFisher-0.10.1.0.zip` · 751,445바이트/7파일 |
| ZIP SHA256 | `c1994176cc73930d898201661857c4ed530715ad89094dd521110e2ade4d0f5b` |
| Plugin DLL SHA256 | `1eb99f7173d53d18aa9441f4d5f43488758e0d58b0d09818235823832ab9592a` |
| 태그 아이콘 SHA256 | `8ff74ceb96779df1630d2c5b88b0f7aee13e3f75094e4abbe1de207fe2951081` |
| [Plogon](https://github.com/PlatyDuckbill/DuckbillFisher/actions/runs/43726193673) | 빌드·태그 소스와 같은 SHA · success |
| 설치 원본 커밋 | `da0e776fafe80c7a83c0105a9f7f9333e87ea8ab` · repo/release.json |
| [공용 동기화](https://github.com/PlatyDuckbill/DuckbillRepo/actions/runs/43726345918) | success · 공용 `9d9a1e94158d60618f23b0c73051bc11ec7c413e` |

## 변경과 검수

- 해역·항로 시간대·일반/환해류·확인된 날씨에 따라 일반 해역은 환해류 유발, 환해류는 직접 미끼 비직감 어종의 기본 점수×삼중 수량 하한으로 추천한다. 남은90초 이하에는 유발 우선을 해제한다. 직감은 실제 대상 미확인의 미끼 후보와 생미끼 경로로 표시하며 출항편 준비 미끼를 보강했다. [추천 계약](../../product/UI-UX-REDESIGN.md#조건별-미끼-추천-개발본-2026-11-15).
- 새 다운로드의 CSV2개·분류 근거 해시 일치, 미끼260행/조건260행/수량259종 재생성 일치, 기존52행 보존·중복/연결 충돌0, 생미끼11행의 같은 해역 경로·순환0을 확인했다. 원본/권리/가공 범위·FAQ 제한 근거와 한계는 [자료 출처](../../development/DATA-SOURCES.md#먼바다-조건별-미끼-추천-개발본-2026-11-15)에 있다. 원저자의 관찰 자료를 공식 보장값이나 독립 실측으로 확대하지 않으며 공개 재사용 라이선스는 미확인이다.
- KR·글로벌·최소 지원15.0.3.6 참조로 직접 Release 빌드 경고0·오류0. 게시 ZIP은 최소 지원 참조 산출물로 고정했다. 추출7파일과 빌드 출력 해시, DLL/위성 리소스/manifest의0.10.1.0/API15, 내장 자료/고지7개, 한영1,160키 전체 값이 일치한다. 내장/manifest/설치 후보/GitHub 노트3문장이 일치하며 기존12개 내장 안내를 보존했다. 테스트는 추가/실행하지 않았다.
- 내장 시각2026-11-15T14:04:13Z는 배포 요청 무렵의 값이며 실제 GitHub 공개 시각과 구분한다. 고정 ZIP/추출 DLL은 확인 뒤 다시 빌드하지 않는다.
- GitHub 자산 digest/크기·태그 소스를 확인하고 공개한 뒤, 익명 ZIP·체크섬·태그 아이콘 HTTP200/원본 해시 일치를 확인했다. 그 다음 후보 설치 객체22필드를 repo/release.json에 반영했다.

## 확인 대기와 재개

- 공용 저장소 고정 커밋과 사용자가 등록한 실제 공유 `main/pluginmaster.json` 모두 HTTP200/후보22필드 일치다. Tweak0.3.0.1·Calendar0.5.0.0의 기존 전체 필드와3개 항목을 보존했으며 캐시 우회 URL만으로 완료 처리하지 않았다.
- 실제 Dalamud 설치/업데이트·새 ZIP 로드와 일반/환해류·남은90초 경계·기항지 이동·직감 활성/종료·생미끼/특수 미끼·출항 전 준비·한영/좁은 폭·재로드는 확인 대기다. [재현·기대 결과](../catalog/UI.md#먼바다-조건별-미끼-추천-개발본-2026-11-15). 이전 버전의 미확인은 새 빌드로 완료 처리하지 않는다.
- 고정 추출 DLL: `$env:DUCKBILL_ROOT/.cache/release-validation/0.10.1.0/DuckbillFisher.dll`. 설치본과 개발 DLL을 동시에 로드하지 않는다.
- 재개용 영수증은 `.cache/release-0.10.1.0/`, 내장 자료/파일 대조 보고서는 `.cache/release-build/0.10.1.0-validation.json`에 있다.
