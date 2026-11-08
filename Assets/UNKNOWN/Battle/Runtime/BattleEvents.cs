namespace Unknown.Battle
{
    public enum BattleEventKind
    {
        TurnStart, TimeOut, ManaSpent, Summon, Attack, Damage, Heal, AttackChanged,
        MaxHPChanged, Draw, Discard, Destroy, Combine, JokerTravel, JokerSuccess, JokerFailure, GameOver, HandRefill, MapEvent, CardGranted, HandReturn, HeroChanged, SuitMapTurn, ManaGained, PokerChanged
    }

    /// <summary>Immutable resolved result. No mutable unit reference survives destruction.</summary>
    public sealed class BattleEvent
    {
        public BattleEventKind Kind { get; }
        public int Owner { get; }
        public int TargetOwner { get; }
        public int SourceID { get; }
        public int TargetID { get; }
        public int Amount { get; }
        public int Secondary { get; }
        public int Slot { get; }
        public Suit Suit { get; }
        public bool Chain { get; }
        public CardDefinition Definition { get; }
        public BattleEvent(BattleEventKind kind, int owner, int source = -1, int target = -1, int amount = 0,
            int secondary = 0, int slot = -1, int targetOwner = -1, Suit suit = Suit.None, bool chain = false, CardDefinition definition = null)
        {
            Kind = kind; Owner = owner; SourceID = source; TargetID = target; Amount = amount;
            Secondary = secondary; Slot = slot; TargetOwner = targetOwner; Suit = suit; Chain = chain; Definition = definition;
        }
    }
}
