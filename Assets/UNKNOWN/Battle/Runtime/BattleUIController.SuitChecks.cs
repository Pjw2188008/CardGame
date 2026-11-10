#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace Unknown.Battle
{
    public sealed partial class BattleUIController
    {
        [Serializable] sealed class SuitCheckReport { public bool completed; public List<string> passed=new List<string>(),failed=new List<string>(),errors=new List<string>(); }
        public void RunSuitChecks() { Invoke(nameof(BeginSuitChecks),0.15f); }
        void BeginSuitChecks() { StopAllCoroutines(); StartCoroutine(SuitChecks()); }
        IEnumerator SuitChecks()
        {
            var report=new SuitCheckReport(); Action<bool,string> verify=(ok,name)=>{if(ok)report.passed.Add(name);else report.failed.Add(name);};
            Application.LogCallback listener=(m,s,t)=>{if(t==LogType.Error || t==LogType.Exception || t==LogType.Assert)report.errors.Add(m);}; Application.logMessageReceived+=listener;
            try
            {
                var definitions=BattleMapSettings.LoadRules(); verify(definitions.Length==6 && definitions.Sum(d=>d.weight)==10000,"Map asset contains five weighted maps and preserved Normal");
                var backgroundNames=new[]{"O_MAP","J_MAP","D_MAP","S_MAP","H_MAP","C_MAP"};
                foreach(BattleMap map in Enum.GetValues(typeof(BattleMap))) {
                    checkRecipient=0;CheckFixture();
                    var authority=new BattleEngine(4,new BattleConfig{Map=map,MapRules=definitions,StartingHand=0});
                    var expected=UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Map/"+backgroundNames[(int)map]+".png");
                    foreach(int recipient in new[]{0,1}) {
                        checkRecipient=recipient;engine=BattleEngine.CreateView(BattleSnapshot.For(authority,recipient,1,0,false,new BattleEvent[0]));Refresh();
                        yield return null;Canvas.ForceUpdateCanvases();
                        verify(expected!=null && mapBackground.texture==expected && mapBackground.gameObject.activeSelf,"Snapshot selects exact background for recipient "+recipient+": "+map);
                    }
                    verify(!mapBackground.raycastTarget && mapBackground.transform.GetSiblingIndex()==0,"Background stays behind UI and accepts no pointer input: "+map);
                    var rect=mapBackground.rectTransform.rect;var parent=battlefield.rect;
                    verify(rect.width>=parent.width-.5f && rect.height>=parent.height-.5f && Mathf.Abs(rect.width/rect.height-(float)expected.width/expected.height)<.01f,"Background covers viewport without stretching: "+map);
                    verify(File.Exists(CaptureAttackFrame(1920,ownBoard,"Background-"+map,false)),"Rendered supplied map background: "+map);
                }
                foreach(var map in new[]{BattleMap.Diamond,BattleMap.Spade,BattleMap.Heart,BattleMap.Club})
                {
                    checkRecipient=0; CheckFixture();
                    engine=new BattleEngine(4,new BattleConfig{Map=map,MapRules=definitions,StartingHand=0,StartingMana=10,ShuffleDecks=false},new CheckDice(0));
                    foreach(var p in engine.State.Players)p.HandCards.Clear(); engine.DrainEvents(); presentedMapEvents.Clear(); ResetHandViews();
                    var info=MapInfo(map); var card=CheckHand(info.suit+"_2"); var enemy=CheckUnit("Heart_K",1); Refresh();
                    verify(engine.Summon(0,card.InstanceID,0),"Matching suit summon succeeds: "+map);
                    int safety=0; while(engine.NeedsTarget && safety++<10) { bool found=false; for(int owner=0;owner<2 && !found;owner++)for(int slot=0;slot<5 && !found;slot++)if(engine.IsValidEffectTarget(owner,slot)){engine.SelectEffectTarget(0,owner,slot);found=true;} }
                    var entries=engine.DrainEvents(); PrepareHandPlayback(entries); StartCoroutine(ActionPresentation(entries)); yield return DragWait();
                    verify(cardViews.ContainsKey(card.InstanceID) && cardViews[card.InstanceID].parent==ownBoard,"Summon bonus and card ability finish in own field: "+map);
                    verify(pendingDraws.Count==0 && handViews.Count==engine.State.Players[0].Hand.Count,"Map summon draw animation and hand projection finish: "+map);
                    engine.ResetSuitEffectForDebug(); engine.State.Players[0].HP=25; engine.State.Players[0].CurrentMana=3;
                    enemy.CurrentHP=1; enemy.Attack=1; Refresh();
                    verify(engine.ResolveSuitMapTurn(0) && !engine.ResolveSuitMapTurn(0),"Map turn resolves once: "+map);
                    entries=engine.DrainEvents(); PrepareHandPlayback(entries); queuedTurnEvents=entries; StartCoroutine(BeginTurnPresentation()); yield return new WaitForSecondsRealtime(0.2f);
                    verify(transitioning && Blocked() && clock.Remaining>39.9,"Map turn animation blocks input and freezes forty-second clock: "+map);
                    yield return DragWait();
                    verify(presentedMapEvents.Count==1,"Suit effect banner presents once: "+map);
                    verify(normalMapName.gameObject.activeSelf && normalMapName.text.Contains(info.name) && mapStatus.text==info.Effects,"Compact map name and effects visible: "+map);
                    ShowMapTooltip(DragData(normalMapName.rectTransform)); verify(tooltip.gameObject.activeSelf && detail.text.Contains(info.Effects),"Hover details include configured map effects: "+map); HideTooltip();
                    verify(unknownLabels[0].text.StartsWith("내 UNKNOWN") && unknownLabels[1]==null,"Suit map shows only own UNKNOWN progression: "+map);
                    verify(File.Exists(CaptureAttackFrame(1920,ownBoard,"SuitMap-"+map,false)),"Rendered themed battlefield: "+map);
                    foreach(int recipient in new[]{0,1}) {
                        var snapshot=BattleSnapshot.For(engine,recipient,1,0,false,new BattleEvent[0]); var view=BattleEngine.CreateView(snapshot);
                        verify(view.SuitEffect.detail==engine.SuitEffect.detail && view.State.Players[0].HP==engine.State.Players[0].HP && view.State.Players[0].CurrentMana==engine.State.Players[0].CurrentMana,"Resolved map effect is identical for recipient "+recipient+": "+map);
                    }
                }
                online=FindFirstObjectByType<Unknown.Multiplayer.NetworkBattleController>();
                if(online!=null)verify(!DebugSuitSummon() && !DebugSuitTurn(),"Suit debug controls are disabled online"); online=null;
                report.completed=true;
            }
            finally { online=null;checkRecipient=-1;Application.logMessageReceived-=listener;File.WriteAllText("Library/UNKNOWN-SuitMap-Checks.json",JsonUtility.ToJson(report,true));Debug.Log($"[UNKNOWN] Suit map checks: passed={report.passed.Count}, failed={report.failed.Count}, errors={report.errors.Count}");Destroy(gameObject); }
        }
    }
}
#endif
