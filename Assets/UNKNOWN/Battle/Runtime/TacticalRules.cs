using System;
using System.Linq;
namespace Unknown.Battle
{
    public sealed partial class BattleEngine
    {
        int exchangeChosen;
        internal int ExchangeChosen => exchangeChosen;
        bool Modern(CardInstance c) => c!=null && c.Definition.CardType==CardType.Normal && c.Definition.Version==CardBalanceVersion.FullBalanceRework;
        static bool Passive(CardEffect e) => e.Kind==EffectKind.StrikeBonus || e.Kind==EffectKind.IgnoreProtection || e.Kind==EffectKind.KillHero || e.Kind==EffectKind.KillGrow || e.Kind==EffectKind.WeakStrike || e.Kind==EffectKind.AttackWeaken || e.Kind==EffectKind.FullStrike || e.Kind==EffectKind.TurnHealSelf || e.Kind==EffectKind.TurnGrow;
        bool Eligible(CardEffect e,CardInstance c) {
            if(e.Kind==EffectKind.WoundedDamage)return c.CurrentHP<c.MaxHP;
            if(e.Kind==EffectKind.WeakDamage || e.Kind==EffectKind.RemoveProtection)return c.CardWeakening>0;
            if(e.Kind==EffectKind.PriorWeakDamage)return chainHits.Contains(c.InstanceID);
            return true;
        }
        System.Collections.Generic.List<CardInstance> EffectTargets(CardEffect e) => Targets(e.Target).Where(c=>Eligible(e,c)).ToList();
        public int PlacementCost(CardInstance card) => Math.Max(1,(Single?.Cost(card) ?? card.Definition.ManaCost)-State.Players[card.Owner].NextDiscount);
        internal static int PlacementCost(CardViewData card,PlayerViewData p) => Math.Max(1,card.Definition.ManaCost-p.nextDiscount);
        void ConsumeDiscount(int owner) {State.Players[owner].NextDiscount=0;State.Players[owner].DiscountTurn=0;}
        void TacticalEndTurn(int owner) {
            foreach(var c in State.Players.SelectMany(p=>p.Slots).Where(c=>c!=null)) {
                if(c.TemporaryAttack!=0){int n=c.TemporaryAttack;c.ImportCore(c.RawAttack-n,c.RawHP);c.AbilityAttack-=n;c.TemporaryAttack=0;Emit(new BattleEvent(BattleEventKind.AttackChanged,c.Owner,target:c.InstanceID,amount:-n));}
                if(c.Owner==owner && c.CardWeakening>0 && State.Players[owner].TurnsStarted>=c.WeakUntil) {
                    int n=c.CardWeakening;c.ImportCore(c.RawAttack+n,c.RawHP);c.AbilityAttack+=n;c.CardWeakening=0;c.WeakUntil=0;
                    Emit(new BattleEvent(BattleEventKind.AttackChanged,c.Owner,target:c.InstanceID,amount:n));
                }
            }
            var ending=State.Players[owner];if(ending.DiscountTurn>0){ending.NextDiscount=0;ending.DiscountTurn=0;}
            ReconcilePoker();
        }
        void TacticalStartTurn(int owner) {
            State.Players[owner].CardManaGained=0;
            foreach(var c in State.Players[owner].Slots.Where(c=>c!=null && c.CurrentHP>0).ToArray()) {
                if(!Modern(c) || c.LastCardTurn==State.TurnNumber)continue;c.LastCardTurn=State.TurnNumber;
                foreach(var e in c.Definition.EffectList) {
                    if(e.Kind==EffectKind.TurnHealSelf)TacticalHeal(c,c,e.Amount,e);
                    if(e.Kind==EffectKind.TurnGrow && c.CurrentHP==c.MaxHP && c.GrowthCount<2){c.GrowthCount++;TacticalAttack(c,c,e.Amount,false,e);}
                }
            }
        }
        void TacticalHeal(CardInstance dealer,CardInstance target,int n,CardEffect e) {
            if(!IsOnBoard(target))return;int actual=Math.Min(n,target.MaxHP-target.CurrentHP);target.CurrentHP+=actual;
            Emit(new BattleEvent(BattleEventKind.Heal,target.Owner,dealer.InstanceID,target.InstanceID,actual,suit:dealer.Definition.Suit));
            MeasureTacticalHealing(dealer,actual);
            MeasureTactical(dealer,e,target,actual,"resolved");
        }
        void TacticalAttack(CardInstance dealer,CardInstance target,int n,bool temporary,CardEffect e) {
            if(!IsOnBoard(target))return;target.ImportCore(target.RawAttack+n,target.RawHP);target.AbilityAttack+=n;if(temporary)target.TemporaryAttack+=n;
            Emit(new BattleEvent(BattleEventKind.AttackChanged,target.Owner,dealer.InstanceID,target.InstanceID,n,suit:dealer.Definition.Suit));
            MeasureTactical(dealer,e,target,n,"resolved");
        }
        void TacticalWeaken(CardInstance dealer,CardInstance target,int n,CardEffect e) {
            int actual=Math.Min(n,Math.Min(3-target.CardWeakening,target.Attack));
            target.ImportCore(target.RawAttack-actual,target.RawHP);target.AbilityAttack-=actual;target.CardWeakening+=actual;
            if(target.CardWeakening>0)target.WeakUntil=State.Players[target.Owner].TurnsStarted+1;
            Emit(new BattleEvent(BattleEventKind.AttackChanged,target.Owner,dealer.InstanceID,target.InstanceID,-actual,suit:dealer.Definition.Suit));
            MeasureTactical(dealer,e,target,-actual,"resolved");
        }
        void Protect(CardInstance dealer,CardInstance target,int n,CardEffect e) {
            int gained=Math.Max(0,n-target.Protection);
            if(gained>0){target.Protection=n;target.ProtectionSource=dealer.InstanceID;}
            MeasureTactical(dealer,e,target,gained,"resolved");
        }
        int ProtectedDamage(CardInstance target,int damage,bool ignore=false) {
            if(damage<=0 || target.Protection<=0)return Math.Max(0,damage);
            int blocked=ignore ? 0 : Math.Min(damage,target.Protection);
            MeasureProtection(target.ProtectionSource,blocked);
            // A protection charge is consumed by the next positive hit, including a piercing attack.
            target.Protection=0;target.ProtectionSource=0;return Math.Max(0,damage-blocked);
        }
        void TacticalHero(CardInstance dealer,int n,CardEffect e) {
            var p=State.Players[1-dealer.Owner];int actual=Math.Min(n,p.HP);p.HP-=actual;
            Emit(new BattleEvent(BattleEventKind.HeroChanged,1-dealer.Owner,source:dealer.InstanceID,amount:-actual));
            MeasureTactical(dealer,e,null,actual,"resolved");if(p.HP<=0)FinishGame(dealer.Owner,VictoryReason.OpponentHPZero);
        }
        int TacticalStrike(CardInstance c,CardInstance target,int n) {
            if(!Modern(c))return n;
            foreach(var e in c.Definition.EffectList) {
                if(e.Kind==EffectKind.StrikeBonus || e.Kind==EffectKind.FullStrike && c.CurrentHP==c.MaxHP || e.Kind==EffectKind.WeakStrike && target?.CardWeakening>0){n+=e.Amount;MeasureTactical(c,e,target,e.Amount,"resolved");}
            }return n;
        }
        void TacticalBeforeAttack(CardInstance c,CardInstance target) {
            if(!Modern(c) || target==null)return;
            foreach(var e in c.Definition.EffectList) {
                if(e.Kind==EffectKind.AttackWeaken)TacticalWeaken(c,target,e.Amount,e);
                if(e.Kind==EffectKind.IgnoreProtection)MeasureTactical(c,e,target,target.Protection,"resolved");
            }
        }
        void TacticalAfterAttack(CardInstance c,CardInstance target,int actual) {
            if(!Modern(c) || target==null || target.CurrentHP>0 || actual<=0)return;
            foreach(var e in c.Definition.EffectList) {
                if(e.Kind==EffectKind.KillHero)TacticalHero(c,e.Amount,e);
                if(e.Kind==EffectKind.KillGrow && IsOnBoard(c)){TacticalHeal(c,c,e.Amount,e);TacticalAttack(c,c,1,false,e);}
            }
        }
        bool ResolveTactical(CardEffect e) {
            if(Passive(e)){effectIndex++;return true;}
            if(e.Kind==EffectKind.Discount) {
                var p=State.Players[source.Owner];p.NextDiscount=e.Amount;p.DiscountTurn=(source.Definition.Rank==Rank.Five || source.Definition.Rank==Rank.Eight) ? State.TurnNumber : 0;
                MeasureBalanceEffect(e,null,e.Amount,"resolved");effectIndex++;return true;
            }
            if(e.Kind==EffectKind.TacticalExchange) {
                if(State.Players[source.Owner].HandCards.Count==0){FinishTacticalExchange();return true;}
                waitingExchange=true;return true;
            }
            if(e.Kind==EffectKind.WeakenThenDamage) {
                var targets=Targets(EffectTarget.Enemy);var prior=targets.Where(c=>c.CardWeakening>0).ToArray();
                // Record the pre-application eligible IDs using the existing target continuation.
                foreach(var c in targets)TacticalWeaken(source,c,1,e);
                chainHits.Clear();foreach(var c in prior)chainHits.Add(c.InstanceID);
                effectIndex++;return false;
            }
            return false;
        }
        bool ApplyTactical(CardEffect e,CardInstance target) {
            var c=source;int before=target.CurrentHP,n=e.Amount;
            switch(e.Kind) {
                case EffectKind.CardWeaken:TacticalWeaken(c,target,n,e);return true;
                case EffectKind.Protection:Protect(c,target,n,e);return true;
                case EffectKind.TemporaryAttack:TacticalAttack(c,target,n,true,e);return true;
                case EffectKind.RemoveProtection:int removed=target.Protection;target.Protection=0;target.ProtectionSource=0;MeasureBalanceEffect(e,target,removed,"resolved");return true;
                case EffectKind.WoundedDamage:case EffectKind.WeakDamage:case EffectKind.DamageGrow:case EffectKind.DamageHero:case EffectKind.DamageTeamGrow:case EffectKind.WeakBonusDamage:case EffectKind.DiamondBonusDamage:case EffectKind.PriorWeakDamage:
                    if(e.Kind==EffectKind.WeakBonusDamage && target.CardWeakening>0)n+=3;
                    if(e.Kind==EffectKind.DiamondBonusDamage && State.Players[c.Owner].Slots.Count(a=>a!=null && a!=c && a.CurrentHP>0 && a.Definition.Suit==Suit.Diamond)>=2)n+=2;
                    DamageUnit(target,n,c.InstanceID,c.Definition.Suit);
                    MeasureBalanceEffect(e,target,Math.Min(before,Math.Max(0,before-target.CurrentHP)),"resolved");
                    if(target.CurrentHP<=0){
                        if(e.Kind==EffectKind.DamageGrow)TacticalAttack(c,c,1,false,e);
                        if(e.Kind==EffectKind.DamageHero)TacticalHero(c,3,e);
                        if(e.Kind==EffectKind.DamageTeamGrow)foreach(var a in State.Players[c.Owner].Slots.Where(a=>a!=null).ToArray())TacticalAttack(c,a,1,false,e);
                    }
                    RemoveDead();return true;
                case EffectKind.HealProtect:case EffectKind.HealFullAttack:
                    int recovered=0;
                    foreach(var a in State.Players[c.Owner].Slots.Where(a=>a!=null && a.CurrentHP>0).ToArray()){
                        if(a.CurrentHP<a.MaxHP)recovered++;TacticalHeal(c,a,n,e);
                        if(e.Kind==EffectKind.HealFullAttack && a.CurrentHP==a.MaxHP)TacticalAttack(c,a,1,true,e);
                    }
                    if(e.Kind==EffectKind.HealProtect)Protect(c,c,Math.Min(3,recovered),e);return true;
            }return false;
        }
        void FinishTacticalExchange() {
            var e=source.Definition.EffectList[effectIndex];
            int draws=e.Amount==4 ? exchangeChosen+2 : exchangeChosen>0 ? e.Amount : 0;
            int before=State.Players[source.Owner].HandCards.Count;Draw(source.Owner,draws);
            MeasureBalanceEffect(e,null,State.Players[source.Owner].HandCards.Count-before,"resolved");
            waitingExchange=false;exchangeChosen=0;effectIndex++;
        }
        partial void MeasureTactical(CardInstance dealer,CardEffect e,CardInstance target,int actual,string outcome);
        partial void MeasureTacticalHealing(CardInstance dealer,int actual);
        partial void MeasureProtection(int sourceID,int prevented);
    }
}
