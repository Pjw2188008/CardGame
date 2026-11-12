using System.Collections.Generic;
using System.Linq;

namespace Unknown.Battle
{
    public static partial class CardDatabase
    {
        static Dictionary<string, CardDefinition> BuildDiamondHeartRework(
            Dictionary<string, CardDefinition> revised, CardBalanceVersion version, bool diamond, bool heart)
        {
            var result = revised.ToDictionary(pair => pair.Key, pair => new CardDefinition(
                pair.Value.CardID, pair.Value.CardName, pair.Value.Suit, pair.Value.Rank,
                pair.Value.Attack, pair.Value.MaxHP, pair.Value.CardType, pair.Value.Description,
                pair.Value.ManaCost, version, pair.Value.EffectList.ToArray()));

            void Set(string id, string description, params CardEffect[] effects)
            {
                var before = revised[id];
                result[id] = new CardDefinition(id, before.CardName, before.Suit, before.Rank,
                    before.Attack, before.MaxHP, before.CardType, description, before.ManaCost, version, effects);
            }
            CardEffect After(int amount) => new CardEffect(EffectKind.FirstAttackHeal, amount, EffectTarget.Self);
            if (diamond)
            {
                Set("Diamond_2", "적 1명 피해 2", Damage(2));
                Set("Diamond_3", "자신 피해 1, 적 1명 피해 3", E(EffectKind.SelfDamage, 1, EffectTarget.Self), Damage(3));
                Set("Diamond_4", "무작위 적 1명 피해 4", E(EffectKind.RandomDamage, 4, EffectTarget.Enemy));
                Set("Diamond_5", "적 1명 피해 3", Damage(3));
                Set("Diamond_6", "자신 피해 1, 적 1명 피해 5", E(EffectKind.SelfDamage, 1, EffectTarget.Self), Damage(5));
                Set("Diamond_7", "서로 다른 적 최대 2명에게 각각 피해 3", E(EffectKind.ChainDamage, 3, EffectTarget.Enemy));
                Set("Diamond_8", "무작위 적 1명 피해 5", E(EffectKind.RandomDamage, 5, EffectTarget.Enemy));
                Set("Diamond_9", "적 1명 피해 5, 자신 공격력 +1", Damage(5), Attack(1, EffectTarget.Self));
                Set("Diamond_10", "적 1명 피해 7", Damage(7));
                Set("Diamond_J", "자신 피해 2, 적 1명 피해 8", E(EffectKind.SelfDamage, 2, EffectTarget.Self), Damage(8));
                Set("Diamond_Q", "서로 다른 적 최대 2명에게 각각 피해 5", E(EffectKind.ChainDamage, 5, EffectTarget.Enemy));
                Set("Diamond_K", "적 1명 피해 9, 자신 공격력 +2", Damage(9), Attack(2, EffectTarget.Self));
                Set("Diamond_A", "아군 전체 공격력 +2, 적 1명 피해 4", Attack(2, EffectTarget.AllAllies), Damage(4));
            }
            if (heart)
            {
                Set("Heart_2", "아군 1명 체력 2 회복", Heal(2, EffectTarget.Ally));
                Set("Heart_3", "자신 최대·현재 체력 +1", HP(1, EffectTarget.Self));
                Set("Heart_4", "첫 능동 공격 후 생존 시 자신 체력 2 회복", After(2));
                Set("Heart_5", "아군 1명 최대·현재 체력 +2", HP(2, EffectTarget.Ally));
                Set("Heart_6", "아군 전체 체력 1 회복", Heal(1, EffectTarget.AllAllies));
                Set("Heart_7", "첫 능동 공격 후 생존 시 자신 체력 3 회복", After(3));
                Set("Heart_8", "아군 1명 최대·현재 체력 +3", HP(3, EffectTarget.Ally));
                Set("Heart_9", "아군 전체 체력 2 회복", Heal(2, EffectTarget.AllAllies));
                Set("Heart_10", "첫 능동 공격 후 생존 시 자신 체력 4 회복", After(4));
                Set("Heart_J", "아군 1명 최대·현재 체력 +3, 이어 체력 2 회복", HP(3, EffectTarget.Ally), Heal(2, EffectTarget.Ally));
                Set("Heart_Q", "아군 전체 최대·현재 체력 +2", HP(2, EffectTarget.AllAllies));
                Set("Heart_K", "아군 전체 체력 3 회복, 첫 능동 공격 후 생존 시 자신 체력 2 회복", Heal(3, EffectTarget.AllAllies), After(2));
                Set("Heart_A", "아군 전체 최대·현재 체력 +2, 이어 체력 2 회복", HP(2, EffectTarget.AllAllies), Heal(2, EffectTarget.AllAllies));
            }
            return result;
        }
    }
}
