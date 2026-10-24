# 커스텀 WAV 알림 소리: 0.3.8.0

2026-10-24 개발본은 파일 선택 시 용도별 영어 고정 이름의 **영구 복사본**을 저장하도록 바꿨다. 아래는 도입 당시 기록이며 현재 원본 수정 반영은 파일 재선택이다. 재생용 임시 복사본/MCI 수명은 유지한다. [현재 동작](../../product/SETTINGS-BACKUP.md#wav의-영구-복사본)·[새 검사와 실기 대기](../configuration/SETTINGS-BACKUP.md).

2026-10-19. 사용자 요청으로 기존 알림 설정에 WAV 파일 선택·미리 듣기·재불러오기·중지를 추가했다. WAV만 지원하고 추가 오디오 라이브러리는 도입하지 않는 구성을 사용자가 선택했다.

## 동작과 저장

- 설정 → 알림 → 소리의 `없음 / SE.1~16 / 커스텀 WAV` 중 선택한다. 기존 설정과 기본 없음은 유지한다. 파일 선택은 Dalamud의 FileDialogManager를 사용한다.
- 커스텀은 로컬 `.wav`/RIFF WAVE만, 최대10MB/30초다. 실제 지원 인코딩은 Windows waveaudio에 따른다. 음량/음소거는 Windows 출력 설정을 따르고 게임 SE 음량과는 별개다. 캐릭터 접속 후 준비/미리 듣기가 가능하다.
- 원본 파일은 바꾸지 않는다. 작업자가 임시 디렉터리에 무작위 이름의 WAV를 만들고 그 복사본만 연다. 원본 수정 후 재불러오기하면 새 복사본을 사용한다. 일반 종료/모드·파일/세션 변경에서 열린 복사본을 닫고 소유한 임시 파일만 삭제한다. 프로세스 강제 종료 시 임시 파일이 남을 수 있다.
- 설정 schema12의 SpawnAlertSound=17과 CustomAlertSoundPath를 공통 설정에 저장한다. schema1~11의 기존 소리·즐겨찾기·캐릭터 기록은 유지하고 다음 저장 때12로 기록한다. 경로는 진단/로그에 출력하지 않는다.
- 한 알림 묶음에 한 번 요청한다. 재생 중 새 요청은 같은 소리를 처음부터 다시 재생하며 중첩/대기열을 쌓지 않는다. 미리 듣기는 기존 초당1회 제한을 유지한다.
- Windows 파일/오디오 실패는 커스텀 상태로 표시하며 채팅 알림을 취소하지 않는다. 자동 SE 대체는 하지 않는다.

## 책임·수명·API 근거

- CustomAlertSound의 전용 관리 스레드가 파일 검증/복사와 Windows MCI alias 하나를 소유한다. Draw/Framework에서는 설정 전달과 상태/요청만 처리한다. 작업이 없을 때 AutoResetEvent로 대기하며 폴링하지 않는다.
- Configure 세대/세션으로 이전 로드 결과와 재생 요청을 폐기한다. 재생 요청은 한 개만 보관하고 단조 시간2초 초과 요청은 재생하지 않는다. 로그아웃/모드·파일 변경은 해당 alias를 닫는다. 알림 활성→비활성 전환과 중지 버튼은 해당 alias만 정지한다. Dispose는 작업자 종료와 소유 자원 해제를 기다린다.
- 기존 KR SE 호출과 가드는 그대로다. 커스텀 WAV는 Windows 출력이며 게임 네이티브 포인터/시그니처·서버 통신을 추가하지 않았다. 외부 코드/음원/패키지 의존성도 도입하지 않았다.
- API는 Microsoft의 [mciSendString](https://learn.microsoft.com/en-us/previous-versions/dd757161(v=vs.85)), [open](https://learn.microsoft.com/en-us/windows/win32/multimedia/open), [set](https://learn.microsoft.com/en-us/windows/win32/multimedia/set), [status](https://learn.microsoft.com/en-us/windows/win32/multimedia/status), [MCI_CLOSE](https://learn.microsoft.com/en-us/windows/win32/multimedia/mci-close)를2026-10-19 확인했다. Unicode entry point, waveaudio 타입 고정, alias별 open/set/status/stop/play/close를 사용하고 `close all`/글로벌 정지/믹서 변경은 호출하지 않는다.
- 설치된 KR Dalamud.xml의 FileDialogManager.OpenFileDialog/Draw/Reset 공개 계약을 확인했다. 선택 취소는 설정을 바꾸지 않으며, 세션이 바뀌거나 패널을 Dispose하면 이전 선택 요청을 폐기한다.
- 진단 alerts/v7: Sound=0~17, SoundReady는 선택한 출력 준비 상태, CustomSoundState/CustomSoundCode 추가. 기존 발송 기록의 커스텀 성공 플래그는 작업 큐 접수이며 실제 가청 재생 성공이 아니다. UI에 ‘커스텀 WAV 재생 요청됨/생략’으로 구분한다. 상태/숫자 코드는 invariant이고 경로는 제외한다.

## 실행한 검사와 남은 확인

- 별도 `.cache/custom-sound-build/`와 정규 Debug 직접 빌드: 경고0/오류0. 한영649키/형식 인자, 문서 링크, git diff --check 확인.
- SDK/의존성/lock 파일은 변경하지 않았다. 테스트 추가/실행·실제 WAV 재생·게임 로드·패키징·게시 없음. 빌드 성공을 오디오/인게임 확인으로 확대하지 않는다.
- 기존 예측·캐시·알림·기록·캐릭터 격리의 미확인 항목은 STATUS대로 유지한다.

인게임 대표 확인:

1. 기존 SE/없음 설정이 유지되는지 확인한 뒤 커스텀 WAV를 선택한다. 짧은 WAV 파일 선택→준비 완료→미리 듣기에서 Windows 출력으로 한 번 재생되어야 한다. 한영/좁은 폭도 확인한다.
2. 중지·파일 교체·재불러오기와 언로드/재로드 후 소리 종류/원본 경로 복원을 확인한다. 원본을 수정하고 재불러오기하면 새 내용이 적용되어야 한다.
3. 미래 출현 알림에서 채팅과 WAV 요청1회·기록의 요청 상태를 확인한다. WAV 오류가 있어도 채팅은 유지되어야 한다. 동일 출현 중복 방지·게임 SE 복귀도 확인한다.
4. 긴 소리 재생 중 소리 없음/로그아웃/언로드하면 기존 재생이 종료되어야 한다. 취소한 파일 선택이나 이전 세션의 요청이 뒤늦게 적용되지 않아야 한다.

손상/미지원 WAV·파일 삭제/권한·제한 초과·출력 장치 없음/변경·동시 요청·긴 IO/종료 경계는 별도 확인 대기다. 사용자 원본이나 실제 설정을 손상시키는 실험은 수행하지 않았다.

사용자가 테스트용 WAV 사이트 안내 뒤 ‘확인 완료 배포 진행하자’로 응답했다. 개발0.3.8.0의 커스텀 WAV 대표 확인으로 기록하며 위 장애/세션/종료 경계를 전수 확인했다고 확대하지 않는다. 실제 게시할 Release ZIP 확인은 [배포 후보](../releases/RELEASE-0.3.8.0.md)에서 구분한다.
