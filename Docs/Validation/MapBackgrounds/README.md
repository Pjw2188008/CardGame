# 맵 배경 교체 검증

2026-11-10, Unity 6000.0.74f1, Windows.

`Assets/Map`의 O/J/D/S/H/C 이미지를 기존 BattleMapSettings의 Texture 필드에 연결했다. 원본 이미지와 Import 설정은 유지한다. 맵 효과/추첨/승리 규칙을 바꾸지 않고, 현재 엔진의 맵을 따라 배경을 선택한다.

## 실제 확인

- 런타임/Editor Roslyn 컴파일 통과.
- 전체 Unity UI 검사 485개 통과, 실패/예외 0: 공격 43, 드래그 58, PvP 26, 조커 사건 107, 문양 맵/배경 80, 포커 134, 손패/설정 37. 개별 JSON과 ChecksSummary를 보존했다.
- 새 배경 검사 30개는 맵 6종 각각의 두 플레이어 Snapshot 시점에서 정확한 원본 Texture가 선택되는지, 배경이 활성화되는지, UI 뒤에 배치되고 Raycast를 받지 않는지, 비율을 유지해 화면을 채우는지와 실제 렌더 캡처를 확인했다.
- 첫 실행에서는 에셋 참조가 런타임에 로드되지 않아 배경 검사 18개가 실패했다. Unity AssetDatabase로 원본 Texture를 직접 지정·저장한 뒤 재실행해 모두 통과했다. 저장된 에셋을 작업 프로젝트에 반영했다. 재연결 메뉴도 추가했다.
- Normal.png/JokerGame.png는 실제 1920×1080 Unity 렌더 결과다. 조커 맵의 렌더를 직접 확인하여 제공된 이미지가 기존 단색 배경을 대체하는 것을 확인했다.
- Windows Development Build 성공: 오류 0, 경고 8. `Builds/UNKNOWN-MapBackgrounds/UNKNOWN.exe`와 폴더 전체를 사용한다. BuildSummary에 DLL 해시를 기록했고 복사본과 빌드 출력의 일치를 확인했다. 기존 빌드는 유지했다. 수정 C#과 PNG 6개의 작업/검증 프로젝트 해시도 SourceManifest에서 일치한다.

## 한계

온라인은 Host Snapshot을 각 recipient의 엔진으로 복원해 동일 맵의 배경 선택을 검사했다. 이번 작업에서 실제 Relay 두 프로세스 접속을 재실행하지 않았다. 다른 OS/GPU와 모든 화면 비율을 실기기로 검사하지는 않았다. 화면 비율이 원본과 다르면 비율을 유지하여 채우므로 가장자리가 화면 밖으로 나갈 수 있다.
