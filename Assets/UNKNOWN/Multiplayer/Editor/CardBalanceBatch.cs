using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unknown.Battle;
namespace Unknown.Multiplayer.Editor
{
    public static class CardBalanceBatch
    {
        static BalanceTestRun run;
        static string first;
        static readonly List<string> passed=new List<string>();
        [Serializable]sealed class Result {public bool completed;public string results,replay,error;public string[] passed;public int valid,unresolved,errors;}
        public static void RunBatch()
        {
            try {
                first=null;passed.Clear();
                if(!EditorApplication.ExecuteMenuItem("Tools/UNKNOWN/Card Balance Tester"))throw new InvalidOperationException("Editor menu is unavailable");
                EditorWindow.GetWindow<CardBalanceTesterWindow>().Close();passed.Add("Editor tool opens from Tools > UNKNOWN > Card Balance Tester");
                passed.AddRange(BalanceVerification.Run(Path.GetFullPath("BalanceResults/VerificationFixtures")));
                run=new BalanceTestRun(new BalanceTestSettings{detailedLog=true},Path.GetFullPath("BalanceResults"));
                EditorApplication.update+=Tick;
            } catch(Exception error){Fail(error);}
        }
        static void Tick()
        {
            try {
                run.Tick(50);
                if(!run.Finished)return;
                if(first==null) {
                    first=run.DirectoryPath;passed.Add("Completed first 1000-match schedule");
                    run=new BalanceTestRun(new BalanceTestSettings{detailedLog=false},Path.GetFullPath("BalanceResults/Replay"));return;
                }
                foreach(string name in new[]{"card_statistics.csv","card_effect_statistics.csv","card_map_statistics.csv","card_ai_statistics.csv","match_results.csv","balance_summary.json","balance_candidates.json"}) {
                    if(File.ReadAllText(Path.Combine(first,name))!=File.ReadAllText(Path.Combine(run.DirectoryPath,name)))throw new InvalidOperationException("Deterministic replay differs: "+name);
                    passed.Add("Replayed 1000 matches identically: "+name);
                }
                if(run.Matches.Count!=1000 || run.Matches.Any(m=>m.status=="error"))throw new InvalidOperationException("Incomplete or erroneous schedule");
                foreach(BattleMap map in Enum.GetValues(typeof(BattleMap)))if(run.Matches.Count(m=>m.map==map.ToString())<166)throw new InvalidOperationException("Missing map schedule");
                foreach(BalanceAI ai in Enum.GetValues(typeof(BalanceAI))) {
                    int a=run.Matches.Count(m=>m.firstAI==ai.ToString()),b=run.Matches.Count(m=>m.secondAI==ai.ToString());
                    if(Math.Abs(a-b)>1)throw new InvalidOperationException("Unbalanced AI seats");
                }
                passed.Add("All six maps and four policy profiles have balanced seat exposure");
                Write(new Result{completed=true,results=first,replay=run.DirectoryPath,passed=passed.ToArray(),valid=run.ValidMatches,unresolved=run.Matches.Count(m=>m.status=="unresolved"),errors=run.Matches.Count(m=>m.status=="error")});
                EditorApplication.update-=Tick;EditorApplication.Exit(0);
            } catch(Exception error){Fail(error);}
        }
        static void Write(Result result)
        {Directory.CreateDirectory("Library");File.WriteAllText("Library/UNKNOWN-CardBalance-Verification.json",JsonUtility.ToJson(result,true));}
        static void Fail(Exception error)
        {
            EditorApplication.update-=Tick;Debug.LogException(error);
            Write(new Result{completed=false,results=first,replay=run?.DirectoryPath,passed=passed.ToArray(),error=error.ToString()});EditorApplication.Exit(1);
        }
    }
}
