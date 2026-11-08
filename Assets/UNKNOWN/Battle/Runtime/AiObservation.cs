using System;
using System.Linq;
namespace Unknown.Battle
{
    public enum AiDifficulty { Easy,Normal,Hard }
    /// <summary>Recipient-filtered copy. No authority reference, hidden hand, deck identities or random state.</summary>
    public sealed class AiObservation
    {
        internal readonly BattleSnapshot View;
        internal readonly BattleConfig Rules;
        internal readonly CardEffect Pending;
        public int Actor => View.recipient;
        public static AiObservation Capture(BattleEngine engine,int actor)
        {
            if(actor<0 || actor>1)throw new ArgumentOutOfRangeException(nameof(actor));
            var view=BattleSnapshot.For(engine,actor,0,0,false,new BattleEvent[0]);view.log=new string[0];
            return new AiObservation(view,engine.Config,engine.PendingAiEffect(actor));
        }
        AiObservation(BattleSnapshot view,BattleConfig rules,CardEffect effect){View=view;Rules=rules;Pending=effect;}
        internal BattleEngine Simulation(int outcome) => BattleEngine.CreateAiSimulation(View,Rules,new AiSimulationRandom(outcome));
        sealed class AiSimulationRandom : IRandomSource
        {
            readonly int outcome;
            public AiSimulationRandom(int value){outcome=value;}
            public int Next(int max) => outcome==0 ? 0 : max-1;
        }
    }
    public sealed partial class BattleEngine
    {
        internal CardEffect PendingAiEffect(int actor) => State.ActivePlayer==actor && source!=null && effectIndex<source.Definition.EffectList.Count ? source.Definition.EffectList[effectIndex] : null;
        internal static BattleEngine CreateAiSimulation(BattleSnapshot s,BattleConfig config,IRandomSource rng)
        {
            // Initialize in Normal so constructing a hypothetical state never rolls a real map event.
            var init=config.Copy();init.StartingHand=0;init.ShuffleDecks=false;init.Map=BattleMap.Normal;
            var g=new BattleEngine(0,init,rng);g.rules.Map=s.map;g.rules.MapEvents=config.MapEvents.Select(e=>e.Copy()).ToArray();
            g.State.ActivePlayer=s.active;g.State.TurnNumber=s.turn;g.State.Winner=s.winner;g.State.VictoryReason=s.victoryReason;g.State.TurnExpired=s.expired;
            g.MapEvent=s.mapEvent?.ToResult();g.SuitEffect=s.suitEffect?.Copy();
            g.nextID=Math.Max(1000,s.players.SelectMany(p=>p.hand.Concat(p.board)).Where(c=>c!=null && c.occupied).Select(c=>c.id).DefaultIfEmpty(0).Max());
            for(int owner=0;owner<2;owner++) {
                var p=g.State.Players[owner];var v=s.players[owner];p.HandCards.Clear();p.DeckCards.Clear();p.DiscardCards.Clear();
                p.HP=v.hp;p.CurrentMana=v.mana;p.MaxMana=v.maxMana;p.TurnsStarted=Math.Max(1,(s.turn+1-owner)/2);p.UnknownCreated=v.unknownCreated;p.JokerSuccesses=v.jokerSuccesses;p.JokerChanceDelta=v.jokerChanceDelta;p.ManualDiscardUsed=v.manualDiscardUsed;p.Poker=v.poker.Copy();
                for(int slot=0;slot<p.Slots.Length;slot++)p.Slots[slot]=v.board[slot]?.ToCard();
                if(owner==s.recipient)p.HandCards.AddRange(v.hand.Select(c=>c.ToCard()));
                else for(int i=0;i<v.handCount;i++)p.HandCards.Add(i==s.revealedIndex && s.revealedCard?.occupied==true ? s.revealedCard.ToCard() : g.NewCard(CardDatabase.Deck[(i*7+3)%54],owner));
                // Hypothetical draws use a fixed public prior, never either player's actual deck order.
                for(int i=0;i<v.deckCount;i++)p.DeckCards.Add(g.NewCard(CardDatabase.Deck[(i*11+owner*3)%54],owner));
                for(int i=0;i<v.discardCount;i++)p.DiscardCards.Add(g.NewCard(CardDatabase.Deck[i%54],owner));
            }
            g.DrainEvents();return g;
        }
    }
}
