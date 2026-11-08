#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Unknown.Battle
{
    public sealed class TutorialCheckRunner : MonoBehaviour { }
    public sealed partial class BattleUIController
    {
        [Serializable] sealed class TutorialReport {public bool completed;public List<string> passed=new List<string>(),failed=new List<string>(),errors=new List<string>();}
        public void RunTutorialChecks() {Invoke(nameof(BeginTutorialChecks),.3f);}
        void BeginTutorialChecks() {var runner=new GameObject("Tutorial check driver").AddComponent<TutorialCheckRunner>();runner.StartCoroutine(TutorialChecks(runner));}
        IEnumerator TutorialWait(Func<bool> predicate)
        {double deadline=Time.realtimeSinceStartupAsDouble+18;while(!predicate() && Time.realtimeSinceStartupAsDouble<deadline)yield return null;if(!predicate())throw new Exception("Tutorial transition timed out at step "+tutorial.Step);}
        IEnumerator TutorialChecks(TutorialCheckRunner runner)
        {
            var report=new TutorialReport();Action<bool,string> verify=(ok,name)=>{if(ok)report.passed.Add(name);else report.failed.Add(name);};
            Application.LogCallback listener=(m,s,t)=>{if(t==LogType.Error || t==LogType.Exception || t==LogType.Assert)report.errors.Add(m+"\n"+s);};Application.logMessageReceived+=listener;
            bool had=PlayerPrefs.HasKey(TutorialProgress.Key);string old=PlayerPrefs.GetString(TutorialProgress.Key);
            try {
                TutorialProgress.Reset();LoadTutorialStep(1);yield return null;simulateAttackPointer=true;
                int canvasID=canvas.GetInstanceID();
                var roomUI=FindFirstObjectByType<Unknown.Multiplayer.RoomTestUI>();var roomDocument=roomUI.GetComponentInChildren<UnityEngine.UIElements.UIDocument>();
                verify(roomDocument.rootVisualElement.style.display==UnityEngine.UIElements.DisplayStyle.None && roomUI.GetComponent<Unknown.Multiplayer.RoomController>().Session==null,"Title route hides room UI and launches lesson without a session");
                verify(online==null && tutorial.Step==1 && Blocked() && !clock.Running && !IsMapIntroActive,"Offline lesson starts on existing Canvas with no timer or map draw");
                verify(!tutorialRings.Any(r=>r.GetComponent<Graphic>().raycastTarget),"Tutorial highlight graphics never intercept input");
                CaptureAttackFrame(1920,ownInfo.rectTransform,"Tutorial-1",false);
                for(int i=0;i<6;i++)tutorialNext.onClick.Invoke();yield return new WaitForSecondsRealtime(.2f);verify(tutorial.Step==1,"Six explanations cannot complete step one");
                tutorialNext.onClick.Invoke();yield return TutorialWait(()=>tutorial.Step==2 && !tutorialSwitching);yield return null;
                verify(TutorialProgress.Load().lastCompleted==1 && TutorialProgress.Load().nextStep==2,"Completed lesson writes next-step checkpoint");
                var card=engine.State.Players[0].Hand[0];DragBegin(card);verify(dragged==card,"Summon uses actual Unity BeginDrag handler");DragDrop(UnitDrop(0,0));
                verify(engine.State.Players[0].CurrentMana==9 && engine.State.Players[0].Board[0]==card,"Real summon spends the existing card cost");
                yield return TutorialWait(()=>tutorial.Step==3 && !tutorialSwitching);yield return null;
                verify(canvas.GetInstanceID()==canvasID && engine.State.Players[0].CurrentMana==10 && handViews.Count==0,"Automatic transition reuses Canvas and clears old hand/mana state");
                var source=engine.State.Players[0].Board[0];AttackBegin(source);DragDrop(UnitDrop(1,0));yield return DragWait();
                verify(tutorial.UnitAttack && !tutorial.DirectAttack && tutorial.Step==3,"Unit attack alone cannot complete direct-attack lesson");
                source=engine.State.Players[0].Board[1];AttackBegin(source);DragDrop(heroAttackArea.GetComponent<CardDropZone>());
                verify(engine.State.Players[1].HP<30,"Real hero drop deals direct damage after enemy is removed");
                yield return TutorialWait(()=>tutorial.Step==4 && !tutorialSwitching);yield return null;
                verify(engine.State.Players[0].Hand.Count==8 && engine.State.Players[0].DiscardCount==1 && tutorial.OverflowShown,"Existing draw animation burns only ninth card");
                CaptureAttackFrame(1920,handContent,"Tutorial-4",false);
                verify(discardButton.IsInteractable(),"Discard button re-enables after earlier lessons and overflow animation");
                verify(ClickCheckUI(discardButton.GetComponent<RectTransform>()) && discardMode,"Raycast and actual pointer click activate discard mode");HandleBattleEscape();verify(!discardMode && engine.State.Players[0].Hand.Count==8,"ESC cancels mode before a card is clicked");
                ClickCheckUI(discardButton.GetComponent<RectTransform>());verify(ClickCheckUI(handViews[engine.State.Players[0].Hand[0].InstanceID]),"Actual pointer click reaches hand card");verify(engine.State.Players[0].Hand.Count==7,"Hand-card click immediately discards without a second confirmation or draw");
                yield return TutorialWait(()=>tutorial.Step==5 && !tutorialSwitching);yield return null;
                pokerLabels[0].GetComponentInParent<Button>().onClick.Invoke();yield return null;verify(pokerList.gameObject.activeSelf && !tutorialHelp.gameObject.activeSelf,"Full poker reference remains readable above tutorial help");
                CaptureAttackFrame(1280,pokerList,"Tutorial-PokerReference",false);pokerList.gameObject.SetActive(false);yield return null;
                card=engine.State.Players[0].Hand[0];DragBegin(card);DragDrop(UnitDrop(0,0));yield return DragWait();
                verify(engine.NeedsTarget,"Heart five retains real ally-target effect");ClickSlot(0,0);yield return DragWait();
                card=engine.State.Players[0].Hand[0];DragBegin(card);DragDrop(UnitDrop(0,1));verify(engine.State.Players[0].Poker.kind==PokerKind.OnePair,"Two fives activate real one-pair synergy");
                verify(engine.State.Players[0].Board.Where(c=>c!=null).All(c=>c.PokerAttack==2),"Both actual units receive plus-two attack");
                yield return TutorialWait(()=>tutorial.Step==6 && !tutorialSwitching);yield return null;
                engine.DebugNextGamble=false;card=engine.State.Players[0].Hand[0];DragBegin(card);DragDrop(UnitDrop(0,0));
                verify(tutorial.Gambled && !tutorial.GambleSucceeded && engine.State.Players[0].Board[0]==null,"Failure destroys unit and is accepted as objective");
                yield return TutorialWait(()=>tutorial.Step==7 && !tutorialSwitching);yield return null;
                verify(TutorialProgress.Load().nextStep==7,"Failure branch auto-advances and checkpoints");
                var lessonCanvas=canvas.GetInstanceID();LoadTutorialStep(TutorialProgress.Load().nextStep);yield return null;
                verify(tutorial.Step==7 && tutorial.Combinations==0 && engine.State.Players[0].Hand.Count==2,"Resume reconstructs unfinished lesson from initial state");
                combinationModeButton.onClick.Invoke();var hand=engine.State.Players[0].Hand.ToArray();foreach(var c in hand)handViews[c.InstanceID].GetComponent<Button>().onClick.Invoke();
                verify(combinationMode && aceID>=0 && faceID>=0 && combineButton.interactable,"Existing material clicks select both cards before confirmation");
                yield return null;
                CaptureAttackFrame(1920,handContent,"Tutorial-7",false);
                LoadTutorialStep(7);yield return null;verify(!combinationMode && aceID<0 && engine.State.Players[0].UnknownCreated==0 && canvas.GetInstanceID()==lessonCanvas,"Restart clears selection and progress without replacing Canvas");
                for(int n=0;n<3;n++) {
                    combinationModeButton.onClick.Invoke();hand=engine.State.Players[0].Hand.Where(c=>c.Definition.CardType==CardType.Normal).ToArray();foreach(var c in hand)handViews[c.InstanceID].GetComponent<Button>().onClick.Invoke();combinationModeButton.onClick.Invoke();
                    if(n<2){yield return TutorialWait(()=>!actionBusy && engine.State.Players[0].Hand.Count(c=>c.Definition.CardType==CardType.Normal)==2);yield return null;verify(engine.State.Players[0].UnknownCreated==n+1,"Next pair arrives after existing animation "+(n+1));}
                }
                yield return TutorialWait(()=>tutorialFinished);yield return null;
                verify(tutorialDone.gameObject.activeInHierarchy && !resultOverlay.activeSelf && engine.State.VictoryReason==VictoryReason.UnknownComplete,"Third distinct UNKNOWN invokes real victory and tutorial completion screen");
                var p=TutorialProgress.Load();verify(p.completed && p.lastCompleted==7 && p.nextStep==7,"Final completion persists all checkpoint fields");
                CaptureAttackFrame(1280,tutorialDone,"Tutorial-Complete",false);
                LoadTutorialStep(6);yield return null;engine.DebugNextGamble=true;card=engine.State.Players[0].Hand[0];DragBegin(card);DragDrop(UnitDrop(0,0));
                verify(tutorial.Gambled && tutorial.GambleSucceeded && engine.State.Players[0].Board[0].JokerUsed,"Joker success also completes objective with real unit buff");
                yield return TutorialWait(()=>tutorial.Step==7 && !tutorialSwitching);yield return null;
                HandleBattleEscape();verify(MenuOpen && Blocked(),"ESC menu offers tutorial exit and blocks battle input");GamePreferences.Instance.Open();verify(GamePreferences.IsOpen,"Existing settings remain available within tutorial");GamePreferences.Instance.Close();ToggleBattleMenu();
                TutorialProgress.Reset();verify(TutorialProgress.Load().lastCompleted==0 && TutorialProgress.Load().nextStep==1 && !TutorialProgress.Load().completed,"Start-over resets only tutorial checkpoint");
                ReturnToMenu();verify(roomDocument.rootVisualElement.style.display!=UnityEngine.UIElements.DisplayStyle.None,"Tutorial exit restores the actual title UI");
                report.completed=true;
            }finally {
                if(had)PlayerPrefs.SetString(TutorialProgress.Key,old);else PlayerPrefs.DeleteKey(TutorialProgress.Key);PlayerPrefs.Save();GamePreferences.Instance.Close();
                Application.logMessageReceived-=listener;File.WriteAllText("Library/UNKNOWN-Tutorial-Checks.json",JsonUtility.ToJson(report,true));Debug.Log($"[UNKNOWN] Tutorial checks: passed={report.passed.Count}, failed={report.failed.Count}, errors={report.errors.Count}");Destroy(runner.gameObject);Destroy(gameObject);
            }
        }
    }
}
#endif
