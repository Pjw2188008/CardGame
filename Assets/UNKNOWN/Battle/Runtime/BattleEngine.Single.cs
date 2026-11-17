using System;
using System.Linq;
using System.Collections.Generic;
namespace Unknown.Battle
{
    public sealed class SingleBattleOptions
    {
        public SingleStage stage;public SingleDifficulty difficulty;public SingleAugment[] augmentData;public string[] deck,augments;public int layer=-1;public int hp,maxHP,seed;public bool tutorial;
    }
    [Serializable] public sealed class SingleEnemyUse { public string card;public int count; }
    [Serializable] public sealed class SingleWeakening { public int id,amount,expires; }
    [Serializable] public sealed class SingleBattleProgress
    {
        public int enemyTurn,enemyMana,enemySpent,enemySummoned;public List<SingleEnemyUse> enemyUses=new List<SingleEnemyUse>();
        public int spent,turnSpent,attacks,directAttacks,shield,weakenSource=-1,weakenIndex=-1,weakenTurn=-1;
        public List<string> usedCards=new List<string>();
        public List<int> pendingEntrances=new List<int>();public string failure="";
        public bool firstSummon,firstExtraDraw,firstWeakening,firstHeal,researchUsed,strongestSpent;
        public bool[] once=new bool[12];public List<int> patterns=new List<int>();public List<SingleWeakening> weakening=new List<SingleWeakening>();
    }
    [Serializable] public sealed class SingleBattleSave
    {
        public BattleSnapshot snapshot;public CardViewData[] firstDeck,secondDeck,firstDiscard,secondDiscard;
        public int[] turnsStarted,randomBounds;public SingleBattleProgress progress;
    }
    public sealed class SingleRandom : IRandomSource
    {
        System.Random random;readonly int seed;public readonly List<int> Bounds=new List<int>();
        public SingleRandom(int seed){this.seed=seed;random=new System.Random(seed);}
        public int Next(int max){Bounds.Add(max);return random.Next(max);}
        public void Restore(int[] bounds){random=new System.Random(seed);Bounds.Clear();foreach(int max in bounds??new int[0])Next(max);}
    }
    public sealed class SingleBattleSession
    {
        readonly BattleEngine g;public readonly SingleBattleOptions Options;public SingleBattleProgress Progress=new SingleBattleProgress();
        readonly Dictionary<int,int> dealers=new Dictionary<int,int>();bool inEvent;public string Failure=>Progress.failure;
        public int Round=>(g.State.TurnNumber+1)/2;
        public int Limit=>Options.stage.ResolveTurnLimit(Options.difficulty,Options.layer);
        public int Budget=>Options.stage.ResolveManaBudget(Options.difficulty);
        public int RemainingBudget=>Math.Max(0,Budget-Progress.spent);
        public IEnumerable<SingleRestriction> Restrictions {
            get {
                var list=Options.stage.restrictions.AsEnumerable();
                if(Options.difficulty.extraRestrictions>0)list=list.Concat(new[]{SingleRestriction.FirstCost,SingleRestriction.AttackLimit,SingleRestriction.HalfHealing}.Take(Options.difficulty.extraRestrictions));
                return list.Distinct();
            }
        }
        public bool Has(SingleRestriction r)=>Restrictions.Contains(r) || Restrictions.Contains(SingleRestriction.Changing) && r==(SingleRestriction)(1+(Round-1)%12);
        public int Amount(int i,int fallback=1)=>Options.augmentData?.FirstOrDefault(a=>a.id==i.ToString())?.amount ?? fallback;
        public bool Aug(int i)=>Options.augments.Contains(i.ToString());
        public SingleBattleSession(BattleEngine engine,SingleBattleOptions options){g=engine;Options=options;Progress.shield=Aug(4)?Amount(4,4):0;}
        internal int WeakeningDuration(CardInstance dealer,int duration) {
            if(dealer.Owner==0 && Aug(2)) {
                if(!Progress.firstWeakening){Progress.firstWeakening=true;Progress.weakenSource=dealer.InstanceID;Progress.weakenIndex=g.PendingIndex;Progress.weakenTurn=g.State.TurnNumber;}
                if(Progress.weakenSource==dealer.InstanceID && Progress.weakenIndex==g.PendingIndex && Progress.weakenTurn==g.State.TurnNumber)duration+=Options.augmentData?.FirstOrDefault(a=>a.id=="2")?.duration ?? 1;
            }return duration;
        }
        public int Cost(CardInstance c)=>c.Definition.ManaCost+(Has(SingleRestriction.FirstCost) && !Progress.firstSummon && c.Definition.CardType!=CardType.Joker?Options.stage.firstCost:0);
        public bool Payable(int amount)=>amount<=RemainingBudget && Progress.turnSpent+amount<=Options.stage.turnSpendLimit;
        public string SummonError(CardInstance c,int slot) {
            if(c.Owner!=0)return "";
            if(!Payable(g.PlacementCost(c)))return "턴당 마나 사용 상한 또는 총 마나 예산이 부족합니다.";
            if(Has(SingleRestriction.Parity) && c.Definition.CardType==CardType.Normal && (PokerSynergy.RankValue(c.Definition.Rank)%2)!=(Round%2))return "이번 턴에는 "+(Round%2==0?"짝수":"홀수")+" 계급만 소환할 수 있습니다.";
            return "";
        }
        public string AttackError(CardInstance c,int slot) {
            if(c.Owner!=0)return "";
            if(Has(SingleRestriction.AttackLimit) && Progress.attacks>=Options.stage.attackLimit)return $"이번 턴 공격은 최대 {Options.stage.attackLimit}회입니다.";
            if(slot<0 && Has(SingleRestriction.DirectLimit) && Progress.directAttacks>=Options.stage.directLimit)return $"이번 턴 직접 공격은 최대 {Options.stage.directLimit}회입니다.";
            return "";
        }
        public bool Seal(CardInstance c) => c.Owner==0 && (Has(SingleRestriction.SealFirstSummon) && !Progress.firstSummon || Has(SingleRestriction.SealSuit) && c.Definition.Suit==Suit.Spade);
        public bool PokerEligible(CardInstance c)=>c.Owner!=0 || !Has(SingleRestriction.SealSlot) || g.State.Players[0].Slots[Options.stage.sealedSlot]!=c;
        public int Damage(CardInstance unit,CardInstance target,int amount) {
            if(unit.Owner!=0)return amount;
            if(Has(SingleRestriction.StrongestPenalty) && !Progress.strongestSpent && unit.Attack==g.State.Players[0].Board.Where(c=>c!=null).Max(c=>c.Attack)){amount=Math.Max(0,amount-Options.stage.strongestPenalty);Progress.strongestSpent=true;}
            if(Aug(1) && !Progress.once[1] && unit.Definition.Suit==Suit.Diamond){amount+=Amount(1,2);Progress.once[1]=true;}
            if(Aug(3) && !Progress.once[3] && target!=null && target.AbilityAttack<0){amount+=Amount(3,2);Progress.once[3]=true;}
            if(Aug(11) && !Progress.once[11] && Round==Limit){amount+=Amount(11,3);Progress.once[11]=true;}
            return amount;
        }
        public bool ExpireBeforeTurnEffects(bool endingPlayerTurn=false) {
            if(g.State.IsOver || Round<Limit || Round==Limit && g.State.ActivePlayer==0 && !endingPlayerTurn)return false;
            Progress.failure="TURN LIMIT";g.SingleFinish(1);return true;
        }
        public void Turn(int owner) {
                foreach(var weak in Progress.weakening.Where(w=>w.expires<=g.State.TurnNumber).ToArray()) {
                    var c=g.SingleFind(weak.id);if(c!=null && g.State.Players[c.Owner].Board.Contains(c)){c.Attack+=weak.amount;c.AbilityAttack+=weak.amount;g.SingleEmit(new BattleEvent(BattleEventKind.AttackChanged,c.Owner,target:c.InstanceID,amount:weak.amount));}Progress.weakening.Remove(weak);
                }

            if(owner==0) {
                if(ExpireBeforeTurnEffects())return;
                Progress.turnSpent=Progress.attacks=Progress.directAttacks=0;Progress.firstSummon=false;Progress.firstExtraDraw=false;Progress.strongestSpent=false;
                foreach(int i in new[]{0,1,3,6,7,11})Progress.once[i]=false;
                if(Has(SingleRestriction.Changing))g.ReconcilePoker();
                if(Has(SingleRestriction.SwapSlots) && Round>1){var slots=g.State.Players[0].Slots;var tmp=slots[0];slots[0]=slots[4];slots[4]=tmp;g.ReconcilePoker();}
            }else {PrepareEnemyTurn();Patterns();}
        }
        public void Patterns() {
            if(g.State.IsOver)return;
            for(int i=0;i<Options.stage.patterns.Length;i++) {
                var p=Options.stage.patterns[i];if(Progress.patterns.Contains(i))continue;
                bool trigger=p.condition=="half" ? (long)g.State.Players[1].HP*100<=Options.stage.enemyHP*p.hpPercent : p.condition=="death" ? g.State.Players[1].DiscardCards.Any(c=>string.IsNullOrEmpty(p.watchCard) || c.Definition.CardID==p.watchCard) : p.condition=="unknown" ? g.State.Players[0].Board.Any(c=>c?.Definition.CardType==CardType.Unknown) : g.State.ActivePlayer==1 && Round>=p.turn;
                if(!trigger)continue;
                if(p.kind==SinglePatternKind.Summon) {
                    if(g.State.ActivePlayer!=1 || !EnemySummon(p.card,p.slot,true))continue;
                    Progress.patterns.Add(i);continue;
                }
                Progress.patterns.Add(i);int amount=p.amount+Options.difficulty.patternBonus;
                foreach(var c in g.State.Players[p.kind==SinglePatternKind.Weaken?0:1].Board.Where(c=>c!=null).ToArray()) {
                    if(p.kind==SinglePatternKind.Rally)g.SingleStats(c,amount,amount);else g.SingleStats(c,p.kind==SinglePatternKind.Weaken?-amount:amount,0);
                }
            }
            g.SingleResolveEntrances();
        }
        int EnemyLimit=>Math.Min(2,Options.stage.enemySummonsPerTurn+(Options.stage.kind==SingleNodeKind.Battle?0:Round>=Options.stage.enemyExtraTurn || (long)g.State.Players[1].HP*100<=Options.stage.enemyHP*Options.stage.enemyExtraHP?1:0));
        void PrepareEnemyTurn() {
            if(Progress.enemyTurn==g.State.TurnNumber)return;
            bool first=Progress.enemyTurn==0;Progress.enemyTurn=g.State.TurnNumber;Progress.enemySummoned=0;
            Progress.enemyMana=Math.Min(Options.stage.enemyManaCap,first?Options.stage.enemyStartMana:Progress.enemyMana+Options.stage.enemyManaRecovery);
            g.State.Players[1].CurrentMana=Progress.enemyMana;g.State.Players[1].MaxMana=Options.stage.enemyManaCap;
        }
        bool EnemySummon(string id,int slot,bool scripted=false) {
            if(g.State.IsOver || g.State.ActivePlayer!=1 || g.NeedsTarget || Progress.enemySummoned>=EnemyLimit || g.State.Players[1].Board.All(c=>c!=null))return false;
            if(!Options.stage.enemyPool.Any(e=>e.card==id && Eligible(e,scripted)))return false;
            int cost=CardDatabase.Get(id).ManaCost;
            if(cost>Progress.enemyMana || cost>Options.stage.enemyBudget-Progress.enemySpent)return false;
            Progress.enemyMana-=cost;g.State.Players[1].CurrentMana=Progress.enemyMana;Progress.enemySpent+=cost;Progress.enemySummoned++;
            var use=Progress.enemyUses.FirstOrDefault(u=>u.card==id);if(use==null){use=new SingleEnemyUse{card=id};Progress.enemyUses.Add(use);}use.count++;
            g.SinglePlace(id,slot,false);g.SingleResolveEntrances();return true;
        }
        bool Eligible(SingleEnemyCard e,bool scripted=false) {
            var board=g.State.Players[1].Board;
            return (scripted || e.condition!="pattern") && Round>=e.minTurn && (e.maxTurn==0 || Round<=e.maxTurn) &&
                (e.maxUses==0 || (Progress.enemyUses.FirstOrDefault(u=>u.card==e.card)?.count??0)<e.maxUses) &&
                (e.condition!="half" || (long)g.State.Players[1].HP*100<=Options.stage.enemyHP*50) &&
                (e.condition!="empty" || board.All(c=>c==null)) && (e.condition!="ally" || board.Any(c=>c!=null)) &&
                CardDatabase.Get(e.card).ManaCost<=Math.Min(Progress.enemyMana,Options.stage.enemyBudget-Progress.enemySpent);
        }
        bool SummonFromPool() {
            if(g.State.Players[1].Board.All(c=>c!=null))return false;
            var pool=Options.stage.enemyPool.Where(e=>Eligible(e)).ToArray();if(pool.Length==0)return false;
            int priority=pool.Max(e=>e.priority);pool=pool.Where(e=>e.priority==priority).ToArray();
            int roll=g.SingleRoll(pool.Sum(e=>e.weight));
            foreach(var e in pool){roll-=e.weight;if(roll<0)return EnemySummon(e.card,e.preferredSlot);}
            return false;
        }
        public string Intent() {
            int next=g.State.ActivePlayer==0?g.State.TurnNumber+1:g.State.TurnNumber;
            var target=g.State.Players[0].Board.Where(c=>c!=null).OrderBy(c=>c.CurrentHP).FirstOrDefault();
            var attacks=g.State.Players[1].Board.Where(c=>c!=null && c.SummonedTurn<next && (g.State.ActivePlayer==0 || !c.HasAttacked)).Select(c=>c.Definition.CardName+" → "+(target?.Definition.CardName??"플레이어")+" 예상 "+Math.Max(0,Math.Min(c.Attack,target?.CurrentHP??g.State.Players[0].HP)-(target==null?Progress.shield:0)));
            var pending=Options.stage.patterns.Where((p,i)=>!Progress.patterns.Contains(i)).Select(p=>
                (p.condition=="turn"?$"{p.turn}턴":p.condition=="half"?$"적 HP {p.hpPercent}% 이하":p.condition=="death"?"지정 적 유닛 사망":"UNKNOWN 소환")+": "+
                (p.kind==SinglePatternKind.Summon?CardDatabase.Get(p.card).CardName+" 소환":p.kind==SinglePatternKind.Weaken?"아군 공격력 -"+(p.amount+Options.difficulty.patternBonus):p.kind==SinglePatternKind.Rally?"적 공격·체력 +"+(p.amount+Options.difficulty.patternBonus):"적 공격력 +"+(p.amount+Options.difficulty.patternBonus)));
            string summon="다음 적 턴 소환 "+(Options.stage.kind==SingleNodeKind.Battle?Options.stage.enemySummonsPerTurn.ToString():"1~2")+"장 · "+Options.stage.enemyType;
            if(g.State.Players[1].Board.All(c=>c!=null))summon+=" (현재 필드 가득 참)";
            if(Progress.enemySpent>=Options.stage.enemyBudget)summon="소환 예산 소진";
            return summon+"\n"+string.Join(" / ",attacks.Concat(pending));
        }
        public bool EnemyStep() {
            if(g.State.ActivePlayer!=1 || g.State.IsOver)return false;
            PrepareEnemyTurn();Patterns();if(g.State.IsOver)return false;
            bool acted;
            if(Progress.enemySummoned<EnemyLimit && SummonFromPool())acted=true;
            else {
                var attacker=g.State.Players[1].Board.FirstOrDefault(c=>g.CanAttack(1,c));
                if(attacker==null)return g.EndTurn(1);
                var board=g.State.Players[0].Board;int slot=Enumerable.Range(0,board.Count).Where(i=>board[i]!=null).OrderBy(i=>board[i].CurrentHP).DefaultIfEmpty(-1).First();
                acted=g.Attack(1,attacker.InstanceID,slot);
            }
            if(acted && !g.State.IsOver)Patterns();
            return acted;
        }

