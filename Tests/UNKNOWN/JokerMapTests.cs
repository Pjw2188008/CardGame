using System;
using System.Linq;
using Unknown.Battle;
static partial class BattleRulesTests
{
    static BattleEngine MapFixture(JokerEventKind kind,int roll=0)
    {
        var e=JokerEventDefinition.Defaults().Single(x=>x.kind==kind); e.weight=100;
        var g=new BattleEngine(42,new BattleConfig{Map=BattleMap.JokerGame,StartingHand=0,ShuffleDecks=false,MapEvents=new[]{e}},new Dice{Value=roll});
        foreach(var p in g.State.Players)p.HandCards.Clear();
        Hand(g,0,"Club_A"); Hand(g,0,"Unknown_Q");
        Hand(g,1,"Unknown_J"); Hand(g,1,"Heart_A"); Hand(g,1,"Joker_1");
        var unit=Unit(g,1,"Heart_K"); unit.CurrentHP=6; g.State.Players[1].HP=21; g.DrainEvents(); return g;
    }
    static void JokerMapChecks()
    {
        PokerChecks();
        HandChecks();
        TutorialChecks();
        Test("Random map is unresolved in lobby and draws exactly once per match",()=>{
            var dice=new Dice(); var maps=new RandomMapSelection(dice); Check(!maps.Current.HasValue); Equal(0,maps.DrawCount);
            Equal(BattleMap.Diamond,maps.BeginMatch()); dice.Value=9000;
            for(int i=0;i<100;i++)Equal(BattleMap.Diamond,maps.BeginMatch()); Equal(1,dice.Calls); Equal(1,maps.DrawCount);
            maps.ResetMatch(); Check(!maps.Current.HasValue); Equal(BattleMap.JokerGame,maps.BeginMatch()); Equal(2,dice.Calls); Equal(2,maps.DrawCount);
        });
        Test("Weighted random maps cover five outcomes and exclude Normal over one hundred thousand matches",()=>{
            var maps=new RandomMapSelection(new SeededRandom(71)); var counts=MapDefinition.Defaults().ToDictionary(d=>d.map,d=>0);
            for(int i=0;i<100000;i++) { counts[maps.BeginMatch()]++; maps.ResetMatch(); }
            Equal(0,counts[BattleMap.Normal]); foreach(var map in new[]{BattleMap.Diamond,BattleMap.Spade,BattleMap.Heart,BattleMap.Club})Check(counts[map]>21500 && counts[map]<23500);
            Check(counts[BattleMap.JokerGame]>9300 && counts[BattleMap.JokerGame]<10700); Equal(100000,maps.DrawCount);
            Console.WriteLine("MAP DISTRIBUTION: "+string.Join(", ",counts.Select(p=>p.Key+"="+p.Value)));
        });
        SuitMapChecks();
        Test("Rematch discards previous map progress and activates only new map rules",()=>{
            foreach(var map in new[]{BattleMap.JokerGame,BattleMap.Normal,BattleMap.JokerGame}) {
                var g=new BattleEngine(4,new BattleConfig{Map=map});
                Check(g.State.Players.All(p=>p.JokerSuccesses==0 && p.UnknownCreated==0)); Check(!g.State.IsOver);
                Equal(map==BattleMap.JokerGame,g.MapEvent!=null); Equal(1,g.State.TurnNumber);
            }
        });
        Test("Joker map has exactly fourteen unique weighted events totaling 100 percent",()=>{
            var e=JokerEventDefinition.Defaults(); Equal(14,e.Length); Equal(14,e.Select(x=>x.kind).Distinct().Count()); Equal(100,e.Sum(x=>x.weight));
            Check(e.Select(x=>x.weight).SequenceEqual(new[]{13,10,10,10,10,8,6,3,8,5,5,4,4,4}));
        });
        foreach(var kind in Enum.GetValues(typeof(JokerEventKind)).Cast<JokerEventKind>().Where(x=>x!=JokerEventKind.None))
            Test("Joker event resolves once: "+kind,()=>{
                var g=MapFixture(kind); Check(g.EndTurn(0)); Equal(kind,g.MapEvent.Kind); Equal(2,g.MapEvent.ID);
                var events=g.DrainEvents(); Equal(1,events.Count(x=>x.Kind==BattleEventKind.MapEvent));
                Check(!g.ResolveMapEvent(1,JokerEventDefinition.Defaults().Single(x=>x.kind==kind))); Equal(0,g.DrainEvents().Count);
                var p=g.State.Players[1];
                switch(kind) {
                    case JokerEventKind.Strengthen: Equal(11,p.Board[0].Attack); break;
                    case JokerEventKind.Curse: Equal(6,p.Board[0].Attack); break;
                    case JokerEventKind.Reveal: Check(g.RevealedFor(1)!=null && g.RevealedFor(0)==null); break;
                    case JokerEventKind.Mana: Equal(5,p.CurrentMana); Equal(3,p.MaxMana); break;
                    case JokerEventKind.Draw: Equal(6,p.Hand.Count); Equal(3,events.Count(x=>x.Kind==BattleEventKind.Draw)); break;
                    case JokerEventKind.Exchange: Equal(4,p.Hand.Count); Equal(0,p.DiscardCount); Check(events.Any(x=>x.Kind==BattleEventKind.HandReturn)); break;
                    case JokerEventKind.Discard: Equal(2,p.Hand.Count); Equal(2,p.DiscardCount); break;
                    case JokerEventKind.HalveHP: Equal(11,p.HP); break;
                    case JokerEventKind.Gift: Equal(5,p.Hand.Count); Equal(53,p.DeckCount); Check(events.Any(x=>x.Kind==BattleEventKind.CardGranted)); break;
                    case JokerEventKind.Retry: Equal(70,g.JokerChance(1)); break;
                    case JokerEventKind.JokerCurse: Equal(30,g.JokerChance(1)); break;
                    case JokerEventKind.ForcedGamble: Equal(3,p.CurrentMana); Equal(1,p.JokerSuccesses); Check(p.Board[0].JokerUsed); break;
                    case JokerEventKind.LastHand: Equal(3,p.Hand.Count); Equal(2,p.DiscardCount); Check(events.Any(x=>x.Kind==BattleEventKind.CardGranted)); break;
                    case JokerEventKind.Multiply: Equal(12,p.Board[0].Attack); Equal(18,p.Board[0].MaxHP); Equal(12,p.Board[0].CurrentHP); Equal(0,p.JokerSuccesses); Check(!p.Board[0].JokerUsed); break;
                }
            });
        Test("Joker stat events clamp and preserve current-health rules on either branch",()=>{
            var g=MapFixture(JokerEventKind.Strengthen,1); Check(g.EndTurn(0)); Equal(15,g.State.Players[1].Board[0].MaxHP); Equal(9,g.State.Players[1].Board[0].CurrentHP);
            g=MapFixture(JokerEventKind.Multiply,1); Check(g.EndTurn(0)); Equal(4,g.State.Players[1].Board[0].Attack); Equal(6,g.State.Players[1].Board[0].MaxHP); Equal(6,g.State.Players[1].Board[0].CurrentHP);
            foreach(int hp in new[]{30,21,10,1}) { g=MapFixture(JokerEventKind.HalveHP); g.State.Players[1].HP=hp; Check(g.EndTurn(0)); Equal((hp+1)/2,g.State.Players[1].HP); Check(!g.State.IsOver); }
        });
        Test("Invalid events are conditionally redrawn and impossible pools finish without looping",()=>{
            var def=JokerEventDefinition.Defaults().Single(x=>x.kind==JokerEventKind.Strengthen); def.weight=100;
            var g=new BattleEngine(1,new BattleConfig{Map=BattleMap.JokerGame,MapEvents=new[]{def},ShuffleDecks=false},new Dice());
            Equal(JokerEventKind.None,g.MapEvent.Kind); Check(g.EndTurn(0)); Equal(JokerEventKind.None,g.MapEvent.Kind); Equal(2,g.State.TurnNumber);
            def.weight=99; var gift=JokerEventDefinition.Defaults().Single(x=>x.kind==JokerEventKind.Gift); gift.weight=1;
            g=new BattleEngine(1,new BattleConfig{Map=BattleMap.JokerGame,MapEvents=new[]{def,gift},ShuffleDecks=false},new Dice()); Equal(JokerEventKind.Gift,g.MapEvent.Kind);
        });
        Test("Gift and last hand create new instances without removing base-deck Jokers",()=>{
            foreach(var kind in new[]{JokerEventKind.Gift,JokerEventKind.LastHand}) {
                var g=MapFixture(kind); var p=g.State.Players[1]; var old=p.Hand.Select(x=>x.InstanceID).ToArray(); var deckIds=p.DeckCards.Select(x=>x.InstanceID).ToArray();
                Check(g.EndTurn(0)); var granted=g.DrainEvents().Single(x=>x.Kind==BattleEventKind.CardGranted);
                Check(!old.Contains(granted.TargetID) && !deckIds.Contains(granted.TargetID)); Equal(CardType.Joker,granted.Definition.CardType); Equal(54,CardDatabase.Deck.Count); Equal(2,CardDatabase.Deck.Count(x=>x.CardType==CardType.Joker));
            }
        });
        Test("Exchange preserves UNKNOWN identity, provenance and instance state",()=>{
            var g=MapFixture(JokerEventKind.Exchange); var card=g.State.Players[1].Hand[0]; card.Attack=19; card.JokerUsed=true;
            Check(g.EndTurn(0)); var p=g.State.Players[1]; var kept=p.Hand.Concat(p.DeckCards).Single(x=>x.InstanceID==card.InstanceID);
            Check(ReferenceEquals(card,kept)); Equal(19,kept.Attack); Check(kept.JokerUsed); Equal(CardType.Unknown,kept.Definition.CardType);
        });
        Test("Temporary mana is capped at ten and resets next personal turn",()=>{
            var g=MapFixture(JokerEventKind.Mana); g.State.Players[1].MaxMana=9; Check(g.EndTurn(0)); Equal(10,g.State.Players[1].CurrentMana); Equal(9,g.State.Players[1].MaxMana);
            g=MapFixture(JokerEventKind.Mana); Check(g.EndTurn(0)); Equal(5,g.State.Players[1].CurrentMana); Check(g.EndTurn(1)); Equal(5,g.State.Players[1].CurrentMana); Check(g.EndTurn(0)); Equal(6,g.State.Players[1].CurrentMana); Equal(4,g.State.Players[1].MaxMana);
        });
        Test("Reveal is recipient-only, expires at turn end and disappears when card leaves",()=>{
            var g=MapFixture(JokerEventKind.Reveal); Check(g.EndTurn(0)); var revealed=g.RevealedFor(1); var entries=g.DrainEvents();
            var own=BattleSnapshot.For(g,1,1,0,true,entries); var other=BattleSnapshot.For(g,0,1,0,true,entries);
            Equal(revealed.InstanceID,own.revealedCard.id); Check(other.revealedCard==null); Equal(0,own.players[0].hand.Length);
            Equal(JokerEventKind.None,other.mapEvent.kind); Check(!other.mapEvent.detail.Contains(revealed.Definition.CardName));
            g.State.Players[0].HandCards.Remove(revealed); Check(g.RevealedFor(1)==null); Check(BattleSnapshot.For(g,1,2,40,false,Array.Empty<BattleEvent>()).revealedCard==null);
            Check(g.EndTurn(1)); Check(g.RevealedFor(1)==null);
        });
        Test("Private map events hide event type, Joker grants and opponent victory counters",()=>{
            foreach(var kind in new[]{JokerEventKind.Gift,JokerEventKind.LastHand,JokerEventKind.Reveal,JokerEventKind.Retry,JokerEventKind.JokerCurse,JokerEventKind.Exchange,JokerEventKind.Discard}) {
                var g=MapFixture(kind); Check(g.EndTurn(0)); g.State.Players[1].JokerSuccesses=2; var e=g.DrainEvents();
                var own=BattleSnapshot.For(g,1,1,0,true,e); var other=BattleSnapshot.For(g,0,1,0,true,e);
                Equal(2,own.players[1].jokerSuccesses); Equal(0,other.players[1].jokerSuccesses); Equal(0,other.players[1].jokerChanceDelta); Equal(0,other.players[1].hand.Length);
                Equal(JokerEventKind.None,other.mapEvent.kind); Check(other.events.All(x=>x.kind!=BattleEventKind.CardGranted));
                Check(other.events.Where(x=>x.kind==BattleEventKind.Draw || x.kind==BattleEventKind.Discard || x.kind==BattleEventKind.HandReturn).All(x=>x.owner==0 || x.definition==null && x.target==-1));
            }
        });
        Test("One-use seventy/thirty percent modifiers have exact boundaries and expire",()=>{
            foreach(var kind in new[]{JokerEventKind.Retry,JokerEventKind.JokerCurse})foreach(int offset in new[]{-1,0}) {
                int threshold=kind==JokerEventKind.Retry ? 70 : 30; var g=MapFixture(kind,threshold+offset); Check(g.EndTurn(0));
                var unit=g.State.Players[1].Board[0]; var joker=g.State.Players[1].Hand.First(x=>x.Definition.CardType==CardType.Joker);
                Check(g.Gamble(1,joker.InstanceID,unit.InstanceID)); Equal(offset<0 ? 1 : 0,g.State.Players[1].JokerSuccesses); Equal(0,g.State.Players[1].JokerChanceDelta);
            }
            var end=MapFixture(JokerEventKind.Retry); Check(end.EndTurn(0)); Equal(20,end.State.Players[1].JokerChanceDelta); Check(end.EndTurn(1)); Equal(0,end.State.Players[1].JokerChanceDelta);
        });
        Test("Forced gamble failure spends no mana and does not reduce progress",()=>{
            var g=MapFixture(JokerEventKind.ForcedGamble,99); g.State.Players[1].JokerSuccesses=2; Check(g.EndTurn(0));
            Equal(3,g.State.Players[1].CurrentMana); Equal(2,g.State.Players[1].JokerSuccesses); Check(g.State.Players[1].Board[0]==null);
        });
        Test("Joker map disables UNKNOWN victory and wins immediately on third successful gamble",()=>{
            var g=new BattleEngine(1,new BattleConfig{Map=BattleMap.JokerGame,MapEvents=new JokerEventDefinition[0],StartingHand=0,ShuffleDecks=false,StartingMana=10},new Dice()); g.State.Players[0].HandCards.Clear();
            for(int i=0;i<3;i++) { var a=Hand(g,0,"Heart_A"); var f=Hand(g,0,"Spade_K"); Check(g.Combine(0,a.InstanceID,f.InstanceID)); }
            Equal(3,g.State.Players[0].UnknownCreated); Check(!g.State.IsOver);
            for(int i=0;i<3;i++) { var u=Unit(g,0,"Heart_K",i); var j=Hand(g,0,"Joker_1"); Check(g.Gamble(0,j.InstanceID,u.InstanceID)); if(i<2)Check(!g.State.IsOver); }
            Equal(0,g.State.Winner); Equal(VictoryReason.JokerJackpot,g.State.VictoryReason); Equal(3,g.State.Players[0].JokerSuccesses);
            Check(!g.EndTurn(0) && !g.Gamble(0,-1,-1) && !g.Combine(0,-1,-1));
            var p=g.State.Players[0]; p.Slots[0]=null; Equal(3,p.JokerSuccesses);
            var fresh=new BattleEngine(1); Equal(BattleMap.Normal,fresh.Config.Map); Equal(0,fresh.State.Players[0].JokerSuccesses); Check(fresh.MapEvent==null);
        });
    }
}
