#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
namespace Unknown.Battle
{
    // These optional callbacks observe resolved rules and never supply rule results.
    public interface IBalanceObserver
    {
        void Event(BattleEngine engine, BattleEvent entry);
        void Effect(BattleEngine engine, CardInstance source, CardEffect effect, CardInstance target, int actual, string outcome);
    }
    public sealed partial class BattleEngine
    {
        public IBalanceObserver BalanceObserver { get; set; }
        partial void MeasureDamageAttempt(CardInstance dealer,CardInstance target,int requested,int actual)
            => (BalanceObserver as BalanceMatchCollector)?.DamageAttempt(dealer,target,requested,actual);
        partial void ObserveBalanceEvent(BattleEvent entry) => BalanceObserver?.Event(this,entry);
        partial void MeasureBalanceEffect(CardEffect effect,CardInstance target,int actual,string outcome)
            => BalanceObserver?.Effect(this,source,effect,target,actual,outcome);
        partial void MeasureAttackRecovery(CardInstance card,CardEffect effect,int before,int actual)
            => BalanceObserver?.Effect(this,card,effect,card,actual,"resolved");
        partial void MeasureAttackRecoverySkipped(CardInstance card,CardEffect effect)
            => BalanceObserver?.Effect(this,card,effect,card,0,"source_destroyed");
        internal void SetBalanceRandomTrace(Action<int,int,int> trace){if(random is SeededRandom seeded)seeded.Trace=trace;}
    }
    [Serializable]
    public sealed class BalanceCardStats
    {
        public string cardID,scope,group;
        public CardBalanceVersion version;
        public long appearances,uses,opportunities,discardedUnused,combinedAsMaterial,matchesUsed,playerMatchesUsed,winsUsed;
        public long useTurnSum,summons,survivalTurnSum,deadUnits,censoredUnits,manaPaid;
        public long attackDamage,effectDamage,enemyUnitDamage,enemyHeroDamage,selfDamage,damageReceived,kills;
        public long healing,attackIncrease,attackDecrease,extraHP,draws,discardEffects;
        public long afterAttackHealing,manaGained,manaWasted,exchangeDraws,exchangesSkipped,jokerPaidUses,jokerFreeUses,overflowDraws,unknownCreated;
        public long effectActivations,effectApplications,effectSuccesses,effectFailures,conditionsMet,invalidated;
        public long jokerSuccesses,jokerFailures,jokerAttackGain,jokerHPGain,jokerLostAttack,jokerLostHP;
        public long mapAttackChange,mapHPChange,mapDamageReceived,mapSummonDraws,mapGrantedAppearances;
        public double buffExtraDamage,debuffPreventedDamage,extraHPAbsorbed,mapExtraDamage,mapHPAbsorbed;
        public double UsageWinRate => playerMatchesUsed==0 ? 0 : (double)winsUsed/playerMatchesUsed;
        public double AverageDamage => uses==0 ? 0 : (double)(attackDamage+effectDamage)/uses;
        public double AverageSurvival => summons==0 ? 0 : (double)survivalTurnSum/summons;
        public double DamagePerMana => manaPaid==0 ? 0 : (double)(attackDamage+effectDamage)/manaPaid;
        public void Add(BalanceCardStats other)
        {
            foreach(var field in typeof(BalanceCardStats).GetFields()) {
                if(field.FieldType==typeof(long))field.SetValue(this,(long)field.GetValue(this)+(long)field.GetValue(other));
                if(field.FieldType==typeof(double))field.SetValue(this,(double)field.GetValue(this)+(double)field.GetValue(other));
            }
        }
    }
    [Serializable]
    public sealed class BalanceEffectStats
    {
        public string cardID,kind,target,unit;
        public int effectIndex;
        public long activations,conditionsMet,applications,successes,failures,invalidated,zeroResults;
        public long requestedAmount;
        public double actualAmount,extraDamage,preventedDamage,absorbedDamage;
        public void Add(BalanceEffectStats other)
        {
            foreach(var field in typeof(BalanceEffectStats).GetFields()) {
                if(field.FieldType==typeof(long))field.SetValue(this,(long)field.GetValue(this)+(long)field.GetValue(other));
                if(field.FieldType==typeof(double))field.SetValue(this,(double)field.GetValue(this)+(double)field.GetValue(other));
            }
        }
    }
    [Serializable]
    public sealed class BalanceMatchResult
    {
        public int match,seed,turns,actions,winner=-1;
        public string conditionID,swapID,version,initialFirstDeckHash,initialSecondDeckHash;
        public int firstDeckSeed,secondDeckSeed,firstPolicySeed,secondPolicySeed,randomCalls,firstUnknownSummonTurn=-1,secondUnknownSummonTurn=-1,firstUnknownCreationTurn=-1,secondUnknownCreationTurn=-1,firstUnknownMax,secondUnknownMax;
        public long randomFingerprint;
        public int firstPolicyRandomCalls,secondPolicyRandomCalls;
        public long firstPolicyRandomFingerprint,secondPolicyRandomFingerprint;
        public int firstHP,secondHP,firstDeck,secondDeck,firstHand,secondHand;
        public string firstBoard,secondBoard,firstHandIDs,secondHandIDs;
        public string map,firstAI,secondAI,status,reason,error;
        public string[] firstUsed,secondUsed;
    }
    [Serializable]
    public sealed class BalanceDetail
    {
        public int match,turn,source,target,amount;
        public int requested,before,after;
        public int firstUnits,secondUnits,firstHeroHP,secondHeroHP,firstMana,secondMana,firstMaxMana,secondMaxMana;
        public string unitStates;
        public int secondaryAmount;
        public int actor,commandFirst,commandSecond,effectIndex;
        public int beforeAttack,afterAttack,beforeMaxHP,afterMaxHP,spentMana;
        public bool paid;
        public double expectedProbability;
        public string targetCardID,targetCardType;
        public string kind,cardID,effect,outcome;
    }
    public sealed class BalanceMatchCollector : IBalanceObserver
    {
        sealed class Tracked
        {
            public CardInstance card;
            public BalanceCardStats stats;
            public int summoned=-1;
        }
        sealed class Grant
        {
            public BalanceCardStats source;
            public BalanceEffectStats effect;
            public int attack;
            public double hp;
            public bool map;
        }
        readonly BattleEngine engine;
        readonly Dictionary<int,Tracked> cards=new Dictionary<int,Tracked>();
        readonly Dictionary<int,List<Grant>> grants=new Dictionary<int,List<Grant>>();
        readonly Dictionary<int,int> lastDamage=new Dictionary<int,int>();
        readonly HashSet<string> activations=new HashSet<string>();
        readonly HashSet<string> fulfilled=new HashSet<string>();
        readonly HashSet<int> used=new HashSet<int>();
        readonly Action<BalanceDetail> detail;
        readonly int match;
        int joker=-1,summoner=-1;
        int jokerBeforeHP,jokerBeforeAttack,jokerBeforeMax;
        bool jokerPaid;
        double jokerChance;
        public readonly Dictionary<string,BalanceCardStats> Rows=new Dictionary<string,BalanceCardStats>();
        public readonly Dictionary<string,BalanceEffectStats> Effects=new Dictionary<string,BalanceEffectStats>();
        AiMove command;
        public AiMove Command {
            get=>command;
            set {command=value;if(value!=null && detail!=null)detail(new BalanceDetail{match=match,turn=engine.State.TurnNumber,kind="Command",actor=engine.State.ActivePlayer,commandFirst=value.First,commandSecond=value.Second,effect=value.Action.ToString()});}
        }
        public BalanceMatchCollector(BattleEngine engine,int match,Action<BalanceDetail> detail=null)
        {
            this.engine=engine;this.match=match;this.detail=detail;
            foreach(var player in engine.State.Players)
                foreach(var card in player.HandCards.Concat(player.DeckCards).Concat(player.DiscardCards).Concat(player.Slots).Where(c=>c!=null))Track(card);
            foreach(var entry in engine.DrainEvents())Event(engine,entry);
            engine.BalanceObserver=this;
            if(detail!=null)engine.SetBalanceRandomTrace((index,max,value)=>detail(new BalanceDetail{match=match,turn=engine.State.TurnNumber,kind="RNG",before=index,requested=max,amount=value}));
        }
        Tracked Track(CardInstance card)
        {
            if(cards.TryGetValue(card.InstanceID,out var found))return found;
            string key=card.Owner+"|"+card.Definition.CardID;
            if(!Rows.TryGetValue(key,out var row)){row=new BalanceCardStats{cardID=card.Definition.CardID,group=card.Owner.ToString(),version=card.Definition.Version};Rows.Add(key,row);}
            found=new Tracked{card=card,stats=row};cards.Add(card.InstanceID,found);return found;
        }
        Tracked Find(int id)
        {
            if(id<0)return null;
            if(cards.TryGetValue(id,out var found))return found;
            var card=engine.State.Players.SelectMany(p=>p.HandCards.Concat(p.DeckCards).Concat(p.DiscardCards).Concat(p.Slots)).FirstOrDefault(c=>c!=null && c.InstanceID==id);
            return card==null ? null : Track(card);
        }
        BalanceEffectStats EffectRow(Tracked source,int index,string kind,string target)
        {
            string key=source.card.Definition.CardID+"|"+index;
            if(!Effects.TryGetValue(key,out var row)) {
                row=new BalanceEffectStats{cardID=source.card.Definition.CardID,effectIndex=index,kind=kind,target=target,unit=kind=="DrawCard" || kind=="DiscardCard" ? "cards" : kind=="Joker" ? "outcomes" : "stat_points"};Effects.Add(key,row);
            }
            return row;
        }
        void Log(string kind,int source,int target,int amount,string effect="",string outcome="",int secondary=0)
        {
            if(detail==null)return;
            detail(new BalanceDetail{match=match,turn=engine.State.TurnNumber,kind=kind,source=source,target=target,amount=amount,secondaryAmount=secondary,cardID=Find(source)?.card.Definition.CardID ?? "",targetCardID=Find(target)?.card.Definition.CardID ?? "",targetCardType=Find(target)?.card.Definition.CardType.ToString() ?? "",effect=effect,outcome=outcome});
        }
        public void Opportunities(IEnumerable<AiMove> moves)
        {
            foreach(int id in moves.Where(m=>m.Action==BattleAction.Summon || m.Action==BattleAction.Joker).Select(m=>m.First).Distinct())Find(id).stats.opportunities++;
        }
        void Use(Tracked card)
        {
            card.stats.uses++;card.stats.useTurnSum+=engine.State.TurnNumber;used.Add(card.card.InstanceID);
        }
        public void DamageAttempt(CardInstance dealer,CardInstance target,int requested,int actual) {
            detail?.Invoke(new BalanceDetail{match=match,turn=engine.State.TurnNumber,kind="DamageAttempt",source=dealer?.InstanceID ?? -1,target=target?.InstanceID ?? -1,
                actor=dealer?.Owner ?? -1,cardID=dealer?.Definition.CardID ?? "",targetCardID=target?.Definition.CardID ?? "",requested=requested,amount=actual,
                effect=command?.Action==BattleAction.Attack ? "Attack" : "Effect"});
        }
        public void EndCommand(){
            if(detail!=null) {
                var a=engine.State.Players[0];var b=engine.State.Players[1];
                detail(new BalanceDetail{match=match,turn=engine.State.TurnNumber,kind="StateSample",actor=engine.State.ActivePlayer,
                    firstUnits=a.Board.Count(c=>c!=null),secondUnits=b.Board.Count(c=>c!=null),firstHeroHP=a.HP,secondHeroHP=b.HP,
                    firstMana=a.CurrentMana,secondMana=b.CurrentMana,firstMaxMana=a.MaxMana,secondMaxMana=b.MaxMana,
                    unitStates=string.Join("|",a.Board.Concat(b.Board).Where(c=>c!=null).Select(c=>c.InstanceID+":"+c.Owner+":"+c.Attack+":"+c.CurrentHP))});
            }
            Command=null;joker=-1;summoner=-1;engine.DrainEvents();}
        public void Event(BattleEngine g,BattleEvent e)
        {
            bool mapMarker=e.Kind==BattleEventKind.MapEvent || e.Kind==BattleEventKind.SuitMapTurn;
            var source=mapMarker ? null : Find(e.SourceID);var target=Find(e.TargetID);
            Log(e.Kind.ToString(),mapMarker ? -1 : e.SourceID,e.TargetID,e.Amount,secondary:e.Secondary);
            switch(e.Kind) {
                case BattleEventKind.Draw:
                    if(e.Secondary==1 && target!=null)target.stats.overflowDraws++;
                    if(e.Secondary==0 && target!=null) {
                        target.stats.appearances++;
                        if(summoner>=0 && g.PendingAiEffect(g.State.ActivePlayer)==null)Find(summoner).stats.mapSummonDraws++;
                    }break;
                case BattleEventKind.CardGranted:if(target!=null){target.stats.appearances++;target.stats.mapGrantedAppearances++;}break;
                case BattleEventKind.Combine:
                    if(source!=null)source.stats.combinedAsMaterial++;
                    if(target!=null)target.stats.combinedAsMaterial++;
                    Find(e.Secondary).stats.appearances++;Find(e.Secondary).stats.unknownCreated++;break;
                case BattleEventKind.Summon:
                    summoner=e.SourceID;Use(source);source.summoned=g.State.TurnNumber;source.stats.summons++;break;
                case BattleEventKind.ManaSpent:
                    if(Command!=null && (Command.Action==BattleAction.Summon || Command.Action==BattleAction.Joker)) {
                        var paying=Find(Command.First);paying.stats.manaPaid+=e.Amount;
                        detail?.Invoke(new BalanceDetail{match=match,turn=g.State.TurnNumber,kind="ManaPayment",actor=paying.card.Owner,source=Command.First,cardID=paying.card.Definition.CardID,requested=paying.card.Definition.ManaCost,amount=e.Amount,before=e.Secondary,after=e.Secondary-e.Amount});
                    }break;
                case BattleEventKind.Discard:if(target!=null && !used.Contains(e.TargetID))target.stats.discardedUnused++;break;
                case BattleEventKind.Attack:
                    Deal(source,target,e.Amount,false,target==null ? g.State.Players[e.TargetOwner].HP+e.Amount : target.card.CurrentHP);
                    if(target!=null)Deal(target,source,e.Secondary,false,source.card.CurrentHP);break;
                case BattleEventKind.Damage:
                    Deal(source,target,e.Amount,true,Math.Max(0,target.card.CurrentHP)+e.Amount);break;
                case BattleEventKind.AttackChanged:case BattleEventKind.MaxHPChanged:
                    if(target!=null)GrantStats(source,target,e);break;
                case BattleEventKind.JokerTravel:
                    jokerBeforeHP=target.card.CurrentHP;jokerBeforeAttack=target.card.Attack;jokerBeforeMax=target.card.MaxHP;jokerPaid=e.Secondary==1;
                    jokerChance=g.Config.Map==BattleMap.JokerGame ? g.JokerChance(e.Owner)/100.0 : .5;
                    joker=e.SourceID;Use(source);if(e.Secondary==1)source.stats.jokerPaidUses++;else source.stats.jokerFreeUses++;EffectRow(source,-1,"Joker","Ally").activations++;source.stats.effectActivations++;break;
                case BattleEventKind.JokerSuccess:case BattleEventKind.JokerFailure:
                    var gambler=Find(joker);
                    if(gambler!=null) {
                        bool success=e.Kind==BattleEventKind.JokerSuccess;
                        detail?.Invoke(new BalanceDetail{match=match,turn=g.State.TurnNumber,kind="JokerOutcome",source=joker,target=e.TargetID,cardID=gambler.card.Definition.CardID,targetCardID=target.card.Definition.CardID,targetCardType=target.card.Definition.CardType.ToString(),actor=e.Owner,paid=jokerPaid,expectedProbability=jokerChance,spentMana=jokerPaid ? gambler.card.Definition.ManaCost : 0,before=jokerBeforeHP,after=success ? target.card.CurrentHP : 0,beforeAttack=jokerBeforeAttack,afterAttack=success ? target.card.Attack : 0,beforeMaxHP=jokerBeforeMax,afterMaxHP=success ? target.card.MaxHP : 0,outcome=success ? "success" : "failure"});
                        var effect=EffectRow(gambler,-1,"Joker","Ally");effect.conditionsMet++;effect.applications++;gambler.stats.conditionsMet++;gambler.stats.effectApplications++;
                        if(e.Kind==BattleEventKind.JokerSuccess){gambler.stats.jokerSuccesses++;effect.successes++;effect.actualAmount++;gambler.stats.effectSuccesses++;}
                        else {gambler.stats.jokerFailures++;effect.failures++;gambler.stats.effectFailures++;gambler.stats.jokerLostAttack+=target.card.Attack;gambler.stats.jokerLostHP+=Math.Max(0,target.card.CurrentHP);lastDamage.Remove(e.TargetID);}
                    }break;
                case BattleEventKind.Destroy:
                    if(target!=null) {
                        target.stats.deadUnits++;CloseLife(target);
                        if(lastDamage.TryGetValue(e.TargetID,out int killer)) {var credited=Find(killer);if(credited!=null && credited.card.Owner!=target.card.Owner)credited.stats.kills++;}
                        lastDamage.Remove(e.TargetID);grants.Remove(e.TargetID);
                    }break;
            }
        }
        void GrantStats(Tracked source,Tracked target,BattleEvent e)
        {
            bool map=source==null && joker<0;
            if(source==null && joker>=0)source=Find(joker);
            BalanceEffectStats effect=null;
            if(source!=null) {
                int index=source.card.Definition.CardType==CardType.Joker ? -1 : source.card.Definition.EffectList.ToList().FindIndex(x=>e.Kind==BattleEventKind.AttackChanged ? x.Kind==EffectKind.ModifyAttack || x.Kind==EffectKind.ChainWeaken : x.Kind==EffectKind.IncreaseMaxHP);
                effect=EffectRow(source,index,index<0 ? "Joker" : source.card.Definition.EffectList[index].Kind.ToString(),index<0 ? "Ally" : source.card.Definition.EffectList[index].Target.ToString());
                if(index<0) {if(e.Kind==BattleEventKind.AttackChanged)source.stats.jokerAttackGain+=e.Amount;else source.stats.jokerHPGain+=e.Amount;}
            }
            if(map){if(e.Kind==BattleEventKind.AttackChanged)target.stats.mapAttackChange+=e.Amount;else target.stats.mapHPChange+=e.Amount;}
            if(!grants.TryGetValue(target.card.InstanceID,out var list)){list=new List<Grant>();grants.Add(target.card.InstanceID,list);}
            list.Add(new Grant{source=source?.stats,effect=effect,map=map,attack=e.Kind==BattleEventKind.AttackChanged ? e.Amount : 0,hp=e.Kind==BattleEventKind.MaxHPChanged ? Math.Max(0,e.Amount) : 0});
        }
        void Deal(Tracked source,Tracked target,int damage,bool effect,int beforeHP)
        {
            if(target!=null) {
                target.stats.damageReceived+=damage;
                if(source==null){target.stats.mapDamageReceived+=damage;if(damage>0)lastDamage.Remove(target.card.InstanceID);}
                if(source!=null && damage>0)lastDamage[target.card.InstanceID]=source.card.InstanceID;
            }
            if(source!=null) {
                if(target!=null && source.card.Owner==target.card.Owner)source.stats.selfDamage+=damage;
                else {
                    if(effect)source.stats.effectDamage+=damage;else source.stats.attackDamage+=damage;
                    if(target==null)source.stats.enemyHeroDamage+=damage;else source.stats.enemyUnitDamage+=damage;
                }
                if(!effect && grants.TryGetValue(source.card.InstanceID,out var buffs)) {
                    double positive=buffs.Sum(b=>Math.Max(0,b.attack)),negative=buffs.Sum(b=>Math.Max(0,-b.attack));
                    double extra=Math.Max(0,damage-Math.Min(beforeHP,Math.Max(0,source.card.Attack-positive)));
                    double prevented=Math.Max(0,Math.Min(beforeHP,source.card.Attack+negative)-damage);
                    foreach(var buff in buffs) {
                        if(buff.attack>0 && positive>0){double n=extra*buff.attack/positive;if(buff.map)source.stats.mapExtraDamage+=n;else {buff.source.buffExtraDamage+=n;buff.effect.extraDamage+=n;}}
                        if(buff.attack<0 && negative>0 && buff.source!=null){double n=prevented*-buff.attack/negative;buff.source.debuffPreventedDamage+=n;buff.effect.preventedDamage+=n;}
                    }
                }
            }
            if(target!=null && grants.TryGetValue(target.card.InstanceID,out var health)) {
                double pool=health.Sum(b=>b.hp),absorbed=Math.Min(pool,Math.Max(0,damage-Math.Max(0,beforeHP-pool)));
                foreach(var buff in health.Where(b=>b.hp>0)) {
                    double n=pool==0 ? 0 : absorbed*buff.hp/pool;buff.hp-=n;
                    if(buff.source!=null){buff.source.extraHPAbsorbed+=n;buff.effect.absorbedDamage+=n;}else if(buff.map)target.stats.mapHPAbsorbed+=n;
                }
            }
        }
        public void Effect(BattleEngine g,CardInstance card,CardEffect effect,CardInstance target,int actual,string outcome)
        {
            var source=Track(card);int index=card.Definition.EffectList.ToList().IndexOf(effect);
            var row=EffectRow(source,index,effect.Kind.ToString(),effect.Target.ToString());
            bool first=activations.Add(card.InstanceID+"|"+index);
            if(first){row.activations++;source.stats.effectActivations++;}
            row.applications++;source.stats.effectApplications++;
            if(outcome=="resolved" && fulfilled.Add(card.InstanceID+"|"+index)){row.conditionsMet++;source.stats.conditionsMet++;}
            if(actual!=0){row.successes++;source.stats.effectSuccesses++;}else {row.failures++;source.stats.effectFailures++;row.zeroResults++;}
            if(outcome=="invalidated" || outcome=="source_destroyed"){row.invalidated++;source.stats.invalidated++;}
            row.actualAmount+=actual;
            row.requestedAmount+=effect.Amount;
            switch(effect.Kind) {
                case EffectKind.Heal:source.stats.healing+=actual;break;
                case EffectKind.FirstAttackHeal:source.stats.healing+=actual;source.stats.afterAttackHealing+=actual;break;
                case EffectKind.GainMana:source.stats.manaGained+=actual;source.stats.manaWasted+=System.Math.Max(0,effect.Amount-actual);break;
                case EffectKind.Exchange:source.stats.draws+=actual;source.stats.exchangeDraws+=actual;if(outcome!="resolved")source.stats.exchangesSkipped++;break;
                case EffectKind.ModifyAttack:case EffectKind.ChainWeaken:if(actual>=0)source.stats.attackIncrease+=actual;else source.stats.attackDecrease-=actual;break;
                case EffectKind.IncreaseMaxHP:source.stats.extraHP+=actual;break;
                case EffectKind.DrawCard:source.stats.draws+=actual;break;
                case EffectKind.DiscardCard:source.stats.discardEffects+=actual;break;
            }
            Log("CardEffect",card.InstanceID,target?.InstanceID ?? -1,actual,effect.Kind.ToString(),outcome);
            if(detail!=null) {
                int after=effect.Kind==EffectKind.GainMana ? g.State.Players[card.Owner].CurrentMana
                    : effect.Kind==EffectKind.DrawCard || effect.Kind==EffectKind.Exchange ? g.State.Players[card.Owner].Hand.Count
                    : target==null ? 0 : effect.Kind==EffectKind.ModifyAttack || effect.Kind==EffectKind.ChainWeaken ? target.Attack : effect.Kind==EffectKind.IncreaseMaxHP ? target.MaxHP : System.Math.Max(0,target.CurrentHP);
                int before=effect.Kind==EffectKind.Damage || effect.Kind==EffectKind.SelfDamage || effect.Kind==EffectKind.RandomDamage || effect.Kind==EffectKind.ChainDamage ? after+actual : effect.Kind==EffectKind.Exchange && outcome=="resolved" ? after-actual+1 : after-actual;
                detail(new BalanceDetail{match=match,turn=g.State.TurnNumber,source=card.InstanceID,target=target?.InstanceID ?? -1,cardID=card.Definition.CardID,actor=card.Owner,effectIndex=index,kind="EffectMeasurement",effect=effect.Kind.ToString(),requested=effect.Amount,amount=actual,before=before,after=after,outcome=outcome});
            }
        }
        void CloseLife(Tracked card)
        {
            if(card.summoned<0)return;
            card.stats.survivalTurnSum+=Math.Max(0,engine.State.TurnNumber-card.summoned);card.summoned=-1;
        }
        public void Complete()
        {
            foreach(var tracked in cards.Values)if(tracked.summoned>=0){tracked.stats.censoredUnits++;CloseLife(tracked);}
            foreach(var row in Rows.Values)if(row.uses>0){row.matchesUsed=1;row.playerMatchesUsed=1;row.winsUsed=int.Parse(row.group)==engine.State.Winner ? 1 : 0;}
            engine.BalanceObserver=null;engine.SetBalanceRandomTrace(null);
        }
        public string[] Used(int owner) => cards.Values.Where(c=>c.card.Owner==owner && used.Contains(c.card.InstanceID)).Select(c=>c.card.Definition.CardID).Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray();
    }
}
#endif
