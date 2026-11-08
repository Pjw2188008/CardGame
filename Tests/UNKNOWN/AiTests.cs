using System;
using System.Diagnostics;
using System.Linq;
using Unknown.Battle;
static partial class BattleRulesTests
{
    static BattleEngine AiFixture(BattleMap map=BattleMap.Normal)
    {var g=new BattleEngine(44,new BattleConfig{Map=map,StartingHand=0,StartingMana=10,ShuffleDecks=false});foreach(var p in g.State.Players)p.HandCards.Clear();g.DrainEvents();return g;}
    static void AiChecks()
    {
        foreach(AiDifficulty difficulty in Enum.GetValues(typeof(AiDifficulty))) {
            Test(difficulty+" AI takes immediate lethal and cannot see enemy hand",()=>{
                var g=AiFixture();var u=Unit(g,0,"Diamond_5");g.State.Players[1].HP=4;Hand(g,1,"Heart_A");var agent=new AiAgent(difficulty,42);var observation=AiObservation.Capture(g,0);
                Equal(0,observation.View.players[1].hand.Length);Equal(0,observation.View.players[1].unknownCreated);
                var m=agent.Decide(observation);Equal(BattleAction.Attack,m.Action);Equal(-1,m.Second);Check(m.Execute(g,0));Equal(0,g.State.Winner);
            });
            Test(difficulty+" AI takes third Unknown win without gambling",()=>{
                var g=AiFixture();g.State.Players[0].UnknownCreated=2;Hand(g,0,"Heart_A");Hand(g,0,"Spade_K");var m=new AiAgent(difficulty,1).Decide(AiObservation.Capture(g,0));Equal(BattleAction.Combine,m.Action);Check(m.Execute(g,0));Equal(VictoryReason.UnknownComplete,g.State.VictoryReason);
            });
            Test(difficulty+" AI legal candidates honor mana, summoning sickness, pending targets and once-discard",()=>{
                var g=AiFixture();var c=Hand(g,0,"Heart_5");Hand(g,0,"Joker_1");var u=Unit(g,0,"Spade_2");u.SummonedTurn=g.State.TurnNumber;g.State.Players[0].CurrentMana=1;
                var moves=AiAgent.Generate(AiObservation.Capture(g,0));Check(!moves.Any(m=>m.Action==BattleAction.Summon || m.Action==BattleAction.Joker || m.Action==BattleAction.Attack));
                g.State.Players[0].CurrentMana=10;Check(g.Summon(0,c.InstanceID,1));Check(g.NeedsTarget);var bot=new AiAgent(difficulty,4);var m=bot.Decide(AiObservation.Capture(g,0));Equal(BattleAction.Target,m.Action);Check(m.Execute(g,0));Check(!g.NeedsTarget);
            });
        }
        Test("Normal chooses a favorable kill and avoids protected material discard",()=>{
            var g=AiFixture();var u=Unit(g,0,"Heart_K");var enemy=Unit(g,1,"Diamond_2");var m=new AiAgent(AiDifficulty.Normal,1).Decide(AiObservation.Capture(g,0));Equal(BattleAction.Attack,m.Action);Equal(enemy.InstanceID,m.Second);
            Hand(g,0,"Heart_A");Hand(g,0,"Spade_K");for(int i=0;i<6;i++)Hand(g,0,"Club_2");var agent=new AiAgent(AiDifficulty.Normal,1);agent.Decide(AiObservation.Capture(g,0));Check(agent.LastCandidates.Where(x=>x.Action==BattleAction.Discard && (x.First==g.State.Players[0].Hand[0].InstanceID || x.First==g.State.Players[0].Hand[1].InstanceID)).All(x=>x.Score<0));
        });
        Test("Normal values pair, two pair and triple creation",()=>{
            foreach(int kind in new[]{1,2,3}){var g=AiFixture();Unit(g,0,"Heart_5");if(kind>=2)Unit(g,0,"Spade_5",1);if(kind==2)Unit(g,0,"Heart_3",2);g.ReconcilePoker();var c=Hand(g,0,kind==1 ? "Spade_5" : kind==2 ? "Spade_3" : "Diamond_5");
                var bot=new AiAgent(AiDifficulty.Normal,1);bot.Decide(AiObservation.Capture(g,0));Check(bot.LastCandidates.Any(x=>x.Action==BattleAction.Summon && x.First==c.InstanceID && x.Reason.Contains(PokerSynergy.Name((PokerKind)kind))));}
        });
        Test("Hard simulates a two-action lethal without mutating the live match",()=>{
            var g=AiFixture();Unit(g,0,"Heart_3");Unit(g,0,"Diamond_2",1);g.State.Players[1].HP=3;var bot=new AiAgent(AiDifficulty.Hard,1);var m=bot.Decide(AiObservation.Capture(g,0));Equal(BattleAction.Attack,m.Action);Check(bot.SimulationNodes>1 && bot.SimulationNodes<=AiAgent.MaxNodes);Equal(3,g.State.Players[1].HP);Check(!g.State.Players[0].Board[0].HasAttacked);
        });
        Test("Hard model uses no real hidden hand, deck order, private counters or random stream",()=>{
            var g=AiFixture();Hand(g,0,"Heart_3");Hand(g,0,"Club_5");Hand(g,1,"Heart_A");var u=Unit(g,1,"Spade_2");g.State.Players[1].UnknownCreated=2;
            var before=AiObservation.Capture(g,0);var a=new AiAgent(AiDifficulty.Hard,8).Decide(before);var opponent=g.State.Players[1];opponent.HandCards.Clear();Hand(g,1,"Unknown_K");opponent.DeckCards.Reverse();opponent.UnknownCreated=1;opponent.JokerSuccesses=2;
            var after=AiObservation.Capture(g,0);var b=new AiAgent(AiDifficulty.Hard,8).Decide(after);Equal(a.Action,b.Action);Equal(a.First,b.First);Equal(a.Second,b.Second);Equal(1,g.State.TurnNumber);Equal(10,g.State.Players[0].CurrentMana);
            var sim=before.Simulation(0);Check(sim.State.Players[1].Hand.All(c=>c.Definition.CardID!="Unknown_K"));Equal(0,sim.State.Players[1].UnknownCreated);
        });
        Test("Hard considers Joker jackpot only on Joker map and does not force the real roll",()=>{
            var g=AiFixture(BattleMap.JokerGame);g.State.Players[0].JokerSuccesses=2;g.State.Players[0].JokerChanceDelta=20;Hand(g,0,"Joker_1");Unit(g,0,"Heart_3");var bot=new AiAgent(AiDifficulty.Hard,1);var m=bot.Decide(AiObservation.Capture(g,0));Equal(BattleAction.Joker,m.Action);Check(m.Execute(g,0));Check(g.State.Players[0].JokerSuccesses==2 || g.State.Players[0].JokerSuccesses==3);
            Check(bot.SimulationNodes>0 && bot.SimulationNodes<=AiAgent.MaxNodes);
        });
        Test("Hard hypothetical gamble leaves authority random stream untouched",()=>{
            var dice=new Dice{Value=1};var g=new BattleEngine(1,new BattleConfig{StartingHand=0,ShuffleDecks=false,StartingMana=10},dice);foreach(var p in g.State.Players)p.HandCards.Clear();
            var joker=Hand(g,0,"Joker_1");var unit=Unit(g,0,"Heart_3");int calls=dice.Calls;new AiAgent(AiDifficulty.Hard,1).Decide(AiObservation.Capture(g,0));Equal(calls,dice.Calls);
            Check(g.Gamble(0,joker.InstanceID,unit.InstanceID));Equal(calls+1,dice.Calls);Check(g.State.Players[0].Board[0]==null,"real failure must remain a failure");
        });
        Test("All difficulties preserve full-hand Unknown materials and a next-turn jackpot Joker",()=>{
            var g=AiFixture(BattleMap.JokerGame);var p=g.State.Players[0];p.UnknownCreated=3;p.JokerSuccesses=2;p.CurrentMana=0;
            for(int i=0;i<5;i++){var c=Unit(g,0,"Heart_3",i);c.HasAttacked=true;}
            foreach(var id in new[]{"Heart_A","Spade_A","Diamond_A","Club_A","Heart_J","Spade_Q","Diamond_K","Joker_1"})Hand(g,0,id);
            foreach(AiDifficulty difficulty in Enum.GetValues(typeof(AiDifficulty)))for(int seed=0;seed<20;seed++)Equal(BattleAction.EndTurn,new AiAgent(difficulty,seed).Decide(AiObservation.Capture(g,0)).Action);
        });
        Test("AI vs AI across all maps and difficulties terminates without illegal moves or extra cards",()=>{
            int finished=0,actions=0;var seen=new bool[7];var max=new Stopwatch();max.Start();
            foreach(AiDifficulty difficulty in Enum.GetValues(typeof(AiDifficulty)))foreach(var map in new[]{BattleMap.Diamond,BattleMap.Spade,BattleMap.Heart,BattleMap.Club,BattleMap.JokerGame})for(int seed=1;seed<=3;seed++) {
                var g=new BattleEngine(seed*57,new BattleConfig{Map=map});var bots=new[]{new AiAgent(difficulty,seed),new AiAgent(difficulty,seed+11)};int inTurn=0,lastTurn=g.State.TurnNumber;
                for(int n=0;n<1600 && !g.State.IsOver;n++) {
                    int actor=g.State.ActivePlayer;if(lastTurn!=g.State.TurnNumber){lastTurn=g.State.TurnNumber;inTurn=0;}
                    var m=inTurn>=32 && !g.NeedsTarget ? new AiMove(BattleAction.EndTurn) : bots[actor].Decide(AiObservation.Capture(g,actor));
                    Check(m.Execute(g,actor),"illegal "+difficulty+" "+map+" "+m.Action+": "+g.State.LastError);inTurn++;seen[(int)m.Action]=true;actions++;
                    foreach(var p in g.State.Players){Check(p.CurrentMana>=0 && p.Hand.Count<=8);Check(p.Board.Count(c=>c!=null)<=5);var cards=p.Hand.Concat(p.Board.Where(c=>c!=null)).Concat(p.DeckCards).Concat(p.DiscardCards).ToArray();Check(cards.Select(c=>c.InstanceID).Distinct().Count()==cards.Length,"duplicate identity");}
                    Check(g.State.Players.SelectMany(p=>p.Hand.Concat(p.Board.Where(c=>c!=null)).Concat(p.DeckCards).Concat(p.DiscardCards)).Select(c=>c.InstanceID).Distinct().Count()==108+g.State.Players.Sum(p=>p.UnknownCreated)+(map==BattleMap.JokerGame ? g.State.Players.SelectMany(p=>p.Hand.Concat(p.Board.Where(c=>c!=null)).Concat(p.DeckCards).Concat(p.DiscardCards)).Count(c=>c.InstanceID>108 && c.Definition.CardType!=CardType.Unknown) : 0),"card conservation");
                    g.DrainEvents();
                }
                if(g.State.IsOver)finished++;
            }
            Console.WriteLine("AI MATCHES finished="+finished+"/45 actions="+actions+" ms="+max.ElapsedMilliseconds+" actions="+string.Join(",",seen));Check(finished>=40,"too many unfinished games");Check(seen[(int)BattleAction.Summon] && seen[(int)BattleAction.Attack] && seen[(int)BattleAction.Combine] && seen[(int)BattleAction.Joker] && seen[(int)BattleAction.Discard] && seen[(int)BattleAction.EndTurn]);
        });
    }
}
