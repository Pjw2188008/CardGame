using System;
using System.Collections.Generic;
using System.Linq;
using Unknown.Battle;

// Compiled with the two production model files by Run-BattleTests.ps1. No Unity runtime required.
static class BattleRulesTests
{
    static int passed, failed;
    static void Check(bool value, string message = "assertion failed") { if (!value) throw new Exception(message); }
    static void Equal<T>(T expected, T actual) { Check(Equals(expected, actual), $"expected {expected}, actual {actual}"); }
    static void Test(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error.Message); }
    }
    sealed class Dice : IRandomSource
    {
        public int Value;
        public int Calls;
        public int Next(int max) { Calls++; return Value % max; }
    }
    static BattleEngine Fresh(IRandomSource dice = null, BattleConfig config = null)
    {
        var game = new BattleEngine(42, config ?? new BattleConfig { StartingHand = 0, ShuffleDecks = false, StartingMana = 10, ManaCap = 10 }, dice);
        foreach (var p in game.State.Players) { p.HandCards.Clear(); }
        return game;
    }
    static CardInstance Hand(BattleEngine game, int owner, string id)
    { var card = game.NewCard(CardDatabase.Get(id), owner); game.State.Players[owner].HandCards.Add(card); return card; }
    static CardInstance Unit(BattleEngine game, int owner, string id, int slot = 0)
    { var card = game.NewCard(CardDatabase.Get(id), owner); card.SummonedTurn = 0; game.State.Players[owner].Slots[slot] = card; return card; }
    static void FinishEffects(BattleEngine game)
    {
        int attempts = 0;
        while (game.NeedsTarget && attempts++ < 20)
        {
            bool found = false;
            for (int owner = 0; owner < 2 && !found; owner++)
                for (int slot = 0; slot < game.Config.FieldSlots && !found; slot++)
                    if (game.IsValidEffectTarget(owner, slot))
                    { Check(game.SelectEffectTarget(game.State.ActivePlayer, owner, slot)); found = true; }
            Check(found, "pending effect has no valid target");
        }
        Check(!game.NeedsTarget, "effect resolver stalled");
    }

    static string Signature(CardDefinition card)
    {
        var kinds = new Dictionary<EffectKind, string> { {EffectKind.Damage,"d"}, {EffectKind.Heal,"h"}, {EffectKind.ModifyAttack,"a"},
            {EffectKind.IncreaseMaxHP,"m"}, {EffectKind.DrawCard,"c"}, {EffectKind.SelfDamage,"s"}, {EffectKind.ChainDamage,"x"},
            {EffectKind.RandomDamage,"r"}, {EffectKind.DiscardCard,"b"} };
        var targets = new Dictionary<EffectTarget, string> { {EffectTarget.Self,"S"}, {EffectTarget.Enemy,"E"}, {EffectTarget.Ally,"A"},
            {EffectTarget.AllAllies,"F"}, {EffectTarget.AllEnemies,"O"}, {EffectTarget.None,"N"} };
        return string.Join(",", card.EffectList.Select(e => kinds[e.Kind] + targets[e.Target] + e.Amount));
    }
    static void DataMatrix()
    {
        string[] rank = { "2","3","4","5","6","7","8","9","10","A","J","Q","K" };
        string[] stats = {
            "2/2 3/2 3/3 4/3 4/4 5/4 5/5 6/5 7/5 6/7 7/6 7/8 9/9",
            "3/1 3/2 4/2 4/3 5/3 5/4 6/4 7/4 8/5 7/7 8/6 8/8 10/9",
            "1/4 2/4 2/5 3/5 3/6 4/6 4/7 5/7 5/8 5/10 6/9 6/11 8/12",
            "1/3 2/3 2/4 3/4 3/5 4/5 4/6 5/6 5/7 5/9 6/8 6/10 8/11" };
        string[] effects = {
            "dE1 sS1,dE2 rE2 dE2 sS1,dE4 xE2 rE4 dE4,aS1 dE5 aF2,dE2 sS2,dE7 xE4 dE7,aS2",
            "aE-1 dE1,aE-1 rE2 aE-2 dE2,aE-1 bN1 aE-3 dE3,aE-2 bN1,dE3 aO-2,bN1 aE-4,dE4 bN2,aO-1 dE6,aE-4",
            "hA2 mS2 dE2,hS2 mA3 hF2 dE3,hS3 mA4 hF3 dE5,hS5 mF3,hF3 dE5,hS5 mF4,hF4 dE7,hS7",
            "aA1 mA2 cN1 aA2 aA1,mA2 cN2 aF1 aA3,mA3 cN2,aA2 cN2,mF2 aA4,cN1 aF2,mF2 aF3,mF3" };
        for (int suit = 0; suit < 4; suit++)
        {
            var numbers = stats[suit].Split(' '); var signatures = effects[suit].Split(' ');
            for (int i = 0; i < rank.Length; i++)
            {
                var card = CardDatabase.Get(((Suit)suit) + "_" + rank[i]);
                Equal(numbers[i], card.Attack + "/" + card.MaxHP); Equal(signatures[i], Signature(card));
                Equal(CardType.Normal, card.CardType); Equal((Suit)suit, card.Suit); Check(card.Description.Length > 0);
            }
        }
        string[] unknowns = { "J", "Q", "K" }, unknownStats = { "11/10", "9/14", "14/14" }, unknownEffects = { "dE7,aS3", "aF3,mF4", "dE10,aO-2" };
        for (int i = 0; i < 3; i++) { var c = CardDatabase.Get("Unknown_" + unknowns[i]); Equal(unknownStats[i], c.Attack + "/" + c.MaxHP); Equal(unknownEffects[i], Signature(c)); }
    }

    public static int Main()
    {
        Test("52 exact stat/effect rows and 3 UNKNOWN rows", DataMatrix);
        Test("54-card deck, distinct jokers, 57 definitions", () => {
            Equal(54, CardDatabase.Deck.Count); Equal(52, CardDatabase.Deck.Count(c => c.CardType == CardType.Normal));
            Equal(2, CardDatabase.Deck.Count(c => c.CardType == CardType.Joker)); Equal(57, CardDatabase.All.Count);
            Equal(54, CardDatabase.Deck.Select(c => c.CardID).Distinct().Count());
        });
        Test("independent decks and starting hand + turn draw", () => {
            var g = new BattleEngine(1); Equal(6, g.State.Players[0].Hand.Count); Equal(5, g.State.Players[1].Hand.Count);
            Equal(48, g.State.Players[0].DeckCount); Equal(49, g.State.Players[1].DeckCount); Equal(30, g.State.Players[0].HP);
            Equal(108, g.State.Players.SelectMany(p => p.Hand.Concat(p.DeckCards)).Select(c => c.InstanceID).Distinct().Count());
        });
        Test("same seed reproducible, different seed shuffles", () => {
            string Deck(int seed) => string.Join(",", new BattleEngine(seed).State.Players[0].DeckCards.Select(c => c.Definition.CardID));
            Equal(Deck(1), Deck(1)); Check(Deck(1) != Deck(2));
        });
        Test("runtime instances do not mutate definitions or other cards", () => {
            var g = Fresh(); var a = Unit(g,0,"Heart_3"); var b = Unit(g,1,"Heart_3"); a.Attack = 99; a.CurrentHP = 1;
            Equal(2, b.Attack); Equal(4,b.CurrentHP); Equal(2,CardDatabase.Get("Heart_3").Attack);
        });
        Test("turn ownership and turn-start draw", () => {
            var g = Fresh(); Check(!g.EndTurn(1)); Check(g.EndTurn(0)); Equal(1,g.State.ActivePlayer); Equal(2,g.State.TurnNumber); Equal(1,g.State.Players[1].Hand.Count);
        });
        Test("multiple affordable summons share mana rather than per-turn count", () => {
            var g = Fresh(); var a = Hand(g,0,"Diamond_2"); var b=Hand(g,0,"Diamond_3");
            Check(g.Summon(0,a.InstanceID,0)); Equal(a,g.State.Players[0].Board[0]); Check(g.Summon(0,b.InstanceID,1)); Equal(0,g.State.Players[0].Hand.Count); Equal(8,g.State.Players[0].CurrentMana);
        });
        Test("cannot summon on enemy turn or out of range/full board", () => {
            var g = Fresh(); var c=Hand(g,0,"Diamond_2"); Check(!g.Summon(1,c.InstanceID,0)); Check(!g.Summon(0,c.InstanceID,5));
            for(int i=0;i<5;i++) Unit(g,0,"Heart_K",i); Check(!g.Summon(0,c.InstanceID,0)); Equal(1,g.State.Players[0].Hand.Count);
        });
        Test("custom field capacity", () => { var g = Fresh(null,new BattleConfig { FieldSlots=3 }); Equal(3,g.State.Players[0].Board.Count); });
        Test("summoning sickness, next own turn, once per turn", () => {
            var g=Fresh(); var c=Hand(g,0,"Diamond_2"); Check(g.Summon(0,c.InstanceID,0)); Check(!g.Attack(0,c.InstanceID));
            Check(g.EndTurn(0)); Check(!g.Attack(1,c.InstanceID)); Check(g.EndTurn(1)); Check(g.Attack(0,c.InstanceID)); Check(!g.Attack(0,c.InstanceID)); Equal(28,g.State.Players[1].HP);
        });
        Test("simultaneous combat kills both", () => {
            var g=Fresh(); var a=Unit(g,0,"Diamond_2"); Unit(g,1,"Diamond_2"); Check(g.Attack(0,a.InstanceID,0));
            Check(g.State.Players[0].Board[0]==null && g.State.Players[1].Board[0]==null);
        });
        Test("enemy field blocks direct attack without consuming attack", () => {
            var g=Fresh(); var a=Unit(g,0,"Diamond_2"); Unit(g,1,"Heart_2"); Check(!g.Attack(0,a.InstanceID)); Check(!a.HasAttacked); Equal(30,g.State.Players[1].HP);
        });
        Test("win prevents all further commands", () => {
            var g=Fresh(); var a=Unit(g,0,"Unknown_K"); g.State.Players[1].HP=1; Check(g.Attack(0,a.InstanceID)); Equal(0,g.State.Winner);
            Check(!g.EndTurn(0)); Check(!g.Attack(0,a.InstanceID)); var c=Hand(g,0,"Heart_3"); Check(!g.Summon(0,c.InstanceID,1));
        });
        Test("full hand burns exactly the drawn card", () => {
            var g=Fresh(); for(int i=0;i<10;i++) Hand(g,0,"Diamond_2"); int before=g.State.Players[0].DeckCount;
            g.Draw(0,1); Equal(10,g.State.Players[0].Hand.Count); Equal(before-1,g.State.Players[0].DeckCount); Equal(1,g.State.Players[0].DiscardCount);
        });
        Test("empty deck safely skips draw without fatigue", () => {
            var g=Fresh(); g.State.Players[0].DeckCards.Clear(); g.Draw(0,3); Equal(0,g.State.Players[0].Hand.Count); Equal(30,g.State.Players[0].HP);
        });
        Test("manual target gates turn and rejects wrong owner/slot", () => {
            var g=Fresh(); var enemy=Unit(g,1,"Heart_K"); var c=Hand(g,0,"Diamond_5"); Check(g.Summon(0,c.InstanceID,0)); Check(g.NeedsTarget);
            Check(!g.EndTurn(0)); Check(!g.SelectEffectTarget(1,1,0)); Check(!g.SelectEffectTarget(0,0,0)); Check(!g.SelectEffectTarget(0,1,8));
            Check(g.SelectEffectTarget(0,1,0)); Equal(10,enemy.CurrentHP); Check(!g.NeedsTarget);
        });
        Test("no enemy target skips damage but keeps later self buff", () => {
            var g=Fresh(); var c=Hand(g,0,"Diamond_9"); Check(g.Summon(0,c.InstanceID,0)); Equal(7,c.Attack); Check(!g.NeedsTarget);
        });
        Test("damage + debuff uses same target", () => {
            var g=Fresh(); var a=Unit(g,1,"Heart_K"); var b=Unit(g,1,"Heart_K",1); var c=Hand(g,0,"Spade_9");
            Check(g.Summon(0,c.InstanceID,0)); Check(g.SelectEffectTarget(0,1,0)); Equal(9,a.CurrentHP); Equal(6,a.Attack); Equal(8,b.Attack); Check(!g.NeedsTarget);
        });
        Test("destroyed target does not redirect follow-up debuff", () => {
            var g=Fresh(); Unit(g,1,"Spade_2"); var survivor=Unit(g,1,"Heart_K",1); var c=Hand(g,0,"Spade_K");
            Check(g.Summon(0,c.InstanceID,0)); Check(g.SelectEffectTarget(0,1,0)); Equal(8,survivor.Attack); Check(!g.NeedsTarget);
        });
        Test("attack reduction clamps at zero", () => {
            var g=Fresh(); var a=Unit(g,1,"Heart_2"); var c=Hand(g,0,"Spade_8"); Check(g.Summon(0,c.InstanceID,0)); FinishEffects(g); Equal(0,a.Attack);
        });
        Test("healing clamps at max HP", () => {
            var g=Fresh(); var a=Unit(g,0,"Heart_K"); a.CurrentHP=11; var c=Hand(g,0,"Heart_2"); Check(g.Summon(0,c.InstanceID,1)); Check(g.SelectEffectTarget(0,0,0)); Equal(12,a.CurrentHP);
        });
        Test("max HP raises current by same amount and same ally keeps buffs", () => {
            var g=Fresh(); var a=Unit(g,0,"Heart_K"); a.CurrentHP=3; var c=Hand(g,0,"Club_9");
            Check(g.Summon(0,c.InstanceID,1)); Check(g.SelectEffectTarget(0,0,0)); Equal(11,a.Attack); Equal(15,a.MaxHP); Equal(6,a.CurrentHP); Check(!g.NeedsTarget);
        });
        Test("all-allies includes source; HP then healing preserves order", () => {
            var g=Fresh(); var a=Unit(g,0,"Heart_K"); a.CurrentHP=4; var c=Hand(g,0,"Heart_A"); Check(g.Summon(0,c.InstanceID,1));
            Equal(15,a.MaxHP); Equal(10,a.CurrentHP); Equal(13,c.CurrentHP); Equal(13,c.MaxHP);
        });
        Test("self damage precedes selected enemy damage", () => {
            var g=Fresh(); var a=Unit(g,1,"Heart_K"); var c=Hand(g,0,"Diamond_6"); Check(g.Summon(0,c.InstanceID,0)); Equal(3,c.CurrentHP);
            FinishEffects(g); Equal(8,a.CurrentHP);
        });
        Test("self destruction cancels follow-up damage", () => {
            var g=Fresh(); var enemy=Unit(g,1,"Heart_K"); var def=new CardDefinition("test","test",Suit.None,Rank.Two,1,1,CardType.Normal,"",
                new CardEffect(EffectKind.SelfDamage,2,EffectTarget.Self),new CardEffect(EffectKind.Damage,99,EffectTarget.Enemy));
            var c=g.NewCard(def,0); g.State.Players[0].HandCards.Add(c); Check(g.Summon(0,c.InstanceID,0)); Equal(12,enemy.CurrentHP); Check(!g.NeedsTarget); Check(g.State.Players[0].Board[0]==null);
        });
        Test("chain hits two different targets, cannot double hit", () => {
            var g=Fresh(); var a=Unit(g,1,"Heart_K"); var b=Unit(g,1,"Heart_K",1); var c=Hand(g,0,"Diamond_7"); Check(g.Summon(0,c.InstanceID,0));
            Check(g.SelectEffectTarget(0,1,0)); Check(g.NeedsTarget); Check(!g.SelectEffectTarget(0,1,0)); Check(g.SelectEffectTarget(0,1,1)); Equal(10,a.CurrentHP); Equal(10,b.CurrentHP); Check(!g.NeedsTarget);
        });
        Test("chain with one enemy only damages once", () => {
            var g=Fresh(); var a=Unit(g,1,"Heart_K"); var c=Hand(g,0,"Diamond_Q"); Check(g.Summon(0,c.InstanceID,0)); FinishEffects(g); Equal(8,a.CurrentHP);
        });
        Test("random damage chooses living valid target", () => {
            var dice=new Dice {Value=1}; var g=Fresh(dice); var a=Unit(g,1,"Heart_K"); var b=Unit(g,1,"Heart_K",4); var c=Hand(g,0,"Diamond_8");
            Check(g.Summon(0,c.InstanceID,0)); Equal(12,a.CurrentHP); Equal(8,b.CurrentHP); Check(!g.NeedsTarget);
        });
        Test("draw and discard effects handle empty/full hands", () => {
            var g=Fresh(); var c=Hand(g,0,"Club_7"); Check(g.Summon(0,c.InstanceID,0)); Equal(2,g.State.Players[0].Hand.Count);
            g=Fresh(); Hand(g,1,"Heart_K"); c=Hand(g,0,"Spade_Q"); Check(g.Summon(0,c.InstanceID,0)); Equal(0,g.State.Players[1].Hand.Count); Check(!g.NeedsTarget);
        });
        Test("A + each face previews and combines across suits", () => {
            foreach(string face in new[]{"J","Q","K"}) {
                var g=Fresh(); var a=Hand(g,0,"Heart_A"); var f=Hand(g,0,"Spade_"+face); Equal("Unknown_"+face,g.CombinationPreview(0,a.InstanceID,f.InstanceID).CardID);
                Check(g.Combine(0,a.InstanceID,f.InstanceID)); Equal(1,g.State.Players[0].Hand.Count); Equal(CardType.Unknown,g.State.Players[0].Hand[0].Definition.CardType); Equal(0,g.State.SummonsThisTurn);
            }
        });
        Test("field materials cannot combine; missing/cancelled selection is harmless", () => {
            var g=Fresh(); var a=Unit(g,0,"Heart_A"); var f=Hand(g,0,"Spade_K"); Check(!g.Combine(0,a.InstanceID,f.InstanceID));
            Check(g.CombinationPreview(0,-1,f.InstanceID)==null); Equal(1,g.State.Players[0].Hand.Count); Equal(0,g.State.Players[0].UnknownCreated);
        });
        Test("UNKNOWN 3 per player persists after cards disappear", () => {
            var g=Fresh(); for(int i=0;i<4;i++) { var a=Hand(g,0,"Heart_A"); var f=Hand(g,0,"Diamond_K"); Equal(i<3,g.Combine(0,a.InstanceID,f.InstanceID)); }
            Equal(3,g.State.Players[0].UnknownCreated); g.State.Players[0].HandCards.Clear(); Equal(3,g.State.Players[0].UnknownCreated); Equal(0,g.State.Players[1].UnknownCreated);
        });
        Test("UNKNOWN summon costs mana, combination is free", () => {
            var g=Fresh(); var c=Hand(g,0,"Unknown_Q"); Check(g.Summon(0,c.InstanceID,0)); Equal(12,c.Attack); Equal(18,c.MaxHP);
            var a=Hand(g,0,"Heart_A"); var f=Hand(g,0,"Spade_J"); Check(g.Combine(0,a.InstanceID,f.InstanceID)); Equal(1,g.State.SummonsThisTurn);
        });
        Test("joker is not a summon", () => { var g=Fresh(); var j=Hand(g,0,"Joker_1"); Check(!g.Summon(0,j.InstanceID,0)); Equal(1,g.State.Players[0].Hand.Count); });
        Test("joker success rounds up, increases current by max delta", () => {
            var g=Fresh(new Dice()); var a=Unit(g,0,"Spade_3"); a.CurrentHP=1; var j=Hand(g,0,"Joker_1"); Check(g.Gamble(0,j.InstanceID,a.InstanceID));
            Equal(5,a.Attack); Equal(3,a.MaxHP); Equal(2,a.CurrentHP); Equal(0,g.State.Players[0].Hand.Count); Equal(0,g.State.SummonsThisTurn);
        });
        Test("joker failure destroys target and consumes joker", () => {
            var g=Fresh(new Dice {Value=1}); var a=Unit(g,0,"Unknown_K"); var j=Hand(g,0,"Joker_1"); Check(g.Gamble(0,j.InstanceID,a.InstanceID));
            Check(g.State.Players[0].Board[0]==null); Equal(0,g.State.Players[0].Hand.Count); Equal(2,g.State.Players[0].DiscardCount);
        });
        Test("joker once per unit, invalid target never consumes it", () => {
            var g=Fresh(new Dice()); var a=Unit(g,0,"Unknown_K"); var j=Hand(g,0,"Joker_1"); Check(!g.Gamble(0,j.InstanceID,-1));
            var enemy=Unit(g,1,"Heart_K"); Check(!g.Gamble(0,j.InstanceID,enemy.InstanceID)); Check(g.Gamble(0,j.InstanceID,a.InstanceID));
            var j2=Hand(g,0,"Joker_2"); Check(!g.Gamble(0,j2.InstanceID,a.InstanceID)); Equal(1,g.State.Players[0].Hand.Count); Equal(21,a.Attack);
        });
        Test("every summon definition resolves without stale targets", () => {
            foreach(var def in CardDatabase.All.Where(c=>c.CardType!=CardType.Joker)) {
                var g=Fresh(); Unit(g,0,"Heart_K",1); Unit(g,1,"Heart_K"); Unit(g,1,"Heart_K",1); Hand(g,1,"Spade_3");
                var c=g.NewCard(def,0); g.State.Players[0].HandCards.Add(c); Check(g.Summon(0,c.InstanceID,0)); FinishEffects(g);
                foreach(var p in g.State.Players) foreach(var u in p.Board.Where(u=>u!=null)) Check(u.CurrentHP>0 && u.CurrentHP<=u.MaxHP && u.Attack>=0,def.CardID);
            }
        });
        Test("seeded complete matches maintain invariants through random actions", () => {
            for(int match=0;match<12;match++) {
                var g=new BattleEngine(match); var choices=new Random(match+500);
                for(int turn=0;turn<160&&!g.State.IsOver;turn++) {
                    int actor=g.State.ActivePlayer; var player=g.State.Players[actor];
                    var ace=player.Hand.FirstOrDefault(c=>c.Definition.CardType==CardType.Normal&&c.Definition.Rank==Rank.Ace);
                    var face=player.Hand.FirstOrDefault(c=>c.Definition.CardType==CardType.Normal&&c.Definition.Rank>=Rank.Jack&&c.Definition.Rank<=Rank.King);
                    if(ace!=null&&face!=null) g.Combine(actor,ace.InstanceID,face.InstanceID);
                    int slot=Array.FindIndex(player.Slots,c=>c==null);
                    var summon=player.Hand.FirstOrDefault(c=>c.Definition.CardType!=CardType.Joker&&c.Definition.ManaCost<=player.CurrentMana);
                    if(slot>=0&&summon!=null) { Check(g.Summon(actor,summon.InstanceID,slot)); FinishEffects(g); }
                    var joker=player.Hand.FirstOrDefault(c=>c.Definition.CardType==CardType.Joker); var target=player.Board.FirstOrDefault(c=>c!=null&&!c.JokerUsed);
                    if(joker!=null&&target!=null&&player.CurrentMana>=2) Check(g.Gamble(actor,joker.InstanceID,target.InstanceID));
                    foreach(var unit in player.Board.Where(c=>c!=null).ToArray()) if(g.CanAttack(actor,unit)) {
                        var enemies=Enumerable.Range(0,g.Config.FieldSlots).Where(i=>g.State.Players[1-actor].Board[i]!=null).ToArray();
                        Check(g.Attack(actor,unit.InstanceID,enemies.Length==0?-1:enemies[choices.Next(enemies.Length)]));
                    }
                    var physical=new List<CardInstance>();
                    foreach(var p in g.State.Players) {
                        Check(p.Hand.Count<=10); Check(p.HP>=0); physical.AddRange(p.Hand); physical.AddRange(p.DeckCards); physical.AddRange(p.DiscardCards);
                        foreach(var u in p.Board.Where(c=>c!=null)) { Check(u.CurrentHP>0&&u.CurrentHP<=u.MaxHP&&u.Attack>=0); physical.Add(u); }
                    }
                    Equal(physical.Count,physical.Select(c=>c.InstanceID).Distinct().Count());
                    if(!g.State.IsOver) Check(g.EndTurn(actor));
                }
            }
        });
        Test("mana costs match all 57 definitions", () => {
            string[] ranks={"2","3","4","5","6","7","8","9","10","J","Q","K","A"};
            int[] costs={1,1,2,2,3,3,4,4,5,6,7,8,7};
            foreach(Suit suit in new[]{Suit.Diamond,Suit.Spade,Suit.Heart,Suit.Club})
                for(int i=0;i<ranks.Length;i++) Equal(costs[i],CardDatabase.Get(suit+"_"+ranks[i]).ManaCost);
            Equal(2,CardDatabase.Get("Joker_1").ManaCost); Equal(2,CardDatabase.Get("Joker_2").ManaCost);
            Equal(7,CardDatabase.Get("Unknown_J").ManaCost); Equal(8,CardDatabase.Get("Unknown_Q").ManaCost); Equal(9,CardDatabase.Get("Unknown_K").ManaCost);
        });
        Test("each player's first turn starts at 3, second at 4", () => {
            var g=new BattleEngine(7); Equal(3,g.State.Players[0].MaxMana); Equal(3,g.State.Players[0].CurrentMana);
            Equal(1,g.State.Players[0].TurnsStarted); Equal(0,g.State.Players[1].TurnsStarted);
            Check(g.EndTurn(0)); Equal(3,g.State.Players[1].CurrentMana); Equal(3,g.State.Players[1].MaxMana);
            Check(g.EndTurn(1)); Equal(4,g.State.Players[0].CurrentMana); Equal(4,g.State.Players[0].MaxMana);
        });
        Test("mana refills without carry-over and opponent mana is untouched", () => {
            var g=Fresh(null,new BattleConfig{StartingHand=0,ShuffleDecks=false}); var c=Hand(g,0,"Diamond_2"); Check(g.Summon(0,c.InstanceID,0));
            Equal(2,g.State.Players[0].CurrentMana); Check(g.EndTurn(0)); Equal(2,g.State.Players[0].CurrentMana);
            Check(g.EndTurn(1)); Equal(4,g.State.Players[0].CurrentMana);
        });
        Test("maximum mana caps at 10 for both players", () => {
            var g=new BattleEngine(8); for(int i=0;i<40;i++) Check(g.EndTurn(g.State.ActivePlayer));
            foreach(var p in g.State.Players) { Equal(10,p.MaxMana); Equal(10,p.CurrentMana); }
        });
        Test("multiple summons consume exact mana, fourth unaffordable summon is atomic", () => {
            var g=Fresh(null,new BattleConfig{StartingHand=0,ShuffleDecks=false});
            for(int slot=0;slot<3;slot++) { var c=Hand(g,0,"Diamond_2"); Check(g.Summon(0,c.InstanceID,slot)); }
            Equal(0,g.State.Players[0].CurrentMana); var fourth=Hand(g,0,"Diamond_2"); Check(!g.Summon(0,fourth.InstanceID,3));
            Equal("마나가 부족합니다",g.State.LastError); Equal(1,g.State.Players[0].Hand.Count); Check(g.State.Players[0].Board[3]==null);
        });
        Test("occupied/full field never charges mana", () => {
            var g=Fresh(); for(int slot=0;slot<5;slot++) Unit(g,0,"Heart_K",slot); var c=Hand(g,0,"Heart_2");
            Check(!g.Summon(0,c.InstanceID,0)); Equal(10,g.State.Players[0].CurrentMana); Equal(1,g.State.Players[0].Hand.Count);
        });
        Test("free combination at zero mana, UNKNOWN pays its summon cost", () => {
            var g=Fresh(); g.State.Players[0].CurrentMana=0; var a=Hand(g,0,"Heart_A");var k=Hand(g,0,"Diamond_K");
            Check(g.Combine(0,a.InstanceID,k.InstanceID)); Equal(0,g.State.Players[0].CurrentMana);var unknown=g.State.Players[0].Hand[0];
            Check(!g.Summon(0,unknown.InstanceID,0)); g.State.Players[0].CurrentMana=9; Check(g.Summon(0,unknown.InstanceID,0)); Equal(0,g.State.Players[0].CurrentMana);
        });
        Test("joker charges 2 on either result; insufficient mana never rolls", () => {
            foreach(int outcome in new[]{0,1}) {
                var dice=new Dice{Value=outcome}; var g=Fresh(dice); var unit=Unit(g,0,"Heart_3"); var joker=Hand(g,0,"Joker_1");
                g.State.Players[0].CurrentMana=1; Check(!g.Gamble(0,joker.InstanceID,unit.InstanceID)); Equal(0,dice.Calls); Equal(1,g.State.Players[0].CurrentMana);
                g.State.Players[0].CurrentMana=3; Check(g.Gamble(0,joker.InstanceID,unit.InstanceID)); Equal(1,g.State.Players[0].CurrentMana); Equal(1,dice.Calls);
                g.DrainEvents(); g.DrainEvents(); Equal(1,dice.Calls);
            }
        });
        Test("invalid or already-used joker target does not charge or reroll", () => {
            var dice=new Dice(); var g=Fresh(dice); var u=Unit(g,0,"Heart_3"); var j=Hand(g,0,"Joker_1");
            Check(!g.Gamble(0,j.InstanceID,-1)); Equal(10,g.State.Players[0].CurrentMana); Check(g.Gamble(0,j.InstanceID,u.InstanceID));
            var j2=Hand(g,0,"Joker_2"); Check(!g.Gamble(0,j2.InstanceID,u.InstanceID)); Equal(8,g.State.Players[0].CurrentMana); Equal(1,dice.Calls);
        });
        Test("clock follows real elapsed time across uneven frames", () => {
            var c=new TurnClock(); c.Begin(60,100);c.Tick(100.1,false);c.Tick(100.6,false);c.Tick(117.25,false); Equal(42.75,c.Remaining);
            var d=new TurnClock();d.Begin(60,100);d.Tick(117.25,false);Equal(c.Remaining,d.Remaining);
        });
        Test("clock pauses, resumes without charging pause duration, and stops", () => {
            var c=new TurnClock();c.Begin(60,0);c.Tick(5,false);Equal(55.0,c.Remaining);c.Tick(6,true);c.Tick(100,true);Equal(55.0,c.Remaining);
            c.Tick(101,false);c.Tick(106,false);Equal(50.0,c.Remaining);c.Stop();c.Tick(999,false);Equal(50.0,c.Remaining);
        });
        Test("clock clamps at zero and resets for the next turn", () => {
            var c=new TurnClock();c.Begin(60,0);c.Tick(100,false);Equal(0.0,c.Remaining);Check(c.Expired&&!c.Running);
            c.Begin(12,100);Equal(12.0,c.Remaining);Check(c.Running&&!c.Expired);
        });
        Test("timeout blocks all new actions until one explicit turn transition", () => {
            var g=Fresh();var unit=Unit(g,0,"Heart_3");var card=Hand(g,0,"Diamond_2");
            Check(g.ExpireTurn(0));Check(!g.ExpireTurn(0));Check(!g.Attack(0,unit.InstanceID));Check(!g.Summon(0,card.InstanceID,1));
            Check(!g.EndTurn(0));Equal(1,g.State.TurnNumber);Check(g.FinishExpiredTurn(0));Check(!g.FinishExpiredTurn(0));Equal(2,g.State.TurnNumber);
        });
        Test("timeout finishes committed chain targets once then waits for presentation", () => {
            var g=Fresh();var a=Unit(g,1,"Heart_K");var b=Unit(g,1,"Heart_K",1);var c=Hand(g,0,"Diamond_7");
            Check(g.Summon(0,c.InstanceID,0));Check(g.NeedsTarget);g.DrainEvents();Check(g.ExpireTurn(0));Check(!g.NeedsTarget);
            Equal(10,a.CurrentHP);Equal(10,b.CurrentHP);Equal(1,g.State.TurnNumber);
            var results=g.DrainEvents();Equal(2,results.Count(e=>e.Kind==BattleEventKind.Damage));Equal(1,results.Count(e=>e.Kind==BattleEventKind.TimeOut));
            Check(!g.SelectEffectTarget(0,1,0));Check(g.FinishExpiredTurn(0));Equal(2,g.State.TurnNumber);
        });
        Test("manual end duplicate with stale actor cannot skip a turn", () => {
            var g=Fresh();Check(g.EndTurn(0));Check(!g.EndTurn(0));Equal(2,g.State.TurnNumber);
        });
        Test("effect events carry actual clamped ATK and heal changes", () => {
            var g=Fresh();var enemy=Unit(g,1,"Heart_2");var c=Hand(g,0,"Spade_8");g.DrainEvents();Check(g.Summon(0,c.InstanceID,0));FinishEffects(g);
            Equal(-1,g.DrainEvents().Single(e=>e.Kind==BattleEventKind.AttackChanged).Amount);Equal(0,enemy.Attack);
            g=Fresh();var ally=Unit(g,0,"Heart_K");ally.CurrentHP=11;c=Hand(g,0,"Heart_2");g.DrainEvents();Check(g.Summon(0,c.InstanceID,1));Check(g.SelectEffectTarget(0,0,0));
            Equal(1,g.DrainEvents().Single(e=>e.Kind==BattleEventKind.Heal).Amount);
        });
        Test("combat events capture actual damage before simultaneous destruction", () => {
            var g=Fresh();var a=Unit(g,0,"Diamond_2");var b=Unit(g,1,"Diamond_2");a.CurrentHP=b.CurrentHP=1;g.DrainEvents();
            Check(g.Attack(0,a.InstanceID,0));var results=g.DrainEvents();var attack=results.Single(e=>e.Kind==BattleEventKind.Attack);
            Equal(1,attack.Amount);Equal(1,attack.Secondary);Equal(2,results.Count(e=>e.Kind==BattleEventKind.Destroy));Equal(0,g.DrainEvents().Count);
        });
        Test("direct attack caps floating damage at remaining hero HP and emits win", () => {
            var g=Fresh();var a=Unit(g,0,"Unknown_K");g.State.Players[1].HP=2;g.DrainEvents();Check(g.Attack(0,a.InstanceID));
            var results=g.DrainEvents();Equal(2,results.Single(e=>e.Kind==BattleEventKind.Attack).Amount);Equal(1,results.Count(e=>e.Kind==BattleEventKind.GameOver));Check(!g.ExpireTurn(0));
        });
        Test("summon and mana events are emitted once and drains cannot replay results", () => {
            var g=Fresh();var c=Hand(g,0,"Heart_3");g.DrainEvents();Check(g.Summon(0,c.InstanceID,0));
            var results=g.DrainEvents();Equal(1,results.Count(e=>e.Kind==BattleEventKind.Summon));Equal(1,results.Count(e=>e.Kind==BattleEventKind.ManaSpent));
            Equal(2,results.Single(e=>e.Kind==BattleEventKind.MaxHPChanged).Amount);Equal(0,g.DrainEvents().Count);
        });
        Test("network projections never contain opponent hand definitions or deck order", () => {
            var g = new BattleEngine(91); var events = g.DrainEvents();
            for (int viewer = 0; viewer < 2; viewer++) {
                var v = BattleSnapshot.For(g, viewer, 0, 60, false, events);
                Equal(0, v.players[1-viewer].hand.Length); Equal(g.State.Players[viewer].Hand.Count, v.players[viewer].hand.Length);
                Check(v.players.All(p => p.board.All(c => !c.occupied)));
                foreach (var e in v.events.Where(e => e.owner != viewer && e.kind == BattleEventKind.Draw)) { Equal(-1,e.target); Check(e.definition == null); }
                var mirror = BattleEngine.CreateView(v); Equal(g.State.Players[1-viewer].Hand.Count, mirror.State.Players[1-viewer].Hand.Count);
                Check(mirror.State.Players[1-viewer].Hand.All(c => c == null)); Check(!mirror.EndTurn(viewer));
            }
        });
        Test("UNKNOWN projection hides material IDs and resulting type from opponent", () => {
            var g = Fresh(); var a = Hand(g,0,"Diamond_A"); var f = Hand(g,0,"Heart_Q"); g.DrainEvents();
            Check(g.Combine(0,a.InstanceID,f.InstanceID)); var events = g.DrainEvents();
            var own = BattleSnapshot.For(g,0,1,60,false,events); var other = BattleSnapshot.For(g,1,1,60,false,events);
            Equal("Unknown_Q", own.events[0].definition); Equal(0,other.events.Length);
            Equal(1,own.players[0].unknownCreated); Equal(0,other.players[0].unknownCreated);
            Equal(0,other.players[0].hand.Length); Check(other.log.All(l => !l.Contains("UNKNOWN")));
            Check(own.log.Any(l=>l.Contains("UNKNOWN 조합"))); Equal(1,other.players[0].handCount);
        });
        Test("private discard only reveals discarded identity to its owner", () => {
            var e = new BattleEvent(BattleEventKind.Discard,1,10,22,definition:CardDatabase.Get("Heart_Q"));
            var hidden = EventViewData.For(e,0); var own = EventViewData.For(e,1);
            Equal(-1,hidden.target); Check(hidden.definition == null); Equal(22,own.target);
        });
        Test("authority rejects wrong actor, duplicate sequence, stale revision and stale turn", () => {
            var a = new BattleAuthority(20); var turn = a.Engine.State.TurnNumber;
            Check(!a.Execute(1,new BattleIntent {sequence=1,revision=0,turn=turn,action=BattleAction.EndTurn}));
            Check(a.Execute(0,new BattleIntent {sequence=1,revision=0,turn=turn,action=BattleAction.EndTurn}));
            Equal(1,a.Revision); Equal(turn+1,a.Engine.State.TurnNumber);
            Check(!a.Execute(0,new BattleIntent {sequence=1,revision=0,turn=turn,action=BattleAction.EndTurn}));
            Check(!a.Execute(1,new BattleIntent {sequence=2,revision=0,turn=turn+1,action=BattleAction.EndTurn}));
            Check(!a.Execute(1,new BattleIntent {sequence=3,revision=1,turn=turn,action=BattleAction.EndTurn}));
            Check(a.Execute(1,new BattleIntent {sequence=4,revision=1,turn=turn+1,action=BattleAction.EndTurn}));
        });
        Test("Host resolves attack target IDs for either player and rejects stale or friendly IDs", () => {
            for(int owner=0;owner<2;owner++) {
                var authority=new BattleAuthority(31); var g=authority.Engine; g.State.ActivePlayer=owner;
                foreach(var p in g.State.Players) { p.HandCards.Clear(); for(int slot=0;slot<p.Slots.Length;slot++)p.Slots[slot]=null; }
                var unit=Unit(g,owner,"Heart_K"); var target=Unit(g,1-owner,"Heart_K",3);
                Check(!authority.Execute(owner,new BattleIntent{sequence=1,revision=0,turn=g.State.TurnNumber,action=BattleAction.Attack,first=unit.InstanceID,second=unit.InstanceID}));
                Check(!unit.HasAttacked); Equal(12,target.CurrentHP);
                Check(authority.Execute(owner,new BattleIntent{sequence=2,revision=0,turn=g.State.TurnNumber,action=BattleAction.Attack,first=unit.InstanceID,second=target.InstanceID}));
                Equal(4,unit.CurrentHP); Equal(4,target.CurrentHP); Check(unit.HasAttacked);
                Check(!authority.Execute(owner,new BattleIntent{sequence=3,revision=1,turn=g.State.TurnNumber,action=BattleAction.Attack,first=unit.InstanceID,second=target.InstanceID}));
                Equal(4,target.CurrentHP);
            }
        });
        Test("Host blocks direct attacks with defenders and accepts ID -1 only on an empty field", () => {
            var authority=new BattleAuthority(32); var g=authority.Engine;
            foreach(var p in g.State.Players)for(int i=0;i<p.Slots.Length;i++)p.Slots[i]=null;
            var unit=Unit(g,0,"Heart_K"); var defender=Unit(g,1,"Heart_K");
            Check(!authority.Execute(0,new BattleIntent{sequence=1,revision=0,turn=g.State.TurnNumber,action=BattleAction.Attack,first=unit.InstanceID,second=-1}));
            Check(!unit.HasAttacked); Equal(30,g.State.Players[1].HP);
            g.State.Players[1].Slots[0]=null;
            Check(!authority.Execute(0,new BattleIntent{sequence=2,revision=0,turn=g.State.TurnNumber,action=BattleAction.Attack,first=unit.InstanceID,second=defender.InstanceID}));
            Check(!unit.HasAttacked);
            Check(authority.Execute(0,new BattleIntent{sequence=3,revision=0,turn=g.State.TurnNumber,action=BattleAction.Attack,first=unit.InstanceID,second=-1}));
            Equal(22,g.State.Players[1].HP);
        });
        Test("authority rejects other player's hand ID and forged attack ID atomically", () => {
            var a = new BattleAuthority(20); var s = a.Engine.State; var mana = s.Players[0].CurrentMana;
            Check(!a.Execute(0,new BattleIntent {sequence=1,revision=0,turn=s.TurnNumber,action=BattleAction.Summon,first=s.Players[1].Hand[0].InstanceID,second=0}));
            Check(!a.Execute(0,new BattleIntent {sequence=2,revision=0,turn=s.TurnNumber,action=BattleAction.Attack,first=int.MaxValue,second=-1}));
            Equal(mana,s.Players[0].CurrentMana); Equal(0,a.Revision);
        });
        Test("public board snapshots preserve resolved stats, attack readiness and target prompt", () => {
            var g = Fresh(); var unit = Unit(g,1,"Club_K"); unit.Attack=2; unit.CurrentHP=3; unit.JokerUsed=true;
            var card = Hand(g,0,"Diamond_2"); Check(g.Summon(0,card.InstanceID,0));
            var v = BattleSnapshot.For(g,1,4,80,false,g.DrainEvents()); var view = BattleEngine.CreateView(v);
            Equal(2,view.State.Players[1].Board[0].Attack); Equal(3,view.State.Players[1].Board[0].CurrentHP);
            Check(view.State.Players[1].Board[0].JokerUsed); Check(view.NeedsTarget); Check(view.IsValidEffectTarget(1,0));
        });
        Test("third UNKNOWN ends immediately in hand with one terminal event for either player", () => {
            for (int owner=0; owner<2; owner++) {
                var g=Fresh(); if(owner==1) Check(g.EndTurn(0)); g.DrainEvents();
                foreach(var face in new[]{"J","Q","K"}) {
                    int before=g.State.Players[owner].UnknownCreated;
                    var a=Hand(g,owner,"Heart_A"); var f=Hand(g,owner,"Spade_"+face);
                    int mana=g.State.Players[owner].CurrentMana; Check(g.Combine(owner,a.InstanceID,f.InstanceID));
                    Equal(mana,g.State.Players[owner].CurrentMana); Equal(before+1,g.State.Players[owner].UnknownCreated);
                    Equal(before==2,g.State.IsOver);
                    if(before<2) g.State.Players[owner].HandCards.Clear();
                }
                Equal(owner,g.State.Winner); Equal(VictoryReason.UnknownComplete,g.State.VictoryReason);
                Equal(3,g.State.Players[owner].UnknownCreated); Check(g.State.Players[owner].Board.All(c=>c==null));
                var events=g.DrainEvents(); Equal(1,events.Count(e=>e.Kind==BattleEventKind.GameOver));
                for(int recipient=0;recipient<2;recipient++) {
                    var snapshot=BattleSnapshot.For(g,recipient,3,60,false,events); var view=BattleEngine.CreateView(snapshot);
                    Equal(owner,view.State.Winner); Equal(VictoryReason.UnknownComplete,view.State.VictoryReason);
                    Equal(owner==recipient ? 3 : 0,view.State.Players[owner].UnknownCreated); Equal(0,snapshot.players[1-recipient].hand.Length);
                }
                var count=g.State.Players[owner].Hand.Count; var deck=g.State.Players[owner].DeckCount; int turn=g.State.TurnNumber;
                Check(!g.Summon(owner,-1,0)); Check(!g.Attack(owner,-1)); Check(!g.Combine(owner,-1,-2));
                Check(!g.Gamble(owner,-1,-2)); Check(!g.EndTurn(owner)); Check(!g.ExpireTurn(owner)); Check(!g.FinishExpiredTurn(owner));
                g.Draw(owner,1); Equal(count,g.State.Players[owner].Hand.Count); Equal(deck,g.State.Players[owner].DeckCount);
                Equal(turn,g.State.TurnNumber); Equal(0,g.DrainEvents().Count);
            }
        });
        Test("HP victory has explicit reason and new matches reset both victory paths", () => {
            var g=Fresh(); var unit=Unit(g,0,"Diamond_K"); g.State.Players[1].HP=1; Check(g.Attack(0,unit.InstanceID));
            Equal(VictoryReason.OpponentHPZero,g.State.VictoryReason);
            var fresh=new BattleEngine(21); Check(!fresh.State.IsOver); Equal(-1,fresh.State.Winner); Equal(VictoryReason.None,fresh.State.VictoryReason);
            foreach(var p in fresh.State.Players) { Equal(0,p.UnknownCreated); Equal(30,p.HP); Equal(3,p.CurrentMana); Equal(3,p.MaxMana); Check(p.Board.All(c=>c==null)); }
        });
        Test("empty wire slots are explicit and populated slots retain logical indices for both recipients", () => {
            var g=Fresh(); Unit(g,0,"Diamond_2",4); Unit(g,1,"Club_K",1);
            for(int recipient=0;recipient<2;recipient++) {
                var s=BattleSnapshot.For(g,recipient,1,60,false,g.DrainEvents());
                Check(s.players[0].board[0]!=null && !s.players[0].board[0].occupied);
                var v=BattleEngine.CreateView(s); Check(v.State.Players[0].Board[0]==null);
                Equal("Diamond_2",v.State.Players[0].Board[4].Definition.CardID); Equal("Club_K",v.State.Players[1].Board[1].Definition.CardID);
            }
        });
        Console.WriteLine($"RESULT: {passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}
