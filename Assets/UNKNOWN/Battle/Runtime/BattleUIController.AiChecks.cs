#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    public sealed class AiCheckRunner : MonoBehaviour { }
    public sealed partial class BattleUIController
    {
        [Serializable] sealed class AiReport {public bool completed;public List<string> passed=new List<string>(),failed=new List<string>(),errors=new List<string>();}
        public void RunAiChecks(){Invoke(nameof(BeginAiChecks),.3f);}
        void BeginAiChecks(){var runner=new GameObject("AI check driver").AddComponent<AiCheckRunner>();runner.StartCoroutine(AiChecks(runner));}
        IEnumerator AiWait(Func<bool> predicate,double seconds=35)
        {double until=Time.realtimeSinceStartupAsDouble+seconds;while(!predicate() && Time.realtimeSinceStartupAsDouble<until)yield return null;if(!predicate())throw new Exception("AI UI wait timed out: turn="+engine.State.TurnNumber+" active="+engine.State.ActivePlayer);}
        IEnumerator AiChecks(AiCheckRunner runner)
        {
            var r=new AiReport();Action<bool,string> verify=(ok,name)=>{if(ok)r.passed.Add(name);else r.failed.Add(name);};Application.LogCallback listener=(m,s,t)=>{if(t==LogType.Error || t==LogType.Exception || t==LogType.Assert)r.errors.Add(m+"\n"+s);};Application.logMessageReceived+=listener;
            try {
                var room=FindFirstObjectByType<Unknown.Multiplayer.RoomTestUI>();
                verify(aiMode && online==null && room.GetComponent<Unknown.Multiplayer.RoomController>().Session==null,"Actual title launches offline AI without a session");
                yield return AiWait(()=>!actionBusy && !transitioning && !IsMapIntroActive);yield return null;
                int canvasID=canvas.GetInstanceID();
                foreach(AiDifficulty difficulty in Enum.GetValues(typeof(AiDifficulty))) {
                    if(aiDifficulty!=difficulty){aiDifficulty=difficulty;seed=71;NewMatch();yield return AiWait(()=>!actionBusy && !transitioning && !IsMapIntroActive);yield return null;}
                    verify(engine.Config.Map!=BattleMap.Normal && aiMaps.DrawCount>=1,difficulty+" uses existing five-map random pool");
                    verify(ViewPlayer==0 && engine.State.ActivePlayer==0 && clock.Running && clock.Duration==40,difficulty+" starts human turn with normal forty-second clock");
                    verify(handViews.Count==engine.State.Players[0].Hand.Count && backViews.Count==engine.State.Players[1].Hand.Count && backViews.All(v=>v.Find("Revealed Face")==null),difficulty+" shows human hand and only AI card backs");
                    verify(engine.State.Players.All(p=>p.UnknownCreated==0 && p.JokerSuccesses==0 && p.CurrentMana>=0),difficulty+" starts with clean progress and mana");
                    if(difficulty==AiDifficulty.Easy)CaptureAttackFrame(1920,handContent,"AiBattle",false);
                    verify(ClickCheckUI(endTurn.GetComponent<RectTransform>()),difficulty+" actual end-turn button accepts pointer click");
                    yield return AiWait(()=>engine.State.ActivePlayer==1 && !transitioning && !actionBusy || engine.State.IsOver);yield return null;
                    verify(ViewPlayer==0 && !passOverlay.activeSelf && !endTurn.interactable,difficulty+" keeps perspective fixed and blocks human input on AI turn");
                    yield return AiWait(()=>engine.State.ActivePlayer==0 && !transitioning && !actionBusy || engine.State.IsOver,50);yield return null;
                    verify(aiActionCount>1 && (engine.State.Players[1].Board.Any(c=>c!=null) || engine.State.Players[1].UnknownCreated>0),difficulty+" executes actions then ends turn through common rules");
                    verify(ViewPlayer==0 && !passOverlay.activeSelf,difficulty+" returns to human without hotseat privacy screen");
                    var hidden=HumanAiEvents(new[]{new BattleEvent(BattleEventKind.Combine,1,source:123,target:124,definition:CardDatabase.Get("Unknown_K",CardBalanceVersion.Baseline)),new BattleEvent(BattleEventKind.Draw,1,target:125,definition:CardDatabase.Get("Heart_A",CardBalanceVersion.Baseline))});
                    verify(hidden.Count==1 && hidden[0].TargetID==-1 && hidden[0].Definition==null,difficulty+" filters private AI combination and draw identities before animation");
                    // Deterministic terminal fixture still requires the live AI and actual attack/victory command.
                    StopAllCoroutines();animations.ResetAll();ClearSelection();actionBusy=transitioning=false;clock.Begin(40,Time.realtimeSinceStartupAsDouble);
                    engine=new BattleEngine(71,new BattleConfig {CardVersion=CardBalanceVersion.Baseline,StartingHand=0,Map=BattleMap.Normal});foreach(var p in engine.State.Players){p.HandCards.Clear();for(int i=0;i<5;i++)p.Slots[i]=null;}
                    engine.State.ActivePlayer=1;engine.State.TurnNumber=4;engine.State.Players[0].HP=1;
                    var unit=engine.NewCard(CardDatabase.Get("Heart_3",CardBalanceVersion.Baseline),1);unit.SummonedTurn=2;engine.State.Players[1].Slots[0]=unit;engine.DrainEvents();ResetHandViews();Refresh();aiNextAction=Time.unscaledTime+.1f;
                    yield return AiWait(()=>resultOverlay.activeSelf && !actionBusy);yield return null;
                    verify(engine.State.Winner==1 && winnerText.text=="DEFEAT" && aiResultInfo.text.Contains(difficulty.ToString().ToUpperInvariant()),difficulty+" real AI lethal attack displays difficulty and result");
                    verify(aiResultInfo.text.Contains("UNKNOWN 0/3") && aiResultInfo.text.Contains("총 4턴"),difficulty+" result displays only own victory counters and total turns");
                    if(difficulty==AiDifficulty.Hard)CaptureAttackFrame(1280,winnerText.rectTransform,"AiResult",false);
                    int draws=aiMaps.DrawCount;NewMatch();yield return AiWait(()=>!transitioning && !actionBusy && !IsMapIntroActive);yield return null;
                    verify(aiMaps.DrawCount==draws+1 && canvas.GetInstanceID()==canvasID && engine.State.TurnNumber==1 && !resultOverlay.activeSelf,difficulty+" rematch draws a fresh map and reuses Canvas with reset state");
                    verify(engine.State.Players.All(p=>p.HP>0 && p.HP<=30 && (engine.Config.Map==BattleMap.JokerGame || p.HP==30) && p.Board.All(c=>c==null) && p.UnknownCreated==0 && p.JokerSuccesses==0 && !p.ManualDiscardUsed),difficulty+" rematch clears all boards, HP, progress and discard use");
                }
                engine.State.ActivePlayer=1;aiNextAction=Time.unscaledTime-.1f;int oldHand=engine.State.Players[1].Hand.Count;clock.Begin(.01,Time.realtimeSinceStartupAsDouble-.1);
                TickAi();verify(engine.State.TurnExpired && engine.State.Players[1].Hand.Count==oldHand && engine.State.Players[1].Board.All(c=>c==null),"AI rechecks real deadline after planning and commits no late card");
                yield return AiWait(()=>engine.State.ActivePlayer==0 && !actionBusy && !transitioning);verify(clock.Running && clock.Duration==40,"AI timeout completes through normal transition to human forty-second turn");
                ReturnToMenu();verify(room.GetComponentInChildren<UnityEngine.UIElements.UIDocument>().rootVisualElement.style.display!=UnityEngine.UIElements.DisplayStyle.None,"Exit restores actual title");r.completed=true;
            }finally {Application.logMessageReceived-=listener;File.WriteAllText("Library/UNKNOWN-AI-Checks.json",JsonUtility.ToJson(r,true));Debug.Log($"[UNKNOWN] AI checks: passed={r.passed.Count}, failed={r.failed.Count}, errors={r.errors.Count}");Destroy(runner.gameObject);Destroy(gameObject);}
        }
    }
}
#endif
