# UNKNOWN 온라인 PvP

## 실행과 빌드

Unity 6000.0.74f1에서 `Assets/Scenes/SampleScene.unity`를 열고 Play 합니다. 중앙 UNKNOWN 메뉴의 **온라인 대전 → 방 만들기 / 방 코드로 참가**를 사용합니다. 각자 준비 버튼을 누르면 방장에게 게임 시작이 활성화됩니다. 로컬 테스트는 네트워크 연결 없이 기존 규칙과 핫시트 방식으로 실행됩니다.

기존 SampleScene의 NetworkManager와 UnityTransport를 그대로 사용합니다. RoomController가 NetworkBattleController를 같은 오브젝트에 추가합니다. 새 NetworkManager, Player prefab, NetworkObject나 카드별 RPC 컴포넌트를 추가할 필요가 없습니다. `gameplayScene`은 비워 둡니다. 온라인 전투 Canvas는 현재 씬에서 생성되며 대기실 UI를 숨깁니다. 기존 UNKNOWN_Battle_Test 씬도 계속 사용할 수 있습니다.

Windows 빌드는 `UNKNOWN > Multiplayer > Build Windows Test Player`를 실행합니다. 결과는 `Builds/UNKNOWN/UNKNOWN.exe`입니다. 배포할 때 exe 파일만 보내지 말고 같은 폴더의 Data 폴더, UnityPlayer.dll 등 전체를 함께 전달합니다. Build Profiles의 첫 활성 씬은 SampleScene이어야 합니다. 빌드는 현재 활성 씬 목록을 사용합니다.

같은 PC에서 두 프로세스를 실행할 때 두 번째 실행은 `UNKNOWN.exe -unknownProfile Guest2`로 서로 다른 익명 인증 프로필을 사용합니다. 실제 다른 PC에서는 인자 없이 실행할 수 있습니다. 기존 Unity Cloud 프로젝트 연결과 패키지 버전은 변경하지 않습니다.

## 연결과 권한

- Multiplayer Services 2.4.1의 `CreateSessionAsync`, `JoinSessionByCodeAsync`, `SessionOptions.WithRelayNetwork()`를 사용합니다. 세션 코드로 참가하며 Relay allocation의 별도 코드를 UI에 혼용하지 않습니다.
- MPS가 기존 NGO NetworkManager를 Host/Client로 시작합니다. StartHost/StartClient를 중복 호출하지 않습니다. 최대 인원 2, 비공개 세션, NGO 접속 승인에서도 2인 제한을 적용합니다.
- 준비 상태는 NGO의 신뢰성 메시지로 Host가 관리하고 양쪽에 갱신합니다. 새 연결/접속 해제 시 준비를 초기화합니다. Host만 세션을 잠그고 시작할 수 있습니다.
- Host의 BattleAuthority만 BattleEngine을 생성하고 판정합니다. 손패/덱/주사위/무작위 버림/효과 대상 계산은 Host에만 존재합니다. Client는 카드 ID와 대상, 행동 종류, 순번, 상태 버전과 턴 번호만 요청합니다.
- Host는 연결 ID에서 행동 주체를 결정합니다. Client가 주체나 피해량/스탯을 제출하는 필드는 없습니다. 연속 순번, 버전, 턴을 검사하고 기존 엔진의 소유권/마나/대상/공격 횟수 검증을 통과한 요청만 적용합니다.
- NGO ReliableFragmentedSequenced 메시지로 수신자별 스냅샷과 순서 있는 결과 이벤트를 전달합니다. 중복 상태 버전은 연출을 재생하지 않습니다. Client의 BattleEngine view는 덱을 생성하지 않고 명령 실행을 거부합니다.
- 서버 시간(ServerTime) 기준 60초 deadline을 전달하며 Client는 표시만 합니다. Host가 만료와 턴 전환을 판정합니다. 확정된 효과는 엔진이 마무리하고 Host 연출 완료 뒤 전환합니다. 온라인에서는 로컬 일시정지를 제공하지 않습니다.

## 비공개 정보

상대 손패의 정의/인스턴스 ID와 덱 순서는 전송하지 않습니다. 수신자의 손패만 완전한 데이터로 전달하고 상대는 수량만 전달합니다. 상대 드로우/버림 이벤트의 카드 정보 및 UNKNOWN 조합의 재료 ID·결과 ID·종류도 제거합니다. UNKNOWN 소환 뒤에는 공개 필드 카드로 표시됩니다. 전투 로그는 엔진의 공개 사실만 사용합니다.

