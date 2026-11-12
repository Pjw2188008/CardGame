# UNKNOWN 카드 밸런스 자동 대전

## 실행

Unity 메뉴의 `Tools > UNKNOWN > Card Balance Tester`에서 실행한다. 기본값은 1,000판, 시드 7312026, AI 4종 혼합, 6개 맵 순환이다. `Start Test`, `Stop Test`, `Open Results Folder`를 제공한다. Play 모드와 화면 렌더링 없이 기존 BattleEngine을 호출한다. 설정을 바꾸려면 진행 중인 테스트를 먼저 중단한다.

진행 중에는 완료 경기 수와 현재 턴을 표시한다. 결과 목록은 이름·ID와 무늬로 필터링하고 이름·무늬·마나·사용 횟수·사용 승률·피해·생존으로 정렬한다. 전체·맵별·AI별 행을 전환할 수 있다. 종료 후 검토 후보를 표시한다. 창을 닫거나 스크립트가 다시 로드되면 중단 결과를 저장한다.

결과는 프로젝트의 `BalanceResults/<UTC 날짜-시간-고유값>/`에 저장한다. 이전 실행 결과는 덮어쓰지 않는다. 상세 로그를 켜면 `effect_events.jsonl`도 생성한다.

## 재사용한 규칙

- BattleEngine, BattleCommands, CardDatabase와 기존 맵·조커 설정을 사용한다.
- 기존 EnablePoker 설정을 이 테스트의 BattleConfig에서만 false로 둔다. 일반 전투의 기본값은 true다. 사망 제거는 기존 RemoveDeadRaw 경로를 사용한다.
- 기존 AiObservation과 AiAgent.Generate의 합법 행동 생성을 재사용한다. 테스트 정책은 동일한 카드를 빈 슬롯에 배치하는 선택과 손패 7장 미만에서의 합법적인 수동 버리기도 포함한다.
- AI는 상대 비공개 손패나 덱 순서를 읽지 않는다. 무작위형은 합법 행동 중 균등 추첨하고, 공격형은 피해·제거, 운영형은 생존·회복·자원, 효과 활용형은 대상 상태에 맞는 고유 효과를 우선한다. 족보 점수는 사용하지 않는다.
- 두 AI의 순서를 홀수 경기마다 바꾸어 선후공을 맞춘다. 맵은 Normal, JokerGame, Diamond, Spade, Heart, Club 순서로 반복한다. 조커 사건은 기존 확률과 효과를 그대로 호출한다.
- UNKNOWN 획득은 기존 A와 J/Q/K 조합을 거친다. 일반 맵의 필드 UNKNOWN 3장 승리와 조커 맵의 도박 3회 성공 승리를 유지한다.
- 카드 수치·덱·드로우 확률을 조정하는 기능은 제공하지 않는다.

## 저장 파일

| 파일 | 내용 |
|---|---|
| card_statistics.csv | 57종 카드 정의, 전체 사용·공격·방어·효과·효율 통계 |
| card_effect_statistics.csv | 효과별 조건·적용·성공·실패·무효·실제 효과량·후속 기여 |
| card_map_statistics.csv | 6개 맵별 카드 통계 |
| card_ai_statistics.csv | 4개 AI 유형별 카드 통계 |
| match_results.csv | 경기 시드·맵·AI·승패·턴·행동·사용 카드·마지막 상태 |
| balance_summary.json | 전체·맵·AI 통계, 표본 상태와 해석 기준 |
| balance_candidates.json | 성능 과다·부족·효과 발동·맵 의존성 검토 후보 |
| test_metadata.json | 설정·카드 정의·맵·사건 규칙·버전 해시·시드·경기 기록 |
| effect_events.jsonl | 상세 로그를 선택한 경우의 실제 이벤트와 효과 적용 기록 |

CSV는 UTF-8 BOM으로 저장한다. JSON은 UTF-8이다. 기본 능력치와 문구는 CardDatabase에서 그대로 내보낸다. 카드 데이터·규칙 데이터·엔진 소스·측정 소스의 SHA-256 해시를 남긴다.

## 통계 해석

