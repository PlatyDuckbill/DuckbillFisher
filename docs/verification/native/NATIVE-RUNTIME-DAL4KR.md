# 0.2.6.0: 동일 버전의 dal4kr 런타임 지원

기록일: 2026-10-17. 기존 15.0.3.7 지원의 후속이며 숫자 버전과 배포 조합을 구분한다.

## 실제 원인과 파일 확보

- 사용자는 본인 PC의0.2.5.0은 동작하지만 친구 PC에서는 세 모듈 모두 NativeDllMismatch라고 보고했다. 동일 KR 게임 빌드/15.0.3.7/정적 목록·도감 표시 감지는 확인됐다. [이전 조사](NATIVE-RUNTIME-15.0.3.7.md).
- 지정 캐시 폴더는 비어 있었지만 사용자 전달 직후 Downloads의 `15.0.3.7.zip`을 찾아 확인했다. 사용자 ZIP SHA256은 `E768E3F99811C1F40F02EE6C3DFF83445E92D0844CAB4F3C4D17910D3B5861DD`다. 필요한 DLL3개와 version.json만 `.cache/friend-runtime/`에 추출했다. 다른 내용은 실행하지 않았다.
- version.json의 DownloadUrl은 **dal4kr/Dalamud.Resources**를 가리킨다. 기존 허용 프로필은 **wasdcxz/dalamud.assets**였다. 실제 제공 DLL3개가 모두 달라 숫자15.0.3.7만으로 같은 배포라고 판단할 수 없었다. 이번 파일 차이는 직접 확인됐으며 Assembly.Location 부재가 원인이라고 확인된 것은 아니다.

## 고정 출처와 동일성

