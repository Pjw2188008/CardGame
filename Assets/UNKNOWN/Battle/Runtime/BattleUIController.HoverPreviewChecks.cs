#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Unknown.Battle
{
    public sealed partial class BattleUIController
    {
        IEnumerator VerifyHoverPreview(Action<bool,string> verify)
        {
            CheckFixture();var hand=CheckHand("Heart_Q");var joker=CheckHand("Joker_2");var enemy=CheckUnit("Diamond_K",1);var own=CheckUnit("Heart_K",0);Refresh();yield return null;
            var source=cardViews[hand.InstanceID];var hover=source.GetComponent<CardHover>();var data=DragData(source);
            ExecuteEvents.Execute(source.gameObject,data,ExecuteEvents.pointerEnterHandler);yield return null;Canvas.ForceUpdateCanvases();
            verify(hoverCardPreview!=null && tooltip.gameObject.activeSelf && hoverCardPreview.rect.height>source.rect.height*1.7f,"Hand hover displays a clearly enlarged card preview");
            verify(!detail.gameObject.activeSelf && !tooltip.GetComponent<Image>().enabled && !tooltip.GetComponent<Outline>().enabled && tooltip.sizeDelta==hoverCardPreview.sizeDelta,"Hover shows only the enlarged card without a details panel or outer frame");
            verify(hoverCardPreview.Find("Card Artwork").GetComponent<RawImage>().texture==CardArtSettings.TextureFor(hand.Definition.CardID) && hoverCardPreview.Find("Stat HP").GetComponent<Text>().text==hand.CurrentHP.ToString(),"Preview uses the hovered card image and current stats");
            verify(!hoverCardPreview.GetComponent<CanvasGroup>().blocksRaycasts && hoverCardPreview.GetComponentsInChildren<Graphic>().All(g=>!g.raycastTarget),"Preview never consumes pointer input");
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            verify(hits.Count>0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==source.gameObject,"Original hand card remains clickable while preview is visible");
            foreach(int width in new[]{1280,1920})verify(System.IO.File.Exists(CaptureAttackFrame(width,source,"HoverPreview",false)),"Rendered standalone enlarged card at "+width);
            ExecuteEvents.Execute(source.gameObject,data,ExecuteEvents.pointerExitHandler);
            verify(hoverCardPreview==null && !tooltip.gameObject.activeSelf && source.localScale==Vector3.one,"Pointer exit removes preview and restores original card");
            hover.OnPointerEnter(data);DragBegin(hand);verify(dragged==hand && hoverCardPreview==null && !tooltip.gameObject.activeSelf,"Starting a summon drag closes the preview");EndCardDrag();
            var enemyView=cardViews[enemy.InstanceID];enemyView.GetComponent<CardHover>().OnPointerEnter(DragData(enemyView));yield return null;
            verify(hoverCardPreview!=null && hoverCardPreview.Find("Card Artwork").GetComponent<RawImage>().texture==CardArtSettings.TextureFor(enemy.Definition.CardID),"Public opponent field card can be inspected");enemyView.GetComponent<CardHover>().OnPointerExit(DragData(enemyView));
            var ownView=cardViews[own.InstanceID];ownView.GetComponent<CardHover>().OnPointerEnter(DragData(ownView));AttackBegin(own);
            verify(AttackDragging && hoverCardPreview==null,"Starting an attack closes the preview");EndAttackDrag();
            foreach(var pointer in new[]{new Vector2(1,1),new Vector2(Screen.width-1,Screen.height-1)}) {
                var edgeData=DragData(source);edgeData.position=pointer;ShowTooltip(joker,cardViews[joker.InstanceID],edgeData);yield return null;Canvas.ForceUpdateCanvases();
                var corners=new Vector3[4];tooltip.GetWorldCorners(corners);var bounds=interactionRoot.rect;
                verify(corners.All(c=>{var p=interactionRoot.InverseTransformPoint(c);return bounds.Contains(new Vector2(p.x,p.y));}),"Preview panel stays inside viewport at "+pointer);HideTooltip();
            }
            ShowTooltip(hand,source,data);ShowCombinationPreview(data);verify(hoverCardPreview==null && tooltip.sizeDelta.x<=400 && detail.gameObject.activeSelf && tooltip.GetComponent<Image>().enabled && tooltip.GetComponent<Outline>().enabled,"Combination details return to visible compact layout");HideTooltip();
            ShowMapTooltip(data);verify(hoverCardPreview==null && detail.rectTransform.anchorMin.x<.1f,"Map details retain the compact text layout");HideTooltip();
            verify(backViews.All(v=>v.Find("Hovered Card Preview")==null),"Hidden opponent hand receives no preview faces");
            Refresh();yield return null;
        }
    }
}
#endif
