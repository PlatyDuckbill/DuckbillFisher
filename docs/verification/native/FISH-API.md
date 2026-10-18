# ENV-03 한섭 수첩 API 조사

확인일: 2026-10-15. 판정: **설치 파일의 API·ID 계약·구현 근거 확인. 실제 수첩 읽기와 준비 상태의 인게임 검증은 대기**.

후속 0.1.1.0의 수첩 감지/정적 시트 읽기 구현과 실제 한섭 보고서는 [수첩 진단 결과](FISH-WIN.md)에 있다. 여기서 수첩 읽기 미확인은 캐릭터별 수집 비트를 뜻한다.

## 대상과 재현

- 사용자 확인 게임 루트: `C:\Program Files (x86)\FINAL FANTASY XIV - KOREA`.
- `game\ffxivgame.ver`: `2026.10.06.0000.0000`. `.bck`의 `2026.07.16.0001.0000`은 현재 버전으로 사용하지 않는다.
- 사용자 확인 Dalamud 루트: `%APPDATA%\XIVLauncherKR`. 조사한 DLL 폴더는 그 아래 `addon\Hooks\dev`다.
- PowerShell 7.6.7에서 다음 명령이 성공했다. 현재 도구에서는 Store PowerShell 실행에 승인된 샌드박스 밖 실행이 필요했다.

```powershell
pwsh -NoLogo -NoProfile -File scripts/inspect-fish-api.ps1 `
  -DalamudPath "$env:APPDATA\XIVLauncherKR\addon\Hooks\dev" `
  -GamePath "C:\Program Files (x86)\FINAL FANTASY XIV - KOREA" `
  -OutputPath .cache/env03-fish-api.json
