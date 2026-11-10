# 맵 배경 이미지

`Assets/Map`의 원본 이미지를 `Assets/UNKNOWN/Battle/Resources/BattleMapSettings.asset`에 Texture 참조로 연결한다.

| 맵 | 이미지 |
|---|---|
| Normal / 기본 | O_MAP.png |
| JokerGame / 조커의 유희 | J_MAP.png |
| Diamond / 다이아 | D_MAP.png |
| Spade / 스페이드 | S_MAP.png |
| Heart / 하트 | H_MAP.png |
| Club / 클로버 | C_MAP.png |

배경은 `BattleUIController.RefreshMapUI`에서 현재 엔진 맵을 따라 선택한다. 온라인 Client도 Host Snapshot의 맵을 사용하므로 별도 배경 추첨을 하지 않는다. 재경기 및 로컬/AI/튜토리얼도 동일 경로를 사용한다.

이번 변경을 포함한 Windows Development Build는 `Builds/UNKNOWN-MapBackgrounds/UNKNOWN.exe`다. 실행 파일을 분리하지 말고 폴더 전체를 사용한다.

원본 PNG의 위치와 Import 설정을 유지한다. Resources 설정 에셋이 원본 Texture를 참조하므로 Player 빌드에도 포함된다. RawImage는 원본 비율을 유지하며 화면을 채운다. 비율이 다른 화면에서는 중앙 기준으로 가장자리가 화면 밖으로 나갈 수 있다. UI 뒤에 배치하고 Raycast를 끈다. 배경 이미지가 있으면 기존 반복 문양 장식을 숨기며, 참조가 없을 경우 기존 단색 배경을 사용한다.

다른 그림으로 교체할 때는 해당 PNG를 교체하거나 BattleMapSettings 에셋의 Background 필드 6개를 지정한다. `UNKNOWN > Maps > Connect Supplied Backgrounds`로 현재 Map 폴더의 6개 이미지를 다시 연결할 수도 있다. 이번 작업에서는 이미 연결했으므로 추가 설정은 필요 없다. 씬에 컴포넌트를 추가할 필요는 없다. Unity에서 Play 중이면 종료한 뒤 Assets > Refresh 후 다시 실행한다. 기존 EXE에는 자동 반영되지 않으므로 배포할 때는 새 Player 빌드를 생성해야 한다.

검증은 `Unknown.Multiplayer.Editor.RoomSetupMenu.RunMapBackgroundChecksBatch` 또는 기존 전체 UI Suite에서 진행한다. 문양 맵 검사에 6맵의 원본 Texture 매칭, 양쪽 Snapshot 시점, 화면 비율/배경 순서/Raycast 검사와 실제 렌더 캡처를 추가했다. 검사 결과는 `Validation/MapBackgrounds`에 보존한다.
