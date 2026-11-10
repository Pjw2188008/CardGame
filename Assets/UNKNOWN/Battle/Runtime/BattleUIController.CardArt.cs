using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    public sealed partial class BattleUIController
    {
        void ApplyCardArtwork(RectTransform rect,CardInstance card)
        {
            var texture=CardArtSettings.TextureFor(card.Definition.CardID);if(texture==null)return;
            var background=rect.GetComponent<Image>();background.color=Color.clear;background.raycastTarget=true;
            var outline=rect.GetComponent<Outline>();outline.useGraphicAlpha=true;
            rect.Find("Card Rank").gameObject.SetActive(false);rect.Find("Card Symbol").gameObject.SetActive(false);
            var go=new GameObject("Card Artwork",typeof(RectTransform),typeof(RawImage));go.transform.SetParent(rect,false);go.transform.SetAsFirstSibling();
            var image=go.GetComponent<RawImage>();image.texture=texture;image.raycastTarget=false;
            var cost=(RectTransform)rect.Find("Mana cost");cost.GetComponent<Image>().color=Color.clear;cost.GetComponent<Image>().raycastTarget=false;
            var costText=cost.GetComponentInChildren<Text>();costText.rectTransform.offsetMin=costText.rectTransform.offsetMax=Vector2.zero;
            var description=rect.Find("Card Description").GetComponent<Text>();description.color=new Color(.98f,.92f,.82f);description.resizeTextMinSize=7;description.resizeTextMaxSize=10;
            var atk=rect.Find("Stat ATK").GetComponent<Text>();var hp=rect.Find("Stat HP").GetComponent<Text>();atk.fontSize=18;hp.fontSize=16;
            atk.resizeTextForBestFit=hp.resizeTextForBestFit=true;atk.resizeTextMinSize=10;hp.resizeTextMinSize=9;atk.resizeTextMaxSize=18;hp.resizeTextMaxSize=16;
            foreach(var text in new[]{costText,description,atk,hp}) {var shadow=text.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.85f);shadow.effectDistance=new Vector2(1,-1);}
            var layout=rect.gameObject.AddComponent<CardArtLayout>();layout.artwork=image;layout.mana=cost;layout.description=description.rectTransform;
            layout.attack=atk.rectTransform;layout.health=hp.rectTransform;layout.joker=card.Definition.CardType==CardType.Joker;layout.RefreshLayout();
            foreach(var text in new[]{costText,atk,hp})text.gameObject.AddComponent<CardBadgeCenter>();
            UpdateCardStats(rect,card,false);
        }
        void DrawAnimationCard(RectTransform rect,CardDefinition definition)
        {
            foreach(var text in rect.GetComponentsInChildren<Text>())text.gameObject.SetActive(false);
            DrawCard(rect,new CardInstance(-1,definition,ViewPlayer),false,false,()=>{},false);
            foreach(var hover in rect.GetComponentsInChildren<CardHover>())hover.enabled=false;
            foreach(var drag in rect.GetComponentsInChildren<CardDrag>())drag.enabled=false;
            foreach(var graphic in rect.GetComponentsInChildren<Graphic>())graphic.raycastTarget=false;
        }
    }
}