        public bool Recycle(int owner) {
            if(owner!=0)return false;var p=g.State.Players[owner];
            var cards=p.DiscardCards.Where(c=>c.Definition.CardType!=CardType.Unknown).ToArray();
            if(cards.Length==0)return false;
            foreach(var c in cards){p.DiscardCards.Remove(c);c.PokerAttack=c.PokerHP=0;c.ImportCore(c.Definition.Attack,c.Definition.MaxHP);c.CurrentHP=c.MaxHP;c.AbilityAttack=c.AbilityHP=c.MapAttack=c.MapHP=c.JokerAttack=c.JokerHP=0;c.HasAttacked=c.JokerUsed=c.FirstAttackHealSpent=c.PokerStraightAttackSpent=c.HeartMapHealSpent=false;c.Protection=c.ProtectionSource=c.CardWeakening=c.WeakUntil=c.TemporaryAttack=c.GrowthCount=0;c.LastCardTurn=-1;c.AttackWeakTurns=0;c.HealthWeakTurns=0;c.HealthWeakening=0;c.HealthWeakCurrent=0;c.AttackWeakAppliedTurn=-1;c.HealthWeakAppliedTurn=-1;c.LastWeakTickTurn=-1;c.AttackWeakSource=0;c.HealthWeakSource=0;p.DeckCards.Add(c);}
            g.SingleShuffle(p.DeckCards);return true;
        }
        public bool CancelDraw(int owner) {
            if(owner!=0 || !Has(SingleRestriction.CancelExtraDraw) || Progress.firstExtraDraw || g.State.TurnNumber<=1)return false;
            // BeginTurn marks its ordinary draw explicitly; only effect/reward draws are restricted.
            if(g.SingleTurnDrawing)return false;Progress.firstExtraDraw=true;return true;
        }
        public BattleEvent Event(BattleEvent e) {
            if(inEvent)return e;
            inEvent=true;
            try {
                if(e.Kind==BattleEventKind.ManaGained && e.Owner==1){Progress.enemyMana=Math.Min(Options.stage.enemyManaCap,g.State.Players[1].CurrentMana);g.State.Players[1].CurrentMana=Progress.enemyMana;}
                var unit=g.SingleFind(e.TargetID);var dealer=g.SingleFind(e.SourceID);
                if(e.Owner==0 && (e.Kind==BattleEventKind.Summon || e.Kind==BattleEventKind.JokerTravel)){var used=e.Kind==BattleEventKind.Summon?unit:dealer;if(used!=null && !Progress.usedCards.Contains(used.Definition.CardID))Progress.usedCards.Add(used.Definition.CardID);}
                if(e.Kind==BattleEventKind.ManaGained && e.Owner==0 && Has(SingleRestriction.LessMana) && e.Amount>0){int lost=Math.Min(e.Amount,Options.stage.manaReduction);g.State.Players[0].CurrentMana-=lost;e=Replace(e,e.Amount-lost);}
                if(e.Kind==BattleEventKind.ManaSpent && e.Owner==0){Progress.spent+=e.Amount;Progress.turnSpent+=e.Amount;}
                if(e.Kind==BattleEventKind.Summon && e.Owner==0) {
                    if(Aug(6) && !Progress.once[6] && unit?.Definition.Suit==Suit.Club){Progress.once[6]=true;Mana(Amount(6));}
                    if(Aug(9) && unit?.Definition.CardType==CardType.Unknown)g.Draw(0,Amount(9));
                    if(Has(SingleRestriction.SummonBlood)){var p=g.State.Players[0];p.HP=Math.Max(0,p.HP-Options.stage.summonBlood);if(p.HP==0)g.SingleFinish(1);}
                }
                if(e.Kind==BattleEventKind.Attack) {
                    if(e.TargetID>=0){dealers[e.TargetID]=e.SourceID;dealers[e.SourceID]=e.TargetID;}
                    if(e.Owner==0){Progress.attacks++;if(e.TargetID<0)Progress.directAttacks++;}
                    if(e.Owner==1 && e.TargetID<0 && Progress.shield>0){int absorb=Math.Min(e.Amount,Progress.shield);Progress.shield-=absorb;g.State.Players[0].HP+=absorb;e=Replace(e,e.Amount-absorb);}
                }
                if(e.Kind==BattleEventKind.Damage && e.TargetID>=0)dealers[e.TargetID]=e.SourceID;
                if(e.Kind==BattleEventKind.Destroy && e.Owner==1 && Aug(0) && !Progress.once[0] && dealers.TryGetValue(e.TargetID,out int id) && g.SingleFind(id)?.Definition.Suit==Suit.Diamond){Progress.once[0]=true;Mana(Amount(0));}
                if(e.Kind==BattleEventKind.Heal && e.Owner==0) {
                    if(Has(SingleRestriction.HalfHealing) && unit!=null){int amount=e.Amount*Options.stage.healingPercent/100;unit.CurrentHP-=e.Amount-amount;e=Replace(e,amount);}
                    if(e.Amount>0 && Aug(5) && !Progress.firstHeal){Progress.firstHeal=true;g.Draw(0,Amount(5));}
                }
                if(e.Kind==BattleEventKind.HeroChanged && e.Owner==0) {
                    if(e.Amount>0 && Has(SingleRestriction.HalfHealing)){int amount=e.Amount*Options.stage.healingPercent/100;g.State.Players[0].HP-=e.Amount-amount;e=Replace(e,amount);}
                    else if(e.Amount<0 && Progress.shield>0){int absorb=Math.Min(-e.Amount,Progress.shield);Progress.shield-=absorb;g.State.Players[0].HP+=absorb;e=Replace(e,e.Amount+absorb);}
                }
                if(e.Kind==BattleEventKind.Destroy)Progress.weakening.RemoveAll(w=>w.id==e.TargetID);
                if(e.Kind==BattleEventKind.AttackChanged && e.Amount<0 && dealer!=null && dealer.Definition.Suit==Suit.Spade && unit!=null) {
                    if(!g.TimedSpade(dealer)){
                        int duration=WeakeningDuration(dealer,1);
                        Progress.weakening.Add(new SingleWeakening{id=unit.InstanceID,amount=-e.Amount,expires=g.State.TurnNumber+duration*2});
                    }
                }
                if(e.Kind==BattleEventKind.Discard && e.Owner==0 && e.Secondary==2 && Aug(7) && !Progress.once[7]){Progress.once[7]=true;g.Draw(0,Amount(7));}
                if(e.Kind==BattleEventKind.JokerSuccess && e.Owner==0 && Aug(10))g.Draw(0,Amount(10));
                if(e.Kind==BattleEventKind.Combine && e.Owner==0 && Aug(8) && !Progress.researchUsed){Progress.researchUsed=true;g.SingleSearch();}
            }finally{inEvent=false;}
            return e;
        }
        static BattleEvent Replace(BattleEvent e,int amount)=>new BattleEvent(e.Kind,e.Owner,e.SourceID,e.TargetID,amount,e.Secondary,e.Slot,e.TargetOwner,e.Suit,e.Chain,e.Definition);
        public void Mana(int amount) {
            if(Has(SingleRestriction.LessMana))amount=Math.Max(0,amount-Options.stage.manaReduction);var p=g.State.Players[0];int before=p.CurrentMana;p.CurrentMana=Math.Min(g.Config.ManaCap,p.CurrentMana+amount);
            g.SingleEmit(new BattleEvent(BattleEventKind.ManaGained,0,amount:p.CurrentMana-before));
        }
    }
    public sealed partial class BattleEngine
    {
        public SingleBattleSession Single {get;private set;}
        internal bool SingleTurnDrawing;
        internal CardInstance SingleFind(int id)=>State.Players.SelectMany(p=>p.Hand.Concat(p.Board).Concat(p.DeckCards).Concat(p.DiscardCards)).FirstOrDefault(c=>c!=null && c.InstanceID==id);
        internal void SingleEmit(BattleEvent e)=>Emit(e);
        internal int SingleRoll(int max)=>random.Next(max);
        internal void SingleFinish(int winner)=>FinishGame(winner,VictoryReason.OpponentHPZero);
        internal void SingleShuffle(List<CardInstance> deck){for(int i=deck.Count-1;i>0;i--){int j=random.Next(i+1);var t=deck[i];deck[i]=deck[j];deck[j]=t;}}
        internal void SingleStats(CardInstance c,int attack,int hp) {
            int a=c.Attack;c.Attack=Math.Max(0,c.Attack+attack);c.AbilityAttack+=c.Attack-a;
            if(hp!=0){c.MaxHP+=hp;c.CurrentHP+=hp;c.AbilityHP+=hp;Emit(new BattleEvent(BattleEventKind.MaxHPChanged,c.Owner,target:c.InstanceID,amount:hp));}
            if(a!=c.Attack)Emit(new BattleEvent(BattleEventKind.AttackChanged,c.Owner,target:c.InstanceID,amount:c.Attack-a));
        }
        internal void SinglePlace(string id,int slot,bool initial) {
            if(State.IsOver || string.IsNullOrEmpty(id))return;
            var p=State.Players[1];if(slot<0 || slot>=5 || p.Slots[slot]!=null)slot=Array.FindIndex(p.Slots,x=>x==null);if(slot<0)return;
            var c=NewCard(CardDatabase.Get(id,rules.CardVersion),1);p.Slots[slot]=c;c.SummonedTurn=initial?-1:State.TurnNumber;
            SingleStats(c,Single.Options.difficulty.attackBonus,Single.Options.difficulty.hpBonus);
            Emit(new BattleEvent(BattleEventKind.Summon,1,source:c.InstanceID,target:c.InstanceID,slot:slot,definition:c.Definition));ReconcilePoker();
            if(!initial)Single.Progress.pendingEntrances.Add(c.InstanceID);
        }
        internal void SingleResolveEntrances() {
            // Never overwrite a player's unresolved summon. Resume queued enemy effects after it completes.
            if(source!=null || State.IsOver)return;
            while(Single.Progress.pendingEntrances.Count>0 && !State.IsOver) {
                int id=Single.Progress.pendingEntrances[0];Single.Progress.pendingEntrances.RemoveAt(0);var card=SingleFind(id);
                if(card==null || !State.Players[1].Board.Contains(card))continue;
                source=card;effectIndex=0;selectedTargets.Clear();chainHits.Clear();ResolveEffects();
                int guard=0;
                while(NeedsTarget && !State.IsOver && guard++<100) {
                    if(NeedsExchange){CompleteExchange(-1);continue;}
                    bool selected=false;
                    foreach(int owner in new[]{0,1}) {
                        for(int slot=0;slot<rules.FieldSlots && !selected;slot++)if(IsValidEffectTarget(owner,slot)){ApplySelectedTarget(owner,slot);selected=true;}
                        if(selected)break;
                    }
                    if(!selected)ResolveEffects();
                }
                if(NeedsTarget && !State.IsOver)throw new InvalidOperationException("Enemy entrance effect failed to resolve");
            }
        }
        IEnumerable<CardDefinition> SingleDeck(int owner) {
            if(owner!=0)return Enumerable.Empty<CardDefinition>();
            var ids=Single.Options.tutorial ? Single.Options.deck.Skip(2).Concat(new[]{"Heart_A","Diamond_J"}) : Single.Options.deck.AsEnumerable();
            return ids.Select(id=>CardDatabase.Get(id,rules.CardVersion));
        }
        void SinglePrepareTeachingHand() {
            var deck=State.Players[0].DeckCards;
            foreach(string id in new[]{"Heart_A","Diamond_J"}) {var card=deck.Last(c=>c.Definition.CardID==id);deck.Remove(card);deck.Add(card);}
        }
        internal void SingleSearch() {
            var p=State.Players[0];var c=p.DeckCards.FirstOrDefault(x=>x.Definition.CardType==CardType.Normal && (x.Definition.Rank==Rank.Ace || x.Definition.Rank>=Rank.Jack));
            if(c==null)return;p.DeckCards.Remove(c);p.DeckCards.Add(c);Draw(0,1);
        }
        public static BattleEngine CreateSingle(SingleBattleOptions options,SingleBattleSave save=null) {
            var g=new BattleEngine(options.seed,new BattleConfig{Single=options,StartingHP=options.maxHP,StartingHand=5,StartingMana=options.stage.startMana,Map=options.stage.map},new SingleRandom(options.seed));
            if(save!=null)g.RestoreSingle(save);return g;
        }
        public SingleBattleSave SaveSingle() {
            return new SingleBattleSave{snapshot=BattleSnapshot.For(this,0,0,0,false,new BattleEvent[0]),firstDeck=State.Players[0].DeckCards.Select(CardViewData.From).ToArray(),secondDeck=State.Players[1].DeckCards.Select(CardViewData.From).ToArray(),firstDiscard=State.Players[0].DiscardCards.Select(CardViewData.From).ToArray(),secondDiscard=State.Players[1].DiscardCards.Select(CardViewData.From).ToArray(),turnsStarted=State.Players.Select(p=>p.TurnsStarted).ToArray(),randomBounds=((SingleRandom)random).Bounds.ToArray(),progress=Single.Progress};
        }
        void RestoreSingle(SingleBattleSave save) {
            var s=save.snapshot;State.ActivePlayer=s.active;State.TurnNumber=s.turn;State.Winner=s.winner;State.VictoryReason=s.victoryReason;State.TurnExpired=s.expired;
            for(int owner=0;owner<2;owner++) {
                var p=State.Players[owner];var v=s.players[owner];p.HandCards.Clear();p.DeckCards.Clear();p.DiscardCards.Clear();
                p.HP=v.hp;p.NextDiscount=v.nextDiscount;p.DiscountTurn=v.discountTurn;p.CardManaGained=v.cardManaGained;p.MaxMana=v.maxMana;p.CurrentMana=v.mana;p.TurnsStarted=save.turnsStarted[owner];p.Poker=v.poker.Copy();p.ManualDiscardUsed=v.manualDiscardUsed;p.UnknownCreated=v.unknownCreated;p.JokerSuccesses=v.jokerSuccesses;
                p.HandCards.AddRange(v.hand.Select(c=>c.ToCard()));p.DeckCards.AddRange((owner==0?save.firstDeck:save.secondDeck).Select(c=>c.ToCard()));p.DiscardCards.AddRange((owner==0?save.firstDiscard:save.secondDiscard).Select(c=>c.ToCard()));
                for(int i=0;i<p.Slots.Length;i++)p.Slots[i]=v.board[i].ToCard();
            }
            nextID=State.Players.SelectMany(p=>p.Hand.Concat(p.Board).Concat(p.DeckCards).Concat(p.DiscardCards)).Where(c=>c!=null).Select(c=>c.InstanceID).DefaultIfEmpty(0).Max();
            source=SingleFind(s.pendingSource);effectIndex=s.pendingIndex;waitingExchange=s.exchangePending;exchangeChosen=s.exchangeChosen;selectedTargets.Clear();chainHits.Clear();
            if(s.pendingTargetKinds!=null)for(int i=0;i<s.pendingTargetKinds.Length;i++)selectedTargets[(EffectTarget)s.pendingTargetKinds[i]]=s.pendingTargetIDs[i];
            if(s.pendingDistinctIDs!=null)foreach(int id in s.pendingDistinctIDs)chainHits.Add(id);
            Single.Progress=save.progress;((SingleRandom)random).Restore(save.randomBounds);State.Entries.Clear();State.Entries.AddRange(s.log);State.EntryOwners.Clear();events.Clear();Single.ExpireBeforeTurnEffects();
        }
    }
}
