# 버리기 버튼 비활성 상태 잔존 수정

원인: Tutorial.TickTutorial에서 discardButton.interactable &= 단계 허용 여부로 설정했다. 앞 단계에서 false가 되면 RefreshHandTools가 true로 복원하지 않아 4단계까지 계속 비활성화됐다. 기존 onClick.Invoke 검사는 Button의 IsInteractable 검사를 우회하여 이를 발견하지 못했다.

수정: RefreshHandTools가 현재 입력 허용 여부, 소환 효과 대기, 턴당 사용 여부, 손패 유무로 interactable을 매번 다시 계산한다. 튜토리얼 단계 제한은 그 갱신 이후 적용한다.

검증: Unity 6000.0.74f1 PlayMode에서 타이틀 → 단계 1~7 전체 진행. 34개 통과, 실패 0, Error/Exception 0. 단계 4에서 IsInteractable, EventSystem.RaycastAll의 최상위 대상, IPointerDown/Up/Click 전달, 실제 손패 감소를 검증한다. 버튼 함수 직접 호출로 대체하지 않는다. 전체 결과는 DiscardPointer-Checks.json.

Windows EXE 재빌드 및 실제 두 PC 접속 재검증은 이번 수정에서 수행하지 않았다. 실행 중인 Editor Play는 스크립트 재컴파일 후 종료하고 다시 시작해야 새 런타임 UI 상태로 확인할 수 있다.
