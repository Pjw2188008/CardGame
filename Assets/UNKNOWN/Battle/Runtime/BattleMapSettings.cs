using System;
using System.Linq;
using UnityEngine;
namespace Unknown.Battle
{
    [CreateAssetMenu(menuName="UNKNOWN/Battle Map Settings")]
    public sealed class BattleMapSettings : ScriptableObject
    {
        public MapDefinition[] maps=MapDefinition.Defaults();
        [Header("Map backgrounds (Assets/Map)")]
        public Texture2D normalBackground,jokerBackground,diamondBackground,spadeBackground,heartBackground,clubBackground;
        public Texture2D BackgroundFor(BattleMap map)
        {
            switch(map) {
                case BattleMap.JokerGame:return jokerBackground;
                case BattleMap.Diamond:return diamondBackground;
                case BattleMap.Spade:return spadeBackground;
                case BattleMap.Heart:return heartBackground;
                case BattleMap.Club:return clubBackground;
                default:return normalBackground;
            }
        }
        public static MapDefinition[] LoadRules()
        {
            var asset=Resources.Load<BattleMapSettings>("BattleMapSettings");
            var result=asset==null ? MapDefinition.Defaults() : asset.maps;
            Validate(result); return result.Select(d=>d.Copy()).ToArray();
        }
        public static void Validate(MapDefinition[] maps)
        {
            if(maps==null || maps.Length!=6 || maps.Any(d=>d==null) || Enum.GetValues(typeof(BattleMap)).Cast<BattleMap>().Any(m=>maps.Count(d=>d.map==m)!=1)
                || maps.Any(d=>d.weight<0 || d.summonAttack<0 || d.summonHP<0 || d.summonDraw<0 || d.turnDamage<0 || d.turnWeaken<0 || d.turnHeal<0 || d.turnMana<0)
                || maps.Sum(d=>d.weight)!=10000 || maps.Single(d=>d.map==BattleMap.Normal).weight!=0
                || maps.Any(d=>MapDefinition.IsSuit(d.map) && d.suit!=(Suit)((int)d.map-2)))
                throw new InvalidOperationException("Battle Map Settings: 6개 맵(테스트용 Normal 포함), Normal 가중치 0, 전체 가중치 10000, 올바른 문양과 음수가 아닌 수치를 설정하세요.");
        }
    }
}
