using System;
using System.Collections.Generic;
using System.Linq;

namespace Unknown.Battle
{
    /// <summary>Offline lesson scenarios and objectives. All player actions use BattleEngine commands.</summary>
    public sealed class TutorialSession
    {
        public int Step { get; }
        public BattleEngine Engine { get; }
        public int Explanation { get; private set; }
        public bool UnitAttack { get; private set; }
        public bool DirectAttack { get; private set; }
        public bool OverflowShown { get; private set; }
        public bool Gambled { get; private set; }
        public bool GambleSucceeded { get; private set; }
        public int Combinations => made.Count;
        readonly HashSet<Rank> made = new HashSet<Rank>();
        readonly int initialMana;
        bool summoned, discarded;
        public TutorialSession(int step, int seed = 0, IRandomSource random = null)
        {
            Step = Math.Max(1, Math.Min(7, step));
            Engine = new BattleEngine(seed, new BattleConfig { StartingHand = 0, ShuffleDecks = false, StartingMana = 10, Map = BattleMap.Normal }, random);
            // Reclaim the constructor's normal opening hand before configuring this local scenario.
            foreach (var p in Engine.State.Players) { p.DeckCards.AddRange(p.HandCards); p.HandCards.Clear(); }
            Engine.DrainEvents(); Engine.State.TurnNumber = 3;
            if (Step == 1) { Give("Heart_3"); Place(0,"Diamond_5",0); Place(1,"Spade_2",0); }
            if (Step == 2) Give("Heart_3");
            if (Step == 3) { Place(0,"Diamond_5",0); Place(0,"Heart_3",1); Place(1,"Spade_2",0); }
            if (Step == 4) foreach (var id in new[]{"Heart_2","Heart_3","Heart_4","Heart_5","Spade_2","Spade_3","Diamond_2","Diamond_3"}) Give(id);
            if (Step == 5) { Give("Heart_5"); Give("Spade_5"); }
            if (Step == 6) { Give("Joker_1"); Place(0,"Heart_3",0); }
            if (Step == 7) GiveMaterials();
            initialMana = Engine.State.Players[0].CurrentMana;
        }
        CardInstance Take(int owner,string id)
        {
            var p=Engine.State.Players[owner];var c=p.DeckCards.First(x=>x.Definition.CardID==id);p.DeckCards.Remove(c);return c;
        }
        void Give(string id) => Engine.State.Players[0].HandCards.Add(Take(0,id));
        void Place(int owner,string id,int slot) {var c=Take(owner,id);c.SummonedTurn=1;Engine.State.Players[owner].Slots[slot]=c;}
        public void ConfirmExplanation() { if(Step==1 && Explanation<7) Explanation++; }
        public void DemonstrateOverflow() {if(Step==4 && !OverflowShown)Engine.Draw(0,1);}
        public bool Allows(BattleAction action)
        {
            if(Complete)return false;
            if(action==BattleAction.Target)return Engine.NeedsTarget && (Step==2 || Step==5 || Step==7);
            return Step==2 || Step==5 ? action==BattleAction.Summon
                : Step==3 ? action==BattleAction.Attack
                : Step==4 ? OverflowShown && action==BattleAction.Discard
                : Step==6 ? action==BattleAction.Joker
                : Step==7 && (action==BattleAction.Combine || action==BattleAction.Summon);
        }
        public void Observe(IEnumerable<BattleEvent> events)
        {
            foreach(var e in events.Where(e=>e.Owner==0))
            {
                if(e.Kind==BattleEventKind.Summon){summoned=true;if(Step==7)Engine.State.Players[0].CurrentMana=10;}
                if(e.Kind==BattleEventKind.Attack) { if(e.TargetID<0 && e.Amount>0)DirectAttack=true;else if(e.TargetID>=0 && e.Amount>0)UnitAttack=true; }
                if(e.Kind==BattleEventKind.Discard && e.Secondary==1)OverflowShown=true;
                if(e.Kind==BattleEventKind.Discard && e.Secondary==2)discarded=true;
                if(e.Kind==BattleEventKind.JokerSuccess || e.Kind==BattleEventKind.JokerFailure) {Gambled=true;GambleSucceeded=e.Kind==BattleEventKind.JokerSuccess;}
                if(e.Kind==BattleEventKind.Combine && e.Definition!=null)made.Add(e.Definition.Rank);
            }
        }
        // Feed the next pair only after the previous combination animation has finished.
        public bool GiveMaterials()
        {
            if(Step!=7 || made.Count>=3 || Engine.State.Players[0].Hand.Any(c=>c.Definition.CardType==CardType.Normal))return false;
            Give(made.Count==0 ? "Heart_A" : made.Count==1 ? "Spade_A" : "Diamond_A");
            Give(made.Count==0 ? "Heart_J" : made.Count==1 ? "Spade_Q" : "Diamond_K");return true;
        }
        public bool Complete
        {
            get {
                var p=Engine.State.Players[0];
                switch(Step) {
                    case 1:return Explanation==7;
                    case 2:return summoned && p.Board.Any(c=>c!=null) && p.CurrentMana<initialMana && !Engine.NeedsTarget;
                    case 3:return UnitAttack && DirectAttack && Engine.State.Players[1].HP<30 && Engine.State.Players[1].Board.All(c=>c==null);
                    case 4:return OverflowShown && discarded && p.ManualDiscardUsed && p.Hand.Count==7 && p.DiscardCount==2;
                    case 5:return !Engine.NeedsTarget && p.Poker.kind==PokerKind.OnePair && p.Board.Count(c=>c!=null && c.PokerAttack==2 && c.Attack==c.NonPokerAttack+2)==2;
                    case 6:return Gambled && p.DiscardCards.Any(c=>c.Definition.CardType==CardType.Joker);
                    case 7:return made.Contains(Rank.Jack) && made.Contains(Rank.Queen) && made.Contains(Rank.King) && p.UnknownProgress==3 && Engine.State.Winner==0 && Engine.State.VictoryReason==VictoryReason.UnknownComplete;
                    default:return false;
                }
            }
        }
    }
}
