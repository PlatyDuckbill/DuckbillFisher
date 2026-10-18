---
name: duckbill-release
description: Build and package DuckbillFisher on Windows, or publish a verified Release to its Dalamud custom repository when requested. Use for Plugin builds, Release validation and deployment; not for ordinary documentation edits or unrelated plugins.
---

# DuckbillFisher 빌드·배포

저장소 루트는 이 파일에서 `../../..`이다. 현재 요청이 개발 DLL 빌드, 로컬 패키지 준비, 공개 배포 중 어디까지인지 먼저 구분한다. 빌드 요청만으로 원격 게시를 승인받았다고 해석하지 않는다.

## 필요한 문서만 읽기

- [STATUS](../../../docs/STATUS.md): 현재 공개 버전과 실제 확인 범위.
- [HANDOFF](../../../docs/SESSION-HANDOFF.md#환경과-경로): 현재 PC의 게임/참조/출력 경로.
- 개발 빌드: [WORKFLOW](../../../docs/development/WORKFLOW.md#windows-빌드).
- 패키지/게시: [DISTRIBUTION](../../../docs/development/DISTRIBUTION.md#개발자-배포-절차).

버전·경로·명령을 이 스킬에 복사하지 않는다. 이전 대화의 출력 경로가 있더라도 파일과 manifest를 확인한다.

## 반복 실수 방지

1. 직접 dotnet build를 사용한다. 테스트 포함 build.ps1/test-release.ps1 또는 테스트 CI는 명시 요청 없이는 실행하지 않는다. 같은 프로젝트의 참조별 빌드는 순서대로 한다.
2. 사용자 상시 언로드 지시를 적용한다. 새 승인을 반복 요청하지 않고 로드할 DLL 절대 경로를 제공한다. 잠금 실패는 우회하지 않는다.
3. Release는 검증한 최소 지원 참조로 만든다. 참조가 없거나 패키지/manifest/API/리소스가 맞지 않으면 준비를 중단하고 원인을 해결한다.
4. ZIP의 SHA256과 사용자가 확인한 추출 DLL을 연결한다. 확인 후 다시 빌드한 실행 파일은 새 후보다. 과거 버전의 실기 확인을 현재 ZIP 확인으로 대체하지 않는다.
5. 게시가 승인되고 후보가 확인되면 DISTRIBUTION의 순서대로 완료한다. **Release 업로드/공개 → 익명 파일·해시 확인 → 설치 JSON 게시 → 실제 공유 raw URL 확인** 순서를 지킨다.
6. 기존 태그/Release/자산이 있으면 재개 상태를 먼저 조사한다. 덮어쓰기·중복 Release 생성·미검증 URL 게시를 하지 않는다. 인증 값은 메모리에서만 다룬다.

완료 보고에는 빌드/패키지/인게임/원격 결과를 구분하고, 배포 때는 저장소 URL·버전·커밋·남은 실제 설치 확인만 간결하게 적는다. 원격 장애면 완료된 단계와 남은 단계를 기록한다.
