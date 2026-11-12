using System.Collections.Generic;
using System.Linq;
namespace Unknown.Battle
{
    public static partial class CardDatabase
    {
        static Dictionary<string,CardDefinition> BuildFullRework(Dictionary<string,CardDefinition> before,CardBalanceVersion version,bool stats,bool diamond)
        {
            // Order: 2..10, J,Q,K,A. Costs, targets, timing and non-normal cards remain unchanged.
            int[][] atk={new[]{2,3,3,4,5,5,6,6,8,8,8,10,7},new[]{2,2,3,3,4,5,5,6,7,7,6,8,6},new[]{1,1,2,2,3,3,4,4,5,5,6,7,5},new[]{2,2,3,3,4,4,5,5,6,6,6,7,6}};
            int[][] hp={new[]{2,2,3,3,4,4,5,5,5,6,8,9,7},new[]{2,3,3,4,4,5,5,5,6,7,9,10,8},new[]{4,4,5,5,6,6,7,7,8,9,10,11,10},new[]{3,3,4,4,5,5,6,6,7,7,9,10,8}};
            string[] ranks={"2","3","4","5","6","7","8","9","10","J","Q","K","A"};
            var damage=new Dictionary<string,int>{{"6",6},{"7",4},{"8",6},{"9",6},{"10",8},{"J",9},{"Q",6},{"K",10},{"A",5}};
            return before.ToDictionary(p=>p.Key,p=>{
                var c=p.Value;int attack=c.Attack,health=c.MaxHP;var effects=c.EffectList.ToArray();string description=c.Description;
                if(c.CardType==CardType.Normal) {
                    int index=System.Array.IndexOf(ranks,RankText(c.Rank));
                    if(stats){attack=atk[(int)c.Suit][index];health=hp[(int)c.Suit][index];}
                    if(diamond && c.Suit==Suit.Diamond && damage.TryGetValue(RankText(c.Rank),out int n)) {
                        for(int i=0;i<effects.Length;i++) {
                            var e=effects[i];if(e.Kind!=EffectKind.Damage && e.Kind!=EffectKind.ChainDamage && e.Kind!=EffectKind.RandomDamage)continue;
                            description=description.Replace("피해 "+e.Amount,"피해 "+n);
                            effects[i]=new CardEffect(e.Kind,n,e.Target,e.FreshTarget);
                        }
                    }
                }
                return new CardDefinition(c.CardID,c.CardName,c.Suit,c.Rank,attack,health,c.CardType,description,c.ManaCost,version,effects);
            });
        }
    }
}
