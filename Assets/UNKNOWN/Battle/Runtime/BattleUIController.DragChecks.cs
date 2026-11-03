#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Unknown.Battle
{
    public sealed partial class BattleUIController
    {
        [Serializable] sealed class DragReport
        {
            public bool completed;
            public List<string> passed=new List<string>(), failed=new List<string>(), errors=new List<string>();
        }
        public void RunDragChecks() { Invoke(nameof(BeginDragChecks),0.15f); }
        public void PreviewDragUI() { Invoke(nameof(SetupDragPreview),0.15f); }
        void SetupDragPreview()
        {
            StopAllCoroutines(); CheckFixture();
            CheckHand("Joker_1"); CheckHand("Heart_A"); CheckHand("Spade_Q"); CheckHand("Diamond_2"); CheckHand("Club_5");
            CheckUnit("Diamond_3",0,1); CheckUnit("Heart_K",1,2);
            for(int i=0;i<6;i++) CheckHand("Club_2",1);
            Refresh();
        }
        void BeginDragChecks() { StopAllCoroutines(); StartCoroutine(DragChecks()); }
        PointerEventData DragData(RectTransform rect)
        { return new PointerEventData(EventSystem.current) { position=RectTransformUtility.WorldToScreenPoint(null,rect.position) }; }
        void DragBegin(CardInstance card)
        { var view=cardViews[card.InstanceID]; ExecuteEvents.Execute(view.gameObject,DragData(view),ExecuteEvents.beginDragHandler); }
        void DragDrop(CardDropZone zone)
        { ExecuteEvents.Execute(zone.gameObject,DragData(zone.transform as RectTransform),ExecuteEvents.dropHandler); }
        CardDropZone UnitDrop(int owner,int slot) => (owner==ViewPlayer ? ownBoard : enemyBoard).Cast<Transform>().Where(t=>t.gameObject.activeSelf).ElementAt(slot).GetComponent<CardDropZone>();
        IEnumerator DragWait() { double until=Time.realtimeSinceStartupAsDouble+8; while((actionBusy || transitioning) && Time.realtimeSinceStartupAsDouble<until)yield return null; if(actionBusy || transitioning)Debug.LogError("Drag action did not finish"); }
        IEnumerator DragChecks()
        {
            yield return null;
            var report=new DragReport();
            Action<bool,string> verify=(ok,name)=> { if(ok)report.passed.Add(name); else report.failed.Add(name); };
            Application.LogCallback listener=(m,s,t)=> { if(t==LogType.Error || t==LogType.Exception)report.errors.Add(m); };
            Application.logMessageReceived+=listener;
            try
            {
                CheckFixture(); var unit=CheckUnit("Unknown_K",0); var enemy=CheckUnit("Heart_K",1); var joker=CheckHand("Joker_1"); Refresh(); yield return null;
                verify(battlefield.anchorMax==Vector2.one && interactionRoot.Find("Controls and Log")==null,"Fixed right panel removed; battlefield uses full canvas");
                DragBegin(joker); verify(dragged==joker && dragGhost!=null,"Unity BeginDrag creates Joker ghost");
                verify(engine.State.Players[0].CurrentMana==10 && engine.State.Players[0].Hand.Contains(joker),"Dragging alone changes no game state");
                HighlightDrop(UnitDrop(0,0)); verify(hoveredOutline!=null && hoveredOutline.effectColor==gold,"Valid allied UNKNOWN target is gold");
                EndCardDrag(); verify(dragged==null && originalGroup==null && engine.State.Players[0].Hand.Contains(joker),"Cancel restores original card without spending");
                DragBegin(joker); DragDrop(UnitDrop(1,0)); verify(engine.State.Players[0].CurrentMana==10 && !unit.JokerUsed,"Enemy target rejected without charging");
                DragBegin(joker); DragDrop(UnitDrop(0,4)); verify(engine.State.Players[0].Hand.Contains(joker),"Empty slot rejected");
                engine.State.Players[0].CurrentMana=1; DragBegin(joker); DragDrop(UnitDrop(0,0)); verify(!unit.JokerUsed && engine.State.Players[0].CurrentMana==1,"Insufficient mana rejected");
                engine.State.Players[0].CurrentMana=10; DragBegin(joker); var drop=UnitDrop(0,0); DragDrop(drop); DragDrop(drop);
                verify(engine.State.Players[0].CurrentMana==8 && unit.JokerUsed && !engine.State.Players[0].Hand.Contains(joker),"Joker drop commits once and spends exactly 2 MP");
                yield return DragWait(); verify(unit.Attack==21 && unit.MaxHP==21,"Deterministic Host-style success uses existing 1.5x rule");
                joker=CheckHand("Joker_2"); Refresh(); yield return null; DragBegin(joker); DragDrop(UnitDrop(0,0)); verify(engine.State.Players[0].Hand.Contains(joker),"Already gambled unit rejects a second Joker");
                CheckFixture(1); unit=CheckUnit("Diamond_2",0); joker=CheckHand("Joker_1"); Refresh(); yield return null;
                DragBegin(joker); DragDrop(UnitDrop(0,0)); yield return DragWait(); verify(engine.State.Players[0].Board[0]==null && engine.State.Players[0].CurrentMana==8,"Failure destroys target and still charges exactly 2 MP");
                CheckFixture(); var ace=CheckHand("Heart_A"); var face=CheckHand("Spade_Q"); var replacement=CheckHand("Club_A"); var number=CheckHand("Diamond_2"); CheckHand("Heart_K",1); CheckHand("Club_Q",1); Refresh(); yield return null;
                DragBegin(number); verify(dragged==null,"Number cards cannot become combination drag materials");
                DragBegin(face); DragDrop(aceSlot.GetComponent<CardDropZone>()); verify(aceID<0,"A slot rejects face card");
                DragBegin(ace); DragDrop(aceSlot.GetComponent<CardDropZone>()); DragBegin(face); DragDrop(faceSlot.GetComponent<CardDropZone>());
                verify(aceID==ace.InstanceID && faceID==face.InstanceID && engine.State.Players[0].Hand.Count==4,"Both materials remain in hand before confirmation");
                verify(selectionInfo.text.Contains("QUEEN") && combineButton.interactable,"Correct UNKNOWN preview enables confirmation");
                DragBegin(replacement); DragDrop(aceSlot.GetComponent<CardDropZone>()); verify(aceID==replacement.InstanceID && engine.State.Players[0].Hand.Count==4,"A material can be replaced without consumption");
                aceSlot.GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); verify(aceID<0 && !combineButton.interactable,"Slot click unregisters material");
                CancelInteractionSelection(); verify(aceID<0 && faceID<0,"ESC cancellation path clears both slots");
                DragBegin(ace); DragDrop(aceSlot.GetComponent<CardDropZone>()); DragBegin(face); DragDrop(faceSlot.GetComponent<CardDropZone>());
                Combine(); yield return DragWait(); verify(engine.State.Players[0].UnknownCreated==1 && engine.State.Players[0].Hand.Any(c=>c.Definition.CardID=="Unknown_Q"),"Confirmation alone consumes materials and creates UNKNOWN");
                verify(engine.State.Players[0].CurrentMana==10,"Combination remains free");
                var card=engine.State.Players[0].Hand[0]; var view=cardViews[card.InstanceID];
                ShowTooltip(card,view,new PointerEventData(EventSystem.current){position=new Vector2(Screen.width-1,Screen.height-1)});
                var r=interactionRoot.rect; var half=tooltip.sizeDelta/2;
                verify(tooltip.gameObject.activeSelf && tooltip.anchoredPosition.x+half.x<=r.xMax && tooltip.anchoredPosition.y+half.y<=r.yMax,"Hover tooltip clamps inside screen edges");
                HideTooltip(); verify(!tooltip.gameObject.activeSelf,"Hover exit hides tooltip");
                verify(opponentHand.Cast<Transform>().All(t=>t.GetComponent<CardHover>()==null && t.GetComponent<CardDrag>()==null),"Opponent hand backs expose no hover or drag interaction");
                yield return animations.Play(new[]{new BattleEvent(BattleEventKind.Draw,1,target:-1),new BattleEvent(BattleEventKind.Draw,1,target:-1),new BattleEvent(BattleEventKind.Draw,1,target:-1)});
                animations.ClearAction(); yield return null;
                verify(animations.ActiveCardGhostCount==0,"Repeated anonymous draws leave no oversized orphan card");
                CheckFixture();
                for(int count=1;count<=10;count++)
                {
                    CheckHand("Club_2"); CheckHand("Heart_2",1); Refresh(); yield return new WaitForSecondsRealtime(0.3f);
                    var ownCenters=handViews.Values.Select(v=>interactionRoot.InverseTransformPoint(v.TransformPoint(v.rect.center)).x).ToArray();
                    var enemyCenters=backViews.Select(v=>interactionRoot.InverseTransformPoint(v.TransformPoint(v.rect.center)).x).ToArray();
                    verify(Mathf.Abs((ownCenters.Min()+ownCenters.Max())/2)<1 && Mathf.Abs((enemyCenters.Min()+enemyCenters.Max())/2)<1,"Both hands centered with "+count+" cards");
                }
                var retained=engine.State.Players[0].Hand[0]; var retainedView=handViews[retained.InstanceID]; Refresh();
                verify(handViews[retained.InstanceID]==retainedView,"Refresh preserves hand card root identity");
                CheckFixture(); var existing=CheckHand("Club_2"); Refresh(); var existingView=handViews[existing.InstanceID];
                engine.Draw(0,2); engine.Draw(1,2); var drawEvents=engine.DrainEvents(); PrepareHandPlayback(drawEvents); Refresh();
                verify(handViews.Count==1 && backViews.Count==0,"Pending draws never appear early in either hand");
                yield return animations.Play(drawEvents); Refresh(); yield return new WaitForSecondsRealtime(0.3f);
                verify(handViews.Count==3 && backViews.Count==2 && handViews[existing.InstanceID]==existingView,"Sequential draws retain existing views and match authoritative counts");
                verify(handViews.Values.All(v=>v.Find("Drawing card back")==null) && pendingDraws.Count==0 && pendingEnemyDraws==0,"Draw flips leave no cover, ghost or pending card");
                verify(unknownLabels[1]==null && unknownPips[1].All(p=>p==null),"Opponent UNKNOWN HUD is never created");
                report.completed=true;
            }
            finally
            {
                Application.logMessageReceived-=listener;
                File.WriteAllText("Library/UNKNOWN-DragUI-Checks.json",JsonUtility.ToJson(report,true));
                Debug.Log("[UNKNOWN] Drag UI checks: passed="+report.passed.Count+", failed="+report.failed.Count+", errors="+report.errors.Count);
                Destroy(gameObject);
            }
        }
    }
}
#endif
