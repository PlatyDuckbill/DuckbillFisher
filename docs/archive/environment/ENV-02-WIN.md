# ENV-02 Windows 검증

## 근거와 환경

- 근거: Windows 환경 확인/build.ps1 출력 로그, 시작 창 열기·닫기 및 후속 언로드·재로드 검증 완료 보고. 자동 원격 실행으로 수집한 결과는 아니다.
- 소스: 전달된 DuckbillFisher-source.zip. Windows 환경의 Git 커밋 SHA 대조는 미실행.
- OS 출력: Microsoft Windows 10.0.26300 / X64.
- PowerShell: 7.6.7.
- .NET SDK: 10.0.402. 테스트 실행기 출력 런타임: .NET 10.0.13.
- 참조 Dalamud: 15.0.3.6, `%APPDATA%\XIVLauncherKR\addon\Hooks\dev`.
- 별도 글로벌 설치에서 15.0.3.0도 발견됐으며, 이번 빌드는 KR 설치를 명시적으로 참조했다.
- 빌드 구성: Debug.

## 실행 명령

프로젝트 루트의 PowerShell 7에서:

```powershell
Get-ChildItem .\scripts\*.ps1 | Unblock-File
pwsh -NoProfile -File scripts/inspect-environment.ps1
.\scripts\build.ps1 -DalamudPath "$env:APPDATA\XIVLauncherKR\addon\Hooks\dev"
```

다운로드 차단 표시 해제 후 환경 확인과 빌드가 정상 실행됐다. 시스템 전체 실행 정책 변경은 하지 않았다.

## 결과

- 의존성 복원 성공.
- Core 테스트: 합계 9 / 성공 9 / 실패 0 / 건너뜀 0.
- Core / Plugin Debug 빌드 성공.
- 출력 Plugin: `src/DuckbillFisher.Plugin/bin/Debug/DuckbillFisher.dll`.
- 한섭 게임에서 Plugin 로드와 시작 창 열기·닫기 성공 확인.
- 언로드·재로드 후 창/명령 중복 없음과 한글 표시 확인 완료. 직전 안내의 검증 항목 모두 정상 완료 보고를 근거로 한다.
- 설치 관리자 Main UI 버튼은 별도 결과가 기록되지 않았다. 이번 완료 판정은 명령을 통한 창 동작과 수명 주기 검증을 기준으로 한다.
- KR 게임 클라이언트 빌드는 기록 대기.

판정: **ENV-02-WIN 완료 — Windows 빌드·테스트, 한섭 로드·언로드·재로드 및 시작 창 동작 통과**.
