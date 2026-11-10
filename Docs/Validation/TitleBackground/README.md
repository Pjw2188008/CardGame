# 타이틀 이미지 교체

2026-11-10, Unity 6000.0.74f1, Windows.

`Assets/Title/Title.png`를 RoomVisualSettings Resources 에셋에 Texture 참조로 연결했다. 원본 이미지와 Import 설정을 유지한다. RoomTestUI의 홈 화면에서 전체 화면에 비율을 유지해 채우고, 이미지에 이미 있는 UNKNOWN 로고와 겹치지 않도록 기존 텍스트 타이틀·장식을 홈에서 숨긴다. 메뉴 5개는 로고 아래 중앙에 배치한다. 온라인/AI/튜토리얼의 하위 화면은 기존 레이아웃을 유지하며 홈 복귀 시 이미지를 복원한다.

연결은 완료되어 추가 설정이 필요 없다. 다시 연결하려면 `UNKNOWN > Maps > Connect Title Background`를 사용한다. Unity Play를 종료하고 Assets > Refresh 후 SampleScene을 실행하면 된다. 기존 EXE에는 자동 반영되지 않는다.

## 실제 검사

- 작업 프로젝트의 실제 패키지 참조를 사용한 런타임/Editor Roslyn 컴파일 통과.
- Unity PlayMode 검사 33개 통과, 실패/예외 0. 두 해상도에서 원본 Texture 연결, 중복 타이틀 숨김, 메뉴 5개 중앙 정렬/화면 안 배치, 패널 Pick에 의한 실제 클릭 대상 확인, NavigationSubmit 이벤트로 온라인/AI/튜토리얼 페이지 열기와 홈 복귀를 검사했다. 새 네트워크 세션이나 전투를 생성하지 않는 것도 확인했다.
- Title-1280.png와 Title-1920.png는 UI Toolkit 패널을 실제 RenderTexture로 렌더한 결과다. 두 이미지를 직접 확인했다.
- Windows Development Build 성공, 오류 0/경고 8. `Builds/UNKNOWN-Title/UNKNOWN.exe`와 폴더 전체를 사용한다. 이번 타이틀과 이전 맵 이미지 교체를 포함한다. 기존 빌드는 유지한다. BuildSummary에 DLL 해시를 기록했고 복사본/빌드 출력 해시 일치를 확인했다. SourceManifest의 이미지/C#/스타일 파일도 작업/검증 프로젝트에서 일치한다.

스크립트: RoomVisualSettings.cs, RoomTestUI.cs, RoomTestUI.TitleChecks.cs, TitleCheckMenu.cs. 스타일: UnknownRoomStyle.uss. 설정: RoomVisualSettings.asset. 씬이나 NetworkManager를 추가하지 않았다.

검사 실행 메서드는 `Unknown.Multiplayer.Editor.TitleCheckMenu.RunBatch`다. 이번 작업에서는 실제 Relay 접속이나 카드 전투 전체 회귀를 다시 실행하지 않았다. 화면 비율이 원본과 다르면 가장자리를 중앙 기준으로 잘라 채운다. 모든 기기/비율을 실기기로 검증하지는 않았다.
