# 0.7.0.0 배포 기록

2026-11-04(UTC). 사용자 지시 **"배포까지 하자고 돌리는것도 하고. 니가 다해"**에 따라 글로벌 클라이언트 지원판을 배포했다. **Release 공개·익명 자산·설치 JSON·공용 저장소 동기화 확인 완료.** 실제 Dalamud 업데이트는 확인 대기다. [영어 안내](../../releases/0.7.0.0.md).

## 변경과 자체 점검

- 기능: KR·글로벌 공통 실행 계약(전역 DLL 가드 제거), 언어 독립 로그 결합 계약, 클라이언트 빌드와 무관한 조건 자료 적용, 첫 실행 UI 언어 감지, 배포 시각 현지 시간 표시. 근거는 [호환성 조사](../native/GLOBAL-COMPAT.md)다.
- 인게임: 개발본으로 글로벌 대표 확인과 KR 회귀 대표 확인을 마쳤다(2026-11-04). **후보 ZIP 자체의 인게임 확인은 하지 않았다.** 사용자 지시로 바로 게시했다. 개발본 확인 뒤 바뀐 것은 버전, 업데이트 내역 문구, 0.3.11.0 항목·키 삭제, README다.
- 별도 테스트는 추가/실행하지 않았다.
- PublishedAtUtc는 배포 준비 시각 **2026-11-04T14:45:00Z**이며 실제 공개 시각이 아니다.
- 빌드: KR·글로벌·최소 지원 참조 Release·정규 Debug 모두 경고 0, 오류 0. 한영 1,013키/형식 인자, 문서 검사, diff 검사, prepare-release.ps1 통과.

## 공식 빌드 점검 (Plogon)

- 성공: run **41855137474** (push 자동 실행, 커밋 `5b45e2b`). 공식 컨테이너 `mcr.microsoft.com/dotnet/sdk:10.0.101`에서 Core·Plugin 빌드 경고 0, 오류 0, 아이콘 512×512 확인.
- 로그의 `Could not parse deletions`, `cannot run difft`, `Could not cleanup workspace`는 심사용 diff와 작업 폴더 정리 단계의 부수 오류이며 빌드 결과와 무관하다.
- 앞선 두 번의 실패는 워크플로 설정 문제였다: run 41854395186(setup-dotnet이 Plogon의 rollForward를 무시하고 SDK 10.0.0 설치 실패), run 41854754567(`master/stable` 폴더 없음). 각각 `23e70a2`, `5b45e2b`에서 고쳤다.

## 고정 후보

- ZIP 빌드 소스 `a35ef87`. 태그 소스 `5b45e2b`와는 `.github/workflows/plogon-check.yml`만 다르고 플러그인 입력(src·data·assets·build·global.json)은 같다.
- 최소 지원 참조 `.cache/compat-15.0.3.6/full/`로 직접 Release 빌드한 뒤 prepare-release.ps1을 사용했다.
- ZIP: `artifacts/share/0.7.0.0/DuckbillFisher-0.7.0.0.zip`, **610,613바이트**, SHA256 **3391598CEE981ABC7D7F3C46CDC4550B5ADB8C0B6CA098B27D5186BD0553B9CA**.
- 추출 DLL: `DUCKBILL_ROOT/.cache/release-validation/0.7.0.0/DuckbillFisher.dll`, SHA256 **ED210AE35CC2D1DF13AD203B0C212638380EB279CEFF995584457B172FDAAFE4**.
- ZIP의 7개 파일과 빌드 출력 해시가 일치한다. DLL·manifest 0.7.0.0, API 15, InternalName `DuckbillFisher`, 후보 설치 JSON의 다운로드·태그 아이콘 URL과 Changelog를 대조했다.

## 원격 게시와 설치 목록

- 태그 **v0.7.0.0** → `5b45e2bc8e74947d2e4b905f3c04de35cff39929`(새 태그, 덮어쓰기 없음).
- [공개 Release](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.7.0.0) ID **434089212**, 공개 **2026-11-04T15:14:25Z**. 초안에서 ZIP·체크섬의 서버 크기/digest가 로컬과 같음을 확인한 뒤 공개했다.
- 익명 다운로드: ZIP·체크섬·태그 아이콘 모두 HTTP 200, 원본 SHA256 일치. 결과는 `.cache/release-remote-check/0.7.0.0/`.
- 그 뒤 후보 `release.json`을 `repo/release.json`에 반영했다.
- 설치 JSON 커밋 `76adb2c`를 push한 뒤 DuckbillRepo `sync.yml` run **41856027309**를 수동 실행해 성공했다. DuckbillRepo main은 `0eec0f017f2b46719593dcc55d8cc52e4d2a8a27`이다.
- 실제 공유 raw URL `https://raw.githubusercontent.com/PlatyDuckbill/DuckbillRepo/main/pluginmaster.json`에서 DuckbillFisher 0.7.0.0, API 15, 설치·업데이트 ZIP과 태그 아이콘 URL을 확인했다. DuckbillTweak 0.2.0.4 항목은 그대로다.
- **남은 확인:** KR·글로벌에서 실제 Dalamud 업데이트로 받은 뒤의 정상 로드, 설정·기록 유지, 업데이트 내역 표시.
