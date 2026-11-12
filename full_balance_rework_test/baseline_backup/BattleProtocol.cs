using System;
using System.Collections.Generic;
using System.Linq;

namespace Unknown.Battle
{
    public enum BattleAction { Summon, Attack, Target, Combine, Joker, EndTurn, Discard, Exchange }
    [Serializable] public sealed class BattleIntent
    {
        public int sequence, revision, turn, first, second;
        public BattleAction action;
    }
    [Serializable] public sealed class CardViewData
    {
        public int id, owner, attack, hp, maxHP, summonedTurn;
        public bool occupied, attacked, jokerUsed;
        public bool firstAttackHealSpent;
        public CardBalanceVersion cardVersion;
        public CardDefinition Definition=>CardDatabase.Get(definition,cardVersion);
        public string definition;
        public int pokerAttack,pokerHP,mapAttack,mapHP,jokerAttack,jokerHP,abilityAttack,abilityHP;
        public int rawAttack,rawHP;
        public bool hasStatLayers;
        public static CardViewData From(CardInstance c) => c == null ? new CardViewData() : new CardViewData {
            occupied = true,
            firstAttackHealSpent=c.FirstAttackHealSpent,cardVersion=c.Definition.Version,
            id = c.InstanceID, owner = c.Owner, definition = c.Definition.CardID, attack = c.Attack,
            hp = c.CurrentHP, maxHP = c.MaxHP, summonedTurn = c.SummonedTurn, attacked = c.HasAttacked, jokerUsed = c.JokerUsed,
            hasStatLayers=true,rawAttack=c.RawAttack,rawHP=c.RawHP,
            pokerAttack=c.PokerAttack,pokerHP=c.PokerHP,mapAttack=c.MapAttack,mapHP=c.MapHP,jokerAttack=c.JokerAttack,jokerHP=c.JokerHP,abilityAttack=c.AbilityAttack,abilityHP=c.AbilityHP };
        public CardInstance ToCard() {
            if(!occupied)return null;
            var c=new CardInstance(id, CardDatabase.Get(definition,cardVersion), owner) {
            FirstAttackHealSpent=firstAttackHealSpent,
            PokerAttack=pokerAttack,PokerHP=pokerHP,Attack = attack, CurrentHP = hp, MaxHP = maxHP, SummonedTurn = summonedTurn, HasAttacked = attacked, JokerUsed = jokerUsed,
            MapAttack=mapAttack,MapHP=mapHP,JokerAttack=jokerAttack,JokerHP=jokerHP,AbilityAttack=abilityAttack,AbilityHP=abilityHP };
            if(hasStatLayers)c.ImportCore(rawAttack,rawHP);
            return c;
        }
    }
    [Serializable] public sealed class PlayerViewData
    {
        public int hp, mana, maxMana, deckCount, handCount, unknownCreated, unknownProgress, jokerSuccesses, jokerChanceDelta;
        public CardViewData[] hand, board;
        public PokerState poker;
        public bool manualDiscardUsed;
        public int discardCount;
    }
    [Serializable] public sealed class EventViewData
    {
        public BattleEventKind kind;
        public int owner, targetOwner, source, target, amount, secondary, slot;
        public Suit suit;
        public bool chain;
        public string definition;
        public CardBalanceVersion cardVersion;
        public BattleEvent ToEvent() => new BattleEvent(kind, owner, source, target, amount, secondary, slot,
            targetOwner, suit, chain, string.IsNullOrEmpty(definition) ? null : CardDatabase.Get(definition,cardVersion));
        public static EventViewData For(BattleEvent e, int recipient)
        {
            // Hand operations reveal only their occurrence/count to the other player.
            bool hidden = e.Owner != recipient && (e.Kind == BattleEventKind.Draw || e.Kind == BattleEventKind.Discard || e.Kind == BattleEventKind.Combine || e.Kind==BattleEventKind.CardGranted || e.Kind==BattleEventKind.HandReturn);
            return new EventViewData { kind = hidden && e.Kind==BattleEventKind.CardGranted ? BattleEventKind.Draw : e.Kind, owner = e.Owner, targetOwner = e.TargetOwner,
                cardVersion=e.Definition?.Version ?? CardBalanceVersion.Revised,
                source = hidden || (e.Owner!=recipient && e.Kind==BattleEventKind.JokerTravel) ? -1 : e.SourceID, target = hidden ? -1 : e.TargetID,
                amount = e.Amount, secondary = hidden && e.Kind!=BattleEventKind.Draw && e.Kind!=BattleEventKind.Discard ? 0 : e.Secondary, slot = e.Slot,
                suit = hidden ? Suit.None : e.Suit, chain = e.Chain, definition = hidden ? null : e.Definition?.CardID };
        }
    }
    [Serializable] public sealed class BattleSnapshot
    {
        public int revision, recipient, active, turn, winner;
        public int handLimit=8;
        public VictoryReason victoryReason;
        public double deadline;
        public double turnDuration = 40;
        public bool clockRunning = true;
        public bool expired, busy;
        public bool exchangePending;
        public CardBalanceVersion cardVersion;
        public int pendingSource=-1,pendingIndex;
        public int[] pendingTargetKinds,pendingTargetIDs,pendingDistinctIDs;
        public PlayerViewData[] players;
        public int[] targets;
        public string prompt;
        public string error;
        public string[] log;
        public EventViewData[] events;
        public BattleMap map;
        public MapEventView mapEvent;
        public SuitMapEffect suitEffect;
        public MapDefinition[] mapRules;
        public CardViewData revealedCard;
        public int revealedIndex=-1;
        public static BattleSnapshot For(BattleEngine engine, int recipient, int revision, double deadline, bool busy, IReadOnlyList<BattleEvent> events, double turnDuration = 40, bool clockRunning = true)
        {
            var s = engine.State;
            return new BattleSnapshot { revision = revision, recipient = recipient, active = s.ActivePlayer,
                handLimit=engine.Config.HandLimit,
                cardVersion=engine.Config.CardVersion,exchangePending=engine.NeedsExchange,pendingSource=engine.PendingSourceID,pendingIndex=engine.PendingIndex,
                pendingTargetKinds=engine.PendingKinds,pendingTargetIDs=engine.PendingIDs,pendingDistinctIDs=engine.PendingDistinct,
                turn = s.TurnNumber, winner = s.Winner, victoryReason = s.VictoryReason, expired = s.TurnExpired, deadline = deadline, busy = busy, turnDuration = turnDuration, clockRunning = clockRunning,
                map=engine.Config.Map, mapEvent=MapEventView.For(engine.MapEvent,recipient),
                suitEffect=engine.SuitEffect?.Copy(),mapRules=engine.Config.MapRules,
                revealedCard=engine.RevealedFor(recipient)==null ? null : CardViewData.From(engine.RevealedFor(recipient)), revealedIndex=engine.RevealedIndexFor(recipient),
                players = s.Players.Select((p, owner) => new PlayerViewData { hp = p.HP, mana = p.CurrentMana, maxMana = p.MaxMana,
                    jokerSuccesses=owner==recipient ? p.JokerSuccesses : 0, jokerChanceDelta=owner==recipient ? p.JokerChanceDelta : 0,
                    deckCount = p.DeckCount, handCount = p.Hand.Count, unknownCreated = owner == recipient ? p.UnknownCreated : 0,
                    unknownProgress = owner == recipient ? p.UnknownProgress : 0,
                    poker=p.Poker.Copy(),manualDiscardUsed=p.ManualDiscardUsed,discardCount=p.DiscardCount,
                    hand = owner == recipient ? p.Hand.Select(CardViewData.From).ToArray() : new CardViewData[0],
                    board = p.Board.Select(CardViewData.From).ToArray() }).ToArray(),
                targets = Enumerable.Range(0, 2 * engine.Config.FieldSlots).Where(i => engine.IsValidEffectTarget(i / engine.Config.FieldSlots, i % engine.Config.FieldSlots)).ToArray(),
                prompt = engine.TargetPrompt, log = engine.VisibleLog(recipient).ToArray(), events = events.Where(e => (e.Kind != BattleEventKind.Combine && e.Kind != BattleEventKind.HandRefill) || e.Owner == recipient).Select(e => EventViewData.For(e, recipient)).ToArray() };
        }
    }
    public sealed partial class BattleEngine
    {
        bool viewOnly;
        int[] viewTargets = new int[0];
        bool viewExchange;
        string viewPrompt = "";
        // A view does not shuffle, draw, create a hidden deck, or execute commands.
        BattleEngine(BattleSnapshot s)
        {
            rules = new BattleConfig { FieldSlots = s.players[0].board.Length, Map=s.map,HandLimit=s.handLimit,CardVersion=s.cardVersion };
            State = new BattleState(rules); viewOnly = true; ApplyView(s);
        }
        public static BattleEngine CreateView(BattleSnapshot s) => new BattleEngine(s);
        public void ApplyView(BattleSnapshot s)
        {
            if (!viewOnly) throw new InvalidOperationException("Authority cannot import a client view.");
            State.ActivePlayer = s.active; State.TurnNumber = s.turn; State.Winner = s.winner; State.VictoryReason = s.victoryReason; State.TurnExpired = s.expired;
            rules.Map=s.map; MapEvent=s.mapEvent?.ToResult(); revealViewer=s.recipient;
            rules.MapRules=(s.mapRules ?? MapDefinition.Defaults()).Select(d=>d.Copy()).ToArray(); SuitEffect=s.suitEffect?.Copy();
            viewRevealed=s.revealedCard?.ToCard(); viewRevealedIndex=s.revealedIndex;
            State.Entries.Clear(); State.Entries.AddRange(s.log); State.LogVersion = s.revision;
            State.EntryOwners.Clear();
            viewTargets = s.targets; viewPrompt = s.prompt;viewExchange=s.exchangePending;rules.CardVersion=s.cardVersion;
            for (int owner = 0; owner < 2; owner++)
            {
                var p = State.Players[owner]; var v = s.players[owner];
                p.HP = v.hp; p.CurrentMana = v.mana; p.MaxMana = v.maxMana; p.UnknownCreated = v.unknownCreated;
                p.JokerSuccesses=v.jokerSuccesses; p.JokerChanceDelta=v.jokerChanceDelta;
                p.Poker=v.poker?.Copy() ?? new PokerState();
                p.ManualDiscardUsed=v.manualDiscardUsed;p.DiscardCards.Clear();for(int i=0;i<v.discardCount;i++)p.DiscardCards.Add(null);
                p.HandCards.Clear(); p.DeckCards.Clear();
                // Count placeholders exist only in memory, with no identity or card definition.
                for (int i = 0; i < v.deckCount; i++) p.DeckCards.Add(null);
                if (owner == s.recipient) p.HandCards.AddRange(v.hand.Select(c => c.ToCard()));
                else for (int i = 0; i < v.handCount; i++) p.HandCards.Add(null);
                for (int i = 0; i < p.Slots.Length; i++) p.Slots[i] = v.board[i]?.ToCard();
            }
        }
    }
    /// <summary>Transport-independent authority. Actor comes from the authenticated connection, never the payload.</summary>
    public sealed class BattleAuthority
    {
        public BattleEngine Engine { get; }
        public int Revision { get; private set; }
        readonly int[] sequences = new int[2];
        public BattleAuthority(int seed, BattleConfig config = null) { Engine = new BattleEngine(seed, config); }
        public bool Execute(int actor, BattleIntent i)
        {
            if (actor < 0 || actor > 1 || i == null || i.sequence != sequences[actor] + 1) return false;
            sequences[actor] = i.sequence;
            if (i.revision != Revision || i.turn != Engine.State.TurnNumber || actor != Engine.State.ActivePlayer) return false;
            bool ok=BattleCommands.Execute(Engine,actor,i.action,i.first,i.second);
            if (ok) Revision++;
            return ok;
        }
        public void AdvanceRevision() { Revision++; }
        public void RejectSequence(int actor, int sequence) { if (sequence == sequences[actor] + 1) sequences[actor] = sequence; }
    }
}
