#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace Unknown.Battle
{
    public static partial class BalanceVerification
    {
        static BattleEngine Fixture()
        {
            var engine=new BattleEngine(99,new BattleConfig {CardVersion=CardBalanceVersion.Baseline,StartingHand=0,StartingMana=10,EnablePoker=false,ShuffleDecks=false});
            foreach(var player in engine.State.Players)player.HandCards.Clear();engine.DrainEvents();return engine;
        }
        static CardInstance Hand(BattleEngine engine,string id,int owner=0)
        {var card=engine.NewCard(CardDatabase.Get(id,CardBalanceVersion.Baseline),owner);engine.State.Players[owner].HandCards.Add(card);return card;}
        static CardInstance Unit(BattleEngine engine,string id,int owner,int slot=0)
        {var card=engine.NewCard(CardDatabase.Get(id,CardBalanceVersion.Baseline),owner);engine.State.Players[owner].Slots[slot]=card;return card;}
        static void Require(bool condition,string name,List<string> passed)
        {if(!condition)throw new InvalidOperationException("Balance verification failed: "+name);passed.Add(name);}
        static BattleEngine CurrentFixture() {
            var g=new BattleEngine(1711,new BattleConfig{StartingHand=0,StartingMana=10,ShuffleDecks=false,EnablePoker=false});
            foreach(var p in g.State.Players)p.HandCards.Clear();g.DrainEvents();return g;
        }
        static CardInstance CurrentHand(BattleEngine g,string id,int owner=0) {
            var c=g.NewCard(CardDatabase.Get(id),owner);g.State.Players[owner].HandCards.Add(c);return c;
        }
        static CardInstance CurrentUnit(BattleEngine g,string id,int owner=0,int slot=0) {
            var c=g.NewCard(CardDatabase.Get(id),owner);g.State.Players[owner].Slots[slot]=c;return c;
        }
        static CardInstance PlayCurrent(BattleEngine g,string id,int slot,int targetOwner=-1,int targetSlot=0) {
            int actor=g.State.ActivePlayer;var c=CurrentHand(g,id,actor);
            if(!g.Summon(actor,c.InstanceID,slot))throw new InvalidOperationException(id+": "+g.State.LastError);
            if(targetOwner>=0 && g.NeedsTarget && !g.SelectEffectTarget(actor,targetOwner,targetSlot))throw new InvalidOperationException(id+" target: "+g.State.LastError);
            return c;
        }
        static void InvokeWeak(BattleEngine g,string method,params object[] args) => typeof(BattleEngine).GetMethod(method,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(g,args);
        public static string[] VerifyTimedSpade() {
            var passed=new List<string>();
            {
                var g=CurrentFixture();var t=CurrentUnit(g,"Diamond_K",1);t.MaxHP=10;t.CurrentHP=8;var attacker=CurrentUnit(g,"Diamond_4",0,1);
                PlayCurrent(g,"Spade_5",0,1);Require(t.MaxHP==8 && t.CurrentHP==6 && t.HealthWeakening==2 && t.HealthWeakCurrent==2,"HP weakening applies 8/10 to 6/8",passed);
                Require(g.Attack(0,attacker.InstanceID,0) && t.CurrentHP==3,"Real combat damage remains distinct from HP weakening",passed);
                g.EndTurn(0);g.EndTurn(1);Require(t.HealthWeakTurns==1 && t.CurrentHP==3,"HP weakening counts target-owner turns only",passed);
                g.EndTurn(0);g.EndTurn(1);Require(t.MaxHP==10 && t.CurrentHP==5 && t.HealthWeakening==0,"Expiry restores only weak HP loss: 3/8 to 5/10",passed);
                g.EndTurn(0);g.EndTurn(1);Require(t.CurrentHP==5,"No duplicate current-HP restoration",passed);
            }
            {
                var g=CurrentFixture();var t=CurrentUnit(g,"Diamond_K",1);t.CurrentHP=1;
                PlayCurrent(g,"Spade_5",0,1);Require(t.CurrentHP==1 && t.HealthWeakCurrent==0 && t.HealthWeakening==2,"HP weakening cannot kill or save nonexistent current-HP loss",passed);
                g.EndTurn(0);g.EndTurn(1);g.EndTurn(0);g.EndTurn(1);Require(t.CurrentHP==1,"Expiry is not an extra heal at minimum HP",passed);
            }
            {
                var g=CurrentFixture();var t=CurrentUnit(g,"Diamond_K",1);PlayCurrent(g,"Spade_5",0,1);PlayCurrent(g,"Spade_5",1,1);
                Require(t.HealthWeakening==3 && t.HealthWeakTurns==2,"Health weakening stacks independently to 3",passed);
                g.EndTurn(0);g.EndTurn(1);g.State.Players[0].CurrentMana=10;PlayCurrent(g,"Spade_5",2,1);
                Require(t.HealthWeakening==3 && t.HealthWeakTurns==2,"At-cap reapplication refreshes HP duration",passed);
            }
            {
                var g=CurrentFixture();var t=CurrentUnit(g,"Diamond_K",1);PlayCurrent(g,"Spade_7",0,1);
                Require(t.CardWeakening==1 && t.HealthWeakening==1 && t.AttackWeakTurns==3 && t.HealthWeakTurns==3,"Spade 7 independently applies both 3-turn states",passed);
                g.State.Players[0].CurrentMana=10;PlayCurrent(g,"Spade_K",1,1);
                Require(t.CardWeakening==3 && t.HealthWeakening==3 && t.AttackWeakTurns==3 && t.HealthWeakTurns==3,"Spade King caps ATK and HP independently",passed);
            }
            {
                var g=CurrentFixture();var t=CurrentUnit(g,"Diamond_K");var dealer=CurrentUnit(g,"Spade_2",1);var effect=dealer.Definition.EffectList[0];
                InvokeWeak(g,"ApplyTimedAttackWeak",dealer,t,1,effect);
                g.EndTurn(0);Require(t.AttackWeakTurns==2,"Application during owner turn does not consume a partial turn",passed);
                g.EndTurn(1);g.EndTurn(0);Require(t.AttackWeakTurns==1,"First full target-owner turn consumes one duration",passed);
                g.EndTurn(1);g.EndTurn(0);Require(t.CardWeakening==0,"Second full target-owner turn expires 2-turn state",passed);
            }
            {
                var g=CurrentFixture();var t=CurrentUnit(g,"Heart_2",1);int baseAtk=t.Attack;PlayCurrent(g,"Spade_J",0,1);
                Require(t.Attack==0 && t.CardWeakening==3,"Attack display never drops below zero while retaining capped state",passed);
                g.EndTurn(0);g.State.Players[1].CurrentMana=10;var joker=CurrentHand(g,"Joker_1",1);g.DebugNextGamble=true;g.Gamble(1,joker.InstanceID,t.InstanceID);
                g.EndTurn(1);g.EndTurn(0);g.EndTurn(1);g.EndTurn(0);g.EndTurn(1);
                Require(t.Attack==baseAtk && t.CardWeakening==0,"JOKER on a zero-ATK weakened unit does not overrestore attack",passed);
            }
            {
                var g=CurrentFixture();var t=CurrentUnit(g,"Diamond_K",1);PlayCurrent(g,"Spade_5",0,1);g.EndTurn(0);
                PlayCurrent(g,"Heart_6",1,1,0);int max=t.MaxHP;int hp=t.CurrentHP;
                g.EndTurn(1);g.EndTurn(0);g.EndTurn(1);
                Require(t.MaxHP==max+2 && t.CurrentHP==hp+2,"Max-HP growth survives HP weakening expiry",passed);
            }
            {
                var g=CurrentFixture();var t=CurrentUnit(g,"Diamond_K",1);PlayCurrent(g,"Spade_3",0,1);
                var a=CurrentUnit(g,"Spade_9",0,1);int hp=t.CurrentHP;g.Attack(0,a.InstanceID,0);
                Require(hp-t.CurrentHP==a.Definition.Attack+3,"Spade 9 recognizes HP-only weakening",passed);
            }
            {
                var g=CurrentFixture();var t=CurrentUnit(g,"Diamond_K",1);PlayCurrent(g,"Spade_7",0,1);
                var s=UnityEngine.JsonUtility.FromJson<BattleSnapshot>(UnityEngine.JsonUtility.ToJson(BattleSnapshot.For(g,0,0,0,false,new BattleEvent[0])));var c=BattleEngine.CreateView(s).State.Players[1].Board[0];
                Require(c.CardWeakening==t.CardWeakening && c.HealthWeakening==t.HealthWeakening && c.HealthWeakCurrent==t.HealthWeakCurrent && c.AttackWeakTurns==3 && c.HealthWeakTurns==3 && c.AttackWeakAppliedTurn==t.AttackWeakAppliedTurn && c.HealthWeakSource==t.HealthWeakSource,"Network round-trip preserves independent values, timers and restoration ledger",passed);
                t.CurrentHP=1;g.State.Players[0].CurrentMana=10;PlayCurrent(g,"Diamond_2",1,1);
                Require(!g.State.Players[1].Board.Contains(t) && t.CardWeakening==0 && t.HealthWeakening==0 && t.AttackWeakTurns==0 && t.HealthWeakTurns==0,"Death clears all weakening state without revival",passed);
            }
            {
                var options=new SingleBattleOptions{stage=new SingleStage{kind=SingleNodeKind.Battle,turnLimit=20,startMana=10,manaRecovery=10,turnSpendLimit=10,manaBudget=100,enemyHP=100},difficulty=new SingleDifficulty(),deck=CardDatabase.Deck.Where(c=>c.CardType==CardType.Normal).Select(c=>c.CardID).ToArray(),augments=new[]{"2"},augmentData=new[]{new SingleAugment{id="2",duration=1}},hp=30,maxHP=30,seed=1711};
                var g=new BattleEngine(1711,new BattleConfig{StartingHand=0,StartingMana=10,EnablePoker=false,Single=options},new SingleRandom(1711));g.State.Players[0].HandCards.Clear();var t=CurrentUnit(g,"Diamond_K",1);
                PlayCurrent(g,"Spade_4",0,1);Require(t.AttackWeakTurns==3 && g.Single.Progress.weakening.Count==0,"Existing single augment adds its unchanged +1 duration once",passed);
                var save=g.SaveSingle();var restored=BattleEngine.CreateSingle(options,save);var copy=restored.State.Players[1].Board[0];
                Require(copy.CardWeakening==2 && copy.AttackWeakTurns==3 && copy.AttackWeakAppliedTurn==t.AttackWeakAppliedTurn,"Single save/continue preserves timed weakening",passed);
            }
            return passed.ToArray();
        }

        public static string[] VerifyCurrentCards() {
            var passed=new List<string>();
            foreach(var d in CardDatabase.All.Where(c=>c.CardType==CardType.Normal)) {
                var g=CurrentFixture();var a=CurrentUnit(g,"Heart_K");a.CurrentHP=2;
                var b=CurrentUnit(g,"Heart_J",0,1);b.CurrentHP=2;
                for(int slot=0;slot<3;slot++){var t=CurrentUnit(g,"Unknown_K",1,slot);t.CurrentHP=8;t.CardWeakening=1;t.AbilityAttack=-1;t.Attack--;}
                CurrentHand(g,"Heart_2");CurrentHand(g,"Spade_2");
                PlayCurrent(g,d.CardID,2);
                for(int step=0;step<20 && g.NeedsTarget;step++) {
                    if(g.NeedsExchange){g.ChooseExchange(0,g.State.Players[0].Hand.FirstOrDefault()?.InstanceID ?? -1);continue;}
                    bool selected=false;
                    for(int owner=0;owner<2 && !selected;owner++)for(int slot=0;slot<5 && !selected;slot++)if(g.IsValidEffectTarget(owner,slot)){g.SelectEffectTarget(0,owner,slot);selected=true;}
                    if(!selected)throw new InvalidOperationException("Unresolvable effect: "+d.CardID);
                }
                Require(!g.NeedsTarget,"52-card summon/target continuation: "+d.CardID,passed);
            }
            {
                var g=CurrentFixture();var t=CurrentUnit(g,"Diamond_K",1);int atk=t.Attack;
                PlayCurrent(g,"Spade_J",0,1);PlayCurrent(g,"Spade_4",1,1);
                Require(t.CardWeakening==3 && t.Attack==atk-3,"Weakening capped at 3",passed);
                g.EndTurn(0);Require(t.CardWeakening==3,"Weakening persists through target owner turn",passed);
                g.EndTurn(1);g.EndTurn(0);g.EndTurn(1);g.EndTurn(0);g.EndTurn(1);Require(t.CardWeakening==0 && t.Attack==atk,"Weakening expires after three target-owner turns",passed);
            }
            {
                var g=CurrentFixture();var t=CurrentUnit(g,"Diamond_K");int hp=t.CurrentHP;
                PlayCurrent(g,"Heart_3",1,0);PlayCurrent(g,"Heart_3",2,0);
                Require(t.Protection==2,"Protection uses maximum rather than stacking",passed);
                g.State.ActivePlayer=1;PlayCurrent(g,"Diamond_2",0,0);
                Require(t.CurrentHP==hp && t.Protection==0,"Next positive spell hit consumes protection",passed);
            }
            {
                var g=CurrentFixture();var a=CurrentUnit(g,"Diamond_7");var t=CurrentUnit(g,"Heart_K",1);t.Protection=3;int before=t.CurrentHP;
                Require(g.Attack(0,a.InstanceID,0) && t.CurrentHP==before-a.Definition.Attack,"Diamond 7 ignores protection on attack",passed);
            }
            {
                var g=CurrentFixture();var a=CurrentUnit(g,"Diamond_2");int atk=a.Attack;
                PlayCurrent(g,"Diamond_8",1,0);Require(a.Attack==atk+2,"Temporary targeted attack applies",passed);
                g.EndTurn(0);Require(a.Attack==atk && a.TemporaryAttack==0,"Temporary attack removed at turn end",passed);
            }
            {
                var g=CurrentFixture();g.State.Players[0].CurrentMana=10;
                PlayCurrent(g,"Club_3",0);PlayCurrent(g,"Club_3",1);PlayCurrent(g,"Club_3",2);
                Require(g.State.Players[0].CardManaGained==2 && g.State.Players[0].CurrentMana==9,"Card mana gains capped at +2 per turn",passed);
                g.EndTurn(0);g.EndTurn(1);Require(g.State.Players[0].CardManaGained==0,"Card mana allowance resets on owner turn",passed);
            }
            {
                var g=CurrentFixture();PlayCurrent(g,"Club_8",0);var c=CurrentHand(g,"Diamond_2");
                Require(g.PlacementCost(c)==1,"Discount respects minimum cost 1",passed);
                int before=g.State.Players[0].CurrentMana;g.Summon(0,c.InstanceID,1);
                Require(before-g.State.Players[0].CurrentMana==1 && g.State.Players[0].NextDiscount==0,"Next placement consumes discount once",passed);
                PlayCurrent(g,"Club_5",2);g.EndTurn(0);Require(g.State.Players[0].NextDiscount==0,"This-turn discount expires",passed);
            }
            for(int choose=0;choose<=2;choose++) {
                var g=CurrentFixture();var a=CurrentHand(g,"Heart_2");var b=CurrentHand(g,"Spade_2");var q=PlayCurrent(g,"Club_Q",0);
                if(choose>0)g.ChooseExchange(0,a.InstanceID);if(choose>1)g.ChooseExchange(0,b.InstanceID);else g.ChooseExchange(0,-1);
                Require(!g.NeedsTarget && g.State.Players[0].Hand.Count==4 && g.State.Players[0].DiscardCount==choose,"Club Queen choose/discard/draw count "+choose,passed);
            }
            {
                var g=CurrentFixture();CurrentHand(g,"Heart_2");PlayCurrent(g,"Club_4",0);
                Require(!g.ChooseExchange(0,-1) && g.NeedsExchange,"Mandatory discard cannot be skipped",passed);
                var c=g.State.Players[0].Hand[0];g.ChooseExchange(0,c.InstanceID);
                Require(g.State.Players[0].DiscardCards.Contains(c) && g.State.Players[0].Hand.Count==2,"Discard moves existing instance then draws 2",passed);
            }
            {
                var g=CurrentFixture();var a=CurrentUnit(g,"Diamond_5");var t=CurrentUnit(g,"Heart_2",1);t.CurrentHP=1;
                Require(g.Attack(0,a.InstanceID,0) && g.State.Players[1].HP==28,"Normal-attack kill triggers hero damage",passed);
                g=CurrentFixture();t=CurrentUnit(g,"Heart_2",1);t.CurrentHP=1;var c=PlayCurrent(g,"Diamond_6",0,1);
                Require(c.Attack==c.Definition.Attack+1 && g.State.Players[1].HP==30,"Summon kill grants only specified reward",passed);
            }
            {
                var g=CurrentFixture();var a=CurrentUnit(g,"Heart_K",1);a.CardWeakening=1;a.Attack--;a.AbilityAttack--;
                var b=CurrentUnit(g,"Heart_K",1,1);int hpA=a.CurrentHP,hpB=b.CurrentHP;
                PlayCurrent(g,"Spade_A",0);Require(g.NeedsTarget && g.IsValidEffectTarget(1,0) && !g.IsValidEffectTarget(1,1),"Spade Ace targets only previously weakened enemies",passed);
                g.SelectEffectTarget(0,1,0);Require(a.CurrentHP==hpA-4 && b.CurrentHP==hpB-1,"Spade Ace damage occurs once after all-enemy weakening",passed);
            }
            {
                var g=CurrentFixture();var a=CurrentUnit(g,"Heart_9");int atk=a.Attack;
                for(int i=0;i<3;i++){g.EndTurn(0);g.EndTurn(1);}
                Require(a.GrowthCount==2 && a.Attack==atk+2,"Heart 9 permanent growth limited to 2 owner turns",passed);
            }
            {
                var g=CurrentFixture();var a=CurrentUnit(g,"Heart_K");a.CurrentHP-=2;var b=CurrentUnit(g,"Heart_10",0,1);b.CurrentHP--;
                var q=PlayCurrent(g,"Heart_Q",2);Require(a.CurrentHP==a.MaxHP && b.CurrentHP==b.MaxHP && q.Protection==2,"Heart Queen counts actually healed units",passed);
                g.State.Players[0].CurrentMana=10;var h=PlayCurrent(g,"Heart_A",3);int atk=a.Definition.Attack;
                Require(a.Attack==atk+1 && a.TemporaryAttack==1,"Heart Ace buffs units full after recovery",passed);
                g.EndTurn(0);Require(a.Attack==atk,"Heart Ace full-health buff expires",passed);
            }
            {
                var g=CurrentFixture();var t=CurrentUnit(g,"Heart_9");t.Protection=3;t.ProtectionSource=t.InstanceID;t.CardWeakening=2;t.WeakUntil=2;t.TemporaryAttack=1;t.GrowthCount=1;
                g.State.Players[0].NextDiscount=2;g.State.Players[0].CardManaGained=1;
                var s=UnityEngine.JsonUtility.FromJson<BattleSnapshot>(UnityEngine.JsonUtility.ToJson(BattleSnapshot.For(g,0,1,0,false,new BattleEvent[0])));
                var restored=BattleEngine.CreateView(s);var c=restored.State.Players[0].Board[0];
                Require(c.Protection==3 && c.ProtectionSource==t.InstanceID && c.CardWeakening==2 && c.WeakUntil==2 && c.TemporaryAttack==1 && c.GrowthCount==1 && restored.State.Players[0].NextDiscount==2,"Network snapshot round-trip preserves all card state and cost discount",passed);
            }
            foreach(string face in new[]{"J","Q","K"}) {
                var g=CurrentFixture();var a=CurrentHand(g,"Diamond_A");var f=CurrentHand(g,"Heart_"+face);
                Require(g.Combine(0,a.InstanceID,f.InstanceID) && g.State.Players[0].Hand.Any(c=>c.Definition.CardID=="Unknown_"+face),"Existing hand-only UNKNOWN combination "+face,passed);
            }
            {
                var stage=new SingleStage{kind=SingleNodeKind.Battle,turnLimit=20,startMana=10,manaRecovery=10,turnSpendLimit=10,manaBudget=100,enemyHP=100};
                var options=new SingleBattleOptions{stage=stage,difficulty=new SingleDifficulty(),deck=CardDatabase.Deck.Where(c=>c.CardType==CardType.Normal).Select(c=>c.CardID).ToArray(),augments=new string[0],hp=30,maxHP=30,seed=1711};
                var g=new BattleEngine(1711,new BattleConfig{StartingHand=0,StartingMana=10,EnablePoker=false,Single=options},new SingleRandom(1711));
                g.State.Players[0].HandCards.Clear();var t=CurrentUnit(g,"Diamond_K",1);int atk=t.Attack;
                PlayCurrent(g,"Spade_4",0,1);Require(g.Single.Progress.weakening.Count==0,"Single weakening uses one expiry owner",passed);
                g.EndTurn(0);g.EndTurn(1);Require(t.CardWeakening==2 && t.AttackWeakTurns==1,"Single two-turn weakening remains after one turn",passed);
                g.EndTurn(0);g.EndTurn(1);Require(t.Attack==atk && t.CardWeakening==0,"Single weakening restores once",passed);
                g.EndTurn(0);g.EndTurn(1);Require(t.Attack==atk,"Single later turns cannot restore weakening twice",passed);
                PlayCurrent(g,"Club_5",1);g.Single.Progress.spent=99;g.Single.Progress.turnSpent=9;
                var hand=CurrentHand(g,"Diamond_4");Require(g.Single.SummonError(hand,2)=="" && g.PlacementCost(hand)==1,"Single budget and turn cap use discounted cost",passed);
                Require(g.Summon(0,hand.InstanceID,2) && g.Single.Progress.spent==100 && g.Single.Progress.turnSpent==10,"Single discounted payment uses actual budget",passed);
                var dead=g.NewCard(CardDatabase.Get("Heart_9"),0);dead.Protection=3;dead.CardWeakening=2;dead.WeakUntil=5;dead.TemporaryAttack=2;dead.GrowthCount=2;dead.LastCardTurn=9;g.State.Players[0].DiscardCards.Add(dead);
                Require(g.Single.Recycle(0) && dead.Protection==0 && dead.CardWeakening==0 && dead.WeakUntil==0 && dead.TemporaryAttack==0 && dead.GrowthCount==0 && dead.LastCardTurn==-1,"Single recycling clears previous field-only card states",passed);
            }
            {
                foreach(var c in CardDatabase.All.Where(c=>c.CardType!=CardType.Normal)) {
                    var before=CardDatabase.Get(c.CardID,CardBalanceVersion.Revised);
                    Require(c.Attack==before.Attack && c.MaxHP==before.MaxHP && c.ManaCost==before.ManaCost && c.Description==before.Description && c.EffectList.Select(e=>e.Kind+":"+e.Amount+":"+e.Target).SequenceEqual(before.EffectList.Select(e=>e.Kind+":"+e.Amount+":"+e.Target)),"Unchanged non-normal definition: "+c.CardID,passed);
                }
                var g=CurrentFixture();CurrentUnit(g,"Unknown_J");CurrentUnit(g,"Unknown_Q",0,1);
                PlayCurrent(g,"Unknown_K",2);while(g.NeedsTarget){bool chosen=false;for(int owner=0;owner<2 && !chosen;owner++)for(int slot=0;slot<5 && !chosen;slot++)if(g.IsValidEffectTarget(owner,slot)){g.SelectEffectTarget(0,owner,slot);chosen=true;}if(!chosen)throw new Exception("UNKNOWN continuation");}
                Require(g.State.IsOver && g.State.VictoryReason==VictoryReason.UnknownComplete,"Existing three living UNKNOWN victory",passed);
                g=CurrentFixture();var a=CurrentUnit(g,"Heart_K");var j=CurrentHand(g,"Joker_1");g.DebugNextGamble=true;
                Require(g.Gamble(0,j.InstanceID,a.InstanceID) && a.Attack==(int)Math.Ceiling(a.Definition.Attack*1.5),"Existing JOKER success scaling",passed);
                g=CurrentFixture();a=CurrentUnit(g,"Heart_K");j=CurrentHand(g,"Joker_2");g.DebugNextGamble=false;
                Require(g.Gamble(0,j.InstanceID,a.InstanceID) && !g.State.Players[0].Board.Contains(a),"Existing JOKER failure destroys target",passed);
                g=new BattleEngine(1711,new BattleConfig{StartingHand=0,StartingMana=10,EnablePoker=true});g.State.Players[0].HandCards.Clear();
                a=CurrentUnit(g,"Diamond_2");var b=PlayCurrent(g,"Heart_2",1,0);var unrelated=PlayCurrent(g,"Club_3",2);
                Require(g.State.Players[0].Poker.kind==PokerKind.OnePair && a.PokerAttack==1 && b.PokerAttack==1 && unrelated.PokerAttack==0,"Existing poker pair affects only member cards",passed);
            }
            return passed.ToArray();
        }

        public static List<string> Run(string parent)
        {
            var passed=new List<string>();
            Require(!BalanceReport.SupportsMetric(CardDatabase.Get("Unknown_Q",CardBalanceVersion.Baseline),"damage_per_use") && BalanceReport.SupportsMetric(CardDatabase.Get("Unknown_Q",CardBalanceVersion.Baseline),"extra_hp_per_use"),"Support-only cards are screened by their actual effect role rather than damage",passed);
            foreach(var definition in CardDatabase.AllFor(CardBalanceVersion.Baseline).Where(d=>d.CardType!=CardType.Joker)) {
                var fixture=Fixture();Unit(fixture,"Heart_K",0,0).CurrentHP=1;Unit(fixture,"Heart_K",0,1).CurrentHP=1;
                for(int slot=0;slot<3;slot++)Unit(fixture,"Unknown_K",1,slot);
                Hand(fixture,"Heart_2",1);Hand(fixture,"Heart_2",1);var hand=Hand(fixture,definition.CardID);
                var observedEffects=new BalanceMatchCollector(fixture,-1);
                if(!fixture.Summon(0,hand.InstanceID,2))throw new InvalidOperationException("Fixture summon rejected: "+definition.CardID);
                for(int action=0;action<20 && fixture.NeedsTarget;action++) {
                    bool chosen=false;
                    for(int owner=0;owner<2 && !chosen;owner++)for(int slot=0;slot<5 && !chosen;slot++)
                        if(fixture.IsValidEffectTarget(owner,slot)){fixture.SelectEffectTarget(0,owner,slot);chosen=true;}
                    if(!chosen)throw new InvalidOperationException("Fixture cannot resolve target");
                }
                for(int index=0;index<definition.EffectList.Count;index++) {
                    var effect=definition.EffectList[index];
                    int count=effect.Target==EffectTarget.AllAllies || effect.Target==EffectTarget.AllEnemies ? 3 : 1;
                    double expected=effect.Amount*count;
                    if(effect.Kind==EffectKind.ChainDamage)expected=effect.Amount*2;
                    if(effect.Kind==EffectKind.DiscardCard)expected=Math.Min(2,effect.Amount);
                    if(effect.Kind==EffectKind.Heal)expected=effect.Target==EffectTarget.Self ? 0 : Math.Min(10,effect.Amount)*(effect.Target==EffectTarget.AllAllies ? 2 : 1);
                    if(!observedEffects.Effects.TryGetValue(definition.CardID+"|"+index,out var actual) || actual.actualAmount!=expected)
                        throw new InvalidOperationException("Effect fixture amount mismatch: "+definition.CardID+" / "+effect.Kind+" expected "+expected+" actual "+(actual?.actualAmount.ToString() ?? "missing"));
                }
                Require(!fixture.NeedsTarget,"Actual intrinsic effect vectors match card data: "+definition.CardID,passed);
            }
            var engine=Fixture();var target=Unit(engine,"Heart_2",1);target.CurrentHP=1;var damage=Hand(engine,"Diamond_2");var collector=new BalanceMatchCollector(engine,0);
            Require(engine.Summon(0,damage.InstanceID,0) && engine.SelectEffectTarget(0,1,0),"Resolved damage uses the real summon/target commands",passed);
            Require(collector.Rows["0|Diamond_2"].effectDamage==1 && collector.Rows["0|Diamond_2"].kills==1,"Actual damage is HP-capped and killing source is preserved",passed);
            engine=Fixture();var healTarget=Unit(engine,"Heart_2",0);healTarget.CurrentHP=3;var heal=Hand(engine,"Heart_2");collector=new BalanceMatchCollector(engine,1);
            engine.Summon(0,heal.InstanceID,1);engine.SelectEffectTarget(0,0,0);
            Require(collector.Rows["0|Heart_2"].healing==1,"Healing records actual HP restored rather than requested amount",passed);
            engine=Fixture();var empty=Hand(engine,"Diamond_2");collector=new BalanceMatchCollector(engine,2);engine.Summon(0,empty.InstanceID,0);
            Require(collector.Effects["Diamond_2|0"].failures==1 && collector.Effects["Diamond_2|0"].conditionsMet==0,"Absent targets record a failed condition",passed);
            engine=Fixture();var ally=Unit(engine,"Heart_2",0);var buff=Hand(engine,"Club_2");collector=new BalanceMatchCollector(engine,3);
            engine.Summon(0,buff.InstanceID,1);if(engine.NeedsTarget)engine.SelectEffectTarget(0,0,0);
            engine.Attack(0,ally.InstanceID);
            Require(collector.Rows["0|Club_2"].buffExtraDamage==1,"Attack-buff source receives only realized additional damage",passed);
            engine=Fixture();var weakened=Unit(engine,"Heart_2",1);var weakening=Hand(engine,"Spade_2");collector=new BalanceMatchCollector(engine,4);
            engine.Summon(0,weakening.InstanceID,0);engine.SelectEffectTarget(0,1,0);engine.State.ActivePlayer=1;
            engine.Attack(1,weakened.InstanceID,0);
            Require(collector.Rows["0|Spade_2"].debuffPreventedDamage==1,"A fully suppressed zero-damage attack still credits its debuff",passed);
            engine=Fixture();var jokerUnit=Unit(engine,"Heart_2",0);var joker=Hand(engine,"Joker_1");collector=new BalanceMatchCollector(engine,5);engine.DebugNextGamble=true;engine.Gamble(0,joker.InstanceID,jokerUnit.InstanceID);
            Require(collector.Rows["0|Joker_1"].jokerSuccesses==1 && collector.Rows["0|Joker_1"].jokerAttackGain==1 && collector.Rows["0|Joker_1"].jokerHPGain==2,"Joker success keeps rounded actual stat gains",passed);
            engine=Fixture();jokerUnit=Unit(engine,"Heart_2",0);joker=Hand(engine,"Joker_2");collector=new BalanceMatchCollector(engine,6);engine.DebugNextGamble=false;engine.Gamble(0,joker.InstanceID,jokerUnit.InstanceID);
            Require(collector.Rows["0|Joker_2"].jokerFailures==1 && collector.Rows["0|Joker_2"].jokerLostHP==4,"Joker failure keeps actual lost HP and an independent outcome count",passed);
            engine=Fixture();var ace=Hand(engine,"Heart_A");var face=Hand(engine,"Club_K");collector=new BalanceMatchCollector(engine,7);engine.Combine(0,ace.InstanceID,face.InstanceID);
            Require(collector.Rows["0|Unknown_K"].appearances==1 && collector.Rows["0|Heart_A"].combinedAsMaterial==1,"UNKNOWN acquisition retains the real combination path",passed);
            engine=Fixture();var pair=Hand(engine,"Heart_5");engine.Summon(0,pair.InstanceID,0);while(engine.NeedsTarget)engine.SelectEffectTarget(0,0,0);pair=Hand(engine,"Spade_5");engine.Summon(0,pair.InstanceID,1);
            Require(engine.State.Players[0].Poker.kind==PokerKind.None && engine.State.Players[0].Board.Where(c=>c!=null).All(c=>c.PokerAttack==0 && c.PokerHP==0),"Existing EnablePoker switch disables only simulator poker grants",passed);
            var normal=new BattleEngine(42);Require(normal.Config.EnablePoker,"Ordinary game configuration keeps poker enabled",passed);
            // Both engines receive identical policies and seeds; telemetry must not affect rules/RNG.
            var plain=new BattleEngine(442,new BattleConfig {CardVersion=CardBalanceVersion.Baseline,EnablePoker=false,Map=BattleMap.JokerGame});
            var observed=new BattleEngine(442,new BattleConfig {CardVersion=CardBalanceVersion.Baseline,EnablePoker=false,Map=BattleMap.JokerGame});
            collector=new BalanceMatchCollector(observed,8);plain.DrainEvents();
            var agents=new[]{new BalancePolicy(BalanceAI.Effect,12),new BalancePolicy(BalanceAI.Control,18)};
            for(int step=0;step<300 && !plain.State.IsOver;step++) {
                int actor=plain.State.ActivePlayer;var observation=AiObservation.Capture(plain,actor);var moves=BalancePolicy.LegalMoves(observation);var move=agents[actor].Choose(observation,moves);
                collector.Command=move;
                if(!move.Execute(plain,actor) || !move.Execute(observed,actor))throw new InvalidOperationException("Parity action failed");collector.EndCommand();plain.DrainEvents();
                string left=JsonUtility.ToJson(BattleSnapshot.For(plain,actor,0,0,false,new BattleEvent[0]));
                string right=JsonUtility.ToJson(BattleSnapshot.For(observed,actor,0,0,false,new BattleEvent[0]));
                if(left!=right)throw new InvalidOperationException("Observer changed game state at step "+step);
            }
            Require(true,"Observed and unobserved engines preserve identical state and RNG across real commands",passed);
            using(var cancelled=new BalanceTestRun(new BalanceTestSettings{matches=10},parent)) {
                cancelled.Tick(1);cancelled.Stop();
                Require(cancelled.Finished && cancelled.State=="Stopped" && File.Exists(Path.Combine(cancelled.DirectoryPath,"test_metadata.json")),"Stopping exports partial results safely",passed);
            }
            using(var limited=new BalanceTestRun(new BalanceTestSettings{matches=2,maxActions=1},parent)) {
                while(!limited.Finished)limited.Tick(30);
                Require(limited.ValidMatches==0 && limited.Matches.All(m=>m.status=="unresolved" && m.winner==-1) && limited.Cards.Values.All(c=>c.uses==0),"Safety-limited matches never contaminate wins or card aggregates",passed);
            }
            var zero=BalanceCardReport.From(new BalanceCardStats{cardID="Joker_1"});
            Require(zero.manaEfficiencyStatus=="not_applicable_zero_paid_mana" && !double.IsNaN(zero.damagePerMana),"Zero paid mana is explicitly not applicable",passed);
            return passed;
        }
    }
}
#endif
