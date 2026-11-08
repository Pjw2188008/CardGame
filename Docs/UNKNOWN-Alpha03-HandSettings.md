# UNKNOWN Alpha 0.3 추가 기능 — 손패 8장·수동 버리기·로컬 설정

## 손패와 온라인 판정

기본 `BattleConfig.HandLimit`은 8이다. 모든 덱 드로우는 기존 공통 `Draw()`를 통과한다. 7장에서 3장을 뽑으면 첫 카드만 손패에 들어가고 나머지 새 카드 2장은 덱에서 제거되어 묘지로 간다. 기존 손패는 건드리지 않는다. 턴 드로우, 빈 손패 5장 보충, 카드 능력, 스트레이트 보상, 클로버 추가 드로우, 조커 맵 드로우에 같은 제한이 적용된다.

조커 생성은 기존 `GiveMapJoker()`의 빈 공간 검사와 맵 사건 자격 검사를 재사용한다. 꽉 찼으면 생성/복제를 진행하지 않는다. UNKNOWN 조합은 기존 재료 2장을 먼저 소비하고 1장을 생성하므로 8장에서도 조합 후 7장이다. 기존 맵 수치·사건 확률·포커·JOKER·승리 조건은 유지했다.

수동 버리기는 `BattleEngine.DiscardHand()`가 Host에서 검증한다. 본인 턴, 본인 손패 인스턴스, 턴당 1회, 미종료·미만료·대상 선택 대기 없음 조건을 검사한다. 성공 시 0마나로 1장을 묘지에 보내며 즉시 드로우·소환·필드 시너지 판정은 실행하지 않는다. 다음 자신의 턴에 사용 상태를 초기화한다. 마지막 카드를 버린 경우에도 다음 자신의 턴에서만 빈 손패 보충이 실행된다.

새 `BattleAction.Discard`는 기존 요청 sequence/revision/turn 검증을 사용한다. 같은 요청의 중복 처리는 거절한다. snapshot에는 손패 제한·묘지 수량·사용 상태를 포함한다. 상대에게는 카드 인스턴스 ID/종류/이름 대신 익명 드로우·폐기 이벤트와 수량만 전달한다. 새 전송 스키마는 **v8**이므로 양쪽 EXE를 같은 빌드로 교체해야 한다.

## 손패 UI

- 손패 오른쪽 아래에 `HAND n/8`, `카드 버리기` 버튼을 추가했다. 최대치에서는 숫자만 강조하고 카드 밝기는 유지한다.
- 버리기 버튼 → 카드 1장 클릭 → 같은 버튼으로 확정한다. 선택 카드는 주황색 외곽선으로 구분한다. 선택 없이 확정하면 안내만 표시한다.
- 버리기 모드에서는 소환/JOKER 드래그·공격 드래그·UNKNOWN 조합 선택을 차단한다. ESC는 먼저 현재 카드 조작 모드를 취소한다.
- 아군·상대 덱 근처에 작은 묘지 수량 표시를 추가했다. 수동 버리기는 손패에서 묘지로 이동하며 중앙 재정렬을 유지한다.
- 초과 드로우는 손패 view를 만들지 않는다. 익명 카드가 덱에서 묘지로 이동·축소·페이드하고 묶음 폐기 수량을 알린다. 상대 화면에 카드 정체를 노출하지 않는다.

## 타이틀·ESC·환경설정

타이틀은 기존 중앙 정렬과 디자인을 유지하고 `게임 시작 / 설정 / 종료`를 제공한다. 게임 시작은 기존 온라인 메뉴로 연결된다. Development Build의 로컬 테스트는 온라인 메뉴 안에서 계속 사용할 수 있다.

전투 ESC 메뉴는 `게임으로 돌아가기 / 설정 / 타이틀로 돌아가기`를 제공한다. 메뉴와 설정은 로컬 입력만 막으며 온라인 서버 시간과 40초 턴 타이머는 계속 흐른다. 타이틀 복귀는 기존 `LeaveRoomAsync()`를 거친다. 설정은 별도 모달 Canvas로 양쪽 플레이어의 전투 입력과 분리했다.

설정 저장 키는 `UNKNOWN.Options.v1`이다. 인증 키/프로필은 건드리지 않는다. 네트워크 snapshot에 개인 설정을 넣지 않는다.

| 옵션 | 동작 |
|---|---|
| Master/BGM/SFX | 0~100% 슬라이더, 수치 표시, 즉시 적용·저장 |
| 화면 모드 | 전체 화면 / 테두리 없는 창 / 창 모드 |
| 해상도 | `Screen.resolutions`의 실제 지원 크기만 중복 제거하여 선택; 목록이 없는 환경은 현재 창 크기 사용 |
| VSync | ON/OFF; 화면 적용 버튼으로 반영·저장 |
| 적용 | 화면 draft 적용·저장 후 닫기 |
| 취소/ESC | 미적용 화면 draft 취소; 즉시 저장한 음량은 유지 |
| 기본값 복원 | 기본 음량 즉시 적용, 권장 화면 설정을 draft로 표시; 화면은 적용으로 확정 |

