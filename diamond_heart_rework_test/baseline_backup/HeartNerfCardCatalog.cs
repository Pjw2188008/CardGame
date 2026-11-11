using System.Collections.Generic;
using System.Linq;

namespace Unknown.Battle
{
    public static partial class CardDatabase
    {
        static Dictionary<string, CardDefinition> BuildHeartNerf(Dictionary<string, CardDefinition> revised)
        {
            var result = new Dictionary<string, CardDefinition>();
            foreach (var original in revised.Values)
            {
                var amounts = original.EffectList.Select(effect => effect.Amount).ToArray();
                string description = original.Description;
                switch (original.CardID)
                {
                    case "Heart_9":
                        amounts[0] = 2;
                        description = description.Replace("체력 3 회복", "체력 2 회복");
                        break;
                    case "Heart_Q":
                        amounts[0] = 2;
                        amounts[1] = 2;
                        description = description.Replace("체력 +3", "체력 +2").Replace("체력 3 회복", "체력 2 회복");
                        break;
                    case "Heart_K":
                        amounts[0] = 3;
                        amounts[1] = 3;
                        description = description.Replace("체력 4 회복", "체력 3 회복").Replace("체력 5 회복", "체력 3 회복");
                        break;
                }
                var effects = original.EffectList.Select((effect, index) => new CardEffect(
                    effect.Kind, amounts[index], effect.Target, effect.FreshTarget)).ToArray();
                result.Add(original.CardID, new CardDefinition(original.CardID, original.CardName,
                    original.Suit, original.Rank, original.Attack, original.MaxHP, original.CardType,
                    description, original.ManaCost, CardBalanceVersion.HeartNerf, effects));
            }
            return result;
        }
    }
}
