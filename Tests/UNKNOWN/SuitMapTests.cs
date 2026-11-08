using System;
using System.Linq;
using Unknown.Battle;
static partial class BattleRulesTests
{
    static BattleEngine SuitFixture(BattleMap map,IRandomSource dice=null) => Fresh(dice ?? new Dice(),new BattleConfig{Map=map,StartingHand=0,ShuffleDecks=false,StartingMana=10});
    static void SuitMapChecks()
    {
        Test("Five map weighted boundaries are exact and Normal stays out of the online pool",()=>{
            int[] rolls={0,2249,2250,4499,4500,6749,6750,8999,9000,9999}; var kinds=new[]{BattleMap.Diamond,BattleMap.Spade,BattleMap.Heart,BattleMap.Club,BattleMap.JokerGame};
            for(int i=0;i<rolls.Length;i++)Equal(kinds[i/2],new RandomMapSelection(new Dice{Value=rolls[i]}).BeginMatch());
            Equal(10000,MapDefinition.Defaults().Sum(d=>d.weight));
        });
        foreach(var map in new[]{BattleMap.Diamond,BattleMap.Spade,BattleMap.Heart,BattleMap.Club})
        {
            Test("Matching normal summon gets its map bonus once: "+map,()=>{
                var g=SuitFixture(map); var d=MapDefinition.For(map); var card=Hand(g,0,d.suit+"_2"); int deck=g.State.Players[0].DeckCount;
                Check(g.Summon(0,card.InstanceID,0));
                Equal(card.Definition.Attack+d.summonAttack,card.Attack); Equal(card.Definition.MaxHP+d.summonHP,card.MaxHP); Equal(card.MaxHP,card.CurrentHP);
                Equal(deck-d.summonDraw,g.State.Players[0].DeckCount); FinishEffects(g); Check(!g.Summon(0,card.InstanceID,1));
                var other=Hand(g,0,(d.suit==Suit.Diamond ? "Spade" : "Diamond")+"_2"); Check(g.Summon(0,other.InstanceID,1)); FinishEffects(g);
                Equal(other.Definition.Attack,other.Attack); Equal(other.Definition.MaxHP,other.MaxHP);
            });
            Test("Turn effect once, host-owned and identical public projections: "+map,()=>{
                var g=SuitFixture(map); Unit(g,0,"Club_2"); g.State.Players[1].HP=25; g.State.Players[1].MaxMana=3; g.DrainEvents(); Check(g.EndTurn(0));
                var effect=g.SuitEffect; Check(effect!=null && effect.owner==1 && effect.id==2); Check(!g.ResolveSuitMapTurn(1)); var events=g.DrainEvents();
                Equal(1,events.Count(e=>e.Kind==BattleEventKind.SuitMapTurn));
                var a=BattleSnapshot.For(g,0,1,0,true,events); var b=BattleSnapshot.For(g,1,1,0,true,events);
                Equal(a.suitEffect.detail,b.suitEffect.detail); var view=BattleEngine.CreateView(a); Check(!view.ResolveSuitMapTurn(0));
                Equal(g.State.Players[0].Board[0]?.Attack,view.State.Players[0].Board[0]?.Attack); Equal(0,a.players[1].hand.Length); Equal(0,b.players[0].hand.Length);
                if(map==BattleMap.Diamond)Equal(CardDatabase.Get("Club_2").MaxHP-1,g.State.Players[0].Board[0].CurrentHP);
                if(map==BattleMap.Spade)Equal(Math.Max(0,CardDatabase.Get("Club_2").Attack-1),g.State.Players[0].Board[0].Attack);
                if(map==BattleMap.Heart)Equal(27,g.State.Players[1].HP);
                if(map==BattleMap.Club) { Equal(4,g.State.Players[1].CurrentMana); Equal(3,g.State.Players[1].MaxMana); }
            });
            Test("Suit map UNKNOWN victory and HP victory stop all new actions: "+map,()=>{
                var g=SuitFixture(map); for(int i=0;i<3;i++)Check(g.Combine(0,Hand(g,0,"Heart_A").InstanceID,Hand(g,0,"Club_K").InstanceID));
                Equal(VictoryReason.UnknownComplete,g.State.VictoryReason); Check(!g.EndTurn(0)); Check(!g.Summon(0,-1,0));
                g=SuitFixture(map); var unit=Unit(g,0,"Diamond_2"); g.State.Players[1].HP=1; Check(g.Attack(0,unit.InstanceID)); Equal(VictoryReason.OpponentHPZero,g.State.VictoryReason);
            });
        }
        Test("Diamond damage kills exactly one random enemy and never damages the hero",()=>{
            var g=SuitFixture(BattleMap.Diamond,new Dice{Value=1}); var a=Unit(g,0,"Club_2",0); var b=Unit(g,0,"Heart_2",1); b.CurrentHP=1;
            int hp=g.State.Players[0].HP; g.DrainEvents(); Check(g.EndTurn(0)); Check(g.State.Players[0].Board[1]==null); Check(g.State.Players[0].Board[0]!=null); Equal(hp,g.State.Players[0].HP);
            var e=g.DrainEvents(); Equal(1,e.Count(x=>x.Kind==BattleEventKind.Damage)); Equal(1,e.Count(x=>x.Kind==BattleEventKind.Destroy));
            g=SuitFixture(BattleMap.Diamond); g.DrainEvents(); Check(g.EndTurn(0)); Check(!g.DrainEvents().Any(x=>x.Kind==BattleEventKind.Damage));
        });
        Test("Spade reduction accumulates, clamps at zero and empty fields are harmless",()=>{
            var g=SuitFixture(BattleMap.Spade); var c=Unit(g,0,"Club_2"); c.Attack=1; Check(g.EndTurn(0)); Equal(0,c.Attack); Check(g.EndTurn(1)); Check(g.EndTurn(0)); Equal(0,c.Attack);
            g=SuitFixture(BattleMap.Spade); Check(g.EndTurn(0)); Check(!g.State.IsOver);
        });
        Test("Heart heals only active hero and clamps the actual heal at starting maximum HP",()=>{
            foreach(int hp in new[]{30,29,28,1}) { var g=SuitFixture(BattleMap.Heart); g.State.Players[1].HP=hp; g.State.Players[0].HP=10; Check(g.EndTurn(0)); Equal(Math.Min(30,hp+2),g.State.Players[1].HP); Equal(10,g.State.Players[0].HP); }
        });
        Test("Club mana caps at ten, does not increase maximum or carry over",()=>{
            var g=SuitFixture(BattleMap.Club); g.State.Players[1].MaxMana=3; Check(g.EndTurn(0)); Equal(4,g.State.Players[1].CurrentMana); g.State.Players[1].CurrentMana=0;
            Check(g.EndTurn(1)); Check(g.EndTurn(0)); Equal(4,g.State.Players[1].MaxMana); Equal(5,g.State.Players[1].CurrentMana);
            g.State.Players[0].MaxMana=10; Check(g.EndTurn(1)); Equal(10,g.State.Players[0].CurrentMana);
        });
        Test("Club summon draw stacks with card ability, handles empty decks and hand burn privately",()=>{
            var g=SuitFixture(BattleMap.Club); int deck=g.State.Players[0].DeckCount; var c=Hand(g,0,"Club_4"); Check(g.Summon(0,c.InstanceID,0)); FinishEffects(g); Equal(deck-2,g.State.Players[0].DeckCount);
            g=SuitFixture(BattleMap.Club); g.State.Players[0].DeckCards.Clear(); c=Hand(g,0,"Club_4"); Check(g.Summon(0,c.InstanceID,0)); Equal(0,g.State.Players[0].Hand.Count);
            g=SuitFixture(BattleMap.Club); c=Hand(g,0,"Club_3"); for(int i=0;i<7;i++)Hand(g,0,"Heart_2"); Check(g.Summon(0,c.InstanceID,0)); Equal(8,g.State.Players[0].Hand.Count);
            var events=g.DrainEvents(); var hidden=BattleSnapshot.For(g,1,1,0,true,events); Check(hidden.events.Where(e=>e.kind==BattleEventKind.Draw).All(e=>e.definition==null && e.target==-1));
        });
        Test("Normal and Joker never receive suit fixed bonuses; UNKNOWN is excluded on every map",()=>{
            foreach(var map in Enum.GetValues(typeof(BattleMap)).Cast<BattleMap>()) { var g=SuitFixture(map); var c=Hand(g,0,"Unknown_J"); Check(g.Summon(0,c.InstanceID,0)); FinishEffects(g); Equal(c.Definition.Attack+3,c.Attack); Equal(c.Definition.MaxHP,c.MaxHP); }
            foreach(var map in new[]{BattleMap.Normal,BattleMap.JokerGame}) { var g=SuitFixture(map); var c=Hand(g,0,"Diamond_2"); Check(g.Summon(0,c.InstanceID,0)); FinishEffects(g); Equal(c.Definition.Attack,c.Attack); Check(g.SuitEffect==null); }
        });
        Test("Suit bonuses stack with self abilities and use editable copied map values",()=>{
            var g=SuitFixture(BattleMap.Heart); var c=Hand(g,0,"Heart_3"); Check(g.Summon(0,c.InstanceID,0)); FinishEffects(g); Equal(c.Definition.MaxHP+4,c.MaxHP);
            var config=new BattleConfig{Map=BattleMap.Diamond,StartingHand=0,StartingMana=10,ShuffleDecks=false}; MapDefinition.For(BattleMap.Diamond,config.MapRules).summonAttack=4;
            g=Fresh(new Dice(),config); MapDefinition.For(BattleMap.Diamond,config.MapRules).summonAttack=99; c=Hand(g,0,"Diamond_2"); Check(g.Summon(0,c.InstanceID,0)); Equal(c.Definition.Attack+4,c.Attack);
        });
    }
}
