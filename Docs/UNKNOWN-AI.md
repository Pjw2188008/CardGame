# UNKNOWN 오프라인 AI 대전

## 실행

`Assets/Scenes/SampleScene.unity`에서 Play → **AI 대전** → EASY / NORMAL / HARD 선택 → **대전 시작**. 기존 튜토리얼, 온라인 대전, 설정, 종료 메뉴를 유지한다. 기존 `게임 시작` 온라인 진입 버튼의 표시 이름은 `온라인 대전`으로 명확히 했다.

Windows Development Build: 강화 HARD 버전은 `Builds/UNKNOWN-AI-Hard/UNKNOWN.exe`, 이전 AI 버전은 `Builds/UNKNOWN-AI/UNKNOWN.exe`다. 실행 파일만 분리하지 말고 해당 폴더 전체를 함께 사용한다. Unity에 기존 Play가 실행 중이면 종료하고, Assets > Refresh 후 SampleScene에서 다시 시작한다. UNKNOWN_Battle_Test는 기존 로컬 디버그 씬이며 타이틀 메뉴 씬은 SampleScene이다.

새 씬·프리팹·NetworkManager 추가는 필요 없다. 기존 RoomTestUI가 선택 화면과 전투 Canvas를 생성한다. AI 경로는 Authentication, UnityServices 초기화, Relay, 방 코드 API를 호출하지 않는다. 기존 온라인 메뉴에서만 해당 연결 경로를 사용한다.

## 전투 구조

- UNKNOWN 승리는 손패 생성 누적이 아니라 **살아 있는 아군 필드 UNKNOWN 3장 동시 배치**로 판정한다. 파괴된 카드는 진행도에서 빠지고 재조합할 수 있다. 조커 맵 예외 및 상세 규칙은 `UNKNOWN-FieldVictory.md`를 따른다.
- 사람은 항상 플레이어 0, AI는 플레이어 1이다. 로컬 핫시트와 달리 턴이 바뀌어도 시점과 자신의 손패를 유지한다. AI 손패는 뒷면으로 표시한다.
- `BattleCommands.Execute`가 공통 명령 진입점이다. 로컬 사람 입력, AI, 온라인 Host의 BattleAuthority가 모두 같은 BattleEngine 소환/공격/효과 대상/조합/도박/버리기/턴 종료 함수를 호출한다. 온라인의 인증된 actor, sequence, revision 검증은 그대로 유지한다.
- 기존 54장, HP 30, 필드 5장, 손패 8장, 마나, 소환 후 공격 제한, 버리기 1회, 포커 9종, UNKNOWN/JOKER 맵별 승리 조건을 변경하지 않는다.
- 기존 RandomMapSelection 및 BattleMapSettings를 사용해 문양 4종 각 22.5%, 조커 10%를 추첨한다. 재대전은 동일 난수 스트림에서 새 추첨을 수행한다. 같은 맵이 연속으로 나올 수 있다.
- 40초 타이머를 양쪽에 동일하게 적용한다. AI는 맵/드로우/행동 연출 종료 후 약 0.4초를 기다려 한 행동씩 실행한다. 실행 후 관측 데이터를 다시 만든다. 최대 32회 행동 및 타임아웃 처리로 무한 턴을 방지한다. 진행 중인 소환 효과는 기존 만료 처리로 완료한다.
- 전투 메뉴와 설정은 기존 구조를 사용한다. AI 결과 화면에서 난이도, 맵, 턴 수, 승리 사유, 나의 UNKNOWN/JOKER 진행을 표시하고 재대전·난이도 선택·타이틀 복귀를 제공한다.

## 난이도

| 난이도 | 판단 |
|---|---|
| EASY | 합법 후보 중 낮은 소환 비용을 다소 선호하는 가중 무작위 선택. 공격과 소환을 지속하며, 조합 25%·도박 20%·버리기 10%의 후보 채택 확률을 사용한다. 이 수치는 **행동 선택 확률**이며 실제 JOKER 성공 확률을 변경하지 않는다. 즉시 직접 승리와 세 번째 UNKNOWN 승리는 우선한다. |
| NORMAL | 공격력/체력/비용/고유 능력/맵 소환 보너스, 포커 강화량, 상대 제거와 반격 손실, 시너지 핵심 유닛, 손패 공간 및 조합 재료를 평가한다. 불리한 교환을 피하고 필요 시 여러 장을 사용한다. |
| HARD | 포커 강화와 향후 재료 보존을 평가하고, 최대 12개 시작 후보에서 자기 행동 최대 4번을 이어 실행한다. 각 단계 상위 3개를 비교한 뒤 상대 공개 필드의 공격 최대 5번을 시뮬레이션한다. 방해 유닛 제거 순서, 남길 직접 공격, 다음 턴 패배 방지와 맵별 승리 진행, JOKER 성공/실패 기대값을 함께 비교한다. 기본 탐색 예산은 384노드다. |

HARD는 전체 게임의 완전 탐색이나 사람 수준의 최적 플레이를 보장하지 않는다. 노드 예산을 기준으로 탐색하며, 기기 부하가 높으면 100ms 비상 중단 검사에서 이미 비교한 후보 또는 휴리스틱 결과를 반환한다. 검사는 개별 시뮬레이션 사이에서 수행하므로 전체 Decide의 엄밀한 시간 상한은 아니다. 게임 스레드에서 실행하며 실제 행동 전에는 기존 40초 마감 시간을 다시 검사한다.

### HARD 조절 지점

