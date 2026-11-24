using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Unknown.Battle
{
    public sealed class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Action<PointerEventData> showDetails; Action hideDetails;
        bool enlarge, hovering, reorder,pendingOrderRestore; Vector3 original; int sibling;Transform originalParent;
        public Func<bool> CanZoom;
        public void Configure(Action<PointerEventData> show, Action hide, bool zoom, bool hand) { showDetails=show; hideDetails=hide; enlarge=zoom; reorder=hand; }
        public void OnPointerEnter(PointerEventData data)
        {
            showDetails?.Invoke(data); if(!enlarge || hovering || (CanZoom!=null && !CanZoom())) return;
            hovering=true;original=transform.localScale;
            if(!pendingOrderRestore || originalParent!=transform.parent){sibling=transform.GetSiblingIndex();originalParent=transform.parent;}
            pendingOrderRestore=false;
            transform.localScale=original*(reorder ? 1.08f : 1.035f); if(reorder) transform.SetAsLastSibling();
        }
        public void OnPointerExit(PointerEventData data) { ResetHover(); hideDetails?.Invoke(); }
        public void ResetHover(){ResetHover(true);}
        void ResetHover(bool restoreImmediately)
        {
            if(!hovering)return;hovering=false;transform.localScale=original;
            if(!reorder)return;pendingOrderRestore=true;
            if(restoreImmediately && isActiveAndEnabled && gameObject.activeInHierarchy)RestoreOrder();
        }
        void RestoreOrder()
        {
            pendingOrderRestore=false;
            if(originalParent!=null && transform.parent==originalParent)transform.SetSiblingIndex(Mathf.Clamp(sibling,0,originalParent.childCount-1));
        }
        void LateUpdate(){if(pendingOrderRestore && isActiveAndEnabled && gameObject.activeInHierarchy)RestoreOrder();}
        // Unity forbids sibling changes while an ancestor is being activated/deactivated.
        void OnDisable() { ResetHover(false); hideDetails?.Invoke(); }
    }
}
