using System;
using System.Collections.Generic;
using System.Linq;

namespace Unknown.Battle
{
    public enum VictoryReason { None, OpponentHPZero, UnknownComplete }
    public sealed class BattleConfig
    {
        public int StartingHP { get; set; } = 30;
        public int StartingHand { get; set; } = 5;
        public int HandLimit { get; set; } = 10;
        public int FieldSlots { get; set; } = 5;
        public int StartingMana { get; set; } = 3;
        public int ManaCap { get; set; } = 10;
        public int UnknownLimit { get; set; } = 3;
        public bool ShuffleDecks { get; set; } = true;
        internal BattleConfig Copy() => (BattleConfig)MemberwiseClone();
    }

    public interface IRandomSource { int Next(int exclusiveMax); }
    public sealed class SeededRandom : IRandomSource
    {
        readonly Random random;
        public SeededRandom(int seed) { random = new Random(seed); }
        public int Next(int exclusiveMax) => random.Next(exclusiveMax);
    }

    public sealed class PlayerState
    {
        internal readonly List<CardInstance> HandCards = new List<CardInstance>();
        internal readonly List<CardInstance> DeckCards = new List<CardInstance>();
        internal readonly List<CardInstance> DiscardCards = new List<CardInstance>();
        internal readonly CardInstance[] Slots;
        public IReadOnlyList<CardInstance> Hand => HandCards.AsReadOnly();
        public IReadOnlyList<CardInstance> Board { get; }
        public int DeckCount => DeckCards.Count;
        public int DiscardCount => DiscardCards.Count;
        public int HP { get; internal set; }
        public int CurrentMana { get; internal set; }
        public int MaxMana { get; internal set; }
        public int TurnsStarted { get; internal set; }
        public int UnknownCreated { get; internal set; }
        internal PlayerState(int hp, int slots) { HP = hp; Slots = new CardInstance[slots]; Board = Array.AsReadOnly(Slots); }
    }

    public sealed class BattleState
    {
        public IReadOnlyList<PlayerState> Players { get; }
        public int ActivePlayer { get; internal set; }
        public int TurnNumber { get; internal set; }
        public int SummonsThisTurn { get; internal set; }
        public bool TurnExpired { get; internal set; }
        public int Winner { get; internal set; } = -1;
        public VictoryReason VictoryReason { get; internal set; }
        public bool IsOver => Winner >= 0;
        public string LastError { get; internal set; } = "";
        public int LogVersion { get; internal set; }
        internal readonly List<string> Entries = new List<string>();
        internal readonly List<int> EntryOwners = new List<int>();
        public IReadOnlyList<string> Log => Entries.AsReadOnly();
        internal BattleState(BattleConfig config)
        { Players = Array.AsReadOnly(new[] { new PlayerState(config.StartingHP, config.FieldSlots), new PlayerState(config.StartingHP, config.FieldSlots) }); }
    }

    /// <summary>Pure C# rules. Commands carry the acting player; UI never rolls random results or edits state.</summary>
    public sealed partial class BattleEngine
    {
        readonly BattleConfig rules;
        readonly IRandomSource random;
        int nextID;
        CardInstance source;
        int effectIndex;
        readonly Dictionary<EffectTarget, int> selectedTargets = new Dictionary<EffectTarget, int>();
        readonly HashSet<int> chainHits = new HashSet<int>();
        readonly List<BattleEvent> events = new List<BattleEvent>();
        public IReadOnlyList<BattleEvent> DrainEvents()
        { var result = events.ToArray(); events.Clear(); return result; }
        void Emit(BattleEvent entry) => events.Add(entry);
        public BattleState State { get; }
        public BattleConfig Config => rules.Copy();
        public bool NeedsTarget => viewOnly ? viewTargets.Length > 0 : source != null;
        public string TargetPrompt
        {
            get
            {
                if (viewOnly) return viewPrompt;
                if (source == null) return "";
                var effect = source.Definition.EffectList[effectIndex];
                return effect.Kind == EffectKind.ChainDamage ? $"연쇄 피해: 서로 다른 적 선택 ({chainHits.Count + 1}/2)"
                    : effect.Target == EffectTarget.Ally ? "소환 효과: 아군 유닛을 선택하세요 (소환한 자신도 가능)" : "소환 효과: 적 유닛을 선택하세요";
            }
        }

