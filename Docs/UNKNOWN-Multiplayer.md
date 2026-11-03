# UNKNOWN 온라인 1vs1 연결 테스트

## 실행

`Assets/Scenes/SampleScene.unity`를 열고 Play를 누른다. 기존 NetworkManager에 RoomController와 RoomTestUI가 연결되어 있다. UI는 Play 시 생성되므로 편집 모드에서는 표시되지 않는다.

1. PC A에서 **방 만들기**를 누른다. 익명 로그인 → 비공개 2인 세션 생성 → Relay Host 시작 → 대기실 표시 순서로 진행된다.
2. **방 코드 복사**로 코드를 전달한다.
3. PC B에서 같은 프로젝트의 빌드를 실행하고 **방 코드로 참가**를 선택한 후 코드를 입력하고 **참가하기**를 누른다.
4. 양쪽에 `2 / 2`가 표시되고 Host의 **게임 시작** 버튼이 활성화되는지 확인한다.
5. Host가 시작하면 양쪽 화면에 **게임 시작**이 표시된다. 현재 카드게임 씬은 없으므로 동일 씬 안에서 테스트 화면을 전환한다.
6. **방 나가기** 후 다시 생성/참가할 수 있다. 한쪽이 나가거나 연결이 끊기면 상대방도 방을 종료한다. 이 버전은 방장 이전이나 자동 재접속을 하지 않는다.

표시하는 코드는 `ISession.Code`인 **MPS 세션 코드**다. SDK 내부 Relay allocation join code를 따로 입력하지 않는다. Relay 구성 및 NGO StartHost/StartClient는 SDK가 처리하므로 다른 스크립트나 Inspector 버튼으로 중복 시작하지 않는다.

## 에디터와 Cloud에서 확인할 설정

- Unity 6000.0.74f1, MPS 2.4.1, NGO 2.13.3, Widgets 1.0.5를 유지한다. 패키지 변경은 필요 없다.
- `Edit > Project Settings > Services`에서 두 빌드가 같은 Unity Cloud 프로젝트에 연결되어 있는지 확인한다.
- 해당 Cloud 프로젝트의 Unity Dashboard에서 Authentication의 Anonymous 제공자와 Multiplayer의 Relay/Lobby 서비스 사용 가능 상태를 확인한다. 이 코드는 MPS Sessions를 통해 Lobby와 Relay를 함께 사용한다. 계정/서비스 설정은 코드가 대신 변경하지 않는다.
- 같은 환경을 사용한다. 현재 코드는 별도 environment override 없이 기본 환경을 사용한다.
- 씬의 활성 NetworkManager는 **하나**여야 한다. Network Topology는 **Client Server**, Network Transport는 같은 오브젝트의 **UnityTransport**를 사용한다.
- 이번 연결 테스트에는 Player Prefab이 필요 없다. 기존 Player Prefab이 지정되어 있다면 그대로 사용한다.
- `Connection Approval`은 연결 작업 시 코드에서 활성화한다. 다른 승인 콜백이 있으면 덮어쓰지 않고 오류를 표시한다. 추후 기존 승인 규칙을 통합해야 한다.
- `Run In Background`는 설치 메뉴가 활성화한다. 에디터/실행 창 포커스 전환 중에도 연결 처리를 계속한다.
- 기존 Create Session 위젯은 삭제하지 않고 비활성화했다. 테스트 UI와 별도 Widgets 세션 흐름을 동시에 켜지 않는다. 기존 Canvas/EventSystem/Transport 설정은 유지한다.
- UI Toolkit 테스트 UI에는 TMP Essentials나 별도 Canvas가 필요 없다. `Resources/UnknownRoomTheme.tss`를 함께 유지한다. 한글은 Windows의 맑은 고딕을 사용한다.
- `File > Build Profiles > Scene List`에 SampleScene이 활성화되어 있어야 한다. 현재 프로젝트에는 이미 등록되어 있다.

다른 씬에 설치할 때는 기존 NetworkManager와 UnityTransport를 먼저 연결한 다음 `UNKNOWN > Multiplayer > Install Room Test UI in Current Scene`을 한 번 실행하고 씬을 저장한다. 이 메뉴는 NetworkManager를 생성하지 않으며 중복 실행 시 RoomController/UI도 추가 생성하지 않는다. 설치 작업은 Undo 가능하다.

