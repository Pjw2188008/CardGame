#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    public sealed partial class BattleUIController
    {
        [Serializable]sealed class RedesignUIResult{public bool completed;public List<string> passed=new List<string>(),failed=new List<string>(),errors=new List<string>();}
        public void RunRedesignUIChecks(){Invoke(nameof(BeginRedesignUI),.2f);}
        void BeginRedesignUI(){StopAllCoroutines();StartCoroutine(RedesignUI());}
        IEnumerator RedesignUI()
        {
            var report=new RedesignUIResult();Action<bool,string> verify=(ok,name)=>{if(ok)report.passed.Add(name);else report.failed.Add(name);};
            Application.LogCallback listener=(message,stack,type)=>{if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)report.errors.Add(message);};Application.logMessageReceived+=listener;
            try {
                checkRecipient=0;CheckFixture();engine=new BattleEngine(32,new BattleConfig{EnablePoker=false,StartingHand=0,StartingMana=10,ShuffleDecks=false});
                foreach(var player in engine.State.Players)player.HandCards.Clear();engine.DrainEvents();
                var exchange=CheckHand("Club_J");var material=CheckHand("Heart_2");Refresh();yield return null;
                DragBegin(exchange);DragDrop(UnitDrop(0,0));yield return DragWait();yield return null;RefreshExchangeTools();
                verify(engine.NeedsExchange && exchangeSkip.gameObject.activeSelf && exchangeSkip.interactable,"Exchange prompt exposes skip button after summon animation");
                verify(!BeginCardDrag(material.InstanceID,handViews[material.InstanceID],DragData(handViews[material.InstanceID])),"Exchange blocks ordinary summon drag");
                verify(ClickCheckUI(handViews[material.InstanceID]),"Actual hand pointer click chooses exchange discard");yield return DragWait();yield return null;
                verify(!engine.NeedsTarget && engine.State.Players[0].CurrentMana==6 && !engine.State.Players[0].ManualDiscardUsed,"Actual exchange applies draw then mana through shared command");
                CheckFixture();engine=new BattleEngine(32,new BattleConfig{EnablePoker=false,StartingHand=0,StartingMana=10,ShuffleDecks=false});foreach(var player in engine.State.Players)player.HandCards.Clear();engine.DrainEvents();
                exchange=CheckHand("Club_J");material=CheckHand("Heart_2");Refresh();DragBegin(exchange);DragDrop(UnitDrop(0,0));yield return DragWait();yield return null;RefreshExchangeTools();
                verify(ClickCheckUI(exchangeSkip.GetComponent<RectTransform>()),"Actual skip button click reaches exchange command");yield return DragWait();
                verify(!engine.NeedsTarget && engine.State.Players[0].CurrentMana==6 && engine.State.Players[0].Hand.Count==1,"Skipping keeps material, commits original summon and follows with mana");
                CheckFixture();engine=new BattleEngine(32,new BattleConfig{EnablePoker=false,StartingHand=0,StartingMana=10});foreach(var player in engine.State.Players)player.HandCards.Clear();engine.DrainEvents();
                var first=CheckUnit("Heart_K",1,0);first.CurrentHP=4;var survivor=CheckUnit("Heart_K",1,1);var king=CheckHand("Spade_K");Refresh();DragBegin(king);DragDrop(UnitDrop(0,0));yield return DragWait();yield return null;
                verify(ClickCheckUI(cardViews[first.InstanceID]),"Actual first Spade K target click deals damage");yield return DragWait();yield return null;
                verify(engine.NeedsTarget && engine.IsValidEffectTarget(1,1),"UI highlights fresh living weakening target after first target dies");
                verify(ClickCheckUI(cardViews[survivor.InstanceID]),"Actual second target click resolves weakening");yield return DragWait();
                verify(survivor.Attack==2 && !engine.NeedsTarget,"Spade K UI finishes without stale target deadlock");
                verify(File.Exists(CaptureAttackFrame(1920,ownBoard,"Redesign",false)),"Rendered redesigned card text and field");
                report.completed=true;
            }finally{Application.logMessageReceived-=listener;File.WriteAllText("Library/CardRedesign-UI.json",JsonUtility.ToJson(report,true));Destroy(gameObject);}
        }
    }
}
#endif
