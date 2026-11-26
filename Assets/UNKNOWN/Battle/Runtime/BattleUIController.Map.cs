using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    public sealed partial class BattleUIController
    {
        readonly HashSet<int> presentedMapEvents=new HashSet<int>();
        RectTransform mapDecor,mapInfoPanel,mapDetailsHover;
        RawImage mapEffectIcon;Text mapEffectFallback;
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
        string MapDescription(BattleMap map) => singleCampaign!=null ? SingleRestrictionText() : MapInfo(map).Effects+(MapDefinition.IsSuit(map) ? "\n필드에 UNKNOWN 3장 배치 또는 상대 HP 0 시 승리" : "");
        Color MapTint(BattleMap map) { ColorUtility.TryParseHtmlString(MapInfo(map).tint,out var color); return color; }
        void ShowMapTooltip(UnityEngine.EventSystems.PointerEventData data)
        {
            if(AttackDragging || dragged!=null || passOverlay.activeSelf || resultOverlay.activeSelf)return;
            HideCardPreview();tooltip.sizeDelta=new Vector2(440,280);
            detail.fontSize=18;detail.resizeTextForBestFit=false;detail.horizontalOverflow=HorizontalWrapMode.Wrap;
            var d=MapInfo(engine.Config.Map);
            detail.text=singleCampaign!=null ? "방해 효과\n\n"+SingleRestrictionText() : d.symbol+" "+d.name+"\n\n"+MapDescription(d.map);
            if(singleCampaign==null && d.map==BattleMap.JokerGame){
                detail.text+="\n\n다음 JOKER "+engine.JokerChance(ViewPlayer)+"%";
                var visible=MapEventView.For(engine.MapEvent,ViewPlayer);if(visible!=null)detail.text+="\n\n"+visible.name+"\n"+visible.detail;
            }
            Canvas.ForceUpdateCanvases();
            tooltip.sizeDelta=new Vector2(440,Mathf.Clamp((detail.preferredHeight+24)/.9f,160,Mathf.Max(160,interactionRoot.rect.height-40)));
            PositionTooltip(data);
        }
        IEnumerator PresentRandomMap(BattleMap result)
        {
            if(singleCampaign!=null){mapIntroPresented=true;yield break;}
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
                mapDetailsHover=hover;hover.GetComponent<Image>().raycastTarget=true; hover.gameObject.AddComponent<CardHover>().Configure(ShowMapTooltip,HideTooltip,true,false);
            }
            if(mapBackground==null)
            {
                mapBackgroundSettings=Resources.Load<BattleMapSettings>("BattleMapSettings");
                var background=new GameObject("Map Background",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter));
                background.transform.SetParent(battlefield,false);
                mapBackground=background.GetComponent<RawImage>();mapBackground.raycastTarget=false;
                mapBackgroundAspect=background.GetComponent<AspectRatioFitter>();
                mapBackgroundAspect.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;
                var shade=Box(background.transform,"Background shade",Vector2.zero,Vector2.one,Unknown.Multiplayer.RoomVisualSettings.BattleShade);
                shade.GetComponent<Image>().raycastTarget=false;
            }
            var texture=mapBackgroundSettings==null ? null : mapBackgroundSettings.BackgroundFor(info.map);
            RefreshDeckArtwork(info.map);
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
            RefreshEffectIcon(info);
            foreach(var pip in unknownPips[0])
            {
                if(pip==null)continue; var icon=pip.transform.Find("Joker Icon");
                if(icon==null && joker) { var label=Label(pip.transform,"♠",Vector2.zero,Vector2.one,17,Color.white); label.name="Joker Icon"; icon=label.transform; }
                if(icon!=null)icon.gameObject.SetActive(joker);
            }
        }
        void RefreshEffectIcon(MapDefinition info)
        {
            // The tooltip uses live rules; this replaces the persistent description panel.
            normalMapName.gameObject.SetActive(false);mapHeader.gameObject.SetActive(false);mapStatus.gameObject.SetActive(false);
            mapDetailsHover.gameObject.SetActive(false);
            if(mapInfoPanel==null){
                mapInfoPanel=Box(battlefield,"Map effect icon",Vector2.zero,Vector2.zero,Color.clear);
                mapInfoPanel.sizeDelta=new Vector2(80,80);mapInfoPanel.GetComponent<Image>().raycastTarget=true;
                var artwork=new GameObject("Effect artwork",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter));
                artwork.transform.SetParent(mapInfoPanel,false);mapEffectIcon=artwork.GetComponent<RawImage>();mapEffectIcon.raycastTarget=false;
                var fit=artwork.GetComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;
                mapEffectFallback=Label(mapInfoPanel,"N",Vector2.zero,Vector2.one,30,gold);mapEffectFallback.raycastTarget=false;
                mapInfoPanel.gameObject.AddComponent<CardHover>().Configure(ShowMapTooltip,HideTooltip,false,false);
            }
            var anchor=singleCampaign!=null ? new Vector2(.08f,.575f) : new Vector2(.08f,.855f);
            mapInfoPanel.anchorMin=mapInfoPanel.anchorMax=anchor;mapInfoPanel.anchoredPosition=Vector2.zero;
            var texture=mapBackgroundSettings==null?null:mapBackgroundSettings.EffectIconFor(info.map);
            mapEffectIcon.texture=texture;mapEffectIcon.gameObject.SetActive(texture!=null);mapEffectFallback.gameObject.SetActive(texture==null);
            mapEffectFallback.text=info.symbol.Length>0?info.symbol:"N";
            if(texture!=null)mapEffectIcon.GetComponent<AspectRatioFitter>().aspectRatio=(float)texture.width/texture.height;
            mapInfoPanel.SetAsLastSibling();if(battleMenu!=null && battleMenu.gameObject.activeSelf)battleMenu.SetAsLastSibling();
        }
