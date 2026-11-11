#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace Unknown.Battle
{
    public static class CardRedesignChecks
    {
        public static readonly List<string> Passed=new List<string>();
        static void Check(bool condition,string name){if(!condition)throw new InvalidOperationException(name);Passed.Add(name);}
        static BattleEngine Fixture(BattleMap map=BattleMap.Normal,CardBalanceVersion version=CardBalanceVersion.Revised)
        {
            var g=new BattleEngine(421,new BattleConfig{StartingHand=0,StartingMana=10,EnablePoker=false,Map=map,CardVersion=version});
            foreach(var p in g.State.Players){p.HandCards.Clear();p.DiscardCards.Clear();}g.DrainEvents();return g;
        }
        static CardInstance Hand(BattleEngine g,string name,int owner=0){var c=g.NewCard(CardDatabase.Get(name,g.Config.CardVersion),owner);g.State.Players[owner].HandCards.Add(c);return c;}
        static CardInstance Unit(BattleEngine g,string name,int owner=0,int slot=0){var c=g.NewCard(CardDatabase.Get(name,g.Config.CardVersion),owner);g.State.Players[owner].Slots[slot]=c;return c;}
        static string Key(CardEffect e)=>e.Kind+":"+e.Amount+":"+e.Target+(e.FreshTarget ? "*" : "");
        public static void VerifyCatalog(string directory)
        {
            string[] ranks={"2","3","4","5","6","7","8","9","10","A","J","Q","K"};
            string[][] stats={
                new[]{"1/2/2","1/3/2","2/3/3","2/4/3","3/4/4","3/5/4","4/5/5","4/6/5","5/7/5","7/6/7","6/7/6","7/7/8","8/9/9"},
                new[]{"1/2/2","1/2/3","2/3/3","2/3/4","3/4/4","3/4/5","4/5/5","4/5/5","5/6/6","6/5/8","6/6/7","6/5/9","8/7/10"},
                new[]{"1/1/4","1/1/4","2/2/5","2/2/5","3/2/6","3/3/6","4/3/7","4/3/7","5/4/8","7/4/10","6/4/9","7/5/10","8/6/11"},
                new[]{"1/1/3","1/2/3","2/2/4","2/3/4","3/3/5","3/3/5","4/4/6","4/4/6","5/5/7","7/4/8","6/5/7","7/5/9","8/6/10"}};
            string[][] effects={
                new[]{"Damage:1:Enemy","SelfDamage:1:Self|Damage:2:Enemy","RandomDamage:3:Enemy","Damage:2:Enemy","SelfDamage:1:Self|Damage:4:Enemy","ChainDamage:2:Enemy","RandomDamage:4:Enemy","Damage:4:Enemy|ModifyAttack:1:Self","Damage:5:Enemy","ModifyAttack:2:AllAllies|Damage:2:Enemy","SelfDamage:2:Self|Damage:7:Enemy","ChainDamage:4:Enemy","Damage:7:Enemy|ModifyAttack:2:Self"},
                new[]{"ModifyAttack:-1:Enemy","ModifyAttack:-1:Enemy|Damage:1:Enemy","ChainWeaken:1:Enemy","ModifyAttack:-2:Enemy","ModifyAttack:-2:Enemy|Damage:1:Enemy","DiscardCard:1:None","ModifyAttack:-3:Enemy","ModifyAttack:-2:Enemy|Damage:2:Enemy","DiscardCard:1:None|ModifyAttack:-2:Enemy","ModifyAttack:-2:AllEnemies|DiscardCard:1:None","ModifyAttack:-4:Enemy|Damage:3:Enemy","DiscardCard:2:None|ModifyAttack:-1:AllEnemies","Damage:4:Enemy|ModifyAttack:-4:Enemy*"},
                new[]{"Heal:2:Ally","IncreaseMaxHP:2:Self","FirstAttackHeal:3:Self","IncreaseMaxHP:3:Ally","Heal:2:AllAllies","Heal:3:Ally|FirstAttackHeal:2:Self","IncreaseMaxHP:4:Ally","Heal:3:AllAllies","Heal:2:AllAllies|FirstAttackHeal:3:Self","IncreaseMaxHP:2:AllAllies|Heal:3:AllAllies","IncreaseMaxHP:3:Ally|FirstAttackHeal:4:Self","IncreaseMaxHP:3:AllAllies|Heal:3:AllAllies","Heal:4:AllAllies|FirstAttackHeal:5:Self"},
                new[]{"Exchange:1:None","GainMana:1:None","DrawCard:1:None","Exchange:2:None","DrawCard:1:None|GainMana:1:None","DrawCard:2:None","DrawCard:1:None|GainMana:1:None","DrawCard:2:None|GainMana:1:None","DrawCard:2:None|GainMana:1:None","DrawCard:2:None|GainMana:2:None","Exchange:2:None|GainMana:2:None","DrawCard:3:None|GainMana:1:None","DrawCard:3:None|GainMana:2:None"}};
            var table=new List<string>{"# 카드 57종 적용 대조표","","첫 개편 수치이며 최적값으로 검증된 수치가 아니다.","","|카드|기준 마나/공격/HP|개편 마나/공격/HP|기준 효과|개편 효과|","|---|---|---|---|---|"};
            for(int suit=0;suit<4;suit++)for(int rank=0;rank<13;rank++) {
                string id=((Suit)suit)+"_"+ranks[rank];var c=CardDatabase.Get(id,CardBalanceVersion.Revised);
                Check(c.ManaCost+"/"+c.Attack+"/"+c.MaxHP==stats[suit][rank] && string.Join("|",c.EffectList.Select(Key))==effects[suit][rank],"Specification data and effect order "+id);
            }
            foreach(string id in new[]{"Unknown_J","Unknown_Q","Unknown_K","Joker_1","Joker_2"}) {
                var before=CardDatabase.Get(id,CardBalanceVersion.Baseline);var after=CardDatabase.Get(id,CardBalanceVersion.Revised);
                Check(before.ManaCost==after.ManaCost && before.Attack==after.Attack && before.MaxHP==after.MaxHP && string.Join("|",before.EffectList.Select(Key))==string.Join("|",after.EffectList.Select(Key)),"Preserved UNKNOWN/Joker "+id);
            }
            foreach(var c in CardDatabase.AllFor(CardBalanceVersion.Revised).OrderBy(c=>c.Suit).ThenBy(c=>c.Rank).ThenBy(c=>c.CardID)) {
                var old=CardDatabase.Get(c.CardID,CardBalanceVersion.Baseline);
                table.Add("|"+c.CardID+"|"+old.ManaCost+"/"+old.Attack+"/"+old.MaxHP+"|"+c.ManaCost+"/"+c.Attack+"/"+c.MaxHP+"|"+old.Description.Replace("|","/")+"|"+c.Description.Replace("|","/")+"|");
            }
            Directory.CreateDirectory(directory);File.WriteAllLines(Path.Combine(directory,"CardComparison57.md"),table);
        }
        public static void Run(string directory)
        {
            Passed.Clear();VerifyCatalog(directory);
            var g=Fixture();var heart=Unit(g,"Heart_4");heart.CurrentHP=3;var enemy=Unit(g,"Spade_2",1);
            Check(g.Attack(0,heart.InstanceID,0) && heart.CurrentHP==4 && heart.FirstAttackHealSpent,"First active attack heals only after simultaneous counter damage");
            heart.HasAttacked=false;heart.CurrentHP=1;Check(g.Attack(0,heart.InstanceID) && heart.CurrentHP==1,"Second active attack never repeats recovery");
            g=Fixture();heart=Unit(g,"Heart_4");enemy=Unit(g,"Club_2",1);enemy.Attack=0;g.Attack(0,heart.InstanceID,0);
            Check(heart.CurrentHP==5 && heart.FirstAttackHealSpent && g.DrainEvents().Any(e=>e.Kind==BattleEventKind.Heal && e.Amount==0),"Full HP consumes recovery with an explicit zero result");
            g=Fixture();heart=Unit(g,"Heart_4");heart.CurrentHP=2;enemy=Unit(g,"Spade_2",1);g.Attack(0,heart.InstanceID,0);
            Check(g.State.Players[0].Board[0]==null && !g.DrainEvents().Any(e=>e.Kind==BattleEventKind.Heal),"Lethal counterattack cannot heal or resurrect");
            g=Fixture();heart=Unit(g,"Heart_4");enemy=Unit(g,"Heart_K",1);enemy.Attack=1;g.State.ActivePlayer=1;g.Attack(1,enemy.InstanceID,0);
            Check(!heart.FirstAttackHealSpent && heart.CurrentHP==4,"Defending counterattack does not consume recovery");
            g=Fixture();heart=Unit(g,"Heart_4");heart.CurrentHP=2;g.Attack(0,heart.InstanceID);Check(heart.CurrentHP==5,"Nonlethal direct attack can recover");
            Check(CardViewData.From(heart).ToCard().FirstAttackHealSpent,"Recovery spent state roundtrips card serialization");
            g=Fixture();heart=Unit(g,"Heart_4");heart.CurrentHP=2;g.State.Players[1].HP=1;g.Attack(0,heart.InstanceID);
            Check(g.State.IsOver && heart.CurrentHP==2,"Winning direct attack ends before recovery");
            foreach(bool lethal in new[]{false,true}) {
                g=Fixture();var first=Unit(g,"Heart_K",1);first.CurrentHP=lethal ? 4 : 10;var other=Unit(g,"Heart_K",1,1);var king=Hand(g,"Spade_K");g.Summon(0,king.InstanceID,0);g.SelectEffectTarget(0,1,0);
                Check(g.NeedsTarget && g.IsValidEffectTarget(1,1),"Spade K requests an independent living target after "+(lethal ? "lethal" : "nonlethal")+" damage");
                g.SelectEffectTarget(0,1,1);Check(other.Attack==2 && !g.NeedsTarget,"Independent Spade K weakening finishes");
            }
            g=Fixture();enemy=Unit(g,"Heart_K",1);enemy.CurrentHP=4;var k=Hand(g,"Spade_K");g.Summon(0,k.InstanceID,0);g.SelectEffectTarget(0,1,0);Check(!g.NeedsTarget,"Spade K finishes when damage leaves no enemy");
            g=Fixture();enemy=Unit(g,"Heart_K",1);enemy.Attack=0;k=Hand(g,"Spade_K");g.Summon(0,k.InstanceID,0);g.SelectEffectTarget(0,1,0);g.SelectEffectTarget(0,1,0);Check(!g.NeedsTarget && enemy.Attack==0,"Spade K may reselect a survivor at the attack floor");
            foreach(string id in new[]{"Spade_4","Diamond_7","Diamond_Q"})foreach(int count in new[]{0,1,3}) {
                g=Fixture();for(int i=0;i<count;i++)Unit(g,"Unknown_K",1,i);var multi=Hand(g,id);g.Summon(0,multi.InstanceID,0);
                if(count>0){Check(g.SelectEffectTarget(0,1,0),"First distinct target "+id+" / "+count);if(count>1){Check(!g.SelectEffectTarget(0,1,0),"Distinct target rejects duplicate "+id);g.SelectEffectTarget(0,1,1);}}
                Check(!g.NeedsTarget,"Distinct effect resolves zero, one or two enemies "+id+" / "+count);
            }
            foreach(int mana in new[]{0,9,10}) {
                g=Fixture();var refund=Hand(g,"Club_J");Hand(g,"Heart_2");g.Summon(0,refund.InstanceID,0);g.State.Players[0].CurrentMana=mana;g.State.Players[0].MaxMana=3;g.ChooseExchange(0,-1);
                Check(g.State.Players[0].CurrentMana==Math.Min(10,mana+2) && g.State.Players[0].MaxMana==3,"Current mana cap and independent growth at "+mana);
                g.EndTurn(0);g.EndTurn(1);Check(g.State.Players[0].CurrentMana==4,"Next own turn recharges from grown max mana");
            }
            g=Fixture();g.State.Players[0].CurrentMana=1;var free=Hand(g,"Club_3");Check(g.Summon(0,free.InstanceID,0) && g.State.Players[0].CurrentMana==1,"Club 3 pays one before returning one");
            Check(!g.Summon(0,free.InstanceID,1) && g.State.Players[0].CurrentMana==1,"Duplicate summon cannot grant mana again");
            foreach(int deck in new[]{0,1,4})foreach(bool cancel in new[]{false,true}) {
                g=Fixture();var exchange=Hand(g,"Club_J");var material=Hand(g,"Heart_2");g.State.Players[0].DeckCards.RemoveRange(deck,g.State.Players[0].DeckCards.Count-deck);g.Summon(0,exchange.InstanceID,0);
                var wire=JsonUtility.FromJson<BattleSnapshot>(JsonUtility.ToJson(BattleSnapshot.For(g,0,0,0,false,new BattleEvent[0])));
                Check(BattleEngine.CreateView(wire).NeedsExchange && AiObservation.Capture(g,0).Simulation(0).NeedsExchange,"Pending exchange restores in client and AI state");
                Check(g.ChooseExchange(0,cancel ? -1 : material.InstanceID),"Exchange choice accepted");
                Check(g.State.Players[0].Hand.Count==(cancel ? 1 : Math.Min(2,deck)) && g.State.Players[0].CurrentMana==6 && !g.State.Players[0].ManualDiscardUsed,"Exchange actual discard, capped draw and following mana / "+deck+" / "+cancel);
                Check(!g.ChooseExchange(0,material.InstanceID) && !g.NeedsTarget,"Exchange cannot be replayed");
            }
            g=Fixture();var noHand=Hand(g,"Club_J");g.Summon(0,noHand.InstanceID,0);Check(!g.NeedsTarget && g.State.Players[0].CurrentMana==6,"Empty-hand exchange skips only its own draw");
            g=Fixture();var timed=Hand(g,"Club_J");Hand(g,"Heart_2");g.Summon(0,timed.InstanceID,0);g.ExpireTurn(0);Check(!g.NeedsTarget && g.State.Players[0].CurrentMana==6 && g.FinishExpiredTurn(0),"Timeout skips exchange and completes follow-up before turn change");
            g=Fixture();var hp=Unit(g,"Heart_2");hp.CurrentHP=1;var queen=Hand(g,"Heart_Q");g.Summon(0,queen.InstanceID,1);
            Check(hp.MaxHP==7 && hp.CurrentHP==7 && queen.MaxHP==13 && queen.CurrentHP==13,"Max/current HP increase includes caster and healing remains separate");
            g=Fixture();heart=Unit(g,"Heart_2");heart.CurrentHP=1;var joker=Hand(g,"Joker_1");g.DebugNextGamble=true;g.Gamble(0,joker.InstanceID,heart.InstanceID);
            Check(heart.Attack==2 && heart.MaxHP==6 && heart.CurrentHP==3,"Joker keeps ceiling and adds only max HP delta to current HP");
            var second=Hand(g,"Joker_2");Check(!g.Gamble(0,second.InstanceID,heart.InstanceID),"Joker remains once per unit");
            foreach(BattleMap map in new[]{BattleMap.Normal,BattleMap.JokerGame}) {
                g=Fixture(map);Unit(g,"Unknown_J",0,0);Unit(g,"Unknown_Q",0,1);var third=Hand(g,"Unknown_K");g.Summon(0,third.InstanceID,2);
                Check(g.State.IsOver==(map==BattleMap.Normal),"UNKNOWN three living units victory and Joker map exception");
            }
            g=Fixture(BattleMap.Heart);g.State.Players[0].HP=20;heart=Unit(g,"Heart_4");heart.CurrentHP=2;g.EndTurn(0);g.EndTurn(1);var mapEvents=g.DrainEvents();g.Attack(0,heart.InstanceID);var afterEvents=g.DrainEvents();
            Check(mapEvents.Any(e=>e.Kind==BattleEventKind.HeroChanged && e.SourceID<0) && afterEvents.Any(e=>e.Kind==BattleEventKind.Heal && e.SourceID==heart.InstanceID && e.Secondary==1),"Map hero recovery and first-attack unit recovery retain separate sources");
            foreach(int handSize in new[]{7,8})foreach(int deckSize in new[]{0,1,4}) {
                g=Fixture();var draw=Hand(g,"Club_Q");for(int i=1;i<handSize;i++)Hand(g,"Heart_2");
                g.State.Players[0].DeckCards.RemoveRange(deckSize,g.State.Players[0].DeckCards.Count-deckSize);g.Summon(0,draw.InstanceID,0);
                int drawn=Math.Min(3,deckSize),accepted=Math.Min(drawn,9-handSize);
                Check(g.State.Players[0].Hand.Count==handSize-1+accepted && g.State.Players[0].DiscardCount==drawn-accepted && g.State.Players[0].CurrentMana==4,"Draw respects existing hand limit, overflow, empty/one-card deck / "+handSize+" / "+deckSize);
            }
            g=Fixture();var a=Hand(g,"Heart_A");var f=Hand(g,"Heart_J");for(int i=0;i<3;i++)Hand(g,"Unknown_J");
            Check(!g.Combine(0,a.InstanceID,f.InstanceID) && g.State.Players[0].Hand.Count==5,"UNKNOWN holding cap preserves both materials on rejection");
            g.State.Players[0].HandCards.RemoveAt(4);Check(g.Combine(0,a.InstanceID,f.InstanceID) && g.State.Players[0].UnknownHeld==3 && !g.State.IsOver,"UNKNOWN consumes two materials and only field presence can win");
            g=Fixture();Unit(g,"Unknown_J",0,0);Unit(g,"Unknown_Q",0,1);var dead=Unit(g,"Unknown_K",0,2);dead.CurrentHP=0;g.ReconcilePoker();Check(g.State.Players[0].UnknownProgress==2 && !g.State.IsOver,"Destroyed UNKNOWN contributes no field victory stack");
            g=Fixture();var paidTarget=Unit(g,"Heart_2");var paidJoker=Hand(g,"Joker_1");var tracker=new BalanceMatchCollector(g,-1);tracker.Command=new AiMove(BattleAction.Joker,paidJoker.InstanceID,paidTarget.InstanceID);g.DebugNextGamble=false;g.Gamble(0,paidJoker.InstanceID,paidTarget.InstanceID);
            Check(tracker.Rows["0|Joker_1"].jokerPaidUses==1 && tracker.Rows["0|Joker_1"].manaPaid==2 && g.State.Players[0].Board[0]==null,"Paid failed Joker records actual cost and lethal loss");
            g=Fixture(BattleMap.JokerGame);g.ResetMapEventForDebug();var freeTarget=Unit(g,"Heart_2");Hand(g,"Joker_1");tracker=new BalanceMatchCollector(g,-1);g.DebugNextGamble=true;g.ResolveMapEvent(0,JokerEventDefinition.Defaults().Single(e=>e.kind==JokerEventKind.ForcedGamble));
            Check(tracker.Rows["0|Joker_1"].jokerFreeUses==1 && tracker.Rows["0|Joker_1"].manaPaid==0 && g.State.Players[0].CurrentMana==10,"Forced map Joker records free success separately");
            g=Fixture();Unit(g,"Heart_K",1,0);Unit(g,"Heart_K",1,1);var distinct=Hand(g,"Spade_4");g.Summon(0,distinct.InstanceID,0);g.SelectEffectTarget(0,1,0);
            var pending=AiObservation.Capture(g,0).Simulation(0);Check(pending.NeedsTarget && !pending.IsValidEffectTarget(1,0) && pending.IsValidEffectTarget(1,1),"Pending distinct-selection history restores without duplicate targets");
            g.ExpireTurn(0);Check(!g.NeedsTarget && g.FinishExpiredTurn(0),"Timeout resolves outstanding distinct targets and releases the turn");
            var authority=new BattleAuthority(77,new BattleConfig{StartingHand=0,StartingMana=10,EnablePoker=false});g=authority.Engine;foreach(var player in g.State.Players)player.HandCards.Clear();var committed=Hand(g,"Club_J");Hand(g,"Heart_2");
            Check(authority.Execute(0,new BattleIntent{sequence=1,revision=0,turn=g.State.TurnNumber,action=BattleAction.Summon,first=committed.InstanceID,second=0}),"Authority commits exchange summon once");
            var skip=new BattleIntent{sequence=2,revision=1,turn=g.State.TurnNumber,action=BattleAction.Exchange,first=-1};Check(authority.Execute(0,skip) && !authority.Execute(0,skip) && g.State.Players[0].CurrentMana==6,"Authority sequence/revision guards prevent replayed exchange refund");
            g=Fixture();foreach(var player in g.State.Players)player.DeckCards.Clear();Check(BalanceTestRun.IsIrreversibleExhaustion(g),"Empty no-fatigue terminal state is classified without awarding a winner");
            Hand(g,"Heart_2");Check(!BalanceTestRun.IsIrreversibleExhaustion(g),"A remaining summonable card prevents exhaustion classification");
            g=Fixture(BattleMap.JokerGame);foreach(var player in g.State.Players)player.DeckCards.Clear();var zero=Unit(g,"Club_2");zero.Attack=0;Check(!BalanceTestRun.IsIrreversibleExhaustion(g),"Joker map can recover a zero-attack unit, so testing continues");
            foreach(BattleMap map in Enum.GetValues(typeof(BattleMap))) {
                g=Fixture(map);var c=Hand(g,"Club_3");g.Summon(0,c.InstanceID,0);
                Check(!g.NeedsTarget && c.Definition.Version==CardBalanceVersion.Revised,"All map interactions resolve new mana source / "+map);
            }
        }
    }
}
#endif
