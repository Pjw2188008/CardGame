using UnityEngine;
using UnityEngine.EventSystems;
namespace Unknown.Battle
{
    public enum CardDropKind { Unit, Ace, Face }
    public sealed class CardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        BattleUIController ui; int cardID;
        public void Configure(BattleUIController controller,int id) { ui=controller; cardID=id; }
        public void OnBeginDrag(PointerEventData data) { ui?.BeginCardDrag(cardID,transform as RectTransform,data); }
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
        public void OnDrop(PointerEventData data) { ui?.DropCard(this); }
        public void OnPointerEnter(PointerEventData data) { ui?.HighlightDrop(this); }
        public void OnPointerExit(PointerEventData data) { ui?.ClearDropHighlight(this); }
    }
}
