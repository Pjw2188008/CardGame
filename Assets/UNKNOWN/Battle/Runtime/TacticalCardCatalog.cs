using System.Collections.Generic;
namespace Unknown.Battle {
    public static partial class CardDatabase {
        static void ApplyTacticalCards(Dictionary<string,CardDefinition> result) {
            void Set(string id,int mana,int atk,int hp,string text,params CardEffect[] effects) {
                var c=result[id];result[id]=new CardDefinition(c.CardID,c.CardName,c.Suit,c.Rank,atk,hp,c.CardType,text,mana,c.Version,effects);
            }
            Set("Diamond_2",1,2,2,"출전: 적 유닛 1명 피해 1",new CardEffect(EffectKind.Damage,1,EffectTarget.Enemy));
            Set("Diamond_3",1,3,1,"공격 시: 이번 공격 피해 +1",new CardEffect(EffectKind.StrikeBonus,1,EffectTarget.Self));
            Set("Diamond_4",2,3,3,"출전: 이미 피해를 입은 적 유닛 1명 피해 2",new CardEffect(EffectKind.WoundedDamage,2,EffectTarget.Enemy));
            Set("Diamond_5",2,4,2,"처치 시: 적 플레이어 피해 2",new CardEffect(EffectKind.KillHero,2,EffectTarget.Self));
            Set("Diamond_6",3,5,3,"출전: 적 유닛 1명 피해 2. 이 효과로 처치했다면 자신 ATK +1 영구",new CardEffect(EffectKind.DamageGrow,2,EffectTarget.Enemy));
            Set("Diamond_7",3,5,4,"공격 시: 대상의 보호 무시",new CardEffect(EffectKind.IgnoreProtection,0,EffectTarget.Self));
            Set("Diamond_8",4,6,4,"출전: 이번 턴 아군 유닛 1명 ATK +2",new CardEffect(EffectKind.TemporaryAttack,2,EffectTarget.Ally));
            Set("Diamond_9",4,7,4,"처치 시: 자신 HP 2 회복, ATK +1 영구",new CardEffect(EffectKind.KillGrow,2,EffectTarget.Self));
            Set("Diamond_10",5,8,5,"출전: 적 유닛 1명 피해 3. 처치했다면 적 플레이어 피해 3",new CardEffect(EffectKind.DamageHero,3,EffectTarget.Enemy));
            Set("Diamond_J",5,7,7,"출전: 이번 턴 아군 전체 ATK +2",new CardEffect(EffectKind.TemporaryAttack,2,EffectTarget.AllAllies));
            Set("Diamond_Q",6,8,8,"출전: 적 유닛 1명 피해 4. 처치했다면 아군 전체 ATK +1 영구",new CardEffect(EffectKind.DamageTeamGrow,4,EffectTarget.Enemy));
            Set("Diamond_K",7,10,9,"출전: 적 유닛 1명 피해 5, 자신 보호 3",new CardEffect(EffectKind.Damage,5,EffectTarget.Enemy),new CardEffect(EffectKind.Protection,3,EffectTarget.Self));
            Set("Diamond_A",6,9,7,"출전: 적 유닛 1명 피해 4. 출전 효과 판정 시 필드에 자신 외 다이아 아군이 2장 이상이면 추가 피해 2",new CardEffect(EffectKind.DiamondBonusDamage,4,EffectTarget.Enemy));
            Set("Spade_2",1,1,3,"출전: 적 유닛 1명 약화 1",new CardEffect(EffectKind.CardWeaken,1,EffectTarget.Enemy));
            Set("Spade_3",1,2,2,"공격 시: 대상이 약화 상태라면 피해 +2",new CardEffect(EffectKind.WeakStrike,2,EffectTarget.Self));
            Set("Spade_4",2,2,4,"출전: 적 유닛 1명 약화 2",new CardEffect(EffectKind.CardWeaken,2,EffectTarget.Enemy));
            Set("Spade_5",2,3,3,"출전: 약화 상태 적 유닛 1명 피해 2",new CardEffect(EffectKind.WeakDamage,2,EffectTarget.Enemy));
            Set("Spade_6",3,4,4,"공격 시: 대상 약화 1 적용 후 피해 계산",new CardEffect(EffectKind.AttackWeaken,1,EffectTarget.Self));
            Set("Spade_7",3,4,5,"출전: 약화 상태 적 유닛 1명의 보호 제거",new CardEffect(EffectKind.RemoveProtection,0,EffectTarget.Enemy));
            Set("Spade_8",4,5,5,"출전: 적 유닛 전체 약화 1",new CardEffect(EffectKind.CardWeaken,1,EffectTarget.AllEnemies));
            Set("Spade_9",4,6,5,"공격 시: 대상이 약화 상태라면 피해 +3",new CardEffect(EffectKind.WeakStrike,3,EffectTarget.Self));
            Set("Spade_10",5,6,7,"출전: 약화 상태 적 유닛 1명 피해 4",new CardEffect(EffectKind.WeakDamage,4,EffectTarget.Enemy));
            Set("Spade_J",5,6,7,"출전: 적 유닛 1명 약화 3",new CardEffect(EffectKind.CardWeaken,3,EffectTarget.Enemy));
            Set("Spade_Q",6,7,9,"출전: 적 유닛 전체 약화 2",new CardEffect(EffectKind.CardWeaken,2,EffectTarget.AllEnemies));
            Set("Spade_K",7,9,9,"출전: 적 유닛 1명 피해 5. 대상이 이미 약화 상태라면 추가 피해 3",new CardEffect(EffectKind.WeakBonusDamage,5,EffectTarget.Enemy));
            Set("Spade_A",6,8,8,"출전: 적 전체 약화 1. 적용 전 이미 약화 상태였던 적 유닛 중 1명에게 피해 3",new CardEffect(EffectKind.WeakenThenDamage,1,EffectTarget.None),new CardEffect(EffectKind.PriorWeakDamage,3,EffectTarget.Enemy));
            Set("Heart_2",1,1,4,"출전: 아군 유닛 1명 HP 2 회복",new CardEffect(EffectKind.Heal,2,EffectTarget.Ally));
            Set("Heart_3",1,2,3,"출전: 아군 유닛 1명 보호 2",new CardEffect(EffectKind.Protection,2,EffectTarget.Ally));
            Set("Heart_4",2,2,5,"턴 시작: 자신 HP 1 회복",new CardEffect(EffectKind.TurnHealSelf,1,EffectTarget.Self));
            Set("Heart_5",2,3,4,"출전: 아군 유닛 1명 HP 3 회복",new CardEffect(EffectKind.Heal,3,EffectTarget.Ally));
            Set("Heart_6",3,3,6,"출전: 아군 유닛 1명 최대 HP +2, 현재 HP +2 영구",new CardEffect(EffectKind.IncreaseMaxHP,2,EffectTarget.Ally));
            Set("Heart_7",3,4,6,"공격 시: 자신의 HP가 최대라면 피해 +2",new CardEffect(EffectKind.FullStrike,2,EffectTarget.Self));
            Set("Heart_8",4,4,8,"출전: 아군 전체 HP 2 회복",new CardEffect(EffectKind.Heal,2,EffectTarget.AllAllies));
            Set("Heart_9",4,5,7,"턴 시작: 자신 HP가 최대라면 ATK +1 영구, 최대 2회",new CardEffect(EffectKind.TurnGrow,1,EffectTarget.Self));
            Set("Heart_10",5,6,9,"출전: 아군 전체 보호 2",new CardEffect(EffectKind.Protection,2,EffectTarget.AllAllies));
            Set("Heart_J",5,5,10,"출전: 아군 전체 최대 HP +2, 현재 HP +2 영구",new CardEffect(EffectKind.IncreaseMaxHP,2,EffectTarget.AllAllies));
            Set("Heart_Q",6,7,11,"출전: 아군 전체 HP 4 회복. 실제로 HP가 회복된 아군 유닛 수만큼 자신 보호 획득, 최대 3",new CardEffect(EffectKind.HealProtect,4,EffectTarget.Self));
            Set("Heart_K",7,8,12,"출전: 아군 전체 보호 3, 자신 ATK +1 영구",new CardEffect(EffectKind.Protection,3,EffectTarget.AllAllies),new CardEffect(EffectKind.ModifyAttack,1,EffectTarget.Self));
            Set("Heart_A",6,7,11,"출전: 아군 전체 HP 3 회복. 회복 처리 후 HP가 최대인 아군 전체 ATK +1, 이번 턴",new CardEffect(EffectKind.HealFullAttack,3,EffectTarget.Self));
            Set("Club_2",1,1,2,"출전: 카드 1장 드로우",new CardEffect(EffectKind.DrawCard,1,EffectTarget.None));
            Set("Club_3",1,2,2,"출전: 현재 마나 +1",new CardEffect(EffectKind.GainMana,1,EffectTarget.None));
            Set("Club_4",2,2,3,"출전: 손패 1장 선택해 버린 뒤 카드 2장 드로우",new CardEffect(EffectKind.TacticalExchange,2,EffectTarget.None));
            Set("Club_5",2,3,3,"출전: 이번 턴 다음 배치 카드 비용 -1",new CardEffect(EffectKind.Discount,1,EffectTarget.None));
            Set("Club_6",3,3,4,"출전: 카드 2장 드로우",new CardEffect(EffectKind.DrawCard,2,EffectTarget.None));
            Set("Club_7",3,4,4,"출전: 카드 1장 드로우, 현재 마나 +1",new CardEffect(EffectKind.DrawCard,1,EffectTarget.None),new CardEffect(EffectKind.GainMana,1,EffectTarget.None));
            Set("Club_8",4,4,5,"출전: 이번 턴 다음 배치 카드 비용 -2",new CardEffect(EffectKind.Discount,2,EffectTarget.None));
            Set("Club_9",4,5,5,"출전: 손패 1장 선택해 버린 뒤 카드 3장 드로우",new CardEffect(EffectKind.TacticalExchange,3,EffectTarget.None));
            Set("Club_10",5,6,6,"출전: 카드 2장 드로우, 다음 배치 카드 비용 -1",new CardEffect(EffectKind.DrawCard,2,EffectTarget.None),new CardEffect(EffectKind.Discount,1,EffectTarget.None));
            Set("Club_J",5,5,7,"출전: 카드 2장 드로우, 현재 마나 +1",new CardEffect(EffectKind.DrawCard,2,EffectTarget.None),new CardEffect(EffectKind.GainMana,1,EffectTarget.None));
            Set("Club_Q",6,6,9,"출전: 손패에서 최대 2장 선택해 버린 뒤 버린 카드 수 +2장 드로우",new CardEffect(EffectKind.TacticalExchange,4,EffectTarget.None));
            Set("Club_K",7,8,9,"출전: 카드 3장 드로우, 다음 배치 카드 비용 -2",new CardEffect(EffectKind.DrawCard,3,EffectTarget.None),new CardEffect(EffectKind.Discount,2,EffectTarget.None));
            Set("Club_A",6,7,8,"출전: 카드 2장 드로우, 현재 마나 +2",new CardEffect(EffectKind.DrawCard,2,EffectTarget.None),new CardEffect(EffectKind.GainMana,2,EffectTarget.None));
        }
    }
}
