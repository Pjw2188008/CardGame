using System;
using System.Linq;
namespace Unknown.Battle {
    public sealed partial class BattleEngine {
        internal static bool IsWeakened(CardInstance c) => c!=null && (c.CardWeakening>0 || c.HealthWeakening>0);
        internal bool TimedSpade(CardInstance c) => Modern(c) && c.Definition.Suit==Suit.Spade;
        int WeakDuration(CardInstance source,CardEffect e) => Single?.WeakeningDuration(source,Math.Max(1,e.Duration)) ?? Math.Max(1,e.Duration);
        void ApplyTimedAttackWeak(CardInstance dealer,CardInstance target,int amount,CardEffect e) {
            int before=target.Attack,delta=Math.Min(amount,Math.Max(0,3-target.CardWeakening));
            target.ImportCore(target.RawAttack-delta,target.RawHP);target.AbilityAttack-=delta;target.CardWeakening+=delta;
            if(target.CardWeakening>0){target.AttackWeakTurns=Math.Max(target.AttackWeakTurns,WeakDuration(dealer,e));target.AttackWeakAppliedTurn=State.TurnNumber;target.AttackWeakSource=dealer.InstanceID;}
            target.WeakUntil=0;
            int actual=before-target.Attack;
            Emit(new BattleEvent(BattleEventKind.AttackChanged,target.Owner,dealer.InstanceID,target.InstanceID,-actual,suit:dealer.Definition.Suit));
            MeasureTactical(dealer,e,target,-actual,"resolved");MeasureWeakApplied(dealer,false,actual,delta>0 || target.CardWeakening>0);
        }
        void ApplyTimedHealthWeak(CardInstance dealer,CardInstance target,int amount,CardEffect e) {
            int maximum=Math.Min(amount,Math.Min(Math.Max(0,3-target.HealthWeakening),Math.Max(0,target.MaxHP-1)));
            int current=Math.Min(maximum,Math.Max(0,target.CurrentHP-1));
            target.ImportCore(target.RawAttack,target.RawHP-maximum);target.AbilityHP-=maximum;target.CurrentHP-=current;
            target.HealthWeakening+=maximum;target.HealthWeakCurrent+=current;
            if(target.HealthWeakening>0){target.HealthWeakTurns=Math.Max(target.HealthWeakTurns,WeakDuration(dealer,e));target.HealthWeakAppliedTurn=State.TurnNumber;target.HealthWeakSource=dealer.InstanceID;}
            Emit(new BattleEvent(BattleEventKind.MaxHPChanged,target.Owner,dealer.InstanceID,target.InstanceID,-maximum,suit:dealer.Definition.Suit));
            MeasureTactical(dealer,e,target,-maximum,"resolved");MeasureWeakApplied(dealer,true,maximum,maximum>0 || target.HealthWeakening>0);
        }
        void TickWeakness(CardInstance c) {
            if(c.LastWeakTickTurn==State.TurnNumber)return;c.LastWeakTickTurn=State.TurnNumber;
            if(c.CardWeakening>0 && c.AttackWeakAppliedTurn!=State.TurnNumber){
                c.AttackWeakTurns=Math.Max(0,c.AttackWeakTurns-1);
                bool expire=c.AttackWeakTurns==0;MeasureWeakDuration(c.AttackWeakSource,false,expire);
                if(expire){int n=c.CardWeakening;c.ImportCore(c.RawAttack+n,c.RawHP);c.AbilityAttack+=n;c.CardWeakening=c.WeakUntil=0;c.AttackWeakSource=0;
                    Emit(new BattleEvent(BattleEventKind.AttackChanged,c.Owner,target:c.InstanceID,amount:n));}
            }
            if(c.HealthWeakening>0 && c.HealthWeakAppliedTurn!=State.TurnNumber){
                c.HealthWeakTurns=Math.Max(0,c.HealthWeakTurns-1);
                bool expire=c.HealthWeakTurns==0;MeasureWeakDuration(c.HealthWeakSource,true,expire);
                if(expire){int n=c.HealthWeakening,current=c.HealthWeakCurrent;c.ImportCore(c.RawAttack,c.RawHP+n);c.AbilityHP+=n;c.CurrentHP=Math.Min(c.MaxHP,c.CurrentHP+current);
                    c.HealthWeakening=c.HealthWeakCurrent=c.HealthWeakSource=0;Emit(new BattleEvent(BattleEventKind.MaxHPChanged,c.Owner,target:c.InstanceID,amount:n));}
            }
        }
        void ClearWeaknessOnDeath(int id) {
            var c=State.Players.SelectMany(p=>p.Slots).FirstOrDefault(c=>c!=null && c.InstanceID==id);if(c==null)return;
            c.ImportCore(c.RawAttack+c.CardWeakening,c.RawHP+c.HealthWeakening);c.AbilityAttack+=c.CardWeakening;c.AbilityHP+=c.HealthWeakening;
            c.CardWeakening=c.WeakUntil=c.HealthWeakening=c.HealthWeakCurrent=c.AttackWeakTurns=c.HealthWeakTurns=c.AttackWeakSource=c.HealthWeakSource=0;
            c.AttackWeakAppliedTurn=c.HealthWeakAppliedTurn=c.LastWeakTickTurn=-1;
        }
        partial void MeasureWeakApplied(CardInstance dealer,bool health,int reduction,bool successful);
        partial void MeasureWeakDuration(int sourceID,bool health,bool expired);
        partial void MeasureWeakCondition(CardInstance dealer,bool successful);
    }
}
