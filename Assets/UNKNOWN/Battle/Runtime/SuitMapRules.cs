using System;
using System.Linq;
namespace Unknown.Battle
{
    public sealed partial class BattleEngine
    {
        public SuitMapEffect SuitEffect { get; private set; }
        void ApplyMapSummon(CardInstance c)
        {
            if(UsesNewHeartMap && rules.Map==BattleMap.Heart) {
                ChangeMapStats(c,c.Attack,c.MaxHP+1); return;
            }
            if(!MapDefinition.IsSuit(rules.Map) || c.Definition.CardType!=CardType.Normal)return;
            var d=MapDefinition.For(rules.Map,rules.MapRules);
            if(c.Definition.Suit!=d.suit)return;
            ChangeMapStats(c,c.Attack+d.summonAttack,c.MaxHP+d.summonHP);
            if(d.summonDraw>0)Draw(c.Owner,d.summonDraw);
        }
        internal bool ResolveSuitMapTurn(int owner)
        {
            if(viewOnly || State.IsOver || owner!=State.ActivePlayer || !MapDefinition.IsSuit(rules.Map) || SuitEffect?.id==State.TurnNumber)return false;
            var d=MapDefinition.For(rules.Map,rules.MapRules); var p=State.Players[owner];
            SuitEffect=new SuitMapEffect{id=State.TurnNumber,owner=owner,map=rules.Map,detail="효과 없음"};
            Emit(new BattleEvent(BattleEventKind.SuitMapTurn,owner,source:State.TurnNumber));
            if(d.turnDamage>0 || d.turnWeaken>0)
            {
                var targets=State.Players[1-owner].Slots.Where(c=>c!=null && c.CurrentHP>0).ToArray();
                if(targets.Length>0)
                {
                    var c=targets[random.Next(targets.Length)];
                    if(d.turnDamage>0) { DamageUnit(c,d.turnDamage,-1,d.suit); RemoveDead(); SuitEffect.detail=c.Definition.CardName+" · 피해 "+d.turnDamage; }
                    else { int old=c.Attack; ChangeMapStats(c,Math.Max(0,c.Attack-d.turnWeaken),c.MaxHP); SuitEffect.detail=c.Definition.CardName+" · ATK -"+(old-c.Attack); }
                }
            }
            if(d.turnHeal>0)
            {
                int heal=Math.Min(d.turnHeal,Math.Max(0,rules.StartingHP-p.HP)); p.HP+=heal;
                if(heal>0)Emit(new BattleEvent(BattleEventKind.HeroChanged,owner,amount:heal));
                SuitEffect.detail=heal>0 ? "플레이어 HP +"+heal : "HP가 최대입니다";
            }
            if(d.turnMana>0)
            {
                int gain=Math.Min(d.turnMana,10-p.CurrentMana),before=p.CurrentMana; p.CurrentMana+=gain;
                if(gain>0)Emit(new BattleEvent(BattleEventKind.ManaGained,owner,amount:gain,secondary:before));
                SuitEffect.detail=gain>0 ? "이번 턴 현재 마나 +"+gain : "마나가 상한입니다";
            }
            return true;
        }
        void DamageUnit(CardInstance target,int amount,int sourceID,Suit suit,bool chain=false)
        {
            int actual=Math.Min(target.CurrentHP,amount);
            var dealer=State.Players.SelectMany(p=>p.Slots).FirstOrDefault(c=>c!=null && c.InstanceID==sourceID);
            MeasureDamageAttempt(dealer,target,amount,actual);
            target.CurrentHP-=amount;
            Emit(new BattleEvent(BattleEventKind.Damage,target.Owner,sourceID,target.InstanceID,actual,suit:suit,chain:chain));
            Log($"{target.Definition.CardName}: 피해 {actual}");
            ApplyHeartMapRecovery(target,actual);
        }
#if UNITY_EDITOR
        internal void ResetSuitEffectForDebug() { SuitEffect=null; }
#endif
    }
}
