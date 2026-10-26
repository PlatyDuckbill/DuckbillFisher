# 0.6.0.0 배포 기록

2026-10-26. 사용자 **‘이상 없음. 마지막으로 너가 자체적으로 점검 후 배포 진행하자’** 요청으로 자체 검토와 배포 후보 준비를 진행했다. d834004 개발 DLL의 새 탭 표시는 대표 정상 확인이며, 아래 Release 추출본은 별도로 **‘정상 로드·대표 확인 완료’** 응답을 받았다. **동일 ZIP의 Release 공개/익명 자산·설치 JSON·실제 공유 raw URL 확인 완료**이며 실제 Dalamud 업데이트는 별도 대기다. [영어 안내](../../releases/0.6.0.0.md).

## 변경과 자체 점검

- 출현 일정·생미끼/직감 조건 중심의 세 추천 탭, 수동 갱신과 조회 기간/필터, 현재 낚시터 미수집 터주·터주왕 로컬 알림, 출현 시점/최대120분 사전 알림, 채팅 상세 링크/잔여 시간, 공통 도감 갱신 안내를 포함한다. [추천 계약](../../product/NEXT-FISH-RECOMMENDATION.md#0600-구현-계약)·[알림 계약](../alerts/SPAWN-ALERTS.md#현재-낚시터-출현-알림-0600-2026-10-25).
- 선별/정렬과 일정 수집의 분리, Unknown 보존·기존 설정/백업 기본값, 작업 취소·세대/캐릭터 경계와 종료, 출현 회차 중복 키·0분/사전 알림 분리, 공통 경고/한영 안내를 코드로 재검토했다. 추가 기능 수정은 없으며 실제 준비 성공·전수 조건/알림 확인으로 확대하지 않는다.
- 앱0.6.0.0/API15·설정schema24·기록schema2다. SDK/의존성/lock·정적 자료·네이티브 가드/게임 DLL 조합은 이번 배포 준비에서 바꾸지 않았다. 자동 게임 조작·런타임 HTTP는 추가하지 않았다.
- 이전 사용자 승인 범위의 추천31개와 별도 알림20/설정32개 기록은 해당 [추천 검증](../forecast/RECOMMENDATIONS.md)을 따른다. 이번 배포 준비에서 테스트를 추가하거나 재실행하지 않았다.
- 내장 한영/설치 화면/영어 안내의 기능 범위를 대조했다. PublishedAtUtc는 배포 요청 무렵 **2026-10-26T05:34:15Z / 한국14:34:15**이며 실제 GitHub 공개 시각이 아니다. 확인한 후보를 날짜 보완만으로 다시 빌드하지 않는다.
- 현재 KR 참조 별도 → 정규 Debug와 최소 지원 참조 Release 모두 경고0/오류0이다. 한영1,256키/형식 인자·문서/diff·prepare-release.ps1 검사를 통과했다.
- 원격 사전 조회에서 저장소 공개/push 권한과 main **2e38b2205b32141e7bb00dbad6db84fa0885d09f**를 확인했다. 새v0.6.0.0 태그는 없고 공개 Release는404다. 소스 준비 커밋까지 로컬28커밋 앞/뒤0이며 자격 증명은 메모리에서만 사용했다.

## 고정 후보

- 소스 **b6870242effcdaa899cb89d4f88971a4c29cc02d**. 최소 지원 참조 `.cache/compat-15.0.3.6/full/`로 직접 Release 빌드한 뒤 prepare-release.ps1을 사용했다.
- ZIP: `artifacts/share/0.6.0.0/DuckbillFisher-0.6.0.0.zip`, **628,536바이트**, SHA256 **6DDDD1563C1BBE5D1F894A6D84BC71B55909A1E61FB8378BA4C8BDA9AA00408C**.
- 추출 DLL: `DUCKBILL_ROOT/.cache/release-validation/0.6.0.0/DuckbillFisher.dll`, SHA256 **0FEA06E6A84C93E6C589D672D2B59EE01298C67F7F0EFAD5CB46E6A8F9F08188**.
- ZIP의7파일과 빌드 출력 해시, Plugin/한국어 위성 DLL0.6.0.0·Core 식별자, 프로젝트/manifest/후보 설치 JSON의 버전/API15·Changelog/설명·다운로드/태그 아이콘 URL을 대조했다. 내장 조건/등급/고지3자료의 원본 해시와 한영 각각1,256키/값이 일치한다. 결과 `.cache/release-build/0.6.0.0-validation.json`.
- 추출 DLL의 정상 로드·추천 화면·업데이트 안내·기존 설정 유지 대표 확인을 요청했고 사용자 **‘정상 로드·대표 확인 완료’** 응답을 받았다. 위 ZIP/DLL 해시에 연결한 대표 확인이며 실행 파일 재빌드 없이 소스/태그 → Release 공개 → 익명 자산/해시 → 설치 JSON → 실제 공유 raw URL 순서로 진행한다. 패키지 검사와 개발본의 대표 확인으로 대신한 결과가 아니다.

## 남은 확인

- 실제 Dalamud 저장소 업데이트가 남아 있다.
- 모든 언어/폭/배율·다른 캐릭터/장애 조합, 첫 투척/반복 투척/재방문/다음 회차/즐겨찾기0분 중복의 전수 실기 대기는 [UI](../catalog/UI.md)·[알림](../alerts/SPAWN-ALERTS.md)을 따른다. 대표 정상 결과로 자동 해소하지 않는다.

## 원격 게시와 설치 목록

- 소스 **b6870242effcdaa899cb89d4f88971a4c29cc02d**를 가리키는 새v0.6.0.0 태그와 승인 범위 main을 정상 push했다. [공개 Release](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.6.0.0)는 ID**423892077**, 실제 공개 **2026-10-26T05:39:33Z / 한국14:39:33**다. 기존 버전/태그/자산은 덮어쓰지 않았다.
- 초안 ZIP/체크섬의 서버digest·크기를 확인한 뒤 공개했다. 익명 정식 ZIP·체크섬·태그 아이콘 HTTP200/원본 SHA256 일치와 공개 본문/영어 안내 일치를 확인했다. 실행 파일 재빌드 없이 사용자가 확인한 ZIP을 그대로 게시했다. 결과 `.cache/release-remote-check/0.6.0.0/`의 publish-state.json/public-release.json/download-check.json.
- 익명 자산 확인 후 후보 설치 JSON을 **ac71cb563edcb10abf3121690b55790e58180cc9**로 커밋·push했다. 인증 값은 메모리에서만 사용했다.
- 쿼리 없는 실제 공유 raw URL은 **2026-10-26T05:40:07.1260229Z**부터 캐시의0.5.4.0을 반환했다. 같은 URL의 일반 익명 요청으로 **2026-10-26T05:45:34.1811339Z / 한국14:45:34**에 HTTP200·0.6.0.0/API15·이름/설명/노트·설치/업데이트/아이콘 링크·LastUpdate 등22필드의 후보 JSON 일치를 확인했다. public-index-attempts.jsonl/public-index-check.json/public-pluginmaster.json에 기록했다. GitHub API에서도 최신 Release와 커밋된 설치 목록0.6.0.0을 확인했으며 실제 공유 URL 확인을 API/캐시 우회 URL로 대신하지 않았다. 실제 Dalamud 설치/업데이트는 별도다.
