using System.Collections.Generic;
using System.Linq;

namespace Unknown.Battle
{
    public static partial class CardDatabase
    {
        static Dictionary<string, CardDefinition> BuildDiamondBuff(Dictionary<string, CardDefinition> revised)
        {
            var result = new Dictionary<string, CardDefinition>();
            foreach (var original in revised.Values)
            {
                var amounts = original.EffectList.Select(effect => effect.Amount).ToArray();
                string description = original.Description;
                switch (original.CardID)
                {
                    case "Diamond_4":
                        amounts[0] = 4;
                        description = description.Replace("피해 3", "피해 4");
                        break;
                    case "Diamond_7":
                        amounts[0] = 3;
                        description = description.Replace("각각 피해 2", "각각 피해 3");
                        break;
                    case "Diamond_10":
                        amounts[0] = 6;
                        description = description.Replace("피해 5", "피해 6");
                        break;
                }
                var effects = original.EffectList.Select((effect, index) => new CardEffect(
                    effect.Kind, amounts[index], effect.Target, effect.FreshTarget)).ToArray();
                result.Add(original.CardID, new CardDefinition(original.CardID, original.CardName,
                    original.Suit, original.Rank, original.Attack, original.MaxHP, original.CardType,
                    description, original.ManaCost, CardBalanceVersion.DiamondBuff, effects));
            }
            return result;
        }
    }
}
