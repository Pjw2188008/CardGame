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
        RawImage mapBackground;
        AspectRatioFitter mapBackgroundAspect;
        BattleMapSettings mapBackgroundSettings;
        Text mapHeader,mapStatus,normalMapName,mapSymbols;
        bool mapIntroPresented;
        RectTransform mapIntro;
        internal int MapIntroCount { get; private set; }
        internal bool IsMapIntroActive => mapIntro!=null && mapIntro.gameObject.activeSelf;
        MapDefinition MapInfo(BattleMap map) => MapDefinition.For(map,engine?.Config.MapRules);
        string MapTitle(BattleMap map) { var d=MapInfo(map); return d.symbol+" "+d.title; }
        string MapDescription(BattleMap map) => MapInfo(map).Effects+(MapDefinition.IsSuit(map) ? "\n필드에 UNKNOWN 3장 배치 또는 상대 HP 0 시 승리" : "");
        Color MapTint(BattleMap map) { ColorUtility.TryParseHtmlString(MapInfo(map).tint,out var color); return color; }
        void ShowMapTooltip(UnityEngine.EventSystems.PointerEventData data)
        {
            if(AttackDragging || dragged!=null || passOverlay.activeSelf || resultOverlay.activeSelf)return;
            HideCardPreview();tooltip.sizeDelta=new Vector2(340,220);
            var d=MapInfo(engine.Config.Map); detail.text=d.symbol+" "+d.name+"\n\n"+MapDescription(d.map); PositionTooltip(data);
        }
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
                var cycle=new[]{BattleMap.Diamond,BattleMap.Spade,BattleMap.Heart,BattleMap.Club,BattleMap.JokerGame};
                name.text=elapsed<0.8f ? MapTitle(cycle[(int)(elapsed/0.09f)%cycle.Length]) : MapTitle(result);
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
            var info=MapInfo(engine.Config.Map); var tint=MapTint(engine.Config.Map);
            if(normalMapName==null)normalMapName=Label(battlefield,"일반 전장",new Vector2(0.015f,0.84f),new Vector2(0.18f,0.925f),20,new Color(0.8f,0.84f,0.9f));
            normalMapName.text=info.symbol+" "+info.name; normalMapName.color=tint;
            normalMapName.gameObject.SetActive(!joker);
            if(mapDecor==null)
            {
                mapDecor=Box(battlefield,"Joker Map Decoration",Vector2.zero,Vector2.one,Color.clear); mapDecor.SetAsFirstSibling();
                mapSymbols=Label(mapDecor,"",new Vector2(0.02f,0.38f),new Vector2(0.98f,0.7f),95,Color.white);
                mapHeader=Label(mapDecor,"",new Vector2(0.015f,0.84f),new Vector2(0.18f,0.925f),20,new Color(0.88f,0.67f,1));
                mapStatus=Label(mapDecor,"",new Vector2(0.015f,0.66f),new Vector2(0.18f,0.81f),15,new Color(0.8f,0.68f,0.85f));
                var hover=Box(battlefield,"Map details hover",new Vector2(0.015f,0.84f),new Vector2(0.18f,0.925f),Color.clear);
                hover.GetComponent<Image>().raycastTarget=true; hover.gameObject.AddComponent<CardHover>().Configure(ShowMapTooltip,HideTooltip,true,false);
            }
            if(mapBackground==null)
            {
                mapBackgroundSettings=Resources.Load<BattleMapSettings>("BattleMapSettings");
                var background=new GameObject("Map Background",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter));
                background.transform.SetParent(battlefield,false);
                mapBackground=background.GetComponent<RawImage>();mapBackground.raycastTarget=false;
                mapBackgroundAspect=background.GetComponent<AspectRatioFitter>();
                mapBackgroundAspect.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;
            }
            var texture=mapBackgroundSettings==null ? null : mapBackgroundSettings.BackgroundFor(info.map);
            if(mapBackground.texture!=texture) {
                mapBackground.texture=texture;
                if(texture!=null)mapBackgroundAspect.aspectRatio=(float)texture.width/texture.height;
            }
            mapBackground.gameObject.SetActive(texture!=null);
            mapBackground.transform.SetAsFirstSibling();
            battlefield.GetComponent<Image>().color=texture!=null ? Color.clear : info.map==BattleMap.Normal ? navy : new Color(0.025f+tint.r*0.045f,0.015f+tint.g*0.03f,0.025f+tint.b*0.05f);
            mapDecor.gameObject.SetActive(info.map!=BattleMap.Normal); mapHeader.gameObject.SetActive(joker);
            mapSymbols.text=info.symbol+"       "+info.symbol+"       "+info.symbol+"       "+info.symbol; mapSymbols.color=new Color(tint.r,tint.g,tint.b,0.07f);
            mapSymbols.gameObject.SetActive(texture==null);
            if(!joker)mapStatus.text=MapDefinition.IsSuit(info.map) ? info.Effects : "";
            if(joker)
            {
                mapHeader.text="조커의 유희\n♠  ♦  ♣  ♥\n다음 JOKER "+engine.JokerChance(ViewPlayer)+"%";
                var visible=MapEventView.For(engine.MapEvent,ViewPlayer);mapStatus.text=visible==null ? "" : visible.name+"\n"+visible.detail;
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
            if(e.Kind==BattleEventKind.SuitMapTurn)
            {
                var effect=engine.SuitEffect;
                if(effect!=null && effect.id==e.SourceID)yield return animations.Banner(MapInfo(effect.map).title+" EFFECT\n"+effect.detail,MapTint(effect.map),1f,160,24);
                yield break;
            }
            var result=MapEventView.For(engine.MapEvent,ViewPlayer)?.ToResult();
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
                DrawCard(front,card,false,false,()=>{},false);front.GetComponent<CardDrag>().enabled=false;
                var visible=card;
                front.GetComponent<CardHover>().Configure(data=>ShowTooltip(visible,front,data),HideTooltip,true,false);
            }
        }
    }
}
