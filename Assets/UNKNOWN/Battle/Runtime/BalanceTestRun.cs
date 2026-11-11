#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
namespace Unknown.Battle
{
    [Serializable]
    public sealed class BalanceTestSettings
    {
        public int matches=1000,seed=7312026,maxTurns=1000,maxActions=20000;
        public bool detailedLog;
    }
    public sealed class BalanceTestRun : IDisposable
    {
        public readonly BalanceTestSettings Settings;
        public readonly string DirectoryPath;
        public readonly List<BalanceMatchResult> Matches=new List<BalanceMatchResult>();
        public readonly Dictionary<string,BalanceCardStats> Cards=new Dictionary<string,BalanceCardStats>();
        public readonly Dictionary<string,BalanceEffectStats> Effects=new Dictionary<string,BalanceEffectStats>();
        public readonly BattleConfig Rules;
        public bool Finished {get;private set;}
        public string State {get;private set;}="Ready";
        public int ValidMatches => Matches.Count(m=>m.status=="completed");
        public int CurrentTurn => engine?.State.TurnNumber ?? 0;
        BattleEngine engine;
        BalanceMatchCollector collector;
        BalancePolicy[] policies;
        BalanceMatchResult current;
        StreamWriter log;
        readonly BalanceScheduledMatch[] schedule;
        public BalanceTestRun(BalanceTestSettings settings,string parentDirectory,BattleConfig rules=null,BalanceScheduledMatch[] schedule=null)
        {
            Settings=JsonUtility.FromJson<BalanceTestSettings>(JsonUtility.ToJson(settings));
            this.schedule=schedule;if(schedule!=null)Settings.matches=schedule.Length;
            if(Settings.matches<1 || Settings.matches>1000000 || Settings.maxTurns<1 || Settings.maxActions<1)throw new ArgumentOutOfRangeException(nameof(settings));
            Rules=(rules ?? new BattleConfig{MapRules=BattleMapSettings.LoadRules(),MapEvents=JokerMapSettings.LoadRules()}).Copy();Rules.EnablePoker=false;
            DirectoryPath=Path.Combine(parentDirectory,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));
            Directory.CreateDirectory(DirectoryPath);
            foreach(var card in CardDatabase.AllFor(Rules.CardVersion)) {
                Cell(card.CardID,"all","all");
                foreach(BattleMap map in Enum.GetValues(typeof(BattleMap)))Cell(card.CardID,"map",map.ToString());
                foreach(BalanceAI ai in Enum.GetValues(typeof(BalanceAI)))Cell(card.CardID,"ai",ai.ToString());
                for(int i=0;i<card.EffectList.Count;i++) {
                    var effect=card.EffectList[i];Effects.Add(card.CardID+"|"+i,new BalanceEffectStats{cardID=card.CardID,effectIndex=i,kind=effect.Kind.ToString(),target=effect.Target.ToString(),unit=effect.Kind==EffectKind.DrawCard || effect.Kind==EffectKind.DiscardCard ? "cards" : "stat_points"});
                }
                if(card.CardType==CardType.Joker)Effects.Add(card.CardID+"|-1",new BalanceEffectStats{cardID=card.CardID,effectIndex=-1,kind="Joker",target="Ally",unit="outcomes"});
            }
            if(Settings.detailedLog)log=new StreamWriter(Path.Combine(DirectoryPath,"effect_events.jsonl"),false,new System.Text.UTF8Encoding(false));
        }
        BalanceCardStats Cell(string id,string scope,string group)
        {
            string key=id+"|"+scope+"|"+group;
            if(!Cards.TryGetValue(key,out var cell)){cell=new BalanceCardStats{cardID=id,scope=scope,group=group,version=Rules.CardVersion};Cards.Add(key,cell);}return cell;
        }
        void BeginMatch()
        {
            int index=Matches.Count,pair=index/6%16;
            if(schedule!=null) {BeginScheduledMatch(schedule[index],index);return;}
            int first=pair/4,second=pair%4;
            if(index%2==1){int swap=first;first=second;second=swap;}
            current=new BalanceMatchResult{match=index,seed=unchecked(Settings.seed+index*104729),map=((BattleMap)(index%6)).ToString(),firstAI=((BalanceAI)first).ToString(),secondAI=((BalanceAI)second).ToString(),status="running"};
            var config=Rules.Copy();config.Map=(BattleMap)(index%6);
            engine=new BattleEngine(current.seed,config);
            policies=new[]{new BalancePolicy((BalanceAI)first,unchecked(current.seed^0x178354)),new BalancePolicy((BalanceAI)second,unchecked(current.seed^0x382917))};
            collector=new BalanceMatchCollector(engine,index,Settings.detailedLog ? (Action<BalanceDetail>)(entry=>log.WriteLine(JsonUtility.ToJson(entry))) : null);
        }
        public void Tick(int workMilliseconds=10)
        {
            if(Finished)return;
            State="Running";var watch=Stopwatch.StartNew();
            do {
                try {
                    if(engine==null)BeginMatch();
                    if(engine.State.IsOver){FinishMatch("completed",null);continue;}
                    if(engine.State.TurnNumber>Settings.maxTurns || current.actions>=Settings.maxActions){FinishMatch("unresolved","Safety limit reached. No winner assigned.");continue;}
                    if(IsIrreversibleExhaustion(engine)){FinishMatch("unresolved","Irreversible exhaustion: no deck, no summonable hand and no future damaging unit. Existing no-fatigue rules preserved.");continue;}
                    int actor=engine.State.ActivePlayer;
                    var observation=AiObservation.Capture(engine,actor);var moves=BalancePolicy.LegalMoves(observation);
                    collector.Opportunities(moves);
                    var move=policies[actor].Choose(observation,moves);collector.Command=move;
                    if(!move.Execute(engine,actor))throw new InvalidOperationException(move.Action+": "+engine.State.LastError);
                    collector.EndCommand();current.actions++;
                    TrackUnknownTiming();
                    if(engine.State.IsOver)FinishMatch("completed",null);
                } catch(Exception error) {FinishMatch("error",error.ToString());}
            } while(!Finished && watch.ElapsedMilliseconds<Math.Max(1,workMilliseconds));
        }
        void FinishMatch(string status,string error)
        {
            if(current==null)throw new InvalidOperationException("Match initialization failed.");
            current.status=status;current.error=error ?? "";current.turns=engine?.State.TurnNumber ?? 0;
            current.version=Rules.CardVersion.ToString();current.randomCalls=engine?.RandomCalls ?? 0;current.randomFingerprint=engine?.RandomFingerprint ?? 0;
            if(policies!=null){current.firstPolicyRandomCalls=policies[0].RandomCalls;current.secondPolicyRandomCalls=policies[1].RandomCalls;current.firstPolicyRandomFingerprint=policies[0].RandomFingerprint;current.secondPolicyRandomFingerprint=policies[1].RandomFingerprint;}
            if(engine!=null) {
                var first=engine.State.Players[0];var second=engine.State.Players[1];
                current.firstHP=first.HP;current.secondHP=second.HP;current.firstDeck=first.DeckCount;current.secondDeck=second.DeckCount;
                current.firstHand=first.Hand.Count;current.secondHand=second.Hand.Count;
                current.firstHandIDs=string.Join("|",first.Hand.Select(c=>c.Definition.CardID));current.secondHandIDs=string.Join("|",second.Hand.Select(c=>c.Definition.CardID));
                current.firstBoard=string.Join("|",first.Board.Where(c=>c!=null).Select(c=>c.Definition.CardID+"/"+c.Attack+"/"+c.CurrentHP));
                current.secondBoard=string.Join("|",second.Board.Where(c=>c!=null).Select(c=>c.Definition.CardID+"/"+c.Attack+"/"+c.CurrentHP));
            }
            current.firstUsed=collector?.Used(0) ?? new string[0];current.secondUsed=collector?.Used(1) ?? new string[0];
            if(status=="completed") {
                current.winner=engine.State.Winner;current.reason=engine.State.VictoryReason.ToString();collector.Complete();
                var seen=new HashSet<string>();
                foreach(var row in collector.Rows.Values) {
                    Merge(row,"all","all",seen);Merge(row,"map",current.map,seen);
                    Merge(row,"ai",row.group=="0" ? current.firstAI : current.secondAI,seen);
                }
                foreach(var entry in collector.Effects)Effects[entry.Key].Add(entry.Value);
            }
            if(engine!=null)engine.BalanceObserver=null;
            Matches.Add(current);engine=null;collector=null;current=null;log?.Flush();
            if(Matches.Count>=Settings.matches && status!="cancelled")Finish("Completed");
        }
        void Merge(BalanceCardStats row,string scope,string group,HashSet<string> seen)
        {
            var cell=Cell(row.cardID,scope,group);cell.Add(row);
            if(row.matchesUsed>0 && !seen.Add(row.cardID+"|"+scope+"|"+group))cell.matchesUsed--;
        }
        public void Stop()
        {
            if(Finished)return;
            if(current!=null)FinishMatch("cancelled","Stopped by user. Excluded from aggregate statistics.");
            if(!Finished)Finish("Stopped");
        }
        void Finish(string state)
        {
            Finished=true;State=state;log?.Dispose();log=null;
            BalanceReport.Save(this);
        }
        public void Dispose(){if(!Finished)Stop();log?.Dispose();}
        void BeginScheduledMatch(BalanceScheduledMatch entry,int index)
        {
            current=new BalanceMatchResult{match=index,conditionID=entry.conditionID,swapID=entry.swapID,seed=entry.combatSeed,map=entry.map.ToString(),firstAI=entry.first.ToString(),secondAI=entry.second.ToString(),firstDeckSeed=entry.firstDeckSeed,secondDeckSeed=entry.secondDeckSeed,firstPolicySeed=entry.firstPolicySeed,secondPolicySeed=entry.secondPolicySeed,status="running"};
            var config=Rules.Copy();config.Map=entry.map;config.InitialDeckSeeds=new[]{entry.firstDeckSeed,entry.secondDeckSeed};
            current.initialFirstDeckHash=InitialDeckHash(entry.firstDeckSeed);current.initialSecondDeckHash=InitialDeckHash(entry.secondDeckSeed);
            engine=new BattleEngine(entry.combatSeed,config);
            policies=new[]{new BalancePolicy(entry.first,entry.firstPolicySeed),new BalancePolicy(entry.second,entry.secondPolicySeed)};
            collector=new BalanceMatchCollector(engine,index,Settings.detailedLog ? (Action<BalanceDetail>)(e=>log.WriteLine(JsonUtility.ToJson(e))) : null);
        }
        string InitialDeckHash(int seed)
        {
            var ids=CardDatabase.DeckFor(Rules.CardVersion).Select(c=>c.CardID).ToArray();var random=new SeededRandom(seed);
            for(int i=ids.Length-1;i>0;i--){int j=random.Next(i+1);string swap=ids[i];ids[i]=ids[j];ids[j]=swap;}
            return BalanceReport.Hash(string.Join("|",ids));
        }
        internal static bool IsIrreversibleExhaustion(BattleEngine engine)
        {
            if(engine.NeedsTarget || engine.State.IsOver)return false;
            if(engine.State.Players.Any(p=>p.DeckCount>0 || p.Hand.Any(c=>c.Definition.CardType!=CardType.Joker)))return false;
            var units=engine.State.Players.SelectMany(p=>p.Board).Where(c=>c!=null).ToArray();
            if(units.Any(c=>c.Attack>0))return false;
            if(engine.Config.Map==BattleMap.JokerGame && units.Length>0)return false;
            return true;
        }
        void TrackUnknownTiming()
        {
            var a=engine.State.Players[0];var b=engine.State.Players[1];int turn=engine.State.TurnNumber;
            if(a.UnknownCreated>0 && current.firstUnknownCreationTurn<0)current.firstUnknownCreationTurn=turn;
            if(b.UnknownCreated>0 && current.secondUnknownCreationTurn<0)current.secondUnknownCreationTurn=turn;
            if(a.Board.Any(c=>c?.Definition.CardType==CardType.Unknown) && current.firstUnknownSummonTurn<0)current.firstUnknownSummonTurn=turn;
            if(b.Board.Any(c=>c?.Definition.CardType==CardType.Unknown) && current.secondUnknownSummonTurn<0)current.secondUnknownSummonTurn=turn;
            current.firstUnknownMax=Math.Max(current.firstUnknownMax,a.UnknownProgress);current.secondUnknownMax=Math.Max(current.secondUnknownMax,b.UnknownProgress);
        }
    }
    [Serializable]public sealed class BalanceScheduledMatch
    {
        public string conditionID,swapID;
        public BattleMap map;
        public BalanceAI first,second;
        public int combatSeed,firstDeckSeed,secondDeckSeed,firstPolicySeed,secondPolicySeed;
    }
}
#endif
