# UNKNOWN 실습형 튜토리얼

타이틀의 **튜토리얼**을 선택한다. 기록이 없으면 `튜토리얼 시작`, 진행 기록이 있으면 `이어하기`와 `처음부터`를 제공한다. 완료 후에도 처음부터 재생할 수 있다. 온라인 방, Authentication 로그인, Relay 연결을 호출하지 않는다.

## 구조와 판정

기존 `BattleUIController` Canvas 하나와 실제 `BattleEngine`을 사용한다. 씬을 전환하거나 전투 규칙을 복제하지 않는다. `TutorialSession`은 지정된 원본 덱에서 카드를 이동해 단계별 초기 상태를 만들고, 실제 전투 이벤트와 최종 상태를 함께 확인한다. 전투 엔진의 카드 수치·확률·효과·맵·네트워크 코드는 변경하지 않았다.

| 단계 | 실제 완료 판정 |
|---|---|
| 1 | HP, 능력치, 손패/덱, 필드, 마나, 턴, 승리 조건 7개 설명을 직접 확인 |
| 2 | Summon 이벤트 + 필드 카드 존재 + 실제 마나 감소 + 소환 효과 처리 완료 |
| 3 | 유닛 대상 Attack 및 직접 Attack 이벤트 + 상대 필드 제거 + 실제 HP 감소 |
| 4 | 실제 Draw/Discard 초과 시연 + 수동 Discard 이벤트 + Hand 7/8 + 사용 횟수·묘지 확인 |
| 5 | 실제 OnePair 판정 + 두 카드의 PokerAttack +2 + 소환 효과 처리 완료 |
| 6 | 실제 JokerSuccess 또는 JokerFailure 이벤트 + 사용한 JOKER 폐기 확인. 성공을 강요하지 않음 |
| 7 | JACK/QUEEN/KING 각각의 Combine 이벤트 + UnknownCreated 3 + 기존 UnknownComplete 승리 |

♥5의 최대 체력 증가 대상 선택도 기존 규칙대로 수행한다. ♠5 효과는 적이 없는 상황에서 기존 엔진 규칙대로 처리한다. JOKER는 기존 50% 판정과 1.5배 올림 계산을 그대로 사용한다. 마지막 조합의 세 번째 생성은 기존 승리 판정을 실제로 발생시킨다.

## 조작과 상태 정리

상단의 목표와 조작 문구, 7단계 진행도, 왼쪽의 짧은 규칙/도움말/재시작 버튼을 제공한다. 얇은 발광 테두리는 Raycast를 받지 않는다. 현재 목표와 무관한 명령, 턴 종료, 타이머, 랜덤 맵 연출, 맵 사건, 상대 임의 행동을 튜토리얼에서만 제한한다. 일반 대전은 기존 40초·자동 드로우·온라인 Host 판정을 유지한다.

단계 완료 후 기존 연출이 끝나면 체크포인트를 저장하고 약 1.5초 완료 배너 후 자동 전환한다. 매번 같은 Canvas를 유지하면서 새 로컬 초기 상태를 만들고 이전 드래그·선택·손패 뷰·일시 효과·연출을 정리한다. 재시작 버튼은 현재 단계의 초기 상태를 복원한다. 목표를 달성하지 않고 시간이 지났다고 완료시키지 않는다. 25초 정체 힌트는 단계마다 한 번만 표시한다.

UNKNOWN은 중앙 `UNKNOWN 조합` → A 클릭 → J/Q/K 클릭 → 같은 버튼의 `조합 확정` 순서다. 성공 연출 이후 다음 재료 쌍만 지급하며, 이미 만든 UNKNOWN도 손패에 유지한다. ESC는 선택 취소가 우선이며, 다시 누르면 기존 메뉴가 열린다. `튜토리얼 종료`로 타이틀에 돌아가고 기존 설정을 열 수 있다.

기존 `GamePreferences` SFX 채널에 등록한 짧은 합성음을 단계 시작/완료 및 행동 결과에 재생한다. 음량·음소거 설정을 따른다. 외부 음원이나 새 네트워크 오브젝트는 필요하지 않다.

## 저장

PlayerPrefs 키 `UNKNOWN.Tutorial.v1`의 JSON에 `lastCompleted`, `nextStep`, `completed`를 저장하고 `PlayerPrefs.Save()`를 호출한다. 진행 중 카드 상태는 저장하지 않는다. 종료 후 이어하기는 미완료 단계의 초기 상태부터 시작한다. `처음부터`는 이 키만 초기화한다. 인증·음량·화면 설정은 초기화하지 않는다. 손상되거나 범위를 벗어난 진행 값은 1~7 범위로 복구한다.

## 파일

추가: `TutorialSession.cs`(시나리오/목표), `TutorialProgress.cs`(체크포인트), `BattleUIController.Tutorial.cs`(HUD/연출/입력/전환), `BattleUIController.TutorialChecks.cs` 및 `TutorialCheckMenu.cs`(Editor 검증), `Tests/UNKNOWN/TutorialTests.cs`.

수정: `BattleUIController.cs`, `.Online.cs`, `.Interaction.cs`, `.Attack.cs`, `.HandActions.cs`의 튜토리얼 전용 분기, `RoomTestUI.cs`의 타이틀 진입, `.AttackChecks.cs`의 캡처 시 강조 위치 갱신, 기존 규칙 테스트 실행 스크립트와 호출 목록.

## Unity에서 실행

1. 기존 `Assets/Scenes/SampleScene.unity`를 열고 Play 한다. `UNKNOWN_Battle_Test`는 타이틀용 씬이 아닌 기존 로컬 디버그 씬이다.
2. 타이틀 → 튜토리얼 → 시작 또는 이어하기를 선택한다.
3. 새 씬/프리팹의 Inspector 연결은 필요 없다. 기존 RoomTestUI가 메뉴와 전투 UI를 런타임에 생성한다. NetworkManager를 추가하지 않는다.
4. 자동 확인은 `UNKNOWN > Tutorial > Run Tutorial Checks`에서 실행한다. 이 메뉴는 SampleScene이 필요하며 테스트 전의 튜토리얼 저장 값을 복원한다.
5. 순수 규칙 검증은 `Tests/UNKNOWN/Run-BattleTests.ps1` 또는 `.cmd`를 실행한다.

실제 검증 결과와 한계는 `Docs/Validation/Tutorial`에 별도 기록한다.
