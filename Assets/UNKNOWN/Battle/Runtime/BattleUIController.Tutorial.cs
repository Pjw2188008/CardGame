using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    public sealed partial class BattleUIController
    {
        TutorialSession tutorial;
        bool tutorialRequested,tutorialSwitching,tutorialFinished;
        int tutorialStart=1;
        Text tutorialGoal,tutorialOperation,tutorialExplanation,tutorialProgress;
        RectTransform tutorialRoot,tutorialHelp,tutorialDone;
        Button tutorialNext;
        float tutorialStarted;
        bool tutorialHinted;
        readonly List<RectTransform> tutorialRings=new List<RectTransform>();
        AudioSource tutorialAudio;
        AudioClip tutorialTone;
        static readonly string[] lessonGoals={"기본 설명 7개 확인","카드 1장 소환","유닛 공격 + 직접 공격","카드 1장 직접 버리기","원페어 활성화","JOKER 사용 및 결과 확인","UNKNOWN 3종 조합 및 필드 배치"};
        static readonly string[] basicRules={
            "상대 HP를 0으로 만들면 승리합니다.\n양쪽 플레이어의 시작 HP는 30입니다.",
            "카드 왼쪽 아래는 공격력, 오른쪽 아래는 체력입니다.\n유닛 전투에서는 서로 동시에 피해를 줍니다.",
            "기본 덱은 54장, 손패 최대는 8장입니다.\n손패가 꽉 차면 새로 뽑은 카드가 폐기됩니다.",
            "아래는 아군 필드, 위는 상대 필드입니다.\n각 필드에 유닛을 최대 5장 놓을 수 있습니다.",
            "파란 숫자는 마나, 카드 왼쪽 위 숫자는 비용입니다.\n마나가 충분해야 소환할 수 있습니다.",
            "자신의 턴에 소환·공격·조합을 합니다.\n일반 대전은 턴당 40초입니다. 여기서는 시간 제한이 없습니다.",
            "상대 HP 0 또는 일반 문양 맵에서 필드에 UNKNOWN 3장 배치 시 승리합니다.\n조커의 유희에서는 UNKNOWN 대신 JOKER 도박 누적 3회 성공으로 승리합니다."};
        public void ConfigureTutorial(int startStep,Action onReturn)
        {tutorialRequested=true;tutorialStart=Mathf.Clamp(startStep,1,7);returned=onReturn;}
        void StartTutorial()
        {
            enemyBoard.sizeDelta=new Vector2(1060,190);
            BuildTutorialUI();LoadTutorialStep(tutorialStart);
        }
        void BuildTutorialUI()
        {
            tutorialRoot=Box(canvas.transform,"Tutorial HUD",Vector2.zero,Vector2.one,Color.clear);
            var top=Box(tutorialRoot,"Tutorial objective",new Vector2(.25f,.845f),new Vector2(.78f,.927f),navy);
            tutorialGoal=Label(top,"",new Vector2(0,.46f),new Vector2(1,1),20,gold);
            tutorialOperation=Label(top,"",new Vector2(0,0),new Vector2(1,.48f),17,Color.white);
            tutorialProgress=Label(tutorialRoot,"",new Vector2(.25f,.815f),new Vector2(.78f,.842f),16,gold);
            tutorialHelp=Box(tutorialRoot,"Tutorial explanation",new Vector2(.008f,.31f),new Vector2(.212f,.84f),new Color(.035f,.05f,.075f,.97f));
            Label(tutorialHelp,"TUTORIAL",new Vector2(.04f,.89f),new Vector2(.96f,.98f),26,gold);
            tutorialExplanation=Label(tutorialHelp,"",new Vector2(.06f,.29f),new Vector2(.94f,.88f),20,Color.white);
            tutorialExplanation.resizeTextForBestFit=true;tutorialExplanation.resizeTextMinSize=14;tutorialExplanation.resizeTextMaxSize=20;
            tutorialNext=ActionButton(tutorialHelp,"다음 설명",new Vector2(.06f,.18f),new Vector2(.94f,.27f),()=>{if(tutorialSwitching)return;tutorial.ConfirmExplanation();RefreshTutorialText();});
            ActionButton(tutorialHelp,"도움말",new Vector2(.06f,.07f),new Vector2(.47f,.16f),()=>{tutorialHinted=true;Toast(TutorialInstruction());TutorialCue(false);});
            ActionButton(tutorialHelp,"단계 재시작",new Vector2(.50f,.07f),new Vector2(.94f,.16f),()=>LoadTutorialStep(tutorial.Step));
            // Existing ESC/settings menu remains available; only its exit label changes.
            var exit=battleMenu.GetComponentsInChildren<Button>(true).First(b=>b.GetComponentInChildren<Text>().text=="타이틀로 돌아가기");
            exit.GetComponentInChildren<Text>().text="튜토리얼 종료";
            tutorialDone=Box(canvas.transform,"Tutorial Complete",Vector2.zero,Vector2.one,new Color(.025f,.04f,.065f,.99f));
            Label(tutorialDone,"TUTORIAL COMPLETE",new Vector2(.1f,.73f),new Vector2(.9f,.85f),44,gold);
            Label(tutorialDone,"UNKNOWN의 기본 규칙을 익혔습니다.",new Vector2(.1f,.65f),new Vector2(.9f,.73f),26,Color.white);
            Label(tutorialDone,"✓ 카드 소환과 마나    ✓ 카드 공격\n✓ 손패 관리    ✓ 포커 시너지\n✓ JOKER 도박    ✓ UNKNOWN 조합    ✓ 게임 승리 조건",new Vector2(.15f,.40f),new Vector2(.85f,.63f),24,Color.white);
            ActionButton(tutorialDone,"타이틀로 돌아가기",new Vector2(.32f,.26f),new Vector2(.68f,.33f),ReturnToMenu);
            ActionButton(tutorialDone,"튜토리얼 다시 플레이",new Vector2(.32f,.16f),new Vector2(.68f,.23f),()=>{TutorialProgress.Reset();LoadTutorialStep(1);});
            tutorialDone.gameObject.SetActive(false);
        }
        void LoadTutorialStep(int step)
        {
            // Keep this Canvas; replace only the local lesson's state and transient visuals.
            StopAllCoroutines();ClearSelection();animations.ResetAll();
            tutorial=new TutorialSession(step,Environment.TickCount);engine=tutorial.Engine;
            ResetHandViews();cardViews.Clear();
            if(pokerList!=null)pokerList.gameObject.SetActive(false);
            tutorialSwitching=tutorialFinished=actionBusy=transitioning=paused=false;clock.Stop();queuedTurnEvents=null;
            toastUntil=0;lastDropID=-1;lastLogCount=0;presentedMapEvents.Clear();
            passOverlay.SetActive(false);resultOverlay.SetActive(false);battleMenu.gameObject.SetActive(false);tutorialDone.gameObject.SetActive(false);
            ClearTutorialRings();Refresh();opponentHand.gameObject.SetActive(false);
            combineButton.gameObject.SetActive(false);
            tutorialStarted=Time.unscaledTime;tutorialHinted=false;feedback="";RefreshTutorialText();TutorialCue(false);
            if(step==4)StartCoroutine(TutorialOverflowDemo());
        }
        IEnumerator TutorialOverflowDemo()
        {
            tutorialSwitching=true;yield return new WaitForSecondsRealtime(1.5f);
            tutorial.DemonstrateOverflow();var events=engine.DrainEvents();tutorial.Observe(events);
            yield return ActionPresentation(events);tutorialSwitching=false;RefreshTutorialText();
            Toast("새 드로우 1장이 폐기되었습니다. 기존 손패 8장은 유지됩니다.");
        }
        string TutorialInstruction()
        {
            switch(tutorial.Step) {
                case 1:return "조작: 강조된 정보를 보고 '다음 설명'을 누르세요.";
                case 2:return "조작: ♥3을 아래 빈 필드로 드래그하세요.";
                case 3:return tutorial.UnitAttack ? "조작: 남은 아군 유닛을 상대 HP 영역으로 드래그하세요." : "조작: 아군 유닛을 위쪽 ♠2에게 드래그하세요.";
                case 4:return !tutorial.OverflowShown ? "조작: 손패 초과 드로우 시연을 확인하세요." : discardMode ? "조작: 버릴 손패 카드를 클릭하세요. 즉시 버려집니다. ESC로 취소." : "조작: 오른쪽 아래 '카드 버리기'를 누르세요.";
                case 5:return engine.NeedsTarget ? "조작: 강조된 아군 카드를 눌러 ♥5 효과를 완료하세요." : "조작: ♥5와 ♠5를 아래 필드에 드래그하세요.";
                case 6:return "조작: JOKER를 아군 ♥3에게 드래그하세요.";
                default:if(tutorial.Combinations>=3)return "조작: 손패의 UNKNOWN 3장을 아래 빈 필드로 드래그하세요.";return !combinationMode ? "조작: 중앙 'UNKNOWN 조합'을 누르세요." : aceID<0 ? "조작: 손패의 A 카드를 클릭하세요." : faceID<0 ? "조작: 손패의 "+(tutorial.Combinations==0 ? "J" : tutorial.Combinations==1 ? "Q" : "K")+" 카드를 클릭하세요." : "조작: 중앙 '조합 확정'을 다시 눌러 생성하세요.";
            }
        }
        void RefreshTutorialText()
        {
            tutorialGoal.text=$"TUTORIAL {tutorial.Step}/7 · 목표: {lessonGoals[tutorial.Step-1]}";
            tutorialOperation.text=TutorialInstruction();
            tutorialProgress.text=string.Join("   ",Enumerable.Range(1,7).Select(i=>i<tutorial.Step ? "✓" : i==tutorial.Step ? "["+i+"]" : "·"));
            tutorialNext.gameObject.SetActive(tutorial.Step==1);tutorialNext.interactable=!tutorialSwitching;
            switch(tutorial.Step) {
                case 1:tutorialExplanation.text=basicRules[Mathf.Min(6,tutorial.Explanation)]+$"\n\n{Mathf.Min(7,tutorial.Explanation+1)} / 7";break;
                case 2:tutorialExplanation.text="목표: 카드 1장 소환\n\n♥3 비용: 1 MP\n소환 후 마나 감소를 확인하세요.\n필드가 꽉 차거나 마나가 부족하면 소환할 수 없습니다.";break;
                case 3:tutorialExplanation.text="목표: 유닛 공격 + 직접 공격\n\n"+(tutorial.UnitAttack ? "✓ 유닛 공격 완료\n상대 필드가 비었습니다." : "상대 유닛을 먼저 제거하세요.")+"\n\n소환한 턴에는 공격할 수 없습니다. 각 유닛은 턴마다 한 번 공격합니다.";break;
                case 4:tutorialExplanation.text="목표: 카드 1장 직접 버리기\n\n손패는 최대 8장입니다. 초과한 새 카드만 폐기됩니다.\n\n수동 버리기는 자신의 턴마다 1회, 무료입니다. 즉시 다시 뽑지 않습니다.";break;
                case 5:tutorialExplanation.text="목표: ONE PAIR · 공격력 +2\n\n같은 계급 5 두 장을 소환하세요.\n♥5 효과의 대상도 선택하세요.\n\n최고 족보 하나만 적용되고,\n깨지면 효과가 해제됩니다.\nJOKER·UNKNOWN은 제외됩니다.\n\n우측 MY SYNERGY 클릭:\n9종 족보 전체 보기";break;
                case 6:tutorialExplanation.text="목표: 도박 실행 및 결과 확인\n\n성공 50%: 공격력·최대 체력 1.5배 (올림)\n실패 50%: 대상 파괴\n\n어느 결과든 학습 완료입니다. 도박 비용은 2 MP입니다.";break;
                case 7:tutorialExplanation.text="목표: UNKNOWN 3종 조합 및 필드 배치\n\nA + J → UNKNOWN JACK\nA + Q → UNKNOWN QUEEN\nA + K → UNKNOWN KING\n\n문양은 무관하며 손패 재료만 사용합니다. 비용 0 MP.\n생성 후 다음 재료를 지급합니다.\n생성한 UNKNOWN 3장을 필드로 드래그하세요.\n이 실습에서는 소환 후 마나를 충전합니다.\n\nUNKNOWN "+tutorial.Combinations+"/3";break;
            }
        }
        void TickTutorial()
        {
            timerText.text="시간 제한 없음";timerText.fontSize=17;timerFill.rectTransform.anchorMax=Vector2.one;
            tutorialHelp.gameObject.SetActive(!MenuOpen && (pokerList==null || !pokerList.gameObject.activeSelf));
            RefreshControls();endTurn.interactable=false;opponentHand.gameObject.SetActive(false);
            discardButton.interactable &= tutorial.Step==4 && !tutorialSwitching;
            combineButton.interactable &= tutorial.Step==7;combinationModeButton.interactable &= tutorial.Step==7;
            combineButton.GetComponentInChildren<Text>().text=tutorial.Step==7 ? "조합 확정" : "UNKNOWN 조합";
            if(tutorialFinished)return;
            if(!tutorialSwitching && !actionBusy && !transitioning) {
                if(tutorial.Complete) {StartCoroutine(CompleteTutorialStep());return;}
                if(tutorial.GiveMaterials())Refresh();
                if(!tutorialHinted && Time.unscaledTime-tutorialStarted>25) {tutorialHinted=true;Toast(TutorialInstruction());}
            }
            RefreshTutorialText();RefreshTutorialRings();
        }
        IEnumerator CompleteTutorialStep()
        {
            tutorialSwitching=true;ClearSelection();TutorialProgress.SaveCompleted(tutorial.Step);TutorialCue(true);
            string message=tutorial.Step==6 ? tutorial.GambleSucceeded ? "도박 성공! 유닛이 강화되었습니다." : "도박 실패! 대상 유닛이 파괴되었습니다." : "STEP "+tutorial.Step+" 완료";
            tutorialOperation.text=message;ClearTutorialRings();
            yield return animations.Banner(message,gold,1.5f);
            if(tutorial.Step==7){tutorialFinished=true;tutorialDone.gameObject.SetActive(true);tutorialDone.SetAsLastSibling();TutorialCue(true);}
            else LoadTutorialStep(tutorial.Step+1);
        }
        void ClearTutorialRings() {foreach(var r in tutorialRings)if(r!=null)Destroy(r.gameObject);tutorialRings.Clear();}
        void RefreshTutorialRings()
        {
            var targets=new List<RectTransform>();int s=tutorial.Step;
            if(s==1) {
                switch(tutorial.Explanation) {
                    case 0:targets.Add(ownInfo.rectTransform);targets.Add(opponentInfo.rectTransform);break;
                    case 1:targets.AddRange(cardViews.Where(k=>BoardCard(k.Key)!=null).Select(k=>k.Value));break;
                    case 2:targets.Add(handContent);targets.Add(deckMarkers[0]);break;
                    case 3:targets.Add(ownBoard);targets.Add(enemyBoard);break;
                    case 4:targets.Add(manaLabels[0].rectTransform);targets.AddRange(handViews.Values);break;
                    case 5:targets.Add(endTurn.GetComponent<RectTransform>());break;
                    default:targets.Add(unknownLabels[0].rectTransform);break;
                }
            }
            if(s==2 || s==5){targets.AddRange(handViews.Values);targets.Add(ownBoard);if(s==5)targets.Add(pokerLabels[0].rectTransform);}
            if(s==3){targets.Add(ownBoard);targets.Add(tutorial.UnitAttack ? opponentInfo.rectTransform : enemyBoard);}
            if(s==4){targets.Add(discardButton.GetComponent<RectTransform>());targets.Add(handCountLabel.rectTransform);targets.AddRange(handViews.Values);}
            if(s==6){targets.AddRange(handViews.Values);targets.Add(ownBoard);}
            if(s==7){targets.Add(combinationModeButton.GetComponent<RectTransform>());targets.AddRange(handViews.Where(k=>engine.State.Players[0].Hand.Any(c=>c.InstanceID==k.Key && c.Definition.CardType==CardType.Normal)).Select(k=>k.Value));}
            while(tutorialRings.Count<targets.Count){var go=new GameObject("Tutorial highlight",typeof(RectTransform),typeof(CanvasRenderer),typeof(AttackGlowGraphic));go.transform.SetParent(tutorialRoot,false);var r=go.GetComponent<RectTransform>();go.GetComponent<AttackGlowGraphic>().raycastTarget=false;tutorialRings.Add(r);}
            for(int i=0;i<tutorialRings.Count;i++) {
                var r=tutorialRings[i];r.gameObject.SetActive(i<targets.Count && !tutorialSwitching && !actionBusy && !MenuOpen && !GamePreferences.IsOpen);
                if(i>=targets.Count || targets[i]==null)continue;
                var corners=new Vector3[4];targets[i].GetWorldCorners(corners);var a=tutorialRoot.InverseTransformPoint(corners[0]);var b=tutorialRoot.InverseTransformPoint(corners[2]);
                r.anchorMin=r.anchorMax=tutorialRoot.pivot;r.anchoredPosition=(a+b)*.5f;r.sizeDelta=b-a;
                r.GetComponent<AttackGlowGraphic>().color=new Color(gold.r,gold.g,gold.b,.45f+.20f*Mathf.Sin(Time.unscaledTime*3));
            }
        }
        void TutorialCue(bool success)
        {
            if(tutorialAudio==null){tutorialAudio=gameObject.AddComponent<AudioSource>();tutorialAudio.playOnAwake=false;tutorialAudio.volume=.5f;GamePreferences.Instance.Register(tutorialAudio,AudioChannel.SFX);}
            if(tutorialTone!=null)Destroy(tutorialTone);
            const int rate=22050;var data=new float[rate/6];float hz=success ? 880 : 660;
            for(int i=0;i<data.Length;i++)data[i]=.1f*Mathf.Sin(2*Mathf.PI*hz*i/rate)*Mathf.Sin(Mathf.PI*i/data.Length);
            tutorialTone=AudioClip.Create("Tutorial cue",data.Length,1,rate,false);tutorialTone.SetData(data,0);tutorialAudio.clip=tutorialTone;tutorialAudio.Play();
        }
    }
}
