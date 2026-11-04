using UnityEngine;
using UnityEngine.EventSystems;
namespace Unknown.Battle
{
    public enum CardDropKind { Unit, Ace, Face, Hero }
    public sealed class CardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        BattleUIController ui; int cardID;
        public void Configure(BattleUIController controller,int id) { ui=controller; cardID=id; }
        public void OnBeginDrag(PointerEventData data) { if(ui!=null && ui.BeginCardDrag(cardID,transform as RectTransform,data))data.eligibleForClick=false; }
        public void OnDrag(PointerEventData data) { ui?.MoveCardDrag(data); }
        public void OnEndDrag(PointerEventData data) { ui?.EndCardDrag(); }
    }
    public sealed class CardDropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        BattleUIController ui;
        public CardDropKind Kind { get; private set; }
        public int Owner { get; private set; }
        public int Slot { get; private set; }
        public void Configure(BattleUIController controller,CardDropKind kind,int owner=-1,int slot=-1) { ui=controller; Kind=kind; Owner=owner; Slot=slot; }
        public void OnDrop(PointerEventData data) { if(ui==null)return; if(ui.AttackDragging)ui.DropAttack(this); else ui.DropCard(this); }
        public void OnPointerEnter(PointerEventData data) { if(ui==null)return; if(ui.AttackDragging)ui.HighlightAttack(this); else ui.HighlightDrop(this); }
        public void OnPointerExit(PointerEventData data) { if(ui==null)return; if(ui.AttackDragging)ui.ClearAttackHighlight(this); else ui.ClearDropHighlight(this); }
    }
    public sealed class UnitAttackDrag : MonoBehaviour, IInitializePotentialDragHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        BattleUIController ui; int id,previousThreshold=-1;
        public void Configure(BattleUIController controller,int cardID) { ui=controller; id=cardID; }
        public void OnInitializePotentialDrag(PointerEventData data)
        {
            // EventSystem's threshold is measured in screen pixels, independently of CanvasScaler.
            if(EventSystem.current!=null) { previousThreshold=EventSystem.current.pixelDragThreshold; EventSystem.current.pixelDragThreshold=8; }
            data.useDragThreshold=true;
        }
        void RestoreThreshold() { if(previousThreshold<0)return; if(EventSystem.current!=null)EventSystem.current.pixelDragThreshold=previousThreshold; previousThreshold=-1; }
        public void OnPointerUp(PointerEventData data) { RestoreThreshold(); }
        public void OnBeginDrag(PointerEventData data) { RestoreThreshold(); if(ui!=null && ui.BeginAttackDrag(id,data))data.eligibleForClick=false; }
        public void OnDrag(PointerEventData data) { ui?.MoveAttackDrag(data); }
        public void OnEndDrag(PointerEventData data) { RestoreThreshold(); ui?.EndAttackDrag(); }
        void OnDisable() { RestoreThreshold(); ui?.CancelAttackFrom(id); }
    }
    public sealed class AttackHeroHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        BattleUIController ui;
        public void Configure(BattleUIController controller) { ui=controller; }
        public void OnPointerEnter(PointerEventData data) { ui?.ShowHeroAttackHint(data); }
        public void OnPointerExit(PointerEventData data) { ui?.HideAttackHint(); }
    }
}
