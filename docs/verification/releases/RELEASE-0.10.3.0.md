# 0.10.3.0 · 어종 상세와 자료 보완

게시: 2026-11-16T13:10:30Z / 2026-11-16 22:10:30 KST. 사용자가 푸시·배포를 요청했고 고정 추출 DLL 확인 요청에는 추가 확인 없이 배포하도록 지시했다. 개발본의 KR 대표 확인과 구분하여 `InGameVerified=false`로 게시했다.

| 항목 | 값 |
|---|---|
| [공개 Release](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.10.3.0) | ID447057672 · draft=false/prerelease=false |
| 빌드·태그 소스 | `ce429c18b804a91868cdb4f9b77cb74654d77471` |
| 고정 후보 | `artifacts/share/0.10.3.0` |
| ZIP | `DuckbillFisher-0.10.3.0.zip` · 759,976바이트/6파일 |
| ZIP SHA256 | `26f9cd08ee6f9c2ba33c50223a608c47a6473e441b2b314a8d7ba3495d8afc8f` |
| Plugin DLL SHA256 | `152d62e3e6afc261e051501afc02346fa592407368825e45a6a354271e1943fd` |
| 태그 아이콘 SHA256 | `8ff74ceb96779df1630d2c5b88b0f7aee13e3f75094e4abbe1de207fe2951081` |
| [Plogon](https://github.com/PlatyDuckbill/DuckbillFisher/actions/runs/43872384016) | 빌드·태그 소스와 같은 SHA · success |
| 설치 원본 커밋 | `d6e5768` · repo/release.json |
| [공용 동기화](https://github.com/PlatyDuckbill/DuckbillRepo/actions/runs/43873347423) | success · 공용 `e096d879c26e4c7f9a1ad45b897ce3bd41adcd3f` |

## 변경과 검수

- 좁은 폭과 확대 배율에서 어종 상세의 동작 버튼·수집 상태·개인 기록 입력과 미끼/조건 설명을 읽기 쉽게 배치한다. KR 개발본은 사용자 대표 정상이며 세부 조합 전체나 고정 Release 확인으로 확대하지 않는다. [상세 검수](../catalog/UI.md#상세-좁은-폭배율-대응-개발본-2026-11-16).
- 샤크네이도680·용암제왕730의 최소 획득력과 일반 어종6종의 이중 낚아채기 수량을 추가했다. 기존 획득력350개·수량·추정 하한14개를 보존했다. 두더지귀뚜라미 이중3~4/삼중5~6 표시는 KR 개발본 대표 정상이며 실제 어획량과 나머지 추가값은 미확인이다. [출처·전수 집계·한계](../../development/DATA-SOURCES.md#최소-획득력직감-선행-수량-누락-보완-2026-11-16).
- KR·글로벌·최소 지원15.0.3.6 Release 빌드 경고0·오류0. 최소 지원 참조 산출물을 고정했고 추출6파일/빌드 출력 해시·DLL/위성 리소스/manifest의0.10.3.0/API15·내장 자료/고지7개·한영1,193키 전체 값이 일치한다. 최소 획득력352종/수량306종의 실제 Release 자료 로더도 유효하다. 테스트는 추가/실행하지 않았다.
- 내장/manifest/설치 후보/GitHub 노트3문장이 일치하고 기존14개 내장 안내를 보존했다. 내장 시각2026-11-16T13:01:55Z는 배포 요청 무렵의 값이며 실제 GitHub 공개 시각과 구분한다. 고정 ZIP과 추출 DLL을 다시 빌드하지 않았다.
- 정확한 소스의 Plogon 성공 뒤 태그와 Release를 만들었다. GitHub 자산 digest/크기·태그 소스를 대조하고 공개 후 익명 ZIP·체크섬·태그 아이콘 HTTP200/해시 일치를 확인한 뒤에만 설치 객체22필드를 게시했다.

## 확인 대기와 재개

- 공용 저장소의 고정 커밋과 우회 인자 없는 실제 공유 [main/pluginmaster.json](https://raw.githubusercontent.com/PlatyDuckbill/DuckbillRepo/main/pluginmaster.json) 모두 HTTP200/후보22필드 일치다. Fisher0.10.3.0·Calendar0.6.0.0·Tweak0.4.0.0의3개 항목과 나머지 두 플러그인의 기존 전체 필드를 보존했다.
- 실제 Dalamud 설치/업데이트·고정 ZIP 로드·한영/재로드와 추가값/좁은 폭의 세부 조합은 대기다. 이전 버전의 먼바다 등 미확인을 새 게시로 완료 처리하지 않는다.
- 대표 확인 시 같은 InternalName의 개발 DLL을 언로드하고 `$env:DUCKBILL_ROOT/.cache/release-validation/0.10.3.0/DuckbillFisher.dll`만 로드한다. 좁은 폭/확대에서 상세와 개인 기록 조작이 겹치지 않는지, 샤크네이도/용암제왕의 최소 획득력680/730과 줄삼치의 이중4마리가 표시되는지 확인한다. 두더지귀뚜라미는 이중3~4/삼중5~6마리이며 언어 변경과 재로드 뒤에도 표시가 유지되어야 한다.
- 재개 영수증은 `.cache/release-0.10.3.0/`, 내장 자료/파일 대조는 `.cache/release-build/0.10.3.0-validation.json`에 있다.
