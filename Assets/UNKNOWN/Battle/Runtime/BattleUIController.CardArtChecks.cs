#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    public sealed partial class BattleUIController
    {
        string ExpectedCardArtPath(CardDefinition d)
        {
            var parts=d.CardID.Split('_');string rank=parts[1]=="A" ? "a" : parts[1]=="J" ? "jack" : parts[1]=="Q" ? "queen" : parts[1]=="K" ? "king" : parts[1];
            if(d.CardType==CardType.Joker)return "Assets/CARD/Joker/joker-"+(parts[1]=="1" ? "red" : "purple")+".png";
            string folder=parts[0]=="Diamond" ? "DIA" : parts[0]=="Club" ? "CLOVE" : parts[0].ToUpperInvariant();
            return "Assets/CARD/"+folder+"/"+parts[0].ToLowerInvariant()+"-"+rank+".png";
        }
        bool BadgeInsideArt(RectTransform card,Text text)
        {
            if(string.IsNullOrEmpty(text.text))return true;
            var mesh=text.canvasRenderer.GetMesh();if(mesh==null || mesh.vertexCount==0)return false;
            var art=card.Find("Card Artwork") as RectTransform;var safe=art.rect;
            foreach(var vertex in mesh.vertices){var point=art.InverseTransformPoint(text.rectTransform.TransformPoint(vertex));if(point.x<safe.xMin+.5f || point.x>safe.xMax-.5f || point.y<safe.yMin+.5f || point.y>safe.yMax-.5f)return false;}
            return true;
        }
        IEnumerator VerifyCardBadgeReflow(Action<bool,string> verify)
        {
            CheckFixture();
            foreach(var id in new[]{"Spade_2","Joker_2","Spade_Q","Heart_3","Diamond_6","Heart_Q"})CheckHand(id);
            Refresh();yield return new WaitForSecondsRealtime(.3f);Canvas.ForceUpdateCanvases();
            foreach(var card in engine.State.Players[0].Hand) {
                var view=cardViews[card.InstanceID];var atk=view.Find("Stat ATK").GetComponent<Text>();var hp=view.Find("Stat HP").GetComponent<Text>();
                verify(BadgeInsideArt(view,atk) && BadgeInsideArt(view,hp),"Six-card hand keeps lower numbers inside artwork: "+card.Definition.CardID);
                view.GetComponent<CardHover>().OnPointerEnter(DragData(view));Canvas.ForceUpdateCanvases();
                verify(BadgeInsideArt(view,atk) && BadgeInsideArt(view,hp),"Hover zoom keeps lower numbers inside artwork: "+card.Definition.CardID);view.GetComponent<CardHover>().OnPointerExit(DragData(view));
            }
            foreach(int width in new[]{1280,1920})verify(System.IO.File.Exists(CaptureAttackFrame(width,handContent,"CardBadgesSix",false)),"Rendered repaired six-card hand: "+width);
            CheckHand("Unknown_K");CheckHand("Joker_1");Refresh();yield return new WaitForSecondsRealtime(.4f);Canvas.ForceUpdateCanvases();
            foreach(var card in engine.State.Players[0].Hand){var view=cardViews[card.InstanceID];verify(BadgeInsideArt(view,view.Find("Stat ATK").GetComponent<Text>()) && BadgeInsideArt(view,view.Find("Stat HP").GetComponent<Text>()),"Hand reflow keeps lower numbers inside artwork: "+card.Definition.CardID);}
        }
        void VerifyCardArt(Action<bool,string> verify)
        {
            var settings=Resources.Load<CardArtSettings>("CardArtSettings");
            verify(settings!=null && settings.cards.Length==57 && settings.cards.Select(e=>e.cardID).Distinct().Count()==57,"All 57 card identities have separate art bindings");
            foreach(var definition in CardDatabase.All) {
                var rect=Box(battlefield,"Art check",Vector2.one*.5f,Vector2.one*.5f,paper);rect.sizeDelta=new Vector2(180,244);
                var card=new CardInstance(-1,definition,0);DrawCard(rect,card,false,false,()=>{},false);Canvas.ForceUpdateCanvases();
                var art=rect.Find("Card Artwork")?.GetComponent<RawImage>();var expected=UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(ExpectedCardArtPath(definition));
                verify(art!=null && expected!=null && art.texture==expected && !art.raycastTarget,"Exact supplied card artwork: "+definition.CardID);
                verify(rect.Find("Mana cost").GetComponentInChildren<Text>().text==definition.ManaCost.ToString() && rect.Find("Card Description").GetComponent<Text>().text==definition.Description
                    && rect.Find("Stat HP").GetComponent<Text>().text==(definition.CardType==CardType.Joker ? "" : card.CurrentHP.ToString()) && rect.Find("Stat ATK").GetComponent<Text>().text==(definition.CardType==CardType.Joker ? "50%" : card.Attack.ToString()),"Model supplies live cost, description and stats: "+definition.CardID);
                verify(rect.GetComponent<Outline>().useGraphicAlpha && rect.GetComponent<Image>().color.a==0 && rect.Find("Artwork Border")==null,"No added border around card art: "+definition.CardID);
                                var badgeTexts=new[]{rect.Find("Mana cost").GetComponentInChildren<Text>(),rect.Find("Stat ATK").GetComponent<Text>(),rect.Find("Stat HP").GetComponent<Text>()};
                verify(badgeTexts.Where(t=>!string.IsNullOrEmpty(t.text)).All(t=>t.GetComponent<CardBadgeCenter>()!=null && t.canvasRenderer.GetMesh()!=null && Vector2.Distance(t.canvasRenderer.GetMesh().bounds.center,t.rectTransform.rect.center)<.6f),"Visible digits are optically centered: "+definition.CardID);
                rect.gameObject.SetActive(false);Destroy(rect.gameObject);
            }
            CheckFixture();CheckUnit("Unknown_K",0,0);CheckUnit("Club_Q",0,1);CheckUnit("Heart_4",0,2);CheckUnit("Diamond_J",1,1);CheckUnit("Spade_A",1,2);
            foreach(var id in new[]{"Diamond_5","Spade_A","Heart_Q","Club_10","Joker_1","Joker_2","Unknown_K","Unknown_J"})CheckHand(id);
            Refresh();Canvas.ForceUpdateCanvases();
            foreach(var width in new[]{1280,1920})verify(System.IO.File.Exists(CaptureAttackFrame(width,handContent,"CardArtwork",false)),"Rendered card art gallery at "+width);
        }
    }
}
#endif
