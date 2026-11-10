# 공격력·체력 기본 글씨 색 고정

2026-11-10, Unity 6000.0.74f1.

UpdateCardStats에서 공격력과 현재 체력의 증감에 따른 초록/빨강 분기를 제거했다. 항상 기존 기본 글씨 색을 사용한다. 카드 이미지의 숫자는 기존 밝은 기본 색이며, 이미지가 없는 기존 카드 표시도 해당 기본 색을 유지한다. 수치 계산과 실시간 갱신, 배치/중앙 정렬, 현재 체력 단일 표시는 바꾸지 않는다. 다른 전투 효과나 포커 목록의 강조 색상은 변경하지 않는다.

관련 Unity UI 검사 175개 통과, 실패/예외 0: 포커/수치 138, 손패/설정 37. 강화된 공격력과 피해를 받은 체력의 기본 색, 공격력 감소 후 기본 색, 피해/회복/체력 증가 후 값 갱신과 기본 색 유지 여부를 검사했다. 전체 UI 검사나 Relay 접속은 이번 작업에서 다시 실행하지 않았다.

수정 파일은 BattleUIController.Poker.cs와 기존 PokerChecks다. 수정 전 파일은 현재 작업 폴더의 card-image-work/default-stat-color/*.original.cs에 보존했다.

Windows Development Build 성공: 오류 0, 경고 8, 정상 종료 코드 0. Builds/UNKNOWN-DefaultStatText/UNKNOWN.exe와 폴더 전체를 사용한다. 복사본과 빌드 출력 DLL 해시 일치를 확인하고 BuildSummary에 기록했다. 기존 빌드는 유지했다.
