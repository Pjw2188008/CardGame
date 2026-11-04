# Alpha 0.2 랜덤 맵

## 실행 흐름

기존 SampleScene에서 방을 만들고 참가한 뒤 양쪽이 준비합니다. 방장이 게임 시작을 누르면 Host가 일반 전장 또는 조커의 유희를 각각 50%로 한 번 추첨합니다. 대기실에는 `맵: RANDOM`과 `경기 시작 시 전장이 무작위로 결정됩니다.`만 표시되며 수동 선택 버튼과 SelectMap API는 제거했습니다.

첫 전투 스냅샷의 맵을 양쪽이 그대로 사용합니다. 중앙 RANDOM MAP 화면은 0.8초 동안 이름을 빠르게 교대로 표시하고 이후 최종 이름·승리 조건을 표시하여 총 약 2초 동안 재생합니다. 이 이름 교대는 고정된 시각 연출이며 난수를 호출하지 않습니다. 전투 중에는 현재 맵 이름이 계속 표시됩니다.

양쪽 맵 공개와 최초 카드·턴 사건 연출이 완료될 때까지 Host의 clockRunning=false, busy=true를 유지합니다. 이 동안 새 소환·공격·턴 종료를 승인하지 않고 40초도 감소하지 않습니다. 연출 완료 확인을 양쪽에서 받은 다음 Host 서버 시각으로 40초를 시작합니다. 이후 턴에도 기존 사건 연출 대기 절차를 유지합니다.

## 재경기 및 기존 규칙

결과 화면에서 대기실로 돌아가면 SelectedMap을 null로 만들고 전투 상태·준비 상태·마감 시각·연출 대기 상태를 초기화합니다. 다시 준비하고 시작하면 같은 Host 난수 스트림에서 새 값을 한 번 뽑습니다. 이전 맵을 재사용하거나 난수 생성기를 같은 seed로 매번 재생성하지 않습니다. 같은 맵이 연속으로 선택되는 것은 정상적인 무작위 결과입니다.

전투 UI는 재경기 때 새로 생성되어 공개 연출도 경기당 한 번 재생합니다. 새 BattleAuthority/BattleEngine이 생성되므로 이전 UNKNOWN/JOKER 진행도, 사건, 공개 카드, 일시 성공률 보정은 다음 경기로 넘어가지 않습니다. Client에는 맵 추첨 코드가 실행되지 않습니다.

일반 전장에서는 HP 0 / UNKNOWN 3장 생성 승리와 기존 배경·본인 UNKNOWN 진행도를 사용하며 조커 사건은 발생하지 않습니다. 조커의 유희에서는 기존 14종 사건의 확률·효과, 새 JOKER, 본인 JACKPOT, HP 0 / JOKER 성공 3회 승리를 그대로 사용합니다. UNKNOWN 생성·소환은 유지하지만 조커 맵에서 UNKNOWN 3장 승리는 발생하지 않습니다. 상대 손패와 두 승리 진행도는 계속 비공개입니다.

## 수정 파일

- `Battle/Runtime/RandomMapSelection.cs`: 경기당 한 번 추첨, 50:50 판정, 재경기 초기화 및 누적 추첨 횟수.
- `Multiplayer/Runtime/NetworkBattleController.cs`: Host만 추첨, 대기실 맵 미결정 상태, 기존 스냅샷으로 맵 전달, 수동 API 제거, 재경기 초기화.
- `Multiplayer/Runtime/RoomTestUI.cs`: 맵 선택 버튼 제거 및 RANDOM 안내.
- `Battle/Runtime/BattleUIController.Map.cs`, `.Online.cs`: 2초 공개 화면, 경기당 한 번 재생, 일반전 맵 이름 표시, 기존 양쪽 연출 대기와 연결.
- `Tests/UNKNOWN/JokerMapTests.cs` 및 실행 스크립트: 추첨 횟수·분포·재경기 규칙 검사.
- `Battle/Runtime/BattleUIController.JokerChecks.cs`: 실제 맵 공개 화면·시간·입력 잠금·중복 재생 차단 검사.
- `Battle/Runtime/BattleUIController.AttackChecks.cs`: 오프스크린 캡처 시 동적 글꼴 아틀라스를 먼저 준비하여 실제 화면의 문구가 빠지지 않도록 검사 화면 생성을 보강.
- `Multiplayer/Runtime/NetworkBattleSmokeRunner.cs`: 수동 선택 없는 실제 EXE 대전, 같은 방 재경기와 추첨 횟수·양쪽 맵·입력 잠금 기록.

패키지, Cloud 프로젝트, Authentication, Relay 및 기존 NetworkManager 설정은 변경하지 않았습니다. 별도 컴포넌트 연결은 필요하지 않습니다. 메시지 버전을 v5로 변경했으므로 두 PC 모두 같은 새 빌드를 사용합니다.

