# 상대 필드 표시 및 UNKNOWN 승리 수정

## 원인과 수정

`BattleProtocol.cs`의 이전 필드 배열은 빈 슬롯을 null로 직렬화했습니다. Unity 6000.0.74f1의 실제 JsonUtility 왕복 검사에서 이 null이 빈 객체로 복원되는 현상을 재현했습니다. 빈 객체가 CardDatabase 조회로 들어가면 Client의 화면 생성/갱신이 예외로 중단될 수 있었습니다. 이제 모든 슬롯에 명시적인 `occupied` 값을 전송하며, 비점유 슬롯은 카드 복원을 하지 않습니다. 공개 필드 슬롯 번호와 카드 ID는 유지합니다.

`NetworkBattleController.LocalPlayer`와 UI 배치는 수신한 스냅샷의 `recipient`를 기준으로 고정합니다. Host/Client 역할이나 현재 턴이 바뀌어도 화면 위는 상대, 아래는 자신입니다. 턴 표시에는 별도로 `ActivePlayer`를 사용합니다. 자기 손패만 상세 데이터로 받고 상대 손패는 수량과 뒷면으로 표시합니다.

전투 상태와 UNKNOWN 진행도는 결과 수신 시 먼저 적용합니다. 애니메이션은 기존 시각 오브젝트를 사용해 재생하고 최종 보드를 갱신합니다. 승리 판정과 입력 차단은 애니메이션 완료를 기다리지 않습니다.

기존 `UnknownCreated`는 누적 생성 횟수였지만, 3회 생성 시 승리하는 분기가 없었습니다. Host 엔진의 Combine 성공 후 세 번째 생성이면 `FinishGame(winner, UnknownComplete)`를 호출합니다. HP 승리도 같은 단일 종료 경로를 사용하며 승리 사유는 스냅샷과 GameOver 이벤트로 동기화됩니다. 종료 뒤 소환/공격/조합/조커/턴 종료/드로우/시간 초과 처리를 차단합니다. 새 엔진 생성 시 HP·마나·카운트·보드·승패가 초기화됩니다.

양쪽에 UNKNOWN 수치와 3개의 보라색 진행 슬롯을 표시합니다. 3개 완료 시 발광하고, 카드 뒷면 3장/보라색 빛/UNKNOWN COMPLETE 연출 뒤 각 수신자 관점에 맞는 VICTORY 또는 DEFEAT와 이유를 표시합니다. 숨겨진 UNKNOWN의 종류를 공개하는 연출은 사용하지 않습니다.

결과 화면에서 메인 메뉴 또는 연결이 유효하면 대기실로 돌아갈 수 있습니다. Client의 복귀 요청도 Host가 처리하며, 세션 잠금을 해제하고 양쪽 준비·전투 상태를 초기화합니다.

## 수정 범위

- BattleEngine.cs: 단일 승리 확정 경로, UNKNOWN 승리, 승리 사유, 종료 후 드로우 차단.
- BattleProtocol.cs: 점유 여부가 명시된 슬롯, 승리 사유 전송/뷰 복원.
- BattleUIController.cs / .Online.cs: 로컬 관점, 진행도, 결과 화면, 타이머 종료, 대기실 버튼.
- CardAnimationController.cs: UNKNOWN 완료 전용 연출.
- NetworkBattleController.cs: 수신자 ID 매핑, 종료 시 남은 시간 0, 경기 초기화.
- RoomController.cs / RoomTestUI.cs: 결과에서 대기실 복귀와 UI 정리.
- BattleUIController.PvPChecks.cs / OnlineTestMenu.cs / NetworkBattleSmokeRunner.cs / BattleRulesTests.cs: 회귀/실행 검증.

씬 프리팹을 강제로 활성화하거나 NetworkManager를 추가하지 않았습니다. 기존 SampleScene 및 UNKNOWN_Battle_Test의 런타임 UI 생성 경로를 사용하므로 씬 참조를 다시 연결할 필요가 없습니다. 카드 데이터·마나 비용·조커 확률·효과·60초 규칙은 유지합니다.

## 검증

- 규칙/프로토콜 테스트: 67개 통과. 양 플레이어의 세 번째 생성 즉시 종료, 누적 카운트, 종료 이벤트 1회, 종료 뒤 모든 명령·드로우 차단, HP 승리 사유, 새 경기 초기화를 포함합니다.
- Unity Play Mode: `UNKNOWN > Multiplayer > Run PvP UI Regression Checks`에서 22개 통과, 실패 0, 런타임 오류 0. 실제 JsonUtility로 이전 null 문제를 재현했고, 새 슬롯의 JSON 왕복 뒤 두 관점의 보드·문양·정보·마나, UNKNOWN 완료 연출·발광·승패 문구·입력 차단을 검사했습니다. 보고서는 `Library/UNKNOWN-PvP-Regressions.json`입니다.
- 테스트용 카드 지급/관점 전환은 UNITY_EDITOR 전용 검사 코드에만 있으며 일반 온라인 플레이와 Windows 빌드에 포함되지 않습니다.

실제 Relay Host/Client 검증 결과는 아래에 추가합니다.
