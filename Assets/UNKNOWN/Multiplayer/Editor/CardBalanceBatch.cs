using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unknown.Battle;
namespace Unknown.Multiplayer.Editor
{
    [InitializeOnLoad]
    public static class CardBalanceBatch
    {

        static BalanceTestRun overhaulRun;
        const string OverhaulFolder="Library/SpadeWeakness";
        [Serializable]sealed class ExpectedCard {public string id,description;public int mana,attack,hp;}
        [Serializable]sealed class ExpectedCards {public ExpectedCard[] cards;}
        [Serializable]sealed class OverhaulStatus {public string state,path,error;public int attempted,completed,unresolved,errors;public string[] checks;}
        static CardBalanceBatch(){EditorApplication.update+=WatchOverhaul;}
        static void WriteOverhaul(string state,string error="",string[] checks=null){
            Directory.CreateDirectory(OverhaulFolder);
            File.WriteAllText(OverhaulFolder+"/status.json",JsonUtility.ToJson(new OverhaulStatus{state=state,path=overhaulRun?.DirectoryPath ?? "",error=error,
                attempted=overhaulRun?.Matches.Count ?? 0,completed=overhaulRun?.ValidMatches ?? 0,
                unresolved=overhaulRun?.Matches.Count(m=>m.status=="unresolved") ?? 0,errors=overhaulRun?.Matches.Count(m=>m.status=="error") ?? 0,checks=checks},true));
        }
        static void WatchOverhaul(){
            if(EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)return;
            string request=OverhaulFolder+"/request";
            if(overhaulRun==null && File.Exists(request)){
                File.Delete(request);
                try {
                    var expected=JsonUtility.FromJson<ExpectedCards>(File.ReadAllText(OverhaulFolder+"/expected.json"));
                    if(expected.cards.Length!=52)throw new Exception("Expected exactly 52 cards");
                    foreach(var e in expected.cards){var c=CardDatabase.Get(e.id);
                        if(c.ManaCost!=e.mana || c.Attack!=e.attack || c.MaxHP!=e.hp || c.Description!=e.description)throw new Exception("Card definition mismatch: "+e.id);}
                    var original=JsonUtility.FromJson<BalanceMetadata>(File.ReadAllText(OverhaulFolder+"/baseline.json"));
                    foreach(var d in original.cards){
                        var c=CardDatabase.Get(d.cardID);
                        if(c.Suit!=Suit.Spade || c.CardType!=CardType.Normal){if(JsonUtility.ToJson(d)!=JsonUtility.ToJson(BalanceDefinition.From(c)))throw new Exception("Unrelated card changed: "+c.CardID);}
                        else if(d.mana!=c.ManaCost || d.attack!=c.Attack || d.hp!=c.MaxHP)throw new Exception("Spade base stats changed: "+c.CardID);
                    }
                    var checks=BalanceVerification.VerifyCurrentCards().Concat(BalanceVerification.VerifyTimedSpade()).ToArray();
                    File.WriteAllText(OverhaulFolder+"/effect_checks.json",JsonUtility.ToJson(new OverhaulStatus{state="passed",checks=checks},true));
                    if(File.Exists(OverhaulFolder+"/started.json"))throw new Exception("This validation run was already started; refusing a duplicate run");
                    var schedule=new List<BalanceScheduledMatch>();
                    // The second block supplies replacement attempts only for missing valid map/first-seat quotas.
                    for(int block=0;block<2;block++)for(int index=0;index<30;index++)for(int map=0;map<6;map++){
                        int sample=block*30+index,seed=unchecked(17112026+sample*104729);
                        schedule.Add(new BalanceScheduledMatch{conditionID="spade-duration-"+sample,swapID="seat-"+(index%2),map=(BattleMap)map,firstPlayer=index%2,
                            first=(BalanceAI)((index/2)%4),second=(BalanceAI)((index/8)%4),combatSeed=seed,
                            firstDeckSeed=unchecked(seed^0x173142),secondDeckSeed=unchecked(seed^0x483471),firstPolicySeed=unchecked(seed^0x178354),secondPolicySeed=unchecked(seed^0x382917)});
                    }
                    overhaulRun=new BalanceTestRun(new BalanceTestSettings{matches=schedule.Count,completedPerMap=30,maxTurns=500,maxActions=20000,detailedLog=false},
                        Path.GetFullPath("spade_weakness_test/20261117-current"),new BattleConfig{EnablePoker=true,MapRules=BattleMapSettings.LoadRules(),MapEvents=JokerMapSettings.LoadRules()},schedule.ToArray());
                    File.WriteAllText(OverhaulFolder+"/started.json",JsonUtility.ToJson(new OverhaulStatus{state="started",path=overhaulRun.DirectoryPath},true));
                    WriteOverhaul("running",checks:checks);
                }catch(Exception e){WriteOverhaul("failed",e.ToString());Debug.LogException(e);}
            }
            if(overhaulRun==null || overhaulRun.Finished)return;
            try{
                overhaulRun.Tick(25);
                if(overhaulRun.Matches.Count>0 && overhaulRun.Matches.Last().status=="error"){
                    string failure=overhaulRun.Matches.Last().error;overhaulRun.Stop();WriteOverhaul("failed",failure);return;
                }
                if(overhaulRun.Finished){WriteOverhaul(overhaulRun.ValidMatches==180 ? "completed" : "incomplete");File.WriteAllText("spade_weakness_test/latest_result.txt",overhaulRun.DirectoryPath);}
                else if(EditorApplication.timeSinceStartup%2<.05)WriteOverhaul("running");
            }catch(Exception e){WriteOverhaul("failed",e.ToString());Debug.LogException(e);}
        }

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
