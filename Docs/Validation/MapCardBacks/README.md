# 맵별 덱 이미지와 덱 클릭 버리기

2026-11-10 Unity 6000.0.74f1에서 구현하고 검사했다.

Assets/CARD/CardBack의 이미지 6개를 BattleMapSettings의 각 맵에 연결한다. 양쪽 덱은 기존 동기화된 engine.Config.Map을 사용하며 이미지 비율을 유지한다. 일반 맵은 normal, 조커는 joker, 문양 맵은 diamond/spade/heart/club을 사용한다. 이미지 원본과 Import 설정은 유지한다.

별도 버리기 버튼을 제거하고 기존 discardButton 참조를 본인 덱의 Button에 연결했다. 내 덱 클릭 후 손패 카드를 클릭하면 기존 Discard 명령을 실행한다. 덱 재클릭과 ESC로 취소한다. 턴당 1회, 현재 턴, 대상 선택 중 입력 제한 및 온라인 Host 판정은 기존 경로를 사용한다. 상대 덱에는 Button을 추가하지 않는다. 튜토리얼의 안내와 강조 대상도 내 덱을 사용한다.

전체 UI 자동 검사 776개 통과, 실패/예외 0개. 6개 맵 각각의 이미지 참조, 양쪽 플레이어 관점, 실제 포인터 클릭으로 버리기 시작/취소/손패 선택, 기존 카드 확대와 드래그를 포함한다. 1280×720과 1920×1080 일반/조커 맵 렌더를 확인했다.

검사 개발 중 변수 이름 충돌을 수정했다. 첫 UI 검사에서 이미지가 비어 있음을 확인하고 Unity 에셋 연결 메서드로 참조를 저장한 후 전체 검사를 다시 통과했다. 다시 연결하려면 UNKNOWN > Cards > Connect Map Card Backs를 실행한다.

이번 온라인 검증은 기존 PvP 로컬 회귀 검사와 양측 표시 검사를 사용했다. 실제 두 PC의 Relay 연결은 다시 실행하지 않았다.

튜토리얼 검사 38개 추가 통과, 전체 합계 814개. Windows Development Build 성공, 오류 0개 및 경고 8개. Builds/UNKNOWN-MapCardBacks/UNKNOWN.exe를 폴더 전체와 함께 사용한다. 복사본의 게임 DLL과 빌드 출력 해시가 일치한다.
