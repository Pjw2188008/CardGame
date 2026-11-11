using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace Unknown.Battle
{
    public sealed partial class BattleUIController
    {
        RectTransform interactionRoot,dragLayer,tooltip,aceSlot,faceSlot;
        Text aceSlotText,faceSlotText;
        CardInstance dragged;
        RectTransform dragGhost;
        CanvasGroup originalGroup;
        float originalAlpha;
        CardDropZone hoveredDrop;
        Outline hoveredOutline;
        Color originalOutline;
        bool addedOutline;
        float toastUntil;
        string toastText="";
        int layoutWidth,layoutHeight;
        bool combinationMode;
        int combinationTurn;
        Button combinationModeButton;
        readonly Color combinationPurple=new Color(0.76f,0.38f,1);
        void BuildCombinationSlots()
        {
            aceSlot=Box(battlefield,"UNKNOWN A Slot",new Vector2(0.34f,0.565f),new Vector2(0.425f,0.611f),panel);
            faceSlot=Box(battlefield,"UNKNOWN Face Slot",new Vector2(0.575f,0.565f),new Vector2(0.66f,0.611f),panel);
            aceSlotText=Label(aceSlot,"A",Vector2.zero,Vector2.one,21,Color.white);
            faceSlotText=Label(faceSlot,"J / Q / K",Vector2.zero,Vector2.one,18,Color.white);
            combinationModeButton=ActionButton(battlefield,"조합 선택",new Vector2(0.435f,0.575f),new Vector2(0.565f,0.611f),ToggleCombinationMode);
            aceSlot.gameObject.AddComponent<Button>().onClick.AddListener(()=>CombinationSlotClick(true));
            faceSlot.gameObject.AddComponent<Button>().onClick.AddListener(()=>CombinationSlotClick(false));
        }
        void ToggleCombinationMode()
        {
            if(tutorial!=null && tutorial.Step!=7)return;
            if(tutorial!=null && combinationMode && engine.CombinationPreview(ViewPlayer,aceID,faceID)!=null){Combine();return;}
            if(discardMode || Blocked() || engine.NeedsTarget || AttackDragging || dragged!=null)return;
            bool enter=!combinationMode; ClearSelection(); combinationMode=enter; combinationTurn=engine.State.TurnNumber;
            RefreshCombinationSlots(); RefreshHands(engine.State.Players[ViewPlayer],engine.State.Players[1-ViewPlayer]);
        }
        void CombinationSlotClick(bool ace)
        {
            if(!combinationMode) { ToggleCombinationMode(); return; }
            if(Blocked())return;
            if(ace)aceID=-1; else faceID=-1;
            RefreshCombinationSlots(); RefreshHands(engine.State.Players[ViewPlayer],engine.State.Players[1-ViewPlayer]);
        }
        void SelectCombinationMaterial(CardInstance card)
        {
            if(!combinationMode || Blocked() || engine.NeedsTarget || card==null || card.Owner!=ViewPlayer || !engine.State.Players[ViewPlayer].Hand.Any(c=>c!=null && c.InstanceID==card.InstanceID))return;
            if(card.Definition.CardType!=CardType.Normal) { Toast("손패의 A와 J/Q/K를 선택하세요"); return; }
            var rank=card.Definition.Rank;
            if(rank==Rank.Ace)aceID=aceID==card.InstanceID ? -1 : card.InstanceID;
            else if(rank>=Rank.Jack && rank<=Rank.King)faceID=faceID==card.InstanceID ? -1 : card.InstanceID;
            else { Toast("손패의 A와 J/Q/K를 선택하세요"); return; }
            handID=attackerID=jokerTarget=-1;
            RefreshCombinationSlots(); RefreshHands(engine.State.Players[ViewPlayer],engine.State.Players[1-ViewPlayer]);
        }
        void ShowCombinationPreview(PointerEventData data)
        {
            if(engine==null || AttackDragging || dragged!=null)return;
            HideCardPreview();
            var result=engine.CombinationPreview(ViewPlayer,aceID,faceID);
            detail.text=result==null ? "조합 선택 후 손패의 A와 J/Q/K를 클릭하세요" : result.CardName+"\n공격 "+result.Attack+" · 체력 "+result.MaxHP+"\n"+result.Description+"\n조합 비용 0 MP";
            PositionTooltip(data);
        }
        void RefreshCombinationSlots()
        {
            if(engine==null || ViewPlayer<0 || ViewPlayer>1) return;
            var hand=engine.State.Players[ViewPlayer].Hand;
            var ace=hand.FirstOrDefault(c=>c!=null && c.InstanceID==aceID);
            var face=hand.FirstOrDefault(c=>c!=null && c.InstanceID==faceID);
            if(ace==null) aceID=-1; if(face==null) faceID=-1;
            aceSlotText.text=ace==null ? "A" : CardDatabase.Symbol(ace.Definition.Suit)+" A ×";
            faceSlotText.text=face==null ? "J / Q / K" : CardDatabase.Symbol(face.Definition.Suit)+" "+CardDatabase.RankText(face.Definition.Rank)+" ×";
            aceSlot.GetComponent<Image>().color=ace==null ? panel : new Color(0.28f,0.16f,0.38f);
            faceSlot.GetComponent<Image>().color=face==null ? panel : new Color(0.28f,0.16f,0.38f);
            var result=engine.CombinationPreview(ViewPlayer,aceID,faceID);
            selectionInfo.text=result==null ? "" : "A + "+CardDatabase.RankText(face.Definition.Rank)+" = "+result.CardName;
            if(combineButton!=null) combineButton.interactable=combinationMode && !Blocked() && !engine.NeedsTarget && result!=null;
            if(combinationModeButton!=null) {
                combinationModeButton.interactable=!discardMode && !Blocked() && !engine.NeedsTarget && !AttackDragging && dragged==null;
                combinationModeButton.GetComponentInChildren<Text>().text=tutorial!=null && combinationMode && result!=null ? "조합 확정" : combinationMode ? "선택 취소" : tutorial!=null ? "UNKNOWN 조합" : "조합 선택";
            }
        }
        void BuildTooltip()
        {
            tooltip=Box(interactionRoot,"Card Tooltip",Vector2.one*0.5f,Vector2.one*0.5f,new Color(0.035f,0.045f,0.075f,0.98f));
            tooltip.sizeDelta=new Vector2(320,190);
            var border=tooltip.gameObject.AddComponent<Outline>(); border.effectColor=gold; border.effectDistance=new Vector2(1,-1);
            detail=Label(tooltip,"",new Vector2(0.04f,0.05f),new Vector2(0.96f,0.95f),18,Color.white);
            foreach(var g in tooltip.GetComponentsInChildren<Graphic>()) g.raycastTarget=false;
            tooltip.gameObject.SetActive(false);
        }
        void BuildDragLayer()
        {
            dragLayer=Box(interactionRoot,"Card Drag Layer",Vector2.zero,Vector2.one,Color.clear);
            var group=dragLayer.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts=false; group.interactable=false;
            tooltip.SetAsLastSibling();
            BuildAttackArrow();
        }
        void ShowTooltip(CardInstance card,RectTransform view,PointerEventData data)
        {
            if(AttackDragging || dragged!=null || passOverlay.activeSelf || resultOverlay.activeSelf) return;
            ShowCardPreview(card);
            PositionTooltip(data);
        }
        void PositionTooltip(PointerEventData data)
        {
            tooltip.gameObject.SetActive(true); tooltip.SetAsLastSibling();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(interactionRoot,data.position,null,out var p);
            var bounds=interactionRoot.rect; Vector2 half=tooltip.sizeDelta/2;
            p+=new Vector2(p.x>0 ? -half.x-22 : half.x+22,p.y>0 ? -half.y-20 : half.y+20);
            tooltip.anchoredPosition=new Vector2(Mathf.Clamp(p.x,bounds.xMin+half.x+10,bounds.xMax-half.x-10),Mathf.Clamp(p.y,bounds.yMin+half.y+10,bounds.yMax-half.y-10));
        }
        void HideTooltip() { HideCardPreview();if(tooltip!=null) tooltip.gameObject.SetActive(false); }
        void Toast(string text) { toastText=text; toastUntil=Time.unscaledTime+1.5f; }
        public bool BeginCardDrag(int id,RectTransform original,PointerEventData data)
        {
            if(tutorial!=null && tutorial.Step!=2 && tutorial.Step!=5 && tutorial.Step!=6 && tutorial.Step!=7)return false;
            if(data.button!=PointerEventData.InputButton.Left || discardMode || combinationMode || AttackDragging || dragged!=null || original==null) return false;
            if(Blocked()) { Toast(engine.State.IsOver ? "종료된 경기입니다." : engine.State.ActivePlayer!=ViewPlayer ? "상대방의 턴입니다." : engine.State.TurnExpired ? "턴 시간이 종료되었습니다." : "현재 동작을 처리하고 있습니다."); return false; }
            if(engine.NeedsTarget) { Toast(engine.TargetPrompt); return false; }
            var card=engine.State.Players[ViewPlayer].Hand.FirstOrDefault(c=>c!=null && c.InstanceID==id);
            if(card==null) return false;
            if(tutorial?.Step==7 && card.Definition.CardType!=CardType.Unknown)return false;
            var d=card.Definition;
            EndCardDrag(); original.GetComponent<CardHover>()?.ResetHover();
            // Clone disabled interaction scripts before assigning the active drag context.
            dragGhost=Instantiate(original.gameObject,dragLayer).GetComponent<RectTransform>(); dragGhost.name="DragGhost";
            foreach(var c in dragGhost.GetComponentsInChildren<CardDrag>()) c.enabled=false;
            foreach(var c in dragGhost.GetComponentsInChildren<CardHover>()) c.enabled=false;
            foreach(var c in dragGhost.GetComponentsInChildren<HandCardMotion>()) c.enabled=false;
            foreach(var g in dragGhost.GetComponentsInChildren<Graphic>()) g.raycastTarget=false;
            dragGhost.anchorMin=dragGhost.anchorMax=dragGhost.pivot=Vector2.one*0.5f; dragGhost.sizeDelta=original.rect.size; dragGhost.localScale=Vector3.one;
            var group=dragGhost.GetComponent<CanvasGroup>(); if(group==null) group=dragGhost.gameObject.AddComponent<CanvasGroup>(); group.alpha=0.92f; group.blocksRaycasts=false;
            dragged=card; originalGroup=original.GetComponent<CanvasGroup>(); if(originalGroup==null) originalGroup=original.gameObject.AddComponent<CanvasGroup>(); originalAlpha=originalGroup.alpha; originalGroup.alpha=0.28f;
            var motion=original.GetComponent<HandCardMotion>(); if(motion!=null)motion.Held=true;
            MoveCardDrag(data); return true;
        }
        public void MoveCardDrag(PointerEventData data)
        {
            if(dragged==null || dragGhost==null) return;
            if(Blocked()) { EndCardDrag(); return; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer,data.position,null,out var p); dragGhost.anchoredPosition=p;
            HighlightDrop(data.pointerCurrentRaycast.gameObject?.GetComponentInParent<CardDropZone>());
        }
        bool CanDrop(CardDropZone zone)
        {
            if(dragged==null || zone==null || Blocked() || engine.NeedsTarget || dragged.Owner!=ViewPlayer
                || !engine.State.Players[ViewPlayer].Hand.Any(c=>c!=null && c.InstanceID==dragged.InstanceID)) return false;
            var d=dragged.Definition;
            if(zone.Kind!=CardDropKind.Unit || zone.Owner!=ViewPlayer || zone.Slot<0 || zone.Slot>=engine.Config.FieldSlots) return false;
            var unit=engine.State.Players[ViewPlayer].Board[zone.Slot];
            return true; // Host validates mana, occupancy and Joker eligibility.
        }
        public void HighlightDrop(CardDropZone zone)
        {
            if(dragged==null) return;
            if(hoveredDrop==zone && hoveredOutline!=null) return;
            RestoreDropOutline(); hoveredDrop=zone;
            var color=CanDrop(zone) ? gold : new Color(1,0.24f,0.25f);
            if(dragGhost!=null) dragGhost.GetComponent<Outline>().effectColor=color;
            if(zone==null) return;
            hoveredOutline=zone.GetComponent<Outline>(); addedOutline=hoveredOutline==null;
            if(addedOutline) hoveredOutline=zone.gameObject.AddComponent<Outline>();
            originalOutline=hoveredOutline.effectColor; hoveredOutline.effectColor=color; hoveredOutline.effectDistance=new Vector2(3,-3);
        }
        public void ClearDropHighlight(CardDropZone zone) { if(hoveredDrop==zone) { RestoreDropOutline(); hoveredDrop=null; } }
        void RestoreDropOutline()
        {
            if(hoveredOutline!=null) { if(addedOutline) { hoveredOutline.enabled=false; Destroy(hoveredOutline); } else hoveredOutline.effectColor=originalOutline; }
            hoveredOutline=null; addedOutline=false;
        }
        public bool DropCard(CardDropZone zone)
        {
            RememberDrop();
            if(!CanDrop(zone)) { if(dragged!=null) Toast("소환할 수 없는 위치입니다."); EndCardDrag(); ReturnRejectedCard(); return false; }
            int id=dragged.InstanceID,actor=ViewPlayer; var kind=zone.Kind; int slot=zone.Slot;
            bool joker=dragged.Definition.CardType==CardType.Joker;
            int target=kind==CardDropKind.Unit && joker ? engine.State.Players[actor].Board[slot]?.InstanceID ?? -1 : -1;
            EndCardDrag(); // Clear before dispatch: duplicate Drop/EndDrag cannot submit twice.
            if(joker)Dispatch(BattleAction.Joker,id,target,()=>engine.Gamble(actor,id,target),"JOKER");
            else Dispatch(BattleAction.Summon,id,slot,()=>engine.Summon(actor,id,slot),"소환 완료"); return true;
        }
        public void EndCardDrag()
        {
            RestoreDropOutline(); hoveredDrop=null; dragged=null;
            if(originalGroup!=null) { originalGroup.alpha=originalAlpha; var motion=originalGroup.GetComponent<HandCardMotion>(); if(motion!=null)motion.Held=false; } originalGroup=null;
            if(dragGhost!=null) { dragGhost.gameObject.SetActive(false); Destroy(dragGhost.gameObject); dragGhost=null; }
            HideTooltip();
        }
        void UpdateInteractionUI()
        {
            RefreshExchangeTools();
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true)
                HandleBattleEscape();
            if(discardMode && (engine.State.TurnNumber!=discardTurn || engine.State.ActivePlayer!=ViewPlayer || engine.State.IsOver || engine.NeedsTarget))CancelInteractionSelection();
            if(dragged!=null && (Blocked() || engine.NeedsTarget)) EndCardDrag();
            if(AttackDragging && !ReadyToAttack(BoardCard(attackDragID)))EndAttackDrag();
            if(combinationMode && (Blocked() || engine.NeedsTarget || combinationTurn!=engine.State.TurnNumber))CancelInteractionSelection();
            UpdateAttackPointer();
            notice.text=engine.NeedsTarget && engine.State.ActivePlayer==ViewPlayer ? engine.TargetPrompt : Time.unscaledTime<toastUntil ? toastText : "";
            if(layoutWidth==Screen.width && layoutHeight==Screen.height) return;
            layoutWidth=Screen.width; layoutHeight=Screen.height; Canvas.ForceUpdateCanvases();
            float width=Mathf.Min(1060,interactionRoot.rect.width-80);
            enemyBoard.sizeDelta=ownBoard.sizeDelta=new Vector2(width,Mathf.Min(220,interactionRoot.rect.height*0.204f));
            if(!actionBusy && !transitioning) Refresh();
        }
        void CancelInteractionSelection()
        { EndAttackDrag(); EndCardDrag(); combinationMode=false;discardMode=false;discardID=-1; aceID=faceID=handID=attackerID=-1; RefreshCombinationSlots(); if(engine!=null && ViewPlayer>=0 && ViewPlayer<2)RefreshHands(engine.State.Players[ViewPlayer],engine.State.Players[1-ViewPlayer]);RefreshHandTools(); }
        internal bool SmokeDrag(int id,CardDropKind kind,int slot=-1)
        {
            if(!Debug.isDebugBuild || !Environment.GetCommandLineArgs().Contains("-unknownSmoke") || !cardViews.TryGetValue(id,out var view)) return false;
            var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,view.position)};
            if(!BeginCardDrag(id,view,data)) return false;
            var zone=kind==CardDropKind.Ace ? aceSlot.GetComponent<CardDropZone>() : kind==CardDropKind.Face ? faceSlot.GetComponent<CardDropZone>()
                : ownBoard.Cast<Transform>().Where(t=>t.gameObject.activeSelf).ElementAt(slot).GetComponent<CardDropZone>();
            return DropCard(zone);
        }
        internal bool SmokeCombine(int ace,int face)
        {
            if(!Debug.isDebugBuild || !Environment.GetCommandLineArgs().Contains("-unknownSmoke"))return false;
            if(!combinationMode)ToggleCombinationMode();
            if(!handViews.TryGetValue(ace,out var aceView) || !handViews.TryGetValue(face,out var faceView))return false;
            aceView.GetComponent<Button>().onClick.Invoke(); faceView.GetComponent<Button>().onClick.Invoke();
            if(!combineButton.interactable) return false;
            combineButton.onClick.Invoke(); return true;
        }
    }
}
