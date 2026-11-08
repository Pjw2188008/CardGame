using System.Linq;
using Unknown.Battle;
static partial class BattleRulesTests
{
    static void TutorialChecks()
    {
        Test("Tutorial scenarios preserve 54 original cards, HP and limits without network",()=>{
            for(int step=1;step<=7;step++) {var t=new TutorialSession(step);var g=t.Engine;Equal(8,g.Config.HandLimit);Equal(BattleMap.Normal,g.Config.Map);Check(!t.Complete);Check(!t.Allows(BattleAction.EndTurn));
                foreach(var p in g.State.Players){Equal(30,p.HP);Equal(54,p.DeckCount+p.Hand.Count+p.Board.Count(c=>c!=null));Equal(5,p.Board.Count);}
            }
        });
        Test("Tutorial step one requires all seven acknowledgements",()=>{var t=new TutorialSession(1);for(int i=0;i<6;i++){t.ConfirmExplanation();Check(!t.Complete);}t.ConfirmExplanation();Check(t.Complete);});
        Test("Tutorial summon uses actual card cost and pending effects",()=>{var t=new TutorialSession(2);var g=t.Engine;var c=g.State.Players[0].Hand[0];Check(t.Allows(BattleAction.Summon));Check(g.Summon(0,c.InstanceID,0));t.Observe(g.DrainEvents());Equal(9,g.State.Players[0].CurrentMana);Check(t.Complete);});
        Test("Tutorial attack requires unit and direct attacks with existing turn rules",()=>{
            var t=new TutorialSession(3);var g=t.Engine;var p=g.State.Players[0];Check(!g.Attack(0,p.Board[0].InstanceID,-1));Check(g.Attack(0,p.Board[0].InstanceID,0));t.Observe(g.DrainEvents());Check(!t.Complete);Check(g.Attack(0,p.Board[1].InstanceID,-1));t.Observe(g.DrainEvents());Check(t.Complete);Check(!g.Attack(0,p.Board[1].InstanceID,-1));
        });
        Test("Tutorial hand overflow preserves old hand, manual discard completes once",()=>{
            var t=new TutorialSession(4);var g=t.Engine;var p=g.State.Players[0];var old=p.Hand.Select(c=>c.InstanceID).ToArray();Check(!t.Allows(BattleAction.Discard));t.DemonstrateOverflow();t.Observe(g.DrainEvents());Check(old.SequenceEqual(p.Hand.Select(c=>c.InstanceID)));Equal(1,p.DiscardCount);Check(!t.Complete);Check(g.DiscardHand(0,p.Hand[0].InstanceID));t.Observe(g.DrainEvents());Check(t.Complete);Equal(7,p.Hand.Count);Check(!g.DiscardHand(0,p.Hand[0].InstanceID));
        });
        Test("Tutorial pair resolves real Heart target then applies attack two to both units",()=>{
            var t=new TutorialSession(5);var g=t.Engine;var p=g.State.Players[0];Check(g.Summon(0,p.Hand[0].InstanceID,0));t.Observe(g.DrainEvents());Check(g.NeedsTarget);Check(!t.Complete);FinishEffects(g);t.Observe(g.DrainEvents());Check(g.Summon(0,p.Hand[0].InstanceID,1));t.Observe(g.DrainEvents());FinishEffects(g);t.Observe(g.DrainEvents());Check(t.Complete);Equal(6,p.CurrentMana);
        });
        foreach(int roll in new[]{0,1})Test("Tutorial Joker accepts real outcome "+roll,()=>{var dice=new Dice{Value=roll};var t=new TutorialSession(6,42,dice);var g=t.Engine;Check(g.Gamble(0,g.State.Players[0].Hand[0].InstanceID,g.State.Players[0].Board[0].InstanceID));t.Observe(g.DrainEvents());Check(t.Complete);Equal(1,dice.Calls);Equal(roll==0,t.GambleSucceeded);Equal(8,g.State.Players[0].CurrentMana);});
        Test("Tutorial requires summoning all three Unknowns to win",()=>{
            var t=new TutorialSession(7);var g=t.Engine;for(int n=0;n<3;n++) {var h=g.State.Players[0].Hand;var a=h.Single(c=>c.Definition.Rank==Rank.Ace);var f=h.Single(c=>c.Definition.CardType==CardType.Normal && c.Definition.Rank!=Rank.Ace);Check(g.Combine(0,a.InstanceID,f.InstanceID));t.Observe(g.DrainEvents());Equal(n+1,t.Combinations);Check(!t.Complete);t.GiveMaterials();Check(!t.GiveMaterials());}
            Check(t.Allows(BattleAction.Summon));for(int slot=0;slot<3;slot++){Check(g.Summon(0,g.State.Players[0].Hand.First(c=>c.Definition.CardType==CardType.Unknown).InstanceID,slot));FinishEffects(g);t.Observe(g.DrainEvents());Equal(slot==2,t.Complete);}
            Check(t.Complete);Equal(VictoryReason.UnknownComplete,g.State.VictoryReason);Equal(3,g.State.Players[0].UnknownProgress);Equal(10,g.State.Players[0].CurrentMana);
        });
        Test("Tutorial restart has no prior attacks, buffs, discard use or combinations",()=>{
            var t=new TutorialSession(7);var h=t.Engine.State.Players[0].Hand;Check(t.Engine.Combine(0,h[0].InstanceID,h[1].InstanceID));t.Observe(t.Engine.DrainEvents());var restart=new TutorialSession(7);Equal(0,restart.Combinations);Equal(0,restart.Engine.State.Players[0].UnknownCreated);Equal(2,restart.Engine.State.Players[0].Hand.Count);Check(!restart.Engine.State.IsOver);
        });
    }
}
