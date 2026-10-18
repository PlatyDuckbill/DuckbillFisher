# ENV-02 로컬 검증

- 날짜: 2026-10-15
- 소스: 초기 프로젝트 구성. Windows 전달용 소스 ZIP과 체크섬을 artifacts에 생성.
- 실행 환경: macOS arm64, .NET SDK 10.0.402, 임시 PowerShell 7.6.7.
- 참조: upstream Dalamud 15.0.3.6. 고정 URL/SHA256은 build/dalamud-reference.json.
- 데이터셋: 없음.

## 실행 결과

| 검사 | 결과 |
|---|---|
| .NET SDK 다운로드 SHA512 | Microsoft 릴리스 메타데이터와 일치 |
| NuGet restore | 성공, 세 프로젝트 lock 생성 |
| Core tests (Release) | 9개 통과, 실패/건너뜀 0 |
| Plugin Release build | 성공, 경고/오류 0 |
| 생성 manifest | InternalName DuckbillFisher, AssemblyVersion 0.1.0.0, API 15 |
| build.ps1 전체 실행 | locked restore, Core 테스트, Plugin 빌드, artifacts/dev/DuckbillFisher 복사 통과 |
| inspect-environment.ps1 | 참조 DLL assembly version 15.0.3.6 확인 |
| PowerShell 스크립트 구문 | 4개 모두 통과 |
| test-release.ps1 | 정상 생성/원본 ZIP 해시 유지/기존 결과 보존 통과 |
| 잘못된 배포 입력 | HTTP, Core DLL 누락, Dalamud.dll 혼입, API/버전 불일치, 중복 항목 모두 거부 |

## 재현 명령

PowerShell 7과 .NET SDK를 PATH에서 찾을 수 있고 `$refPath`가 검증한 Dalamud 참조 디렉터리인 경우:

```powershell
pwsh -File scripts/build.ps1 -DalamudPath $refPath -Configuration Release -DeployDirectory artifacts/dev/DuckbillFisher
pwsh -File scripts/test-release.ps1 -PackagePath src/DuckbillFisher.Plugin/bin/Release/DuckbillFisher/latest.zip
```

Mac의 제한된 실행 환경에서 최초 NuGet 요청은 DNS 차단으로 실패했고, 테스트 실행기는 로컬 소켓 권한으로 실패했다. 승인된 네트워크/테스트 실행으로 재실행해 위 결과를 얻었다. 코드 실패를 무시하거나 테스트를 건너뛴 결과가 아니다.

## 아직 검증하지 않은 항목

Windows 자체에서의 스크립트 실행, KR 게임 빌드, 실제 한섭 로드, UI/한글 표시, 언로드/재로드, 다른 플러그인과의 공존, GitHub Actions, 공개 URL 다운로드, 친구 설치/업데이트.

판정: **로컬 자동 검증 통과 / Windows 인게임 검증 대기**.
