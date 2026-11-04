# Alpha 0.1 공격 UX

아군 필드 유닛에서 적 유닛 또는 상단 상대 HP 영역으로 드래그하면 공격합니다. 조작 중에는 필드 카드가 고정되고, 로컬 UI 레이어에 곡선 화살표만 표시됩니다. 유효한 대상은 붉게 강조·확대되고, 잘못된 대상은 회색 화살표로 표시됩니다. 빈 공간·아군·빈 슬롯·손패·버튼에 놓거나 ESC를 누르면 공격 횟수를 쓰지 않고 취소됩니다.

공격 가능한 아군 유닛은 초록 테두리·검 표시·약한 발광을 사용합니다. 공격한 유닛은 `공격 완료`, 소환 직후는 시계 표시를 사용합니다. Hover 툴팁에는 소환 턴 제한·이미 공격함·상대 턴·다른 행동 처리 중 등의 이유가 나옵니다. 상대 필드가 비고 공격 가능한 아군이 있으면 상대 HP 영역에 약한 붉은 테두리가 나타납니다.

기존 클릭 공격은 보조 조작으로 유지합니다. 손패 일반 카드·UNKNOWN은 빈 아군 슬롯으로 드래그해 소환할 수 있고, A/J/Q/K는 중앙 조합 슬롯으로 드래그할 수도 있습니다. JOKER는 기존처럼 아군 유닛에 드래그합니다. 손패 드래그와 공격 드래그는 동시에 시작할 수 없습니다.

## 구현 및 연결

- `BattleUIController.Attack.cs`: 공격 시작/이동/드롭/취소, HP 대상 영역, 대상 강조, 공격 가능 상태, 툴팁과 기존 Dispatch 연결.
- `AttackArrowGraphic.cs`: Unity UI VertexHelper 기반 곡선 화살표와 삼각형 끝. Raycast를 막지 않으며 Canvas 좌표로 화면 포인터를 변환합니다.
- `CardDrag.cs`: 필드 전용 UnitAttackDrag 및 공통 드롭 분기. 성공한 드래그에서는 클릭을 억제합니다.
- `BattleUIController.cs`, `.Interaction.cs`, `CardHover.cs`, `CardAnimationController.cs`: UI 구성, 상태 갱신, 기존 소환/조커/조합과의 충돌 방지. 애니메이션 복제본의 입력·손패 정렬 컴포넌트는 비활성화합니다.
- `BattleProtocol.cs`: 온라인 공격 요청의 `first`는 공격자 ID, `second`는 적 카드 ID입니다. `-1`만 플레이어 직접 공격을 뜻합니다. Host가 대상 ID를 현재 적 필드 슬롯으로 해석하고 기존 BattleEngine.Attack으로 소유권·턴·소환 턴·공격 완료·유효 대상·직접 공격 조건을 검증합니다. 사라진 대상 ID를 직접 공격으로 바꾸지 않습니다.
- `NetworkBattleController.cs`: 공격 대상 필드의 의미 변경에 맞춰 전투 메시지를 v2로 구분합니다. 양쪽 모두 최신 빌드를 사용해야 합니다. Relay 세션 생성/참가 및 인증 프로필 구조는 유지합니다.
- `NetworkBattleSmokeRunner.cs`: 실제 드래그 공격 메서드를 사용하고 Host/Guest·유닛/직접 공격 횟수와 공개 상태를 기록합니다. 검사 코드 파일 읽기는 공유 모드 및 재시도를 사용합니다.

BattleEngine의 능력치·공격/반격·마나·타이머·UNKNOWN 승리·조커 확률 규칙은 수정하지 않았습니다. 공격 확정 전에 공격 횟수나 HP를 UI가 변경하지 않습니다. 화살표 자체는 네트워크로 전송하지 않고 확정 이벤트만 양쪽 기존 애니메이션으로 재생합니다.

Canvas는 기존 런타임 생성 구조를 그대로 사용합니다. SampleScene의 기존 RoomTestUI/NetworkBattleController에서 생성하는 BattleUIController와 로컬 전투 씬 모두 새 UI가 자동 연결됩니다. 씬·프리팹의 수동 참조 연결이나 NetworkManager 추가는 필요하지 않습니다.

## 검증