        public BattleEngine(int seed, BattleConfig config = null, IRandomSource randomSource = null)
        {
            rules = (config ?? new BattleConfig()).Copy();
            if (rules.FieldSlots < 1 || rules.HandLimit < 1 || rules.StartingHP < 1 || rules.StartingHand < 0 || rules.StartingHand > rules.HandLimit
                || rules.UnknownLimit < 0 || rules.StartingMana < 0 || rules.ManaCap < rules.StartingMana || rules.ManaCap > 10) throw new ArgumentOutOfRangeException(nameof(config));
            random = randomSource ?? new SeededRandom(seed);
            State = new BattleState(rules);
            foreach (var player in State.Players) { player.MaxMana = rules.StartingMana; player.CurrentMana = rules.StartingMana; }
            for (int p = 0; p < 2; p++)
            {
                foreach (var definition in CardDatabase.Deck) State.Players[p].DeckCards.Add(NewCard(definition, p));
                if (rules.ShuffleDecks)
                {
                    var deck = State.Players[p].DeckCards;
                    for (int i = deck.Count - 1; i > 0; i--) { int j = random.Next(i + 1); var card = deck[i]; deck[i] = deck[j]; deck[j] = card; }
                }
                Draw(p, rules.StartingHand);
            }
            BeginTurn(0);
        }

