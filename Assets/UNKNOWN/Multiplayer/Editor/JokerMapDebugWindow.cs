using UnityEditor;
using UnityEngine;
using Unknown.Battle;
namespace Unknown.Multiplayer.Editor
{
    public sealed class JokerMapDebugWindow : EditorWindow
    {
        JokerEventKind selected=JokerEventKind.Strengthen;
        BattleMap selectedMap=BattleMap.Diamond;
        [MenuItem("UNKNOWN/Joker Map/Local Debug")]
        static void Open() { GetWindow<JokerMapDebugWindow>("Joker Map Debug"); }
        [MenuItem("UNKNOWN/Joker Map/Run All Map Checks")]
        static void RunChecks() { AttackCheckMenu.RunSuiteBatch(); }
        void OnGUI()
        {
            var ui=Object.FindFirstObjectByType<BattleUIController>();
            EditorGUILayout.LabelField(ui==null ? "로컬 전투를 실행하세요." : ui.DebugMapStatus,EditorStyles.wordWrappedLabel);
            using(new EditorGUI.DisabledScope(!Application.isPlaying || ui==null || !ui.CanLocalMapDebug))
            {
                selectedMap=(BattleMap)EditorGUILayout.EnumPopup("테스트 맵",selectedMap);
                if(GUILayout.Button("선택 맵 새 게임"))ui.DebugMapNewGame(selectedMap);
                if(GUILayout.Button("문양 맵 턴 효과 강제 적용"))ui.DebugSuitTurn();
                if(GUILayout.Button("같은 문양 2 카드 소환 테스트"))ui.DebugSuitSummon();
                if(GUILayout.Button("일반 전장 새 게임"))ui.DebugMapNewGame(BattleMap.Normal);
                if(GUILayout.Button("조커의 유희 새 게임"))ui.DebugMapNewGame(BattleMap.JokerGame);
                selected=(JokerEventKind)EditorGUILayout.EnumPopup("사건",selected);
                if(selected!=JokerEventKind.None && GUILayout.Button("선택 사건 적용"))ui.DebugMapEvent(selected);
                if(GUILayout.Button("JOKER 지급"))ui.DebugGiveJoker();
                if(GUILayout.Button("다음 도박 성공"))ui.DebugGambleOutcome(1);
                if(GUILayout.Button("다음 도박 실패"))ui.DebugGambleOutcome(0);
                if(GUILayout.Button("확률 판정 복원"))ui.DebugGambleOutcome(-1);
                if(GUILayout.Button("현재 맵 승리 조건 테스트"))ui.DebugMapVictory();
            }
        }
    }
}
