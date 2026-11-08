# UNKNOWN Alpha 0.3 — 포커 시너지와 카드 사용 UX

## 동작

실제 필드의 살아 있는 일반 카드로 가장 높은 족보 하나를 판정한다. 손패, JOKER, UNKNOWN은 구성 카드에서 제외한다. `Rank.Ace`의 기존 enum 값과 포커 순서는 다르므로 판정 전 2~14로 변환한다. A2345와 TJQKA를 인정하고 순환 스트레이트는 인정하지 않는다.

| 족보 | 대상 | ATK | 최대·현재 HP | 추가 효과 |
|---|---|---:|---:|---|
| ONE PAIR | 같은 계급 2장 | +2 | 0 | |
| TWO PAIR | 아군 필드 전체 | +2 | +1 | UNKNOWN도 강화 대상 |
| TRIPLE | 같은 계급 3장 | +3 | +2 | |
| STRAIGHT | 아군 필드 전체 | +3 | 0 | 플레이어별 경기당 1회 2장 드로우 |
| FLUSH | 아군 필드 전체 | +2 | +4 | |
| FULL HOUSE | 아군 필드 전체 | +4 | +4 | |
| FOUR OF A KIND | 같은 계급 4장 | +5 | +3 | |
| STRAIGHT FLUSH | 아군 필드 전체 | +6 | +6 | |
| ROYAL FLUSH | 아군 필드 전체 | +8 | +8 | |

상위 족보만 활성화하므로 스트레이트 플러시와 로열 플러시에 스트레이트 드로우를 중복 적용하지 않는다. 일반 스트레이트 보상은 손패 10장과 남은 덱 수의 기존 제한을 따른다. 매 경기 새 `PlayerState`를 생성하므로 사용한 드로우 보상과 이전 족보가 초기화된다.

카드의 기본 능력치, 카드 능력, 맵, JOKER, 포커, 기타 변화량을 분리한다. 포커 변경 시 포커 보너스만 교체한다. 최대 체력 변화의 실제 차이만큼 현재 체력도 변경하여 이미 받은 피해를 보존한다. 현재 체력 0 이하 유닛을 제거한 뒤 재판정하며, 최대 필드 카드 수에 의해 제한된 반복문으로 파괴 연쇄를 정리한다. 중간 족보는 별도 연출하지 않고 최종 족보 전환과 최종 능력치 차이만 알린다.

기존 JOKER의 1.5배 올림과 조커 맵의 배율 사건은 실행 시점의 최종 능력치에 적용한다. 그때 실제 증가·감소한 양을 각각 JOKER·맵 변화량으로 기록한다. 이후 포커가 해제되어도 해당 행동으로 얻은 변화량은 유지된다. 기존 카드 능력과 사건의 계산식을 변경하지 않는다.

## UI

- 아군·상대 필드 옆에 `MY SYNERGY`, `ENEMY SYNERGY`, 족보와 대상·효과를 표시한다.
- 배지를 누르면 왼쪽 여백에 9종 조건·효과 목록을 펼친다. 기본은 접힘이며 아군 활성 족보는 금색, 상대 족보는 분홍색이다. 기존 오른쪽 사이드바는 추가하지 않는다.
- 구성 카드에 작은 보라색 아이콘과 얇은 안쪽 테두리를 표시한다. 공격 가능 테두리와 공격 화살표 레이어는 별도로 유지한다. 활성화 시 약한 빛과 1.1초 알림, 하향 교체 시 변경 알림, 완전 해제 시 LOST 알림을 표시한다.
- ATK는 기본값보다 높으면 초록, 낮으면 빨강이다. HP는 현재/최대를 분리하고 피해를 받은 현재 HP와 최대 HP의 강화 상태를 따로 표시한다. Hover는 실제 적용된 원인과 피해량을 보여준다.
- 마나가 부족한 손패도 정상 밝기로 표시하고 Hover·드래그를 허용한다. 유효한 아군 슬롯으로 놓으면 Host가 기존 규칙으로 검증한다. 거절 시 손패·필드·마나를 보존하고 0.22초 복귀 효과 및 필요/현재 마나가 포함된 1.5초 안내를 표시한다. 반복 시 안내 한 개를 갱신한다.

## 온라인

Host의 실제 `BattleEngine`만 족보, 대상, 체력 변화, 드로우와 파괴를 결정한다. Client view의 `ReconcilePoker()`는 아무 효과를 실행하지 않는다. `BattleSnapshot`으로 양쪽 공개 족보, 구성/강화 대상 ID, 최종 수치와 원인별 변화량을 전달한다. 하한으로 잘린 수치도 원인 합계가 일치하도록 내부 원본 수치를 함께 전달한다. 상대 손패·UNKNOWN/JOKER 승리 진행도는 기존대로 숨긴다.

