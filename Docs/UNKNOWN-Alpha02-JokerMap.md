# Alpha 0.2 — 조커의 유희

## 실행 및 설정

`Assets/Scenes/SampleScene.unity`를 열고 Play → 온라인 대전 → 방 만들기를 선택합니다. 대기실의 `맵: 일반 전장`을 누르면 `맵: 조커의 유희`로 전환됩니다. 방장만 변경할 수 있고 참가자에게 같은 이름이 표시됩니다. 맵 변경은 양쪽 준비 상태를 취소하므로 두 사람이 다시 준비한 뒤 방장이 시작합니다. 기존 NetworkManager와 UnityTransport를 그대로 사용하며 추가 오브젝트 연결은 필요하지 않습니다.

양쪽 PC에서 같은 새 빌드를 사용해야 합니다. 전투 메시지 버전을 v4로 변경했습니다. 인증 프로필, 저장된 로그인, Unity Cloud 프로젝트 및 환경 설정은 변경하지 않았습니다.

`Assets/UNKNOWN/Battle/Resources/JokerMapSettings.asset`을 선택하면 Inspector에서 사건 이름, Weight, Amount를 수정할 수 있습니다. 사건 종류 14개를 유지하고 Weight 합계를 100으로 설정해야 합니다. Amount는 강화·저주·추가 드로우·폐기·임시 마나·성공 확률 보정량에 사용되며, 반토막과 증식에서는 퍼센트입니다. 교환 1장, 선물 JOKER 1장, 마지막 패 2장 폐기 후 JOKER 1장, 강제 도박 1회는 사건의 고정 구조입니다. 잘못된 설정은 명시적인 오류를 발생시킵니다. 일반 전장은 이 에셋에 의존하지 않습니다.

## 규칙

| 사건 | Weight | 기본 효과 |
|---|---:|---|
| 운명의 강화 | 13 | 무작위 아군 ATK 또는 최대 HP +3, 최대 HP 증가분만큼 현재 HP 증가 |
| 불운의 저주 | 10 | 무작위 아군 ATK 또는 최대 HP -2, ATK 최소 0 / 최대 HP 최소 1 |
| 비밀의 공개 | 10 | 현재 턴 동안 상대 손패 1장 앞면 및 툴팁 공개 |
| 행운의 마나 | 10 | 현재 마나 +2, 최대 10, 최대 마나 유지 |
| 운명의 드로우 | 10 | 실제 덱에서 추가 2장, 기존 손패 10장 제한 적용 |
| 혼돈의 교환 | 8 | 손패 인스턴스 그대로 덱으로 반환·셔플·1장 드로우 |
| 강제 폐기 | 6 | 무작위 손패 최대 2장 폐기 |
| 운명의 반토막 | 3 | 현재 HP의 절반을 올림, 최소 1 |
| 조커의 선물 | 8 | 기본 덱과 별도로 새 JOKER 인스턴스 1장 생성 |
| 운명의 재도전 | 5 | 이번 턴 다음 도박 1회의 성공률 70% |
| 조커의 저주 | 5 | 이번 턴 다음 도박 1회의 성공률 30% |
| 강제 도박 | 4 | 실제 손패 JOKER를 미사용 아군 유닛에 자동 사용, 마나 무료 |
| 마지막 패 | 4 | 무작위 손패 2장 폐기 후 새 JOKER 1장 생성 |
| 혼돈의 증식 | 4 | 무작위 아군 ATK·최대 HP ±50%, 증가 올림 / 감소 내림 |

유효한 사건만 남겨 가중 추첨합니다. 이는 대상 없는 사건을 반복 재추첨하는 것과 같은 조건부 확률이며 무한 반복을 피합니다. 유효 사건이 없으면 효과 없음으로 진행합니다. 덱이 비어 추가 드로우가 불가능하거나 손패가 가득 차 선물을 받을 수 없는 경우도 제외합니다. 교환은 UNKNOWN/JOKER와 기존 강화 상태까지 인스턴스 그대로 보존합니다. 증식은 JOKER 사용 여부나 성공 횟수를 변경하지 않습니다.

