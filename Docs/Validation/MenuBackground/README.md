# 하위 메뉴 배경 교체

2026-11-10, Unity 6000.0.74f1, Windows.

`Assets/Title/back.png`를 RoomVisualSettings의 menuBackground에 연결했다. 온라인 대전, 방 코드 참가, 대기실, 게임 시작 안내, 튜토리얼 선택, AI 난이도 선택 화면에서 사용한다. 홈은 기존 Title.png를 사용한다. 중앙 정렬과 기존 버튼 동작을 유지하며 배경은 비율을 유지해 채우고 입력을 가로채지 않는다. 전투 화면은 기존 맵별 이미지를 계속 사용한다.

## 확인

- 실제 패키지 참조를 사용한 런타임/Editor Roslyn 컴파일 통과.
- Unity UI 검사 59개 통과, 실패/예외 0. 홈의 두 해상도 검사, 6개 하위 페이지의 back.png 연결/표시/입력 대상, 실제 NavigationSubmit 이벤트에 의한 온라인/튜토리얼/AI 선택과 홈 복귀 시 이미지 전환을 포함한다.
- Menu-Online.png와 Menu-Tutorial.png를 직접 확인했다. 캡처는 UI Toolkit 패널을 실제 RenderTexture로 렌더한 1280×720 이미지다. Join/Lobby/AiSelect 캡처도 보존한다.
- 로컬 UI 상태로 화면을 검사했다. 실제 Relay 세션을 생성하거나 참가하지 않았으며, 대기실 스크린샷은 연결 전 UI 모양 확인용이다. 이번 변경에서 전투 전체 회귀를 다시 실행하지 않았다.
- Windows Development Build 성공: 오류 0, 경고 8. `Builds/UNKNOWN-MenuBackgrounds/UNKNOWN.exe`와 폴더 전체를 사용한다. 기존 타이틀/맵 배경도 포함한다. 복사본과 빌드 출력 DLL의 해시 일치를 확인했으며 BuildSummary/SourceManifest를 보존한다. 기존 빌드 폴더는 유지한다.

별도 사용자 설정은 필요 없다. Play를 종료하고 Assets > Refresh 후 SampleScene을 실행한다. 원본 PNG와 Import 설정, 씬, NetworkManager는 변경하지 않았다. 필요하면 `UNKNOWN > Maps > Connect Title Background`로 홈/하위 메뉴 이미지를 함께 다시 연결할 수 있다.