동일 revision의 거절 응답도 별도 오류 횟수로 관찰한다. 따라서 동일한 마나 부족 요청을 반복해도 UI 메시지와 복귀 효과가 다시 동작하며 전투 이벤트를 재생하지 않는다. 메시지 채널은 v7이다. **양쪽 PC에 이번 빌드를 함께 배포해야 한다.**

## 수정 위치

| 파일 | 역할 |
|---|---|
| `Assets/UNKNOWN/Battle/Runtime/PokerSynergy.cs` | 9종 판정, 유지형 보너스, 연쇄 파괴, 드로우 보상 |
| `CardData.cs` | 독립 능력치 계층과 원인별 수치 |
| `BattleEngine.cs`, `JokerMapRules.cs` | 기존 소환·파괴·카드·맵·JOKER 경로 연결 |
| `BattleProtocol.cs`, `BattleEvents.cs` | 공개 상태와 족보 전환 이벤트 |
| `BattleUIController.Poker.cs` | 배지·목록·상세 수치·색상·반환 효과 |
| `BattleUIController.cs`, `.Hands.cs`, `.Interaction.cs`, `.Online.cs`, `.Map.cs` | 기존 UI/드래그/온라인 연결 |
| `CardAnimationController.cs` | 시너지 알림 및 MAX HP 변화 표시 |
| `Assets/UNKNOWN/Multiplayer/Runtime/NetworkBattleController.cs` | v7 채널과 반복 거절 알림 |
| `NetworkBattleSmokeRunner.cs` | Development Build 전용 명시적 Relay 자동 검증 |
| `Tests/UNKNOWN/PokerTests.cs` | 포커 및 상호작용 순수 규칙 검증 |
| `BattleUIController.PokerChecks.cs`, `Assets/UNKNOWN/Multiplayer/Editor/AttackCheckMenu.cs` | Editor 전용 실제 UI·직렬화·연출 검사 |

## 에디터에서 확인하기

1. 기존 `Assets/Scenes/SampleScene.unity`를 연다. 기존 NetworkManager·UnityTransport·RoomController 설정을 그대로 사용한다.
2. 컴파일이 끝난 후 Play에서 기존 방 코드 흐름으로 접속한다. 새 UI는 기존 `BattleUIController`가 런타임에 생성하므로 씬/프리팹에 별도 슬롯이나 컴포넌트를 수동 연결할 필요가 없다.
3. `UNKNOWN > Multiplayer > Run Attack UI Checks`는 6단계 전체 검증을 실행한다. `Run Poker UI Checks`로 새 기능만 검사할 수도 있다. 자동 테스트는 격리 프로젝트에서 실행했으며 사용자의 실행 중인 에디터는 종료하지 않았다.
4. 순수 규칙 검사는 프로젝트 루트에서 `powershell -ExecutionPolicy Bypass -File Tests/UNKNOWN/Run-BattleTests.ps1`로 실행한다.
5. 실제 두 PC 검증은 `Builds/UNKNOWN` 폴더 전체를 각 PC에 복사하여 같은 빌드로 실행한다. EXE만 복사하면 안 된다.

씬·프리팹, 설치 패키지, Cloud Project/Environment, 인증 프로필은 이번 기능을 위해 변경하거나 초기화하지 않았다. NetworkManager도 추가하지 않았다.

## 검증 기록

규칙 검사 144개와 Play Mode 검사 414개가 모두 통과했고 런타임 오류는 0이었다. Windows Development Build 성공: 오류 0, 기존 격리 환경/URP 관련 경고 8. 실제 EXE 2개로 Relay에 접속해 하트 맵 9턴, 재경기 조커 맵 19턴을 끝까지 진행했다. 서로 다른 Player ID, 일치하는 Project ID/Environment, 9개의 공개 시너지 전환, 양쪽 최종 공개 상태와 UI 수치 일치, 각 EXE·경기별 2회씩 총 8회 마나 부족 거절을 확인했다. 상대 비공개 정보, 맵 사건, 결과 화면, 대기실 복귀 및 마지막 방 나가기 검사도 통과했다.

최종 결과와 원본 JSON, 렌더링 캡처는 [검증 기록](Validation/Poker-Alpha03/README.md)에 저장했다. 실제 다른 두 PC, 인위적인 지연·패킷 손실 환경은 이 작업 환경에서 검증하지 못했다.
