# 0.10.5.0 · 낚시 이동 취소 방지

게시: 2026-11-16T16:45:58Z / 2026-11-17 01:45:58 KST. 사용자가 배포를 요청하고 고정 Release의 추가 실기를 생략하도록 지시했다. `InGameVerified=false`이며 개발본 대표 정상과 구분한다.

| 항목 | 값 |
|---|---|
| [공개 Release](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.10.5.0) | ID447316908 · draft=false/prerelease=false |
| 빌드·태그 소스 | `d4509b5b77990b918898f4728a181a7a3adc58e2` |
| 고정 후보 | `artifacts/share/0.10.5.0` |
| ZIP | `DuckbillFisher-0.10.5.0.zip` · 763,995바이트/7파일 |
| ZIP SHA256 | `a6a8f74d3584c5f4cbb79888e802b21609759b4f7161085ab9df2b97c33f2e3b` |
| Plugin DLL SHA256 | `9c514388aac42293a92d8067f97c76225bed3009782217852190fb48de16fcd2` |
| 고정 Release DLL | `DUCKBILL_ROOT/.cache/release-validation/0.10.5.0/DuckbillFisher.dll` |
| [Plogon](https://github.com/PlatyDuckbill/DuckbillFisher/actions/runs/43910799213) | 빌드·태그 소스와 같은 SHA · success |
| 설치 원본 커밋 | `bd66291bf83c93cfc8d696c6f459da0cf1bade27` · repo/release.json |
| [공용 동기화](https://github.com/PlatyDuckbill/DuckbillRepo/actions/runs/43911172134) | success · `858385af5af5349ce1c1094f85a9ad9a5e02d811` |

## 변경과 검수

- 타이머 설정→캐스팅 알림에 기본 꺼짐인 ‘낚시 중 이동으로 인한 취소 방지’를 추가한다. 이동 취소 처리만 차단하고 ‘중단’ 기술로 종료하는 옵션이며 채팅/카메라/낚시 기술 입력과 게임 자체의 강제 중단은 유지한다. [실행 계약과 개발본 대표 확인](../native/GLOBAL-COMPAT.md#낚시-이동-취소-방지-개발본-2026-11-17).
- 개발본 `8756357`의 KR 사용자 “잘됨” 보고는 개발본 대표 확인이다. 고정 Release ZIP 확인으로 자동 확대하지 않으며, 사용자가 후보 고정 후 “추가 실기는 생략하고 배포”를 선택했다. 고정 Release의 실기를 완료로 처리하지 않았다.
- KR·글로벌·최소 지원15.0.3.6 Release 빌드 경고0/오류0, locked restore와 한영1,199키/인자 일치. 최소 지원 참조 산출물만 고정 ZIP으로 준비했다. 추출7파일/빌드 출력 해시·DLL/위성 리소스/manifest의0.10.5.0/API15·내장 자료/고지7개·한영 전체 리소스 값이 일치한다. 테스트는 추가/실행하지 않았다.
- 내장 한영 안내·설치 화면·GitHub 노트는 이동 취소 방지 옵션을 한 문장으로 설명하며 기존16개 내장 안내는 보존한다. 내장 시각2026-11-16T16:41:50Z는 배포 요청 무렵의 값이며 GitHub 공개 시각과 구분한다. 기존 데이터·의존성·저장 schema26은 바꾸지 않았다.
- 재개/검사 근거는 `.cache/release-0.10.5.0/`와 `.cache/release-build/0.10.5.0-validation.json`이다. 고정 ZIP/추출 DLL을 확인 뒤 다시 빌드하지 않는다.

## 게시와 확인 대기

- 새 태그/Release가 없음을 조회하고 정확한 소스의 Plogon 성공과 사용자 실기 생략 지시 후 태그/Release를 공개했다. 업로드 자산 digest/크기·태그 소스, 익명 ZIP/체크섬/태그 아이콘 HTTP200 및 원본 해시 일치를 확인한 뒤 설치 JSON을 게시했다.
- 공용 동기화 성공 후의 고정 커밋과 우회 인자 없는 실제 공유 [main/pluginmaster.json](https://raw.githubusercontent.com/PlatyDuckbill/DuckbillRepo/main/pluginmaster.json)은 HTTP200/Fisher 후보22필드 일치다. 초기 실제 URL의 캐시된0.10.4.0 응답 이후 다시 확인해0.10.5.0을 확인했다. Calendar0.6.0.0/Tweak0.4.0.1과3개 항목을 보존하고 현재 고정 sources.lock의 원본manifest SHA256·전체 필드도 일치한다. 근거는 `.cache/release-0.10.5.0/shared-verification.json`이다.
- 고정 DLL을 단독 로드하고 이동 취소 방지를 켠 뒤 이동 키/마우스 양쪽 버튼으로 낚시 유지, ‘중단’ 기술로 정상 종료를 확인한다. 기본 꺼짐·옵션 해제·저장/한영/재로드·글로벌과 실제 설치 업데이트는 별도 확인이며 개발본 대표 정상으로 전수 완료 처리하지 않는다.
