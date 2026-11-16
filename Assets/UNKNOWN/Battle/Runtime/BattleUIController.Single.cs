using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    public sealed partial class BattleUIController
    {
        SingleCampaign singleCampaign;Action singleReturn;bool singleFinished;Text singleStatus,singleIntent;
        public void ConfigureSingle(SingleCampaign campaign,Action onReturn) {
            singleCampaign=campaign;singleReturn=onReturn;aiMode=true;revealHands=false;turnDuration=86400;
            returned=()=>{singleReturn?.Invoke();};
        }
        BattleEngine MakeSingleEngine() {
            var run=singleCampaign.Run;
            var options=new SingleBattleOptions{stage=singleCampaign.Stage,augmentData=singleCampaign.Data.augments,difficulty=singleCampaign.Data.difficulties[run.difficulty],deck=run.deck.ToArray(),augments=run.augments.ToArray(),hp=run.hp,maxHP=run.maxHP,seed=unchecked(run.seed+run.current*104729),tutorial=singleCampaign.IsTeachingBattle};
            var g=BattleEngine.CreateSingle(options,run.battle);localMap=options.stage.map;run.phase="battle";singleFinished=false;return g;
        }
        void SingleCheckpoint() {
            if(singleCampaign==null || engine==null || singleFinished)return;
            engine.Single.Patterns();singleCampaign.Run.battle=engine.SaveSingle();
            foreach(string id in engine.Single.Progress.usedCards)if(!singleCampaign.Run.usedCards.Contains(id))singleCampaign.Run.usedCards.Add(id);
            singleCampaign.Save();
        }
        void OnApplicationQuit(){SingleCheckpoint();}
        void TickSingleEnemy() {
            int oldTurn=engine.State.TurnNumber;engine.Single.EnemyStep();SingleCheckpoint();aiNextAction=Time.unscaledTime+.4f;
            if(engine.State.IsOver){StartCoroutine(ActionPresentation(engine.DrainEvents()));return;}
            if(oldTurn!=engine.State.TurnNumber){queuedTurnEvents=HumanAiEvents(engine.DrainEvents());StartCoroutine(BeginTurnPresentation());}
            else StartCoroutine(ActionPresentation(engine.DrainEvents()));
        }
        void SingleBattleFinished() {
            if(singleFinished)return;singleFinished=true;clock.Stop();
            singleCampaign.BattleResult(engine);singleReturn?.Invoke();Destroy(gameObject);
        }
        string SingleRestrictionText()=>singleCampaign==null?"":string.Join(" / ",engine.Single.Restrictions.Select(SingleChapterUI.RestrictionName));
        void RefreshSingleUI() {
            if(singleCampaign==null)return;
            if(singleStatus==null) {
                var bg=Box(battlefield,"Chapter resources",new Vector2(.015f,.525f),new Vector2(.19f,.65f),new Color(.035f,.03f,.06f,.95f));
                singleStatus=Label(bg,"",Vector2.zero,Vector2.one,16,gold);
                var intent=Box(battlefield,"Chapter intent",new Vector2(.015f,.24f),new Vector2(.19f,.52f),new Color(.035f,.03f,.06f,.9f));
                singleIntent=Label(intent,"",Vector2.zero,Vector2.one,14,Color.white);
            }
            var s=engine.Single;singleStatus.text=$"남은 턴 {Math.Max(0,s.Limit-s.Round+1)} / {s.Limit}\n총 마나 {s.RemainingBudget}/{s.Budget}\n턴 사용 {s.Progress.turnSpent}/{s.Options.stage.turnSpendLimit}   토큰 {singleCampaign.Run.tokens}";
            singleIntent.text=s.Intent()+"\n\n제한: "+SingleRestrictionText()+"\n\n증강: "+string.Join(" / ",singleCampaign.Run.augments.Select(id=>singleCampaign.Data.augments.First(a=>a.id==id).name))+"\n보호막 "+s.Progress.shield;
            if(mapStatus!=null)mapStatus.text=SingleRestrictionText();
            if(mapHeader!=null)mapHeader.text=singleCampaign.Stage.name;
            opponentInfo.text=$"{singleCampaign.Stage.name}   HP {engine.State.Players[1].HP}/{singleCampaign.Stage.enemyHP}";
            ownInfo.text=$"나   HP {engine.State.Players[0].HP}/{singleCampaign.Run.maxHP}   덱 {engine.State.Players[0].DeckCount}   패 {engine.State.Players[0].Hand.Count}/8";
            foreach(var label in unknownLabels)if(label!=null)label.gameObject.SetActive(false);
            foreach(var row in unknownPips)foreach(var pip in row)if(pip!=null)pip.gameObject.SetActive(false);
        }
    }
}
