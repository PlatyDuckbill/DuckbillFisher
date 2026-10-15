# 최소 구조

아래 구조로 시작 프로젝트를 생성했다. 프로젝트명·표시명·InternalName은 `DuckbillFisher`다.

```text
src/
  DuckbillFisher.Core/       # 수집 상태/알림 대상 판정, 관찰 이력; 추후 시간·날씨·타이머
  DuckbillFisher.Plugin/     # 진입점·관찰 UI·공개 Condition 어댑터; 추후 수첩·설정 I/O
tests/
  DuckbillFisher.Core.Tests/
data/                       # 검증된 원본/정제 JSON, 출처와 라이선스
scripts/                    # 환경 확인·빌드·배포 준비·배포 도구 테스트
repo/                       # 친구 공유용 설치 목록, 현재 빈 배열
docs/
```

참조 방향: **Plugin → Core ← Tests**. Core에서 Dalamud, Lumina, ImGui, 게임 DLL을 참조하지 않는다. 처음부터 DI 프레임워크·DB·범용 이벤트 버스·다수의 프로젝트를 추가하지 않는다.

Plugin의 `AssemblyName`은 `DuckbillFisher`다. 주 DLL은 `DuckbillFisher.dll`, 생성 manifest는 `DuckbillFisher.json`이다. 최신 샘플 방식에 따라 manifest 입력 필드는 csproj에 두고 SDK가 JSON을 생성한다.

초기 도구: .NET SDK 10.0.402, Core net10.0, Plugin net10.0-windows / x64, Dalamud.NET.Sdk 15.0.0. .NET SDK와 NuGet lock 파일을 고정했다. 빌드 근거는 REFERENCES, 실제 검증은 STATUS를 따른다.

## 경계

| 부분 | 책임 |
|---|---|
| Core | UTC↔ET, 날씨 창 교집합, 미수집 필터, 알림 중복 방지, 타이머 상태 전이 |
| Game adapter | 로그인/지역/시전/수집 상태를 관찰하여 관리 객체의 snapshot/event로 전달 |
| UI | snapshot을 표시하고 사용자 선택을 전달 |
| Storage | JSON 설정 로드/검증/저장, 버전별 migration, 캐릭터별 상태 분리 |
| Notifications/Map | 허용된 로컬 출력, 사용자 클릭에 따른 지도 열기 |

인터페이스는 실제 테스트와 어댑터 경계에 필요한 것만 만든다. 공개 API 유무는 ENV-03에서 확인하며 `CastStarted` 같은 개념적 이벤트 이름을 실제 Dalamud API로 오인하지 않는다.

## 시간과 성능

- 예측은 UTC 기반 절대 시간, 입질 경과는 Stopwatch 같은 단조 증가 시간으로 처리한다.
- ET/날씨 계산을 렌더링 루프 밖에서 수행하고 지역·날씨 구간·데이터 revision별로 캐시한다.
- 로그인/로그아웃/지역 전환/선택 변경 시 필요한 상태만 갱신한다.
- 적절한 이벤트가 없다면 필요한 상태만 제한된 주기로 관찰하고 성능을 측정한다. 이벤트 API가 있다고 추측하지 않는다.
- UI는 프레임마다 그리더라도 전체 데이터 재계산을 하지 않는다. 긴 예측은 계산 예산과 취소를 지원한다.
- 게임 API와 포인터는 해당 API가 요구하는 스레드/수명 안에서만 사용한다. 백그라운드 계산에는 복사된 값만 넘긴다.

## 캐릭터 상태와 실패 처리

- 수집 상태는 Caught / Uncaught / Unknown. 값 없음은 false가 아니다.
- 수동 override는 자동 판정과 따로 저장하고 '자동 상태 사용'으로 복귀할 수 있게 한다.
- 캐릭터 식별자는 로컬 설정 격리에만 사용하고 로그·공개 테스트 자료에 넣지 않는다.
- 로그인/캐릭터 전환 때 이전 캐릭터의 snapshot과 알림 예약을 폐기한다.
- 호환되지 않는 상태 읽기는 비활성화하고 설명을 표시한다. 수동 체크와 검증된 정적 안내는 계속 제공할 수 있게 한다.
- 이벤트/후크와 타이머는 Dispose 때 해제한다. 종료 중 파일 쓰기/비동기 작업도 정리한다.
- 설정은 스키마 버전, migration, 손상 시 원본 보존, 임시 파일 후 교체 방식의 저장을 고려한다.

## 데이터 모델 검토 항목

정식 스키마는 첫 실제 어종을 검수한 뒤 확정한다.

- dataset schemaVersion, dataVersion, 대상 region/KR patch, source URL/revision/license, 검증일.
- fish item ID와 fish guide row ID의 명시적 구분·매핑. zone/territory/map/fishing spot ID도 혼동하지 않는다.
- 지역·낚시터·좌표, 확장팩, KR 표시명. 이름은 식별자로 사용하지 않는다.
- ET 구간(자정 통과 포함), 현재 날씨 목록, 직전 날씨 목록, AND/OR 결합 의미.
- 미끼와 생미끼 연쇄, 직감 등 추가 조건, 지원/미지원/미확인 상태.
- 훅킹 권장과 적용 조건, 입질 구간/측정 기준/변동 조건/출처. 데이터가 없으면 unknown.

검수는 중복/미존재 ID, 생미끼 연쇄 순환, 잘못된 시간 구간, 잘못된 min/max, 누락 출처를 검사한다. 기존 커뮤니티 자료는 라이선스와 재배포 범위를 확인한 뒤 개발 시점에 정제하여 내장한다.

## 의미 있는 테스트

- ET 자정·날씨 경계 직전/정각/직후, 현재+직전 날씨 교집합, 시간대 변경과 독립성.
- 알려진 UTC/지역/날씨 기대값. 구현을 복사한 계산식을 테스트 정답으로 쓰지 않는다.
- 창 중간 로그인, 이미 지난 사전 알림, 절전 복귀 시 알림 폭주 방지, 같은 창 중복 방지.
- 수집 성공/실패/지연, 수동 override 우선순위, 캐릭터 전환 격리.
- 시전 시작/중복/취소/완료/생미끼/로그아웃 상태 전이, OS 시각 변경에도 경과 타이머 유지.
- Plugin은 Windows에서 로드/언로드/재로드, 채널별 알림 1회, 지도 좌표를 직접 확인한다.