- 등장 횟수는 손패에 실제 입장한 드로우·생성·UNKNOWN 조합을 센다. 재드로우도 새 등장이다. 초과 드로우로 즉시 폐기된 카드는 손패 등장에 넣지 않는다.
- 사용은 소환과 조커 사용이다. UNKNOWN 조합 재료 소비는 별도 열이다. 사용 가능 횟수는 합법적인 소환·조커 행동이 있었던 의사결정 지점 수다.
- matchesUsed는 중복을 제거한 경기 수다. playerMatchesUsed는 해당 카드를 사용한 플레이어-경기 수다. 사용 승률은 승리한 플레이어의 사용 경기 수를 playerMatchesUsed로 나눈 값이다. 양쪽이 같은 카드를 사용한 경기도 이 방식으로 처리한다.
- 피해는 남은 체력을 넘는 과잉 피해를 제외한다. 일반 공격·반격·고유 효과·자해·대상별 피해를 구분한다. 회복은 실제 복구된 체력만 센다.
- 생존 턴은 양쪽 턴을 합한 글로벌 턴 단위다. 게임 종료까지 생존한 유닛은 우측 중도절단 표본으로 별도 표시하며 종료 시점까지의 생존을 포함한다.
- 고유 효과 activation은 카드 인스턴스와 효과 인덱스별로 한 번 센다. 조건 충족도 같은 단위로 센다. 다중 대상 적용과 성공·실패·0 효과는 대상별로 센다. 대상 부재·파괴된 소스·무효 대상은 로그에서 구분한다.
- 공격력 증가에 따른 후속 추가 피해는 해당 증가가 없었을 때의 피해와 비교하고, 여러 증가분에 비례 배분하는 귀속 추정값이다. 공격력 감소에 의한 예방 피해도 같은 방식이다. 별개 카드들의 직접 피해에 이 값을 다시 더하면 중복 집계가 된다.
- 추가 체력 흡수는 기본 체력을 먼저 소비하는 풀을 가정한 귀속 추정값이다. 소모된 추가 체력 풀을 회복으로 다시 채우지 않는다. 실제 엔진은 체력을 합산하므로 개별 버프의 독립적인 인과 효과를 확정할 수 없다.
- 맵의 공격력·체력 변화, 추가 피해, 추가 체력 흡수, 소환 추가 드로우와 조커 지급을 고유 효과와 구분한다.
- 조커는 성공·실패 횟수, 실제 확률, 성공 시 공격력·체력 증가, 실패 시 잃은 공격력·체력을 각각 기록한다. 서로 다른 단위를 단일 점수로 합산하지 않는다. 맵의 무료 도박도 기록하며 실제 지출 마나는 0이다.
- 마나 효율은 실제 지출 마나를 분모로 한다. 지출이 0이면 CSV를 비우고 JSON의 상태를 not_applicable_zero_paid_mana로 둔다. 이 경우 JSON 숫자 0은 표시용 값이다.
- 현재 카드에 없는 보호막·직접 피해 감소 기능은 not_applicable이다. 공격력 약화에 의한 예방 피해는 별도 열에 있다.

## 후보 분류

전체 자연 사용 200회 이상, 사용 경기 10회 이상인 카드만 후보로 분류한다. 같은 무늬·카드 유형·효과 역할과 마나 ±1 구간의 충분히 사용된 카드 2종 이상을 비교한다. 피해·회복·드로우·약화·버프 피해·추가 체력·흡수 체력을 각 단위로 비교한다. 해당 효과를 가진 카드끼리 비교하며, 지원 전용 UNKNOWN Q를 피해량 부족으로 분류하지 않는다. 평균의 1.75배 이상 또는 0.5배 이하이며 AI 3종 이상에서 방향이 일치할 때 과다·부족 후보를 표시한다.

발동 20회 이상인 효과에서 조건 충족이 드물거나 0 효과 적용이 80% 이상이면 발동 검토 후보로 표시한다. 맵별 사용 30회 이상인 맵이 3개 이상일 때 다른 맵 평균 대비 2배 이상 또는 0.35배 이하인 성능을 맵 의존성 후보로 표시한다.

이 기준은 검토용 휴리스틱이다. 사용 승률은 관찰 상관관계다. 카드 역할·사용 순서·게임 길이·AI 선택 편향을 함께 검토해야 한다. 카드 문구의 의미와 코드가 완전히 일치하는지는 자동 확정하지 않는다. 실제 문구와 효과 벡터를 함께 내보내고 카드별 결정적 검사로 수치 경로를 확인한다.

## 안전 상한과 재현

