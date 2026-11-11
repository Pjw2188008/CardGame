using System;
using System.Collections.Generic;
using System.Linq;

namespace Unknown.Battle
{
    public enum Suit { Diamond, Spade, Heart, Club, None }
    public enum Rank { Two = 2, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Ace, Jack, Queen, King, Joker }
    public enum CardType { Normal, Joker, Unknown }
    public enum CardBalanceVersion { Revised=0, Baseline=1 }
    public enum EffectKind { Damage, Heal, ModifyAttack, IncreaseMaxHP, DrawCard, SelfDamage, ChainDamage, RandomDamage, DiscardCard, FirstAttackHeal, GainMana, Exchange, ChainWeaken }
    public enum EffectTarget { Self, Enemy, Ally, AllEnemies, AllAllies, None }

    public sealed class CardEffect
    {
        public EffectKind Kind { get; }
        public EffectTarget Target { get; }
        public int Amount { get; }
        public bool FreshTarget { get; }
        public CardEffect(EffectKind kind, int amount, EffectTarget target = EffectTarget.None, bool freshTarget=false)
        { Kind = kind; Amount = amount; Target = target; FreshTarget=freshTarget; }
    }

    public sealed class CardDefinition
    {
        public string CardID { get; }
        public string CardName { get; }
        public Suit Suit { get; }
        public Rank Rank { get; }
        public int Attack { get; }
        public int MaxHP { get; }
        public CardType CardType { get; }
        readonly int explicitMana=-1;
        public CardBalanceVersion Version {get;internal set;}
        public int ManaCost => explicitMana>=0 ? explicitMana : CardType == CardType.Joker ? 2 : CardType == CardType.Unknown
            ? (Rank == Rank.Jack ? 7 : Rank == Rank.Queen ? 8 : 9)
            : Rank == Rank.Ace ? 7 : Rank == Rank.Jack ? 6 : Rank == Rank.Queen ? 7 : Rank == Rank.King ? 8 : (int)Rank / 2;
        public string Description { get; }
        public IReadOnlyList<CardEffect> EffectList { get; }
        public CardDefinition(string id, string name, Suit suit, Rank rank, int attack, int hp, CardType type, string description, params CardEffect[] effects)
        {
            CardID = id; CardName = name; Suit = suit; Rank = rank; Attack = attack; MaxHP = hp;
            CardType = type; Description = description; EffectList = Array.AsReadOnly((CardEffect[])effects.Clone());
        }
        public CardDefinition(string id,string name,Suit suit,Rank rank,int attack,int hp,CardType type,string description,int mana,CardBalanceVersion version,params CardEffect[] effects)
            :this(id,name,suit,rank,attack,hp,type,description,effects) {explicitMana=mana;Version=version;}
    }

    public sealed class CardInstance
    {
        public int InstanceID { get; }
        public CardDefinition Definition { get; }
        public int Owner { get; }
        int coreAttack,coreHP;
        public int PokerAttack { get; internal set; }
        public int PokerHP { get; internal set; }
        public int MapAttack { get; internal set; }
        public int MapHP { get; internal set; }
        public int JokerAttack { get; internal set; }
        public int JokerHP { get; internal set; }
        public int AbilityAttack { get; internal set; }
        public int AbilityHP { get; internal set; }
        public int NonPokerAttack => Math.Max(0,coreAttack);
        public int NonPokerHP => Math.Max(1,coreHP);
        internal int RawAttack => coreAttack;
        internal int RawHP => coreHP;
        internal void ImportCore(int attack,int hp) { coreAttack=attack; coreHP=hp; }
        public int Attack { get=>Math.Max(0,coreAttack+PokerAttack); internal set=>coreAttack=value-PokerAttack; }
        public int MaxHP { get=>Math.Max(1,coreHP+PokerHP); internal set=>coreHP=value-PokerHP; }
        public int OtherAttack => coreAttack-Definition.Attack-MapAttack-JokerAttack-AbilityAttack;
        public int OtherHP => coreHP-Definition.MaxHP-MapHP-JokerHP-AbilityHP;
        public int CurrentHP { get; internal set; }
        public int SummonedTurn { get; internal set; } = -1;
        public bool HasAttacked { get; internal set; }
        public bool JokerUsed { get; internal set; }
        public bool FirstAttackHealSpent {get;internal set;}
        internal CardInstance(int id, CardDefinition definition, int owner)
        {
            InstanceID = id; Definition = definition; Owner = owner;
            Attack = definition.Attack; MaxHP = definition.MaxHP; CurrentHP = MaxHP;
        }
    }

