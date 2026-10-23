# 개발 워크플로우

## 작업 선택과 완료

1. AGENTS/STATUS와 Git 상태를 확인하고 한 사용자 동작 또는 검증 가능한 가설을 정한다. 요구 동작·변경 범위·근거·검사·인게임 기대 결과를 짧게 정리한다.
2. 필요한 설계만 문서 지도에서 읽는다. 큰 UI 변경은 사용자와 합의하고, 게임 접근 변경은 정확한 KR 근거를 확인한다.
3. 구현·검토 후 변경에 맞는 검사를 수행한다. 문서만 바꾸면 문서 검사, 문자열을 바꾸면 리소스 검사, C#을 바꾸면 직접 빌드다. 테스트는 명시 요청 때만 추가/실행한다.
4. 빌드 성공과 실제 게임 확인을 구분한다. 실패한 출력 대신 이전 DLL로 확인을 계속하지 않는다. 인게임 변경은 재현 단계·기대 결과·DLL 절대 경로를 안내한다.
5. STATUS와 필요한 설계/검증 기록을 갱신한다. git diff/status를 확인해 작업 파일만 명시적으로 스테이징하고 커밋한다. 테스트 미요청 작업은 [skip ci]를 붙인다. 푸시/배포는 승인 범위에 있을 때만 진행한다.

## Windows 빌드