```

출력 폴더는 먼저 준비한다. 이번에는 기존 `.cache`에 보고서를 생성했다. 이 파일은 Git 추적 대상이 아니다. 스크립트는 설치된 Mono.Cecil로 DLL의 메타데이터·IL·선택적 PDB를 읽는다. 게임 DLL을 실행용으로 로드하거나 게임 프로세스에 연결하지 않는다. 네트워크 요청도 하지 않는다.

## 설치 파일 식별

| 파일 | Assembly / InformationalVersion | SHA256 |
|---|---|---|
| Dalamud.dll | 15.0.3.6 / 15.0.3.6+896d83190a34e7ff92deef967ba635ebb7dbdbae | `d93be6ab790d89fe980920b2be03bc0cd41adda9aae104121854c0cc86d5a063` |
| FFXIVClientStructs.dll | 7.57.0.9376 / 1.0.0+cae2c3755ef02f9f2728082ae8043669ac9fbfe3 | `8cc4c29da5899f547caec065569151def545cb190b9a8488a23d9b0fad03fcf2` |
| InteropGenerator.Runtime.dll | 1.0.0.0 / 1.0.0+cae2c3755ef02f9f2728082ae8043669ac9fbfe3 | `84638b1f76f3deeea0eaa2554559a3e5c925474035c79861bfc0f1b842a6c64f` |
| Lumina.Excel.dll | 7.0.0.0 / 7.5.1 | `0dbee3c021db750b8d95d49e5e1671c6872477e563e1a589378796d5907a55f0` |
| Lumina.dll | 7.0.0.0 / 7.7.1+e383f839cb6ecefdedeb045554dbb39ce8f07065 | `ed61eed214aa5173631e4b40a4b28a81ca1d160286d80104aa2c76a3b7203d7b` |

Dalamud의 SCMVersion은 `15.0.3.6-46-g896d83190-dirty`, GitHashClientStructs는 `cae2c3755-dirty`다. 공개 revision 이후의 로컬 변경이 포함될 수 있으므로 같은 assembly 버전만으로 동일 바이너리라고 판단하지 않는다. 이번에 확인한 필드와 메서드의 일치를 전체 구조의 호환성으로 확대하지 않는다.

`build/dalamud-reference.json`의 `3e8e6e…`는 upstream 배포 저장소의 ZIP snapshot이다. 위 설치 DLL의 소스 revision이나 한섭 런타임 검증 결과와 구분한다. 라이브러리의 `7.57`/`7.5.1` 표기는 게임 패치 번호를 확정하는 근거로 사용하지 않는다.

## 공개 API와 준비 상태

- 설치 Dalamud DLL의 공개 `Dalamud.Plugin.Services` 인터페이스 메서드 이름에서 `Fish`/`Caught` 후보는 0개였다. XML 검색에서도 수첩 수집 API를 찾지 못했다. 전체 기능의 부재를 증명한 것은 아니며 이번 조사 범위에서 공개 서비스 경로는 확보하지 못했다.
- 공개 `IClientState.IsLoggedIn`, `IPlayerState.IsLoaded`는 존재한다. 후자는 네이티브 `PlayerState.Instance()->IsLoaded`를 반환하는 구현과 일치했다. 문서는 LocalPlayer 게임 객체와 준비 시점이 다를 수 있다고 설명한다.
- 이 두 값이 어류 수첩 비트의 수신 완료를 보장한다는 근거는 찾지 못했다. 로그인 이벤트 한 번이나 LocalPlayer 존재만으로 `false`를 미수집으로 확정하지 않는다.
- 로그인 전·직후, 수첩을 열기 전·후, 로그아웃·다른 캐릭터 로그인에서의 실제 값과 준비 순서는 미확인이다. 캐릭터 식별자는 내부 격리에만 사용하고 보고서에 출력하지 않는다.

## 수집 상태 구현과 ID 계약

- 설치 DLL에 `PlayerState.IsFishCaught(uint fishParameterId)`가 존재한다. XML과 고정 소스 모두 `IsInLog == true`인 FishParameter 행에만 호출하도록 명시한다.
- 메서드는 네이티브 함수를 호출하지 않고 `CaughtFishBitArray.Get((int)fishParameterId)`로 비트를 읽는다. 잘못된 포인터 접근을 관리 예외 처리로 안전하게 만들 수 있다는 의미는 아니다.
- 설치 메타데이터의 PlayerState 크기는 `0x928`, IsLoaded 필드는 `0x00`, `_caughtFish`는 `0x41F`의 191바이트 배열이다. 생성 getter는 상수 `1522`를 BitArray 생성자에 전달한다. 해당 부분은 고정 소스와 일치했다. 이 수치는 조사 근거이며 Plugin에 오프셋을 복사하지 않는다.
- 설치 BitArray.Get의 범위 검사는 음수 또는 `index > 1522`를 거부한다. 반면 고정 소스의 TryGet은 `index >= bitCount`를 거부하므로 경계 의미가 다르다. 실제 대상 시트의 마지막 IsInLog 행과 대조하기 전 임의로 상한을 일반화하지 않는다. 범위 예외는 미수집이 아니다.
- FishParameter.RowId가 수집 조회 인자다. `FishParameter.Item.RowId`를 이 인자로 넘기지 않는다.
- 설치 Lumina.Excel의 `FishParameter.Item` 타입은 `RowRef<Item>`이 아닌 **비제네릭 RowRef**다. getter의 IL과 고정 EXDSchema는 `EventItem`, `Item`을 참조 대상으로 둔다. 일반 아이템 매핑은 참조 타입·행 존재를 확인한 뒤 사용하고 퀘스트 아이템을 분리한다.
- 최초 파일 조사에서는 실제 KR 시트의 값은 읽지 않았다. 후속 0.1.1.0 한섭 보고서에서 전체 2,512행, IsInLog 1,518행, 최대 IsInLog RowId 1,522를 확인했다. 개별 어종 이름/Item 매핑은 Selected 보고서 대기다. 외부 데이터로 임의 보충하지 않았다.

## 원문 근거

설치 파일과 다음 고정 revision의 원문을 대조했다. 원문 다운로드는 개발 중 조사이며 Plugin 런타임 통신이 아니다. 다운로드는 HTTP 200으로 성공했고 로컬 `.cache`에만 두었다. 외부 소스·어종 데이터를 제품 코드/내장 데이터로 복사하지 않았다.

- [PlayerState: cae2c3755ef02f9f2728082ae8043669ac9fbfe3](https://github.com/aers/FFXIVClientStructs/blob/cae2c3755ef02f9f2728082ae8043669ac9fbfe3/FFXIVClientStructs/FFXIV/Client/Game/UI/PlayerState.cs)
- [BitArray: 같은 revision](https://github.com/aers/FFXIVClientStructs/blob/cae2c3755ef02f9f2728082ae8043669ac9fbfe3/InteropGenerator.Runtime/BitArray.cs)
- [IPlayerState: 896d83190a34e7ff92deef967ba635ebb7dbdbae](https://github.com/goatcorp/Dalamud/blob/896d83190a34e7ff92deef967ba635ebb7dbdbae/Dalamud/Plugin/Services/IPlayerState.cs)
- [Dalamud PlayerState wrapper: 같은 revision](https://github.com/goatcorp/Dalamud/blob/896d83190a34e7ff92deef967ba635ebb7dbdbae/Dalamud/Game/Player/PlayerState.cs)
- [FishParameter schema: 75f674655bb89d6172effa3a5a2d93bcfc7deb51](https://github.com/xivdev/EXDSchema/blob/75f674655bb89d6172effa3a5a2d93bcfc7deb51/FishParameter.yml)

## 다음 한 작업과 완료 조건

공개 API만 사용하는 진단으로 로그인 준비 상태와 실제 KR 시트의 ID 매핑을 확인한다.

1. 사용자의 명시적 동작으로 `IDataManager.GetExcelSheet<FishParameter>()`를 읽고 IsInLog 행 수·최대 RowId·선택한 행의 Item 참조 타입/ID/이름을 표시한다. 시트 불일치나 누락은 Unknown과 이유를 표시한다. 렌더링 중 전체 행을 검색하지 않는다.
2. `IsLoggedIn`과 `IsLoaded`를 로그인 전후·수첩 열기 전후·캐릭터 전환에서 관찰하고 이전 캐릭터의 표시를 폐기한다. 네이티브 수첩 비트는 아직 읽지 않는다.
3. 수집/미수집이 알려진 일반 낚시 어종을 각 1종 골라 인게임 수첩과 ID를 대조한다. 이후 대상 DLL/빌드를 한정한 읽기 전용 어댑터의 검증 조건을 정한다. 준비 불명·미지원·읽기 실패는 Unknown으로 유지한다.

이번 변경은 조사 스크립트와 문서다. 스크립트의 실제 실행은 성공했으며 Plugin/Core 소스는 변경하지 않았다. 빌드·Core 테스트·수첩 인게임 검증은 실행하지 않았다. ENV-03 전체 완료로 판정하지 않는다.
