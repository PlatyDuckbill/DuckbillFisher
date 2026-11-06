# 0.9.0.0 배포 기록

2026-11-06(UTC). 사용자는 고정 후보의 실기 요청에 **“그냥 배포하고 테스트할게”**라고 답했다. 이번 버전은 인게임 확인을 배포 후 진행하는 명시적 지시로 게시를 진행하며 빌드/자료 검사와 구분한다. [영어 안내](../../releases/0.9.0.0.md).

## 변경과 검사

- 그라파이트/페이퍼 선택·상세 최소 획득력/납품 대상/구간·수량 자료 보완·먼바다 Beta의 일정/관찰/기본 미끼/현재 해역 목록과 조건 후보·수첩 슬롯 순서다. Beta는 미확인을 후보에 남기며 점수/업적 최적화나 자동 낚시는 제공하지 않는다.
- KR/글로벌/최소 지원15.0.3.6 참조 Release 빌드 경고0/오류0. 한영1,115키 검사와 prepare-release.ps1 통과. ZIP7파일과 최소 참조 빌드 출력 해시, Plugin/한국어 리소스/manifest0.9.0.0/API15, 내장 자료/고지7개 원본 해시를 대조했다. 내장 한영·설치 화면·영어 릴리스 요약은 같은7개 기능 문장이다.
- 최근10개 내장 릴리스만 유지하도록0.4.1.0 항목과 전용5개 키를 제거했다. PublishedAtUtc는 요청 시각2026-11-06T12:28:36Z이며 실제 GitHub 공개 시각이 아니다. 제품 테스트는 추가/실행하지 않았다.
- 앞선 Debug의 KR 대표 확인(수량/획득력/납품 표시·기록→상세)과 오프라인 대표 렌더는 각 기능 기록에 보존한다. 이 고정 ZIP의 실제 시전/어획·먼바다 승선/해역 시간/환해류·조건 후보·페이퍼/설정 복원은 배포 후 확인 대기다.

## 고정 후보와 게시 단계

- ZIP `artifacts/share/0.9.0.0-rc2/DuckbillFisher-0.9.0.0.zip`:711,608바이트, SHA256 `536DB092BE1F8B5DEE6C6D651DE502DE838A946F31C22CAD1EF8AEBE6418C84B`.
- 추출 DLL `DUCKBILL_ROOT/.cache/release-validation/0.9.0.0-rc2/DuckbillFisher.dll`: SHA256 `722EE5DF3BC4AEA0AA5D9B4521BC923DE4195F169C28C6C5E3F74D1157636D87`.
- 후보를 다시 빌드하거나 덮어쓰지 않는다. 소스 커밋·Plogon·태그/Release·익명 자산·설치 JSON/공용 raw 확인 결과는 아래와 같다.

- 사용자 요청으로 업데이트 내역을 사용자 동선 순서로 다시 다듬어 최종 후보를 rc2 경로에 별도로 준비했다. 앞선 후보는 게시하지 않았으며 원본을 덮어쓰지 않았다. 영어7문장의 내장 리소스/manifest/릴리스 요약 완전 일치와 대응 한국어를 확인했다.

- 소스 커밋55a88255a85a761999220b3c8e292cacbcc5f9f6을 push하고 Plogon run42219256709의 성공을 확인한 뒤 같은 SHA에 v0.9.0.0 태그를 게시했다.
- [Release](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.9.0.0) ID436487263을2026-11-06T12:40:54Z에 공개했다. 초안에 올린 ZIP/체크섬의 서버 digest·크기를 대조한 뒤 공개했다.
- 익명 ZIP·체크섬·태그 아이콘은 모두HTTP200/원본 SHA256 일치다. 설치 목록 갱신 전 공용 raw의 DuckbillTweak0.2.0.4 항목을 별도 보관했다. 익명 확인 후 후보release.json을 원본repo/release.json에 반영했다.

- 설치 목록 커밋1744e388ead2a5a97d6a9cc156a87d65dc5694ad을 push한 뒤 Plogon run42219793413도 성공했다. DuckbillRepo sync.yml run42219794690 수동 동기화 성공, 공용 저장소 커밋74ad49f380a2da1af65e27ec664d329f55f51439이다.
- **2026-11-06T12:47:07Z**, 실제 공유 URL https://raw.githubusercontent.com/PlatyDuckbill/DuckbillRepo/main/pluginmaster.json 의 일반 GET에서HTTP200/0.9.0.0을 확인했다. 후보 설치 JSON의 모든 필드(API·주소·아이콘·7개 안내 포함)가 같고 DuckbillTweak0.2.0.4 전체 필드는 동기화 전과 같다. 동기화 직후0.8.0.0 캐시를 관찰했고 재검증 후 같은 URL의 일반 응답까지 확인했다. 쿼리 우회 URL만으로 완료하지 않았다.
- 실제 Dalamud 설치 업데이트와 이 고정 산출물의 인게임 기능 확인은 사용자 지시대로 배포 후 확인 대기다.
