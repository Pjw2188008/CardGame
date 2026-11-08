using System;
using System.Linq;
using Unknown.Battle;
static partial class BattleRulesTests
{
    static void HandChecks()
    {
        Test("Eight-card default, 7+3 burns only the last two newly extracted cards",()=>{
            var g=Fresh();Equal(8,g.Config.HandLimit);for(int n=0;n<7;n++)Hand(g,0,"Heart_2");var p=g.State.Players[0];var before=p.Hand.Select(c=>c.InstanceID).ToArray();var expected=p.DeckCards.AsEnumerable().Reverse().Take(3).ToArray();int deck=p.DeckCount;g.DrainEvents();g.Draw(0,3);
            Equal(8,p.Hand.Count);Equal(deck-3,p.DeckCount);Check(before.SequenceEqual(p.Hand.Take(7).Select(c=>c.InstanceID)));Equal(expected[0],p.Hand[7]);Check(p.DiscardCards.SequenceEqual(expected.Skip(1)));var entries=g.DrainEvents();Equal(2,entries.Count(e=>e.Kind==BattleEventKind.Discard && e.Secondary==1));
        });
        foreach(var example in new[]{new[]{8,1,8,1},new[]{5,2,7,0}})Test("Draw cap example "+example[0]+"+"+example[1],()=>{
            var g=Fresh();for(int n=0;n<example[0];n++)Hand(g,0,"Heart_2");int deck=g.State.Players[0].DeckCount;g.Draw(0,example[1]);Equal(example[2],g.State.Players[0].Hand.Count);Equal(example[3],g.State.Players[0].DiscardCount);Equal(deck-example[1],g.State.Players[0].DeckCount);
        });
        Test("Manual discard is free, once per personal turn, with no immediate draw or poker changes",()=>{
            var g=PokerGame();PokerField(g,0,"Heart_5","Spade_5");g.ReconcilePoker();var c=Hand(g,0,"Joker_1");int deck=g.State.Players[0].DeckCount,mana=g.State.Players[0].CurrentMana;g.DrainEvents();Check(g.DiscardHand(0,c.InstanceID));Equal(0,g.State.Players[0].Hand.Count);Equal(deck,g.State.Players[0].DeckCount);Equal(mana,g.State.Players[0].CurrentMana);Check(g.State.Players[0].DiscardCards.Contains(c));Equal(PokerKind.OnePair,g.State.Players[0].Poker.kind);Equal(0,g.State.SummonsThisTurn);Check(!g.DrainEvents().Any(e=>e.Kind==BattleEventKind.PokerChanged));
            var d=Hand(g,0,"Heart_A");Check(!g.DiscardHand(0,d.InstanceID));Check(g.EndTurn(0));Check(!g.DiscardHand(0,d.InstanceID));Check(g.EndTurn(1));Check(!g.State.Players[0].ManualDiscardUsed);Check(g.DiscardHand(0,d.InstanceID));
        });
        Test("Discarding last card refills five only at next own turn, limited by remaining deck",()=>{
            foreach(int remaining in new[]{0,2,5}){var g=Fresh();var c=Hand(g,0,"Heart_2");g.State.Players[0].DeckCards.RemoveRange(remaining,g.State.Players[0].DeckCount-remaining);Check(g.DiscardHand(0,c.InstanceID));Equal(0,g.State.Players[0].Hand.Count);Check(g.EndTurn(0));Equal(0,g.State.Players[0].Hand.Count);Check(g.EndTurn(1));Equal(remaining,g.State.Players[0].Hand.Count);}
        });
        Test("Invalid ownership and instance IDs cannot spend manual discard allowance",()=>{
            var g=Fresh();var own=Hand(g,0,"Heart_2");var enemy=Hand(g,1,"Club_2");Check(!g.DiscardHand(1,enemy.InstanceID));Check(!g.DiscardHand(0,enemy.InstanceID));Check(!g.DiscardHand(0,-1));Check(!g.State.Players[0].ManualDiscardUsed);Check(g.DiscardHand(0,own.InstanceID));
        });
        Test("Authority rejects duplicate, stale and second discard requests without double mutation",()=>{
            var a=new BattleAuthority(2);var p=a.Engine.State.Players[0];var c=p.Hand[0];var intent=new BattleIntent{sequence=1,revision=0,turn=a.Engine.State.TurnNumber,action=BattleAction.Discard,first=c.InstanceID};Check(a.Execute(0,intent));int count=p.DiscardCount;Check(!a.Execute(0,intent));Equal(count,p.DiscardCount);intent.sequence=2;intent.revision=a.Revision;intent.first=p.Hand[0].InstanceID;Check(!a.Execute(0,intent));Equal(count,p.DiscardCount);
        });
        Test("Discard projection sends counts and reason but never an opponent hand identity",()=>{
            var g=Fresh();var c=Hand(g,0,"Unknown_J");g.DrainEvents();Check(g.DiscardHand(0,c.InstanceID));var entries=g.DrainEvents();var s=BattleSnapshot.For(g,1,1,40,false,entries);Equal(8,s.handLimit);Equal(0,s.players[0].hand.Length);var e=s.events.Single();Equal(-1,e.target);Equal(-1,e.source);Equal(null,e.definition);Equal(2,e.secondary);Check(!s.log.Any(l=>l.Contains("UNKNOWN")));var view=BattleEngine.CreateView(s);Equal(1,view.State.Players[0].DiscardCount);Check(view.State.Players[0].ManualDiscardUsed);Check(!view.DiscardHand(1,-1));
        });
        Test("Full-hand generated Joker is skipped; full-hand UNKNOWN combination safely frees a slot",()=>{
            var g=PokerGame(BattleMap.JokerGame);var a=Hand(g,0,"Heart_A");var f=Hand(g,0,"Spade_J");for(int n=0;n<6;n++)Hand(g,0,"Heart_2");int deck=g.State.Players[0].DeckCount;var gift=JokerEventDefinition.Defaults().Single(e=>e.kind==JokerEventKind.Gift);Check(!g.ResolveMapEvent(0,gift));Equal(8,g.State.Players[0].Hand.Count);Equal(deck,g.State.Players[0].DeckCount);Check(g.Combine(0,a.InstanceID,f.InstanceID));Equal(7,g.State.Players[0].Hand.Count);Equal(1,g.State.Players[0].Hand.Count(c=>c.Definition.CardType==CardType.Unknown));
        });
    }
}