- [dal4kr/Dalamud.Resources](https://github.com/dal4kr/Dalamud.Resources), 고정 revision **8a3c698bc4697e8a68013e156417c31a50a5e263**.
- [Release.zip](https://raw.githubusercontent.com/dal4kr/Dalamud.Resources/8a3c698bc4697e8a68013e156417c31a50a5e263/Dalamud/latest/Release.zip), 49,748,552바이트, SHA256 **EA859A153E3C1235F6E773EF04B94406680E376A835D3BC158C1D5C03E2AC79C**.
- 원본 ZIP을 별도 캐시에 내려받아 세 모듈을 해시 비교했고 친구가 제공한 파일과 모두 동일했다. 런처/게임에 설치하거나 플러그인 배포에 포함하지 않았다.
- Dalamud의 ProductVersion 소스는 **bf26bb431c62f167abf233a0e983d4e059c9dd1b**(KR 브랜치의15.0.3.7 병합), ClientStructs/Interop은 **5ecda407c82597890b41f704e087381fc7dfb912**다. 기존 source와 분기 이력이 다르므로 전체 소스가 같다고 판단하지 않았다. Dalamud AGPL3, ClientStructs/Interop MIT의 참조 자료이며 새 구현 코드를 복제하지 않았다.

| 모듈 | AssemblyVersion | MVID | SHA256 |
|---|---|---|---|
| Dalamud | 15.0.3.7 | b4e06917-bffd-4e12-89da-82bde6b4c73e | B92AC6B5BB51BB874590EB2E5D4BCDD1F9328786AB3D8A5D493583EFFD447926 |
| FFXIVClientStructs | 7.57.0.9700 | 3b9613ec-ea8d-4d0e-9c1e-6f91f3b46d50 | 0E9F985538F800F75B4AABADE1C443F3537D93C695BE368852CA44D9A7D3371E |
| InteropGenerator.Runtime | 1.0.0.0 | d5357f8d-0f92-464f-8936-de6fe0816b4d | A75408D83A54D2ED59B3BF178A0474772CE86F4046F2645E76C2B84AE232B178 |

## 네이티브 접근 대조

고정 wasdcxz15.0.3.7 ZIP의 디스크 DLL을 기준으로 삼았다. 실시간 dev 폴더는 런처 갱신으로 변할 수 있어 비교 기준으로 사용하지 않았다. 캐시의 Mono.Cecil로 메타데이터만 읽고 게임 코드/메모리에 접근하지 않았다.

기존 방식과 같이 타입/중첩 타입의 크기·정렬·필드 타입/오프셋/상수·속성 자료·메서드 시그니처/구현 속성·이름으로 해석한 IL·로컬 타입을 JSON으로 대조했다. 다음8개 대상이 모두 동일했다.

| 대상 | 크기 | 결과 |
|---|---:|---|
| PlayerState | 2344 | 동일 |
| AgentFishGuide와 중첩 자료/함수 포인터 | 424 | 동일 |
| EventFramework와 중첩 주소 | 18432 | 동일 |
| EventHandlerModule | 192 | 동일 |
| FishingEventHandler와 중첩 주소 | 560 | 동일 |
| UIGlobals와 효과음 포인터 | 1 | 동일 |
| StdVector | 제네릭 | 동일 |
| Interop BitArray | 관리 형식 | 동일 |

이는 사용 접근의 정적 근거이며 실제 친구 환경에서의 기능 확인과 구분한다. 기존 KR2026.10.06.0000.0000 게임/주소/스레드/수명 검사는 그대로 유지한다.

## 구현과 정책

- 0.2.6.0에서 같은 버전에 여러 검증된 배포 조합을 둘 수 있게 했다. 후보의 세 모듈 MVID와 SHA256이 **모두 한 조합에 일치**해야 허용한다. 배포 간 DLL 혼합, 미등록 버전/변조 파일을 허용하는 방식은 아니다.
- 기존 wasdcxz15.0.3.6/15.0.3.7 프로필은 유지한다. 친구 조합은 NativeProfile=`15.0.3.7-dal4kr`로 보고한다. 미일치 보고는 가장 가까운 조합에서 다른 모듈을 표시한다. 진단 필드/형식은 기존v3/v6를 유지하며 프로필 값만 추가한다.
- 수집 판정·수동 우선·필터/즐겨찾기·알림·히스토리·네이티브 읽기/호출 구현은 변경하지 않았다. 런타임 HTTP/게임 자동 조작 없음. 새 리소스 키나 의존성/SDK 변경 없음.

## 빌드·패키지와 남은 확인

- 현재 설치 wasdcxz15.0.3.7 참조와 고정 dal4kr15.0.3.7 참조의 별도 Debug 빌드 모두 경고0/오류0. 한영557키/형식 인자 검사 통과. 테스트 추가·실행 없음.
- Release는 원래 지원하는15.0.3.6의 고정 KR ZIP에서 참조를 복구해 사용했다. 세 DLL의 해시가 기존 프로필과 같음을 확인했다. 산출물 참조는 Dalamud15.0.3.6/ClientStructs7.57.0.9376/Interop1.0.0.0으로 기존 지원 런타임보다 높은 참조를 요구하지 않는다. Release 빌드 경고0/오류0.
- `prepare-release.ps1` 통과. `artifacts/share/0.2.6.0/DuckbillFisher-0.2.6.0.zip`은323,019바이트, SHA256 **1E881E03C0EFA7CBE33C2D60C416CD18653EA210E801CD355133334443AC5E7A**.7항목을 `.cache/release-validation/0.2.6.0/`에 추출해 ZIP과 해시 동일, Plugin/ko0.2.6.0/Core1.0.0.0/manifest API15 확인.
- 정규/현재 로드 DLL, 설치된 런처/프레임워크 파일, 공개0.2.5.0/기존 릴리스는 직접 교체하지 않았다. 사용자가 ‘2.6 로컬에서 확인 완료’로 후보의 도감 연동·타이머/기록 대표 확인을 알렸다. 친구의 실제 dal4kr 환경 확인과 전 기능 확인으로 확대하지 않는다. 제공 파일과 조사 산출물은 캐시에만 있고 Git에 포함하지 않는다.
- 사용자 로컬 대표 확인 후 구현6c106e7/확인6fc2fc1에 태그v0.2.6.0과 [Release](https://github.com/PlatyDuckbill/DuckbillFisher/releases/tag/v0.2.6.0)(ID415773597)를 게시했다. 확인한 ZIP을 재빌드하지 않았다. 익명 ZIP·체크섬·태그 아이콘 HTTP200, ZIP/원본 이미지 해시 동일 확인 후 저장소 JSON을 갱신한다. 공개 JSON 최종 확인과 친구의 실제 업데이트 결과는 별도로 기록한다.
- 설치 JSON은7179f43에서 갱신했다. 2026-10-17T11:49:10Z에 기존 공유 URL이 익명 HTTP200/0.2.6.0/API15/1항목을 반환함을 확인했다. ZIP manifest와 버전·아이콘·15줄 Description 일치, 설치/업데이트/아이콘 HTTP200, 이번 게시 커밋의 CI 실행0건이다. 결과는 `.cache/release-remote-check/0.2.6.0/publication.json`에 보관했다. 친구는 같은 저장소의 일반 업데이트로 도감·수집·타이머를 확인하면 된다. 친구 환경의 인게임 결과는 대기다.
- 이후 사용자가 ‘친구 PC 에서도 정상적으로 완료 확인함’으로 배포0.2.6.0 업데이트 후 도감 열기·수집 연동·캐스팅 타이머의 대표 확인을 알렸다. 동일 버전의 다른 KR 배포 조합으로 발생한 차단 문제는 해결 확인으로 종료한다. 전체 기능·모든 결과·다른 캐릭터 검증으로 확대하지 않는다. 이번 결과 반영은 문서만 변경하며 재빌드·재패키징·테스트 실행은 없다.