## 검증

순수 C# 규칙 102개와 Unity Play Mode 207개(공격 43, 드래그 60, PvP 회귀 22, 조커·랜덤 맵 82)가 모두 통과했습니다. 실패와 런타임 오류는 0개입니다. 10,000회 추첨 결과는 일반 전장 4,975회 / 조커 맵 5,025회였으며 정확히 50:50이어야 한다는 가정은 하지 않았습니다. 별도 검사로 같은 경기의 BeginMatch를 반복 호출해도 난수가 한 번만 호출되고, ResetMatch 후에는 새 값을 호출하는 것을 확인했습니다.

실제 Unity 렌더로 두 맵의 중앙 제목과 승리 조건을 확인했습니다. 연출 시간은 실제 경과 시간 기준 약 2초이며, 입력 잠금·중복 재생 차단·본인 진행도·일반전 복원·기존 14종 사건 검사도 통과했습니다. 검사 JSON, Rules.log 및 화면 PNG는 `Docs/Validation/RandomMaps-Alpha02/`에 보관합니다.

Windows Development Build는 성공했고 오류 0개, 경고 8개입니다. 빌드 경고에는 기존 URP 셰이더 관련 경고·안내 등이 포함됩니다. 실행 파일은 `Builds/UNKNOWN/UNKNOWN.exe`이며 검증 프로젝트와 배포 폴더의 Assembly-CSharp.dll SHA-256은 `E4DA0BD56594694D38F97560A47B4CF209D05B4AD261D344720D1719715CFE0B`로 일치합니다.

실제 Windows EXE 두 개를 Authentication/Session/Relay/Netcode로 연결하여 **같은 방에서 두 경기를 연속 완료**했습니다. 재현 가능한 개발 검사 seed 2를 사용했으며 맵 종류·사건·도박 결과를 강제로 지정하지 않았습니다.

| 검사 | 1차전 | 같은 방 재경기 |
|---|---|---|
| 양쪽 맵 | JokerGame | Normal |
| 종료 턴 | 16 | 56 |
| 승리 | Client, 상대 HP 0 | Client, 상대 HP 0 |
| Host 누적 맵 추첨 | 1 | 2 |
| Client 맵 추첨 | 0 | 0 |
| 맵 공개 연출 | 각 1회 | 각 1회 |
| 조커 사건 | 16회, 중복 없음 | 0회 |
| 추가 JOKER | 총 4장 | 0장 |
| 런타임 오류 | 0 | 0 |

두 경기 모두 맵 결과, 최종 공개 상태, 전체 공격 결과, 공개 능력치 변경 결과, 사건 ID가 일치했습니다. 대기실 맵 미공개, 수동 변경 API 제거, 초기화 상태, 공개 연출 중 입력 잠금·40초 유지, 상대 손패 및 승리 진행도 비공개, 실제 카드 UI·승패 UI 일치, 대기실 복귀를 확인했습니다. 첫 경기는 같은 세션을 유지하여 재경기했고, 두 번째 경기 후 양쪽 방 나가기를 확인한 다음 테스트 EXE를 종료했습니다. 따라서 첫 경기 결과 JSON의 cleaned=false는 재경기를 위해 방을 유지한 기록입니다.

두 EXE는 서로 다른 익명 Player ID(default / unknown_local_2)를 사용했고 같은 Project ID와 production 환경에 연결됐습니다. [온라인 결과 요약](Validation/RandomMaps-Alpha02/RelaySummary.json), 경기별 Host/Client JSON, 인증 토큰 없는 연결·맵 추첨 로그를 보관했습니다. 일반전 UNKNOWN 3장 승리와 조커 맵 JOKER 성공 3회 승리는 규칙 및 양쪽 수신자 Play Mode 검사로 검증했습니다. 이번 실제 두 경기는 모두 HP 0으로 종료했습니다.

검증 범위의 한계: 서로 다른 물리 PC·외부 네트워크, 인위적인 고지연·패킷 손실, 실제 마우스로 대기실 메뉴를 조작하는 수동 검사는 수행하지 않았습니다. 온라인은 실제 EXE의 자동 드래그·전투 경로로 검사했고 화면은 Unity Camera.Render로 확인했습니다. 원본 에디터를 유지하기 위해 설치된 동일 패키지를 사용하는 별도 검증 프로젝트를 사용했습니다. 작업 전부터 존재한 `UNKNOWN_Battle_Test.unity`의 사용자 변경은 수정하지 않았습니다.

직접 확인하려면 같은 `Builds/UNKNOWN` 폴더 전체를 두 PC에 복사하고 방 생성·참가 → 양쪽 준비 → 게임 시작을 진행하세요. 경기 종료 후 대기실로 복귀하여 다시 준비·시작하면 재추첨합니다. 두 번 연속 같은 맵이 나오는 것도 정상입니다.