기본 음량은 80/60/90%, 화면은 지원되는 현재 디스플레이 해상도·테두리 없는 창·VSync ON이다. 새 실행에서는 저장 값을 읽고 실제 지원 해상도와 범위를 검증하여 적용한다. 에디터·batch 테스트에서는 데스크톱 화면을 자동 전환하지 않는다.

### 오디오 연결

현재 프로젝트에는 기존 AudioManager·AudioMixer·BGM/SFX 음원 파일이 없었다. 별도의 믹서 에셋을 임의 생성하지 않고 `AudioListener.volume`으로 Master를, 기존 `AudioSource` 원래 볼륨 × 채널 음량으로 BGM/SFX를 분리했다. 0%는 정확한 0이다. 원래 Source 볼륨을 보존해 반복 적용 시 곱셈이 누적되지 않는다.

설정의 BGM/SFX 미리듣기는 실제 별도 AudioSource와 짧은 테스트 음을 사용한다. 새 음악을 붙일 때 AudioSource에 `GameAudioChannel`을 추가하고 `BGM`을 선택하면 된다. 지정하지 않은 AudioSource는 SFX로 연결된다. 이번 작업에서 새 전투 음악·효과음 콘텐츠를 추가한 것은 아니다.

## 파일과 씬

| 파일 | 변경 |
|---|---|
| `BattleEngine.cs`, `BattleProtocol.cs` | 8장 제한, 수동 버리기, snapshot·authority·비공개 이벤트 |
| `BattleUIController.HandActions.cs` | 손패 도구·묘지·선택 모드·애니메이션·ESC 메뉴 |
| `BattleUIController.cs`, `.Hands.cs`, `.Interaction.cs`, `.Attack.cs` | 기존 손패·드래그·공격 입력과 연결 |
| `BattleUIController.Poker.cs` | 드로우 도중 제거된 카드 UI를 포커 연출에서 다시 참조하지 않도록 보호 |
| `CardAnimationController.cs` | 기존 폐기 이벤트를 새 이동 연출로 전달 |
| `GamePreferences.cs`, `.UI.cs`, `GameAudioChannel.cs` | 저장·오디오·화면·공통 설정 패널 |
| `RoomTestUI.cs`, `NetworkBattleController.cs` | 타이틀 버튼·기존 로비 연결·v8 채널 |
| `NetworkBattleSmokeRunner.cs`, `GamePreferences.Probe.cs` | 명시적인 Development Build 전용 온라인/재실행 검증 |
| `Tests/UNKNOWN/HandTests.cs` | 8장·초과 폐기·소유권·중복·보충·생성·공개 투영 규칙 검사 |
| `BattleUIController.HandSettingsChecks.cs`, `GamePreferences.EditorChecks.cs` | 실제 UI/오디오 값/설정 저장·복원 검사 |

`Assets/Scenes/SampleScene.unity`와 기존 NetworkManager/UnityTransport를 그대로 사용한다. 씬·프리팹 수동 연결이나 패키지 설치는 필요 없다. 설정 서비스는 실행 시 한 번 생성되고 씬 전환에도 유지된다. NetworkManager는 생성하지 않는다.

## 검증

규칙 검사 153개, 실제 SampleScene 기준 Play Mode 검사 451개 통과, 최종 검사 런타임 오류 0. 원본 결과와 렌더링 이미지는 `Docs/Validation/HandSettings-Alpha03`에 보관한다. 기존 검사 중 손패 최대값에 의존한 기대값만 8장으로 갱신했고 포커/맵 밸런스 기대값은 변경하지 않았다.

실제 EXE에서 3종 화면 모드·지원 해상도·VSync 적용과 종료 후 설정 복원을 확인했다. 수정 빌드의 Relay 두 EXE는 클로버 경기 17턴을 완료했고, 초과 폐기 5회·플레이어별 수동 버리기 2회·추가 요청 거절·비공개 정보·설정 중 타이머 진행·세션 정리가 통과했다. 첫 장기 검사에서 발견한 드로우/포커 UI 참조 오류와 수정 내역, 남은 경고 및 검증 범위는 [검증 기록](Validation/HandSettings-Alpha03/README.md)에 구분해 적었다.

SampleScene을 연 뒤 기존 `UNKNOWN > Multiplayer > Run Attack UI Checks`로 전체 7단계를 검사한다. 순수 규칙은 `Tests/UNKNOWN/Run-BattleTests.ps1` 또는 `.cmd`를 실행한다. Windows 배포는 `Builds/UNKNOWN` 폴더 전체를 복사한다.
