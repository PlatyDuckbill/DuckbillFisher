# 예측 대조 자료

`forecast-reference.json`은 운영 C# 코드로 생성하지 않은 고정 기대값이다. `fishing.kr.v1.json`은 [보존된 초기 조건 자료](../../../data/archive/fishing.kr.v1.json)를 테스트 출력에 연결하며 복사본을 따로 관리하지 않는다. 현재 제품은 v2만 로드한다. 과거 기대값의 의미를 바꾸지 않도록 테스트 입력은 v1을 유지한다.

## 근거와 생성

초기 고정 대조는10종/134회차이며 현재 보존 v1 파일은 이후140종으로 확대된 자료다. 테스트는 fixture의10개 낚시터/어종과 대조 회차의 일치·v1 내 해당 ID 존재·출처 revision/게임 빌드를 확인한다. 이를 현재 전체 어종 예측 검증으로 해석하지 않는다. v2 조건과 등급 자료는 제품 파일을 출력에 링크해 별도로 런타임 판독기 검증을 수행한다.

- 날씨 목표값: Fish Tracker의 [고정 weather.js](https://github.com/icykoneko/ff14-fish-tracker-app/blob/d0e1ab5bba62c2b57356e690a73676cd4751b64d/js/app/weather.js)에 있는 `calculateForecastTarget` 함수 본문을 JavaScript로 실행했다. MIT 고지와 가공 범위는 루트 `THIRD-PARTY-NOTICES.txt`를 따른다.
- 사용한 weather.js SHA256: `83763BFD2E725C7A5F9D891B524C5BE664079F6FE70F48034DA4518D58BD13AB`.
- 입력 조건: 제품의 초기 10종 JSON. 날씨 확률은 KR 빌드 `2026.10.06.0000.0000`의 읽기 전용 시트 덤프에서 추출했다. 10개 낚시터 모두 고정 원본의 누적 확률표와 일치했다.
- 창 계산: `scripts/forecast-reference.mjs`가 ET 시작/종료와 날씨 전환 시각을 모아 구간별 조건을 평가하고 연속 구간을 합친다. 운영 코드의 중첩 구간 교집합 탐색을 복제하지 않는다. 직감/물고기의 눈 등 개인 조건은 적용하지 않는다.
- 시각: 사용자 보고 시각의 밀리초 입력과 이후 7일의 같은 UTC 시각, 첫 창 시작/종료의 -1/0/+1ms. 10종 합계 134건이다. 날씨 목표값은 직전 구간부터 434개 구간이다.
- 열린 창의 start는 현재 날씨 구간 이전으로 소급하지 않는 운영 계약을 적용한다. 종료 시각/다음 창 검증과 과거 원래 시작 시각 복원을 구분한다.

최초 생성에서는 도구의 V8에서 생성 함수와 고정 원본 함수를 실행했고 당시 Node CLI 자체는 실행하지 않았다. 아래 도구는 입력의 모든 어종을 처리하므로 현재140종 v1을 그대로 주면 기존10종 기대값의 단순 재현이 아니다. 기존 고정 자료 재생성에는 해당10종 입력을 별도로 준비하고 변경을 검토한다. 캐시의 KR 덤프는 이전 상세 자료 대조 도구의 출력이며 캐릭터 정보를 포함하지 않는다.

```powershell
node scripts/forecast-reference.mjs .cache/pinned-ten-fish.json .cache/kr-detail-audit.json .cache/fishing-sources/weather.js .cache/forecast-reference-candidate.json
```

기대값은 회귀 검증 자료이므로 매 테스트 실행 시 재생성하지 않는다. 데이터 갱신 시 출처 revision·한섭 확률표·차이를 검토하고 갱신한다. .NET 테스트 자체에는 Node나 게임 설치가 필요 없다.
