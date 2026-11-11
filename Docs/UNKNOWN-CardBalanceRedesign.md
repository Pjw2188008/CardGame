# UNKNOWN 카드 밸런스 개편 구현·검증 보고서

검증일 2026-11-11. Unity 6000.0.74f1. 실제 적용 프로젝트는 `D:/git/ECHO-/Echogame/CardGame`이다. 첨부 명세를 최우선으로 적용했다. 개편 수치는 1차 실험안이며 최종 균형을 입증한 결과가 아니다.

## 적용 내용

- 실제 플레이 기본 데이터는 Revised다. 기존 Build 카탈로그는 Baseline으로 보존했다. 이전 57종 카드 정의와 수치·효과·설명을 전수 비교했고 동일했다. 수정 전 원본 C#과 해시를 Validation/CardRedesign/baseline-source.zip 및 BaselineManifest.json에 보존했다.
- 다이아 4 피해 3, 스페이드 본체·약화 순서·서로 다른 최대 2대상, 하트 첫 능동 공격 후 생존 회복, 클로버 드로우·현재 마나·선택 교환을 명세대로 적용했다. 전체 수치와 효과는 UNKNOWN-CardBalanceRedesign-57Cards.md에서 확인할 수 있다.
- UNKNOWN J/Q/K와 조커 1·2 데이터, UNKNOWN Q 공격 버프 +3, 조커 실패 시 파괴를 유지했다. 조커 성공 시 공격력과 최대 HP를 각각 1.5배 올림하고 현재 HP에는 최대 HP 증가분을 더한다. 유닛당 1회 제한을 유지했다.
- 일반 맵은 생존 필드 UNKNOWN 3장 또는 상대 HP 0, 조커 맵은 누적 도박 성공 3회 또는 상대 HP 0 규칙을 유지했다. 기존 맵 설정 에셋 해시를 검증했다. 맵 지급 조커가 현재 경기의 카드 버전을 따르도록 조회만 수정했다.
- 후공 보너스, 덱 고갈 피해, 공격력 최저 1 같은 추가 규칙은 적용하지 않았다. 일반 플레이 포커 시너지를 유지하고 실험의 BattleConfig에서만 껐다.

## 효과 순서와 상태

1. 소환 비용을 먼저 지불하고 소환 효과 배열 순서대로 공통 BattleEngine에서 처리한다. 마나 획득은 현재 마나에 더하고 10에서 제한한다. 턴별 최대 마나는 그대로이며 다음 본인 턴에는 기존 충전 규칙을 사용한다.
2. 하트는 최초 능동 공격을 실행할 때 소비 플래그를 기록한다. 동시 피해와 반격, 사망 제거와 승리 판정 후 생존한 소스에 한 번 회복한다. 무손상 회복도 기회를 소비한다. 방어 반격은 소비하지 않는다. 승리 확정 후 추가 회복을 실행하지 않는다. 사망 조건 실패와 실효 회복 0은 별도 기록한다.
3. 스페이드 K의 피해 4를 먼저 처리한다. 두 번째 약화는 생존 적 전체에서 새로 선택한다. 첫 대상이 생존했으면 다시 선택 가능하다. FreshTarget 지정은 이 독립 효과에만 사용해 기존 연속 대상 규칙을 보존했다. ChainWeaken/ChainDamage는 선택한 ID를 기록해 중복 선택을 막는다.
4. 교환은 소환 후 남은 손패에서 카드 1장을 선택하거나 건너뛴다. 선택 시 버리기와 드로우를 하나의 진행 명령으로 완료한다. 수동 버리기 횟수와 독립적이다. 취소·ESC·턴 종료·시간 만료는 교환만 건너뛰고 후속 효과를 처리한다. 클로버 J는 건너뛰어도 마나 +2가 실행된다.
5. 스냅샷은 카드 버전, 첫 공격 소비 상태, 효과 소스·인덱스, 선택 대상 이력, 다중 대상 ID, 교환 대기를 보존한다. Host 권한 BattleCommands에서 명령을 검증한다. 중복 sequence와 중복 선택을 검사했다. AI는 공개 관측으로 합법 교환·대상 선택을 생성하며 생존 후 회복, 낭비, 손패 상한과 마나 상한을 평가한다.

