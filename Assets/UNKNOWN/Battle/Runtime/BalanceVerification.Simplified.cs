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
        [Serializable] sealed class SimpleExpected {public SimpleCard[] cards;}
        [Serializable] sealed class SimpleCard {public string id,description;public int mana,attack,hp;public BalanceEffectDefinition[] effects;}
        sealed class SimpleUnit {
            public CardInstance card;public int attack,hp,max,protection,attackTurns,healthTurns;
            public SimpleUnit(CardInstance c){card=c;attack=c.Attack;hp=c.CurrentHP;max=c.MaxHP;protection=c.Protection;}
        }
        // Explicit expected definitions come from the user's table, not the runtime catalog.
        public static string[] VerifySimplifiedCards(string expectedPath)
        {
            var passed=new List<string>();var expected=JsonUtility.FromJson<SimpleExpected>(File.ReadAllText(expectedPath));
            Require(expected.cards.Length==52 && CardDatabase.All.Count(c=>c.CardType==CardType.Normal)==52,"52 live normal definitions",passed);
            foreach(var d in expected.cards){
                var definition=CardDatabase.Get(d.id);
                Require(definition.Version==CardBalanceVersion.FullBalanceRework && definition.ManaCost==d.mana && definition.Attack==d.attack && definition.MaxHP==d.hp && definition.Description==d.description,"Live stats and description: "+d.id,passed);
                var actual=BalanceDefinition.From(definition).effects;
                Require(actual.Length==d.effects.Length && actual.Zip(d.effects,(a,b)=>a.kind==b.kind && a.amount==b.amount && a.target==b.target && a.duration==b.duration && a.freshTarget==b.freshTarget).All(v=>v),"Exact on-placement effects: "+d.id,passed);
                var g=CurrentFixture();g.State.ActivePlayer=0;g.State.Players[0].CurrentMana=10;
                var ally=CurrentUnit(g,"Heart_K");ally.CurrentHP=3;ally.Protection=1;
                var other=CurrentUnit(g,"Heart_J",0,1);other.CurrentHP=4;
                var enemy=CurrentUnit(g,"Unknown_K",1);enemy.MaxHP=40;enemy.CurrentHP=30;enemy.Protection=3;
                var enemy2=CurrentUnit(g,"Unknown_Q",1,1);enemy2.MaxHP=40;enemy2.CurrentHP=30;enemy2.Protection=3;
                CurrentHand(g,"Heart_2");CurrentHand(g,"Spade_2");var source=CurrentHand(g,d.id);
                var units=new[]{ally,other,enemy,enemy2,source}.Select(c=>new SimpleUnit(c)).ToArray();
                int hand=2,mana=10-d.mana;
                foreach(var effect in d.effects){
                    if(effect.kind=="DrawCard"){hand=Math.Min(8,hand+effect.amount);continue;}
                    if(effect.kind=="GainMana"){mana=Math.Min(10,mana+Math.Min(2,effect.amount));continue;}
                    if(effect.kind=="TacticalExchange"){hand=hand-(definition.Rank==Rank.Queen?2:1)+effect.amount;continue;}
                    IEnumerable<SimpleUnit> targets=effect.target=="AllAllies" ? units.Where(u=>u.card.Owner==0) : effect.target=="AllEnemies" ? units.Where(u=>u.card.Owner==1) : new[]{effect.target=="Ally"?units[0]:units[2]};
                    foreach(var target in targets)switch(effect.kind){
                        case "RemoveProtection":target.protection=0;break;
                        case "Damage":int blocked=Math.Min(target.protection,effect.amount);target.hp-=effect.amount-blocked;target.protection=0;break;
                        case "ModifyAttack":target.attack+=effect.amount;break;
                        case "Heal":target.hp=Math.Min(target.max,target.hp+effect.amount);break;
                        case "IncreaseMaxHP":target.max+=effect.amount;target.hp+=effect.amount;break;
                        case "Protection":target.protection=Math.Max(target.protection,effect.amount);break;
                        case "CardWeaken":target.attack=Math.Max(0,target.attack-Math.Min(3,effect.amount));target.attackTurns=effect.duration;break;
                        case "HealthWeaken":int n=Math.Min(effect.amount,Math.Min(3,target.max-1));target.max-=n;target.hp=Math.Max(1,target.hp-n);target.healthTurns=effect.duration;break;
                        default:throw new InvalidOperationException("Unexpected simple effect "+effect.kind);
                    }
                }
                Require(g.Summon(0,source.InstanceID,2),"Summon allowed: "+d.id,passed);
                for(int step=0;g.NeedsTarget && step<20;step++){
                    if(g.NeedsExchange){Require(g.ChooseExchange(0,g.State.Players[0].Hand[0].InstanceID),"Existing exchange selection: "+d.id,passed);continue;}
                    bool chosen=false;for(int owner=0;owner<2 && !chosen;owner++)for(int slot=0;slot<5 && !chosen;slot++)if(g.IsValidEffectTarget(owner,slot)){g.SelectEffectTarget(0,owner,slot);chosen=true;}
                    Require(chosen,"Target can resolve: "+d.id,passed);
                }
                Require(!g.NeedsTarget && g.State.Players[0].Hand.Count==hand && g.State.Players[0].CurrentMana==mana,"Draw, mana and target continuation: "+d.id,passed);
                Require(units.All(u=>u.card.Attack==u.attack && u.card.CurrentHP==u.hp && u.card.MaxHP==u.max && u.card.Protection==u.protection && u.card.AttackWeakTurns==u.attackTurns && u.card.HealthWeakTurns==u.healthTurns),"Actual battlefield effects match specification: "+d.id,passed);
                Require(!CardDisplayText.For(source).Contains("공격 시") && !CardDisplayText.For(source).Contains("처치 시") && !CardDisplayText.For(source).Contains("턴 시작"),"Display uses placement timing: "+d.id,passed);
            }
            foreach(string id in new[]{"Spade_2","Spade_7","Spade_Q","Spade_A"}){
                var g=CurrentFixture();g.State.ActivePlayer=0;var t=CurrentUnit(g,"Unknown_K",1);int atk=t.Attack,hp=t.CurrentHP,max=t.MaxHP;
                PlayCurrent(g,id,0,1);int turns=Math.Max(t.AttackWeakTurns,t.HealthWeakTurns);int weakHP=t.HealthWeakening;
                t.CurrentHP-=2;int injured=t.CurrentHP;g.EndTurn(0);
                Require(Math.Max(t.AttackWeakTurns,t.HealthWeakTurns)==turns,"Non-owner end does not consume duration: "+id,passed);
                for(int i=0;i<turns;i++){
                    g.EndTurn(1);
                    Require(Math.Max(t.AttackWeakTurns,t.HealthWeakTurns)==turns-i-1,"Target-owner duration: "+id+" step "+i,passed);
                    if(i<turns-1)g.EndTurn(0);
                }
                Require(t.Attack==atk && t.MaxHP==max && t.CurrentHP==injured+weakHP && t.CurrentHP==hp-2,"Expiry restores only weakened stats, not combat wounds: "+id,passed);
            }
            {
                var g=CurrentFixture();g.State.ActivePlayer=0;var t=CurrentUnit(g,"Unknown_K",1);int attack=t.Attack;
                PlayCurrent(g,"Spade_K",0,1);g.State.Players[0].CurrentMana=10;PlayCurrent(g,"Spade_6",1,1);
                Require(t.CardWeakening==3 && t.HealthWeakening==3 && t.AttackWeakTurns==3 && t.HealthWeakTurns==3,"Independent weakening caps and max duration",passed);
                var snap=JsonUtility.FromJson<BattleSnapshot>(JsonUtility.ToJson(BattleSnapshot.For(g,0,0,0,false,new BattleEvent[0])));var copy=BattleEngine.CreateView(snap).State.Players[1].Board[0];
                Require(copy.CardWeakening==3 && copy.HealthWeakening==3 && copy.AttackWeakTurns==3 && copy.HealthWeakTurns==3 && copy.HealthWeakCurrent==t.HealthWeakCurrent,"Network snapshot preserves both debuffs and HP ledger",passed);
                g.State.ActivePlayer=1;g.State.Players[1].CurrentMana=10;PlayCurrent(g,"Diamond_5",1,1);
                Require(t.Attack==attack-1 && t.AbilityAttack==-1,"Permanent attack gain remains separate from weakening",passed);
            }
            {
                var g=CurrentFixture();g.State.ActivePlayer=0;var t=CurrentUnit(g,"Heart_2",1);t.CurrentHP=1;PlayCurrent(g,"Spade_K",0,1);
                Require(t.CurrentHP==1 && g.State.Players[1].Board.Contains(t),"HP weakening never kills by itself",passed);
            }
            foreach(string id in new[]{"Club_4","Club_9"}){
                var g=CurrentFixture();g.State.ActivePlayer=0;PlayCurrent(g,id,0);
                Require(!g.NeedsTarget && g.State.Players[0].Hand.Count==(id=="Club_4"?2:3),"Empty-hand discard card still draws stated count: "+id,passed);
            }
            for(int choose=0;choose<=2;choose++){
                var g=CurrentFixture();g.State.ActivePlayer=0;CurrentHand(g,"Heart_2");CurrentHand(g,"Spade_2");PlayCurrent(g,"Club_Q",0);
                for(int i=0;i<choose;i++)g.ChooseExchange(0,g.State.Players[0].Hand[0].InstanceID);
                if(g.NeedsExchange)g.ChooseExchange(0,-1);
                Require(!g.NeedsTarget && g.State.Players[0].Hand.Count==5-choose && g.State.Players[0].DiscardCount==choose,"Club Q fixed three draws after "+choose+" discards",passed);
            }
            {
                var g=CurrentFixture();g.State.ActivePlayer=0;
                PlayCurrent(g,"Club_3",0);PlayCurrent(g,"Club_5",1);PlayCurrent(g,"Club_3",2);
                Require(g.State.Players[0].CardManaGained==2 && g.State.Players[0].CurrentMana==8,"Card mana limited to +2 each turn",passed);
                g.EndTurn(0);g.EndTurn(1);Require(g.State.Players[0].CardManaGained==0,"Card mana allowance resets on owner's next turn",passed);
            }
            {
                var g=CurrentFixture();g.State.ActivePlayer=0;for(int i=0;i<7;i++)CurrentHand(g,"Heart_2");int deck=g.State.Players[0].DeckCount;
                PlayCurrent(g,"Club_6",0);Require(g.State.Players[0].Hand.Count==8 && g.State.Players[0].DeckCount==deck-2 && g.State.Players[0].DiscardCount==1,"Existing eight-card cap and overflow discard retained",passed);
            }
            {
                var data=SingleChapterData.Load();var stage=data.stages.First(s=>s.kind==SingleNodeKind.Battle);
                var options=new SingleBattleOptions{stage=stage,difficulty=data.difficulties[0],deck=data.starters[0].cards,augments=new[]{"2"},augmentData=data.augments,hp=30,maxHP=30,seed=1711};
                var g=new BattleEngine(1711,new BattleConfig{StartingHand=0,StartingMana=10,EnablePoker=false,Single=options},new SingleRandom(1711));g.State.ActivePlayer=0;g.State.Players[0].HandCards.Clear();g.State.Players[0].CurrentMana=10;
                var t=CurrentUnit(g,"Unknown_K",1);PlayCurrent(g,"Spade_4",0,1);
                Require(t.AttackWeakTurns==3,"Existing single-player augment extends timed weakening",passed);
                var restored=BattleEngine.CreateSingle(options,g.SaveSingle());Require(restored.State.Players[1].Board[0].AttackWeakTurns==3,"Single battle save preserves weakening",passed);
            }
            foreach(var rank in new[]{"J","Q","K"}){
                var g=CurrentFixture();g.State.ActivePlayer=0;var ace=CurrentHand(g,"Diamond_A");var face=CurrentHand(g,"Heart_"+rank);
                Require(g.CombinationPreview(0,ace.InstanceID,face.InstanceID)?.CardID=="Unknown_"+rank && g.Combine(0,ace.InstanceID,face.InstanceID),"Hand A+face UNKNOWN combination unchanged: "+rank,passed);
            }
            return passed.ToArray();
        }
    }
}
#endif