기본 안전 상한은 글로벌 1,000턴과 경기당 20,000행동이다. 상한 경기, 오류 경기와 취소 경기는 정상 승패 및 카드 집계에서 제외한다. 빈 덱에 피로 피해를 추가하거나 무승부를 승리로 환산하는 규칙은 도입하지 않았다. 중단 경기의 마지막 HP·손패·덱·필드 상태는 match_results.csv에 남긴다.

같은 Unity 버전·카드 데이터·맵 설정·측정 코드와 시드로 재실행한다. 정책은 시간 제한 탐색을 사용하지 않으므로 컴퓨터 속도가 선택 결과에 영향을 주지 않는다. 경기별 시드와 매칭 계산식은 메타데이터에 저장한다.

검증용 배치 메서드는 `Unknown.Multiplayer.Editor.CardBalanceBatch.RunBatch`다. 55종 일반·UNKNOWN 카드의 효과량 검사와 세부 통계 검사 후, 같은 시드로 1,000판씩 두 번 실행하여 CSV·요약·후보 결과를 비교한다. 검증용으로 만든 고정 상태는 이 검사에만 사용하며 실제 1,000판의 덱에는 적용하지 않는다.

## 구현 파일

- 수정한 BattleEngine.cs에 선택적인 이벤트·효과 관찰 지점을 추가했다. 측정기를 연결한 시뮬레이터만 이 콜백을 사용한다.
- BalanceTelemetry.cs에서 실제 결과, 버프 출처와 카드별 통계를 기록한다.
- BalancePolicy.cs에서 기존 합법 행동 생성을 이용하는 4종 정책을 제공한다.
- BalanceTestRun.cs에서 순환 실행·초기화·중단·오류 격리와 집계를 처리한다.
- BalanceReport.cs에서 CSV·JSON과 후보를 생성한다.
- BalanceVerification.cs와 CardBalanceBatch.cs에서 고정 검사·1,000판 재현을 검증한다.
- CardBalanceTesterWindow.cs에서 에디터 실행과 필터·정렬을 제공한다.

측정 및 테스트 구현은 UNITY_EDITOR 또는 Editor 폴더에 한정한다. 게임용 카드 데이터·씬·패키지·네트워크 설정은 이번 변경 대상에서 제외했다. 실제 Relay 두 PC 접속 검증은 별도다.


## 카드 개편 대응 비교

현재 일반 플레이는 실험용 DiamondHeartRework 카드 데이터를 사용한다. Revised, HeartNerf와 기존 Baseline을 별도 보존했다. 전후 비교는 Tools > UNKNOWN > Card Redesign Comparison 창에서 Start 1000-Match Comparison으로 실행한다. 시드 기본값은 7312026이다. 주 비교 960판과 Random 스트레스 40판을 실행하고 각 코호트 및 통합 9개 파일을 새 폴더에 저장한다.

이번 실제 결과는 UNKNOWN-CardBalanceRedesign.md와 UNKNOWN-CardBalanceRedesign-57Cards.md를 참고한다. schemaVersion 2.0에서 카드 버전, 효과 요청량·실제량·전후 상태, 현재 마나 획득·낭비, 교환, 공격 후 회복, 유료·무료 조커, 대응 초기 덱과 AI RNG 기록을 추가했다. 미종료 경기는 완료 통계에서 제외하며 원본 이벤트를 남긴다. 자연 사용 200회와 맵별 30회 미달은 표본 부족이다.


## Git에 올리는 상세 로그

`effect_events.jsonl` 원본은 로컬에 보관하고 `.gitignore`로 제외한다. 현재 결과의 상세 이벤트는 같은 위치의 `effect_events.jsonl.gz`로 압축해 올린다. CSV와 요약 JSON은 그대로 사용한다. 압축 전후 SHA-256과 용량은 `Docs/Validation/CardRedesign/CompressedLogManifest.json`에 있다.

새로 받은 저장소에서 감사 도구는 원본 로그가 없으면 gzip을 직접 읽는다. 원본 텍스트가 필요하면 `effect_events.jsonl.gz`를 압축 해제한다. 새 자동 대전 실행으로 만든 원본도 Git 제외 규칙이 적용된다. 상세 로그를 공유하려면 gzip 압축본을 추가한다.

## 하트 생존 성능 하향 실험

