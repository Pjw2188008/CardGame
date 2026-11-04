using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    public sealed partial class BattleUIController
    {
        readonly HashSet<int> presentedMapEvents=new HashSet<int>();
        RectTransform mapDecor;
        Text mapHeader,mapStatus;
        void RefreshMapUI()
        {
            if(engine==null || ViewPlayer<0 || ViewPlayer>1)return;
            bool joker=engine.Config.Map==BattleMap.JokerGame;
            if(joker && mapDecor==null)
            {
                mapDecor=Box(battlefield,"Joker Map Decoration",Vector2.zero,Vector2.one,Color.clear); mapDecor.SetAsFirstSibling();
                var symbols=Label(mapDecor,"♠       ♦       ♣       ♥",new Vector2(0.02f,0.38f),new Vector2(0.98f,0.7f),95,new Color(0.7f,0.12f,0.38f,0.07f));
                mapHeader=Label(mapDecor,"",new Vector2(0.015f,0.84f),new Vector2(0.18f,0.925f),20,new Color(0.88f,0.67f,1));
                mapStatus=Label(mapDecor,"",new Vector2(0.015f,0.66f),new Vector2(0.18f,0.81f),15,new Color(0.8f,0.68f,0.85f));
            }
            battlefield.GetComponent<Image>().color=joker ? new Color(0.065f,0.025f,0.095f) : navy;
            if(mapDecor!=null)mapDecor.gameObject.SetActive(joker);
            if(joker)
            {
                mapHeader.text="조커의 유희\n♠  ♦  ♣  ♥\n다음 JOKER "+engine.JokerChance(ViewPlayer)+"%";
                mapStatus.text=engine.MapEvent==null ? "" : engine.MapEvent.Name+"\n"+engine.MapEvent.Detail;
            }
            foreach(var pip in unknownPips[0])
            {
                if(pip==null)continue; var icon=pip.transform.Find("Joker Icon");
                if(icon==null && joker) { var label=Label(pip.transform,"♠",Vector2.zero,Vector2.one,17,Color.white); label.name="Joker Icon"; icon=label.transform; }
                if(icon!=null)icon.gameObject.SetActive(joker);
            }
        }
        IEnumerator PresentMapEvent(BattleEvent e)
        {
            if(!presentedMapEvents.Add(e.SourceID))yield break;
            RefreshMapUI(); RefreshOpponentReveal();
            var result=engine.MapEvent;
            if(result!=null && result.ID==e.SourceID)
                yield return animations.Banner("JOKER EVENT\n"+result.Name+"\n"+result.Detail,new Color(0.9f,0.65f,1),1.2f,190,23);
        }
        void RefreshOpponentReveal()
        {
            if(engine==null || ViewPlayer<0 || ViewPlayer>1)return;
            var card=engine.RevealedFor(ViewPlayer); int index=engine.RevealedIndexFor(ViewPlayer);
            for(int i=0;i<backViews.Count;i++)
            {
                var back=backViews[i]; if(back==null)continue;
                var face=back.Find("Revealed Face");
                bool show=card!=null && i==index;
                if(!show) { if(face!=null) { face.gameObject.SetActive(false); Destroy(face.gameObject); } continue; }
                if(face!=null)continue;
                var front=Box(back,"Revealed Face",Vector2.zero,Vector2.one,paper); front.GetComponent<Image>().raycastTarget=true;
                Label(front,CardDatabase.Symbol(card.Definition.Suit)+" "+CardDatabase.RankText(card.Definition.Rank),Vector2.zero,Vector2.one,17,SuitInk(card.Definition.Suit));
                var visible=card;
                front.gameObject.AddComponent<CardHover>().Configure(data=>ShowTooltip(visible,front,data),HideTooltip,true,false);
            }
        }
    }
}