        internal CardInstance NewCard(CardDefinition definition, int owner) => new CardInstance(++nextID, definition, owner);
        public IEnumerable<string> VisibleLog(int recipient) => State.Entries.Where((entry,index) => index >= State.EntryOwners.Count || State.EntryOwners[index] < 0 || State.EntryOwners[index] == recipient);
        internal void Log(string message, int privateOwner = -1)
        {
            State.Entries.Add(message);
            State.EntryOwners.Add(privateOwner);
            State.LogVersion++;
            if (State.Entries.Count > 100) { State.Entries.RemoveAt(0); State.EntryOwners.RemoveAt(0); }
        }
        bool Fail(string message) { State.LastError = message; return false; }
        bool Guard(int actor, bool allowTarget = false)
        {
            if (viewOnly) return Fail("온라인 판정은 Host에서만 수행합니다.");
            State.LastError = "";
            if (State.IsOver) return Fail("게임이 종료되었습니다.");
            if (State.TurnExpired) return Fail("TIME OUT: 새로운 행동을 시작할 수 없습니다.");
            if (actor != State.ActivePlayer) return Fail("현재 턴의 플레이어만 행동할 수 있습니다.");
            if (!allowTarget && NeedsTarget) return Fail("소환 효과의 대상을 먼저 선택하세요.");
            return true;
        }
        internal void Draw(int owner, int count)
        {
            if (State.IsOver || viewOnly) return;
            var player = State.Players[owner];
            for (int i = 0; i < count; i++)
            {
                if (player.DeckCards.Count == 0) { Log($"P{owner + 1}: 덱이 비어 드로우 생략"); continue; }
                int last = player.DeckCards.Count - 1;
                var card = player.DeckCards[last]; player.DeckCards.RemoveAt(last);
                Emit(new BattleEvent(BattleEventKind.Draw, owner, target: card.InstanceID, definition: card.Definition));
                if (player.HandCards.Count >= rules.HandLimit)
                { player.DiscardCards.Add(card); Emit(new BattleEvent(BattleEventKind.Discard, owner, target: card.InstanceID)); Log($"P{owner + 1}: 패가 가득 차 뽑은 카드 소멸"); }
                else { player.HandCards.Add(card); Log($"P{owner + 1}: 카드 1장 드로우"); }
            }
        }
        void BeginTurn(int player)
        {
            State.ActivePlayer = player; State.TurnNumber++; State.SummonsThisTurn = 0;
            State.TurnExpired = false;
            var current = State.Players[player];
            Log($"── 턴 {State.TurnNumber} · P{player + 1} ──");
            Emit(new BattleEvent(BattleEventKind.TurnStart, player));
            if (current.HandCards.Count == 0)
            {
                int count = Math.Min(Math.Min(5, rules.HandLimit), current.DeckCount);
                Emit(new BattleEvent(BattleEventKind.HandRefill, player, amount: count));
                // The refill replaces the ordinary draw. Empty decks retain their existing no-fatigue behavior.
                Draw(player, count > 0 ? count : 1);
            }
            else Draw(player, 1);
            current.TurnsStarted++;
            if (current.TurnsStarted > 1) current.MaxMana = Math.Min(rules.ManaCap, current.MaxMana + 1);
            current.CurrentMana = current.MaxMana;
            foreach (var unit in current.Slots) if (unit != null) unit.HasAttacked = false;
        }
        public bool ExpireTurn(int actor)
        {
            if (viewOnly || State.IsOver || State.TurnExpired || actor != State.ActivePlayer) return false;
            State.TurnExpired = true; Log("TIME OUT: 진행 중인 효과 완료 후 턴 종료");
            Emit(new BattleEvent(BattleEventKind.TimeOut, actor));
            // Finish an already committed summon without accepting another player command.
            while (NeedsTarget)
            {
                bool chosen = false;
                for (int owner = 0; owner < 2 && !chosen; owner++)
                    for (int slot = 0; slot < rules.FieldSlots && !chosen; slot++)
                        if (IsValidEffectTarget(owner, slot)) { ApplySelectedTarget(owner, slot); chosen = true; }
                if (!chosen) ResolveEffects();
            }
            return true;
        }
        public bool FinishExpiredTurn(int actor)
        {
            if (State.IsOver || !State.TurnExpired || NeedsTarget || actor != State.ActivePlayer) return false;
            BeginTurn(1 - actor); return true;
        }
        void SpendMana(int owner, int amount)
        {
            var player = State.Players[owner]; int before = player.CurrentMana; player.CurrentMana -= amount;
            Emit(new BattleEvent(BattleEventKind.ManaSpent, owner, amount: amount, secondary: before));
        }
        public bool EndTurn(int actor)
        {
            if (!Guard(actor)) return false;
            BeginTurn(1 - actor); return true;
        }
        public bool Summon(int actor, int handID, int slot)
        {
            if (!Guard(actor)) return false;
            var player = State.Players[actor];
            var card = player.HandCards.Find(c => c.InstanceID == handID);
            if (card == null) return Fail("패에 있는 카드를 선택하세요.");
            if (card.Definition.CardType == CardType.Joker) return Fail("조커는 유닛으로 소환할 수 없습니다.");
            if (slot < 0 || slot >= rules.FieldSlots || player.Slots[slot] != null) return Fail("비어 있는 아군 필드 칸을 선택하세요.");
            if (player.CurrentMana < card.Definition.ManaCost) return Fail("마나가 부족합니다");
            SpendMana(actor, card.Definition.ManaCost);
            player.HandCards.Remove(card); player.Slots[slot] = card;
            card.SummonedTurn = State.TurnNumber; card.HasAttacked = false; State.SummonsThisTurn++;
            Log($"P{actor + 1}: {card.Definition.CardName} 소환");
            Emit(new BattleEvent(BattleEventKind.Summon, actor, source: card.InstanceID, target: card.InstanceID, slot: slot, definition: card.Definition));
            source = card; effectIndex = 0; selectedTargets.Clear(); chainHits.Clear();
            ResolveEffects(); return true;
        }
        public bool CanAttack(int actor, CardInstance unit) => !State.IsOver && !State.TurnExpired && !NeedsTarget && actor == State.ActivePlayer && unit != null
            && unit.Owner == actor && IsOnBoard(unit) && unit.SummonedTurn < State.TurnNumber && !unit.HasAttacked;

