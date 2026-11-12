#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
namespace Unknown.Battle
{
    public static class FullReworkChecks
    {
        static readonly List<string> passed=new List<string>();
        static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);passed.Add(name);}
        static string Effects(CardDefinition c)=>string.Join("|",c.EffectList.Select(e=>e.Kind+":"+e.Amount+":"+e.Target+":"+e.FreshTarget));
        static BattleEngine Game(CardBalanceVersion v=CardBalanceVersion.FullBalanceRework,BattleMap m=BattleMap.Heart)=>new BattleEngine(718,new BattleConfig{StartingHand=0,StartingMana=10,EnablePoker=false,CardVersion=v,Map=m});
        static CardInstance Put(BattleEngine g,string id,int owner=0,int slot=0) {
            var c=g.NewCard(CardDatabase.Get(id,g.Config.CardVersion),owner);g.State.Players[owner].Slots[slot]=c;return c;
        }
        static CardInstance Summon(BattleEngine g,string id,int slot=0) {
            var c=g.NewCard(CardDatabase.Get(id,g.Config.CardVersion),0);g.State.Players[0].HandCards.Add(c);
            Check(g.Summon(0,c.InstanceID,slot),"Summon "+id);return c;
        }
        public static string[] Run() {
            passed.Clear();Check(CardDatabase.DefaultVersion==CardBalanceVersion.DiamondHeartRework && new BattleConfig().CardVersion==CardBalanceVersion.DiamondHeartRework,"Live default unchanged");
            var vv=new[]{CardBalanceVersion.FullStatsOnly,CardBalanceVersion.FullDiamondOnly,CardBalanceVersion.FullHeartMapOnly,CardBalanceVersion.FullBalanceRework};
            // Independent rank/suit specification, not read back from the factory.
            string[] ranks={"2","3","4","5","6","7","8","9","10","J","Q","K","A"};
            string[] body={"2/2 3/2 3/3 4/3 5/4 5/4 6/5 6/5 8/5 8/6 8/8 10/9 7/7","2/2 2/3 3/3 3/4 4/4 5/5 5/5 6/5 7/6 7/7 6/9 8/10 6/8","1/4 1/4 2/5 2/5 3/6 3/6 4/7 4/7 5/8 5/9 6/10 7/11 5/10","2/3 2/3 3/4 3/4 4/5 4/5 5/6 5/6 6/7 6/7 6/9 7/10 6/8"};
            int[] damage={2,3,4,3,6,4,6,6,8,9,6,10,5};
            foreach(var v in vv) {
                Check(CardDatabase.AllFor(v).Count==57,"57 cards "+v);
                foreach(var old in CardDatabase.AllFor(CardBalanceVersion.DiamondHeartRework)) {
                    var c=CardDatabase.Get(old.CardID,v);
                    Check(c.ManaCost==old.ManaCost && c.CardType==old.CardType && c.Suit==old.Suit && c.Rank==old.Rank,"Preserved identity/cost "+v+old.CardID);
                    bool stats=v==CardBalanceVersion.FullStatsOnly || v==CardBalanceVersion.FullBalanceRework;
                    if(stats && c.CardType==CardType.Normal) {
                        var expected=body[(int)c.Suit].Split(' ')[Array.IndexOf(ranks,CardDatabase.RankText(c.Rank))];
                        Check(c.Attack+"/"+c.MaxHP==expected,"Exact body "+v+c.CardID);
                    }else Check(c.Attack==old.Attack && c.MaxHP==old.MaxHP,"Unchanged body "+v+c.CardID);
                    if(c.Suit==Suit.Diamond && (v==CardBalanceVersion.FullDiamondOnly || v==CardBalanceVersion.FullBalanceRework)) {
                        Check(c.EffectList.Count==old.EffectList.Count,"Effect count "+v+c.CardID);
                        for(int i=0;i<c.EffectList.Count;i++) {
                            var e=c.EffectList[i];var effectBefore=old.EffectList[i];bool hit=e.Kind==EffectKind.Damage || e.Kind==EffectKind.ChainDamage || e.Kind==EffectKind.RandomDamage;
                            Check(e.Kind==effectBefore.Kind && e.Target==effectBefore.Target && e.FreshTarget==effectBefore.FreshTarget && e.Amount==(hit ? damage[Array.IndexOf(ranks,CardDatabase.RankText(c.Rank))] : effectBefore.Amount),"Exact effect "+v+c.CardID+i);
                        }
                    }else Check(Effects(c)==Effects(old) && c.Description==old.Description,"Preserved effect "+v+c.CardID);
                }
            }
            var g=Game();var unit=Summon(g,"Spade_2");Check(unit.MapHP==1 && unit.CurrentHP==unit.Definition.MaxHP+1,"All suits get bonus");
            var unknown=Summon(g,"Unknown_J",1);Check(unknown.MapHP==1,"UNKNOWN gets bonus");
            g=Game();var a=Put(g,"Heart_4");var b=Put(g,"Diamond_2",1);a.CurrentHP=4;b.Attack=2;a.Attack=1;
            Check(g.Attack(0,a.InstanceID,0) && a.CurrentHP==5 && a.HeartMapHealSpent && a.FirstAttackHealSpent,"Map heal and card recovery are independent");
            Check(b.CurrentHP==b.MaxHP && b.HeartMapHealSpent,"Defender first nonlethal hit heals");
            Check(CardViewData.From(a).ToCard().HeartMapHealSpent,"Spent flag serialization");
            a.HasAttacked=false;g.Attack(0,a.InstanceID,0);Check(a.CurrentHP==3,"No repeat heal on second hit");
            g=Game();a=Put(g,"Spade_2");b=Put(g,"Diamond_2",1);a.Attack=2;b.CurrentHP=2;b.Attack=0;
            g.Attack(0,a.InstanceID,0);Check(g.State.Players[1].Board[0]==null && b.HeartMapHealSpent,"Lethal hit never recovers");Check(!a.HeartMapHealSpent,"Zero damage does not consume first hit");
            g=Game();b=Put(g,"Heart_A",1);int hp=b.CurrentHP;Summon(g,"Diamond_4");Check(b.CurrentHP==hp-3 && b.HeartMapHealSpent,"Effect damage immediately recovers once");
            g=Game();a=Summon(g,"Diamond_3");Check(a.HeartMapHealSpent && a.CurrentHP==a.MaxHP,"Self damage counts as first damage");
            g=Game();g.State.Players[0].HP=20;g.EndTurn(0);g.EndTurn(1);Check(g.State.Players[0].HP==20 && g.Config.MapRules.Single(d=>d.map==BattleMap.Heart).turnHeal==0,"No periodic hero recovery");
            g=Game(CardBalanceVersion.DiamondHeartRework);g.State.Players[0].HP=20;g.EndTurn(0);g.EndTurn(1);Check(g.State.Players[0].HP==22,"Reference heart map untouched");
            g=Game(CardBalanceVersion.FullBalanceRework,BattleMap.Normal);a=Put(g,"Heart_A");b=Put(g,"Diamond_2",1);int prior=a.CurrentHP;g.Attack(0,a.InstanceID,0);Check(a.CurrentHP==prior-2 && !a.HeartMapHealSpent,"Other maps do not recover");
            foreach(int targets in new[]{1,2}) {
                g=Game(CardBalanceVersion.FullBalanceRework,BattleMap.Normal);var x=Put(g,"Heart_A",1);CardInstance y=targets==2 ? Put(g,"Heart_K",1,1) : null;
                int xhp=x.CurrentHP,yhp=y?.CurrentHP ?? 0;Summon(g,"Diamond_7");g.SelectEffectTarget(0,1,0);
                if(targets==2)g.SelectEffectTarget(0,1,1);
                Check(!g.NeedsTarget && x.CurrentHP==xhp-4 && (y==null || y.CurrentHP==yhp-4),"Chain distinct count "+targets);
            }
            return passed.ToArray();
        }
    }
}
#endif
