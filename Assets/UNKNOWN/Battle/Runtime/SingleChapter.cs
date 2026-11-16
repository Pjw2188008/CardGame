using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Unknown.Battle
{
    public enum SingleNodeKind { Start, Battle, Elite, Augment, Heal, Purge, Event, Boss }
    public enum SingleRestriction { None, AttackLimit, StrongestPenalty, DirectLimit, SealFirstSummon, SealSlot, SealSuit, HalfHealing, EndHealLimit, SummonBlood, CancelExtraDraw, LessMana, FirstCost, Parity, Changing, SwapSlots }
    public enum SinglePatternKind { Strengthen, Summon, Weaken, Rally }
    [Serializable] public sealed class SingleUnit { public string card; public int slot; }
    [Serializable] public sealed class SinglePattern { public int turn,amount=1; public string condition="turn",card,watchCard;public int hpPercent=50; public SinglePatternKind kind; public int slot; }
    [Serializable] public sealed class SingleEnemyCard { public string card,condition=""; public int priority,minTurn=1,maxTurn,weight=1,preferredSlot=-1,maxUses; }
    public enum SingleBattleType { Normal, Rush, Bulwark, ManaLimited, CardLimited, Elite, Boss }
    [Serializable] public sealed class SingleTurnAdjustment { public int minLayer=1,maxLayer=10,reduction;public SingleBattleType[] types=new SingleBattleType[0]; }
    [Serializable] public sealed class SingleStage
    {
        public SingleBattleType battleType;public int minimumTurns=3;public int[] layerTurnLimits=new int[0];
        public string id,name,enemyType="";public SingleNodeKind kind;public BattleMap map;
        public int cost,enemyHP=20,turnLimit=8,startMana=3,manaRecovery=3,turnSpendLimit=7,manaBudget=30;
        public int attackLimit=2,strongestPenalty=2,directLimit=1,healingPercent=50,summonBlood=1,firstCost=1,manaReduction=1,sealedSlot=2;
        public SingleRestriction[] restrictions=new SingleRestriction[0];
        public int enemyStartMana=5,enemyManaRecovery=4,enemyManaCap=10,enemyBudget=40,enemySummonsPerTurn=1,enemyExtraTurn=3,enemyExtraHP=50;
        public SingleEnemyCard[] enemyPool=new SingleEnemyCard[0];
        public SingleUnit[] units=new SingleUnit[0];public SinglePattern[] patterns=new SinglePattern[0];
        public bool IsCombat=>kind==SingleNodeKind.Battle || kind==SingleNodeKind.Elite || kind==SingleNodeKind.Boss;
        // Layer is the actual generated node's zero-based layer, not the stage's ID/order.
        public int ResolveTurnLimit(SingleDifficulty difficulty,int layer=-1) {
            if(!IsCombat)return 0;
            int turns=layer>=0 && kind!=SingleNodeKind.Boss && layerTurnLimits!=null && layerTurnLimits.Length==3?layerTurnLimits[layer<=2?0:layer<=5?1:2]:turnLimit;
            return Math.Max(Math.Max(3,minimumTurns),turns)+Math.Max(0,difficulty.additionalTurns);
        }
        public int ResolveManaBudget(SingleDifficulty difficulty) {
            return IsCombat ? checked(manaBudget+Math.Max(0,difficulty.additionalTurns)*manaRecovery) : 0;
        }
    }
    [Serializable] public sealed class SingleDifficulty { public SingleTurnAdjustment[] turnAdjustments=new SingleTurnAdjustment[0];public string name;public int additionalTurns;public int cost,requiredClear=-1,attackBonus,hpBonus,turnPenalty,budgetPenalty,extraRestrictions,rewardPercent=100,patternBonus; }
    [Serializable] public sealed class SingleAugment { public string id,name,description;public int amount=1,duration=1; }
    [Serializable] public sealed class SingleUnlock { public string id,name;public int cost,clear=-1;public string[] cards=new string[0]; }
    [Serializable] public sealed class SingleStarter { public string id,name,unlock;public string[] cards; }
    [Serializable] public sealed class SingleChapterData
    {
        public int startingTokens=10,maxHP=30,healing=8,firstClearBonus=20,eventDamage=5,eventTokens=4,eventHealing=4;
        public int[] runRewards={1,2,3,4,5},permanentRewards={10,15,20,25,30};
        public SingleStage[] stages;public SingleDifficulty[] difficulties;public SingleAugment[] augments;public SingleUnlock[] unlocks;public SingleStarter[] starters;
        public static SingleChapterData Load() {
            var text=Resources.Load<TextAsset>("SingleChapter1");
            if(text==null)throw new InvalidOperationException("SingleChapter1.json is missing.");
            var d=JsonUtility.FromJson<SingleChapterData>(text.text);d.Validate();return d;
        }
        public void Validate() {
            if(stages==null || stages.Length!=25 || stages.Select(s=>s.id).Distinct().Count()!=25)throw new InvalidDataException("Chapter requires 25 unique stages");
            int[] counts={1,11,4,3,2,2,1,1};for(int i=0;i<counts.Length;i++)if(stages.Count(s=>(int)s.kind==i)!=counts[i])throw new InvalidDataException("Invalid node composition");
            if(difficulties.Length!=4 || augments.Length!=12 || starters.Any(s=>s.cards.Length!=12))throw new InvalidDataException("Invalid chapter content");
            if(difficulties.Any(d=>d.additionalTurns<0))throw new InvalidDataException("Invalid difficulty turn bonus");
            foreach(var difficulty in difficulties)foreach(var rule in difficulty.turnAdjustments??new SingleTurnAdjustment[0])if(rule.minLayer<1 || rule.maxLayer>10 || rule.minLayer>rule.maxLayer || rule.reduction<0 || rule.types==null || rule.types.Length==0)throw new InvalidDataException("Invalid turn adjustment");
            foreach(var starter in starters)foreach(string card in starter.cards)CardDatabase.Get(card);
            foreach(var unlock in unlocks)foreach(string card in unlock.cards)CardDatabase.Get(card);
            foreach(var s in stages) {
                if(s.cost<0 || s.IsCombat && s.turnLimit<3 || !s.IsCombat && s.turnLimit<0 || s.manaBudget<0 || s.units.Select(u=>u.slot).Distinct().Count()!=s.units.Length || s.units.Any(u=>u.slot<0 || u.slot>4))throw new InvalidDataException("Invalid stage "+s.id);
                if(s.IsCombat && (s.minimumTurns<3 || s.minimumTurns>s.turnLimit || s.layerTurnLimits==null || s.layerTurnLimits.Length!=3 || s.layerTurnLimits.Any(t=>t<s.minimumTurns)))throw new InvalidDataException("Invalid stage turns: "+s.id);
                if(s.sealedSlot<0 || s.sealedSlot>4 || s.startMana<0 || s.startMana>10 || s.turnSpendLimit<1 || s.healingPercent<0 || s.healingPercent>100)throw new InvalidDataException("Invalid stage limits: "+s.id);
                if(s.enemyPool==null)s.enemyPool=new SingleEnemyCard[0];
                if(s.enemyStartMana<0 || s.enemyManaRecovery<0 || s.enemyManaCap<1 || s.enemyBudget<0 || s.enemySummonsPerTurn<1 || s.enemySummonsPerTurn>2)throw new InvalidDataException("Invalid enemy budget: "+s.id);
                foreach(var entry in s.enemyPool) {
                    var card=CardDatabase.Get(entry.card);
                    if(card.CardType==CardType.Joker || entry.weight<1 || entry.minTurn<1 || entry.maxTurn>0 && entry.maxTurn<entry.minTurn || entry.preferredSlot< -1 || entry.preferredSlot>4 || entry.maxUses<0 || !new[]{"","half","empty","ally","pattern"}.Contains(entry.condition))throw new InvalidDataException("Invalid enemy card: "+s.id);
                }
                foreach(var u in s.units)CardDatabase.Get(u.card);
                foreach(var p in s.patterns)if(p.kind==SinglePatternKind.Summon)CardDatabase.Get(p.card);
            }
        }
    }
    public enum SingleNodeState { Current, Available, InsufficientTokens, Unreached, Completed, Skipped }
    [Serializable] public sealed class SingleNode { public int id,layer,lane;public string stage;public int[] next; }
    public static class SingleGraph
    {
        public static SingleNode[] Generate(SingleChapterData data,int seed) {
            var r=new System.Random(seed);int[] sizes={1,3,3,3,3,3,3,3,2,1};
            var early=data.stages.Where(s=>s.kind==SingleNodeKind.Battle).OrderBy(s=>s.enemyHP).ThenBy(s=>s.id,StringComparer.Ordinal).ToList();
            var rest=data.stages.Where(s=>s.kind!=SingleNodeKind.Start && s.kind!=SingleNodeKind.Boss && s.kind!=SingleNodeKind.Battle).OrderBy(_=>r.Next()).ToList();
            var order=early.Take(6).OrderBy(_=>r.Next()).Concat(early.Skip(6).Concat(rest).OrderBy(_=>r.Next())).ToList();
            var nodes=new List<SingleNode>();int cursor=0;
            for(int layer=0;layer<10;layer++)for(int lane=0;lane<sizes[layer];lane++)nodes.Add(new SingleNode{id=nodes.Count,layer=layer,lane=lane,stage=layer==0 ? data.stages.First(s=>s.kind==SingleNodeKind.Start).id : layer==9 ? data.stages.First(s=>s.kind==SingleNodeKind.Boss).id : order[cursor++].id});
            for(int layer=0;layer<9;layer++) {
                var from=nodes.Where(n=>n.layer==layer).ToArray();var to=nodes.Where(n=>n.layer==layer+1).ToArray();
                foreach(var n in from)n.next=to.Length<=2 ? to.Select(x=>x.id).ToArray() : to.OrderBy(_=>r.Next()).Take(r.Next(2,4)).Select(x=>x.id).OrderBy(x=>x).ToArray();
                foreach(var missing in to.Where(t=>!from.Any(n=>n.next.Contains(t.id)))) {
                    var source=from.FirstOrDefault(n=>n.next.Length<3);if(source==null)throw new InvalidDataException("Unreachable layer");source.next=source.next.Concat(new[]{missing.id}).OrderBy(x=>x).ToArray();
                }
            }
            nodes.Last().next=new int[0];var result=nodes.ToArray();AssignGrowthSafeStages(data,result,order.Skip(6).ToArray(),r);Validate(data,result);return result;
        }
        // Keep the low-HP first six battles, 25 unique stages and the existing random edges.
        // Assign remaining stage kinds against all incoming path histories, not one sampled route.
        static void AssignGrowthSafeStages(SingleChapterData data,SingleNode[] nodes,SingleStage[] remaining,System.Random random) {
            var pools=remaining.GroupBy(s=>s.kind).ToDictionary(g=>g.Key,g=>g.ToList());
            var incoming=nodes.Select(n=>nodes.Where(p=>p.next.Contains(n.id)).Select(p=>p.id).ToArray()).ToArray();
            var pending=new bool[25];var streak=new int[25];int attempts=0;
            Func<int,bool> assign=null;
            assign=index=> {
                if(index==24)return true;
                if(++attempts>10000)return false;
                bool waiting=incoming[index].Any(p=>pending[p]);int consecutive=incoming[index].Select(p=>streak[p]).DefaultIfEmpty(0).Max()+1;
                foreach(var kind in pools.Keys.Where(k=>pools[k].Count>0).OrderBy(k=>random.Next()).ToArray()) {
                    bool combat=kind==SingleNodeKind.Battle || kind==SingleNodeKind.Elite;
                    if(nodes[index].layer==8 && !combat || !combat && consecutive>1 || kind==SingleNodeKind.Augment && waiting)continue;
                    int combats=pools.Where(p=>p.Key==SingleNodeKind.Battle || p.Key==SingleNodeKind.Elite).Sum(p=>p.Value.Count);
                    if(nodes[index].layer<8 && combat && combats<=2)continue;
                    var pool=pools[kind];var stage=pool[pool.Count-1];pool.RemoveAt(pool.Count-1);nodes[index].stage=stage.id;
                    pending[index]=!combat && (waiting || kind==SingleNodeKind.Augment);streak[index]=combat?0:consecutive;
                    if(assign(index+1))return true;
                    pool.Add(stage);
                }
                return false;
            };
            if(assign(7))return;
            // Bounded fallback guarantees generation even for an unlucky search order.
            // Each support layer is separated by a full combat layer, so every edge remains safe.
            var support=remaining.Where(s=>!s.IsCombat).OrderBy(s=>random.Next()).ToList();
            var battles=remaining.Where(s=>s.IsCombat).OrderBy(s=>random.Next()).ToList();
            var supportSlots=nodes.Where(n=>n.layer==3 || n.layer==5 || n.layer==7).OrderBy(n=>random.Next()).ToArray();
            for(int i=0;i<supportSlots.Length;i++) {
                var stage=i<support.Count?support[i]:battles[battles.Count-1];supportSlots[i].stage=stage.id;
                if(i>=support.Count)battles.RemoveAt(battles.Count-1);
            }
            foreach(var node in nodes.Where(n=>n.layer==4 || n.layer==6 || n.layer==8)){node.stage=battles[battles.Count-1].id;battles.RemoveAt(battles.Count-1);}
        }
        // Old runs keep their stages, rewards and position. Bypass support-to-support
        // edges through the nearest combat frontier, preserving alternate support choices.
        public static SingleNode[] RequireCombatBetweenSupport(SingleChapterData data,SingleNode[] original,bool combatBorders=true) {
            var nodes=original.Select(n=>new SingleNode{id=n.id,layer=n.layer,lane=n.lane,stage=n.stage,next=(int[])n.next.Clone()}).ToArray();
            var combat=nodes.Select(n=>data.stages.First(s=>s.id==n.stage).IsCombat || n.id==0).ToArray();
            var edges=nodes.Select(n=>new HashSet<int>(n.next)).ToArray();
            Func<int,IEnumerable<int>> before=null,after=null;
            before=id=>original.Where(n=>n.next.Contains(id)).SelectMany(n=>combat[n.id]?new[]{n.id}:before(n.id)).Distinct();
            after=id=>original[id].next.SelectMany(n=>combat[n]?new[]{n}:after(n)).Distinct();
            foreach(var node in original)foreach(int target in node.next)if(!combat[node.id] && !combat[target]) {
                edges[node.id].Remove(target);
                foreach(int next in after(target))edges[node.id].Add(next);
                foreach(int previous in before(node.id))edges[previous].Add(target);
            }
            foreach(var node in nodes)node.next=edges[node.id].OrderBy(id=>id).ToArray();
            Validate(data,nodes,true,combatBorders);return nodes;
        }
        public static void Validate(SingleChapterData data,SingleNode[] nodes,bool growthRules=true,bool combatBorders=true) {
            if(nodes.Length!=25 || nodes.Select(n=>n.stage).Distinct().Count()!=25 || nodes.Where((n,i)=>n.id!=i).Any() || nodes[0].layer!=0 || nodes[24].layer!=9)throw new InvalidDataException("Invalid graph size");
            if(data.stages.First(s=>s.id==nodes[0].stage).kind!=SingleNodeKind.Start || data.stages.First(s=>s.id==nodes[24].stage).kind!=SingleNodeKind.Boss)throw new InvalidDataException("Invalid start/boss stages");
            var seen=new HashSet<int>{0};var economy=new Dictionary<int,int>{{0,data.startingTokens}};
            foreach(var n in nodes) {
                if(!seen.Contains(n.id))throw new InvalidDataException("Isolated node");
                if(n.next==null || n.next.Distinct().Count()!=n.next.Length)throw new InvalidDataException("Invalid node edges");
                if(n.id!=24 && n.next.Length<1)throw new InvalidDataException("Dead end");
                foreach(int id in n.next) {
                    if(id<=n.id || id>=25 || nodes[id].layer<=n.layer)throw new InvalidDataException("Cycle or invalid edge");seen.Add(id);
                    var s=data.stages.First(x=>x.id==nodes[id].stage);int balance;
                    if(economy.TryGetValue(n.id,out balance) && balance>=s.cost) {
                        int reward=s.kind==SingleNodeKind.Battle || s.kind==SingleNodeKind.Elite || s.kind==SingleNodeKind.Boss ? data.runRewards.Max() : 0;
                        int next=balance-s.cost+reward;if(!economy.ContainsKey(id) || economy[id]<next)economy[id]=next;
                    }
                }
            }
            if(growthRules) {
                var pending=new bool[25];var streak=new int[25];
                foreach(var node in nodes.Skip(1)) {
                    var stage=data.stages.First(s=>s.id==node.stage);var parents=nodes.Where(p=>p.next.Contains(node.id)).ToArray();
                    bool waiting=parents.Any(p=>pending[p.id]);int consecutive=parents.Select(p=>streak[p.id]).DefaultIfEmpty(0).Max()+1;
                    if(combatBorders && (parents.Any(p=>p.id==0) || node.next.Contains(24)) && !stage.IsCombat)throw new InvalidDataException("Start/boss must border combat");
                    if(stage.kind==SingleNodeKind.Augment && waiting)throw new InvalidDataException("Augments require an intervening combat");
                    if(!stage.IsCombat && consecutive>1)throw new InvalidDataException("Consecutive noncombat nodes");
                    pending[node.id]=!stage.IsCombat && (waiting || stage.kind==SingleNodeKind.Augment);streak[node.id]=stage.IsCombat?0:consecutive;
                }
            }
            var reachesBoss=new HashSet<int>{24};
            for(int i=nodes.Length-2;i>=0;i--)if(nodes[i].next.Any(reachesBoss.Contains))reachesBoss.Add(i);
            if(reachesBoss.Count!=nodes.Length)throw new InvalidDataException("Node cannot reach boss");
            if(!economy.ContainsKey(24))throw new InvalidDataException("No affordable perfect-health boss route");
        }
    }
    [Serializable] public sealed class SingleRun
    {
        public string id,phase="map",failure="",starter;public int graphVersion;public int seed,difficulty,current,tokens,hp,maxHP,permanentAward,lastRunAward,firstClearAward;
        public SingleNode[] nodes;public List<int> visited=new List<int>{0};public List<string> deck=new List<string>(),augments=new List<string>(),offers=new List<string>();
        public SingleBattleSave battle;public bool settled,legacyCombatBorders;public List<string> usedCards=new List<string>();
    }
    [Serializable] public sealed class SingleHistory { public string id,result,starter;public int seed,difficulty,visited,hp,reward; }
    [Serializable] public sealed class SingleProfile
    {
        public int version=1,permanentTokens;public bool firstClear;
        public List<int> unlocked=new List<int>{0},cleared=new List<int>();public List<string> content=new List<string>(),settledRuns=new List<string>();
        public List<SingleHistory> history=new List<SingleHistory>();public SingleRun run;
    }
    public sealed class SingleCampaign
    {
        public readonly SingleChapterData Data;public SingleProfile Profile;public SingleRun Run=>Profile.run;
        public string SavePath {get;}public event Action Changed;
        public SingleCampaign(SingleChapterData data,string savePath=null) {
            Data=data;SavePath=savePath??Path.Combine(Application.persistentDataPath,"unknown-chapter1-v1.json");
            Profile=File.Exists(SavePath) ? JsonUtility.FromJson<SingleProfile>(File.ReadAllText(SavePath)) : new SingleProfile();
            if(Profile==null || Profile.version!=1)throw new InvalidDataException("Unsupported chapter save version");
            if(Run!=null) {
                SingleGraph.Validate(Data,Run.nodes,false);
                if(Run.graphVersion<2){
                    Run.legacyCombatBorders=Run.graphVersion==0;
                    Run.nodes=SingleGraph.RequireCombatBetweenSupport(Data,Run.nodes,!Run.legacyCombatBorders);Run.graphVersion=2;Save();
                } else SingleGraph.Validate(Data,Run.nodes,true,!Run.legacyCombatBorders);
            }
        }
        public void Save() {
            string dir=Path.GetDirectoryName(SavePath);Directory.CreateDirectory(dir);string temp=SavePath+".tmp";
            File.WriteAllText(temp,JsonUtility.ToJson(Profile,true));
#if UNITY_WEBGL && !UNITY_EDITOR
            // IDBFS is a virtual filesystem; native atomic File.Replace is unavailable.
            // The web template enables Unity's automatic persistentDataPath -> IndexedDB sync.
            File.Copy(temp,SavePath,true);File.Delete(temp);
#else
            if(File.Exists(SavePath))File.Replace(temp,SavePath,null);else File.Move(temp,SavePath);
#endif
            Changed?.Invoke();
        }
        public bool CanUse(SingleStarter s)=>string.IsNullOrEmpty(s.unlock)||Profile.content.Contains(s.unlock);
        public void Start(int difficulty,int seed)=>Start(difficulty,Data.starters.First(s=>string.IsNullOrEmpty(s.unlock)).id,seed);
        public void Start(int difficulty,string starter,int seed) {
            if(!Profile.unlocked.Contains(difficulty))throw new InvalidOperationException("난이도가 잠겨 있습니다.");
            var d=Data.starters.First(s=>s.id==starter);if(!CanUse(d))throw new InvalidOperationException("시작 덱이 잠겨 있습니다.");
            if(Run!=null && Run.phase!="done")Finish(false,"ABANDONED");
            SingleNode[] graph=null;for(int attempt=0;attempt<100;attempt++)try{graph=SingleGraph.Generate(Data,unchecked(seed+attempt));seed=unchecked(seed+attempt);break;}catch(InvalidDataException){ }
            if(graph==null)throw new InvalidDataException("Unable to generate affordable graph");
            Profile.run=new SingleRun{graphVersion=2,id=Guid.NewGuid().ToString("N"),seed=seed,difficulty=difficulty,starter=starter,nodes=graph,tokens=Data.startingTokens,hp=Data.maxHP,maxHP=Data.maxHP,deck=d.cards.ToList()};Save();
        }
        public bool IsTeachingBattle=>Stage.kind==SingleNodeKind.Battle && Run.visited.Count(id=>{var k=Data.stages.First(s=>s.id==Run.nodes[id].stage).kind;return k==SingleNodeKind.Battle || k==SingleNodeKind.Elite;})==1;
        public int TurnLimitFor(SingleStage stage,int node)=>stage.ResolveTurnLimit(Data.difficulties[Run.difficulty],Run.nodes[node].layer);
        public int CurrentTurnLimit=>TurnLimitFor(Stage,Run.current);
        public int ManaBudgetFor(SingleStage stage)=>stage.ResolveManaBudget(Data.difficulties[Run.difficulty]);
        public int CurrentManaBudget=>ManaBudgetFor(Stage);
        public SingleStage Stage=>Data.stages.First(s=>s.id==Run.nodes[Run.current].stage);
        public IEnumerable<SingleNode> Next=>Run.nodes[Run.current].next.Select(id=>Run.nodes[id]);
        public SingleNodeState NodeState(int node) {
            if(node==Run.current)return SingleNodeState.Current;
            if(Run.visited.Contains(node))return SingleNodeState.Completed;
            if(Run.nodes[node].layer<=Run.nodes[Run.current].layer)return SingleNodeState.Skipped;
            if(Run.phase=="map" && Run.nodes[Run.current].next.Contains(node))return EntryError(node).Length==0?SingleNodeState.Available:SingleNodeState.InsufficientTokens;
            return SingleNodeState.Unreached;
        }
        public string EntryError(int node) {
            if(Run==null || node<0 || node>=Run.nodes.Length)return "현재 위치에서 이동할 수 없습니다.";
            if(node==Run.current)return "현재 위치입니다.";
            if(Run.visited.Contains(node))return "이미 완료한 스테이지입니다.";
            if(Run.phase=="done")return "종료된 런입니다.";
            if(Run.phase!="map")return "현재 스테이지를 먼저 완료하세요.";
            if(!Run.nodes[Run.current].next.Contains(node))return Run.nodes[node].layer<=Run.nodes[Run.current].layer?"선택하지 않은 이전 분기입니다.":"연결되지 않은 경로입니다.";
            var nextStage=Data.stages.First(s=>s.id==Run.nodes[node].stage);
            if(Run.current!=0 && !Stage.IsCombat && !nextStage.IsCombat)return "다음 비전투 노드에 가려면 먼저 전투를 클리어해야 합니다.";
            int cost=nextStage.cost;
            return Run.tokens<cost?$"필요 {cost} / 보유 {Run.tokens} · {cost-Run.tokens}토큰 부족":"";
        }
        public void Enter(int node) {
            string reason=EntryError(node);if(reason.Length>0)throw new InvalidOperationException(reason);
            var stage=Data.stages.First(s=>s.id==Run.nodes[node].stage);
            Run.tokens-=stage.cost;Run.current=node;Run.visited.Add(node);Run.phase="prepare";Run.battle=null;Run.settled=false;Run.offers.Clear();Save();
        }
        public static int Tier(int hp,int maxHP)=>hp<=0?0:Math.Min(4,Math.Max(0,(int)(((long)hp*5-1)/Math.Max(1,maxHP))));
        public void BattleResult(BattleEngine engine) {
            if(Run.phase!="battle" || Run.settled || !engine.State.IsOver)return;
            Run.settled=true;Run.hp=engine.State.Players[0].HP;Run.battle=null;
            if(engine.State.Winner!=0){Finish(false,engine.Single.Failure.Length>0?engine.Single.Failure:"PLAYER HP ZERO");return;}
            Run.lastRunAward=Data.runRewards[Tier(Run.hp,Run.maxHP)];Run.tokens+=Run.lastRunAward;
            if(Stage.kind==SingleNodeKind.Boss){Finish(true,"");return;}
            Offer(Stage.kind==SingleNodeKind.Elite?"augment":"card");Save();
        }
        public string[] CardPool() {
            var allowed=new List<string>();foreach(var s in Data.starters.Where(CanUse))allowed.AddRange(s.cards);
            foreach(var u in Data.unlocks.Where(u=>Profile.content.Contains(u.id)))allowed.AddRange(u.cards);
            return allowed.Distinct().ToArray();
        }
        void Offer(string type) {
            Run.phase=type;var rng=new System.Random(unchecked(Run.seed+Run.current*991));
            Run.offers=(type=="augment"?Data.augments.Where(a=>!Run.augments.Contains(a.id)).Select(a=>a.id):CardPool()).OrderBy(_=>rng.Next()).Take(3).ToList();
        }
        public void ResolveSupport() {
            if(Run.phase!="prepare")return;
            switch(Stage.kind) {
                case SingleNodeKind.Augment:Offer("augment");Save();break;
                case SingleNodeKind.Heal:Run.hp=Math.Min(Run.maxHP,Run.hp+Data.healing);CompleteNode();break;
                case SingleNodeKind.Purge:Run.phase="purge";Save();break;
                case SingleNodeKind.Event:Run.phase="event";Save();break;
            }
        }
        public void TakeReward(string id,int replace=-1) {
            if(!Run.offers.Contains(id) || Run.phase!="card" && Run.phase!="augment")throw new InvalidOperationException("Invalid reward");
            if(Run.phase=="card")Run.deck.Add(id);else if(Run.augments.Count<3)Run.augments.Add(id);else if(replace>=0 && replace<3)Run.augments[replace]=id;else throw new InvalidOperationException("교체할 증강을 선택하세요.");
            CompleteNode();
        }
        public void SkipReward(){if(Run.phase=="card" || Run.phase=="augment")CompleteNode();}
        public void Purge(int index,bool replace) {
            if(Run.phase!="purge" || index<0 || index>=Run.deck.Count || Run.deck.Count<=5)return;
            Run.deck.RemoveAt(index);if(replace){Offer("card");Save();}else CompleteNode();
        }
        public void Event(bool risk) {
            if(Run.phase!="event")return;
            if(risk){Run.hp=Math.Max(1,Run.hp-Data.eventDamage);Run.tokens+=Data.eventTokens;}else Run.hp=Math.Min(Run.maxHP,Run.hp+Data.eventHealing);CompleteNode();
        }
        public void CompleteNode() {
            Run.phase="map";Run.offers.Clear();Run.battle=null;
            if(!Next.Any(n=>Data.stages.First(s=>s.id==n.stage).cost<=Run.tokens))Finish(false,"INSUFFICIENT TOKENS");else Save();
        }
        public void Finish(bool clear,string failure) {
            if(Run==null || Run.phase=="done")return;
            Run.phase="done";Run.failure=clear?"":failure;Run.settled=true;Run.battle=null;
            if(!Profile.settledRuns.Contains(Run.id)) {
                if(clear) {
                    Run.permanentAward=Data.permanentRewards[Tier(Run.hp,Run.maxHP)]*Data.difficulties[Run.difficulty].rewardPercent/100;
                    if(!Profile.firstClear){Run.firstClearAward=Data.firstClearBonus;Run.permanentAward+=Run.firstClearAward;Profile.firstClear=true;}
                    Profile.permanentTokens+=Run.permanentAward;if(!Profile.cleared.Contains(Run.difficulty))Profile.cleared.Add(Run.difficulty);
                }
                Profile.settledRuns.Add(Run.id);Profile.history.Add(new SingleHistory{id=Run.id,result=clear?"CLEAR":failure,starter=Run.starter,seed=Run.seed,difficulty=Run.difficulty,visited=Run.visited.Count,hp=Run.hp,reward=Run.permanentAward});
            }
            Run.tokens=0;Save();
        }
        public void UnlockDifficulty(int index) {
            var d=Data.difficulties[index];if(Profile.unlocked.Contains(index))return;
            if(!Profile.cleared.Contains(d.requiredClear) || Profile.permanentTokens<d.cost)throw new InvalidOperationException("선행 클리어 또는 영구 토큰이 부족합니다.");
            Profile.permanentTokens-=d.cost;Profile.unlocked.Add(index);Save();
        }
        public void Unlock(string id) {
            var u=Data.unlocks.First(x=>x.id==id);if(Profile.content.Contains(id))return;
            if(u.clear>=0 && !Profile.cleared.Contains(u.clear) || Profile.permanentTokens<u.cost)throw new InvalidOperationException("해금 조건을 충족하지 못했습니다.");
            Profile.permanentTokens-=u.cost;Profile.content.Add(id);Save();
        }
    }
}
