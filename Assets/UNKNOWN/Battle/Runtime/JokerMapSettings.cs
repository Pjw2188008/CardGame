using System;
using System.Linq;
using UnityEngine;
namespace Unknown.Battle
{
    [CreateAssetMenu(menuName="UNKNOWN/Joker Map Settings")]
    public sealed class JokerMapSettings : ScriptableObject
    {
        public JokerEventDefinition[] events=JokerEventDefinition.Defaults();
        public static JokerEventDefinition[] LoadRules()
        {
            var asset=Resources.Load<JokerMapSettings>("JokerMapSettings");
            var rules=asset==null ? JokerEventDefinition.Defaults() : asset.events;
            if(rules==null || rules.Length!=14 || rules.Any(e=>e==null || (int)e.kind<0 || (int)e.kind>13) || rules.Select(e=>e.kind).Distinct().Count()!=14 || rules.Any(e=>e.weight<0 || e.amount<0) || rules.Sum(e=>e.weight)!=100)
                throw new InvalidOperationException("Joker Map Settings: 사건 14종, 확률 합계 100%, 음수가 아닌 수치를 설정하세요.");
            return rules.Select(e=>e.Copy()).ToArray();
        }
    }
}
