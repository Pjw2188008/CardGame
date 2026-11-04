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
        Text mapHeader,mapStatus,normalMapName;
        bool mapIntroPresented;
        RectTransform mapIntro;
        internal int MapIntroCount { get; private set; }
        internal bool IsMapIntroActive => mapIntro!=null && mapIntro.gameObject.activeSelf;
        static string MapTitle(BattleMap map) => map==BattleMap.JokerGame ? "JOKER'S GAME" : "NORMAL BATTLEFIELD";
        static string MapDescription(BattleMap map) => map==BattleMap.JokerGame
            ? "매 턴 무작위 사건 발생\nJOKER 도박 3회 성공 또는 상대 HP 0 시 승리"
            : "UNKNOWN 3장 생성 또는 상대 HP 0 시 승리";
        IEnumerator PresentRandomMap(BattleMap result)
        {
            if(mapIntroPresented)yield break;
            mapIntroPresented=true; MapIntroCount++;
            mapIntro=Box(canvas.transform,"Random Map Reveal",Vector2.zero,Vector2.one,new Color(0.025f,0.015f,0.045f,1));
            mapIntro.SetAsLastSibling(); mapIntro.GetComponent<Image>().raycastTarget=true;
            var heading=Label(mapIntro,"RANDOM MAP",new Vector2(0.1f,0.65f),new Vector2(0.9f,0.74f),32,gold);
            var name=Label(mapIntro,"",new Vector2(0.08f,0.47f),new Vector2(0.92f,0.62f),48,Color.white);
            var description=Label(mapIntro,"",new Vector2(0.08f,0.28f),new Vector2(0.92f,0.44f),24,new Color(0.85f,0.75f,0.95f));
            // Fixed visual alternation; no RNG and no authority mutation during playback.
            double began=Time.realtimeSinceStartupAsDouble;
            while(Time.realtimeSinceStartupAsDouble-began<2)
            {
                float elapsed=(float)(Time.realtimeSinceStartupAsDouble-began);
                name.text=elapsed<0.8f ? MapTitle(((int)(elapsed/0.09f)%2)==0 ? BattleMap.Normal : BattleMap.JokerGame) : MapTitle(result);
                description.text=elapsed<0.8f ? "" : MapDescription(result);
                // Rapid name changes may repack a shared dynamic font atlas.
                heading.SetVerticesDirty(); name.SetVerticesDirty(); description.SetVerticesDirty();
                yield return null;
            }
            mapIntro.gameObject.SetActive(false); Destroy(mapIntro.gameObject); mapIntro=null;
        }
        void RefreshMapUI()
        {
            if(engine==null || ViewPlayer<0 || ViewPlayer>1)return;
            bool joker=engine.Config.Map==BattleMap.JokerGame;
            if(normalMapName==null)normalMapName=Label(battlefield,"일반 전장",new Vector2(0.015f,0.84f),new Vector2(0.18f,0.925f),20,new Color(0.8f,0.84f,0.9f));
            normalMapName.gameObject.SetActive(!joker);
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