#if UNITY_EDITOR
        internal IEnumerator VerifyMapTextPresentation(System.Action<bool,string> verify,string folder,SingleCampaign campaign)
        {
            StopAllCoroutines();animations.ResetAll();actionBusy=transitioning=paused=false;
            passOverlay.SetActive(false);resultOverlay.SetActive(false);
            foreach(BattleMap map in System.Enum.GetValues(typeof(BattleMap)))foreach(int recipient in new[]{0,1}){
                checkRecipient=recipient;singleCampaign=null;
                var host=new BattleEngine(17,new BattleConfig{Map=map,StartingHand=0,CardVersion=CardDatabase.DefaultVersion});
                engine=BattleEngine.CreateView(BattleSnapshot.For(host,recipient,1,0,false,new BattleEvent[0]));
                ResetHandViews();Refresh();yield return null;yield return null;Canvas.ForceUpdateCanvases();
                ButtonTextLayout.VerifyVisible(gameObject,verify,"battle "+map+" / "+recipient);
                verify(endTurn is SingleButtonSkin,"Shared online/AI/tutorial combat button artwork: "+map+" / "+recipient);
                verify(mapInfoPanel.gameObject.activeInHierarchy && !mapStatus.gameObject.activeSelf && !normalMapName.gameObject.activeSelf,"Effects replaced by hover icon: "+map+" / "+recipient);
                verify(mapEffectIcon.texture==mapBackgroundSettings.EffectIconFor(map) && (map==BattleMap.Normal || mapEffectIcon.texture!=null),"Correct imported effect icon: "+map+" / "+recipient);
                mapInfoPanel.GetComponent<CardHover>().OnPointerEnter(DragData(mapInfoPanel));
                verify(tooltip.gameObject.activeSelf && detail.text.Contains(MapInfo(map).Effects),"Icon hover shows actual map effects: "+map+" / "+recipient);
                verify(detail.rectTransform.rect.height>=detail.preferredHeight,"Map tooltip fits full description: "+map+" / "+recipient);
                if(map==BattleMap.Heart && recipient==0 || map==BattleMap.JokerGame && recipient==1){
                    ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(folder+"/map-text-"+map+".png"));yield return new WaitForEndOfFrame();yield return null;
                }
            }
            checkRecipient=0;CheckFixture();
            engine=new BattleEngine(17,new BattleConfig{StartingHand=0,StartingMana=10,CardVersion=CardDatabase.DefaultVersion});
            engine.State.ActivePlayer=0;
            var attacker=CheckUnit("Heart_K",0);
            Refresh();
            verify(CombatBorder(cardViews[attacker.InstanceID]).isActiveAndEnabled && heroAttackIcon.gameObject.activeSelf,"Art border and direct-attack badge appear before choosing an attacker");
            var enemy=CheckUnit("Heart_K",1);
            var otherEnemy=CheckUnit("Heart_2",1,1);
            enemy.CardWeakening=1;enemy.AttackWeakTurns=2;enemy.HealthWeakening=1;enemy.HealthWeakTurns=3;
            Refresh();attackerID=attacker.InstanceID;RefreshAttackIndicators();
            verify(!heroAttackIcon.gameObject.activeSelf && CombatBorder(cardViews[enemy.InstanceID]).isActiveAndEnabled && CombatBorder(cardViews[otherEnemy.InstanceID]).isActiveAndEnabled,"All attackable enemy units glow and block direct attack");
            var surface=FeedbackSurface(cardViews[enemy.InstanceID]);
            verify(surface.Find("Attack weakness turns").GetComponentInChildren<Text>().text=="ATK↓ 2턴"
                && surface.Find("Health weakness turns").GetComponentInChildren<Text>().text=="HP↓ 3턴","Independent weakness durations displayed");
            enemy.CardWeakening=0;enemy.AttackWeakTurns=0;UpdateCardStats(cardViews[enemy.InstanceID],enemy,true);
            verify(!surface.Find("Attack weakness turns").gameObject.activeSelf && surface.Find("Health weakness turns").gameObject.activeSelf,"Only the expired status badge disappears");
            var spell=CheckHand("Diamond_2");
            verify(engine.Summon(0,spell.InstanceID,1) && engine.NeedsTarget,"Targeting fixture opens a real effect selection");
            Refresh();
            verify(CombatBorder(cardViews[enemy.InstanceID]).color.r==gold.r && CombatBorder(cardViews[enemy.InstanceID]).color.g==gold.g
                && !CombatBorder(cardViews[attacker.InstanceID]).gameObject.activeSelf,"Effect targets use gold while attack readiness is suppressed");
            // Enter single combat through its real lifecycle rather than repurposing the multiplayer HUD.
            gameObject.SetActive(false);var soloObject=new GameObject("Existing single battle info checks");
            var solo=soloObject.AddComponent<BattleUIController>();solo.ConfigureSingle(campaign,()=>{});
            yield return null;yield return null;
            solo.StopAllCoroutines();solo.animations.ResetAll();solo.actionBusy=solo.transitioning=solo.paused=false;solo.passOverlay.SetActive(false);solo.Refresh();
            solo.RefreshSingleUI();
            yield return null;yield return null;Canvas.ForceUpdateCanvases();
            ButtonTextLayout.VerifyVisible(solo.gameObject,verify,"single battle");
            verify(!System.Linq.Enumerable.Any(solo.GetComponentsInChildren<Button>(true),b=>b.name=="전투 정보") && solo.transform.Find("Chapter intent")==null,"Single combat info button and panel removed");
            solo.mapInfoPanel.GetComponent<CardHover>().OnPointerEnter(solo.DragData(solo.mapInfoPanel));
            verify(solo.tooltip.gameObject.activeSelf && solo.detail.text=="방해 효과\n\n"+solo.SingleRestrictionText(),"Single icon hover shows actual active restrictions");
            verify(solo.detail.rectTransform.rect.height>=solo.detail.preferredHeight,"Single restriction tooltip fits full text");
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(folder+"/single-restrictions.png"));yield return new WaitForEndOfFrame();yield return null;
            Destroy(soloObject);gameObject.SetActive(true);
        }
#endif
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
