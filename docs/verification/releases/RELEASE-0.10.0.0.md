# 0.10.0.0 배포 기록

2026-11-15(KST). 사용자가 이번 지도·상단 메뉴 개선의 배포와 간결한 업데이트 노트를 요청했다. 고정 패키지 실기 질문에는 **“이번 패키지는 배포 후 확인”**으로 답해 이번 ZIP의 게임 확인을 게시 후로 미뤘다. [영어 안내](../../releases/0.10.0.0.md).

## 변경과 검사

- 낚시 수첩의 미발견 일반 낚시터 지도 핀, 상단 메뉴 위 휠의 가로 이동, 좁은 폭의 간이 목록/타이머 아이콘 축약이다. 내장 한영·설치 화면·GitHub 안내에 같은3개 사용자 기능 문장을 사용한다. 개발 중 실패나 내부 구현을 공개판 변경 내역으로 쓰지 않는다.
- KR/글로벌/최소 지원15.0.3.6 참조 Release 빌드 경고0/오류0. prepare-release.ps1 통과, ZIP7파일의 추출본/최소 참조 빌드 해시·Plugin/한국어 리소스/manifest0.10.0.0/API15·내장 자료/고지7개·한영1,112개 모든 컴파일 문자열·노트3문장 일치를 확인했다. 테스트는 추가/실행하지 않았다.
- 최근10개 내장 릴리스만 유지하며0.5.0.0 항목과 전용6키를 제거하고 새3키를 추가했다. PublishedAtUtc는 요청 무렵2026-11-14T23:35:08Z이며 실제 GitHub 공개 시각이 아니다. SDK/lock/저장 형식·기존 자료는 바꾸지 않았다.
- 개발본의 미발견 지도 핀과 상단 메뉴 휠은 사용자 KR 대표 정상 보고를 받았다. 근거는 [지도](../native/GLOBAL-COMPAT.md#첫-kr-클릭-차단과-컴포넌트-검사-보완-2026-11-15)·[메뉴](../catalog/UI.md#최초-휠-무반응과-imgui188-호환-수정-2026-11-15)다. 이 고정 Release ZIP의 실제 로드/두 기능/내장 한영 안내와 발견한 항목·본문 스크롤·바로가기·재로드 경계는 확인 대기다. 이전 버전의 기능별 미확인을 이번 작업으로 완료 처리하지 않는다.

## 고정 패키지와 게시 단계

- ZIP `artifacts/share/0.10.0.0/DuckbillFisher-0.10.0.0.zip`:719,424바이트, SHA256 `5803A4249B877FFFE4776C4E11298A8BDDAEEE8FB4C766B96F77CA4994663C58`.
- 추출 DLL `DUCKBILL_ROOT/.cache/release-validation/0.10.0.0/DuckbillFisher.dll`: SHA256 `600496F95454D66F8A7FE6810755D25245BEF4E9C66DBE09FA7F55B705080E37`.
- 로컬 검사 보고서는 `.cache/release-build/0.10.0.0-validation.json`이다. 고정 ZIP/DLL을 다시 빌드하거나 덮어쓰지 않는다.
- 배포 소스 `539a26dcfe011c626cf6985474a9ce028d1d5594`의 [Plogon run43654399965](https://github.com/PlatyDuckbill/DuckbillFisher/actions/runs/43654399965) 성공 후 같은 소스에 태그 `v0.10.0.0`을 만들었다.
- [Release445678721](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.10.0.0)을2026-11-14T23:43:26Z에 공개했다. 업로드 크기/서버 SHA256과 익명 ZIP·체크섬·태그 아이콘 HTTP200/로컬 해시 일치를 확인했다. 기존 태그/ZIP은 덮어쓰지 않았다.
- 익명 자산 확인 뒤 설치 JSON을0.10.0.0 후보와 동일하게 반영하고 `bf6072acccf0c9713c5a37bbe9f682304d032b35`로 push했다. 이 커밋의 [Plogon run43654893617](https://github.com/PlatyDuckbill/DuckbillFisher/actions/runs/43654893617)도 성공했다.
- 공용 [sync run43654899933](https://github.com/PlatyDuckbill/DuckbillRepo/actions/runs/43654899933)이 성공했고 DuckbillRepo 커밋 `8d13fd6153941168613a2fafaa7e82d7ba65fc57`에 반영됐다.2026-11-14T23:52:16Z에 [실제 공유 raw URL](https://raw.githubusercontent.com/PlatyDuckbill/DuckbillRepo/main/pluginmaster.json)의 익명 HTTP200·0.10.0.0/API15·설치/업데이트/아이콘 주소·노트를 포함한 모든 필드 일치를 확인했다. 캐시가0.9.0.0을 반환하는 동안에는 완료로 처리하지 않았다. DuckbillTweak0.2.3.0·DuckbillCalendar0.4.0.1의 모든 필드는 이전 목록과 동일하다.
- 실제 Dalamud 설치/업데이트·고정 ZIP 게임 확인은 사용자 지시대로 배포 후 확인 대기다. 설치 목록을 새로고침해0.10.0.0으로 업데이트하고, 미발견 일반 낚시터 클릭→지도 핀·넘치는 상단 메뉴 위 휠→좌우 이동·업데이트 안내의 새3항목을 확인한다. 좁은 폭의 간이 목록/타이머 아이콘과 본문 스크롤·발견한 항목의 경계, 글로벌 실기는 개별 확인 대기다.
