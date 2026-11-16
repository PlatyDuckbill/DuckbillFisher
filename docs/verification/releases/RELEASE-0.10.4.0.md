# 0.10.4.0 · 어종 상세와 자료 보완 통합 안내

게시: 2026-11-16T13:31:44Z / 2026-11-16 22:31:44 KST. 사용자가 자료 보완의 공개 배포와 앞선0.10.3.0 업데이트 노트의 통합을 요청했다. 추가 인게임 확인은 생략하도록 지시했으며 `InGameVerified=false`다. 중단 후 재개한 공용 실제 URL 확인은2026-11-17 KST에 완료했다.

| 항목 | 값 |
|---|---|
| [공개 Release](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.10.4.0) | ID447082107 · draft=false/prerelease=false |
| 빌드·태그 소스 | `0c0199d9244d1ef92c7d090fe8a7740cd7741202` |
| 고정 후보 | `artifacts/share/0.10.4.0` |
| ZIP | `DuckbillFisher-0.10.4.0.zip` · 761,300바이트/7파일 |
| ZIP SHA256 | `017d5d684bd4a877ea73e4a4780863dfbc9badbc6177512f866ef4217161e30f` |
| Plugin DLL SHA256 | `bffede89323cb15d003181f6a2347764cdab74d5c12ba55fa56c049f8f1dd615` |
| 태그 아이콘 SHA256 | `8ff74ceb96779df1630d2c5b88b0f7aee13e3f75094e4abbe1de207fe2951081` |
| [Plogon](https://github.com/PlatyDuckbill/DuckbillFisher/actions/runs/43876286287) | 빌드·태그 소스와 같은 SHA · success |
| 설치 원본 커밋 | `f675c4c111f9e73c8ddb6ed83358fb30a3bbb743` · repo/release.json |
| [공용 동기화](https://github.com/PlatyDuckbill/DuckbillRepo/actions/runs/43876674181) | success · `643aedf98c0bc09be0f6e63c2cc886e46cbc9201` |
| 실제 공유 URL 확인 커밋 | `6b6b035ee8d2d4947ec385575844b9d51f51656c` · 이후 Tweak 독립 업데이트 포함 |

## 변경과 검수

- 직감 선행14종 후속 조사에서 둔갑문어의 이중2/삼중3, 폭포 망령의 이중2이상(추정)을 추가했다. 나머지12종은 수량 근거가 부족해 미확인으로 유지한다. 기존306행의 수량·출처·근거를 보존했고 획득력/조건 자료는 변경하지 않았다. [출처·판단·한계](../../development/DATA-SOURCES.md#직감-선행14종-후속-검토-개발본-2026-11-16).
- 사용자 요청에 따라 최신 내장 한영 안내·설치 요약·GitHub 노트는 앞선0.10.3.0의 좁은 폭 상세 개선/최소 획득력2종 보완과 이번 이중·삼중 수량 보완을3문장으로 통합했다. 공개0.10.3.0의 태그/ZIP/노트와 기존15개 내장 안내는 보존한다.
- KR·글로벌·최소 지원15.0.3.6 Release 빌드 경고0·오류0. 최소 지원 참조 산출물을 고정했고 추출7파일/빌드 출력 해시·DLL/위성 리소스/manifest의0.10.4.0/API15·내장 자료/고지7개·한영1,194키 전체 값이 일치한다. 테스트는 추가/실행하지 않았다.
- 실제 고정 Release DLL 로더는 수량308종/획득력352종 모두 `Invalid=false`이며 원본 해시와 일치한다. 일반 수량49종(이중47·삼중21), 먼바다259종(양쪽259)이다. 직감 선행43종 중 한쪽 이상31종, 양쪽 미확인12종과 추정 하한15개를 유지한다. KR/글로벌 시트2,512행 ID/종류 불일치0은 자료 보완 기록을 따른다.
- 내장 시각2026-11-16T13:28:41Z는 배포 요청 무렵의 값이며 실제 GitHub 공개 시각과 구분한다. 고정 ZIP과 추출 DLL은 다시 빌드하지 않는다.
- 정확한 소스의 Plogon 성공 뒤 태그·Release를 만들었다. GitHub 자산 digest/크기·태그 소스를 대조하고 공개 후 익명 ZIP·체크섬·태그 아이콘 HTTP200/원본 해시 일치를 확인한 뒤에만 설치 객체22필드를 게시했다.

## 확인 대기와 재개

- Fisher 동기화 직후 고정 커밋643aedf는 후보22필드 일치, 기존Calendar0.6.0.0/Tweak0.4.0.0 전체 필드와3개 항목 보존을 확인했다. 이후 독립 동기화6b6b035에서 Tweak0.4.0.1로 갱신됐다. 현재 고정 커밋과 우회 인자 없는 실제 공유 [main/pluginmaster.json](https://raw.githubusercontent.com/PlatyDuckbill/DuckbillRepo/main/pluginmaster.json)은 모두 HTTP200/Fisher 후보22필드 일치다. 나머지 두 항목은 현재sources.lock.json의 고정 원본manifest SHA256 및 전체 필드와 일치하므로 과거 버전으로 복원하지 않았다.
- 실제 Dalamud 설치/업데이트·고정 ZIP 로드·새 수량의 게임 표시/실제 어획량·한영/재로드는 대기다. 앞선 개발본의 좁은 폭/두더지귀뚜라미 대표 확인을 새 패키지 확인으로 확대하지 않는다. 이전 버전의 미확인도 유지한다.
- 대표 확인 시 같은 InternalName의 개발 DLL을 언로드하고 `$env:DUCKBILL_ROOT/.cache/release-validation/0.10.4.0/DuckbillFisher.dll`만 로드한다. 둔갑문어 상세의 이중2/삼중3, 폭포 망령의 이중2이상(추정)/삼중 미확인과 한영 전환/재로드 뒤의 구분 유지를 확인한다. 유목메기·E.B.E-9318은 양쪽 수량 미확인이어야 한다.
- 재개 영수증은 `.cache/release-0.10.4.0/`, 내장 자료/파일 대조는 `.cache/release-build/0.10.4.0-validation.json`에 있다.
