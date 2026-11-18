using System.Collections.Generic;
namespace Unknown.Battle {
    public static partial class CardDatabase {
        static void ApplyTacticalCards(Dictionary<string,CardDefinition> result) {
            void Set(string id,int mana,int atk,int hp,string text,params CardEffect[] effects) {
                var c=result[id];result[id]=new CardDefinition(c.CardID,c.CardName,c.Suit,c.Rank,atk,hp,c.CardType,text,mana,c.Version,effects);
            }
            Set("Diamond_2",1,2,2,"출전: 적 1명 피해 1",new CardEffect(EffectKind.Damage,1,EffectTarget.Enemy,false,0));
            Set("Diamond_3",1,3,1,"출전: 적 1명 피해 2",new CardEffect(EffectKind.Damage,2,EffectTarget.Enemy,false,0));
            Set("Diamond_4",2,3,3,"출전: 적 1명 피해 2",new CardEffect(EffectKind.Damage,2,EffectTarget.Enemy,false,0));
            Set("Diamond_5",2,4,2,"출전: 아군 1명 ATK +1",new CardEffect(EffectKind.ModifyAttack,1,EffectTarget.Ally,false,0));
            Set("Diamond_6",3,5,3,"출전: 적 1명 피해 4",new CardEffect(EffectKind.Damage,4,EffectTarget.Enemy,false,0));
            Set("Diamond_7",3,5,4,"출전: 적 1명 보호 제거 후 피해 2",new CardEffect(EffectKind.RemoveProtection,0,EffectTarget.Enemy,false,0), new CardEffect(EffectKind.Damage,2,EffectTarget.Enemy,false,0));
            Set("Diamond_8",4,6,4,"출전: 아군 전체 ATK +1",new CardEffect(EffectKind.ModifyAttack,1,EffectTarget.AllAllies,false,0));
            Set("Diamond_9",4,7,4,"출전: 적 1명 피해 5",new CardEffect(EffectKind.Damage,5,EffectTarget.Enemy,false,0));
            Set("Diamond_10",5,8,5,"출전: 적 전체 피해 2",new CardEffect(EffectKind.Damage,2,EffectTarget.AllEnemies,false,0));
            Set("Diamond_J",5,7,7,"출전: 아군 전체 ATK +2",new CardEffect(EffectKind.ModifyAttack,2,EffectTarget.AllAllies,false,0));
            Set("Diamond_Q",6,8,8,"출전: 적 전체 피해 3",new CardEffect(EffectKind.Damage,3,EffectTarget.AllEnemies,false,0));
            Set("Diamond_K",7,10,9,"출전: 적 1명 피해 7, 아군 1명 ATK +2",new CardEffect(EffectKind.Damage,7,EffectTarget.Enemy,false,0), new CardEffect(EffectKind.ModifyAttack,2,EffectTarget.Ally,false,0));
            Set("Diamond_A",6,9,7,"출전: 적 전체 피해 2, 아군 전체 ATK +1",new CardEffect(EffectKind.Damage,2,EffectTarget.AllEnemies,false,0), new CardEffect(EffectKind.ModifyAttack,1,EffectTarget.AllAllies,false,0));
            Set("Spade_2",1,1,3,"출전: 적 1명 ATK -1, 2턴",new CardEffect(EffectKind.CardWeaken,1,EffectTarget.Enemy,false,2));
            Set("Spade_3",1,2,2,"출전: 적 1명 최대 HP -1, 2턴",new CardEffect(EffectKind.HealthWeaken,1,EffectTarget.Enemy,false,2));
            Set("Spade_4",2,2,4,"출전: 적 1명 ATK -2, 2턴",new CardEffect(EffectKind.CardWeaken,2,EffectTarget.Enemy,false,2));
            Set("Spade_5",2,3,3,"출전: 적 1명 최대 HP -2, 2턴",new CardEffect(EffectKind.HealthWeaken,2,EffectTarget.Enemy,false,2));
            Set("Spade_6",3,4,4,"출전: 적 1명 ATK -1, 최대 HP -1, 2턴",new CardEffect(EffectKind.CardWeaken,1,EffectTarget.Enemy,false,2), new CardEffect(EffectKind.HealthWeaken,1,EffectTarget.Enemy,false,2));
            Set("Spade_7",3,4,5,"출전: 적 1명 ATK -2, 3턴",new CardEffect(EffectKind.CardWeaken,2,EffectTarget.Enemy,false,3));
            Set("Spade_8",4,5,5,"출전: 적 전체 ATK -1, 2턴",new CardEffect(EffectKind.CardWeaken,1,EffectTarget.AllEnemies,false,2));
            Set("Spade_9",4,6,5,"출전: 적 1명 최대 HP -3, 2턴",new CardEffect(EffectKind.HealthWeaken,3,EffectTarget.Enemy,false,2));
            Set("Spade_10",5,6,7,"출전: 적 전체 최대 HP -1, 2턴",new CardEffect(EffectKind.HealthWeaken,1,EffectTarget.AllEnemies,false,2));
            Set("Spade_J",5,6,7,"출전: 적 1명 ATK -3, 3턴",new CardEffect(EffectKind.CardWeaken,3,EffectTarget.Enemy,false,3));
            Set("Spade_Q",6,7,9,"출전: 적 전체 ATK -2, 3턴",new CardEffect(EffectKind.CardWeaken,2,EffectTarget.AllEnemies,false,3));
            Set("Spade_K",7,9,9,"출전: 적 1명 ATK -3, 최대 HP -3, 3턴",new CardEffect(EffectKind.CardWeaken,3,EffectTarget.Enemy,false,3), new CardEffect(EffectKind.HealthWeaken,3,EffectTarget.Enemy,false,3));
            Set("Spade_A",6,8,8,"출전: 적 전체 ATK -1, 최대 HP -2, 3턴",new CardEffect(EffectKind.CardWeaken,1,EffectTarget.AllEnemies,false,3), new CardEffect(EffectKind.HealthWeaken,2,EffectTarget.AllEnemies,false,3));
            Set("Heart_2",1,1,4,"출전: 아군 1명 HP 2 회복",new CardEffect(EffectKind.Heal,2,EffectTarget.Ally,false,0));
            Set("Heart_3",1,2,3,"출전: 아군 1명 보호 2",new CardEffect(EffectKind.Protection,2,EffectTarget.Ally,false,0));
            Set("Heart_4",2,2,5,"출전: 아군 1명 HP 3 회복",new CardEffect(EffectKind.Heal,3,EffectTarget.Ally,false,0));
            Set("Heart_5",2,3,4,"출전: 아군 1명 최대 HP +2, 현재 HP +2",new CardEffect(EffectKind.IncreaseMaxHP,2,EffectTarget.Ally,false,0));
            Set("Heart_6",3,3,6,"출전: 아군 전체 HP 2 회복",new CardEffect(EffectKind.Heal,2,EffectTarget.AllAllies,false,0));
            Set("Heart_7",3,4,6,"출전: 아군 1명 보호 3",new CardEffect(EffectKind.Protection,3,EffectTarget.Ally,false,0));
            Set("Heart_8",4,4,8,"출전: 아군 전체 최대 HP +1, 현재 HP +1",new CardEffect(EffectKind.IncreaseMaxHP,1,EffectTarget.AllAllies,false,0));
            Set("Heart_9",4,5,7,"출전: 아군 전체 HP 3 회복",new CardEffect(EffectKind.Heal,3,EffectTarget.AllAllies,false,0));
            Set("Heart_10",5,6,9,"출전: 아군 전체 보호 2",new CardEffect(EffectKind.Protection,2,EffectTarget.AllAllies,false,0));
            Set("Heart_J",5,5,10,"출전: 아군 전체 최대 HP +2, 현재 HP +2",new CardEffect(EffectKind.IncreaseMaxHP,2,EffectTarget.AllAllies,false,0));
            Set("Heart_Q",6,7,11,"출전: 아군 전체 HP 4 회복, 보호 2",new CardEffect(EffectKind.Heal,4,EffectTarget.AllAllies,false,0), new CardEffect(EffectKind.Protection,2,EffectTarget.AllAllies,false,0));
            Set("Heart_K",7,8,12,"출전: 아군 전체 보호 3, 최대·현재 HP +2",new CardEffect(EffectKind.Protection,3,EffectTarget.AllAllies,false,0), new CardEffect(EffectKind.IncreaseMaxHP,2,EffectTarget.AllAllies,false,0));
            Set("Heart_A",6,7,11,"출전: 아군 전체 최대 HP +2, 현재 HP +2, 보호 1",new CardEffect(EffectKind.IncreaseMaxHP,2,EffectTarget.AllAllies,false,0), new CardEffect(EffectKind.Protection,1,EffectTarget.AllAllies,false,0));
            Set("Club_2",1,1,2,"출전: 카드 1장 드로우",new CardEffect(EffectKind.DrawCard,1,EffectTarget.None,false,0));
            Set("Club_3",1,2,2,"출전: 마나 +1",new CardEffect(EffectKind.GainMana,1,EffectTarget.None,false,0));
            Set("Club_4",2,2,3,"출전: 손패 1장 버리고 카드 2장 드로우",new CardEffect(EffectKind.TacticalExchange,2,EffectTarget.None,false,0));
            Set("Club_5",2,3,3,"출전: 마나 +1",new CardEffect(EffectKind.GainMana,1,EffectTarget.None,false,0));
            Set("Club_6",3,3,4,"출전: 카드 2장 드로우",new CardEffect(EffectKind.DrawCard,2,EffectTarget.None,false,0));
            Set("Club_7",3,4,4,"출전: 카드 1장 드로우, 마나 +1",new CardEffect(EffectKind.DrawCard,1,EffectTarget.None,false,0), new CardEffect(EffectKind.GainMana,1,EffectTarget.None,false,0));
            Set("Club_8",4,4,5,"출전: 카드 2장 드로우",new CardEffect(EffectKind.DrawCard,2,EffectTarget.None,false,0));
            Set("Club_9",4,5,5,"출전: 손패 1장 버리고 카드 3장 드로우",new CardEffect(EffectKind.TacticalExchange,3,EffectTarget.None,false,0));
            Set("Club_10",5,6,6,"출전: 카드 2장 드로우, 마나 +1",new CardEffect(EffectKind.DrawCard,2,EffectTarget.None,false,0), new CardEffect(EffectKind.GainMana,1,EffectTarget.None,false,0));
            Set("Club_J",5,5,7,"출전: 카드 3장 드로우",new CardEffect(EffectKind.DrawCard,3,EffectTarget.None,false,0));
            Set("Club_Q",6,6,9,"출전: 손패 최대 2장 교환, 카드 3장 드로우",new CardEffect(EffectKind.TacticalExchange,3,EffectTarget.None,false,0));
            Set("Club_K",7,8,9,"출전: 카드 3장 드로우, 마나 +2",new CardEffect(EffectKind.DrawCard,3,EffectTarget.None,false,0), new CardEffect(EffectKind.GainMana,2,EffectTarget.None,false,0));
            Set("Club_A",6,7,8,"출전: 카드 2장 드로우, 마나 +2",new CardEffect(EffectKind.DrawCard,2,EffectTarget.None,false,0), new CardEffect(EffectKind.GainMana,2,EffectTarget.None,false,0));
        }
    }
}
