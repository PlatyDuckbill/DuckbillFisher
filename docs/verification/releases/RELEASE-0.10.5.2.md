# 0.10.5.2 · 이동 취소 방지 수정

게시: 2026-11-25T12:20:49Z / 2026-11-25 21:20:49 KST. 사용자가 개발본 실기 전에 “배포까지 해줘 그냥 일단”으로 배포를 지시했다. 개발본·고정 Release 모두 실기 전이며 `InGameVerified=false`, `SkipInGameApproved=true`로 기록한다.

| 항목 | 값 |
|---|---|
| [공개 Release](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.10.5.2) | ID458075415 · draft=false/prerelease=false |
| 빌드·태그 소스 | `d575567421a33c0977de6b9c583c35b50305a0b6` (수정 `85d8b28` 포함) |
| 고정 후보 | `artifacts/share/0.10.5.2` |
| ZIP | `DuckbillFisher-0.10.5.2.zip` · 764,628바이트/7파일 |
| ZIP SHA256 | `9b4acd301dc551c90612dffc895153fe82abbe588bf7d99329f2dbb119008272` |
| Plugin DLL SHA256 | `225586e1cdf1c49d962f0837f545fe7a301dc819d2bb75de1c83c88db50512e8` |
| 고정 Release DLL | `DUCKBILL_ROOT/.cache/release-validation/0.10.5.2/DuckbillFisher.dll` |
| [Plogon](https://github.com/PlatyDuckbill/DuckbillFisher/actions/runs/45612590275) | 빌드 소스와 같은 SHA · success |
| 설치 원본 커밋 | `64c55fca70a0158f065546ce75aee40f639b8e3f` · repo/release.json |
| [공용 동기화](https://github.com/PlatyDuckbill/DuckbillRepo/actions/runs/45612945697) | 수동 실행 success · `99896a5c0f1e697cdd5f762686f16b0a68229b79` |

## 변경과 검수

- 옵션을 켜 둔 채 시작하면 껐다 켜야 동작하던 이동 취소 방지의 후크 생성을 첫 낚시 시작 시점으로 미뤘다. 원인은 가설이다. [근거](../native/GLOBAL-COMPAT.md#켜-둔-상태로-시작하면-무동작하는-문제-보완-개발본-2026-11-25).
- KR·글로벌·최소 지원15.0.3.6 Release 빌드 경고0/오류0, locked restore와 한영1,205키/인자 일치. 최소 지원 참조 산출물만 고정 ZIP으로 준비했다. 추출7파일/빌드 출력 해시·DLL/위성 리소스/manifest의0.10.5.2/API15·내장 자료/고지7개·한영 전체 리소스 값이 일치한다. 테스트는 추가/실행하지 않았다.
- 내장 한영 안내·설치 화면·GitHub 노트는 같은 한 문장이며 기존18개 내장 안내는 보존한다. 내장 시각2026-11-25T12:17:18Z는 배포 요청 무렵의 값이다. 데이터·의존성·저장 schema26은 바꾸지 않았다.
- 재개/검사 근거는 `.cache/release-0.10.5.2/`와 `.cache/release-build/0.10.5.2-validation.json`이다. 고정 ZIP/추출 DLL을 다시 빌드해 교체하지 않는다.

## 게시와 확인 대기

- 새 태그/Release가 없음을 확인하고 정확한 소스의 Plogon 성공 뒤 태그/Release를 공개했다. 업로드 자산 digest/크기·태그 소스, 익명 ZIP/체크섬/태그 아이콘 HTTP200과 원본 해시 일치를 확인한 다음 설치 JSON을 게시했다.
- 공용 동기화 성공 후 고정 커밋 `99896a5c0f1e697cdd5f762686f16b0a68229b79`과 우회 인자 없는 실제 공유 [main/pluginmaster.json](https://raw.githubusercontent.com/PlatyDuckbill/DuckbillRepo/main/pluginmaster.json)의 HTTP200/Fisher22필드 일치를 확인했다. 실제 URL은 처음 세 번 캐시된0.10.5.1을 반환했고 네 번째(약3분 뒤)에0.10.5.2를 확인했다. Calendar0.6.0.0/Tweak0.4.0.1과3개 항목을 보존했고 sources.lock의 원본 manifest SHA256·전체 필드도 일치한다. 근거는 `.cache/release-0.10.5.2/shared-verification.json`이다.
- **실기 대기:** 실제 업데이트 후 옵션을 켜 둔 채 게임 재시작→로그인→어부→첫 투척 중 이동으로 낚싯대 유지(껐다 켜지 않음), ‘중단’ 정상 종료, 옵션 해제 시 종전 이동 취소, 재로드 후 저장값을 확인한다. 실패 시 메인 설정 실패 코드와 dalamud.log의 `FishingMovementGuard` 줄을 확인한다.