## 빌드와 같은 PC 테스트

Play 모드를 종료한 후 `UNKNOWN > Multiplayer > Build Windows Test Player`를 실행한다. 출력은 `Builds/UNKNOWN/UNKNOWN.exe`다. 다른 PC에는 exe만 복사하지 말고 **Builds/UNKNOWN 폴더 전체**를 복사한다. Unity 에디터가 없는 PC에서도 실행 가능하다.

같은 PC에서 에디터와 빌드를 동시에 시험하면 익명 계정 캐시가 겹칠 수 있으므로 빌드에 별도 프로필을 지정한다.

```powershell
& .\Builds\UNKNOWN\UNKNOWN.exe -unknownProfile Guest2 -screen-fullscreen 0 -screen-width 1280 -screen-height 720
```

세 번째 참가자를 시험할 때는 `-unknownProfile Guest3`를 사용한다. 다른 PC에서는 기본 프로필로 충분하다. 프로필은 영문/숫자로 지정한다. 프로필 전환은 로그인 전에만 적용된다.

## 동작 구조와 확장

- `Runtime/RoomController.cs`: 초기화/익명 로그인, Create/Join, 정원 제한, 비동기 오류 처리, 퇴장/종료, 연결 손실 처리, Host 시작 신호.
- `Runtime/RoomTestUI.cs`: UI Toolkit 메뉴/대기실/시작 확인 화면. 중복 클릭 방지, 오류 표시, 코드 복사.
- `Resources/UnknownRoomTheme.tss`: UI Toolkit 기본 런타임 테마.
- `Editor/RoomSetupMenu.cs`: 기존 NetworkManager에 설치하는 메뉴 및 Windows 테스트 빌드 메뉴.

정원은 `SessionOptions.MaxPlayers = 2`와 NGO 승인 콜백에서 각각 제한한다. 승인 중인 요청도 정원에 포함한다. 거절된 세 번째 연결의 disconnect 알림은 기존 두 플레이어의 방을 종료시키지 않는다. 시작 가능 조건은 Host 권한, 세션 인원 2명, 실제 Host의 NGO 연결 인원 2명이다. 시작 시 서비스 방을 잠그고 다시 연결 인원을 확인한 뒤 서버의 신뢰성 있는 메시지로 양쪽 화면을 전환한다. 클라이언트가 보낸 시작 메시지는 무시한다.

서비스 이벤트를 구독하고 5초마다 Refresh로 보완한다. 연결 작업에는 45초 취소 토큰을 사용한다. 서비스 초기화/로그인은 SDK의 요청 제한 시간을 따르고 예외를 화면에 표시한다. 방 정리가 8초를 넘으면 로컬 Transport를 종료하고 늦게 도착한 예외도 관찰한다. Cloud 요청이 실패한 상태에서는 서비스 측 방 제거까지 즉시 보장할 수 없으며 SDK/서비스 정리에 의존한다.

MPS 2.4.1은 Lobby의 정원 초과/잠김 오류를 `SessionError.Unknown`으로 변환하고 원래 예외를 보존하지 않는다. 이에 해당 두 경우는 SDK 메시지의 full/locked 표시로 안내하고, 나머지는 SessionError와 상세 메시지를 표시한다.

실제 카드게임 씬이 준비되면:

1. 해당 씬을 Build Profiles에 추가한다. 두 PC에 같은 빌드를 사용한다.
2. RoomController의 **Gameplay Scene**에 씬 이름 또는 경로를 지정한다.
3. NetworkManager의 **Enable Scene Management**를 켠다.
4. 게임 씬에 NetworkManager를 추가하지 않는다. 기존 NetworkManager가 유지된다.
5. 시작 시 Host가 NGO SceneManager.LoadScene을 호출하여 클라이언트도 함께 로드한다.
6. 실제 게임 UI를 붙일 때 RoomTestUI를 비활성화하고 RoomController의 Changed/GameStarted 이벤트 및 공개 메서드에 연결한다. 게임 로직은 네트워크 씬 로드 완료 이후 초기화한다. GameStarted 이벤트는 로드 완료 통지가 아닌 시작 신호다.

