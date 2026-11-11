using System.Collections.Generic;
using System.Linq;
namespace Unknown.Battle
{
    public static partial class CardDatabase
    {
        static Dictionary<string,CardDefinition> BuildRevised(Dictionary<string,CardDefinition> baseline)
        {
            var result=baseline.ToDictionary(p=>p.Key,p=>new CardDefinition(p.Value.CardID,p.Value.CardName,p.Value.Suit,p.Value.Rank,p.Value.Attack,p.Value.MaxHP,p.Value.CardType,p.Value.Description,p.Value.ManaCost,CardBalanceVersion.Revised,p.Value.EffectList.ToArray()));
            void Set(string id,int mana,int attack,int hp,string description,params CardEffect[] effects) {
                var original=baseline[id];result[id]=new CardDefinition(id,original.CardName,original.Suit,original.Rank,attack,hp,original.CardType,description,mana,CardBalanceVersion.Revised,effects);
            }
            CardEffect Weaken(int n,bool fresh=false)=>new CardEffect(EffectKind.ModifyAttack,-n,EffectTarget.Enemy,fresh);
            CardEffect After(int n)=>new CardEffect(EffectKind.FirstAttackHeal,n,EffectTarget.Self);
            CardEffect Mana(int n)=>new CardEffect(EffectKind.GainMana,n);
            CardEffect Draw(int n)=>new CardEffect(EffectKind.DrawCard,n);
            CardEffect Exchange(int n)=>new CardEffect(EffectKind.Exchange,n);
            Set("Diamond_4",2,3,3,"무작위 적 1명 피해 3",new CardEffect(EffectKind.RandomDamage,3,EffectTarget.Enemy));
            Set("Diamond_7",3,5,4,"서로 다른 적 최대 2명에게 각각 피해 2",new CardEffect(EffectKind.ChainDamage,2,EffectTarget.Enemy));
            Set("Diamond_Q",7,7,8,"서로 다른 적 최대 2명에게 각각 피해 4",new CardEffect(EffectKind.ChainDamage,4,EffectTarget.Enemy));
            Set("Spade_2",1,2,2,"적 1명 공격력 -1",Weaken(1));
            Set("Spade_3",1,2,3,"적 1명 공격력 -1, 같은 대상 피해 1",Weaken(1),Damage(1));
            Set("Spade_4",2,3,3,"서로 다른 적 최대 2명 공격력 각각 -1",new CardEffect(EffectKind.ChainWeaken,1,EffectTarget.Enemy));
            Set("Spade_5",2,3,4,"적 1명 공격력 -2",Weaken(2));
            Set("Spade_6",3,4,4,"적 1명 공격력 -2, 같은 대상 피해 1",Weaken(2),Damage(1));
            Set("Spade_7",3,4,5,"상대 손패 무작위 1장 버리기",new CardEffect(EffectKind.DiscardCard,1));
            Set("Spade_8",4,5,5,"적 1명 공격력 -3",Weaken(3));
            Set("Spade_9",4,5,5,"적 1명 공격력 -2, 같은 대상 피해 2",Weaken(2),Damage(2));
            Set("Spade_10",5,6,6,"상대 손패 무작위 1장 버리기, 적 1명 공격력 -2",new CardEffect(EffectKind.DiscardCard,1),Weaken(2));
            Set("Spade_A",6,5,8,"적 전체 공격력 -2, 상대 손패 무작위 1장 버리기",new CardEffect(EffectKind.ModifyAttack,-2,EffectTarget.AllEnemies),new CardEffect(EffectKind.DiscardCard,1));
            Set("Spade_J",6,6,7,"적 1명 공격력 -4, 같은 대상 피해 3",Weaken(4),Damage(3));
            Set("Spade_Q",6,5,9,"상대 손패 무작위 최대 2장 버리기, 적 전체 공격력 -1",new CardEffect(EffectKind.DiscardCard,2),new CardEffect(EffectKind.ModifyAttack,-1,EffectTarget.AllEnemies));
            Set("Spade_K",8,7,10,"적 1명 피해 4. 사망 처리 후 살아 있는 적 1명을 새로 선택해 공격력 -4",Damage(4),Weaken(4,true));
            Set("Heart_3",1,1,4,"자신 최대·현재 체력 +2",HP(2,EffectTarget.Self));
            Set("Heart_4",2,2,5,"자신의 첫 공격 후 생존하면 자신 체력 3 회복",After(3));
            Set("Heart_5",2,2,5,"아군 1명 최대·현재 체력 +3",HP(3,EffectTarget.Ally));
            Set("Heart_6",3,2,6,"아군 전체 체력 2 회복",Heal(2,EffectTarget.AllAllies));
            Set("Heart_7",3,3,6,"아군 1명 체력 3 회복. 자신의 첫 공격 후 생존하면 자신 체력 2 회복",Heal(3,EffectTarget.Ally),After(2));
            Set("Heart_8",4,3,7,"아군 1명 최대·현재 체력 +4",HP(4,EffectTarget.Ally));
            Set("Heart_9",4,3,7,"아군 전체 체력 3 회복",Heal(3,EffectTarget.AllAllies));
            Set("Heart_10",5,4,8,"아군 전체 체력 2 회복. 자신의 첫 공격 후 생존하면 자신 체력 3 회복",Heal(2,EffectTarget.AllAllies),After(3));
            Set("Heart_A",7,4,10,"아군 전체 최대·현재 체력 +2, 이어 체력 3 회복",HP(2,EffectTarget.AllAllies),Heal(3,EffectTarget.AllAllies));
            Set("Heart_J",6,4,9,"아군 1명 최대·현재 체력 +3. 자신의 첫 공격 후 생존하면 자신 체력 4 회복",HP(3,EffectTarget.Ally),After(4));
            Set("Heart_Q",7,5,10,"아군 전체 최대·현재 체력 +3, 이어 체력 3 회복",HP(3,EffectTarget.AllAllies),Heal(3,EffectTarget.AllAllies));
            Set("Heart_K",8,6,11,"아군 전체 체력 4 회복. 자신의 첫 공격 후 생존하면 자신 체력 5 회복",Heal(4,EffectTarget.AllAllies),After(5));
            Set("Club_2",1,1,3,"남은 손패 1장을 버리면 카드 1장 뽑기 (건너뛰기 가능)",Exchange(1));
            Set("Club_3",1,2,3,"현재 마나 +1 (상한 10)",Mana(1));
            Set("Club_4",2,2,4,"카드 1장 뽑기",Draw(1));
            Set("Club_5",2,3,4,"남은 손패 1장을 버리면 카드 2장 뽑기 (건너뛰기 가능)",Exchange(2));
            Set("Club_6",3,3,5,"카드 1장 뽑기, 현재 마나 +1",Draw(1),Mana(1));
            Set("Club_7",3,3,5,"카드 2장 뽑기",Draw(2));
            Set("Club_8",4,4,6,"카드 1장 뽑기, 현재 마나 +1",Draw(1),Mana(1));
            Set("Club_9",4,4,6,"카드 2장 뽑기, 현재 마나 +1",Draw(2),Mana(1));
            Set("Club_10",5,5,7,"카드 2장 뽑기, 현재 마나 +1",Draw(2),Mana(1));
            Set("Club_A",7,4,8,"카드 2장 뽑기, 현재 마나 +2",Draw(2),Mana(2));
            Set("Club_J",6,5,7,"남은 손패 1장을 버리면 카드 2장 뽑기, 현재 마나 +2 (교환 건너뛰기 가능)",Exchange(2),Mana(2));
            Set("Club_Q",7,5,9,"카드 3장 뽑기, 현재 마나 +1",Draw(3),Mana(1));
            Set("Club_K",8,6,10,"카드 3장 뽑기, 현재 마나 +2",Draw(3),Mana(2));
            return result;
        }
    }
}
