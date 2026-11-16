using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    public sealed partial class BattleUIController
    {
        SingleCampaign singleCampaign;Action singleReturn;bool singleFinished;Text singleIntent;RectTransform singleDetails;bool singleExpanded;
        public void ConfigureSingle(SingleCampaign campaign,Action onReturn) {
            singleCampaign=campaign;singleReturn=onReturn;aiMode=true;revealHands=false;turnDuration=86400;
            returned=()=>{singleReturn?.Invoke();};
        }
        BattleEngine MakeSingleEngine() {
            var run=singleCampaign.Run;
            var options=new SingleBattleOptions{layer=run.nodes[run.current].layer,stage=singleCampaign.Stage,augmentData=singleCampaign.Data.augments,difficulty=singleCampaign.Data.difficulties[run.difficulty],deck=run.deck.ToArray(),augments=run.augments.ToArray(),hp=run.hp,maxHP=run.maxHP,seed=unchecked(run.seed+run.current*104729),tutorial=singleCampaign.IsTeachingBattle};
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
            if(singleDetails==null) {
                ActionButton(battlefield,"전투 정보",new Vector2(.015f,.545f),new Vector2(.19f,.59f),()=>{singleExpanded=!singleExpanded;singleDetails.gameObject.SetActive(singleExpanded);});
                var intent=Box(battlefield,"Chapter intent",new Vector2(.01f,.27f),new Vector2(.15f,.53f),new Color(.035f,.03f,.06f,.9f));
                singleDetails=intent;intent.gameObject.AddComponent<RectMask2D>();
                singleIntent=Label(intent,"",new Vector2(0,1),new Vector2(1,1),16,Color.white);singleIntent.alignment=TextAnchor.UpperLeft;singleIntent.verticalOverflow=VerticalWrapMode.Overflow;
                var content=singleIntent.rectTransform;content.pivot=new Vector2(.5f,1);content.sizeDelta=new Vector2(-16,0);content.anchoredPosition=Vector2.zero;
                var scroll=intent.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.viewport=intent;scroll.content=content;scroll.movementType=ScrollRect.MovementType.Clamped;
                singleDetails.gameObject.SetActive(singleExpanded);
            }
            var s=engine.Single;
            singleIntent.text=$"마나 예산 {s.RemainingBudget}/{s.Budget} · 턴 사용 {s.Progress.turnSpent}/{s.Options.stage.turnSpendLimit}\n덱 {engine.State.Players[0].DeckCount}장\n"+s.Intent()+"\n\n제한: "+SingleRestrictionText()+"\n\n증강: "+string.Join(" / ",singleCampaign.Run.augments.Select(id=>singleCampaign.Data.augments.First(a=>a.id==id).name))+"\n보호막 "+s.Progress.shield;
            singleIntent.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Math.Max(singleDetails.rect.height,singleIntent.preferredHeight+16));
            if(mapStatus!=null)mapStatus.text="";
            if(mapHeader!=null)mapHeader.text=singleCampaign.Stage.name;
            opponentInfo.text=$"{singleCampaign.Stage.name}   HP {engine.State.Players[1].HP}/{singleCampaign.Stage.enemyHP}";
            ownInfo.text=$"나   HP {engine.State.Players[0].HP}/{singleCampaign.Run.maxHP}   덱 {engine.State.Players[0].DeckCount}   패 {engine.State.Players[0].Hand.Count}/8";
            foreach(var label in unknownLabels)if(label!=null)label.gameObject.SetActive(false);
            foreach(var row in unknownPips)foreach(var pip in row)if(pip!=null)pip.gameObject.SetActive(false);
        }
    }
}