## 실행한 검사

|검사|결과|근거|
|---|---|---|
|57종 데이터 포함 개편 기능 검사|153 통과, 실패 0|Validation/CardRedesign/CardRedesign-Checks.json|
|실제 UI 클릭과 대상 선택|11 통과, 오류 0|Validation/CardRedesign/CardRedesign-UI.json|
|기존 전투 회귀 검사|869 통과, 실패 0|Validation/CardRedesign/RegressionSummary.json 및 개별 JSON|
|통계 정합성 감사|50,259 조건 통과|BalanceResults의 analysis/AuditChecks.json|
|Windows Development Build|성공, 오류 0, 경고 8|Validation/CardRedesign/SourceAndValidationManifest.json|

기존 회귀 검사의 고정 카드 상황은 Baseline 데이터를 사용해 기존 예상값을 유지한다. 새 데이터의 기능은 별도 153개와 실제 클릭 11개로 검증했다. 튜토리얼과 일반 플레이의 기본 데이터는 Revised다. 통계 감사는 마나 지출·피해 보존·조건 대응 등을 검사하며 기능 검사 개수와 별개다. 빌드 경고 8개는 남아 있다.

검사에는 반격 사망/무손상/방어/두 번째 공격/직접 공격/승리 공격/맵 중첩, 스페이드 K 대상 생존·사망·부재·공격력 0, 다중 대상 0/1/3명, 마나 0/9/10과 다음 턴 충전, 교환 취소·빈 손패·시간 만료·중복 명령, 손패 7/8과 덱 0/1/4, UNKNOWN·조커 규칙, 스냅샷·AI 관측 복원이 포함된다. UI 검사에서는 교환 선택·취소와 K 첫 대상을 처치한 뒤 새 생존 대상을 클릭했다.

실제 Relay 확인 범위와 제한은 Validation/CardRedesign/RelayValidation.json에 기록한다. 서로 다른 PC 두 대를 사용한 검사는 이 환경에서 수행하지 못했다. 기존 구조는 영속 저장·Host 이관 기능을 제공하지 않아 해당 기능은 검사 대상에 포함하지 않았다. 효과 진행 스냅샷 왕복 복원은 검증했다.

## 1,000경기 실제 결과

|코호트|시도|완료|미종료|처리 오류|완료 경기 평균 글로벌 턴|
|---|---|---|---|---|---|
|baseline_main|480|480|0|0|22.52|
|revised_main|480|468|12|0|36.90|
|baseline_stress|20|20|0|0|27.15|
|revised_stress|20|19|1|0|34.84|

총 987경기 완료, 13경기 미종료, 처리 오류 0. 주 비교는 초기 조건 240개에 대해 버전별 선후공 교환 2판씩 실행한 960판이다. 6맵 각각 40조건이며 동일 AI 세 쌍 각각 4조건, 혼합 세 쌍 각각 9조건과 순환 1조건을 배정했다. Random 스트레스는 초기 조건 20개를 양 버전에서 한 번씩 실행했다.

모든 경기 포커 시너지를 껐다. 두 버전의 규칙·맵·AI 해시는 같고 카드 해시는 두 종류다. 초기 덱·손패·AI 정체성과 정책 시드를 대응시켰다. 주 비교 480개 대응 경기 중 전투 RNG 소비 경로가 471개에서 달랐다. 같은 시드가 전투 전체 난수를 일대일 대응시키지는 않는다. 정확한 64비트 지문은 원본 C# JSON/CSV를 사용한다.

완료 경기만 카드·효과·승률 집계에 포함한다. 미종료 경기의 이벤트와 마지막 상태는 코호트와 match ID로 남겨 두었다. 사용 승률은 그 카드를 사용한 승리 플레이어-경기 수 / 해당 카드를 사용한 플레이어-경기 수다. 관측 연관성이므로 개별 카드의 인과 효과를 확정하지 않는다.

### 회복 0과 스페이드 K

