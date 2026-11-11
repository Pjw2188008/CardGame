#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace Unknown.Battle
{
    public static class BalanceVerification
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
