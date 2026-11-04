using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace Unknown.Battle
{
    public enum AttackDragState { Idle, Dragging, ValidTarget, InvalidTarget, AttackConfirmed, Cancelled }
    public sealed partial class BattleUIController
    {
        int attackDragID=-1;
        public AttackDragState AttackState { get; private set; }
        int awaitingAttack=-1;
        Vector3 sourceScale;
        bool targetOutlineEnabled;
        RectTransform attackVisualLayer,attackHint;
        Text attackHintText,heroAttackIcon;
        readonly List<RaycastResult> attackHits=new List<RaycastResult>();
#if UNITY_EDITOR
        bool simulateAttackPointer;
#endif
        Camera AttackCamera => canvas.renderMode==RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        readonly Color attackYellow=new Color(1,0.92f,0.25f);
        public bool AttackDragging => attackDragID>=0;
        AttackArrowGraphic attackArrow;
        RectTransform heroAttackArea,attackSource,attackTargetView;
        Outline heroAttackGlow,targetAttackOutline;
        CardDropZone attackZone;
        Color targetOutlineColor;
        Vector3 targetScale;
        Vector2 attackPointer;
        readonly Color attackRed=new Color(1,0.22f,0.18f), attackInvalid=new Color(0.48f,0.51f,0.56f);
        void BuildAttackTarget()
        {
            heroAttackArea=Box(battlefield,"Opponent Player Attack Target",new Vector2(0.20f,0.955f),new Vector2(0.80f,0.998f),navy);
            heroAttackArea.gameObject.AddComponent<CardDropZone>().Configure(this,CardDropKind.Hero);
            heroAttackArea.gameObject.AddComponent<AttackHeroHint>().Configure(this);
            heroAttackGlow=heroAttackArea.gameObject.AddComponent<Outline>(); heroAttackGlow.effectDistance=new Vector2(3,-3); heroAttackGlow.useGraphicAlpha=false; heroAttackGlow.enabled=false;
            heroAttackIcon=Label(heroAttackArea,"⚔",new Vector2(0.92f,0),Vector2.one,24,attackRed); heroAttackIcon.gameObject.SetActive(false);
        }
        void BuildAttackArrow()
        {
            attackVisualLayer=Box(dragLayer,"Local Attack Feedback",Vector2.zero,Vector2.one,Color.clear);
            attackVisualLayer.GetComponent<Image>().raycastTarget=false;
            var overlay=attackVisualLayer.gameObject.AddComponent<Canvas>(); overlay.overrideSorting=true; overlay.sortingOrder=canvas.sortingOrder+10;
            var go=new GameObject("Local Attack Arrow",typeof(RectTransform),typeof(CanvasRenderer),typeof(AttackArrowGraphic)); go.transform.SetParent(attackVisualLayer,false);
            var rect=(RectTransform)go.transform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero;
            attackArrow=go.GetComponent<AttackArrowGraphic>(); attackArrow.raycastTarget=false; go.SetActive(false);
            attackHint=Box(attackVisualLayer,"Attack Cursor Hint",Vector2.zero,Vector2.zero,new Color(0.025f,0.035f,0.055f,0.96f));
            attackHint.sizeDelta=new Vector2(190,38); attackHint.pivot=new Vector2(0,0.5f); attackHint.GetComponent<Image>().raycastTarget=false;
            attackHintText=Label(attackHint,"",Vector2.zero,Vector2.one,20,Color.white); attackHint.gameObject.SetActive(false);
        }
        CardInstance BoardCard(int id)
        {
            if(engine==null)return null;
            int actor=ViewPlayer;
            return actor<0 || actor>=engine.State.Players.Count ? null : engine.State.Players[actor].Board.FirstOrDefault(c=>c!=null && c.InstanceID==id);
        }
        bool ReadyToAttack(CardInstance card) => card!=null && card.CurrentHP>0 && !Blocked() && !presenting && engine.CanAttack(ViewPlayer,card);
        string AttackReason(CardInstance card)
        {
            if(engine.State.IsOver)return "게임이 종료되었습니다.";
            if(engine.State.ActivePlayer!=ViewPlayer)return "상대방의 턴입니다.";
            if(engine.State.TurnExpired || (online==null && clock.Expired))return "턴 제한 시간이 끝났습니다.";
            if(IsPresenting || engine.NeedsTarget || (online!=null && !online.CanAct))return "현재 다른 행동을 처리 중입니다.";
            if(card.HasAttacked)return "이미 공격한 유닛입니다.";
            if(card.SummonedTurn>=engine.State.TurnNumber)return "소환된 턴에는 공격할 수 없습니다. 다음 턴부터 공격 가능";
            return "드래그하여 공격하세요.";
        }
        public bool BeginAttackDrag(int id,PointerEventData data)
        {
            if(data.button!=PointerEventData.InputButton.Left || AttackDragging || dragged!=null || !ReadyToAttack(BoardCard(id)) || !cardViews.TryGetValue(id,out var source))return false;
            HideTooltip(); source.GetComponent<CardHover>()?.ResetHover();
            handID=attackerID=aceID=faceID=-1; RefreshCombinationSlots();
            attackDragID=id; attackSource=source; sourceScale=source.localScale; source.localScale=sourceScale*1.08f;
            AttackState=AttackDragState.Dragging; attackArrow.gameObject.SetActive(true); attackHint.gameObject.SetActive(true);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(attackVisualLayer,data.position,AttackCamera,out attackPointer);
            DrawAttackArrow(); RefreshAttackIndicators(); return true;
        }
        public void MoveAttackDrag(PointerEventData data)
        {
            if(!AttackDragging)return;
            if(!ReadyToAttack(BoardCard(attackDragID)) || attackSource==null) { EndAttackDrag(); return; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(attackVisualLayer,data.position,AttackCamera,out attackPointer);
            HighlightAttack(data.pointerCurrentRaycast.gameObject?.GetComponentInParent<CardDropZone>());
            DrawAttackArrow();
        }
        // Re-raycast even while the pointer is stationary: targets can disappear or move.
        void UpdateAttackPointer()
        {
#if UNITY_EDITOR
            if(simulateAttackPointer)return;
#endif
            if(!AttackDragging)return;
            if(!ReadyToAttack(BoardCard(attackDragID)) || attackSource==null) { EndAttackDrag(); return; }
            if(Mouse.current==null || EventSystem.current==null)return;
            var data=new PointerEventData(EventSystem.current){position=Mouse.current.position.ReadValue()};
            attackHits.Clear(); EventSystem.current.RaycastAll(data,attackHits);
            if(attackHits.Count>0)data.pointerCurrentRaycast=attackHits[0];
            MoveAttackDrag(data);
        }
        bool CanAttackDrop(CardDropZone zone)
        {
            if(zone==null || !ReadyToAttack(BoardCard(attackDragID)))return false;
            if(zone.Kind==CardDropKind.Hero)return engine.State.Players[1-ViewPlayer].Board.All(c=>c==null);
            if(zone.Kind!=CardDropKind.Unit || zone.Owner!=1-ViewPlayer || zone.Slot<0 || zone.Slot>=engine.Config.FieldSlots)return false;
            var target=engine.State.Players[zone.Owner].Board[zone.Slot]; return target!=null && target.CurrentHP>0;
        }
        void DrawAttackArrow()
        {
            if(!AttackDragging || attackSource==null)return;
            var start=(Vector2)attackVisualLayer.InverseTransformPoint(attackSource.TransformPoint(new Vector2(attackSource.rect.center.x,attackSource.rect.yMax)));
            bool valid=CanAttackDrop(attackZone);
            if(AttackState!=AttackDragState.Dragging)AttackState=valid ? AttackDragState.ValidTarget : AttackDragState.InvalidTarget;
            var tint=valid ? new Color(1,0.42f,0.28f) : AttackState==AttackDragState.Dragging ? attackRed : attackInvalid;
            attackArrow.SetPoints(start,attackPointer,tint,canvas.scaleFactor);
            attackHintText.text=valid ? attackZone.Kind==CardDropKind.Hero ? "놓으면 직접 공격" : "놓으면 공격" : AttackState==AttackDragState.Dragging ? "공격 대상 선택" : "공격 불가";
            attackHintText.color=tint;
            var bounds=attackVisualLayer.rect;
            attackHint.anchoredPosition=new Vector2(Mathf.Clamp(attackPointer.x+22,bounds.xMin+8,bounds.xMax-attackHint.rect.width-8)-bounds.xMin,
                Mathf.Clamp(attackPointer.y+24,bounds.yMin+24,bounds.yMax-24)-bounds.yMin);
        }
        public void HighlightAttack(CardDropZone zone)
        {
            if(!AttackDragging)return;
            AttackState=CanAttackDrop(zone) ? AttackDragState.ValidTarget : AttackDragState.InvalidTarget;
            if(attackZone==zone && CanAttackDrop(zone)==(attackTargetView!=null)) { DrawAttackArrow(); return; }
            RestoreAttackTarget(); attackZone=zone;
            if(CanAttackDrop(zone))
            {
                attackTargetView=(RectTransform)zone.transform;
                attackTargetView.GetComponent<CardHover>()?.ResetHover(); targetScale=attackTargetView.localScale;
                if(zone.Kind==CardDropKind.Unit)attackTargetView.localScale=targetScale*1.05f;
                targetAttackOutline=attackTargetView.GetComponent<Outline>();
                if(targetAttackOutline!=null) { targetOutlineColor=targetAttackOutline.effectColor; targetOutlineEnabled=targetAttackOutline.enabled; targetAttackOutline.effectColor=attackRed; targetAttackOutline.enabled=true; }
            }
            DrawAttackArrow(); RefreshAttackIndicators();
        }
        public void ClearAttackHighlight(CardDropZone zone) { if(attackZone==zone) { RestoreAttackTarget(); attackZone=null; DrawAttackArrow(); RefreshAttackIndicators(); } }
        void RestoreAttackTarget()
        {
            if(attackTargetView!=null)attackTargetView.localScale=targetScale;
            if(targetAttackOutline!=null) { targetAttackOutline.effectColor=targetOutlineColor; targetAttackOutline.enabled=targetOutlineEnabled; }
            attackTargetView=null; targetAttackOutline=null;
        }
        public bool DropAttack(CardDropZone zone)
        {
            if(!AttackDragging)return false;
            if(!CanAttackDrop(zone)) { EndAttackDrag(); return false; }
            int source=attackDragID;
            int target=zone.Kind==CardDropKind.Hero ? -1 : engine.State.Players[zone.Owner].Board[zone.Slot].InstanceID;
            EndAttackDrag(); awaitingAttack=source; SendAttack(source,target); return true;
        }
        void SendAttack(int source,int target)
        {
            if(!ReadyToAttack(BoardCard(source)))return;
            int actor=ViewPlayer;
            int slot=target==-1 ? -1 : System.Array.FindIndex(engine.State.Players[1-actor].Board.ToArray(),c=>c!=null && c.InstanceID==target);
            if(target!=-1 && slot<0)return;
            Dispatch(BattleAction.Attack,source,target,()=>engine.Attack(actor,source,slot),"공격 완료");
            RefreshAttackIndicators();
        }
        public void EndAttackDrag()
        {
            if(AttackDragging)AttackState=AttackDragState.Cancelled;
            if(attackSource!=null)attackSource.localScale=sourceScale;
            RestoreAttackTarget(); attackZone=null; attackDragID=-1; attackSource=null;
            if(attackArrow!=null)attackArrow.gameObject.SetActive(false);
            if(attackHint!=null)attackHint.gameObject.SetActive(false);
            HideTooltip(); RefreshAttackIndicators();
        }
        void ObserveAttackResult(System.Collections.Generic.IEnumerable<BattleEvent> entries)
        {
            if(awaitingAttack<0)return;
            if(entries.Any(e=>e.Kind==BattleEventKind.Attack && e.SourceID==awaitingAttack && e.Owner==ViewPlayer))
            { awaitingAttack=-1; AttackState=AttackDragState.AttackConfirmed; }
        }
        public void CancelAttackFrom(int id) { if(attackDragID==id)EndAttackDrag(); }
        void RefreshAttackIndicators()
        {
            if(engine==null || heroAttackGlow==null)return;
            // Room teardown releases LocalPlayer before destroying the battle UI.
            if(ViewPlayer<0 || ViewPlayer>=engine.State.Players.Count)
            { heroAttackGlow.enabled=false; if(heroAttackIcon!=null)heroAttackIcon.gameObject.SetActive(false); return; }
            bool allowed=!Blocked() && !presenting;
            bool any=false;
            foreach(var pair in cardViews)
            {
                var view=pair.Value;
                if(view==null || view.parent!=ownBoard)continue;
                var card=BoardCard(pair.Key);
                bool readyNow=allowed && card!=null && card.CurrentHP>0 && engine.CanAttack(ViewPlayer,card); any|=readyNow;
                var border=view.GetComponent<Outline>();
                if(hoveredOutline!=border && targetAttackOutline!=border)border.effectColor=attackDragID==pair.Key ? attackYellow : readyNow ? ready : card==null ? attackInvalid : SuitInk(card.Definition.Suit);
                var status=view.Find("Attack Status")?.GetComponent<Text>();
                if(status!=null) { status.text=card==null ? "" : card.HasAttacked ? "공격 완료" : card.SummonedTurn>=engine.State.TurnNumber ? "◷" : readyNow ? "⚔" : ""; status.color=readyNow ? ready : new Color(0.5f,0.55f,0.62f); }
                var glow=view.Find("Attack Ready Glow"); if(glow!=null) {
                    glow.gameObject.SetActive(readyNow); var tint=attackDragID==pair.Key ? attackYellow : ready;
                    glow.GetComponent<AttackGlowGraphic>().color=new Color(tint.r,tint.g,tint.b,attackDragID==pair.Key ? 0.4f : 0.22f);
                }
            }
            bool direct=allowed && any && engine.State.Players[1-ViewPlayer].Board.All(c=>c==null);
            heroAttackGlow.enabled=direct;
            heroAttackIcon.gameObject.SetActive(direct && AttackDragging && attackZone?.Kind==CardDropKind.Hero);
            heroAttackGlow.effectColor=AttackDragging && attackZone?.Kind==CardDropKind.Hero && direct ? attackRed : new Color(1,0.25f,0.2f,0.3f);
        }
        public void ShowHeroAttackHint(PointerEventData data)
        {
            if(AttackDragging || dragged!=null || engine==null)return;
            string reason=engine.State.Players[1-ViewPlayer].Board.Any(c=>c!=null) ? "상대 필드에 유닛이 있어 직접 공격할 수 없습니다." : "아군 유닛을 이 영역으로 드래그하여 직접 공격";
            detail.text=reason; PositionTooltip(data);
        }
        public void HideAttackHint() { HideTooltip(); }
        internal bool SmokeAttack(int source,int target)
        {
            if(!Debug.isDebugBuild || !System.Environment.GetCommandLineArgs().Contains("-unknownSmoke") || !cardViews.TryGetValue(source,out var view))return false;
            var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,view.position)};
            if(!BeginAttackDrag(source,data))return false;
            var zone=target==-1 ? heroAttackArea.GetComponent<CardDropZone>() : cardViews.TryGetValue(target,out var targetView) ? targetView.GetComponent<CardDropZone>() : null;
            if(zone==null) { EndAttackDrag(); return false; }
            data.position=RectTransformUtility.WorldToScreenPoint(AttackCamera,zone.transform.position);
            data.pointerCurrentRaycast=new RaycastResult{gameObject=zone.gameObject}; MoveAttackDrag(data);
            if(AttackState!=AttackDragState.ValidTarget || !attackArrow.gameObject.activeInHierarchy || !attackHint.gameObject.activeInHierarchy
                || attackSource.localScale!=sourceScale*1.08f || targetAttackOutline?.effectColor!=attackRed)
            { EndAttackDrag(); return false; }
            return DropAttack(zone);
        }
    }
}
