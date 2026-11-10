# 카드 이미지 교체

`Assets/CARD`의 일반 카드 52장, 조커 2장, UNKNOWN 3장을 `CardArtSettings` Resources 에셋으로 연결한다. 이미지 원본과 Import 설정은 변경하지 않는다.

| 카드 | 원본 경로 |
|---|---|
| 다이아 | CARD/DIA/diamond-{2~10,a,jack,queen,king}.png |
| 스페이드 | CARD/SPADE/spade-{2~10,a,jack,queen,king}.png |
| 하트 | CARD/HEART/heart-{2~10,a,jack,queen,king}.png |
| 클로버 | CARD/CLOVE/club-{2~10,a,jack,queen,king}.png |
| Joker_1 / Joker_2 | CARD/Joker/joker-red.png / joker-purple.png |
| UNKNOWN | CARD/UNKNOWN/unknown-{jack,queen,king}.png |

`CardArtLayout`은 카드의 입력/드래그 영역을 유지하면서 그 안에 그림을 비율대로 맞춘다. 빈 칸에 기존 CardDefinition의 마나/능력 설명과 CardInstance의 공격력/현재 체력을 Text로 표시한다. 카드에 이미 그려진 이름과 중복되는 예전 계급·문양 텍스트는 숨긴다. 버프/피해/맵/족보에 따른 수치 갱신은 유지하고, 공격력·체력의 글씨는 증감 여부와 관계없이 기본 색으로 고정한다. 손패·필드·드래그 복제·소환/공격 복제·UNKNOWN 조합 결과·규칙상 공개된 상대 카드에 사용한다. 비공개 상대 손패와 덱은 기존 뒷면을 유지한다.

추가 색상 테두리와 공격 준비/족보 테두리는 카드 이미지에서 표시하지 않는다. 원본 PNG에 포함된 금색 프레임은 유지한다. 숫자와 설명은 카드 크기에 따라 자동 축소하며 이미지가 없을 때는 기존 텍스트 카드 표시를 사용한다.

별도 설정은 완료되어 있다. 다시 연결하려면 `UNKNOWN > Cards > Connect Supplied Card Images`를 사용한다. 배포에는 새 Player 빌드가 필요하다. Unity에서는 Play를 종료하고 Assets > Refresh 후 SampleScene을 실행한다.

검사 실행 메서드: `Unknown.Multiplayer.Editor.CardArtSetup.RunBatch`. 실제 검사 결과와 미검증 범위는 `Validation/CardArtwork`를 참고한다.

Windows Development Build: `Builds/UNKNOWN-CardOnlyHover/UNKNOWN.exe`. 실행 파일을 분리하지 말고 폴더 전체를 사용한다. 기존 맵/타이틀/하위 메뉴 배경 변경도 포함한다.

카드 체력은 현재 값만 한 숫자로 표시한다. 최대 체력과 회복 상한 계산은 유지한다. CardBadgeCenter는 폰트 여백 대신 실제 숫자 메시를 기준으로 마나·공격력·체력을 배지 중앙에 정렬한다. 조커의 마나 배지는 해당 이미지의 위치를 별도로 사용한다.

하단 숫자 배치는 원본의 공격력·체력 배지 안쪽으로 조정했다. 조커 50%는 설명 아래 별도 내부 영역에 작게 표시한다. 숫자 메시가 지정 영역을 넘으면 비율을 유지해 축소하고, 손패 재배치 프레임의 최종 레이아웃도 갱신한다. 검증 기록은 Validation/CardBadgeContainment를 참고한다.

카드에 마우스를 올리면 약 2배 크기의 카드만 표시한다. 별도 설명 패널과 수치 변화 내역은 표시하지 않는다. 미리보기는 입력을 받지 않으며 마우스를 떼거나 소환/공격 드래그를 시작하면 닫힌다. 공개된 상대 필드 카드도 볼 수 있고, 비공개 손패는 기존 뒷면을 유지한다. 최신 검증 결과는 Validation/CardOnlyHover를 참고한다.
