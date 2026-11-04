using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Unknown.Battle
{
    public sealed class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Action<PointerEventData> showDetails; Action hideDetails;
        bool enlarge, hovering, reorder; Vector3 original; int sibling;
        public Func<bool> CanZoom;
        public void Configure(Action<PointerEventData> show, Action hide, bool zoom, bool hand) { showDetails=show; hideDetails=hide; enlarge=zoom; reorder=hand; }
        public void OnPointerEnter(PointerEventData data)
        {
            showDetails?.Invoke(data); if(!enlarge || hovering || (CanZoom!=null && !CanZoom())) return;
            hovering=true; original=transform.localScale; sibling=transform.GetSiblingIndex();
            transform.localScale=original*(reorder ? 1.08f : 1.035f); if(reorder) transform.SetAsLastSibling();
        }
        public void OnPointerExit(PointerEventData data) { ResetHover(); hideDetails?.Invoke(); }
        public void ResetHover() { if(!hovering)return; transform.localScale=original; if(reorder)transform.SetSiblingIndex(sibling); hovering=false; }
        void OnDisable() { ResetHover(); hideDetails?.Invoke(); }
    }
}
