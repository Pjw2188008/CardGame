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
        [Serializable] sealed class HandSettingsReport {public bool completed;public List<string> passed=new List<string>(),failed=new List<string>(),errors=new List<string>();}
        public void RunHandSettingsChecks() {Invoke(nameof(BeginHandSettingsChecks),0.15f);}
        void BeginHandSettingsChecks() {StopAllCoroutines();StartCoroutine(HandSettingsChecks());}
        IEnumerator HandSettingsChecks()
        {
            var report=new HandSettingsReport();Action<bool,string> verify=(ok,name)=>{if(ok)report.passed.Add(name);else report.failed.Add(name);};
            Application.LogCallback listener=(m,s,t)=>{if(t==LogType.Error || t==LogType.Exception || t==LogType.Assert)report.errors.Add(m+"\n"+s);};Application.logMessageReceived+=listener;
            const string key="UNKNOWN.Options.v1";bool hadPrefs=PlayerPrefs.HasKey(key);string oldPrefs=PlayerPrefs.GetString(key);var saved=GamePreferences.Instance.Current.Copy();int oldVsync=QualitySettings.vSyncCount;
            try {
                checkRecipient=0;CheckFixture();for(int i=0;i<7;i++)CheckHand("Heart_2");engine.DrainEvents();engine.Draw(0,3);var entries=engine.DrainEvents();PrepareHandPlayback(entries);Refresh();yield return null;
                verify(engine.Config.HandLimit==8 && handViews.Count==7,"Eight-card limit hides only pending accepted draw, never burns");
                verify(entries.Count(e=>e.Kind==BattleEventKind.Discard && e.Secondary==1)==2,"Seven plus three produces two overflow discards");
                yield return animations.Play(entries);animations.ClearAction();Refresh();yield return new WaitForSecondsRealtime(0.3f);
                verify(handViews.Count==8 && handCountLabel.text=="HAND 8/8" && handCountLabel.color==gold,"Eight centered hand cards, full-count highlight, no opacity overlay");
                verify(handViews.Values.All(v=>v.GetComponent<CanvasGroup>().alpha==1) && pendingDraws.Count==0,"All eight cards remain readable after overflow");
                verify(toastText.Contains("2장 폐기") && graveMarkers[0].GetComponentInChildren<Text>().text=="묘지 2","Batched overflow feedback and grave count");
                verify(File.Exists(CaptureAttackFrame(1920,ownBoard,"HandEight",false)),"Rendered eight-card hand and small discard tools");
                var c=engine.State.Players[0].Hand[0];ToggleDiscard();verify(discardMode && discardID<0,"Discard button enters explicit selection mode");
                ToggleDiscard();verify(!engine.State.Players[0].ManualDiscardUsed && toastText.Contains("선택"),"Confirmation without a card cannot discard");
                DragBegin(c);verify(dragged==null,"Discard mode blocks summon/Joker dragging");ToggleCombinationMode();verify(!combinationMode,"Discard mode blocks combination selection");
                SelectHand(c);verify(discardID==c.InstanceID && handViews[c.InstanceID].GetComponent<Outline>().effectColor==new Color(1,0.45f,0.35f),"Selected discard card has a distinct highlight");
                verify(File.Exists(CaptureAttackFrame(1920,ownBoard,"ManualDiscard",false)),"Rendered selected manual discard");
                HandleBattleEscape();verify(!discardMode && !MenuOpen && engine.State.Players[0].Hand.Count==8,"ESC cancels discard without opening menu or changing hand");
                ToggleDiscard();SelectHand(c);ToggleDiscard();yield return DragWait();yield return null;
                verify(engine.State.Players[0].Hand.Count==7 && engine.State.Players[0].DiscardCount==3 && engine.State.Players[0].ManualDiscardUsed,"Confirmed discard animates to grave and reflows hand");
                ToggleDiscard();verify(!discardMode && toastText.Contains("이미"),"Second discard shows once-per-turn reason");
                CheckFixture();checkRecipient=0;var last=CheckHand("Joker_1");Refresh();ToggleDiscard();SelectHand(last);ToggleDiscard();yield return DragWait();verify(engine.State.Players[0].Hand.Count==0 && !engine.State.IsOver,"Last discard never refills immediately");
                CheckFixture();var card=CheckHand("Heart_2");Refresh();HandleBattleEscape();double before=clock.Remaining;yield return new WaitForSecondsRealtime(0.25f);
                verify(MenuOpen && Blocked() && clock.Remaining<before,"ESC menu blocks inputs while forty-second clock continues");
                var preferences=GamePreferences.Instance;preferences.Open();yield return null;
                var roomUI=FindFirstObjectByType<Unknown.Multiplayer.RoomTestUI>();var roomDocument=roomUI?.GetComponentInChildren<UnityEngine.UIElements.UIDocument>();
                verify(roomDocument!=null && !roomDocument.rootVisualElement.enabledInHierarchy,"Settings also block underlying title keyboard and pointer actions");
                verify(GamePreferences.IsOpen && !BeginCardDrag(card.InstanceID,handViews[card.InstanceID],DragData(handViews[card.InstanceID])),"Settings modal blocks underlying card input");
                verify(preferences.CaptureOptionsFrame("Library/AttackVisuals/Settings-1920.png"),"Rendered centered settings with audio sliders and screen options");
                preferences.Close();ToggleBattleMenu();verify(!MenuOpen && !Blocked(),"Closing settings and returning restores battle inputs");
                yield return null;verify(roomDocument!=null && roomDocument.rootVisualElement.enabledInHierarchy,"Closing settings restores title input routing");
                CheckFixture();engine=new BattleEngine(4,new BattleConfig{Map=BattleMap.Club,StartingHand=0,StartingMana=10,ShuffleDecks=false});foreach(var player in engine.State.Players)player.HandCards.Clear();engine.DrainEvents();CheckUnit("Heart_5",0);var summoned=CheckHand("Club_5");Refresh();
                verify(engine.Summon(0,summoned.InstanceID,1),"Club summon commits with extra draw and a new poker pair");var stacked=engine.DrainEvents();
                verify(Array.FindIndex(stacked.ToArray(),e=>e.Kind==BattleEventKind.Draw)<Array.FindIndex(stacked.ToArray(),e=>e.Kind==BattleEventKind.PokerChanged),"Reproduction orders draw before poker banner while hand view is removed");
                PrepareHandPlayback(stacked);StartCoroutine(ActionPresentation(stacked));yield return DragWait();
                verify(engine.State.Players[0].Poker.kind==PokerKind.OnePair && cardViews.TryGetValue(summoned.InstanceID,out var fieldView) && fieldView!=null && fieldView.parent==ownBoard,"Summon/draw/poker playback replaces destroyed hand references with a live field view");
                if(engine.NeedsTarget){RunCommand(()=>engine.SelectEffectTarget(0,0,0),"club target");yield return DragWait();}
                preferences.VerifyForEditor(verify);yield return null;
                var expected=preferences.Current.Copy();Destroy(preferences.gameObject);yield return null;preferences=GamePreferences.Instance;
                verify(Mathf.Approximately(preferences.Current.master,expected.master) && preferences.Current.mode==expected.mode && preferences.Current.width==expected.width && preferences.Current.vsync==expected.vsync,"New preferences instance reloads all saved options");
                report.completed=true;
            }finally {
                var p=GamePreferences.Instance;p.Close();p.SetAudio(saved.master,saved.bgm,saved.sfx);p.ApplyScreen(saved);QualitySettings.vSyncCount=oldVsync;
                if(hadPrefs)PlayerPrefs.SetString(key,oldPrefs);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
                checkRecipient=-1;Application.logMessageReceived-=listener;File.WriteAllText("Library/UNKNOWN-HandSettings-Checks.json",JsonUtility.ToJson(report,true));Debug.Log($"[UNKNOWN] Hand/settings checks: passed={report.passed.Count}, failed={report.failed.Count}, errors={report.errors.Count}");Destroy(gameObject);
            }
        }
    }
}
#endif
