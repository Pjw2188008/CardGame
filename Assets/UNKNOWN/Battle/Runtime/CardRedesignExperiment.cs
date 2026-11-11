#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace Unknown.Battle
{
    [Serializable]public sealed class RedesignCohort
    {
        public string name,directory;
        public int attempted,completed,unresolved,errors,firstWins,sameAICompleted,sameAIFirstWins;
        public double firstWinRate,sameAIFirstWinRate,sameAILow,sameAIHigh,meanTurns,unknownFirstSummonMedian,unknownVictoryShare;
        public string[] maps;
        public double[] mapMeanTurns;
        public BalanceMatchResult[] matches;
    }
    [Serializable]public sealed class RedesignExperimentSummary
    {
        public string schemaVersion="2.0",status="completed",design="First redesign proposal, not a validated optimum";
        public int seed,attempted,errors,unresolved,initialConditionCount=240,stressConditionCount=20,pairedRngDivergences;
        public RedesignCohort[] cohorts;
        public string[] alerts;
        public string[] interpretation={"Only initial deck order, initial hand identity, map and AI identity are paired. Different action/effect paths consume combat RNG differently.","Stats include completed matches only. Unresolved match event streams remain separately identifiable by cohort and match ID.","Usage win rate is per player-game exposure, not causal card strength. Per-card 200 uses and per-map 30 uses are sampling warnings.","First-player target 45-55%, unfinished <=1, overall mean-turn change +/-10%, per-map +/-15%, UNKNOWN median -1 turn or victory share +5 percentage points are exploratory alarms.","Confidence intervals are Wilson binomial 95% descriptive intervals. Paired/swap observations are dependent, so these intervals are not a complete paired causal analysis.","These 1000 games explore integration risks, not final equilibrium for 52 normal cards."};
    }
    public sealed class CardRedesignExperiment : IDisposable
    {
        readonly int seed;
        readonly string[] names={"baseline_main","revised_main","baseline_stress","revised_stress"};
        readonly BalanceScheduledMatch[] main,stress;
        readonly List<RedesignCohort> cohorts=new List<RedesignCohort>();
        BalanceTestRun active;
        int phase;
        public string DirectoryPath {get;}
        public bool Finished {get;private set;}
        public string Status {get;private set;}="running";
        public int Completed => cohorts.Sum(c=>c.attempted)+(active?.Matches.Count ?? 0);
        public string Progress => Finished ? "Completed" : names[phase]+" / "+(active?.CurrentTurn ?? 0);
        public CardRedesignExperiment(int seed,string parent)
        {
            this.seed=seed;DirectoryPath=Path.Combine(parent,"redesign-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));Directory.CreateDirectory(DirectoryPath);
            main=MainSchedule(seed);stress=StressSchedule(seed);StartPhase();
        }
        static BalanceScheduledMatch Leg(string id,BattleMap map,BalanceAI a,BalanceAI b,int seed,bool swap)
        {
            return new BalanceScheduledMatch{conditionID=id,swapID=swap ? "BA" : "AB",map=map,first=swap ? b : a,second=swap ? a : b,combatSeed=seed,
                firstDeckSeed=seed^(swap ? 0x273841 : 0x374839),secondDeckSeed=seed^(swap ? 0x374839 : 0x273841),
                firstPolicySeed=seed^(swap ? 0x174832 : 0x184392),secondPolicySeed=seed^(swap ? 0x184392 : 0x174832)};
        }
        public static BalanceScheduledMatch[] MainSchedule(int seed)
        {
            var schedule=new List<BalanceScheduledMatch>();int condition=0;
            var maps=new[]{BattleMap.Normal,BattleMap.Diamond,BattleMap.Spade,BattleMap.Heart,BattleMap.Club,BattleMap.JokerGame};
            var styles=new[]{BalanceAI.Aggressive,BalanceAI.Control,BalanceAI.Effect};
            for(int map=0;map<maps.Length;map++) {
                var pairs=new List<BalanceAI[]>();
                foreach(var style in styles)for(int i=0;i<4;i++)pairs.Add(new[]{style,style});
                for(int a=0;a<3;a++)for(int b=a+1;b<3;b++)for(int i=0;i<9;i++)pairs.Add(new[]{styles[a],styles[b]});
                var extras=new[]{new[]{styles[0],styles[1]},new[]{styles[0],styles[2]},new[]{styles[1],styles[2]}};pairs.Add(extras[map%3]);
                foreach(var pair in pairs) {
                    int matchSeed=unchecked(seed+condition*104729);string id="main-"+condition.ToString("D3");condition++;
                    schedule.Add(Leg(id,maps[map],pair[0],pair[1],matchSeed,false));schedule.Add(Leg(id,maps[map],pair[0],pair[1],matchSeed,true));
                }
            }
            return schedule.ToArray();
        }
        public static BalanceScheduledMatch[] StressSchedule(int seed)
        {
            var styles=new[]{BalanceAI.Aggressive,BalanceAI.Control,BalanceAI.Effect};
            return Enumerable.Range(0,20).Select(i=>Leg("stress-"+i.ToString("D2"),(BattleMap)(i%6),BalanceAI.Random,i<10 ? BalanceAI.Random : styles[(i-10)%3],unchecked(seed+20000003+i*104729),false)).ToArray();
        }
        void StartPhase()
        {
            bool baseline=phase==0 || phase==2;
            active=new BalanceTestRun(new BalanceTestSettings{detailedLog=true,seed=seed},Path.Combine(DirectoryPath,names[phase]),new BattleConfig{EnablePoker=false,CardVersion=baseline ? CardBalanceVersion.Baseline : CardBalanceVersion.Revised,MapRules=BattleMapSettings.LoadRules(),MapEvents=JokerMapSettings.LoadRules()},phase<2 ? main : stress);
        }
        public void Tick(int budget=20)
        {
            if(Finished)return;
            active.Tick(budget);if(!active.Finished)return;
            cohorts.Add(Summarize(active,names[phase]));active.Dispose();active=null;phase++;
            if(phase<4)StartPhase();else {Finished=true;Status="completed";Export();}
        }
        static double Median(IEnumerable<int> source)
        {
            var values=source.Where(x=>x>=0).OrderBy(x=>x).ToArray();return values.Length==0 ? -1 : values.Length%2==1 ? values[values.Length/2] : (values[values.Length/2-1]+values[values.Length/2])/2.0;
        }
        static void Wilson(int wins,int n,out double low,out double high)
        {
            if(n==0){low=0;high=1;return;}const double z=1.95996398454005;double p=(double)wins/n,den=1+z*z/n;
            double center=(p+z*z/(2*n))/den,margin=z*Math.Sqrt(p*(1-p)/n+z*z/(4*n*n))/den;low=center-margin;high=center+margin;
        }
        static RedesignCohort Summarize(BalanceTestRun run,string name)
        {
            var complete=run.Matches.Where(m=>m.status=="completed").ToArray();var same=complete.Where(m=>m.firstAI==m.secondAI).ToArray();
            var result=new RedesignCohort{name=name,directory=run.DirectoryPath,attempted=run.Matches.Count,completed=complete.Length,unresolved=run.Matches.Count(m=>m.status=="unresolved"),errors=run.Matches.Count(m=>m.status=="error"),firstWins=complete.Count(m=>m.winner==0),sameAICompleted=same.Length,sameAIFirstWins=same.Count(m=>m.winner==0),meanTurns=complete.Length==0 ? 0 : complete.Average(m=>m.turns),matches=run.Matches.ToArray(),maps=Enum.GetNames(typeof(BattleMap)),unknownFirstSummonMedian=Median(complete.SelectMany(m=>new[]{m.firstUnknownSummonTurn,m.secondUnknownSummonTurn})),unknownVictoryShare=complete.Length==0 ? 0 : (double)complete.Count(m=>m.reason==VictoryReason.UnknownComplete.ToString())/complete.Length};
            result.firstWinRate=complete.Length==0 ? 0 : (double)result.firstWins/complete.Length;result.sameAIFirstWinRate=same.Length==0 ? 0 : (double)result.sameAIFirstWins/same.Length;
            Wilson(result.sameAIFirstWins,same.Length,out result.sameAILow,out result.sameAIHigh);
            result.mapMeanTurns=result.maps.Select(map=>{var group=complete.Where(m=>m.map==map).ToArray();return group.Length==0 ? 0 : group.Average(m=>m.turns);}).ToArray();return result;
        }
        void Export()
        {
            var alerts=new List<string>();var baseline=cohorts[0];var revised=cohorts[1];int divergences=0;
            for(int i=0;i<baseline.matches.Length;i++) {
                var a=baseline.matches[i];var b=revised.matches[i];
                if(a.conditionID!=b.conditionID || a.swapID!=b.swapID || a.initialFirstDeckHash!=b.initialFirstDeckHash || a.initialSecondDeckHash!=b.initialSecondDeckHash || a.firstPolicySeed!=b.firstPolicySeed || a.secondPolicySeed!=b.secondPolicySeed)throw new InvalidOperationException("Initial pairing differs at "+i);
                if(a.randomCalls!=b.randomCalls || a.randomFingerprint!=b.randomFingerprint)divergences++;
            }
            foreach(var cohort in cohorts) {
                if(cohort.errors>0)alerts.Add(cohort.name+" processing errors "+cohort.errors);
                if(cohort.unresolved>1)alerts.Add(cohort.name+" unresolved warning "+cohort.unresolved);
                if(cohort.sameAICompleted>0 && (cohort.sameAIFirstWinRate<.45 || cohort.sameAIFirstWinRate>.55))alerts.Add(cohort.name+" same-AI first-player rate outside 45-55%, n="+cohort.sameAICompleted);
            }
            if(baseline.meanTurns>0 && Math.Abs(revised.meanTurns/baseline.meanTurns-1)>.10)alerts.Add("Overall mean turns changed beyond +/-10%");
            for(int map=0;map<baseline.maps.Length;map++)if(baseline.mapMeanTurns[map]>0 && Math.Abs(revised.mapMeanTurns[map]/baseline.mapMeanTurns[map]-1)>.15)alerts.Add(baseline.maps[map]+" mean turns changed beyond +/-15%");
            if(baseline.unknownFirstSummonMedian>=0 && revised.unknownFirstSummonMedian>=0 && baseline.unknownFirstSummonMedian-revised.unknownFirstSummonMedian>=1)alerts.Add("UNKNOWN first summon median at least one global turn earlier");
            if(revised.unknownVictoryShare-baseline.unknownVictoryShare>=.05)alerts.Add("UNKNOWN victory share increased at least 5 percentage points");
            var summary=new RedesignExperimentSummary{seed=seed,cohorts=cohorts.ToArray(),attempted=cohorts.Sum(c=>c.attempted),errors=cohorts.Sum(c=>c.errors),unresolved=cohorts.Sum(c=>c.unresolved),alerts=alerts.ToArray(),pairedRngDivergences=divergences};
            File.WriteAllText(Path.Combine(DirectoryPath,"balance_summary.json"),JsonUtility.ToJson(summary,true));
            File.WriteAllText(Path.Combine(DirectoryPath,"test_metadata.json"),JsonUtility.ToJson(new ScheduleMetadata{seed=seed,main=main,stress=stress,cohorts=cohorts.ToArray(),versions=cohorts.Select(c=>{var m=JsonUtility.FromJson<BalanceMetadata>(File.ReadAllText(Path.Combine(c.directory,"test_metadata.json")));return new CohortVersion{cohort=c.name,cardDataSHA256=m.cardDataSHA256,ruleDataSHA256=m.ruleDataSHA256,mapDataSHA256=m.mapDataSHA256,aiCodeSHA256=m.aiCodeSHA256,rulesCodeSHA256=m.rulesCodeSHA256};}).ToArray()},true));
            foreach(string name in new[]{"card_statistics.csv","card_effect_statistics.csv","card_map_statistics.csv","card_ai_statistics.csv","match_results.csv"}) {
                using(var writer=new StreamWriter(Path.Combine(DirectoryPath,name),false,new System.Text.UTF8Encoding(true))) {
                    bool header=false;
                    foreach(var cohort in cohorts) {
                        var lines=File.ReadAllLines(Path.Combine(cohort.directory,name));
                        if(!header){writer.WriteLine("\"cohort\","+lines[0]);header=true;}
                        // Error fields can contain embedded line breaks. Preserve CSV records by parsing quotes.
                        foreach(string record in CsvRecords(File.ReadAllText(Path.Combine(cohort.directory,name))).Skip(1))writer.WriteLine("\""+cohort.name+"\","+record);
                    }
                }
            }
            var candidates=new List<string>();
            foreach(var cohort in cohorts)candidates.Add("{\"cohort\":\""+cohort.name+"\",\"result\":"+File.ReadAllText(Path.Combine(cohort.directory,"balance_candidates.json"))+"}");
            File.WriteAllText(Path.Combine(DirectoryPath,"balance_candidates.json"),"{\"schemaVersion\":\"2.0\",\"cohorts\":["+string.Join(",",candidates)+"]}");
            using(var writer=new StreamWriter(Path.Combine(DirectoryPath,"effect_events.jsonl")))foreach(var cohort in cohorts)
                foreach(string line in File.ReadLines(Path.Combine(cohort.directory,"effect_events.jsonl")))writer.WriteLine("{\"cohort\":\""+cohort.name+"\","+line.Substring(1));
        }
        static IEnumerable<string> CsvRecords(string text)
        {
            bool quoted=false;int start=0;
            for(int i=0;i<text.Length;i++) {
                if(text[i]=='"'){if(quoted && i+1<text.Length && text[i+1]=='"'){i++;continue;}quoted=!quoted;}
                if(text[i]=='\n' && !quoted){yield return text.Substring(start,i-start).TrimEnd('\r');start=i+1;}
            }
            if(start<text.Length)yield return text.Substring(start).TrimEnd('\r');
        }
        public void Dispose()
        {
            if(Finished)return;
            active?.Dispose();if(active!=null)cohorts.Add(Summarize(active,names[phase]));active=null;Finished=true;Status="stopped";
            File.WriteAllText(Path.Combine(DirectoryPath,"balance_summary.json"),JsonUtility.ToJson(new RedesignExperimentSummary{status="stopped",seed=seed,cohorts=cohorts.ToArray(),attempted=cohorts.Sum(c=>c.attempted),errors=cohorts.Sum(c=>c.errors),unresolved=cohorts.Sum(c=>c.unresolved)},true));
        }
        [Serializable]sealed class ScheduleMetadata {public string schemaVersion="2.0";public int seed;public BalanceScheduledMatch[] main,stress;public RedesignCohort[] cohorts;public CohortVersion[] versions;}
        [Serializable]sealed class CohortVersion {public string cohort,cardDataSHA256,ruleDataSHA256,mapDataSHA256,aiCodeSHA256,rulesCodeSHA256;}
    }
}
#endif
