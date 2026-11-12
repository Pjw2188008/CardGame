using System;
namespace Unknown.Battle
{
    public sealed partial class BattleEngine
    {
        public bool UsesNewHeartMap => rules.CardVersion==CardBalanceVersion.FullHeartMapOnly || rules.CardVersion==CardBalanceVersion.FullBalanceRework;
        void ConfigureFullReworkMap()
        {
            if(!UsesNewHeartMap)return;
            var d=MapDefinition.For(BattleMap.Heart,rules.MapRules);
            d.name="생명의 가호";d.summonHP=1;d.turnHeal=0;d.firstDamageRecovery=true;
        }
        void ApplyHeartMapRecovery(CardInstance unit,int actualDamage)
        {
            if(!UsesNewHeartMap || rules.Map!=BattleMap.Heart || actualDamage<=0 || unit.HeartMapHealSpent)return;
            // A lethal first hit can never be rescued. Mark the first hit before emitting any event.
            unit.HeartMapHealSpent=true;
            if(unit.CurrentHP<=0)return;
            int heal=Math.Min(1,Math.Max(0,unit.MaxHP-unit.CurrentHP));unit.CurrentHP+=heal;
            Emit(new BattleEvent(BattleEventKind.Heal,unit.Owner,source:-1,target:unit.InstanceID,amount:heal,suit:Suit.Heart));
            Log(unit.Definition.CardName+": 생명의 가호 +"+heal);
        }
    }
}
