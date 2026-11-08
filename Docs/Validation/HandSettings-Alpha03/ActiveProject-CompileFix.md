# 활성 프로젝트 컴파일 누락 수정 (2026-11-08)

- 실제 프로젝트의 `Library/Bee/tundra.log.json`에서 `discardMode`, `RefreshHandTools`, `SmokeDiscard`, `SmokeOptions` 관련 CS0103/CS1061 오류를 확인했다.
- 해당 정의가 들어 있는 `BattleUIController.Discard.cs`는 디스크에 있었지만, 활성 Unity의 Assembly-CSharp.rsp 및 IDE csproj의 Compile 항목에서 누락돼 있었다. 일반 Refresh와 파일 수정 후 Refresh에도 누락이 유지됐다. 누락을 처음 발생시킨 내부 AssetDatabase 원인은 확정하지 못했다.
- 파일과 .meta를 함께 `BattleUIController.HandActions.cs`로 변경하여 기존 GUID를 보존하고 다시 가져왔다. 게임 로직은 변경하지 않았다.
- 활성 Unity에서 새 파일의 rsp/csproj 포함, Assembly-CSharp.dll 재생성, Bee 실패 노드 0개, Console 오류 0개 및 경고 0개를 확인했다. Console을 Clear하지 않고 재컴파일 결과를 확인했다.
- 이 수정에서는 EXE 온라인 경기를 다시 실행하지 않았다. 이전 온라인 검증 및 빌드 기록과 구분한다.