하트 하향 실험 버전은 HeartNerf다. Revised는 하향 전 비교용으로 보존되어 있다. ♥9 광역 회복 2, ♥Q 최대·현재 HP +2 및 광역 회복 2, ♥K 광역 회복 3 및 최초 능동 공격 후 생존 자기 회복 3을 적용했다.

`Tools > UNKNOWN > Run Heart Nerf Comparison`을 실행하면 기존 AI를 사용해 Revised 1,000판과 HeartNerf 1,000판을 대응 비교한다. 모두 포커 시너지를 끈다. 실행 결과는 `heart_nerf_test/<UTC 실행 시각>/` 아래 저장한다. 실제 이번 실행의 통계와 해석은 프로젝트 루트의 `heart_nerf_test/HeartNerfReport.md`에서 확인한다.

상세 이벤트 gzip은 원본과 SHA-256이 일치하며 업로드할 수 있다. 새로운 원본 `effect_events.jsonl`은 자동으로 Git에서 제외한다. 기존 Windows EXE에 새 수치를 반영하려면 별도 빌드가 필요하다.

## 다이아 돌파 성능 강화 실험

이전 DiamondBuff 실험은 하향 전 Revised에서 ♦4 피해 4, ♦7 서로 다른 적 최대 2명에게 각각 피해 3, ♦10 피해 6만 적용한다. HeartNerf는 독립적으로 보존한다.

`Tools > UNKNOWN > Run Diamond Buff Comparison`을 누르면 Revised 1,000판과 DiamondBuff 1,000판을 같은 일정으로 비교한다. 결과와 해석은 `diamond_buff_test/DiamondBuffReport.md` 및 날짜별 실행 폴더의 comparison CSV/JSON에 있다. 상세 이벤트는 검증된 gzip으로 보관하며 원본 JSONL은 Git에서 제외한다. 새 수치를 Windows EXE에 넣으려면 별도 빌드가 필요하다.


## 다이아·하트 26종 재설계

현재 기본 소스는 DiamondHeartRework다. 이전 실제 버전 DiamondBuff와 비교 기준 Revised를 보존했다. `Tools > UNKNOWN > Run Diamond Heart Rework Comparison`은 Revised, DiamondReworkOnly, HeartReworkOnly, DiamondHeartRework를 각각 1,000판씩 실행한다. 네 버전의 동일 초기 조건으로 다이아와 하트 변화의 기여를 분리한다. Random 스트레스도 AB/BA를 교환한다.

실제 이번 실행의 수치와 채택 권장 여부는 `diamond_heart_rework_test/balance_report.md`에 있다. 날짜별 폴더에 필수 CSV/JSON과 추가 상호작용·원인 분리 자료를 저장한다. JSONL 원본은 Git에서 제외하고 gzip을 공유한다. 기존 EXE에 새 수치를 넣으려면 별도 빌드가 필요하다.


## 52종 기본 능력치 및 하트 맵 분리 실험 (2026-11-12)

실제 게임 기본값은 `DiamondHeartRework`로 유지한다. 신규 `FullStatsOnly`(7), `FullDiamondOnly`(8), `FullHeartMapOnly`(9), `FullBalanceRework`(10)는 실험용이며 기본 플레이에 자동 적용되지 않는다.

`Tools > UNKNOWN > Run Full Balance Rework Comparison`으로 기존 기준을 포함한 다섯 버전 각 1,000판, 총 5,000판을 실행한다. Stop 메뉴는 진행 결과를 stopped로 보존한다. 결과는 `full_balance_rework_test/<실행시각>/`, 보고서는 `full_balance_rework_test/balance_report.md`이다. 포커는 끄고 네 AI/여섯 맵/동일 초기조건과 AB/BA를 사용한다. 원본 JSONL은 Git에서 제외하고 gzip만 공유한다.

신규 하트 맵은 해당 두 실험 버전에서만 모든 소환 유닛 HP +1 및 첫 양의 피해 후 생존 시 HP 1 일회 회복이다. 기존 실제 하트 맵은 하트 일반 유닛 HP +2 및 플레이어 턴 HP +2였으며 나머지 버전에서 보존한다. UNKNOWN 필드 3장 승리 규칙은 이번에 변경하지 않았다. 신규 수치를 실제 게임에 채택하려면 결과 검토 후 별도로 기본 버전을 전환해야 한다.
