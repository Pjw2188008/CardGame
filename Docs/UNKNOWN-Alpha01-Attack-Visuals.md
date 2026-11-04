# Alpha 0.1 공격 드래그 시각화

## 확인한 원인

실제 UI 렌더링 검사에서 `Local Attack Arrow`의 `CanvasRenderer` 누락으로 `MissingComponentException`이 발생했습니다. 설치된 UGUI의 `Graphic`은 `RectTransform`만 필수 컴포넌트로 선언하므로 커스텀 Graphic에 렌더러를 직접 추가해야 합니다. 이전 검사는 활성화 여부와 포인터 좌표만 확인하여 실제 화살표 메시 누락을 잡지 못했습니다.

최초 렌더링 검사에서 확인한 메시지:

```text
MissingComponentException: There is no 'CanvasRenderer' attached to the "Local Attack Arrow" game object, but a script is trying to access it.
at UnityEngine.CanvasRenderer.GetMesh ()
at Unknown.Battle.BattleUIController.CaptureAttackFrame (...)
```

기존 코드는 드래그 이벤트에서만 포인터를 갱신하고 Overlay 전용 null 카메라를 사용했습니다. 플레이어 대상 Outline도 배경 알파 0.2의 영향을 받았으며, 공격자 확대와 커서 안내가 없었습니다. 발광용 사각 Image에 Outline을 중첩하면 카드 내용까지 덮는 문제도 렌더링 이미지에서 확인하여 빈 둘레 메시로 교체했습니다.

첫 Relay 재검증에서는 대전 결과가 일치했지만 방 복귀 때 `OnDestroy → EndAttackDrag → RefreshAttackIndicators → BoardCard`에서 `ArgumentOutOfRangeException` 1건이 발생했습니다. 방 복귀가 LocalPlayer를 먼저 -1로 해제한 뒤 UI를 제거하기 때문입니다. 플레이어 인덱스가 유효하지 않으면 카드 조회와 표시 갱신을 중단하도록 보강하고, 인덱스 해제 후 취소 검사를 추가했습니다.

## 변경 파일과 동작

- `BattleUIController.Attack.cs`: 기존 공격 판정을 재사용합니다. 시작 즉시 공격자를 1.08배 확대하고 밝은 노란색으로 강조합니다. 매 프레임 포인터를 다시 Raycast하여 유효한 대상만 붉게 강조하고 1.05배 확대합니다. 커서 안내는 `공격 대상 선택`, `놓으면 공격`, `놓으면 직접 공격`, `공격 불가`입니다. HP 대상에는 검 아이콘을 표시합니다. 화면 좌표는 Canvas의 실제 렌더 모드와 카메라로 변환합니다.
- `AttackArrowGraphic.cs`: CanvasRenderer를 필수 지정하고 곡선 메시를 그립니다. 두께는 Canvas 배율과 관계없이 화면 7px이며 삼각형 끝이 정확히 포인터에 닿습니다.
- `AttackGlowGraphic.cs`: 카드 내부를 덮지 않는 얇은 발광 테두리입니다.
- `CardDrag.cs`: 필드 공격의 드래그 시작 거리는 화면 8px입니다. 다른 입력에 영향을 주지 않도록 기존 EventSystem 임계값은 시작/해제/비활성화 시 복원합니다.
- `BattleUIController.cs`, `.Interaction.cs`, `.Online.cs`: 새 발광 연결, 매 프레임 추적, 초점 상실·일시정지·시간 만료·파괴·종료·연결 실패 정리를 연결합니다. 실제 승인된 Attack 이벤트에서만 AttackConfirmed로 전환하며 거절 시 기존 오류 안내를 사용합니다.
- `BattleUIController.AttackChecks.cs`: 실제 CanvasRenderer 메시와 Camera 렌더링, 화살표 두께·커서 끝 좌표·비차단 레이어·원상 복구 검사를 추가했습니다.

시각화는 로컬 전용입니다. 좌표나 강조 상태는 네트워크로 전송하지 않으며 유효한 드롭만 기존 공격 요청을 한 번 보냅니다. 손패 소환, JOKER, UNKNOWN 조합과 공격 드래그는 서로 동시에 시작할 수 없습니다.

## 에디터 사용

기존 씬에서 BattleUIController가 UI를 자동 생성하므로 Inspector에 새 오브젝트를 연결할 필요가 없습니다. 기존 NetworkManager를 그대로 사용합니다. Play 중 아군 필드의 초록 테두리 카드를 드래그하고 적 카드 또는 상대 HP 영역에 놓습니다. 직접 공격은 적 필드가 비어 있을 때만 가능합니다. 잘못된 곳에 놓거나 ESC를 누르면 확대·발광·화살표가 복구되고 공격 횟수는 소비하지 않습니다.