|코호트|하트 카드|자연 사용|실효 자기 회복|효과 활성화|반격 사망 조건 실패|
|---|---|---|---|---|---|
|baseline_main|Heart_4|323|0|323|0|
|baseline_main|Heart_7|356|0|356|0|
|baseline_main|Heart_10|338|0|338|0|
|baseline_main|Heart_J|323|0|323|0|
|baseline_main|Heart_K|272|0|272|0|
|revised_main|Heart_4|529|513|248|25|
|revised_main|Heart_7|546|523|326|17|
|revised_main|Heart_10|564|949|396|14|
|revised_main|Heart_J|525|1209|374|14|
|revised_main|Heart_K|521|1478|365|2|

대상 하트 5종의 주 비교 자기 회복은 0에서 총 4,672로 증가했다. 개편판 1,709회 활성화 중 1,637회 생존 조건을 충족했고 72회는 반격 사망이었다. 최대 HP 증가·맵 회복은 별도 지표다. 첫 공격 전 파괴와 무손상 회복은 실효 기여를 낮출 수 있다.

스페이드 K 두 번째 약화는 기준 주 비교에서 정상 처리 51회, 기존 대상 참조 무효 244회, 대상 부재 3회였다. 개편 주 비교는 정상 처리 508회, 참조 무효 0회, 대상 부재 14회였다. 공격력 0인 유효 대상의 정상 처리는 실제 약화량 0일 수 있다. 스트레스의 개편판 참조 무효도 0회였다.

### 선공과 장기전

주 비교 전체 완료 경기 선공 승률은 69.17%에서 57.26%로 변했다. 동일 AI끼리는 98/144=68.06%에서 79/143=55.24%로 변했다. 개편판은 목표 상단 55%를 소폭 초과한다. Wilson 95% 서술 구간은 기준 60.06~75.12%, 개편 47.06~63.15%다. 선후공 교환 관측의 의존성 때문에 독립 표본을 가정한 구간만으로 개선을 확정할 수 없다. 혼합 AI를 포함한 전체 승률은 정책 차이의 영향을 받는다.

전체 평균 턴은 22.52에서 36.90로 63.84% 증가해 ±10% 경보선을 넘었다. 글로벌 턴은 양쪽 턴을 합친 값이다.

|맵|기준 평균 턴|개편 평균 턴|변화|개편 미종료|
|---|---|---|---|---|
|Normal|20.85|36.67|75.90%|0|
|JokerGame|17.95|25.69|43.11%|0|
|Diamond|20.60|35.41|71.89%|2|
|Spade|24.70|40.17|62.63%|9|
|Heart|29.14|44.87|54.01%|1|
|Club|21.89|39.00|78.18%|0|

모든 맵이 ±15% 경보선을 넘었다. 하트 지연 회복, 클로버 순환·마나와 스페이드 공격력 감소가 함께 바뀌어 개별 원인의 인과 기여를 분리할 수 없다. 후속 실험은 하나씩 변경해야 한다.

### UNKNOWN과 조커 대상

관측된 UNKNOWN 첫 소환 글로벌 턴 중앙값은 12에서 13으로 바뀌었다. 1턴 이상 조기화 경보는 발생하지 않았다. UNKNOWN 특수 승리는 2/480=0.42%에서 4/468=0.85%로 약 0.44%p 늘었다. 5%p 경보에는 도달하지 않았고 사건 수가 작다. 소환된 경기·유닛에 조건부인 중앙값과 생존 시간은 전체 경기의 조합 가속 효과를 확정하지 않는다.

|코호트|UNKNOWN|소환 수|평균 관측 생존 글로벌 턴|종료까지 생존한 중도절단 수|
|---|---|---|---|---|
|baseline_main|Unknown_J|213|2.53|42|
|baseline_main|Unknown_Q|215|3.31|67|
|baseline_main|Unknown_K|176|3.43|47|
|revised_main|Unknown_J|384|3.44|48|
|revised_main|Unknown_Q|356|5.29|89|
|revised_main|Unknown_K|321|4.94|62|

