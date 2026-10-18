# 문자열과 다국어 유지보수

공통 원칙은 [AGENTS](../AGENTS.md#다국어)를 따른다. 이 문서는 리소스 수정 위치와 검사 절차를 관리하며, 키 개수·버전별 결과는 해당 검증 기록에만 둔다.

## 구현 위치

- src/DuckbillFisher.Plugin/Localization/Strings.resx: 기본 영어.
- Strings.ko.resx: 한국어 번역.
- TextKey: 코드에서 사용하는 안정적인 키 enum.
- UiText: ResourceManager 조회·형식 인자·언어 선택·고정 ImGui ID.
- UiMessage: 번역 결과 대신 키/인자를 보관하는 표시 메시지.

언어 선택은 설정에 저장한다. 호스트 CurrentCulture/CurrentUICulture를 바꾸지 않는다. ko-KR은 ko, 미지원 언어/누락 번역은 기본 리소스로 돌아간다. 게임 이름·아이템·지역은 실제 클라이언트 시트 언어를 유지하며 UI 언어와 구분한다.

```csharp
UiText.Get(TextKey.GuideRows, total, inLog, maxId);
new UiMessage(TextKey.RowsReadError, exceptionType);
UiText.Label(TextKey.ReloadFish, "ReloadFish");
```

## 수정 순서

1. TextKey와 기본/한국어 리소스의 키·형식 인자를 함께 수정한다. 문장 전체를 리소스에 넣고 상태 분기에 표시 문자열을 사용하지 않는다.
2. 표시 코드는 UiText/UiMessage를 사용한다. ImGui의 고정 ID, 목록 선택, 캐시된 상태가 언어 변경에도 유지되는지 검토한다.
3. scripts/check-localization.ps1로 키 누락/중복·복합 형식·인자 번호를 확인한다. 일반 작업에서 테스트 포함 build.ps1을 대신 호출하지 않는다.
4. C# 변경은 직접 빌드한다. 게임 UI 변경은 긴 문구/좁은 폭/한영 전환의 확인 단계와 미확인 범위를 기록한다.

추가 언어는 Strings.<culture>.resx와 UiLanguage/UiText·설정의 언어 선택을 함께 확장한다. 불필요한 번역 라이브러리를 추가하지 않는다.

## 진단과 배포

- 진단 필드명·상태 코드·숫자는 invariant 형식이다. 사람이 읽는 UI와 복사 보고서 계약을 구분한다. 보고서 버전은 각 생산 코드/검증 문서를 따른다.
- 메시지 키/인자로 오류 중복을 비교하고 언어 변경으로 관찰 이력을 지우지 않는다. 캐릭터 식별자/포인터/민감 정보를 공개 로그에 넣지 않는다.
- 번역은 satellite assembly다. 한국어 ko/DuckbillFisher.resources.dll이 출력/ZIP에 있어야 한다. 요구 파일 목록은 scripts/package-files.ps1이 관리한다.
- manifest의 영어 소개는 런타임 UI와 별개인 csproj 메타데이터다.

초기 리소스/fallback 확인 원문은 정리 전 Git 커밋 b33f4eb의 이 파일에 보존되어 있다. 최신 패키지 확인은 [Release 기록](validation/RELEASE-0.3.0.0.md)을 따른다. 기존 설계 출처는 [.NET 지역화](https://learn.microsoft.com/en-us/dotnet/core/extensions/localization)와 [ResourceManager](https://learn.microsoft.com/en-us/dotnet/api/system.resources.resourcemanager?view=net-10.0)다.