    public static partial class CardDatabase
    {
        static CardEffect E(EffectKind kind, int amount, EffectTarget target = EffectTarget.None) => new CardEffect(kind, amount, target);
        static CardEffect Damage(int n) => E(EffectKind.Damage, n, EffectTarget.Enemy);
        static CardEffect Heal(int n, EffectTarget target) => E(EffectKind.Heal, n, target);
        static CardEffect Attack(int n, EffectTarget target) => E(EffectKind.ModifyAttack, n, target);
        static CardEffect HP(int n, EffectTarget target) => E(EffectKind.IncreaseMaxHP, n, target);
        static readonly Dictionary<string, CardDefinition> baselineCards = BuildBaseline();
        static readonly Dictionary<string, CardDefinition> cards = BuildRevised(baselineCards);
        static Dictionary<string,CardDefinition> BuildBaseline() {var result=Build();foreach(var card in result.Values)card.Version=CardBalanceVersion.Baseline;return result;}
        public static IReadOnlyCollection<CardDefinition> AllFor(CardBalanceVersion version) => version==CardBalanceVersion.Baseline ? Array.AsReadOnly(baselineCards.Values.ToArray()) : All;
        public static IReadOnlyList<CardDefinition> DeckFor(CardBalanceVersion version) => version==CardBalanceVersion.Baseline ? Array.AsReadOnly(baselineCards.Values.Where(c=>c.CardType!=CardType.Unknown).ToArray()) : Deck;
        public static IReadOnlyList<CardDefinition> Deck { get; } = Array.AsReadOnly(cards.Values.Where(c => c.CardType != CardType.Unknown).ToArray());
        public static IReadOnlyCollection<CardDefinition> All => Array.AsReadOnly(cards.Values.ToArray());
        public static CardDefinition Get(string id) => cards.TryGetValue(id, out var definition) ? definition : throw new ArgumentException("Unknown card ID: " + id);
        public static CardDefinition Get(string id,CardBalanceVersion version) => version==CardBalanceVersion.Baseline ? baselineCards[id] : Get(id);
        public static string Symbol(Suit suit) => suit == Suit.Diamond ? "♦" : suit == Suit.Spade ? "♠" : suit == Suit.Heart ? "♥" : suit == Suit.Club ? "♣" : "◇";
        public static string RankText(Rank rank) => rank == Rank.Ace ? "A" : rank == Rank.Jack ? "J" : rank == Rank.Queen ? "Q" : rank == Rank.King ? "K" : rank == Rank.Joker ? "JOKER" : ((int)rank).ToString();