`new AiAgent(AiDifficulty.Hard, seed, new AiSearchSettings(candidates:12, continuations:3, ownSteps:4, replySteps:5, nodeBudget:384))`로 정책을 교체할 수 있다. 기존 2인자 생성자는 이 기본값을 사용한다. 후보 수는 1~16, 단계별 비교 수는 1~5, 자기 행동은 1~6, 상대 반격은 0~5, 노드는 16~1024로 제한한다. `replySteps:0`은 반격 탐색을 끄는 비교용 설정이다. 설정은 에이전트별로 독립적이며 카드 능력치·마나·도박 확률은 바꾸지 않는다. 더 깊은 탐색이 언제나 더 높은 승률을 보장하지는 않으므로 시드와 좌석을 바꾼 대전 결과로 조정한다.

자기 후속 행동은 처음 관측한 손패만 사용한다. 가상 드로우로 받은 미지 카드의 정체를 이용해 계획하지 않는다. 상대 반격은 공개 필드의 합법 공격만 사용하고, 알려지지 않은 상대 손패 소환은 예측하지 않는다. 상대는 불리한 공격을 생략할 수 있다. 모든 후보는 실제 엔진 규칙과 소환 효과를 거치지만, 턴 시작 사건·무작위 효과는 제한된 가정 분기이므로 모든 확률 사건을 완전 탐색하지는 않는다. 매 실제 행동 이후 다시 관측하고 계획한다.

## 비공개 정보와 가상 상태

`AiObservation.Capture`는 기존 BattleSnapshot의 recipient 필터를 통해 AI 자신의 손패와 양쪽 공개 필드·HP·마나·공개 규칙만 복사한다. 상대 손패는 빈 배열, 상대 UNKNOWN/JOKER 누적 진행은 0으로 마스킹되며, 실제 덱 카드나 덱 순서는 전달하지 않는다. 실제 사건으로 공개된 한 장은 해당 턴의 공개 필드로만 전달한다. 에이전트는 실전 BattleEngine 참조를 받지 않는다.

가상 엔진은 이 관측 사본에서만 구성한다. 알려지지 않은 드로우/손패는 고정된 공개 기본 덱 사전 분포의 가정값이며 실제 카드 순서의 예측이 아니다. JOKER 후보는 가상 성공·실패 분기를 기존 능력치 계산으로 실행하고 현재 공개 확률로 가중한다. 실제 엔진의 난수 상태·DebugNextGamble을 사용하거나 수정하지 않는다.

AI 행동 연출은 인간 플레이어 0용 EventViewData로 필터링한다. 비공개 드로우 카드, 조합 재료/종류/생성 진행은 기존 온라인처럼 숨긴다. AI가 실제 소환하거나 공격한 필드 카드는 공개한다. 비공개 조커 사건 이름/내용도 MapEventView로 가린다. 결과 화면에는 상대의 비공개 승리 카운터를 출력하지 않는다.

## 추가/수정 파일

추가:
- `Assets/UNKNOWN/Battle/Runtime/BattleCommands.cs`: 공통 명령 실행
- `AiObservation.cs`: 정보 제한 관측 및 가상 엔진 복원
- `AiAgent.cs`: 합법 후보, 난이도 평가, 제한 탐색
- `BattleUIController.AI.cs`: 고정 시점, AI 턴/재대전/결과/비공개 연출
- `BattleUIController.AiChecks.cs`, `Multiplayer/Editor/AiCheckMenu.cs`: 실제 메뉴와 전투 UI 자동 검사
- `Tests/UNKNOWN/AiTests.cs`: 규칙·전략·정보 제한·AI 대 AI 실행기

수정:
- `BattleProtocol.cs`: 기존 Host 검증 이후 공통 실행기 호출
- `BattleUIController.cs`, `.Online.cs`, `.Hands.cs`, `.Map.cs`: 기존 로컬/온라인/튜토리얼 분기를 유지한 AI 연결
- `RoomTestUI.cs`: 난이도 선택 및 기존 메뉴 연결
- 규칙 테스트 실행 스크립트와 호출 목록

## 개발 검증

- `Tests/UNKNOWN/Run-BattleTests.ps1` 또는 `.cmd`: 기존 규칙과 AI 테스트, UI 없이 3난이도 × 5맵 × 3시드 = 45경기 AI 대 AI 대전. 불법 명령, 마나 음수, 손패 8장 초과, 중복 ID, 카드 수 보존, 종료/루프를 검사한다.
- Unity `UNKNOWN > AI > Run AI UI Checks`: SampleScene에서 실제 타이틀 경로, 세 난이도 AI 턴, 포인터 입력, 숨김 손패, 실제 명령에 의한 승패, 연속 재대전 상태 초기화를 검사한다. 결과는 `Library/UNKNOWN-AI-Checks.json`.
- Unity `UNKNOWN > AI > Run AI Tutorial and Regression Suite`: AI → 튜토리얼 → 기존 온라인/맵/전투 UI 회귀를 순서대로 실행한다. Editor 전용 `OfflineModesCheckMenu.cs`가 검사를 연결한다.
- 개발 빌드에서만 선택 행동, 이유, 휴리스틱 후보 점수, 탐색 노드 수, 성공/실패, 턴 종료 이유를 로그에 출력한다. 일반 플레이 UI에는 표시하지 않는다.

최초 AI 개발 검사는 `Docs/Validation/AI`, HARD 강화 이후 실제 검사와 한계는 `Docs/Validation/AI-Hard`에 별도로 기록한다.
