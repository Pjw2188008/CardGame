using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unknown.Battle;
namespace Unknown.Multiplayer.Editor
{
    public sealed class CardBalanceTesterWindow : EditorWindow
    {
        BalanceTestSettings settings=new BalanceTestSettings();
        BalanceTestRun run;
        string filter="";
        int suit=5,sort,scope;
        Vector2 scroll;
        string lastFolder="";
        [MenuItem("Tools/UNKNOWN/Card Balance Tester")]
        public static void Open(){GetWindow<CardBalanceTesterWindow>("Card Balance Tester");}
        void OnEnable(){EditorApplication.update+=UpdateRun;AssemblyReloadEvents.beforeAssemblyReload+=Stop;}
        void OnDisable(){EditorApplication.update-=UpdateRun;AssemblyReloadEvents.beforeAssemblyReload-=Stop;Stop();}
        void Stop(){if(run!=null && !run.Finished){run.Stop();lastFolder=run.DirectoryPath;}}
        void UpdateRun()
        {
            if(run==null || run.Finished)return;
            try {run.Tick();lastFolder=run.DirectoryPath;Repaint();}
            catch(Exception error){Debug.LogException(error);Stop();}
        }
        void OnGUI()
        {
            bool running=run!=null && !run.Finished;
            using(new EditorGUI.DisabledScope(running)) {
                settings.matches=EditorGUILayout.IntField("Matches",settings.matches);
                settings.seed=EditorGUILayout.IntField("Random Seed",settings.seed);
                settings.detailedLog=EditorGUILayout.Toggle("Detailed Event Log",settings.detailedLog);
                settings.maxTurns=EditorGUILayout.IntField("Safety Turn Limit",settings.maxTurns);
                settings.maxActions=EditorGUILayout.IntField("Safety Action Limit",settings.maxActions);
                EditorGUILayout.LabelField("AI", "Random / Aggressive / Control / Effect");
                EditorGUILayout.LabelField("Maps", "All 6 maps, cyclic, poker disabled");
                if(GUILayout.Button("Start Test")) {
                    try {run=new BalanceTestRun(settings,Path.GetFullPath("BalanceResults"));lastFolder=run.DirectoryPath;}
                    catch(Exception error){Debug.LogException(error);}
                }
            }
            using(new EditorGUI.DisabledScope(!running))if(GUILayout.Button("Stop Test"))Stop();
            using(new EditorGUI.DisabledScope(!Directory.Exists(lastFolder)))if(GUILayout.Button("Open Results Folder"))EditorUtility.RevealInFinder(lastFolder);
            if(run==null)return;
            EditorGUILayout.LabelField(run.State+"  "+run.Matches.Count+" / "+run.Settings.matches+"  Valid "+run.ValidMatches+"  Turn "+run.CurrentTurn);
            var progress=GUILayoutUtility.GetRect(10,18);EditorGUI.ProgressBar(progress,(float)run.Matches.Count/run.Settings.matches,"Matches");
            filter=EditorGUILayout.TextField("Name / ID",filter);
            suit=EditorGUILayout.Popup("Suit",suit,new[]{"Diamond","Spade","Heart","Club","None","All"});
            sort=EditorGUILayout.Popup("Sort",sort,new[]{"Name","Suit","Mana","Uses","Usage Win Rate","Average Damage","Survival"});
            scope=EditorGUILayout.Popup("Rows",scope,new[]{"All","Map","AI"});
            string selectedScope=scope==0 ? "all" : scope==1 ? "map" : "ai";
            var rows=run.Cards.Values.Where(r=>r.scope==selectedScope && (suit==5 || (int)CardDatabase.Get(r.cardID).Suit==suit)
                && (r.cardID.IndexOf(filter,StringComparison.OrdinalIgnoreCase)>=0 || CardDatabase.Get(r.cardID).CardName.IndexOf(filter,StringComparison.OrdinalIgnoreCase)>=0));
            switch(sort) {
                case 1:rows=rows.OrderBy(r=>CardDatabase.Get(r.cardID).Suit);break;
                case 2:rows=rows.OrderBy(r=>CardDatabase.Get(r.cardID).ManaCost);break;
                case 3:rows=rows.OrderByDescending(r=>r.uses);break;
                case 4:rows=rows.OrderByDescending(r=>r.UsageWinRate);break;
                case 5:rows=rows.OrderByDescending(r=>r.AverageDamage);break;
                case 6:rows=rows.OrderByDescending(r=>r.AverageSurvival);break;
                default:rows=rows.OrderBy(r=>r.cardID,StringComparer.Ordinal);break;
            }
            EditorGUILayout.HelpBox("사용 승률은 사용한 플레이어 기준입니다. 생존은 양측의 턴을 합산하며 종료 시 생존 카드도 포함합니다. 전체 200회·맵별 30회 미만 사용은 표본 부족으로 표시합니다.",MessageType.Info);
            scroll=EditorGUILayout.BeginScrollView(scroll);
            foreach(var row in rows) {
                var definition=CardDatabase.Get(row.cardID);
                EditorGUILayout.LabelField(definition.CardName+" / "+row.cardID+" / "+row.group+" / "+definition.ManaCost+" MP",EditorStyles.boldLabel);
                EditorGUILayout.LabelField("사용 "+row.uses+" · 경기 "+row.matchesUsed+" · 승률 "+row.UsageWinRate.ToString("P1")+" · 평균 피해 "+row.AverageDamage.ToString("F2")+" · 생존 "+row.AverageSurvival.ToString("F2"));
                EditorGUILayout.LabelField("효과 "+row.effectActivations+" · 피해/마나 "+(row.manaPaid==0 ? "N/A" : row.DamagePerMana.ToString("F2"))+" · 회복 "+row.healing+" · 드로우 "+row.draws+(row.uses<(row.scope=="map" ? 30 : 200) || row.matchesUsed<10 ? " · 표본 부족" : ""));
            }
            if(run.Finished) {
                EditorGUILayout.LabelField("Balance Review Candidates",EditorStyles.boldLabel);
                foreach(var candidate in BalanceReport.Candidates(run))EditorGUILayout.LabelField(candidate.cardID+" · "+candidate.category+" · "+candidate.metric+" · n="+candidate.sample);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