Host는 판정에 필요한 양쪽 비공개 데이터를 보유합니다. 악의적으로 변조한 Host로부터 손패를 숨기는 구조는 아닙니다. 이를 방지하려면 별도의 신뢰 서버가 필요합니다. 자동 재접속과 Host migration은 지원하지 않습니다.

## UI

1920×1080 CanvasScaler를 사용합니다. 온라인에서는 턴과 무관하게 자기 손패/보드를 아래, 상대 손패 뒷면/보드를 위에 고정합니다. 턴/시간/턴 종료는 중앙 전투 구분선에 있습니다. 카드 마나는 좌상단, 문양은 중앙, 효과는 하단, 공격력과 체력은 각각 양쪽 아래에 표시합니다. 문양별 색과 UNKNOWN 보라색 테두리, 마나 부족 시 흐림, 선택 상승, 손패 간격 조절, hover 확대와 상세 설명을 적용합니다. 온라인에서는 손패 공개/새 게임 디버그 버튼을 숨깁니다.

## 수동 확인 순서

1. PC A에서 방 생성, PC B에서 코드 참가. 인원/준비 상태가 일치하는지 확인합니다.
2. 한 명만 준비했을 때 시작 불가, 두 명 준비 후 Host만 시작 가능한지 확인합니다.
3. 서로 자기 손패만 보이고 위쪽에는 상대 뒷면과 수량만 나오는지 확인합니다.
4. 소환→대상 선택→공격→턴 종료, 마나 부족, 반복 클릭, 상대 턴 입력을 확인합니다.
5. UNKNOWN 조합 전에는 상대에게 종류가 보이지 않고 소환 뒤 공개되는지 확인합니다. 조커 결과/스탯/파괴가 양쪽에서 같은지 확인합니다.
6. 60초 무입력 자동 전환과 진행 중 효과의 만료 처리를 확인합니다.
7. 승리 결과 일치, 메뉴 복귀, 재방 생성, 대기실 이탈과 전투 중 연결 해제를 확인합니다.

## 자동 검증 도구

`Tests/UNKNOWN/Run-BattleTests.cmd`는 실제 규칙 코드와 프로토콜을 컴파일하여 기존 규칙, 손패 투영, UNKNOWN 비공개, 버림 비공개, 중복/과거 버전/턴/소유권 거부를 검사합니다.

개발 빌드 전용 Relay 자동 대전: 에디터에서 `UNKNOWN > Multiplayer > Run Relay Host Smoke Check`를 실행한 뒤 Windows 빌드를 다음 인자로 실행합니다.

```
UNKNOWN.exe -unknownProfile GuestSmoke -unknownSmoke guest "D:\git\ECHO-\Echogame\CardGame\Library\UNKNOWN-RelaySmoke.code"
```

명시적인 `-unknownSmoke` 옵션 없이는 자동 입력이 실행되지 않습니다. 실제 Relay/익명 인증을 사용하며 첫 턴은 실제 60초를 기다려 만료를 검사합니다. 이후 각 프로세스가 자기 수신 데이터만으로 합법 행동을 선택합니다. `Library/UNKNOWN-RelaySmoke.code.host.json` 및 `.guest.json`에 진행 상태, 오류, 공개 상태와 이벤트 수를 기록합니다. 두 보고서의 최종 공개 상태가 일치해야 합니다. 이 검사는 같은 PC 두 프로세스 검사이며 서로 다른 인터넷 회선의 두 PC 검증을 대신하지 않습니다.

## Dashboard

기존 방 연결이 성공한 프로젝트의 설정을 유지합니다. 추가 서비스 생성이나 키 발급은 필요하지 않습니다. 다른 Cloud 프로젝트에 복제하는 경우 Unity Editor의 `Edit > Project Settings > Services`에서 올바른 프로젝트를 연결하고, Unity Dashboard에서 해당 프로젝트의 Authentication 설정에서 Anonymous 로그인이 가능하며 Multiplayer/Relay 서비스를 사용할 수 있는지 확인합니다. 이 작업에서 Dashboard 권한·요금·서비스 설정을 변경하지 않습니다.

## 검증 결과

프로토콜 포함 순수 C# 테스트 64개 통과. 실제 에디터/Windows Relay 검증 결과는 작업 완료 시 아래에 기록합니다.
