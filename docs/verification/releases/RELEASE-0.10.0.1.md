# 0.10.0.1 · 공통 디자인과 새 대표 아이콘

게시: 2026-11-15T10:40:48Z / 2026-11-15 19:40:48 KST. 사용자가 최종 아이콘으로 세 플러그인 배포를 명시 요청했다. 고정 ZIP의 인게임 확인은 하지 않았으며 `InGameVerified=false`를 유지한다.

| 항목 | 값 |
|---|---|
| [공개 Release](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.10.0.1) | ID445981172 · draft=false/prerelease=false |
| 빌드·태그 소스 | `fa4d70e399db09482fdd251191c48c7461cd9a75` |
| 고정 후보 | `artifacts/share/0.10.0.1-final` |
| ZIP | `DuckbillFisher-0.10.0.1.zip` · 729,889바이트/7파일 |
| ZIP SHA256 | `678e3652df2c56283cc98313567ab48baea9505a3f35221234e5d7261ee692b0` |
| 태그 아이콘 SHA256 | `8ff74ceb96779df1630d2c5b88b0f7aee13e3f75094e4abbe1de207fe2951081` |
| 설치 원본 커밋 | `f9d4ae4` · repo/release.json |
| [Plogon](https://github.com/PlatyDuckbill/DuckbillFisher/actions/runs/43704896722) | 배포 소스와 같은 SHA · success |
| [공용 동기화](https://github.com/PlatyDuckbill/DuckbillRepo/actions/runs/43705600164) | success · 공용 `5cb4a754332c0b3ca7bba19edc4e27ba4a4cc679` |

## 변경과 확인 범위

- 스모크/펄, 공통 설정과 기본 접힌 실시간 밝기·입체감·투명도 조절, 대표 기능/기타 개선의 내역 표시, 제목줄 접기/닫기 영역 겹침 수정과 기능 중심 대표 아이콘을 묶었다. 기존 목록/상세 정보와 기록·설정 값은 유지한다. [UI 근거](../catalog/UI.md#제목줄-접기닫기-클릭-영역-겹침-수정-2026-11-15).
- KR·글로벌·검증한 최소 지원15.0.3.6 참조의 직접 Release 빌드가 경고0/오류0다. 게시 ZIP은 최소 지원 참조 산출물이며 재빌드/덮어쓰기 없이 고정했다. 한영1,141키·실제 위성 리소스와 내장 데이터/고지7개가 소스와 일치한다. DLL/manifest/API/ZIP·설치 후보와 영문 내장/manifest/GitHub 노트5항목을 대조했다. 제품 테스트 추가/실행은 하지 않았다.
- 내장 배포 시각2026-11-15T10:32:30Z는 배포 요청 무렵의 값이며 실제 GitHub 공개 시각과 구분한다. 이미 공개한0.10.0.0 태그/ZIP은 보존하고, 이전 기능 묶음의 내장 표기만 사용자 결정0.9.1.0을 유지한다.
- 초안 자산 digest/크기와 태그 소스를 확인한 뒤 공개했다. GitHub 초안이 반환한 임시 untagged 다운로드 주소의 검증은 실패했고, 공개 후 정식 v0.10.0.1 주소로 다시 확인했다. 인증 없는 ZIP·체크섬·태그 아이콘이 모두 HTTP200/원본 해시 일치임을 확인한 뒤 설치 JSON을 게시했다.
- 공용 고정 커밋 URL과 실제 공유 `main/pluginmaster.json` 모두 이름/버전/API/URL/설명/내역 등22필드가 후보 원본과 일치한다. 기존 세 항목을 유지하고 Calendar0.5.0.0/Tweak0.3.0.0도 함께 확인했다. 캐시 우회 URL만으로 완료 처리하지 않았다.
- 실제 Dalamud 설치/업데이트·새 ZIP 로드, 제목줄 버튼/배율 경계, 실시간 조절/저장·재로드와 기존 낚시 기능의 미확인은 대기다. 이전 버전의 미확인 결과를 이번 빌드/다운로드 확인으로 소급 완료하지 않는다.

재개용 산출물/검증·게시 영수증은 `.cache/release-suite-2026-11-15/`, 추출 DLL은 `.cache/release-validation/0.10.0.1-final/DuckbillFisher.dll`에 있다.