검사 메뉴는 `UNKNOWN > Multiplayer > Run Attack UI Checks`입니다. 최종 검증 결과와 렌더링 이미지는 아래에 기록합니다.

## 자동 검사 및 빌드 결과 (2026-11-04)

| 검사 | 결과 |
|---|---|
| 규칙 및 Host 요청 검증 | 69 통과 |
| 공격 UI Play Mode | 44 통과 / 실패 0 / 오류 0 |
| 기존 손패·소환·JOKER·조합 드래그 | 40 통과 / 실패 0 / 오류 0 |
| PvP 표시·비공개 정보·승리 UI | 22 통과 / 실패 0 / 오류 0 |
| Windows Development Build | 성공 / 오류 0 / 경고 8 |

렌더링 검사는 실제 CanvasRenderer 메시를 생성하고 Screen Space Camera에서 1920×1080, 1600×900, 1280×720 이미지를 저장합니다. 각 해상도에서 화살표 두께가 화면 7px인지, 끝점이 포인터와 0.5px 이내로 일치하는지 검사합니다. 기본 Overlay 모드의 포인터 좌표 검사도 유지했습니다. 이미지를 직접 확인하여 화살표와 노란 공격자 테두리, 붉은 대상 테두리, 커서 안내가 표시되고 카드 안쪽이 발광으로 가려지지 않는 것을 확인했습니다.

결과 JSON과 PNG는 `Docs/Validation/Attack-Visuals/`에 있습니다. 최종 전체 검사 로그는 `Library/UNKNOWN-Attack-Visuals-Suite.log`, 빌드 로그는 `Library/UNKNOWN-Attack-Visuals-Windows-Build.log`입니다. 빌드는 원본과 UNKNOWN C# 파일 해시가 모두 일치하는 기존 격리 프로젝트에서 생성했고 `Builds/UNKNOWN/`에 반영했습니다. 원본 씬이나 NetworkManager 설정은 변경하지 않았습니다.

## 최종 Windows / Relay 재검증

수정한 Development Build를 같은 PC에서 두 프로세스로 실행했습니다. Host PID 34284는 default, Client PID 14252는 unknown_local_2 인증 프로필을 사용했으며 Player ID가 서로 다릅니다. 동일 Project ID 및 production 환경에서 Session Code로 참가하고 Netcode 연결 인원 2명을 확인했습니다. 최종 Session Code는 WWH869이며 검사 후 세션을 정리했습니다.

시드 15의 실제 덱으로 자동 대전을 실행했습니다. 각 공격에서 기존 BeginAttackDrag → MoveAttackDrag → DropAttack을 호출하고, 공격자 1.08배 확대·붉은 유효 대상·활성 화살표·커서 안내 상태를 확인한 뒤 Host에 요청합니다. 마우스 좌표를 상대에게 전송하지 않습니다. 이 EXE 검사는 headless 자동 조작이며 실제 그림은 위 Camera 렌더링 검사로 별도 확인했습니다.

| 항목 | 최종 결과 |
|---|---|
| Host / Client 공격 | 14회 / 3회 |
| 유닛 / 직접 공격 | 12회 / 5회 |
| 종료 | 17턴, 플레이어 1의 HP 승리 |
| 양쪽 공격 결과 목록 / 최종 공개 상태 | 모두 일치 |
| 양쪽 필드 표시 / 비공개 정보 | 정상 |
| 승리 화면 / 방 복귀 / 세션 정리 | 양쪽 모두 정상 |
| Runtime 오류 | Host 0 / Client 0 |

최종 보고서는 `Docs/Validation/Attack-Visuals/Relay-Host.json`, `Relay-Guest.json`, `Summary.json`에 있습니다. 상세 로그는 `Library/UNKNOWN-AttackVisual-Relay-Final-Host.log`, `Library/UNKNOWN-AttackVisual-Relay-Final-Guest.log`입니다. 세션 정리 완료 후 테스트 EXE 두 개를 종료했습니다.

실행 파일은 `Builds/UNKNOWN/UNKNOWN.exe`입니다. 다른 PC에서 검사할 때는 양쪽에 `Builds/UNKNOWN/` 폴더 전체를 복사합니다. 별도 Inspector 연결 작업은 없습니다.

## 검증하지 못한 항목

서로 다른 물리 PC의 실제 마우스 조작, 전체 화면/창 모드 전환 중의 수동 시각 검사는 수행하지 않았습니다. 세 해상도의 렌더링·픽셀 두께·끝점 좌표 및 같은 PC의 실제 Relay 두 EXE 동작을 검증했습니다. 기존 게임 규칙과 네트워크 SDK는 변경하지 않았습니다.
