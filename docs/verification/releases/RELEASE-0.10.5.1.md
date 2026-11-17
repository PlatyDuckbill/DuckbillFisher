# 0.10.5.1 · 낚시 편의 개선

게시: 2026-11-17T12:36:51Z / 2026-11-17 21:36:51 KST. 사용자가 배포를 요청하고 고정 Release의 추가 실기를 생략하도록 명시했다. `InGameVerified=false`이며 개발본 `eafb351`의 KR 대표 정상 보고와 구분한다.

| 항목 | 값 |
|---|---|
| [공개 Release](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.10.5.1) | ID448610250 · draft=false/prerelease=false |
| 빌드·태그 소스 | `7db6a9eedc9aa292c06d815cef09b22448c1b3fb` |
| 고정 후보 | `artifacts/share/0.10.5.1` |
| ZIP | `DuckbillFisher-0.10.5.1.zip` · 764,592바이트/7파일 |
| ZIP SHA256 | `81fff046e719541a077e76ba9ea4a39c09d8d2d8f7722c467a725a035bded86f` |
| Plugin DLL SHA256 | `7ec8769b1ee1f03b5f61609f7ab377b609fb00f59720a415fcba7b8eb3492a61` |
| 고정 Release DLL | `DUCKBILL_ROOT/.cache/release-validation/0.10.5.1/DuckbillFisher.dll` |
| [Plogon](https://github.com/PlatyDuckbill/DuckbillFisher/actions/runs/44084473687) | 빌드 소스와 같은 SHA · success |
| 설치 원본 커밋 | `e9f59a509e8382acc1fbe2f4dd78afaab717cd98` · repo/release.json |
| [공용 동기화](https://github.com/PlatyDuckbill/DuckbillRepo/actions/runs/44084863776) | success · `777e5703ca6edd24603582d148ee51c6f9a54ced` |

## 변경과 검수

- 이동 취소 방지의 후크 진입 주소 비교와 생성 실패 복구를 보완하고 옵션을 메인 설정→낚시 편의로 옮겼다. 타이머 시계는 타이머 설정 화면을 열고 닫는다. [실행 계약·개발본 대표 정상 보고](../native/GLOBAL-COMPAT.md#간헐적-무동작과-옵션-위치-보완-개발본-2026-11-17).
- KR·글로벌·최소 지원15.0.3.6 Release 빌드 경고0/오류0, locked restore와 한영1,204키/인자 일치. 최소 지원 참조 산출물만 고정 ZIP으로 준비했다. 추출7파일/빌드 출력 해시·DLL/위성 리소스/manifest의0.10.5.1/API15·내장 자료/고지7개·한영 전체 리소스 값이 일치한다. 테스트는 추가/실행하지 않았다.
- 내장 한영 안내·설치 화면·GitHub 노트는 같은 세 변경을 항목별로 설명하며 기존17개 내장 안내는 보존한다. 내장 시각2026-11-17T12:33:06Z는 배포 요청 무렵의 값이다. 데이터·의존성·저장 schema26은 바꾸지 않았다.
- 재개/검사 근거는 `.cache/release-0.10.5.1/`와 `.cache/release-build/0.10.5.1-validation.json`이다. 고정 ZIP/추출 DLL을 다시 빌드해 교체하지 않는다.

## 게시와 확인 대기

- 사용자가 고정 DLL 경로와 추가 실기/생략 선택을 안내받은 뒤 “추가 실기는 생략하고 배포”를 선택했다. `InGameVerified=false`, `SkipInGameApproved=true`로 기록하고 같은 고정 ZIP을 게시했다.
- 새 태그/Release가 없음을 확인하고 정확한 소스의 Plogon 성공 뒤 태그/Release를 공개했다. 업로드 자산 digest/크기·태그 소스, 익명 ZIP/체크섬/태그 아이콘 HTTP200과 원본 해시 일치를 확인한 다음 설치 JSON을 게시했다.
- 공용 동기화 성공 후 고정 커밋과 우회 인자 없는 실제 공유 [main/pluginmaster.json](https://raw.githubusercontent.com/PlatyDuckbill/DuckbillRepo/main/pluginmaster.json)의 HTTP200/Fisher22필드 일치를 확인했다. 초기 실제 URL의 캐시된0.10.5.0 응답 이후 다시 확인해0.10.5.1을 확인했다. Calendar0.6.0.0/Tweak0.4.0.1과3개 항목을 보존했고 고정 sources.lock의 원본 manifest SHA256·전체 필드도 일치한다. 근거는 `.cache/release-0.10.5.1/shared-verification.json`이다.
- 추가 확인 시 고정 DLL을 단독 로드하고 시계의 설정 열기/닫기, 메인 설정의 이동 취소 방지, 한 번 켠 뒤 낚시 유지와 ‘중단’ 기술로 종료를 확인한다. 옵션 해제·저장/재로드·한영과 글로벌/세부 단계는 개별 확인이며 개발본 대표 정상으로 전수 완료 처리하지 않는다.