개편판 UNKNOWN 관측 생존이 길어졌다. 경기 자체의 장기화와 종료 중도절단을 함께 고려해야 한다. 조커는 analysis/joker_target_statistics.csv에서 대상 카드 186개 코호트·유료/무료 그룹으로 분리했다. 성공·실패·확률 합계·실제 마나·성공 공격력/현재 HP 증가·실패 손실을 보존했다. 기본 성공률과 치명 실패 규칙은 유지했다. 기대값은 공격력·HP를 별도 단위로 제공한다.

### 표본 부족

|코호트|자연 사용 200회 미달 카드|맵별 30회 미달 카드-맵 행|
|---|---|---|
|baseline_main|6/57|35/342|
|revised_main|0/57|2/342|
|baseline_stress|57/57|342/342|
|revised_stress|57/57|342/342|

기준 주 비교의 부족 카드는 Club_A, Diamond_A, Heart_A, Joker_2, Spade_A, Unknown_K다. 개편판 전체 사용량이 200회를 넘더라도 맵별 행과 정책별 비교의 표본을 확인해야 한다. 스트레스는 균형 추정 표본이 작다. 자동 후보는 검토용이며 실제 카드를 자동 조정하지 않는다.

## 반복 교착과 후속 우선순위

미종료 13회는 1회 경보선을 초과했다. 주 비교는 Diamond 2회, Spade 9회, Heart 1회이며 스트레스는 Normal 1회다. 양쪽 덱이 비고 손패가 비거나 사용할 대상 없는 조커만 남은 상태에서 필드가 비거나 모든 공격력이 0이 되어 현재 규칙으로 승리가 진행되지 않는 상태를 확인했다. 상세 마지막 상태는 analysis/AnalysisAudit.json에 있다.

테스터에서만 보수적인 고갈 교착 판별을 추가해 해당 상태를 unresolved로 종료하고 집계에서 제외한다. 조커 맵에 유닛이 있으면 사건으로 다시 강화될 가능성이 있어 이 판별을 적용하지 않는다. 실제 게임의 승패 규칙을 추가하지 않았으므로 동일한 고갈 상태는 플레이에서도 남을 수 있다.

1. 고갈·공격력 0 교착의 실제 게임 종료 규칙을 별도 명세로 결정하고 단일 변경 실험한다. 현재 명세의 빈 덱 규칙 보존과 충돌할 수 있어 이번에는 게임 규칙을 변경하지 않았다.
2. 장기전 원인을 분리하기 위해 스페이드 약화의 지속 방식 또는 단일 카드 수치 한 항목만 바꾸는 비교 실험을 우선 검토한다. 공격력 최저값을 자동 도입하지 않았다.
3. 클로버 순환과 하트 생존 기여를 각각 별도 통제 실험으로 확인한 뒤 조정 후보를 좁힌다. UNKNOWN·조커 버프를 추가 변경하지 않았다.
4. 동일 AI의 더 많은 대응 표본과 실제 사람 대전을 확보한다. 이번 결과만으로 52종 최종 균형을 결론 내리지 않는다.

## 실행과 산출물

Unity 메뉴 Tools > UNKNOWN > Card Redesign Comparison에서 Seed 7312026으로 Start 1000-Match Comparison을 누른다. 실행 중 Play Mode를 시작하지 않는다. Stop Test 또는 창 닫기로 중단하면 partial 결과를 별도 폴더에 저장한다. Open Results Folder로 결과를 연다. 반복 실행마다 새 폴더를 만든다.

- 실제 Windows 빌드 위치는 `D:/git/ECHO-/Echogame/CardGame/Builds/UNKNOWN-CardRedesign/UNKNOWN.exe`다. 전체 폴더를 함께 사용한다.
- 이번 결과의 전달 위치는 `D:/git/ECHO-/Echogame/CardGame/BalanceResults/redesign-20261111-024515-546-fbb6f0`다. 기존 9개 파일을 루트와 코호트별 폴더에 보존했다. 루트의 cohort 열로 구분한다. schemaVersion 2.0이며 새 필드와 데이터 버전이 명시되어 있다.
- 원본 메타데이터의 절대 경로는 검증용 격리 프로젝트의 실행 당시 경로다. 전달 폴더의 상대 코호트 구조는 그대로 유지했다. 복제본과 원본 파일 해시를 비교한다.
- Validation/CardRedesign에는 기능·UI·기존 회귀 결과, 원본 소스, 변경 소스 해시, 빌드 요약과 실제 Relay 검증 범위를 저장한다.

