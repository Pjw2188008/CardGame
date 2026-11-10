using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Unknown.Battle
{
    public sealed partial class BattleUIController
    {
        RectTransform hoverCardPreview;
        void ShowCardPreview(CardInstance card)
        {
            HideCardPreview();
            var texture=CardArtSettings.TextureFor(card.Definition.CardID);
            float aspect=texture!=null ? (float)texture.width/texture.height : 320f/432f;
            var bounds=interactionRoot.rect;
            float height=Mathf.Min(440,bounds.height-48);
            float width=Mathf.Min(height*aspect,(bounds.width-48)*.45f);height=width/aspect;
            tooltip.sizeDelta=new Vector2(width,height);
            tooltip.GetComponent<Image>().enabled=false;
            tooltip.GetComponent<Outline>().enabled=false;
            detail.gameObject.SetActive(false);
            hoverCardPreview=Box(tooltip,"Hovered Card Preview",Vector2.one*.5f,Vector2.one*.5f,Color.clear);
            hoverCardPreview.sizeDelta=new Vector2(width,height);
            DrawCard(hoverCardPreview,card,false,false,()=>{},false);
            var group=hoverCardPreview.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;
            foreach(var hover in hoverCardPreview.GetComponentsInChildren<CardHover>()){hover.Configure(null,null,false,false);hover.enabled=false;}
            foreach(var drag in hoverCardPreview.GetComponentsInChildren<CardDrag>())drag.enabled=false;
            foreach(var button in hoverCardPreview.GetComponentsInChildren<Button>())button.interactable=false;
            foreach(var graphic in hoverCardPreview.GetComponentsInChildren<Graphic>())graphic.raycastTarget=false;
        }
        void HideCardPreview()
        {
            if(hoverCardPreview!=null){hoverCardPreview.gameObject.SetActive(false);Destroy(hoverCardPreview.gameObject);hoverCardPreview=null;if(tooltip!=null)tooltip.sizeDelta=new Vector2(320,190);}
            if(tooltip!=null){tooltip.GetComponent<Image>().enabled=true;tooltip.GetComponent<Outline>().enabled=true;}
            if(detail!=null)detail.gameObject.SetActive(true);
        }
    }
}
