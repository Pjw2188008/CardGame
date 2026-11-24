using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    public sealed partial class BattleUIController
    {
        SingleCampaign singleCampaign;Action singleReturn;bool singleFinished;Text singleIntent;RectTransform singleDetails;bool singleExpanded;RectTransform singleNotice;
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
            singleCampaign.Run.battle=engine.SaveSingle();
            foreach(string id in engine.Single.Progress.usedCards)if(!singleCampaign.Run.usedCards.Contains(id))singleCampaign.Run.usedCards.Add(id);
            singleCampaign.Save();
        }
        void OnApplicationQuit(){SingleCheckpoint();}
        void TickSingleEnemy() {
            int oldTurn=engine.State.TurnNumber;engine.Single.EnemyStep();SingleCheckpoint();aiNextAction=Time.unscaledTime+.1f;
            if(engine.State.IsOver){StartCoroutine(ActionPresentation(engine.DrainEvents()));return;}
            if(oldTurn!=engine.State.TurnNumber){queuedTurnEvents=HumanAiEvents(engine.DrainEvents());StartCoroutine(BeginTurnPresentation());}
            else StartCoroutine(ActionPresentation(engine.DrainEvents()));
        }
        void SingleBattleFinished() {
            if(singleFinished)return;singleFinished=true;clock.Stop();
            if(SingleGuideActive && engine.State.Winner==0)singleCampaign.Run.battleGuide=5;
            singleCampaign.BattleResult(engine);singleReturn?.Invoke();Destroy(gameObject);
        }
        RectTransform singleGuidePanel;Text singleGuideTitle,singleGuideBody;
        bool SingleGuideActive=>singleCampaign!=null && singleCampaign.IsTeachingBattle && singleCampaign.Run.battleGuide>0 && singleCampaign.Run.battleGuide<5;
        void ObserveSingleGuide(System.Collections.Generic.IEnumerable<BattleEvent> events) {
            if(!SingleGuideActive)return;
            var run=singleCampaign.Run;int previous=run.battleGuide;
            foreach(var e in events.Where(e=>e.Owner==0)) {
                if(run.battleGuide==1 && e.Kind==BattleEventKind.Summon){run.battleGuide=2;run.battleGuideTurn=engine.State.TurnNumber;}
                if(run.battleGuide==3 && e.Kind==BattleEventKind.Attack)run.battleGuide=4;
            }
            if(previous!=run.battleGuide)singleCampaign.Save();
        }
        void RefreshSingleGuide() {
            if(!SingleGuideActive){if(singleGuidePanel!=null)singleGuidePanel.gameObject.SetActive(false);return;}
            var run=singleCampaign.Run;var state=engine.State;var single=engine.Single;
            if(run.battleGuide==2 && state.ActivePlayer==0 && state.TurnNumber>run.battleGuideTurn && !actionBusy && !transitioning){run.battleGuide=3;singleCampaign.Save();}
            if(singleGuidePanel==null) {
                singleGuidePanel=Box(battlefield,"Single live tutorial",new Vector2(.008f,.64f),new Vector2(.21f,.93f),new Color(.025f,.025f,.04f,.98f));
                singleGuidePanel.GetComponent<Image>().raycastTarget=false;
                singleGuideTitle=Label(singleGuidePanel,"",new Vector2(.05f,.78f),new Vector2(.95f,.97f),20,Color.white);
                singleGuideBody=Label(singleGuidePanel,"",new Vector2(.06f,.21f),new Vector2(.94f,.78f),18,Color.white);
                singleGuideBody.resizeTextForBestFit=true;singleGuideBody.resizeTextMinSize=14;singleGuideBody.resizeTextMaxSize=18;
                ActionButton(singleGuidePanel,"안내 스킵",new Vector2(.12f,.04f),new Vector2(.88f,.17f),()=>{singleCampaign.Run.battleGuide=-1;singleCampaign.Save();singleGuidePanel.gameObject.SetActive(false);});
            }
            singleGuidePanel.gameObject.SetActive(!MenuOpen && !GamePreferences.IsOpen && !state.IsOver && !singleExpanded);
            singleGuideTitle.text=new[]{"","1 · 카드 소환","2 · 턴과 적 행동","3 · 공격","4 · 승리와 보상"}[run.battleGuide];
            string status=$"남은 {Math.Max(0,single.Limit-single.Round+1)}턴 · 마나 예산 {single.RemainingBudget}/{single.Budget}\n";
            string instruction;
            if(engine.NeedsTarget)instruction="강조된 효과 대상을 선택해 소환 효과를 마무리하세요.";
            else if(state.ActivePlayer==1)instruction="적이 카드를 소환하고 공격하는 중입니다. 내 턴이 돌아오면 이어서 행동하세요.";
            else if(run.battleGuide==1)instruction="패의 카드를 빈 아군 슬롯에 드래그하세요. 현재 마나·비용·턴 사용 상한을 확인하세요.";
            else if(run.battleGuide==2)instruction="소환 완료! 소환한 유닛은 다음 내 턴부터 공격합니다. 턴 종료를 눌러 적 행동과 마나 회복을 확인하세요.";
            else if(run.battleGuide==3)instruction=state.Players[0].Board.Any(c=>engine.CanAttack(0,c))?"공격 가능한 아군을 적 유닛에 드래그하세요. 적 필드가 비면 적 HP 영역에 드래그해 직접 공격하세요.":"지금 공격 가능한 유닛이 없습니다. 소환 후 다음 내 턴을 준비하세요. 전투 정보에서 방해 효과도 확인하세요.";
            else instruction="공격 성공! 제한 턴 안에 적 HP를 0으로 만드세요. 전투 정보에서 적 행동·방해 효과를 확인하세요. 승리 후 카드 보상과 런 토큰을 얻습니다.";
            singleGuideBody.text=status+"\n"+instruction;
        }
        string SingleRestrictionText()=>singleCampaign==null?"":string.Join("\n",engine.Single.Restrictions.Where(r=>r!=SingleRestriction.None).Select(SingleChapterUI.RestrictionName).DefaultIfEmpty("없음"));
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
            if(singleNotice==null) {
                singleNotice=Box(interactionRoot,"Single action notice",new Vector2(.28f,.56f),new Vector2(.72f,.65f),new Color(.025f,.025f,.04f,.96f));
                singleNotice.GetComponent<Image>().raycastTarget=false;
                singleNotice.gameObject.AddComponent<CanvasGroup>().blocksRaycasts=false;
                notice.transform.SetParent(singleNotice,false);var rect=notice.rectTransform;
                rect.anchorMin=new Vector2(.03f,.08f);rect.anchorMax=new Vector2(.97f,.92f);rect.offsetMin=rect.offsetMax=Vector2.zero;
                notice.fontSize=22;notice.resizeTextForBestFit=true;notice.resizeTextMinSize=16;notice.resizeTextMaxSize=22;
            }
            singleNotice.gameObject.SetActive(!engine.State.IsOver && !IsPaused && notice.text.Length>0);
            if(singleNotice.gameObject.activeSelf)singleNotice.SetAsLastSibling();
            RefreshSingleGuide();
            singleIntent.text=SingleRestrictionText();
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