## 실제 수정 파일

33개 C# 파일이다. 개별 SHA-256과 신규/수정 구분은 SourceAndValidationManifest.json에 있다. 아래 목록의 검사 파일 변경은 고정 상황의 버전 지정과 새 효과 검증을 위한 것이다.

|구분|경로|
|---|---|
|수정|`Assets/UNKNOWN/Battle/Runtime/AiAgent.cs`|
|수정|`Assets/UNKNOWN/Battle/Runtime/AiObservation.cs`|
|수정|`Assets/UNKNOWN/Battle/Runtime/BalancePolicy.cs`|
|수정|`Assets/UNKNOWN/Battle/Runtime/BalanceReport.cs`|
|수정|`Assets/UNKNOWN/Battle/Runtime/BalanceTelemetry.cs`|
|수정|`Assets/UNKNOWN/Battle/Runtime/BalanceTestRun.cs`|
|수정|`Assets/UNKNOWN/Battle/Runtime/BalanceVerification.cs`|
|수정|`Assets/UNKNOWN/Battle/Runtime/BattleCommands.cs`|
|수정|`Assets/UNKNOWN/Battle/Runtime/BattleEngine.cs`|
|추가|`Assets/UNKNOWN/Battle/Runtime/BattleEngine.NewEffects.cs`|
|수정|`Assets/UNKNOWN/Battle/Runtime/BattleProtocol.cs`|
|수정|`Assets/UNKNOWN/Battle/Runtime/BattleUIController.AiChecks.cs`|
|수정|`Assets/UNKNOWN/Battle/Runtime/BattleUIController.cs`|
|추가|`Assets/UNKNOWN/Battle/Runtime/BattleUIController.Exchange.cs`|
|수정|`Assets/UNKNOWN/Battle/Runtime/BattleUIController.HandActions.cs`|
|수정|`Assets/UNKNOWN/Battle/Runtime/BattleUIController.HandSettingsChecks.cs`|
|수정|`Assets/UNKNOWN/Battle/Runtime/BattleUIController.Interaction.cs`|
|수정|`Assets/UNKNOWN/Battle/Runtime/BattleUIController.JokerChecks.cs`|
|수정|`Assets/UNKNOWN/Battle/Runtime/BattleUIController.PlayModeChecks.cs`|
|수정|`Assets/UNKNOWN/Battle/Runtime/BattleUIController.PokerChecks.cs`|
|수정|`Assets/UNKNOWN/Battle/Runtime/BattleUIController.PvPChecks.cs`|
|추가|`Assets/UNKNOWN/Battle/Runtime/BattleUIController.RedesignChecks.cs`|
|수정|`Assets/UNKNOWN/Battle/Runtime/BattleUIController.SuitChecks.cs`|
|수정|`Assets/UNKNOWN/Multiplayer/Editor/CardBalanceTesterWindow.cs`|
|수정|`Assets/UNKNOWN/Battle/Runtime/CardData.cs`|
|추가|`Assets/UNKNOWN/Multiplayer/Editor/CardRedesignBatch.cs`|
|추가|`Assets/UNKNOWN/Battle/Runtime/CardRedesignChecks.cs`|
|추가|`Assets/UNKNOWN/Multiplayer/Editor/CardRedesignComparisonWindow.cs`|
|추가|`Assets/UNKNOWN/Battle/Runtime/CardRedesignExperiment.cs`|
|추가|`Assets/UNKNOWN/Multiplayer/Editor/CardRedesignUiBatch.cs`|
|수정|`Assets/UNKNOWN/Battle/Runtime/JokerMapRules.cs`|
|수정|`Assets/UNKNOWN/Multiplayer/Runtime/NetworkBattleSmokeRunner.cs`|
|추가|`Assets/UNKNOWN/Battle/Runtime/RevisedCardCatalog.cs`|