카드 상태 동기화, 턴 판정, 재접속/호스트 이전, 계정에 연결된 게임 입장 검증은 다음 단계다. 현재 NGO 승인은 정원/시작 상태를 검사하며 Authentication ID와 NGO client ID를 보안상 바인딩하지 않는다.

## 수동 검증 항목

| 항목 | 기대 결과 |
| --- | --- |
| 코드 없이 참가 | 영문/숫자 코드 입력 안내, 요청 없음 |
| 존재하지 않는 코드 | 오류 표시 후 재시도 가능 |
| Host만 입장 | 1 / 2, 시작 불가 |
| 다른 프로필/PC 참가 | 양쪽 2 / 2, Host만 시작 가능 |
| 세 번째 프로필 참가 | 정원 초과 오류, 기존 두 연결 유지 |
| Host 시작 | 양쪽 시작 확인 화면, 이후 입장 차단 |
| 참가자/Host 나가기 | 양쪽 메뉴 복귀, 새 방 생성 가능 |
| 인터넷 연결 중단/프로세스 종료 | Transport 감지 시간 후 방 종료 안내 |
| 빠른 연속 클릭 | 요청 중 버튼 비활성화, 중복 세션 없음 |

인터넷 차단은 Transport의 기존 timeout 설정에 따라 감지되므로 즉시 통지되지 않을 수 있다.

## 확인한 API 근거

설치된 `Library/PackageCache/com.unity.services.multiplayer@cf2613f41536/Runtime/Multiplayer`의 MultiplayerService, SessionOptionsExtensions, SessionHandler, GameObjectsNetcodeNetworkHandler, LobbyConverter 소스를 직접 확인했다. NGO 2.13.3의 NetworkManager 및 CustomMessagingManager도 확인했다. 패키지 캐시는 수정하지 않았다.

- [Unity 공식 Session networking 문서](https://docs.unity.com/en-us/mps-sdk/session-networking)
- [Unity 공식 NGO 세션 시작 안내](https://docs.unity.com/en-us/mps-sdk/tutorials/build-your-first-session)

## UI 테마와 조작

메인 메뉴 → 방 코드 입력 → 대기실 → 시작 화면으로 순차 전환한다. 어두운 남색 배경, 가는 은색 장식, 명조 계열 글꼴, 떠다니는 입자와 옅은 안개를 사용한다. 장식은 UI Toolkit의 Painter2D로 그리며 외부 이미지가 필요 없다.

- 마우스 또는 Tab으로 항목 선택, Enter로 확인.
- 위/아래 방향키로 메뉴 선택. 코드 입력 필드에서는 텍스트 편집을 유지한다.
- 코드 입력 화면에서 Esc로 메인 메뉴 복귀. 대기실에서는 방 나가기 항목을 사용한다.
- 연결 작업 중에는 버튼을 비활성화하고 호흡하는 문양을 표시한다.
- 화면 전환은 짧은 페이드로 처리한다. 시간이 흐르는 연출에는 Time.unscaledTime을 사용한다.
- 색상/크기/간격은 `Assets/UNKNOWN/Multiplayer/Resources/UnknownRoomStyle.uss`에서 수정한다.
- 제목은 Times New Roman, 한글은 바탕을 우선 사용하며 설치된 운영체제 글꼴로 대체한다.

UI 변경 후 다른 PC에서 확인할 때는 Windows 테스트 플레이어를 다시 빌드한다. 기존 방 생성/참가/정원/시작 조건은 RoomController에서 그대로 관리한다.
타이틀·메뉴·방 코드·접속 정보는 화면 중앙에 정렬한다. 창 크기가 바뀌면 실제 ScrollView 뷰포트 높이를 기준으로 중앙 배치를 갱신하며, 작은 화면에서는 스크롤할 수 있다. 메인 화면에는 UNKNOWN 타이틀과 메뉴만 표시하고 부제목·캐치프레이즈를 사용하지 않는다.
