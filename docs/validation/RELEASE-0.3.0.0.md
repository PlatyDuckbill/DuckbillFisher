# 0.3.0.0 배포 준비

기록: 2026-10-18. 사용자가 빌드와 배포를 요청했다. 이 문서는 Release 빌드/패키지 검사와 인게임 확인/실제 게시를 구분한다. 현재 **ZIP 추출본 확인 대기이며 미게시**다.

## 범위와 소개

- 공개0.2.6.0 이후의 기록 안정화/첫 칸 보관 생미끼 연결/일반 상세1258종 보완과 미끼 준비·찾기를 포함한다. 이전 버전의 실제 확인 범위를 전 기능 검증으로 확대하지 않는다.
- 버전은 기존 미게시0.3.0.0/API15를 유지했다. 영어 manifest 소개와 README에 미끼 준비/수량/페이지 표식·강조를 추가했다. 실행 정책·의존성·게임 접근 가드 변경은 없다.
- [영어 릴리스 요약](../releases/0.3.0.0.md), [미끼 구현/실제 확인 범위](BAIT-PREPARATION.md), [데이터 잔여 보고서](../reports/fishing-data-2026-10-17/remaining.md).

## 실제 실행한 검사

1. `git status`로 사용자 파일을 제외했고 원격main/새 태그 부재를 조회했다. 인증을 출력/파일에 저장하지 않고 Git credential을 메모리에서 사용해 공개 저장소/Push 권한/비보관 상태를 확인했다. 당시 공개 최신은v0.2.6.0, 원격main은3dbf947이었다.
2. `dotnet build src/DuckbillFisher.Plugin/DuckbillFisher.Plugin.csproj --no-restore -c Release -p:DalamudLibPath=C:/Users/hyogu/Downloads/DuckbillFisher/.cache/compat-15.0.3.6/full/ -p:OutputPath=C:/Users/hyogu/Downloads/DuckbillFisher/.cache/bait-preparation-release/`: 경고0/오류0.
3. `scripts/check-localization.ps1`: 한영606키/형식 인자 일치. 테스트 추가/실행 없음. `scripts/build.ps1`은 실행하지 않았다.
4. `scripts/prepare-release.ps1 -PackagePath .cache/bait-preparation-release/DuckbillFisher/latest.zip -AssetBaseUrl https://github.com/PlatyDuckbill/DuckbillFisher/releases/download/v0.3.0.0 -OutputDirectory artifacts/share/0.3.0.0`: 통과. 후보 설치 JSON만 생성했고 공개repo JSON은 수정하지 않았다.
5. ZIP7개 항목을`.cache/release-validation/0.3.0.0/`에 새로 추출했다. 모든 추출 파일/Release 출력 SHA256이 같았다. Plugin/ko0.3.0.0, Core1.0.0.0/API15, 설명17줄이다. 공급 DLL/SDK는 ZIP에 포함하지 않는다.
6. Mono.Cecil로 산출물의 참조 Dalamud15.0.3.6/FFXIVClientStructs7.57.0.9376/Interop1.0.0.0을 확인했다. 내장 FishingData/FishClassifications/ThirdPartyNotices는 각 저장소 원본 해시와 일치했다. 설치/업데이트/아이콘 URL이v0.3.0.0을 가리키는1개 항목을 확인했다. 태그가 아직 없으므로 해당 공개 다운로드 성공으로 표현하지 않는다.

## 고정 산출물

- ZIP: `artifacts/share/0.3.0.0/DuckbillFisher-0.3.0.0.zip`, **370,524바이트**.
- SHA256: **4EC80617542A66D55AB5FA649FAAB4DB963DB173BE3E7215DBC3BFB9430C605F**.
- 체크섬: 같은 폴더의`DuckbillFisher-0.3.0.0.zip.sha256`.
- 실제 확인 DLL: `C:\Users\hyogu\Downloads\DuckbillFisher\.cache\release-validation\0.3.0.0\DuckbillFisher.dll`.
- 인게임 확인 뒤 같은 ZIP을 게시한다. 실행 파일을 재빌드/변경하면 확인 범위도 다시 구분한다.

## 남은 확인과 게시 순서

- 사용자에게 Release 추출본으로 목록/도감/간이 목록, 준비 수량/다른 페이지 찾기, 낚시1회 타이머/기록, 한영 전환과 재로드 보존을 요청했다. 현재 답변 대기다. 앞선 Debug 피드백이나 이번 배포 요청을 이 ZIP의 실행 완료로 취급하지 않는다.
- 확인 뒤 기록 갱신/커밋/정상push→확인한 소스의v0.3.0.0 태그→GitHub Release에 고정 ZIP/체크섬 게시→익명 다운로드/해시/태그 아이콘 확인→후보repo JSON 반영/커밋/push→실제 공유 URL의0.3.0.0/API15 및 설치/업데이트 URL 확인 순서다.
- 현재 새 태그/Release/ZIP 업로드/공개 JSON 변경/push는 하지 않았다. 공개0.2.6.0은 유지한다. 다른 캐릭터/전체 테마/전체 미확인 어종/친구 PC 새 기능 전수 확인을 이번 패키지 확인에 포함했다고 주장하지 않는다.
