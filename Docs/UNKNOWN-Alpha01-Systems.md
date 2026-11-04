# Alpha 0.1 클릭 조합·손패 보충·40초 턴

## 조작

중앙 `조합 선택` 또는 재료 표시 영역을 누르면 조합 모드에 들어갑니다. 자신의 손패에서 A와 J/Q/K를 클릭하면 보라색 테두리와 중앙 재료·결과 미리 보기가 표시됩니다. `UNKNOWN 조합`을 눌러야 Host에 두 인스턴스 ID를 전송합니다. 선택만으로는 손패·마나·생성 횟수가 바뀌지 않습니다.

같은 카드를 다시 누르면 해제되고, 다른 A 또는 J/Q/K는 기존 선택을 교체합니다. ESC, 중앙 `선택 취소`, 턴 종료 시 모드와 재료가 초기화됩니다. 조합 모드에서는 소환 선택·손패 드래그·필드 공격 드래그를 막고, 모드를 나가면 원래 조작을 복원합니다. 재료 영역은 더 이상 드래그 드롭을 받지 않습니다. 일반 소환과 JOKER 도박은 기존 드래그 방식입니다.

## 규칙

턴 시작 직전 손패가 0장일 때만, 실제 덱에서 최대 5장을 뽑습니다. 이 경우 일반 턴 드로우를 추가하지 않습니다. 손패가 1장 이상이면 기존처럼 1장만 뽑습니다. 덱이 부족하면 남은 카드만 뽑으며 덱을 다시 만들지 않습니다. 작은 HandLimit을 쓰는 테스트 설정에서는 손패 한도를 초과하지 않습니다. 턴 중 손패를 모두 썼다고 즉시 보충하지 않습니다.

각 카드의 기존 Draw 이벤트와 순차 드로우·중앙 정렬 연출을 사용합니다. 보충 안내 `손패 소진 — 카드 N장 보충`은 소유자에게만 표시합니다. 상대에게는 카드 뒷면과 수량만 전달되며 실제 정의·ID나 전용 보충 이벤트는 전달하지 않습니다. UNKNOWN 조합 이벤트와 생성 횟수도 계속 소유자만 받습니다.

## 타이머

기본 40초입니다. 온라인은 Host의 NetworkBattleController가 최종 권한을 가집니다. 턴 시작 때 양쪽 화면의 드로우 연출 완료를 확인할 때까지 busy 상태와 40초를 유지하고, 완료 후 Host가 서버 시각 기준 마감 시각을 발행합니다. 일반 공격·조합 연출에서는 기존처럼 시간이 흐릅니다. 0초에는 새 행동을 거절하고 승인된 행동의 처리가 끝난 다음 턴을 넘깁니다.

표시는 40~11초 기본색, 10~6초 주황색, 5~1초 빨간색, 0초 `TIME OUT`입니다. 게이지 분모는 Host가 보낸 설정값을 사용합니다. 온라인 메시지 버전은 v3이므로 양쪽 모두 같은 새 빌드를 사용해야 합니다.

## 파일 및 에디터 설정

- `BattleEngine.cs`, `BattleEvents.cs`, `BattleProtocol.cs`: 빈 손패 검사·실제 Draw 이벤트·비공개 보충 이벤트·타이머 메타데이터. 기존 Combine 판정과 카드 데이터는 유지했습니다.
- `BattleUIController.Interaction.cs`, `.Hands.cs`, `.cs`, `.Online.cs`, `.Attack.cs`: 클릭 선택 모드·보라색 표시·미리 보기·보충 안내·타이머 표시와 기존 드래그의 상호 배제.
- `NetworkBattleController.cs`: Inspector의 40초 기본 설정·서버 마감 시각·턴 시작 연출 완료 메시지·Host 행동 잠금.
- `SampleScene.unity`: 기존 NetworkManager 오브젝트에 NetworkBattleController 설정을 직렬화했습니다. NetworkManager는 추가하지 않았습니다.
- `UNKNOWN_Battle_Test.unity`: 로컬 전투 테스트의 turnDuration을 40으로 변경했습니다.
- `RoomSetupMenu.cs`: 테스트 UI 설치 시 기존 NetworkManager에 NetworkBattleController가 없을 때만 추가합니다.
- `BattleRulesTests.cs`, UI Checks, `NetworkBattleSmokeRunner.cs`: 변경 규칙 및 실제 UI 입력·연출·온라인 회귀 검사.

온라인 턴 시간은 `SampleScene > NetworkManager > Network Battle Controller > Turn Duration`, 로컬은 `UNKNOWN_Battle_Test > Battle UI Controller > Turn Duration`에서 설정합니다. 새 UI는 런타임에 구성하므로 버튼이나 슬롯을 Inspector에 수동 연결할 필요가 없습니다. 작업 전부터 씬이 열려 있어 새 직렬화 컴포넌트가 보이지 않는 경우 저장된 SampleScene을 다시 열어 확인합니다.

## 검증

| 검사 | 결과 |
|---|---|
| 규칙·Host 유효성·빈 손패/덱 부족·비공개 보충 이벤트 | 74 통과 |
| 기존 공격 UI·화살표 렌더링 | 43 통과 / 실패 0 / 오류 0 |
| 클릭 조합·기존 드래그·보충 연출·로컬 타이머 | 60 통과 / 실패 0 / 오류 0 |
| PvP 화면·비공개 UNKNOWN·승리 | 22 통과 / 실패 0 / 오류 0 |