턴 시작 순서는 플레이어 확정 → 마나 증가·회복 → 일반 드로우 또는 빈 손패 최대 5장 보충 → 사건 1회 판정 → 연출입니다. 양쪽 연출 완료 후 Host가 40초 마감 시각을 발행합니다. 사건 알림은 1.2초이며 기존 능력치 변화·폐기·드로우·도박 연출을 사용합니다.

일반 전장의 HP 0 / UNKNOWN 3장 생성 승리는 유지합니다. 조커 맵에서는 UNKNOWN 생성·소환은 가능하지만 생성 3장 승리만 비활성화합니다. 기존 최대 3장 생성 제한은 유지합니다. 대신 실제 JOKER 도박 성공을 게임 전체에 누적하여 세 번째 성공 즉시 승리합니다. 강제 도박도 포함하고 실패·유닛 파괴로 감소하지 않습니다. HP 0 승리는 두 맵 모두 유지합니다.

## 온라인 권한 및 비공개 정보

Host만 맵 선택, 사건 추첨, 카드 생성, 도박 및 승리를 판정합니다. 사건 ID는 경기 내 턴 번호이며 같은 턴 중복 적용과 중복 배너를 차단합니다. Client는 투영된 상태와 기존 이벤트를 재생하며 카드나 사건을 직접 생성하지 않습니다.

상대 손패는 개수만 전달하며 정의·인스턴스 ID는 전달하지 않습니다. 선물·마지막 패·교환·폐기·공개·성공률 보정 사건은 상대에게 종류와 상세 설명을 숨긴 일반 알림을 전달합니다. 상대에게 보내는 CardGranted 이벤트도 일반 Draw로 바꾸므로 JOKER 획득 여부가 노출되지 않습니다. JOKER 성공 누적 횟수·보정 확률과 UNKNOWN 누적 횟수는 본인만 받습니다. 공개 사건은 지정된 카드 하나만 별도 필드로 전송하고 카드 제거 또는 턴 변경 시 종료합니다. 필드에서 발생한 강화·파괴 결과와 경기 종료 사유는 공개합니다.

## 개발 전용 검사

로컬 테스트 실행 후 `UNKNOWN > Joker Map > Local Debug`를 사용합니다. 맵 전환, 유효한 사건 강제 적용, JOKER 지급, 다음 도박 성공/실패 지정, 확률 판정 복원, 현재 맵 승리 조건 검사가 있습니다. 대상 없는 강제 사건은 적용되지 않습니다. 온라인 전투에서는 비활성화되며 이 디버그 코드는 Editor에서만 컴파일됩니다.

`UNKNOWN > Joker Map > Run All Map Checks`는 Play Mode 자동 검사 전체를 실행합니다. 순수 규칙 검사는 `Tests/UNKNOWN/Run-BattleTests.cmd`입니다. 결과 JSON과 화면은 `Docs/Validation/JokerMap-Alpha02/`에 보관합니다.

## 주요 수정 파일

- `BattleEngine.cs`, `JokerMapRules.cs`, `BattleEvents.cs`: 맵 규칙, 14종 사건, 공통 도박 처리 및 맵별 승리 조건.
- `BattleProtocol.cs`, `MapEventView.cs`: 수신자별 손패·공개 카드·진행도·사건 정보 투영.
- `NetworkBattleController.cs`, `RoomTestUI.cs`: 방장 맵 선택, 준비 초기화, v4 동기화 및 턴 연출 대기.
- `JokerMapSettings.cs`, Resources 에셋: Inspector에서 수정하는 사건 설정.
- `BattleUIController.Map.cs`, 기존 UI/Hands/Online 부분 및 `CardAnimationController.cs`: 배경, 본인 JACKPOT, 사건 배너, 공개 카드와 추가 드로우.
- `BattleUIController.MapDebug.cs`, `JokerMapDebugWindow.cs`: 로컬 Editor 디버그.
- `JokerMapTests.cs`, `BattleUIController.JokerChecks.cs`, `AttackCheckMenu.cs`, `NetworkBattleSmokeRunner.cs`: 규칙·실제 UI·실제 두 EXE Relay 검사.

## 검증 결과

Unity 6000.0.74f1의 별도 검증 프로젝트에서 실행했습니다. 원본 프로젝트를 사용 중인 에디터는 종료하지 않았습니다. 원본 씬·프로젝트 설정·패키지는 그대로 유지했습니다.

