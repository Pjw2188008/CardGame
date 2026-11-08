# 손패 클릭 즉시 버리기

버리기 버튼을 누른 뒤 실제 손패 Button의 onClick을 한 번 호출하면 즉시 Discard 명령을 제출한다. 별도 확인 클릭은 제거했다. 같은 버튼 재클릭 또는 ESC는 손패 클릭 전 모드를 취소한다. 온라인도 기존 Dispatch → Host 판정을 이용하며 턴당 1회와 마나 무료 규칙은 유지한다.

Unity 6000.0.74f1 격리 PlayMode: Hand/settings 37개 통과, 실패 0, 실행 예외 0. 실제 손패 버튼 클릭, 연속 클릭 방어, 취소, 마지막 카드 폐기, 턴당 1회 제한을 포함한다. 보고서는 DiscardClick-Checks.json. 현재 변경의 Windows EXE 재빌드와 두 EXE 온라인 재검증은 수행하지 않았다.