        public bool Attack(int actor, int attackerID, int enemySlot = -1)
        {
            if (!Guard(actor)) return false;
            var attacker = State.Players[actor].Slots.FirstOrDefault(c => c != null && c.InstanceID == attackerID);
            if (!CanAttack(actor, attacker)) return Fail("소환한 턴이거나 이미 공격한 유닛입니다.");
            var opponent = State.Players[1 - actor];
            if (enemySlot == -1)
            {
                if (opponent.Slots.Any(c => c != null)) return Fail("상대 필드에 유닛이 있어 직접 공격할 수 없습니다.");
                int damage = Math.Min(opponent.HP, attacker.Attack);
                attacker.HasAttacked = true; opponent.HP = Math.Max(0, opponent.HP - attacker.Attack);
                Emit(new BattleEvent(BattleEventKind.Attack, actor, source: attacker.InstanceID, amount: damage, targetOwner: 1 - actor));
                Log($"{attacker.Definition.CardName} → P{2 - actor} 직접 피해 {damage}");
                if (opponent.HP <= 0) FinishGame(actor, VictoryReason.OpponentHPZero);
                return true;
            }
            if (enemySlot < 0 || enemySlot >= rules.FieldSlots || opponent.Slots[enemySlot] == null) return Fail("유효한 적 유닛을 선택하세요.");
            var defender = opponent.Slots[enemySlot];
            attacker.HasAttacked = true;
            // Both attacks use the pre-combat stats, even if one of the hits is lethal.
            int outgoing = attacker.Attack, incoming = defender.Attack;
            int actualOutgoing = Math.Min(defender.CurrentHP, outgoing), actualIncoming = Math.Min(attacker.CurrentHP, incoming);
            Emit(new BattleEvent(BattleEventKind.Attack, actor, attacker.InstanceID, defender.InstanceID,
                actualOutgoing, actualIncoming, targetOwner: 1 - actor));
            attacker.CurrentHP -= incoming; defender.CurrentHP -= outgoing;
            Log($"{attacker.Definition.CardName} ↔ {defender.Definition.CardName}: 피해 {actualOutgoing} / 반격 {actualIncoming}");
            RemoveDead(); return true;
        }
        public CardDefinition CombinationPreview(int actor, int aceID, int faceID)
        {
            if (actor < 0 || actor > 1 || aceID == faceID) return null;
            var hand = State.Players[actor].HandCards;
            var ace = hand.Find(c => c.InstanceID == aceID); var face = hand.Find(c => c.InstanceID == faceID);
            if (ace == null || face == null || ace.Definition.CardType != CardType.Normal || face.Definition.CardType != CardType.Normal
                || ace.Definition.Rank != Rank.Ace || face.Definition.Rank < Rank.Jack || face.Definition.Rank > Rank.King) return null;
            return CardDatabase.Get("Unknown_" + CardDatabase.RankText(face.Definition.Rank));
        }
        public bool Combine(int actor, int aceID, int faceID)
        {
            if (!Guard(actor)) return false;
            var player = State.Players[actor];
            if (player.UnknownCreated >= rules.UnknownLimit) return Fail("UNKNOWN은 플레이어당 게임에서 최대 3장만 생성할 수 있습니다.");
            var result = CombinationPreview(actor, aceID, faceID);
            if (result == null) return Fail("패의 A와 J/Q/K를 순서대로 선택하세요.");
            foreach (int id in new[] { aceID, faceID })
            { var card = player.HandCards.Find(c => c.InstanceID == id); player.HandCards.Remove(card); player.DiscardCards.Add(card); }
            var combined = NewCard(result, actor); player.HandCards.Add(combined); player.UnknownCreated++;
            Emit(new BattleEvent(BattleEventKind.Combine, actor, aceID, faceID, secondary: combined.InstanceID, definition: result));
            Log($"P{actor + 1}: UNKNOWN 조합 ({player.UnknownCreated}/{rules.UnknownLimit})", actor);
            if (player.UnknownCreated >= 3) FinishGame(actor, VictoryReason.UnknownComplete);
            return true;
        }
        void FinishGame(int winner, VictoryReason reason)
        {
            if (State.IsOver) return;
            State.Winner = winner; State.VictoryReason = reason;
            Log($"P{winner + 1} 승리! · " + (reason == VictoryReason.UnknownComplete ? "UNKNOWN COMPLETE" : "OPPONENT HP ZERO"));
            Emit(new BattleEvent(BattleEventKind.GameOver, winner, secondary: (int)reason));
        }
        public bool Gamble(int actor, int jokerID, int targetID)
        {
            if (!Guard(actor)) return false;
            var player = State.Players[actor];
            var joker = player.HandCards.Find(c => c.InstanceID == jokerID && c.Definition.CardType == CardType.Joker);
            var unit = player.Slots.FirstOrDefault(c => c != null && c.InstanceID == targetID);
            if (joker == null || unit == null) return Fail("패의 조커와 아군 필드 유닛을 선택하세요.");
            if (unit.JokerUsed) return Fail("이 유닛은 이미 조커를 사용했습니다.");
            if (player.CurrentMana < joker.Definition.ManaCost) return Fail("마나가 부족합니다");
            SpendMana(actor, joker.Definition.ManaCost);
            player.HandCards.Remove(joker); player.DiscardCards.Add(joker); unit.JokerUsed = true;
            Emit(new BattleEvent(BattleEventKind.JokerTravel, actor, joker.InstanceID, unit.InstanceID));
            if (random.Next(2) == 0)
            {
                int oldAttack = unit.Attack, oldMax = unit.MaxHP;
                unit.Attack = (int)Math.Ceiling(unit.Attack * 1.5);
                int max = (int)Math.Ceiling(unit.MaxHP * 1.5); unit.CurrentHP += max - unit.MaxHP; unit.MaxHP = max;
                Emit(new BattleEvent(BattleEventKind.JokerSuccess, actor, target: unit.InstanceID));
                Emit(new BattleEvent(BattleEventKind.AttackChanged, actor, target: unit.InstanceID, amount: unit.Attack - oldAttack));
                Emit(new BattleEvent(BattleEventKind.MaxHPChanged, actor, target: unit.InstanceID, amount: max - oldMax));
                Log($"조커 성공! {unit.Definition.CardName} 공격·최대 체력 1.5배");
            }
            else { Emit(new BattleEvent(BattleEventKind.JokerFailure, actor, target: unit.InstanceID)); unit.CurrentHP = 0; Log($"조커 실패! {unit.Definition.CardName} 파괴"); RemoveDead(); }
            return true;
        }