기본 환경은 FFXIV가 실행되는 Windows PC다. 현재 경로/지원 참조는 [HANDOFF](../SESSION-HANDOFF.md#환경과-경로), 새 PC 도구 설치는 [WINDOWS-SETUP](WINDOWS-SETUP.md)을 따른다.

PowerShell 7, 저장소 루트에서 실행한다. [개발 환경 변수](WINDOWS-SETUP.md#개발-환경-변수)를 **해당 세션에서 먼저 설정**하며 오래된 캐시 경로를 다른 PC에 그대로 쓰지 않는다.

```powershell
. ./scripts/enter-dev.ps1
$duckbillRoot = $env:DUCKBILL_ROOT
$duckbillProject = 'src/DuckbillFisher.Plugin/DuckbillFisher.Plugin.csproj'
$duckbillRefs = $env:DALAMUD_HOME
$duckbillBuild = Join-Path $duckbillRoot '.cache/work-build'
dotnet build $duckbillProject --no-restore -c Debug "-p:DalamudLibPath=$duckbillRefs" "-p:OutputPath=$duckbillBuild/"
if ($LASTEXITCODE -ne 0) { throw 'Separate build failed' }
# 별도 빌드 성공 후 정규 출력. 같은 프로젝트의 참조별 빌드는 순서대로 한다.
dotnet build $duckbillProject --no-restore -c Debug "-p:DalamudLibPath=$duckbillRefs"
if ($LASTEXITCODE -ne 0) { throw 'Regular build failed' }
pwsh -NoLogo -NoProfile -File scripts/check-localization.ps1
```

- 각 명령의 종료 코드와 경고/오류를 확인한다. 실패하면 다음 빌드·교체·패키징을 중단하고 원인을 해결한다.
- 신규 체크아웃은 같은 참조로 먼저 dotnet restore --locked-mode를 수행한다. 의존성 변경은 별도 작업이며 lock 파일을 임의 갱신하지 않는다.
- 참조 선택은 명시한 `-p:DalamudLibPath` → `DALAMUD_HOME` 순이다. Plugin SDK와 데이터 도구 모두 같은 환경 변수를 사용한다. `build/DalamudReferences.targets`가 누락 DLL·Plugin API 세대 불일치를 조기에 보고한다. 참조 디렉터리는 한 배포판의 완전한 묶음으로 준비한다.
- 언로드는 사용자 상시 지시의 기본 상태다. 반복 확인하지 않는다. 파일 잠금 실패를 우회하거나 게임을 강제 종료하지 않는다.
- 정규 등록 경로는 저장소 아래 src/DuckbillFisher.Plugin/bin/Debug/DuckbillFisher.dll의 **절대 경로**다. 동시에 다른 개발/배포 경로를 로드하지 않는다.
- 최소 참조/다른 지원 배포판 빌드는 게임 API 변경이나 배포에서 필요한 범위만 수행한다. 일반 upstream CI 성공은 KR 런타임 확인이 아니다.
- scripts/build.ps1과 scripts/test-release.ps1은 테스트를 실행한다. 일반 작업에서는 사용하지 않는다. CI 설정은 .github/workflows/core.yml을 따른다.
- Release는 [DISTRIBUTION](DISTRIBUTION.md#개발자-배포-절차)을 따른다. 배포 권한·ZIP 실기 확인·게시를 각각 구분한다.

## 검사와 진단

```powershell
pwsh -NoLogo -NoProfile -File scripts/check-docs.ps1
pwsh -NoLogo -NoProfile -File scripts/check-localization.ps1
git diff --check
```

문서 검사는 추적 Markdown과 명시한 프로젝트 문서/스킬 경로만 읽는다. 로컬 링크/문서 색인과 프로젝트 스킬의 단일 행 메타데이터 형식을 확인하며 외부 HTTP·게임 API·제품 테스트를 실행하지 않는다. 의미상 모순은 수동 검토가 필요하다.

KR 런타임 업데이트는 출처가 고정된 전체 참조 묶음을 확보한 뒤 `compare-native-runtime.ps1`로 반복 정적 대조를 할 수 있다. 아래는 저장소 루트에서 실행하며 보고서의 부모 폴더는 먼저 준비한다. 네트워크/게임 접근·허용 목록 수정 없이 지정한49개 루트/중첩 타입의 차이를 보고한다. 네이티브 소비 코드가 바뀌면 비교 목록도 검토한다. 동일 결과만으로 자동 허용하지 않으며 게임·소스·스레드·수명 근거와 별도 빌드/실기 범위는 [KR 호환성 기록](../verification/native/NATIVE-RUNTIME-DAL4KR.md)을 따른다.

```powershell
pwsh -NoLogo -NoProfile -File scripts/compare-native-runtime.ps1 `
    -BaselinePath .cache/compat-15.0.3.7/full `
    -CandidatePath .cache/compat-15.0.3.9/full `
    -ReportPath .cache/compat-15.0.3.9/metadata.json
```

진단은 필요한 상태 필드나 한 줄부터 요청한다. 원시 로그와 사람 설명, 자동 검사와 실기 확인을 구분한다. 전체 로그·주소·캐릭터 식별자·자격 증명은 게시하지 않는다. [검증 템플릿](templates/VALIDATION.md)의 관련 항목만 사용한다.

## 요청한 테스트 실행

테스트 추가/실행은 사용자 명시 요청 범위에서만 한다. 테스트 수를 늘리는 것보다 시간 경계·Unknown·중복 알림·시전 전이·설정 이전·저장 실패/격리의 결과를 고정한다.

~~~powershell
dotnet restore tests/DuckbillFisher.Core.Tests/DuckbillFisher.Core.Tests.csproj --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked test restore failed' }
dotnet test tests/DuckbillFisher.Core.Tests/DuckbillFisher.Core.Tests.csproj --no-restore -c Release --logger "trx;LogFileName=contracts.trx" --results-directory .cache/test-results
if ($LASTEXITCODE -ne 0) { throw 'Contract tests failed' }
~~~

- Core.Tests는 기능별 디렉터리로 구분하며 Core와 게임 의존성 없는 Plugin 정책/저장 구현을 검증한다. 소스 링크 대상은 csproj에 명시한다. 게임 설치·Dalamud DLL·Node가 필요 없으며 패키지 버전을 임의로 변경하지 않는다.
- 고정 기대값은 운영 코드로 자동 재생성하지 않는다. 초기10종 예측 대조와 현재 v2 전체 자료의 형식/참조 검사를 구분한다. 자료/출처 변경은 기대값 검토와 함께 진행한다.
- 저장 테스트는 테스트별 임시 경로만 사용한다. 백그라운드 작업은 제한 시간 안에 기다리고 Dispose 뒤 임시 자료를 정리하며 실제 사용자 설정·기록에 접근하지 않는다.
- 현재 [검증 범위와 한계](../verification/ARCHITECTURE-TESTS.md)를 확인한다. 관리 코드 테스트 통과는 네이티브/게임/UI 실기 확인을 대신하지 않는다.

## 문서 관리

| 문서 | 관리하는 정보 | 넣지 않을 정보 |
|---|---|---|
| AGENTS.md | 변하지 않는 행동 규칙·작업 경계 | 버전별 결과·빌드 경로 복제 |
| STATUS.md | 현재 배포/대표 확인·남은 확인·다음 한 작업 | 긴 로그·명령 전문·회고 |
| SESSION-HANDOFF.md | 재개 순서·현재 PC 경로/참조·주의할 로컬 상태 | STATUS 복사·과거 버전별 결과 |
| PRODUCT.md | 합의한 현재 사용자 동작·정책 | 구현 연대기·아직 후보인 기능 |
| ARCHITECTURE.md | 코드 책임·데이터 흐름·수명/실패 계약 | 버전마다 동일 설계 설명 |
| ROADMAP.md | 제공 범위·남은 후보·명시적 보류 | 이미 끝난 작업의 다음 지시 |
| 기능 설계 문서 | 해당 주제의 상세 합의·미결정 사항 | 다른 기능의 공통 규칙 |
| verification/ | 날짜/버전별 근거·검사 결과·실기 확인 범위 | 현재 정책을 덮어쓰는 지시 |
| archive/ | 보존된 과거 상태 사본 | 새로운 전체 문서 사본의 반복 생성 |
| releases/ | 버전별 외부 릴리스 요약 | 내부 개발 로그 |
| verification/data/reports/·data/ | 데이터 잔여 목록·출처·재생성 근거 | 사람의 수집 상태 |

- docs 바로 아래는 README/STATUS/SESSION-HANDOFF 세 파일만 둔다. 현재 명세는 product, 절차는 development, 실제 근거는 verification, 과거 조사는 archive에 둔다.
- 기존 주제가 있으면 **그 문서를 갱신**한다. 작은 UI 수정/재빌드마다 새 검증 파일을 만들지 않는다.
- 새 독립 기능의 장기 설계, 새 KR 계약, 새 배포 버전처럼 독립적으로 참조해야 할 근거일 때만 새 문서를 만든다. docs/README.md 또는 docs/verification/README.md에서 연결한다. 과거 기록은 docs/archive/README.md에서 연결한다.
- 현재 문서에서는 폐기된 설명을 교체한다. 세부 경위는 기존 검증 기록과 Git 이력을 사용하고, 정책 변경 이유가 필요하면 해당 주제에 짧게 남긴다.
- STATUS/HANDOFF는 각각 약 60줄, 하나의 사실은 담당 문서 한 곳에 기록하고 다른 곳은 링크한다. 새 상태 문서 사본은 기본적으로 생성하지 않는다.
- 파일 이동/병합 전 링크와 스크립트 참조를 확인한다. 날짜가 오래됐다는 이유만으로 실제 KR 근거·라이선스·미확인 기록을 삭제하지 않는다.
- 2026-10-18 정리 전 핵심 문서 원문은 Git 커밋 b33f4eb2faa50503dd514a76c3a1e2a23d2e2df8에 보존되어 있다. 필요할 때 특정 파일만 git show로 조회한다.

## 스킬 운영

프로젝트 .agents/skills의 빌드·배포와 KR 조사 스킬은 이 문서들을 필요한 순서로 연결한다. 절차 본문/버전/경로를 스킬에 복제하지 않는다. 일상 수정에 모든 스킬을 읽거나 특정 모델/하위 에이전트를 의무화하지 않는다. 현재 작업에 필요할 때만 적용하며 새 외부 도구나 전역 설정을 자동 설치하지 않는다.
