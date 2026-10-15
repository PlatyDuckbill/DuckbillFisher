# 참고 자료와 전제

확인일: 2026-10-15. 문서의 최신 내용과 설치된 한섭 버전은 다를 수 있다. 아래 출처는 설계 근거이며 대상 한섭 런타임의 호환성을 입증하지 않는다.

| 자료 | 확인한 내용 | 적용 |
|---|---|---|
| [Dalamud 시작](https://dalamud.dev/plugin-development/getting-started/) | SamplePlugin과 SDK 기반 시작, 공식 제출 관련 문서 | 한섭에 맞는 revision 확인 후 사용 |
| [프로젝트 구조](https://dalamud.dev/plugin-development/project-layout/) | InternalName, manifest, Dispose 요구 | 확정된 DuckbillFisher 이름 적용, 수명 관리 |
| [기술 고려사항](https://dalamud.dev/plugin-development/technical-considerations/) | Windowing API, 로컬 Lumina 데이터 사용 권장 | 실제 대상 버전 API 확인 후 적용 |
| [개발 플러그인 로드](https://dalamud.dev/faq/getting-started/) | Dev Plugin Locations 등록 안내 | 한섭의 메뉴/경로 확인 필요 |
| [플러그인 제한](https://dalamud.dev/plugin-publishing/restrictions/) | 자동 서버 상호작용 등 제한, 심사 재량 | 자동화 없음만으로 승인 보장 불가 |
| [AI 정책](https://dalamud.dev/plugin-publishing/ai-policy/) | AI 사용 공개, 이해와 직접 테스트, 사람의 제출문 작성 | 전면 무검토 AI 생성 흐름을 피하고 실제 기여 기록 |
| [제출 절차](https://dalamud.dev/plugin-publishing/submission/) | upstream 저장소 제출/빌드 절차 | 한섭 배포 절차와 분리해 확인 |
| [한국 Dalamud 플러그인 저장소 사례](https://github.com/YeopPung/DalamudPlugins) | KR용 버전 제공 및 별도 제출 설명 | 대상 설치와의 일치 여부는 별도 확인 |
| [글로벌 SamplePlugin 고정 revision](https://github.com/goatcorp/SamplePlugin/tree/b8477daaa678a5dd72cdcd4a32bc249d3c23a598) | SDK 15.0.0, csproj manifest, WindowSystem 사용 확인 | 코드/에셋을 통째로 복제하지 않고 최소 시작 창 작성 |
| [Dalamud SDK 고정 revision](https://github.com/goatcorp/Dalamud.NET.Sdk/tree/18377d560976f9b200094b19441710486537433d) | net10.0-windows, C# 14, x64, 참조 경로 설정 | SDK 15.0.0 사용, 패키지 lock 고정 |
| [D15.0.3.6 공식 배포 snapshot](https://github.com/goatcorp/dalamud-distrib/tree/3e8e6eb456c928401febd1d9452c4b8d4cad0eb5) | 컴파일용 참조 | SHA256을 build/dalamud-reference.json에 기록, 자체 ZIP에 포함하지 않음 |
| [커스텀 저장소](https://dalamud.dev/plugin-publishing/custom-repositories/) | JSON 배열, 설치/업데이트 ZIP URL, 인증 미지원 | 친구 공유용 배포 구조 |
| [.NET 10 공식 릴리스 메타데이터](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json) | SDK 10.0.402 설치 주소와 SHA512 | Mac SDK 다운로드 해시 검증, Windows x64 설치 안내 |
| [Fish Tracker 소스](https://github.com/icykoneko/ff14-fish-tracker-app) | 낚시 조건/예측 데이터 처리 참고 후보 | 파일·revision별 라이선스/KR 적용 확인 후 검토 |
| [Microsoft .NET 설치](https://learn.microsoft.com/en-us/dotnet/core/install/windows) | SDK와 Runtime의 차이 | 빌드에는 호환 SDK 준비 |
| [Codex AGENTS.md](https://learn.chatgpt.com/docs/agent-configuration/agents-md) | 저장소별 작업 지침 | 모델 교체 후 규칙 유지 |

## 정책에 관한 구분

Dalamud 커뮤니티의 공식 저장소와 게임 운영사의 공식 승인은 같은 개념이 아니다. 이 프로젝트는 커뮤니티 가이드라인 준수를 목표로 하며 운영사의 허용이나 계정 안전을 보장하지 않는다.

upstream AI 정책은 보조 사용을 허용하되 개발자의 책임과 공개를 요구한다. 의미 있는 사람 관여 없는 전면 생성물은 거절하며, Copilot/Auto 수준은 이해도·범위에 따라 심사한다. 제출 설명/AI 공개문은 사람이 작성해야 한다. AI가 구현을 많이 했다는 사실을 숨기거나 형식적으로 등급을 낮추지 않는다.

어종 데이터 라이선스 검토와 KR 실행 호환성 검증은 아직 수행하지 않았다. 어종 데이터/외부 플러그인 코드는 도입하지 않았다. 빌드에는 Dalamud SDK 15.0.0과 그 의존성, 테스트에는 Microsoft.NET.Test.Sdk 17.14.1, xunit 2.9.3, xunit.runner.visualstudio 3.1.5를 사용하며 전이 의존성은 각 프로젝트의 packages.lock.json에 기록했다. 프로젝트 자체 라이선스는 아직 선정하지 않았다.