- 규칙 및 Host 요청 검증: **69개 통과**. 대상 ID 해석, 아군/사라진 대상 ID 거절, 직접 공격 방어 조건, 중복 공격을 포함합니다.
- Unity 실제 Play Mode 공격 UI: **30개 통과, 오류 0개**. 카드 고정, 화살표 포인터 좌표, 대상 강조, 취소·중복 드롭, 공격 완료·소환 직후·상대 턴·처리 중 표시, 시간 만료 취소, 직접 공격과 HP 승리, 손패 드래그 소환, 왼쪽 버튼 전용 입력 및 파괴 확정 직후 강조 해제를 확인했습니다.
- 기존 드래그·손패 UI: **40개 통과, 오류 0개**. 조커 성공/실패와 마나, 중앙 UNKNOWN 등록/취소/확정, 1~10장 손패 중심, 드로우 순서와 비공개 정보를 확인했습니다.
- PvP 표시·승리 UI: **22개 통과, 오류 0개**. 양쪽 플레이어 화면 배치와 UNKNOWN 승리·자기 진행도만 표시되는 동작을 확인했습니다.

검사 메뉴는 `UNKNOWN > Multiplayer > Run Attack UI Checks`입니다. 배치 검사 진입점은 `Unknown.Multiplayer.Editor.AttackCheckMenu.RunSuiteBatch`이고, JSON은 Library의 UNKNOWN-AttackUI-Checks / UNKNOWN-DragUI-Checks / UNKNOWN-PvP-Regressions에 생성됩니다. 최종 검증 자료는 `Docs/Validation/Attack-UX/`에 보관합니다.

현재 에디터를 유지하기 위해 Windows 빌드는 Assets·Packages·ProjectSettings를 복사한 독립 임시 프로젝트에서 생성합니다. 원본 Library를 초기화하거나 임시 프로젝트와 공유하지 않습니다. 새 프로젝트의 패키지 해석 대기를 피하기 위해 기존에 설치된 같은 버전의 패키지 소스 76개를 임시 프로젝트의 Packages에 복사했습니다. 원본 manifest는 수정하지 않았고, 빌드 복사본의 모든 UNKNOWN C# 소스가 원본과 동일함을 해시로 확인했습니다.

## Windows / Relay 결과 (2026-11-04)

최종 Development Build는 성공했으며 오류 0개, 경고 8개입니다. 빌드 로그는 `Library/UNKNOWN-Attack-Final-Windows-Build.log`입니다. 실행 파일은 `Builds/UNKNOWN/UNKNOWN.exe`이며 배포할 때 해당 폴더 전체를 복사합니다. 전투 메시지 v2를 사용하므로 양쪽 모두 이 최신 빌드를 사용해야 합니다.

같은 PC의 Windows EXE 두 개를 실제 Unity Relay로 연결하고, 고정 시드 15로 정상 덱을 섞어 자동 대전을 실행했습니다. 테스트용 유닛이나 HP를 온라인 게임에 주입하지 않았습니다. 봇은 실제 UI의 BeginAttackDrag/DropAttack과 기존 소환·조커·조합 요청 경로를 사용합니다. `-unknownSmokeNoWait`로 첫 턴의 의도적인 60초 대기를 생략했으며 일반 실행의 타이머 규칙은 바꾸지 않았습니다.

| 항목 | 결과 |
|---|---|
| Host / Client PID | 45252 / 8528 |
| 세션 코드 | GHKL8R (검사 종료 후 정리됨) |
| 종료 턴 / 승리 | 17턴 / 플레이어 1, 상대 HP 0 |
| Host / Client 공격 | 14회 / 3회 |
| 유닛 / 직접 공격 | 12회 / 5회 |
| 공격자·대상·피해·반격·플레이어 HP 기록 | 17개 모두 양쪽 동일 |
| 최종 공개 게임 상태 | 양쪽 동일 |
| 실제 UI와 수신 상태 비교 | Host 132회 / Client 125회, 모두 일치 |
| UNKNOWN 정보 비공개 | 양쪽 통과 |
| 기존 기능 동시 실행 | 각 플레이어 UNKNOWN 조합 1회, 조커 결과 3회 관찰 |
| 승패 화면 / 대기실 복귀 / 방 정리 | 양쪽 통과 |
| 실행 오류 | 양쪽 0개 |

결과와 공격별 기록은 `Docs/Validation/Attack-UX/Summary.json`, `Relay-Host.json`, `Relay-Guest.json`에 보관했습니다. Relay 연결 로그 발췌도 같은 폴더에 있습니다. 검사용 두 EXE는 방 정리 확인 후 종료했습니다.

남은 확인 범위: 서로 다른 물리 PC 및 외부 네트워크 환경, 실제 마우스로 다양한 해상도에서 외관을 확인하는 수동 검사는 하지 않았습니다. 자동 Play Mode 검사는 실제 Unity UI 이벤트와 Canvas 좌표 변환을 사용했고, 온라인 검사는 두 로컬 EXE의 실제 Relay 통신을 사용했습니다. 빌드에 ServicesCore/URP 등의 경고가 남지만 C# 컴파일 오류와 검사 중 실행 오류는 없습니다.
