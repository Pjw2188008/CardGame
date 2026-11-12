using System;
using System.Linq;
namespace Unknown.Battle
{
    [Serializable] public sealed class MapDefinition
    {
        public BattleMap map;
        public string name,title,symbol,tint;
        public int weight,summonAttack,summonHP,summonDraw,turnDamage,turnWeaken,turnHeal,turnMana;
        public bool firstDamageRecovery;
        public Suit suit=Suit.None;
        public MapDefinition Copy() => (MapDefinition)MemberwiseClone();
        public static MapDefinition[] Defaults() => new[] {
            new MapDefinition{map=BattleMap.Normal,name="일반 전장",title="NORMAL BATTLEFIELD",symbol="◇",tint="#8797AD"},
            new MapDefinition{map=BattleMap.Diamond,name="공격의 전장",title="DIAMOND",symbol="♦",tint="#ED5757",suit=Suit.Diamond,weight=2250,summonAttack=1,turnDamage=1},
            new MapDefinition{map=BattleMap.Spade,name="제압의 전장",title="SPADE",symbol="♠",tint="#AE78EA",suit=Suit.Spade,weight=2250,summonHP=1,turnWeaken=1},
            new MapDefinition{map=BattleMap.Heart,name="생명의 전장",title="HEART",symbol="♥",tint="#EE79AB",suit=Suit.Heart,weight=2250,summonHP=2,turnHeal=2},
            new MapDefinition{map=BattleMap.Club,name="번영의 전장",title="CLUB",symbol="♣",tint="#68CB92",suit=Suit.Club,weight=2250,summonDraw=1,turnMana=1},
            new MapDefinition{map=BattleMap.JokerGame,name="조커의 유희",title="JOKER'S GAME",symbol="★",tint="#BB83E4",weight=1000}
        };
        public static MapDefinition For(BattleMap map,MapDefinition[] definitions=null) => (definitions ?? Defaults()).Single(d=>d.map==map);
        public static bool IsSuit(BattleMap map) => map>=BattleMap.Diamond && map<=BattleMap.Club;
        public string Effects => firstDamageRecovery ? "모든 유닛 소환 시 최대·현재 HP +1\n첫 피해 후 생존 시 HP 1 회복 (유닛당 1회)" : map==BattleMap.Normal ? "필드에 UNKNOWN 3장 배치 또는 상대 HP 0 시 승리" : map==BattleMap.JokerGame ? "매 턴 무작위 사건 발생\nJOKER 도박 3회 성공 또는 상대 HP 0 시 승리"
            : "고정: "+(summonAttack>0 ? "같은 문양 소환 시 ATK +"+summonAttack : summonHP>0 ? "같은 문양 소환 시 최대·현재 HP +"+summonHP : "같은 문양 소환 시 "+summonDraw+"장 드로우")
                +"\n턴: "+(turnDamage>0 ? "무작위 적 유닛 피해 "+turnDamage : turnWeaken>0 ? "무작위 적 ATK -"+turnWeaken : turnHeal>0 ? "자신 HP +"+turnHeal+" (최대 HP까지)" : "현재 마나 +"+turnMana+" (상한 10)");
    }
    [Serializable] public sealed class SuitMapEffect
    {
        public int id,owner;
        public BattleMap map;
        public string detail;
        public SuitMapEffect Copy() => (SuitMapEffect)MemberwiseClone();
    }
}
