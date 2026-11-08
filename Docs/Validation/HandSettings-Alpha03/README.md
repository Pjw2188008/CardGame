# 손패·버리기·환경설정 검증

검증일: 2026-11-08. Unity 6000.0.74f1, 기존 패키지와 NetworkManager를 사용했다. 사용자의 에디터를 종료하지 않고 격리 검증 프로젝트에서 실행했다. 최종 빌드는 `Builds/UNKNOWN/UNKNOWN.exe`이며 폴더 전체를 배포한다.

## 자동 검사

| 검사 | 통과 | 실패 | 오류 |
|---|---:|---:|---:|
| 순수 전투 규칙 | 153 | 0 | 0 |
| 공격 UI | 43 | 0 | 0 |
| 드래그·손패·조합 | 58 | 0 | 0 |
| PvP 공개 투영·승리 | 22 | 0 | 0 |
| 조커 맵 | 107 | 0 | 0 |
| 문양 맵 | 50 | 0 | 0 |
| 포커 | 134 | 0 | 0 |
| 손패·버리기·설정·연출 충돌 | 37 | 0 | 0 |
| Play Mode 합계 | 451 | 0 | 0 |

`Rules.txt`와 `UNKNOWN-*.json`은 원본 결과이다. `.ps1`과 `.cmd` 모두 규칙 검사 153개가 통과했다. 기존 10장 손패의 기대값을 8장으로 갱신했고 기존 카드/맵/포커 효과 수치는 유지했다. 드래그 정렬 검사도 1~8장을 검사하도록 바꿨다. 실제 SampleScene을 명시적으로 열어 타이틀 문서의 입력 차단과 기존 온라인 컴포넌트가 있는 상태를 검사했다.

`Settings-1920.png`, `Attack-HandEight-1920.png`, `Attack-ManualDiscard-1920.png`는 실제 Canvas를 렌더링한 화면이다. 음량, 모드, 해상도, VSync와 버튼이 중앙 정렬되고, 8장 손패·작은 버리기 도구가 필드 및 조합 버튼과 겹치지 않는지 확인했다.

## 실제 EXE 화면·저장 검사

`PreferencesProbe.write.json`은 그래픽 장치를 사용하는 Windows EXE에서 실제 적용한 요청과 Unity가 반환한 상태를 기록한다.

| 요청 | 실제 상태 |
|---|---|
| 창 모드 1280×720, VSync OFF | Windowed 1280×720, VSync 0 |
| 테두리 없는 창 1280×720, VSync ON | FullScreenWindow 1280×720, VSync 1 |
| 전체 화면 2560×1440, VSync OFF | ExclusiveFullScreen 2560×1440, VSync 0 |

Master 42%, BGM 35%, SFX 77%, 창 모드 1280×720, VSync OFF를 저장하고 종료했다. 다시 실행할 때 명령행에 다른 크기 1600×900을 줬지만 저장한 1280×720으로 복원됐고, 음량을 포함한 모든 저장 필드의 일치 검사가 통과했다. `PreferencesProbe.read.json`은 최종 빌드에서 재확인한 결과다.

설정 프로세스 검증은 명시적인 Development Build 옵션에서만 동작하며 `UNKNOWN.Options.Probe.v1`이라는 별도 키를 사용했다. 일반 사용자의 `UNKNOWN.Options.v1`과 인증 데이터는 변경하지 않는다. Editor 검사는 기존 옵션 키를 백업한 후 원래 값 또는 원래 키 없음 상태로 복구한다.

## 발견하고 수정한 오류

첫 실제 Relay 검사(seed 4)의 클로버 경기 39턴에서 Guest의 `RefreshPokerUI → UpdateCardStats → Transform.Find`가 제거된 카드 UI를 참조하는 NullReferenceException을 기록했다. 이 실행은 중단했으며 성공 결과에 포함하지 않았다. `Interrupted-guest.json`, `Interrupted-host.json`, `Interrupted-stack.txt`로 보관했다.

클로버 소환의 추가 드로우가 손패 view를 제거한 뒤 포커 알림이 이어지는 순서에서, `cardViews`에 남은 옛 view를 다시 사용하던 문제였다. 손패 view 제거 시 동일한 사전 항목도 정리하고, 포커 갱신·빛 효과·능력치 갱신에서 Unity 객체 유효성을 검사하도록 수정했다. 소환 → 추가 드로우 → 포커 알림 순서를 재현하는 검사를 추가했고 정상 필드 view로 교체되는 것까지 확인했다.

## 최종 실제 Relay 검사

수정한 최종 Development Build 두 개를 같은 PC에서 실행했다. seed 71의 클로버 경기이며 정상 덱·카드 효과·마나·드로우·포커·승리 판정을 사용했다. 처음 8턴은 실제 턴 드로우로 손패를 채웠고 카드/필드를 주입하지 않았다.

- 17턴에 경기 종료, 양쪽 결과 화면 일치, 최종 대기실 복귀와 방 나가기 완료.
- 초과 카드 폐기 5회. Host와 Guest의 드로우/폐기 공개 이벤트 기록과 최종 공개 상태가 동일했다.
- 각 플레이어 수동 버리기 2회, 턴별 사용 상태 초기화와 재사용 확인.
- 양쪽 모두 추가 버리기 요청을 Host가 거절했고 손패·묘지 수량과 revision이 보존됐다.
- 모든 snapshot의 손패 한도 8장, 상대 손패 비공개 검사 통과.
- 양쪽에서 설정을 1.1초 열어 두어도 서버의 턴 시간이 계속 감소했고, 로컬 입력 차단과 닫은 후 복원이 통과했다.
- Host 84회, Guest 62회의 최종 화면 검사 통과. 공개 필드 상태/배지/표시 공격력을 확정 snapshot과 비교했다.
- 최종 검사 오류/예외 0. 검사에서 실행한 프로세스만 종료했다.

`RelaySummary.json`, `Relay-host.json`, `Relay-guest.json` 및 연결 로그를 참고한다. 연결 로그는 토큰을 포함하지 않는 기존 진단 행만 추출했다. 최종 DLL SHA256, 빌드 입력 소스 일치는 `BuildSummary.json`에 기록했다.

## 남은 경고와 검증 범위

최종 Windows Development Build 성공, 오류 0, 경고 8. 기존 격리 빌드 환경의 ServicesCore 연결 경고 및 URP 전처리/stripper/Light2D shader 경고가 남아 있다. 전체 빌드 로그는 `Library/HandSettings-Build-final.log`이다. 실제 Authentication/Relay 연결은 위 검사에서 성공했다.

현재 프로젝트에 기존 음원 파일이 없어 새 전투 음악이나 효과음 콘텐츠를 만들지는 않았다. 실제 AudioSource/AudioListener 볼륨과 미리듣기 연결, 정확한 0% 음소거를 검사했으나 스피커 출력의 청감 검증은 하지 않았다. 서로 다른 두 PC, 다른 모니터/GPU 조합, 인위적인 지연/패킷 손실은 검증하지 않았다.
