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
        bool aiMode;
        AiDifficulty aiDifficulty;
        AiAgent aiAgent;
        RandomMapSelection aiMaps;
        Action aiSelection;
        int aiActionTurn=-1,aiActionCount;
        float aiNextAction;
        Text aiResultInfo;
        const int AiActionLimit=32;
        public void ConfigureAI(AiDifficulty difficulty,Action onTitle,Action onDifficulty)
        {aiMode=true;aiDifficulty=difficulty;returned=onTitle;aiSelection=onDifficulty;revealHands=false;}
        void ResetAiMatch()
        {
            if(aiMaps==null)aiMaps=new RandomMapSelection(new SeededRandom(Environment.TickCount),BattleMapSettings.LoadRules());
            aiMaps.ResetMatch();localMap=aiMaps.BeginMatch();aiAgent=new AiAgent(aiDifficulty,Environment.TickCount^71417);
            aiActionTurn=-1;aiActionCount=0;aiNextAction=Time.unscaledTime+.5f;
            if(mapIntro!=null){mapIntro.gameObject.SetActive(false);Destroy(mapIntro.gameObject);mapIntro=null;}
            mapIntroPresented=false;revealHands=false;
        }
        void BuildAiResult(RectTransform result)
        {
            winnerText.rectTransform.anchorMin=new Vector2(.1f,.76f);winnerText.rectTransform.anchorMax=new Vector2(.9f,.88f);
            aiResultInfo=Label(result,"",new Vector2(.15f,.45f),new Vector2(.85f,.74f),23,Color.white);
            ActionButton(result,"다시 대전",new Vector2(.33f,.26f),new Vector2(.67f,.33f),NewMatch);
            ActionButton(result,"난이도 선택",new Vector2(.33f,.16f),new Vector2(.67f,.23f),()=>{aiSelection?.Invoke();Destroy(gameObject);});
        }
        void ShowAiResult()
        {
            var p=engine.State.Players[0];winnerText.text=engine.State.Winner==0 ? "VICTORY" : "DEFEAT";
            string reason=engine.State.VictoryReason==VictoryReason.UnknownComplete ? "필드에 UNKNOWN 3장 배치" : engine.State.VictoryReason==VictoryReason.JokerJackpot ? "JOKER 도박 누적 3회 성공" : "상대 HP 0";
            aiResultInfo.text=$"{aiDifficulty.ToString().ToUpperInvariant()} AI · {MapInfo(engine.Config.Map).name}\n총 {engine.State.TurnNumber}턴 · 승리 조건: {reason}\n나의 UNKNOWN {p.UnknownProgress}/3 · JOKER 성공 {p.JokerSuccesses}/3";
        }
        IReadOnlyList<BattleEvent> HumanAiEvents(IReadOnlyList<BattleEvent> entries)
        {
            if(!aiMode)return entries;
            return entries.Where(e=>e.Owner==0 || e.Kind!=BattleEventKind.Combine && e.Kind!=BattleEventKind.HandRefill).Select(e=>EventViewData.For(e,0).ToEvent()).ToArray();
        }
        void TickAi()
        {
            if(!aiMode || engine.State.ActivePlayer!=1 || engine.State.IsOver || engine.State.TurnExpired || IsPaused || actionBusy || transitioning || passOverlay.activeSelf || Time.unscaledTime<aiNextAction)return;
            if(aiActionTurn!=engine.State.TurnNumber){aiActionTurn=engine.State.TurnNumber;aiActionCount=0;}
            AiMove move;
            if(aiActionCount>=AiActionLimit && !engine.NeedsTarget)move=new AiMove(BattleAction.EndTurn){Reason="행동 안전 상한"};
            else move=aiAgent.Decide(AiObservation.Capture(engine,1));
            // Planning consumes real time too. Never commit a move after its deadline.
            clock.Tick(Time.realtimeSinceStartupAsDouble,IsPaused);
            if(clock.Duration>0 && clock.Expired){engine.ExpireTurn(1);StartCoroutine(ActionPresentation(engine.DrainEvents()));return;}
            if(Debug.isDebugBuild)Debug.Log($"[UNKNOWN {aiDifficulty} AI] {move.Action} score={move.Score:F1} reason={move.Reason}; candidates="+string.Join(", ",aiAgent.LastCandidates.Take(6).Select(c=>$"{c.Action}:{c.Score:F1}")));
            aiActionCount++;aiNextAction=Time.unscaledTime+.45f;
            if(move.Action==BattleAction.EndTurn){StartCoroutine(TransitionTurn(false));return;}
            bool ok=move.Execute(engine,1);
            if(Debug.isDebugBuild)Debug.Log($"[UNKNOWN {aiDifficulty} AI] result={ok}, turn={engine.State.TurnNumber}"+(ok ? "" : " error="+engine.State.LastError));
            if(ok)StartCoroutine(ActionPresentation(engine.DrainEvents()));
            else if(!engine.NeedsTarget)StartCoroutine(TransitionTurn(false));
            else {engine.ExpireTurn(1);StartCoroutine(ActionPresentation(engine.DrainEvents()));}
        }
    }
}
