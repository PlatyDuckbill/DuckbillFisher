# DuckbillFisher

한국 서버 FFXIV용 터주 정보·알림 Dalamud 플러그인. 프로젝트명·표시명·InternalName은 **DuckbillFisher**다.

`/duckbillfisher` 창에서 낚시 상태를 관찰하고 기록을 복사할 수 있다. 0.1.1.0에는 수첩 열림을 계기로 게임의 정적 어종 목록·ID를 읽는 [진단 탭](docs/validation/ENV-03-FISH-WIN.md)을 추가했고 한섭 기본 동작을 확인했다. 수집 여부 자동 판정·내장 터주 데이터·캐스팅 타이머는 아직 없다. 기존 낚시 상태 관찰의 [한섭 기본 검증도 통과](docs/validation/ENV-03-WIN-RESULTS.md)했으며 로그인/재로드 추가 경계와 정밀 입질 시점은 미확인이다.

0.1.2.0에는 조사한 KR 빌드에서 선택 어종의 실제 수집 비트를 읽는 기능을 추가했다. [수집/미수집 각 1종 대조는 통과](docs/validation/ENV-03-FISH-NATIVE.md)했으며, 전체 자동 필터에 연결하기 전 로그인 준비 상태와 추가 경계를 검증한다.

0.1.3.0부터 한국어·영어 UI 선택과 리소스 기반 문자열을 제공한다. 추가 문구/언어는 [다국어 지침](docs/LOCALIZATION.md)을 따른다. UI 언어와 게임 시트의 어종 이름은 별개다.

0.1.4.0에는 **어종 목록** 탭의 첫 버전을 추가했다. 게임 아이콘·이름을 행으로 표시하고 전체 스크롤, 이름/ID 검색, 이름순/ID순 정렬, 수첩 포함 여부 필터를 제공한다. 도감을 열지 않아도 정적 목록을 로드할 수 있다. 수집 상태·낚시터·미끼·출현 시간 상세 정보는 후속 작업이며 [첫 목록 검증](docs/validation/MVP-01-LIST.md)은 인게임 대기다. 0.1.3.0의 [재접속 초기화와 새 조회](docs/validation/ENV-03-FISH-SESSION.md)는 확인했다.

최소 배포 목표는 친구가 커스텀 저장소 URL로 설치·업데이트하는 것이고, 장기 목표는 공식 등록이다. 게임 입력 대행과 플러그인 런타임 외부 통신은 구현하지 않는다.

0.1.4.0 검색은 사용자 확인을 받았다. 0.1.4.1은 ID 0과 이름/참조 확인 불가 행을 일반 목록에서 제외하고 진단 옵션·보고서로 보존한다. 목록의 수집 표시는 연동 예정이며 실제 수집 상태가 아니다. 수정 후 화면은 인게임 검증 대기다.

## 지금 시작하기

- 0.1.5.0: 어종 선택 시 낚시터·지도 핀 버튼과 초기 10종의 미끼·낚아채기·조건·시간/날씨 창 예측을 표시한다. 상세 조건 필터와 출현 시간순 정렬을 추가했다. 입질 초 수 통계는 후속으로 미뤘다. [데이터 출처/고지](data/README.md)와 [인게임 검증 대기 범위](docs/validation/MVP-01-DETAILS.md)를 따른다.
- 앞으로 FFXIV가 실행되는 **Windows PC에서 직접 편집·빌드·게임 확인**을 진행한다. 이번 세션을 이어받을 때는 [Windows 세션 인수인계](docs/SESSION-HANDOFF.md)부터 읽는다.
- Windows 사용자는 [Windows 준비 및 첫 실행](docs/WINDOWS-SETUP.md)을 따른다.
- 현재 상태와 다음 한 작업은 [STATUS](docs/STATUS.md)에 있다.
- 친구 공유 방식은 [배포 안내](docs/DISTRIBUTION.md)에 있다. 아직 활성화된 배포 URL은 없다.

## 개발 도구

| 도구/대상 | 고정 버전 |
|---|---|
| .NET SDK | 10.0.402 (`global.json`) |
| Dalamud.NET.Sdk | 15.0.0 |
| Core / Plugin | net10.0 / net10.0-windows, Plugin x64 |
| 컴파일 확인용 Dalamud | upstream 15.0.3.6, revision/SHA256 고정 |
| 빌드 스크립트 | PowerShell 7 이상 |

게임과 독립된 테스트:

```sh
dotnet test tests/DuckbillFisher.Core.Tests/DuckbillFisher.Core.Tests.csproj -c Release
```

Windows 개발 환경은 PowerShell 7과 .NET SDK 10.0.402를 사용한다. 초기 Mac의 저장소 내부 SDK/캐시는 Git에 포함하지 않으며, 재개 절차는 Windows 설치 도구를 기준으로 한다.

Plugin 빌드(PowerShell 7):

```powershell
pwsh -File scripts/build.ps1 -DalamudPath "Dalamud.dll이 있는 실제 폴더" -Configuration Release
```

출력 DLL은 `src/DuckbillFisher.Plugin/bin/Release/DuckbillFisher.dll`, ZIP은 같은 폴더의 `DuckbillFisher/latest.zip`이다. upstream 컴파일 성공은 한섭 실행 성공과 구분한다.

## 문서

- [AGENTS.md](AGENTS.md): AI 개발 규칙.
- [워크플로우](docs/WORKFLOW.md): Windows에서 편집·빌드·게임 확인, 모델/스킬 운용.
- [세션 인수인계](docs/SESSION-HANDOFF.md): 확인한 환경, 소스/이력 이동, 새 세션 시작, 다음 한 작업.
- [제품 범위](docs/PRODUCT.md), [구조](docs/ARCHITECTURE.md), [로드맵](docs/ROADMAP.md).
- [검증 기록 템플릿](docs/templates/VALIDATION.md), [실행한 검증](docs/validation/ENV-02-local.md).
- [참고 자료](docs/REFERENCES.md): 버전 근거와 제출 정책.