클릭 검사는 실제 손패 Button 이벤트를 호출하고, 스냅샷처럼 CardInstance 객체를 교체한 후에도 기존 버튼이 인스턴스 ID로 선택하는지 확인합니다. 선택·재클릭·교체·ESC·턴 종료·소환 충돌·명시적 조합 버튼을 확인했습니다. 보충 검사는 pending 카드가 미리 나타나지 않고 순차 연출·중앙 정렬이 끝난 뒤 40초 카운트가 시작되는 것을 실제 Coroutine으로 검증합니다. 중앙 조합 화면을 1920×1080으로 렌더링하여 보라색 테두리와 배치를 확인했습니다.

결과 JSON, 규칙 검사 로그, 조합 화면은 `Docs/Validation/Systems-Alpha01/`에 저장했습니다. Play Mode 전체 로그는 `Library/UNKNOWN-Systems-Suite.log`입니다. 첫 임포트 중 강제 재컴파일로 Play Mode 진입이 취소된 첫 시도는 검사 시작 전 timeout이었으며, 임포트 완료 후 위 검사를 재실행했습니다. 이는 통과 결과에 포함하지 않았습니다.

### Windows / Relay 실제 검증 (2026-11-04)

Windows Development Build는 성공했고 오류 0개, 경고 11개입니다. 경고에는 설치된 Test Framework의 폐기 예정 Switch API, Input System/UGUI의 미사용 필드, URP 셰이더 형 변환 및 Services 프로젝트 연결 경고가 포함됩니다. 프로젝트의 전투 C# 컴파일에서는 오류가 없었습니다. 설치된 패키지를 수정하거나 업그레이드하지 않았습니다. 전체 빌드 로그는 `Library/UNKNOWN-Systems-Windows-Build.log`입니다.

사용자 에디터를 유지하기 위해 `Library/UNKNOWN-Systems-BuildProject`에서 빌드했습니다. 원본과 UNKNOWN C# 파일이 모두 일치하는 것을 해시로 확인했으며 기존 설치 버전의 패키지 소스 76개를 이 격리 프로젝트에만 복사했습니다. 원본 Library나 패키지 설정을 초기화하지 않았습니다. 최종 빌드는 `Builds/UNKNOWN/`에 반영했습니다.

같은 PC에서 EXE 두 개를 실제 Relay로 연결했습니다. Host PID 22664는 default, Client PID 48376은 unknown_local_2 프로필을 사용했으며 서로 다른 Player ID와 동일 Project ID/production 환경을 확인했습니다. Session Code WMKP9R로 참가했으며 검사 후 세션을 정리했습니다.

실행 검사는 `-unknownSmokeSystems`를 명시한 Development Build에서만 초기 손패를 0장으로 설정했습니다. 실제 54장 덱에서 드로우하며 카드를 임의 생성하거나 덱을 재구성하지 않습니다. 일반 실행의 초기 손패 설정은 바꾸지 않았습니다. 첫 턴은 의도적으로 기다려 실제 40초 자동 종료를 검사한 뒤, 봇이 실제 손패 Button 클릭과 `UNKNOWN 조합` 버튼을 통해 조합하고 기존 드래그 경로로 공격·JOKER를 실행했습니다.

| 항목 | 결과 |
|---|---|
| 첫 시간 초과 | Host 40.000초 / Client 관측 40.019초 |
| 보충·턴 시작 연출 중 고정된 40초 관측 | Host 634회 / Client 643회 |
| Host 보충 | 4회, 실제 덱에서 총 20장 |
| Client 보충 | 5회, 실제 덱에서 총 25장 |
| 클릭 조합 | Host 2회 / Client 3회 |
| 공격 | Host 27회 / Client 14회, 총 41회 결과 일치 |
| 승리 | 36턴, Client의 UNKNOWN 누적 3장 승리 |
| 양쪽 최종 공개 상태 / 화면 상태 | 일치 |
| 상대 손패 정의·UNKNOWN 생성 정보 | 비공개 유지 |
| 승리 화면 / 방 복귀 / 세션 정리 | 양쪽 모두 정상 |
| Runtime 오류 | Host 0 / Client 0 |

최종 자료는 `Docs/Validation/Systems-Alpha01/Relay-Host.json`, `Relay-Guest.json`, `Summary.json`입니다. 상세 로그는 `Library/UNKNOWN-Systems-Relay-Host.log`, `Library/UNKNOWN-Systems-Relay-Guest.log`입니다. 테스트용 세션 정리가 끝난 후 해당 EXE 두 개만 종료했습니다.

실행 파일은 `Builds/UNKNOWN/UNKNOWN.exe`입니다. 다른 PC에는 `Builds/UNKNOWN/` 폴더 전체를 복사하고 양쪽 모두 이 v3 빌드를 사용합니다. 재검사는 `UNKNOWN > Multiplayer > Run Drag UI Checks`, 공격 검사는 `Run Attack UI Checks` 메뉴를 사용합니다. 전체 배치 진입점은 `Unknown.Multiplayer.Editor.AttackCheckMenu.RunSuiteBatch`입니다.

### 남은 제한 사항

서로 다른 물리 PC 및 수동 전체 화면/창 모드 전환 검사는 수행하지 않았습니다. 같은 PC의 실제 Relay 두 EXE, 실제 Unity Play Mode와 렌더링을 검증했습니다. 온라인 실행 검사는 초기 손패 0장인 명시적 테스트 설정을 사용했으며, 일반 초기 손패와 기존 카드 데이터·마나·JOKER·공격·HP/UNKNOWN 승리는 규칙 및 UI 회귀 검사에 포함했습니다.
