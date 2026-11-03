# 방 코드 참가 실패 진단 (2026-11-03)

## 재현한 실패 원인

일반 실행에서는 `SampleScene`의 `RoomController.authenticationProfile`이 비어 있고, 이전 초기화 코드가 `UnityServices.InitializeAsync()` 및 기본 프로필로 익명 로그인했습니다. 같은 PC의 두 EXE가 `default` 인증 캐시를 읽으므로 같은 Player ID를 사용했습니다. 기존 `-unknownProfile`을 지정한 테스트는 이 일반 실행 경로를 검증하지 못했습니다.

동일 PC에서 기존 인증 동작을 유지한 진단 Development Build 두 개로 재현했습니다.

| 항목 | EXE A | EXE B |
|---|---|---|
| PID | 13656 | 44392 |
| 프로필 | default | default |
| Player ID | XX2PB1QdSSfhnQmozF46WDbomFV8 | XX2PB1QdSSfhnQmozF46WDbomFV8 |
| Session Code | TJCCWH 생성 | TJCCWH 참가 요청 |
| 결과 | 방 생성·Relay Host 연결 성공 | SessionConflict(45) |

실제 오류: `player is already a member of the session`. `JoinSessionByCodeAsync`에서 발생했으며, 참가자의 Netcode가 시작되기 전입니다. 잘못된 방 코드나 Relay 소켓 연결 실패가 원인이 아니었습니다.

## 수정

- `LocalAuthenticationProfile.cs`: 프로세스 간 named mutex로 인증 캐시 프로필을 예약합니다. 첫 프로세스는 기존 `default`, 추가 프로세스는 `unknown_local_2`, `unknown_local_3` 등을 사용합니다. 저장된 프로필은 재사용하며 종료 시 잠금만 해제합니다. 프로필·토큰·PlayerPrefs 삭제 및 강제 로그아웃은 하지 않습니다. 다른 PC는 각자의 로컬 캐시를 사용합니다.
- `RoomController.cs`: 프로필을 초기화 전에 선택하고 설치된 SDK의 `InitializationOptions.SetProfile(...).SetEnvironmentName(...)`으로 전달합니다. 기본 환경은 기존 standalone 기본값과 같은 `production`입니다. 이미 초기화되었지만 아직 로그인하지 않았다면 `SwitchProfile` 후 로그인합니다. 이미 로그인된 다른 프로필을 몰래 바꾸지 않고 오류를 표시합니다. 명시적인 `-unknownProfile Name`도 유지하며 다른 프로세스가 점유 중이면 설명과 함께 차단합니다.
- `RoomController.Diagnostics.cs`: PID, 프로필, Player ID, Project ID, 환경, Session Code, 세션 예외 이름·숫자 코드·원인 메시지, NGO 연결 상태와 연결 해제 이유를 개발 로그에 기록합니다. 토큰·비밀 키 자체는 읽거나 출력하지 않으며 예외 메시지의 인증 필드도 마스킹합니다.
- `RoomConnectionCheck.cs`: Development Build에서 명시적으로 요청한 경우에만 두 프로세스 방 생성/참가, 세션 및 Netcode 인원, UI 표시를 검사하고 JSON을 저장합니다. 일반 실행에서는 실행하지 않습니다.
- `RoomTestUI.cs`: 검사 도구가 실제 대기실의 인원·참가자 표시 값을 확인할 수 있는 내부 읽기 속성만 추가했습니다.

씬·프리팹·패키지·전투 규칙은 이번 진단에서 수정하지 않았습니다. 기존 NetworkManager를 사용하며 별도 `StartHost`/`StartClient`를 호출하지 않습니다. MPS 2.4.1의 `.WithRelayNetwork()`가 NGO/UnityTransport 연결을 수행합니다.

## 확인한 설정과 API

- Cloud Project ID: `13cfb794-511c-49c9-b0d6-402d6ebe74ce`, 프로젝트 이름 RealCardGame.
- 빌드 씬: `Assets/Scenes/SampleScene.unity`. 활성 NetworkManager 한 개, 그 매니저에 UnityTransport 한 개가 연결되어 있습니다. 기존 Widgets Create Session 오브젝트는 비활성입니다.
- 생성은 private, MaxPlayers=2인 MPS Session이며 `.WithRelayNetwork()`를 사용합니다. UI 표시·복사 및 참가 입력은 `ISession.Code`입니다. 방 이름이나 Relay allocation join code를 입력하는 구조가 아닙니다.
- 설치 소스: Authentication `AuthenticationExtensions.SetProfile`, `AuthenticationCache.Prefix`, Core `EnvironmentsOptionsExtensions`, MPS `SessionException.Error`, `SessionError.SessionConflict=45`, `GameObjectsNetcodeNetworkHandler.StartAsync`를 직접 확인했습니다.
- 양쪽 실행의 프로젝트 ID와 초기화 환경이 일치하는지 실제 로그로 확인했습니다.