        internal bool IsOnBoard(CardInstance unit) => unit != null && State.Players[unit.Owner].Slots.Contains(unit) && unit.CurrentHP > 0;
        List<CardInstance> Targets(EffectTarget target) => State.Players[target == EffectTarget.Enemy || target == EffectTarget.AllEnemies ? 1 - source.Owner : source.Owner]
            .Slots.Where(c => c != null && c.CurrentHP > 0).ToList();
        public bool IsValidEffectTarget(int owner, int slot)
        {
            if (viewOnly) return Array.IndexOf(viewTargets, owner * rules.FieldSlots + slot) >= 0;
            if (!NeedsTarget || owner < 0 || owner > 1 || slot < 0 || slot >= rules.FieldSlots) return false;
            var card = State.Players[owner].Slots[slot];
            var effect = source.Definition.EffectList[effectIndex];
            return card != null && Targets(effect.Target).Contains(card) && (effect.Kind != EffectKind.ChainDamage || !chainHits.Contains(card.InstanceID));
        }
        public bool SelectEffectTarget(int actor, int owner, int slot)
        {
            if (!Guard(actor, true)) return false;
            if (!IsValidEffectTarget(owner, slot)) return Fail("강조된 유효한 효과 대상을 선택하세요.");
            ApplySelectedTarget(owner, slot); return true;
        }
        void ApplySelectedTarget(int owner, int slot)
        {
            var effect = source.Definition.EffectList[effectIndex]; var card = State.Players[owner].Slots[slot];
            if (effect.Kind == EffectKind.ChainDamage) chainHits.Add(card.InstanceID);
            else selectedTargets[effect.Target] = card.InstanceID;
            Apply(effect, card);
            if (effect.Kind != EffectKind.ChainDamage) effectIndex++;
            ResolveEffects();
        }
        void ResolveEffects()
        {
            while (source != null && effectIndex < source.Definition.EffectList.Count)
            {
                if (!IsOnBoard(source)) { Log("소환 유닛 파괴: 남은 소환 효과 취소"); break; }
                var effect = source.Definition.EffectList[effectIndex];
                if (effect.Kind == EffectKind.DrawCard) { Draw(source.Owner, effect.Amount); effectIndex++; continue; }
                if (effect.Kind == EffectKind.DiscardCard)
                {
                    var enemy = State.Players[1 - source.Owner];
                    for (int i = 0; i < effect.Amount && enemy.HandCards.Count > 0; i++)
                    { int index = random.Next(enemy.HandCards.Count); Emit(new BattleEvent(BattleEventKind.Discard, 1 - source.Owner, source.InstanceID, enemy.HandCards[index].InstanceID, suit: source.Definition.Suit));
                        enemy.DiscardCards.Add(enemy.HandCards[index]); enemy.HandCards.RemoveAt(index); Log("상대 패 무작위 1장 버림"); }
                    effectIndex++; continue;
                }
                if (effect.Target == EffectTarget.Self) { Apply(effect, source); effectIndex++; continue; }
                var targets = Targets(effect.Target);
                if (effect.Target == EffectTarget.AllAllies || effect.Target == EffectTarget.AllEnemies)
                { foreach (var target in targets) Apply(effect, target); effectIndex++; continue; }
                if (effect.Kind == EffectKind.RandomDamage)
                { if (targets.Count > 0) Apply(effect, targets[random.Next(targets.Count)]); effectIndex++; continue; }
                if (effect.Kind == EffectKind.ChainDamage)
                {
                    if (chainHits.Count < 2 && targets.Any(c => !chainHits.Contains(c.InstanceID))) return;
                    chainHits.Clear(); effectIndex++; continue;
                }
                if (selectedTargets.TryGetValue(effect.Target, out int id))
                { var target = targets.Find(c => c.InstanceID == id); if (target != null) Apply(effect, target); effectIndex++; continue; }
                if (targets.Count == 0) { Log("대상 없음: 해당 소환 효과 생략"); effectIndex++; continue; }
                return;
            }
            source = null; selectedTargets.Clear(); chainHits.Clear();
        }
        void Apply(CardEffect effect, CardInstance target)
        {
            if (!IsOnBoard(target)) return;
            switch (effect.Kind)
            {
                case EffectKind.Damage: case EffectKind.SelfDamage: case EffectKind.ChainDamage: case EffectKind.RandomDamage:
                    int actual = Math.Min(target.CurrentHP, effect.Amount); target.CurrentHP -= effect.Amount;
                    Emit(new BattleEvent(BattleEventKind.Damage, target.Owner, source.InstanceID, target.InstanceID, actual, suit: source.Definition.Suit, chain: effect.Kind == EffectKind.ChainDamage));
                    Log($"{target.Definition.CardName}: 피해 {actual}"); break;
                case EffectKind.Heal:
                    int healed = Math.Min(target.MaxHP - target.CurrentHP, effect.Amount); target.CurrentHP += healed;
                    Emit(new BattleEvent(BattleEventKind.Heal, target.Owner, source.InstanceID, target.InstanceID, healed, suit: source.Definition.Suit));
                    Log($"{target.Definition.CardName}: 체력 +{healed}"); break;
                case EffectKind.ModifyAttack:
                    int attackBefore = target.Attack; target.Attack = Math.Max(0, target.Attack + effect.Amount);
                    Emit(new BattleEvent(BattleEventKind.AttackChanged, target.Owner, source.InstanceID, target.InstanceID, target.Attack - attackBefore, suit: source.Definition.Suit));
                    Log($"{target.Definition.CardName}: 공격력 {target.Attack - attackBefore:+0;-0;0}"); break;
                case EffectKind.IncreaseMaxHP:
                    target.MaxHP += effect.Amount; target.CurrentHP += effect.Amount;
                    Emit(new BattleEvent(BattleEventKind.MaxHPChanged, target.Owner, source.InstanceID, target.InstanceID, effect.Amount, suit: source.Definition.Suit));
                    Log($"{target.Definition.CardName}: 최대·현재 체력 +{effect.Amount}"); break;
            }
            RemoveDead();
        }
        void RemoveDead()
        {
            foreach (var player in State.Players)
                for (int i = 0; i < rules.FieldSlots; i++)
                    if (player.Slots[i] != null && player.Slots[i].CurrentHP <= 0)
                    { var dead = player.Slots[i]; Emit(new BattleEvent(BattleEventKind.Destroy, dead.Owner, target: dead.InstanceID, slot: i));
                        player.Slots[i] = null; player.DiscardCards.Add(dead); Log($"{dead.Definition.CardName} 파괴"); }
        }
    }
}
