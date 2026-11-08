# Alpha 0.3 검증

검증 날짜: 2026-11-08, Unity 6000.0.74f1. 사용자 프로젝트의 Assets를 기존 격리 검증 프로젝트로 복사하여 실행했다. 패키지나 사용자의 실행 중인 Unity는 초기화하지 않았다.

| 구분 | 통과 | 실패 | 런타임 오류 | 기록 |
|---|---:|---:|---:|---|
| 순수 전투 규칙 | 144 | 0 | 0 | `Rules.txt` |
| 기존 공격 UI | 43 | 0 | 0 | `UNKNOWN-AttackUI-Checks.json` |
| 기존 드래그·조합·손패 | 60 | 0 | 0 | `UNKNOWN-DragUI-Checks.json` |
| 기존 PvP 투영·승리 | 22 | 0 | 0 | `UNKNOWN-PvP-Regressions.json` |
| 조커 사건·맵 연출 | 106 | 0 | 0 | `UNKNOWN-JokerMap-Checks.json` |
| 문양 맵 | 49 | 0 | 0 | `UNKNOWN-SuitMap-Checks.json` |
| 새 포커·능력치·마나 UX | 134 | 0 | 0 | `UNKNOWN-Poker-Checks.json` |
| Play Mode 합계 | 414 | 0 | 0 | 위 6개 JSON |

기존 순수 규칙과 일부 UI fixture는 이전 단독 규칙의 기준값을 검증하기 위해 포커를 명시적으로 끈다. 실게임의 기본 설정과 새 포커 검사에서는 포커가 켜져 있다. 새 규칙 검사는 실제 소환·전투 경로, 9종 판정, 중첩/해제, 체력 파괴 연쇄, 경기당 보상, 두 recipient의 공개 상태/비공개 손패를 별도로 검사한다.

처음 실행한 포커 UI 검사에서는 반환 카드가 없어야 한다는 검사에 상시 공격 피드백 레이어를 포함해 한 항목이 실패했다. 반환 복제본과 상시 레이어를 구분하도록 수정했고, 초기화 중인 반환 복제본도 정리하는 검사를 추가했다. 위 표와 JSON은 수정 후 최종 결과이다.

화면 캡처는 실제 Play Mode Canvas를 Camera.Render로 출력한 1920×1080 PNG이다. 전체 족보 목록을 펼쳐도 필드·타이머·맵 이름이 가려지지 않는지 직접 확인했다.

- `Attack-PokerFullHouse-1920.png`: 아군 풀하우스/상대 원페어, 구성 카드 표시, 최종 능력치
- `Attack-PokerReference-1920.png`: 9종 목록, 활성 강조 및 필드와 분리된 배치
- `Attack-PokerStats-1920.png`: 포커/맵/JOKER/카드 능력과 피해가 함께 있는 상세 정보
- `Attack-ManaRejected-1920.png`: 마나 0에서 거절 안내와 손패 복귀

Windows x64 Development Build는 `Builds/UNKNOWN/UNKNOWN.exe`이다. 빌드 결과는 성공, 오류 0, 경고 8이다. 기존 격리 검증 환경의 URP 전처리/stripper 경고, Light2D shader 벡터 축소 경고, ServicesCore의 에디터 연결 경고가 포함되어 있다. 실제 Authentication·Relay 연결 결과는 별도의 온라인 기록으로 판단한다. 런타임 Project ID와 Environment를 변경하지 않았다.

최종 DLL의 SHA256 및 변경된 Assets C# 파일과 빌드 입력 복사본의 일치 여부는 `BuildSummary.json`에 기록한다. 전체 빌드 로그는 프로젝트 `Library/Alpha03-Build.log`에 남긴다.

## 실제 Relay 검증

Development Build의 EXE 두 개를 같은 PC에서 실행했다. 정상 덱·카드 효과·마나·맵 사건을 사용했고, 테스트 fixture로 필드나 카드를 주입하지 않았다. 명시적인 Development Build 검증 옵션으로 RNG seed 348을 고정해 재현성을 확보했으며, 평상시 실행에서는 이 옵션이 적용되지 않는다. 두 프로세스는 `default`, `unknown_local_2` 프로필로 서로 다른 Authentication Player ID를 사용했고 Project ID와 `production` 환경은 일치했다.

| 경기 | 맵 | 종료 턴 | 공개 시너지 전환 | Host/Guest UI 검사 표본 | 결과 |
|---|---|---:|---:|---|---|
| 1 | 하트 | 9 | 1 | 60 / 47 | 통과 |
| 재경기 | 조커의 유희 | 19 | 8 | 145 / 125 | 통과 |

확인 사항:

- 총 9개 시너지 전환 기록이 양쪽에서 일치했다. 조커 경기에서 원페어 → 투페어 → 원페어 → 없음, 양쪽 플레이어의 원페어 발생과 파괴 후 해제를 실제로 확인했다.
- 두 경기의 최종 공개 상태 문자열이 일치했다. 문자열에는 양쪽 필드 카드 ID/능력치/원인별 변화량 및 시너지 종류·구성·대상이 포함된다. 각 수신 화면의 필드 위치·표시 ATK·배지와 수신 모델의 HP/최대 HP를 Host 확정 snapshot과 비교했다. HP 텍스트 자체의 렌더링 값은 위 Play Mode 검사에서 별도로 확인했다.
- 각 EXE에서 경기당 2회, 총 8회 마나 부족 카드를 UI 드래그로 놓았다. Host 거절 후 revision·손패·마나가 보존되고, 실제 Client 안내 메시지에 필요/현재 마나가 일치했으며 같은 오류를 반복해도 안내가 갱신됐다.
- 양쪽 손패 비공개, UNKNOWN/JOKER 진행도 비공개, 맵/결과 화면 일치, 맵 사건 1회 처리, 재경기 초기화·맵 재추첨이 모두 통과했다.
- 두 경기 모두 승리 판정 후 대기실로 돌아왔고, 마지막에는 양쪽 세션이 정리됐다. 테스트에서 실행한 두 프로세스만 종료했다. 오류/예외 수집 결과 0이다.

`RelaySummary.json`에 비교 결과, `Relay-host-round1.json` 등 네 파일에 각 클라이언트의 원본 결과, `Relay-host-connection.txt`와 `Relay-guest-connection.txt`에 인증·연결·맵 추첨 로그를 저장했다. 로그에 인증 토큰이나 비밀 키를 추가하지 않았다.

실제 다른 두 PC, 인위적인 고지연·패킷 손실 환경은 검증하지 않았다. 9종 모두의 재현은 통제된 Play Mode 및 규칙 검사이며, 실제 무작위 온라인 경기에서 9종 모두를 자연 발생시켰다고 주장하지 않는다.