## 실제 검증

수정 1차 Development Build에서 인증 프로필 옵션 없이 같은 PC에서 두 EXE를 실행했습니다. A(PID 59664)는 기존 Player ID를 유지했고, B(PID 48576)는 `unknown_local_2`의 `oekLQRkJmhMFjvNgfspALFOErEuq`로 로그인했습니다. Session Code `89PWTR`로 참가했으며 양쪽 JSON에서 `sessionPlayers=2`, `connectedPlayers=2`, `twoPlayers=true`, `connected=true`, `cleaned=true`를 확인했습니다. B의 로그에는 `SessionJoined ... Host=False members=2/2`, `RelayConnected ... connected=True ... connectedPlayers=2`가 기록됐습니다.

최종 빌드 재실행 결과:

| 항목 | EXE A | EXE B |
|---|---|---|
| PID | 59704 | 46684 |
| 프로필 | default | unknown_local_2 |
| Player ID | XX2PB1QdSSfhnQmozF46WDbomFV8 | oekLQRkJmhMFjvNgfspALFOErEuq |
| Session Code | TCPF9J | TCPF9J |
| 환경 | production | production |
| 세션 인원 / NGO 연결 인원 | 2 / 2 | 2 / 2 |
| 실제 UI 인원·참가자 값 검사 | uiTwoPlayers=true | uiTwoPlayers=true |
| 종료 정리 | cleaned=true | cleaned=true |

양쪽 Player ID가 수정 1차 검사와 같으므로 캐시 삭제 없이 계정을 재사용한 것도 확인했습니다. 검사에서는 `-batchmode -nographics -unknownRoomCheck host/guest`로 두 Windows EXE를 실행했으며 `-unknownProfile`은 지정하지 않았습니다. UI 검사는 실제 RoomTestUI의 인원 문자열·참가자 문자열·대기실 표시 스타일을 읽은 결과이며, 화면 캡처나 수동 버튼 입력 검사는 아닙니다. 방 생성/참가는 버튼과 같은 RoomController 공개 메서드를 사용했습니다. 기존 전투·네트워크 투영 규칙 검사 67개도 통과했습니다.

중간 재실행 검사에서 검사 도구의 공유 코드 파일 읽기가 `IOException: Sharing violation`으로 중단됐습니다. 일반 게임 UI 입력 경로에는 없는 검사 도구의 파일 경쟁이었으며 `ReadCode`가 잠금 중에는 기다렸다 재시도하도록 수정했습니다. 위 최종 검사는 이 수정까지 포함한 빌드로 두 EXE를 동시에 시작해 완료한 결과입니다.

검증 자료: `Docs/Validation/Room-Connection/`의 Baseline/Fixed/Verified JSON 및 Connection 로그 발췌. 최종 빌드 로그는 `Library/UNKNOWN-Connection-Verified-Build.log`이며 전체 개발 로그는 `Library/UNKNOWN-Connection-*.log`에 있습니다. 인증 토큰은 자료에 포함하지 않습니다.

## 사용 방법과 범위

최신 실행 파일은 `Builds/UNKNOWN/UNKNOWN.exe`입니다. 다른 PC로 전달할 때는 해당 폴더 전체를 복사합니다. 동일 PC에서는 별도 인자 없이 두 번 실행하면 자동으로 인증 프로필이 분리됩니다. Inspector에서 RoomController의 Services Environment를 변경한다면 양쪽 모두 같은 환경으로 빌드해야 합니다. 기존 실행 파일에는 수정이 소급 적용되지 않습니다.

서로 다른 물리 PC/외부 네트워크에서의 접속과 방화벽 조건은 이번 동일 PC 테스트로 검증하지 못했습니다. 빌드는 오류 0개이며 기존 Services 프로젝트 연결 경고 1개와 URP Light2D 셰이더 경고 2개가 남았습니다. 이 경고가 있는 상태에서도 실제 인증·세션·Relay 연결은 성공했으며, 이번 참가 실패의 직접 원인은 재현 로그의 동일 Player ID였습니다.