| 검사 | 실제 결과 |
|---|---|
| 순수 C# 규칙 | 99 통과, 0 실패 |
| Play Mode 공격 UI | 43 통과, 0 실패, 0 오류 |
| Play Mode 드래그·조합·손패 UI | 60 통과, 0 실패, 0 오류 |
| Play Mode 기존 PvP 회귀 | 22 통과, 0 실패, 0 오류 |
| Play Mode 조커 맵 | 68 통과, 0 실패, 0 오류 |
| Windows Development Build | 성공, 오류 0, 경고 8 |
| 실제 Relay 일반전 | 17턴, 상대 HP 0으로 Host 승리, 맵 사건 0회 |
| 실제 Relay 조커 맵 | 28턴, Client JOKER 성공 3회 승리, 사건 28회, 추가 JOKER 총 9장 |

Play Mode에서는 실제 설정 에셋을 읽고 14종 사건을 각각 적용하여 배너 1회 표시, 연출 중 40초 유지 및 입력 잠금, 드로우 완료 후 손패 뷰 일치를 확인했습니다. 공개 카드 앞면·전체 툴팁·제거 즉시 숨김, 본인 전용 JACKPOT, 온라인 디버그 차단, 양쪽 수신자 승리/패배 UI, 일반전 복원도 검사했습니다. [검사 JSON](Validation/JokerMap-Alpha02/UNKNOWN-JokerMap-Checks.json)과 [실제 Unity 렌더 화면](Validation/JokerMap-Alpha02/Attack-JokerMap-1920.png)을 보관했습니다.

온라인은 같은 PC의 EXE 4개로 독립된 두 방을 동시에 검사했습니다. 익명 로그인은 default / unknown_local_2 / unknown_local_3 / unknown_local_4의 서로 다른 Player ID였으며 Project ID와 production 환경은 일치했습니다. 테스트는 재현 가능한 일반 RNG seed(일반전 15 / 조커 맵 4)를 사용했고 사건 종류나 도박 결과를 강제로 지정하지 않았습니다. 새 JOKER는 기존 손패 드래그 도박 경로를 사용했습니다.

두 경기 모두 Host/Client의 최종 공개 상태, 모든 공격 결과, 공개 능력치 변경 결과가 일치했습니다. 조커 맵 사건 ID는 양쪽 모두 1~28로 중복 없이 일치했습니다. 상대 손패·UNKNOWN/JOKER 누적 진행도와 확률 보정 비공개, Client 맵 변경 차단, 맵 일치, 실제 카드 UI와 전송 상태 일치, 승패 UI 일치, 대기실 복귀, 방 나가기까지 모두 통과했고 런타임 오류는 0개였습니다. [Relay 결과 요약](Validation/JokerMap-Alpha02/RelaySummary.json), 각 EXE 원본 결과 JSON 및 인증 토큰 없는 연결 증거 로그를 함께 보관했습니다. 테스트용 EXE 네 개는 방을 나간 뒤 종료했습니다.

빌드는 `Builds/UNKNOWN/UNKNOWN.exe`입니다. 검증용 빌드와 배포 폴더의 Assembly-CSharp.dll SHA-256은 `E71BB0EFD9868D87D0997D1336DB142B2B8B0C78E70D26AD4B7FC499E73AEC78`로 일치합니다. Unity 빌드 경고에는 URP Light2D 셰이더의 벡터 잘림과 디버그 셰이더 변형 제거 안내 등이 포함됩니다. C# 컴파일 오류는 없습니다.

검증 범위의 한계: 서로 다른 실제 PC·외부 네트워크 사이의 접속, 인위적인 패킷 손실·고지연, 실제 포인터로 대기실 메뉴를 조작하는 수동 검사는 이번 환경에서 수행하지 않았습니다. 온라인 검사는 실제 Authentication/Session/Relay/Netcode와 UI 드래그 경로를 사용하는 자동 EXE 대전이며, 화면 검사는 Unity Camera.Render 결과입니다. Windows 두 PC에 동일한 Builds/UNKNOWN 폴더를 복사하여 마지막 수동 접속 검사를 진행할 수 있습니다.
