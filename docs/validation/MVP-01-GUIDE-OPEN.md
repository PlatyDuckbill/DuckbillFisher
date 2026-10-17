# 0.1.33.0 연동 안내의 어류도감 열기

갱신: 2026-10-17 · 별도 빌드 완료 · 정규 반영/인게임 확인 대기

## 사용자 동작과 경계

- 어종 목록·즐겨찾기 관리·독립 상세의 기존 연동 안내에 ‘어류도감 열기’ 버튼을 추가했다. 안내가 없는 정상 연동 상태에는 나타나지 않는다.
- 클릭만 요청으로 보관하고 Framework.Update에서 한 번 처리한다. 현재 세션/캐릭터/공개 로그인·준비 상태를 재확인한다. 2초 넘게 지연되거나 캐릭터가 바뀐 요청은 폐기한다. 재시도나 자동 열기 없음.
- 열려 있는 낚시 탭은 공개 FocusAddon으로 포커스만 옮긴다. 닫혀 있거나 작살 탭이면 검증한 API로 낚시 탭을 연다. 유효한 초기 선택은 캐시 목록의 첫 지원 어종 Item ID이며, 0이나 임의 ID를 전달하지 않는다.
- 여는 성공을 수집 확인 완료로 해석하지 않는다. 기존 전체 도감 대조가 완료되어야 안내가 사라진다. 수집/수동 지정/필터/예측/알림 정책은 변경하지 않았다.
- 새 FishGuideOpenService에 게임 접근과 요청 수명을 격리했다. 게임 명령/키 입력/필드 직접 쓰기 없음. Dispose에서 업데이트 구독과 대기 요청을 해제한다.
- 지원하지 않는 빌드/DLL에서는 버튼을 비활성화하고 기존 수동 열기 안내를 유지한다. 주소 미해결/목록 준비 전에는 해당 사유를 표시한다. 네이티브 접근을 try/catch로 보호했다고 주장하지 않는다.

## 현재 KR 설치 근거

- 공개 IGameGui는 GetAgentById/포인터 래퍼의 FocusAddon을 제공하지만 전용 어류도감 열기는 확인하지 못했다. OpenForItemId 호출에만 기존 NativeFishCollectionReader의 정확한 게임 버전/DLL SHA256/MVID 호환성 검사를 재사용한다.
- 고정 [AgentFishGuide 원본](https://github.com/aers/FFXIVClientStructs/blob/cae2c3755ef02f9f2728082ae8043669ac9fbfe3/FFXIVClientStructs/FFXIV/Client/UI/Agent/AgentFishGuide.cs), revision `cae2c3755ef02f9f2728082ae8043669ac9fbfe3`. [라이선스 MIT](https://github.com/aers/FFXIVClientStructs/blob/cae2c3755ef02f9f2728082ae8043669ac9fbfe3/LICENSE). 원본은 조사 캐시에만 있고 코드 전문/에셋은 도입하지 않았다. 기존 참조 DLL의 API 호출을 추가했다.
- 설치 DLL 메타데이터/IL에서 `OpenForItemId(uint, bool)`, MemberFunctionPointers 주소 확인 뒤 calli를 수행하는 생성 코드, AgentId.FishGuide=30을 확인했다. MVID는 `f13b1f1f-fd4a-441a-bdac-b3c5abb3675f`로 기존 가드와 일치한다.
- KR `2026.10.06.0000.0000` 실행 파일의 정적 바이트 대조: 설치 DLL과 원본의 시그니처 `E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 44 0F B6 C3 33 D2`가 1회 일치. 파일 오프셋 `0xF718D5`, 상대 CALL 대상 파일 오프셋 `0xF35EB0`. 이 값은 근거이며 코드의 주소/오프셋 상수로 사용하지 않는다.
- 실제 호출 직전에 생성 함수 포인터와 공개 서비스로 얻은 Agent 포인터가 null이 아닌지 확인한다. 포인터는 현재 Framework 콜백 안에서만 사용한다. 게임 실행 중 추가 스캔/외부 HTTP 없음.
- 위 조사는 설치 파일 메타데이터/정적 실행 파일만 읽었으며 게임 메모리 읽기·네이티브 호출·인게임 성공의 증거가 아니다.

## 실행한 검사와 남은 확인

- 첫 별도 빌드는 UiMessage 네임스페이스 누락으로 오류 3개가 발생했다. using 추가 후 같은 dotnet build 재실행 → 경고 0/오류 0.
- 빌드는 SESSION-HANDOFF 명령에 `-p:OutputPath=C:/Users/hyogu/Downloads/DuckbillFisher/.cache/ui-refine-build/`를 추가했다.
- `pwsh -NoLogo -NoProfile -File scripts/check-localization.ps1` → 한영 454키/형식 인자 일치. 테스트 추가/실행 없음. tmp_note.md 접근 없음.
- 정규 DLL은 0.1.32.0 유지. 새 언로드 확인 후 정규 0.1.33.0 빌드가 필요하다. 로컬 커밋만 수행하며 푸시/릴리스 없음.

1. 재접속 또는 재로드 후 도감 조회 전 안내의 ‘어류도감 열기’를 누르면 게임 낚시 탭이 열리는지 확인한다.
2. 기존 전체 도감 대조가 끝난 뒤 안내가 사라지고 기존 수집 판정/필터가 유지되는지 확인한다.
3. 낚시 탭이 열렸지만 자료 수신 전이면 다시 눌러도 닫히지 않아야 한다. 작살 탭을 열어 둔 경우 낚시 탭으로 전환되어야 한다.
4. 로그아웃/세션 전환/재로드 후 이전 클릭이 재생되지 않아야 한다. 이 경계는 인게임 확인 대기다.
