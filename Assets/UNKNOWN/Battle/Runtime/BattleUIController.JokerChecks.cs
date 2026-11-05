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
    public sealed partial class BattleUIController
    {
        [Serializable] sealed class JokerCheckReport { public bool completed; public List<string> passed=new List<string>(),failed=new List<string>(),errors=new List<string>(); }
        public void RunJokerChecks() { Invoke(nameof(BeginJokerChecks),0.15f); }
        void BeginJokerChecks() { StopAllCoroutines(); StartCoroutine(JokerChecks()); }
        void JokerFixture()
        {
            checkRecipient=0; CheckFixture();
            engine=new BattleEngine(42,new BattleConfig{Map=BattleMap.JokerGame,StartingHand=0,StartingMana=10,MapEvents=new JokerEventDefinition[0],ShuffleDecks=false},new CheckDice(0));
            foreach(var p in engine.State.Players)p.HandCards.Clear(); engine.DrainEvents(); engine.ResetMapEventForDebug();
            presentedMapEvents.Clear(); ResetHandViews();
        }
        IEnumerator JokerChecks()
        {
            var report=new JokerCheckReport(); Action<bool,string> verify=(ok,name)=> { if(ok)report.passed.Add(name);else report.failed.Add(name); };
            Application.LogCallback listener=(m,s,t)=> { if(t==LogType.Error || t==LogType.Exception || t==LogType.Assert)report.errors.Add(m); };
            Application.logMessageReceived+=listener;
            try
            {
                var data=JokerMapSettings.LoadRules(); verify(data.Length==14 && data.Sum(e=>e.weight)==100,"Editable map asset has fourteen events and total weight one hundred");
                verify(data.Select(e=>e.weight).SequenceEqual(JokerEventDefinition.Defaults().Select(e=>e.weight)),"Asset weights match shipped rule defaults");
                foreach(var definition in data)
                {
                    JokerFixture(); var unit=CheckUnit("Heart_K",0); CheckHand("Joker_1"); CheckHand("Heart_A"); CheckHand("Unknown_Q"); CheckHand("Club_5",1);
                    if(definition.kind==JokerEventKind.Mana)engine.State.Players[0].CurrentMana=3;
                    Refresh(); verify(engine.ResolveMapEvent(0,definition),"Map event accepted: "+definition.kind);
                    var entries=engine.DrainEvents(); PrepareHandPlayback(entries); Refresh(); queuedTurnEvents=entries;
                    StartCoroutine(BeginTurnPresentation()); yield return new WaitForSecondsRealtime(0.2f);
                    verify(transitioning && Blocked() && clock.Remaining>39.9,"Event presentation freezes forty-second timer: "+definition.kind);
                    yield return DragWait();
                    verify(presentedMapEvents.Count==1 && presentedMapEvents.Contains(engine.State.TurnNumber),"Map banner presented exactly once: "+definition.kind);
                    verify(pendingDraws.Count==0 && handViews.Count==engine.State.Players[0].Hand.Count,"Map hand and sequential draw effects finish consistently: "+definition.kind);
                }
                JokerFixture(); CheckUnit("Heart_K",0); CheckHand("Joker_1"); var secret=CheckHand("Unknown_K",1); Refresh();
                engine.ResolveMapEvent(0,data.Single(e=>e.kind==JokerEventKind.Reveal)); engine.DrainEvents(); Refresh();
                verify(backViews.Count==1 && backViews[0].Find("Revealed Face")!=null,"Reveal shows exactly the selected opponent card face");
                var face=(RectTransform)backViews[0].Find("Revealed Face"); ShowTooltip(secret,face,DragData(face));
                verify(detail.text.Contains(secret.Definition.CardName) && detail.text.Contains("MP") && detail.text.Contains(secret.Definition.Description),"Revealed face tooltip shows name, mana and ability");
                verify(File.Exists(CaptureAttackFrame(1920,ownBoard,"JokerMap",false)),"Rendered purple map, own jackpot progress and revealed opponent card");
                engine.State.Players[1].HandCards.Remove(secret); RefreshOpponentReveal();
                verify(!face.gameObject.activeSelf && !tooltip.gameObject.activeSelf,"Removing the revealed card immediately hides face and tooltip");
                verify(unknownLabels[0].text.StartsWith("JOKER JACKPOT") && unknownLabels[1]==null,"Joker map replaces only own progression HUD");
                var network=FindFirstObjectByType<Unknown.Multiplayer.NetworkBattleController>(); online=network;
                if(network!=null)verify(!CanLocalMapDebug && !DebugGiveJoker(),"Local debug controls are blocked on an online battle"); online=null;
                foreach(int recipient in new[]{0,1})
                {
                    JokerFixture(); for(int i=0;i<3;i++) { var u=CheckUnit("Heart_K",0,i); var j=CheckHand("Joker_1"); engine.Gamble(0,j.InstanceID,u.InstanceID); }
                    var snapshot=BattleSnapshot.For(engine,recipient,5,0,false,engine.DrainEvents()); checkRecipient=recipient; engine=BattleEngine.CreateView(snapshot); ResetHandViews(); Refresh(); ShowWinner();
                    verify(winnerText.text==(recipient==0 ? "VICTORY — JOKER JACKPOT" : "DEFEAT — OPPONENT HIT JACKPOT"),"Jackpot terminal result is correct for recipient "+recipient);
                    verify(!endTurn.interactable && !combineButton.interactable && resultOverlay.activeSelf,"Jackpot stops new actions for recipient "+recipient);
                }
                checkRecipient=0; CheckFixture(); Refresh();
                verify((mapDecor==null || !mapDecor.gameObject.activeSelf) && unknownLabels[0].text.StartsWith("내 UNKNOWN"),"Normal map restores old background and UNKNOWN progression");
                verify(normalMapName!=null && normalMapName.gameObject.activeSelf,"Normal battlefield name remains visible during combat");
                verify(typeof(Unknown.Multiplayer.NetworkBattleController).GetMethod("SelectMap")==null,"Manual Host map selection API is removed");
                foreach(var map in (BattleMap[])Enum.GetValues(typeof(BattleMap)))
                {
                    mapIntroPresented=false; int before=MapIntroCount; actionBusy=true; clock.Begin(40,Time.realtimeSinceStartupAsDouble);
                    double began=Time.realtimeSinceStartupAsDouble; StartCoroutine(PresentRandomMap(map)); yield return new WaitForSecondsRealtime(0.25f);
                    verify(IsMapIntroActive && Blocked(),"Random map intro blocks input: "+map);
                    yield return new WaitForSecondsRealtime(0.8f);
                    var labels=mapIntro.GetComponentsInChildren<Text>();
                    verify(labels.Any(t=>t.text==MapTitle(map)) && labels.Any(t=>t.text==MapDescription(map)),"Intro displays authoritative final name and map win conditions: "+map);
                    double renderStart=Time.realtimeSinceStartupAsDouble;
                    verify(File.Exists(CaptureAttackFrame(1920,ownBoard,"RandomMap-"+map,false)),"Rendered random map result: "+map);
                    double renderTime=Time.realtimeSinceStartupAsDouble-renderStart;
                    var heading=labels.Single(t=>t.text=="RANDOM MAP");
                    verify(heading.canvasRenderer.GetMesh()!=null && heading.canvasRenderer.GetMesh().vertexCount>0,"RANDOM MAP heading has rendered geometry: "+map);
                    while(IsMapIntroActive)yield return null;
                    verify(Time.realtimeSinceStartupAsDouble-began>=1.9 && Time.realtimeSinceStartupAsDouble-began<2.4+renderTime,"Random reveal lasts about two seconds: "+map);
                    yield return PresentRandomMap(map); verify(MapIntroCount==before+1,"Later snapshots cannot repeat map reveal: "+map); actionBusy=false;
                }
                report.completed=true;
            }
            finally
            {
                online=null; checkRecipient=-1; Application.logMessageReceived-=listener;
                File.WriteAllText("Library/UNKNOWN-JokerMap-Checks.json",JsonUtility.ToJson(report,true));
                Debug.Log($"[UNKNOWN] Joker map checks: passed={report.passed.Count}, failed={report.failed.Count}, errors={report.errors.Count}"); Destroy(gameObject);
            }
        }
    }
}
#endif
