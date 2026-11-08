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
        [Serializable] sealed class PokerCheckReport { public bool completed; public List<string> passed=new List<string>(),failed=new List<string>(),errors=new List<string>(); }
        public void RunPokerChecks() { Invoke(nameof(BeginPokerChecks),0.15f); }
        void BeginPokerChecks() { StopAllCoroutines();StartCoroutine(PokerChecks()); }
        void PokerFixture() {
            checkRecipient=0;CheckFixture();engine=new BattleEngine(4,new BattleConfig{StartingHand=0,StartingMana=10,ShuffleDecks=false},new CheckDice(0));
            foreach(var p in engine.State.Players)p.HandCards.Clear();engine.DrainEvents();ResetHandViews();
            if(pokerList!=null)pokerList.gameObject.SetActive(false);
        }
        IEnumerator PokerChecks()
        {
            var report=new PokerCheckReport();Action<bool,string> verify=(ok,name)=>{if(ok)report.passed.Add(name);else report.failed.Add(name);};
            Application.LogCallback listener=(m,s,t)=>{if(t==LogType.Error || t==LogType.Exception || t==LogType.Assert)report.errors.Add(m+"\n"+s);};Application.logMessageReceived+=listener;
            try {
                string[][] hands={new[]{"Heart_5","Spade_5"},new[]{"Heart_5","Spade_5","Club_9","Diamond_9","Unknown_J"},new[]{"Heart_5","Spade_5","Club_5"},new[]{"Heart_A","Spade_2","Club_3","Diamond_4","Heart_5"},new[]{"Heart_2","Heart_4","Heart_6","Heart_8","Heart_J"},new[]{"Heart_5","Spade_5","Club_5","Heart_9","Diamond_9"},new[]{"Heart_5","Spade_5","Club_5","Diamond_5"},new[]{"Spade_5","Spade_6","Spade_7","Spade_8","Spade_9"},new[]{"Heart_10","Heart_J","Heart_Q","Heart_K","Heart_A"}};
                for(int k=1;k<=9;k++) {
                    PokerFixture();for(int slot=0;slot<hands[k-1].Length;slot++)CheckUnit(hands[k-1][slot],0,slot);
                    CheckUnit("Heart_3",1);CheckUnit("Club_3",1,1);engine.ReconcilePoker();var events=engine.DrainEvents();Refresh();yield return null;
                    verify(pokerLabels[0].text.Contains(PokerSynergy.Name((PokerKind)k)) && pokerLabels[1].text.Contains("ONE PAIR"),"Independent own and enemy badges: "+(PokerKind)k);
                    verify(!pokerList.gameObject.activeSelf,"Reference starts collapsed: "+(PokerKind)k);
                    foreach(var c in engine.State.Players[0].Board.Where(c=>c!=null)) {
                        var rect=cardViews[c.InstanceID];bool member=engine.State.Players[0].Poker.members.Contains(c.InstanceID);
                        verify(rect.Find("Stat ATK").GetComponent<Text>().text==c.Attack.ToString() && rect.Find("Stat HP").GetComponent<Text>().text.Contains(">"+c.MaxHP+"</color>"),"Final field stat text: "+c.Definition.CardID+" / "+(PokerKind)k);
                        verify(rect.Find("Poker Icon").gameObject.activeSelf==member && rect.Find("Poker border").gameObject.activeSelf==member,"Only composition members have inner border/icon: "+c.Definition.CardID+" / "+(PokerKind)k);
                    }
                    if(k==6) {
                        verify(File.Exists(CaptureAttackFrame(1920,ownBoard,"PokerFullHouse",false)),"Rendered full-house battlefield");
                        pokerLabels[0].transform.parent.GetComponent<Button>().onClick.Invoke();
                        verify(pokerList.gameObject.activeSelf && pokerRows.Skip(1).All(t=>!string.IsNullOrEmpty(t.text)) && pokerRows[6].color==gold && pokerList.anchorMax.x<ownBoard.anchorMin.x,"Nine reference rows, active highlight, drawer outside field");
                        verify(File.Exists(CaptureAttackFrame(1920,ownBoard,"PokerReference",false)),"Rendered expanded reference");pokerList.gameObject.SetActive(false);
                        yield return animations.Play(events.Where(e=>e.Kind==BattleEventKind.PokerChanged).Take(1).ToArray());animations.ClearAction();
                    }
                    // Exercise Unity serializer and both recipient perspectives, without local re-evaluation.
                    var authority=engine;
                    foreach(int recipient in new[]{0,1}) {
                        checkRecipient=recipient;var wire=JsonUtility.FromJson<BattleSnapshot>(JsonUtility.ToJson(BattleSnapshot.For(authority,recipient,1,40,false,events)));
                        engine=BattleEngine.CreateView(wire);Refresh();yield return null;
                        verify(pokerLabels[recipient].text.Contains(PokerSynergy.Name((PokerKind)k)) && wire.players[1-recipient].hand.Length==0,"Wire projection preserves public synergy and hidden hand: P"+recipient+" / "+(PokerKind)k);
                    }
                }
                PokerFixture();var unit=CheckUnit("Heart_5",0);CheckUnit("Spade_5",0,1);CheckUnit("Club_5",0,2);engine.ReconcilePoker();engine.DrainEvents();
                unit.MapAttack=2;unit.MapHP=2;unit.Attack+=2;unit.MaxHP+=2;unit.CurrentHP+=2;unit.AbilityAttack=1;unit.Attack++;unit.JokerAttack=3;unit.Attack+=3;unit.CurrentHP--;Refresh();yield return null;
                ShowTooltip(unit,cardViews[unit.InstanceID],DragData(cardViews[unit.InstanceID]));
                verify(detail.text.Contains("포커 시너지") && detail.text.Contains("맵 효과") && detail.text.Contains("카드 특수 능력") && detail.text.Contains("JOKER 효과") && detail.text.Contains("받은 피해"),"Tooltip explains each applied source and damage");
                verify(cardViews[unit.InstanceID].Find("Stat ATK").GetComponent<Text>().color==statUp && cardViews[unit.InstanceID].Find("Stat HP").GetComponent<Text>().text.Contains(ColorUtility.ToHtmlStringRGB(statDown)),"Strengthened attack green; wounded current HP red");
                verify(File.Exists(CaptureAttackFrame(1920,ownBoard,"PokerStats",false)),"Rendered layered stat tooltip");HideTooltip();
                engine.State.Players[0].Slots[1].CurrentHP=0;engine.ReconcilePoker();var changed=engine.DrainEvents();Refresh();
                verify(engine.State.Players[0].Poker.kind==PokerKind.OnePair && unit.PokerAttack==2 && unit.PokerHP==0 && unit.MapAttack==2 && unit.JokerAttack==3 && unit.AbilityAttack==1,"Triple downgrade removes only poker bonus");
                verify(changed.Any(e=>e.Kind==BattleEventKind.PokerChanged && e.Amount==(int)PokerKind.Triple && e.Secondary==(int)PokerKind.OnePair),"Downgrade emits one changed notification");
                yield return animations.Play(changed.Where(e=>e.Kind==BattleEventKind.PokerChanged).ToArray());animations.ClearAction();
                engine.State.Players[0].Slots[2].CurrentHP=0;engine.ReconcilePoker();changed=engine.DrainEvents();Refresh();
                verify(unit.PokerAttack==0 && unit.Attack==unit.Definition.Attack+6 && pokerLabels[0].text.Contains("NONE"),"Lost synergy restores remaining permanent stats and badge");
                yield return animations.Play(changed.Where(e=>e.Kind==BattleEventKind.PokerChanged).ToArray());animations.ClearAction();
                PokerFixture();var expensive=CheckHand("Heart_K");engine.State.Players[0].CurrentMana=0;Refresh();yield return null;
                verify(handViews[expensive.InstanceID].GetComponent<CanvasGroup>().alpha==1,"Unaffordable hand card has full opacity");
                DragBegin(expensive);verify(dragged==expensive && dragGhost!=null,"Zero-mana card begins real drag");MoveCardDrag(DragData(UnitDrop(0,0).transform as RectTransform));DragDrop(UnitDrop(0,0));yield return null;
                verify(engine.State.Players[0].CurrentMana==0 && engine.State.Players[0].Hand.Contains(expensive) && engine.State.Players[0].Board.All(c=>c==null),"Rejected summon spends nothing and keeps card in hand");
                verify(toastText.Contains("필요 마나: "+expensive.Definition.ManaCost) && toastText.Contains("현재 마나: 0"),"Rejection notice contains actual required/current mana");
                verify(File.Exists(CaptureAttackFrame(1920,ownBoard,"ManaRejected",false)),"Rendered mana rejection and return animation");
                yield return new WaitForSecondsRealtime(0.3f);float oldUntil=toastUntil;DragBegin(expensive);DragDrop(UnitDrop(0,0));yield return new WaitForSecondsRealtime(0.3f);
                verify(toastUntil>oldUntil && dragged==null && dragGhost==null && handViews[expensive.InstanceID].GetComponent<CanvasGroup>().alpha==1,"Repeated rejection refreshes single notice and restores hand");
                verify(dragLayer.Cast<Transform>().All(t=>t==attackVisualLayer),"Return ghosts are destroyed after 0.22 seconds; only permanent attack feedback remains");
                yield return new WaitForSecondsRealtime(1.3f);verify(Time.unscaledTime>toastUntil,"Mana notice expires after 1.5 seconds");
                DragBegin(expensive);DragDrop(UnitDrop(1,0));verify(toastText.Contains("위치"),"Invalid drop gives location reason");
                ResetHandViews();Refresh();yield return null;verify(dragLayer.Cast<Transform>().All(t=>t==attackVisualLayer),"Hand reset removes an in-flight return ghost");
                yield return new WaitForSecondsRealtime(0.3f);
                engine.State.ActivePlayer=1;Refresh();DragBegin(expensive);verify(dragged==null && toastText.Contains("상대방의 턴"),"Wrong turn gives actual action reason");
                report.completed=true;
            } finally {checkRecipient=-1;Application.logMessageReceived-=listener;File.WriteAllText("Library/UNKNOWN-Poker-Checks.json",JsonUtility.ToJson(report,true));Debug.Log($"[UNKNOWN] Poker checks: passed={report.passed.Count}, failed={report.failed.Count}, errors={report.errors.Count}");Destroy(gameObject);}
        }
    }
}
#endif
