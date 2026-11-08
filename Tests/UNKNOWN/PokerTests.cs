using System;
using System.Linq;
using Unknown.Battle;
static partial class BattleRulesTests
{
    static BattleEngine PokerGame(BattleMap map=BattleMap.Normal) {var g=new BattleEngine(4,new BattleConfig{Map=map,StartingHand=0,StartingMana=10,ShuffleDecks=false},new Dice());foreach(var p in g.State.Players)p.HandCards.Clear();g.DrainEvents();return g;}
    static void PokerField(BattleEngine g,int owner,params string[] ids) {for(int i=0;i<ids.Length;i++)Unit(g,owner,ids[i],i);}
    static void PokerChecks()
    {
        string[][] hands={new string[0],new[]{"Heart_5","Spade_5"},new[]{"Heart_5","Spade_5","Club_9","Diamond_9"},new[]{"Heart_5","Spade_5","Club_5"},new[]{"Heart_A","Spade_2","Club_3","Diamond_4","Heart_5"},new[]{"Heart_2","Heart_4","Heart_6","Heart_8","Heart_J"},new[]{"Heart_5","Spade_5","Club_5","Heart_9","Diamond_9"},new[]{"Heart_5","Spade_5","Club_5","Diamond_5"},new[]{"Spade_5","Spade_6","Spade_7","Spade_8","Spade_9"},new[]{"Heart_10","Heart_J","Heart_Q","Heart_K","Heart_A"}};
        for(int i=0;i<hands.Length;i++) {int index=i;Test("Poker highest category and targets: "+(PokerKind)i,()=>{
            var g=PokerGame();PokerField(g,0,hands[index]);g.ReconcilePoker();var p=g.State.Players[0];Equal((PokerKind)index,p.Poker.kind);
            foreach(var c in p.Board.Where(c=>c!=null)){Equal(c.Definition.Attack+PokerSynergy.Attack((PokerKind)index),c.Attack);Equal(c.Definition.MaxHP+PokerSynergy.HP((PokerKind)index),c.MaxHP);}
            var shuffled=PokerSynergy.Evaluate(p.Board.Reverse());Equal(p.Poker.kind,shuffled.kind);Check(p.Poker.members.SequenceEqual(shuffled.members));
            g.DrainEvents();for(int n=0;n<20;n++)g.ReconcilePoker();Equal(0,g.DrainEvents().Count);
        });}
        Test("Ace straights allow low and high only, with exact royal flush recognition",()=>{
            foreach(var suit in new[]{"Heart","Spade","Diamond","Club"}){var g=PokerGame();PokerField(g,0,suit+"_A",suit+"_2",suit+"_3",suit+"_4",suit+"_5");Equal(PokerKind.StraightFlush,PokerSynergy.Evaluate(g.State.Players[0].Board).kind);}
            var h=PokerGame();PokerField(h,0,"Heart_10","Spade_J","Club_Q","Diamond_K","Heart_A");Equal(PokerKind.Straight,PokerSynergy.Evaluate(h.State.Players[0].Board).kind);
            h=PokerGame();PokerField(h,0,"Heart_K","Spade_A","Club_2","Diamond_3","Heart_4");Equal(PokerKind.None,PokerSynergy.Evaluate(h.State.Players[0].Board).kind);
        });
        Test("Hand, UNKNOWN and Joker never participate; all-field two-pair includes UNKNOWN",()=>{
            var g=PokerGame();PokerField(g,0,"Heart_5","Unknown_J","Joker_1");Hand(g,0,"Spade_5");g.ReconcilePoker();Equal(PokerKind.None,g.State.Players[0].Poker.kind);
            g=PokerGame();PokerField(g,0,"Heart_5","Spade_5","Club_9","Diamond_9","Unknown_J");g.ReconcilePoker();var c=g.State.Players[0].Board[4];Equal(2,c.PokerAttack);Equal(1,c.PokerHP);Check(!g.State.Players[0].Poker.members.Contains(c.InstanceID));
        });
        Test("Matching-only bonuses never affect a kicker or UNKNOWN",()=>{
            foreach(var index in new[]{1,3,7}){var g=PokerGame();PokerField(g,0,hands[index].Concat(new[]{"Unknown_J"}).Take(5).ToArray());g.ReconcilePoker();var c=g.State.Players[0].Board.Last(x=>x!=null);Equal(0,c.PokerAttack);Equal(0,c.PokerHP);}
        });
        Test("Pair to triple replacement, member swap and independent opponent hand are idempotent",()=>{
            var g=PokerGame();PokerField(g,0,"Heart_5","Spade_5");PokerField(g,1,"Heart_2","Spade_2");g.ReconcilePoker();Unit(g,0,"Club_5",2);g.ReconcilePoker();Equal(PokerKind.Triple,g.State.Players[0].Poker.kind);Equal(PokerKind.OnePair,g.State.Players[1].Poker.kind);
            var p=g.State.Players[0];p.Slots[0]=null;g.ReconcilePoker();Equal(PokerKind.OnePair,p.Poker.kind);Equal(0,p.Board[1].PokerHP);Equal(p.Board[1].Definition.Attack+2,p.Board[1].Attack);
        });
        Test("Health bonus removal preserves damage and resolves a finite death cascade",()=>{
            var g=PokerGame();PokerField(g,0,"Heart_5","Spade_5","Club_5");g.ReconcilePoker();var p=g.State.Players[0];p.Slots[0].CurrentHP=0;p.Slots[1].CurrentHP=1;p.Slots[2].CurrentHP=1;g.ReconcilePoker();Equal(0,p.Board.Count(c=>c!=null));Equal(PokerKind.None,p.Poker.kind);Equal(3,g.DrainEvents().Count(e=>e.Kind==BattleEventKind.Destroy));
            g=PokerGame();PokerField(g,0,"Heart_5","Spade_5","Club_5");g.ReconcilePoker();p=g.State.Players[0];var survivor=p.Slots[1];survivor.CurrentHP=4;p.Slots[0].CurrentHP=0;g.ReconcilePoker();Equal(2,survivor.CurrentHP);Equal(survivor.Definition.MaxHP,survivor.MaxHP);
        });
        Test("Straight reward occurs once per player, respects eight-card cap, and resets next game",()=>{
            var g=PokerGame();PokerField(g,0,hands[4]);for(int i=0;i<7;i++)Hand(g,0,"Heart_2");int deck=g.State.Players[0].DeckCount;g.ReconcilePoker();Equal(deck-2,g.State.Players[0].DeckCount);Equal(8,g.State.Players[0].Hand.Count);Check(g.State.Players[0].Poker.straightRewardUsed);
            var c=g.State.Players[0].Slots[4];g.State.Players[0].Slots[4]=null;g.ReconcilePoker();g.State.Players[0].Slots[4]=c;g.ReconcilePoker();Equal(deck-2,g.State.Players[0].DeckCount);
            PokerField(g,1,hands[4]);int enemyDeck=g.State.Players[1].DeckCount;g.ReconcilePoker();Equal(enemyDeck-2,g.State.Players[1].DeckCount);Check(!PokerGame().State.Players[0].Poker.straightRewardUsed);
            var empty=PokerGame();empty.State.Players[0].DeckCards.Clear();PokerField(empty,0,hands[4]);empty.ReconcilePoker();Equal(0,empty.State.Players[0].Hand.Count);Check(empty.State.Players[0].Poker.straightRewardUsed);
        });
        Test("Royal and straight flush are exclusive categories without the straight reward",()=>{
            foreach(int index in new[]{8,9}) {var g=PokerGame();int deck=g.State.Players[0].DeckCount;PokerField(g,0,hands[index]);g.ReconcilePoker();Equal(deck,g.State.Players[0].DeckCount);Check(!g.State.Players[0].Poker.straightRewardUsed);}
        });
        Test("Map and Joker layers survive poker loss; existing final-stat 1.5x gamble is preserved",()=>{
            var g=PokerGame(BattleMap.Diamond);var a=Hand(g,0,"Diamond_5");Check(g.Summon(0,a.InstanceID,0));FinishEffects(g);Unit(g,0,"Heart_5",1);g.ReconcilePoker();Equal(1,a.MapAttack);int before=a.Attack;var j=Hand(g,0,"Joker_1");Check(g.Gamble(0,j.InstanceID,a.InstanceID));Equal((int)Math.Ceiling(before*1.5),a.Attack);Check(a.JokerAttack>0);int jokerBonus=a.JokerAttack;
            g.State.Players[0].Slots[1].CurrentHP=0;g.ReconcilePoker();Equal((int)Math.Ceiling(before*1.5)-2,a.Attack);Equal(jokerBonus,a.JokerAttack);Equal(1,a.MapAttack);Equal(0,a.PokerAttack);
        });
        Test("Public snapshots carry both poker states and exact stat causes without hidden hand data",()=>{
            var g=PokerGame();PokerField(g,0,hands[3]);PokerField(g,1,hands[1]);Hand(g,0,"Unknown_J");Hand(g,1,"Joker_1");g.ReconcilePoker();var entries=g.DrainEvents();
            foreach(int recipient in new[]{0,1}) {var s=BattleSnapshot.For(g,recipient,1,0,true,entries);var v=BattleEngine.CreateView(s);Equal(PokerKind.Triple,v.State.Players[0].Poker.kind);Equal(PokerKind.OnePair,v.State.Players[1].Poker.kind);Equal(0,s.players[1-recipient].hand.Length);var c=v.State.Players[0].Board[0];Equal(3,c.PokerAttack);Equal(2,c.PokerHP);Equal(g.State.Players[0].Board[0].Attack,c.Attack);v.ReconcilePoker();Equal(0,v.DrainEvents().Count);}
        });
        Test("Clamped negative core and source totals survive recipient projection",()=>{
            var g=PokerGame();PokerField(g,0,"Heart_5","Spade_5");g.ReconcilePoker();var c=g.State.Players[0].Slots[0];c.Attack=0;c.AbilityAttack=-5;
            g.State.Players[0].Slots[1].CurrentHP=0;g.ReconcilePoker();Equal(0,c.Attack);Equal(-2,c.RawAttack);
            var imported=CardViewData.From(c).ToCard();Equal(c.RawAttack,imported.RawAttack);Equal(c.OtherAttack,imported.OtherAttack);Equal(c.Attack,imported.Attack);
        });
        Test("Real summon activates pair and real combat removes it with exact damage",()=>{
            var g=PokerGame();var a=Hand(g,0,"Heart_2");Check(g.Summon(0,a.InstanceID,0));FinishEffects(g);var b=Hand(g,0,"Club_2");Check(g.Summon(0,b.InstanceID,1));FinishEffects(g);
            Equal(PokerKind.OnePair,g.State.Players[0].Poker.kind);Check(g.DrainEvents().Any(e=>e.Kind==BattleEventKind.PokerChanged));
            var foe=Unit(g,1,"Unknown_K",0);foe.SummonedTurn=0;g.State.ActivePlayer=1;Check(g.Attack(1,foe.InstanceID,0));Equal(PokerKind.None,g.State.Players[0].Poker.kind);Equal(0,b.PokerAttack);
        });
        Test("Unaffordable summon and Joker rejection preserve hand, mana, field and synergy",()=>{
            var g=PokerGame();PokerField(g,0,hands[1]);g.ReconcilePoker();g.DrainEvents();g.State.Players[0].CurrentMana=0;var c=Hand(g,0,"Heart_K");Check(!g.Summon(0,c.InstanceID,3));Check(g.State.LastError.Contains("필요 마나: 8 / 현재 마나: 0"));Check(g.State.Players[0].Hand.Contains(c));Equal(0,g.DrainEvents().Count);Equal(PokerKind.OnePair,g.State.Players[0].Poker.kind);
            var j=Hand(g,0,"Joker_1");Check(!g.Gamble(0,j.InstanceID,g.State.Players[0].Board[0].InstanceID));Check(g.State.Players[0].Hand.Contains(j));Equal(0,g.State.Players[0].CurrentMana);
        });
    }
}