        static Dictionary<string, CardDefinition> Build()
        {
            var result = new Dictionary<string, CardDefinition>();
            void Add(Suit suit, Rank rank, int attack, int hp, string description, params CardEffect[] effects)
            {
                string id = suit + "_" + RankText(rank);
                result.Add(id, new CardDefinition(id, Symbol(suit) + RankText(rank), suit, rank, attack, hp, CardType.Normal, description, effects));
            }
            const EffectTarget self = EffectTarget.Self, ally = EffectTarget.Ally, all = EffectTarget.AllAllies, enemy = EffectTarget.Enemy, enemies = EffectTarget.AllEnemies;
            Add(Suit.Diamond, Rank.Two, 2, 2, "적 1명 피해 1", Damage(1));
            Add(Suit.Diamond, Rank.Three, 3, 2, "자신 피해 1, 적 1명 피해 2", E(EffectKind.SelfDamage, 1, self), Damage(2));
            Add(Suit.Diamond, Rank.Four, 3, 3, "무작위 적 1명 피해 2", E(EffectKind.RandomDamage, 2, enemy));
            Add(Suit.Diamond, Rank.Five, 4, 3, "적 1명 피해 2", Damage(2));
            Add(Suit.Diamond, Rank.Six, 4, 4, "자신 피해 1, 적 1명 피해 4", E(EffectKind.SelfDamage, 1, self), Damage(4));
            Add(Suit.Diamond, Rank.Seven, 5, 4, "서로 다른 적 2명 피해 2", E(EffectKind.ChainDamage, 2, enemy));
            Add(Suit.Diamond, Rank.Eight, 5, 5, "무작위 적 1명 피해 4", E(EffectKind.RandomDamage, 4, enemy));
            Add(Suit.Diamond, Rank.Nine, 6, 5, "적 1명 피해 4, 자신 공격력 +1", Damage(4), Attack(1, self));
            Add(Suit.Diamond, Rank.Ten, 7, 5, "적 1명 피해 5", Damage(5));
            Add(Suit.Diamond, Rank.Ace, 6, 7, "아군 전체 공격력 +2, 적 1명 피해 2", Attack(2, all), Damage(2));
            Add(Suit.Diamond, Rank.Jack, 7, 6, "자신 피해 2, 적 1명 피해 7", E(EffectKind.SelfDamage, 2, self), Damage(7));
            Add(Suit.Diamond, Rank.Queen, 7, 8, "서로 다른 적 2명 피해 4", E(EffectKind.ChainDamage, 4, enemy));
            Add(Suit.Diamond, Rank.King, 9, 9, "적 1명 피해 7, 자신 공격력 +2", Damage(7), Attack(2, self));
            Add(Suit.Spade, Rank.Two, 3, 1, "적 1명 공격력 -1", Attack(-1, enemy));
            Add(Suit.Spade, Rank.Three, 3, 2, "적 1명 피해 1, 공격력 -1", Damage(1), Attack(-1, enemy));
            Add(Suit.Spade, Rank.Four, 4, 2, "무작위 적 1명 피해 2", E(EffectKind.RandomDamage, 2, enemy));
            Add(Suit.Spade, Rank.Five, 4, 3, "적 1명 공격력 -2", Attack(-2, enemy));
            Add(Suit.Spade, Rank.Six, 5, 3, "적 1명 피해 2, 공격력 -1", Damage(2), Attack(-1, enemy));
            Add(Suit.Spade, Rank.Seven, 5, 4, "상대 패 무작위 1장 버리기", E(EffectKind.DiscardCard, 1));
            Add(Suit.Spade, Rank.Eight, 6, 4, "적 1명 공격력 -3", Attack(-3, enemy));
            Add(Suit.Spade, Rank.Nine, 7, 4, "적 1명 피해 3, 공격력 -2", Damage(3), Attack(-2, enemy));
            Add(Suit.Spade, Rank.Ten, 8, 5, "상대 패 1장 버리기, 적 1명 피해 3", E(EffectKind.DiscardCard, 1), Damage(3));
            Add(Suit.Spade, Rank.Ace, 7, 7, "적 전체 공격력 -2, 상대 패 1장 버리기", Attack(-2, enemies), E(EffectKind.DiscardCard, 1));
            Add(Suit.Spade, Rank.Jack, 8, 6, "적 1명 공격력 -4, 피해 4", Attack(-4, enemy), Damage(4));
            Add(Suit.Spade, Rank.Queen, 8, 8, "상대 패 2장 버리기, 적 전체 공격력 -1", E(EffectKind.DiscardCard, 2), Attack(-1, enemies));
            Add(Suit.Spade, Rank.King, 10, 9, "적 1명 피해 6, 공격력 -4", Damage(6), Attack(-4, enemy));
            Add(Suit.Heart, Rank.Two, 1, 4, "아군 1명 체력 2 회복", Heal(2, ally));
            Add(Suit.Heart, Rank.Three, 2, 4, "자신 최대 체력 +2", HP(2, self));
            Add(Suit.Heart, Rank.Four, 2, 5, "적 1명 피해 2, 자신 체력 2 회복", Damage(2), Heal(2, self));
            Add(Suit.Heart, Rank.Five, 3, 5, "아군 1명 최대 체력 +3", HP(3, ally));
            Add(Suit.Heart, Rank.Six, 3, 6, "아군 전체 체력 2 회복", Heal(2, all));
            Add(Suit.Heart, Rank.Seven, 4, 6, "적 1명 피해 3, 자신 체력 3 회복", Damage(3), Heal(3, self));
            Add(Suit.Heart, Rank.Eight, 4, 7, "아군 1명 최대 체력 +4", HP(4, ally));
            Add(Suit.Heart, Rank.Nine, 5, 7, "아군 전체 체력 3 회복", Heal(3, all));
            Add(Suit.Heart, Rank.Ten, 5, 8, "적 1명 피해 5, 자신 체력 5 회복", Damage(5), Heal(5, self));
            Add(Suit.Heart, Rank.Ace, 5, 10, "아군 전체 최대 체력 +3, 체력 3 회복", HP(3, all), Heal(3, all));
            Add(Suit.Heart, Rank.Jack, 6, 9, "적 1명 피해 5, 자신 체력 5 회복", Damage(5), Heal(5, self));
            Add(Suit.Heart, Rank.Queen, 6, 11, "아군 전체 최대 체력 +4, 체력 4 회복", HP(4, all), Heal(4, all));
            Add(Suit.Heart, Rank.King, 8, 12, "적 1명 피해 7, 자신 체력 7 회복", Damage(7), Heal(7, self));
            Add(Suit.Club, Rank.Two, 1, 3, "아군 1명 공격력 +1", Attack(1, ally));
            Add(Suit.Club, Rank.Three, 2, 3, "아군 1명 최대 체력 +2", HP(2, ally));
            Add(Suit.Club, Rank.Four, 2, 4, "카드 1장 뽑기", E(EffectKind.DrawCard, 1));
            Add(Suit.Club, Rank.Five, 3, 4, "아군 1명 공격력 +2", Attack(2, ally));
            Add(Suit.Club, Rank.Six, 3, 5, "아군 1명 공격력 +1, 최대 체력 +2", Attack(1, ally), HP(2, ally));
            Add(Suit.Club, Rank.Seven, 4, 5, "카드 2장 뽑기", E(EffectKind.DrawCard, 2));
            Add(Suit.Club, Rank.Eight, 4, 6, "아군 전체 공격력 +1", Attack(1, all));
            Add(Suit.Club, Rank.Nine, 5, 6, "아군 1명 공격력 +3, 최대 체력 +3", Attack(3, ally), HP(3, ally));
            Add(Suit.Club, Rank.Ten, 5, 7, "카드 2장 뽑기, 아군 1명 공격력 +2", E(EffectKind.DrawCard, 2), Attack(2, ally));
            Add(Suit.Club, Rank.Ace, 5, 9, "카드 2장 뽑기, 아군 전체 최대 체력 +2", E(EffectKind.DrawCard, 2), HP(2, all));
            Add(Suit.Club, Rank.Jack, 6, 8, "아군 1명 공격력 +4, 카드 1장 뽑기", Attack(4, ally), E(EffectKind.DrawCard, 1));
            Add(Suit.Club, Rank.Queen, 6, 10, "아군 전체 공격력 +2, 최대 체력 +2", Attack(2, all), HP(2, all));
            Add(Suit.Club, Rank.King, 8, 11, "아군 전체 공격력 +3, 최대 체력 +3", Attack(3, all), HP(3, all));
            for (int i = 1; i <= 2; i++)
                result.Add("Joker_" + i, new CardDefinition("Joker_" + i, "JOKER", Suit.None, Rank.Joker, 0, 0, CardType.Joker,
                    "성공 50%: 공격·최대 체력 1.5배(올림). 실패: 대상 파괴. 유닛당 한 번."));
            void Unknown(Rank rank, int attack, int hp, string description, params CardEffect[] effects)
            {
                string id = "Unknown_" + RankText(rank);
                result.Add(id, new CardDefinition(id, "UNKNOWN " + rank.ToString().ToUpperInvariant(), Suit.None, rank, attack, hp, CardType.Unknown, description, effects));
            }
            Unknown(Rank.Jack, 11, 10, "적 1명 피해 7, 자신 공격력 +3", Damage(7), Attack(3, self));
            Unknown(Rank.Queen, 9, 14, "아군 전체 공격력 +3, 최대 체력 +4", Attack(3, all), HP(4, all));
            Unknown(Rank.King, 14, 14, "적 1명 피해 10, 적 전체 공격력 -2", Damage(10), Attack(-2, enemies));
            return result;
        }
    }
}
