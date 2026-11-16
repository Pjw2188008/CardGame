using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
namespace Unknown.Battle
{
    public sealed class SingleChapterUI : MonoBehaviour
    {
        public SingleCampaign Campaign {get;private set;}RectTransform root;
#if UNITY_EDITOR
        internal static string CheckSavePath;
#endif
        RectTransform tooltip;Text tooltipText;ScrollRect mapScroll;float mapOffset=-1;
        Font font;Action returnTitle;int difficulty,selected=-1,replaceSlot=-1;bool replaceCard;string error="";BattleUIController battle;
        public static SingleChapterUI Open(Action onReturn) {
            var go=new GameObject("UNKNOWN Chapter 1");var ui=go.AddComponent<SingleChapterUI>();ui.returnTitle=onReturn;return ui;
        }
        void Start() {
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},18);
            var go=new GameObject("Chapter Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1100;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            root=go.GetComponent<RectTransform>();
            if(FindFirstObjectByType<EventSystem>()==null){var e=new GameObject("Single EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));e.transform.SetParent(transform,false);e.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();}
            try{Campaign=new SingleCampaign(SingleChapterData.Load()
#if UNITY_EDITOR
                ,CheckSavePath
#endif
                );Home();}catch(Exception ex){Clear("저장 또는 데이터 오류");Text(ex.Message,.1f,.3f,.8f,.4f,24);Button("타이틀",.4f,.12f,.2f,.07f,Exit);}
        }
        RectTransform Rect(Transform parent,string name,float x,float y,float w,float h,Color? color=null) {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=new Vector2(x,y);r.anchorMax=new Vector2(x+w,y+h);r.offsetMin=r.offsetMax=Vector2.zero;
            if(color.HasValue){var i=go.AddComponent<Image>();i.color=color.Value;}return r;
        }
        void Clear(string title,bool permanent=false) {
            tooltip=null;tooltipText=null;foreach(Transform child in root){child.gameObject.SetActive(false);Destroy(child.gameObject);}Rect(root,"Background",0,0,1,1,new Color(.025f,.03f,.05f));
            var visuals=Resources.Load<Unknown.Multiplayer.RoomVisualSettings>("RoomVisualSettings");
            if(visuals!=null && visuals.menuBackground!=null)Artwork(root,"Menu artwork",visuals.menuBackground,0,0,1,1,true);
            var shade=Rect(root,"Shade",0,0,1,1,new Color(.015f,.008f,.018f,.48f));shade.GetComponent<Image>().raycastTarget=false;
            Text(title,.08f,.89f,.84f,.07f,34);
            if(Campaign!=null && permanent)Text("◆ 영구 토큰 "+Campaign.Profile.permanentTokens,.78f,.96f,.2f,.03f,18);
            if(error.Length>0){Text(error,.05f,.015f,.9f,.04f,18);error="";}
        }
        Text Text(string value,float x,float y,float w,float h,int size=20,Transform parent=null) {
            var r=Rect(parent??root,"Text",x,y,w,h);var t=r.gameObject.AddComponent<Text>();t.font=font;t.text=value;t.fontSize=size;t.color=Color.white;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.resizeTextForBestFit=true;t.resizeTextMinSize=Math.Min(14,size);t.resizeTextMaxSize=size;return t;
        }
        Button Button(string title,float x,float y,float w,float h,Action action,bool enabled=true,Transform parent=null) {
            var p=parent??root;var r=Rect(p,title,x,y,w,h,new Color(.13f,.075f,.09f,.96f));var edge=r.gameObject.AddComponent<Outline>();edge.effectColor=new Color(.55f,.39f,.16f,.8f);edge.effectDistance=new Vector2(1,-1);
            var b=r.gameObject.AddComponent<Button>();b.interactable=enabled;
            var colors=b.colors;colors.disabledColor=new Color(.32f,.32f,.35f,.55f);b.colors=colors;
            Text(title,.03f,.04f,.94f,.92f,18,r);b.onClick.AddListener(()=>{try{action();}catch(Exception ex){error=ex.Message;Debug.LogException(ex);Home();}});return b;
        }
        public void Home() {
            root.gameObject.SetActive(true);Clear("CHAPTER 1",true);
            Text("난이도",.1f,.74f,.8f,.04f);
            for(int i=0;i<4;i++){int n=i;var d=Campaign.Data.difficulties[i];var b=Button((difficulty==i?"✓ ":"")+d.name,.12f+i*.195f,.65f,.175f,.065f,()=>{difficulty=n;Home();},Campaign.Profile.unlocked.Contains(i));Hint(b.gameObject,d.name+" · 보상 "+d.rewardPercent+"%"+(Campaign.Profile.unlocked.Contains(i)?"":" · 콘텐츠 해금 필요"));}
            var start=Button("새로운 런",.33f,.47f,.34f,.075f,()=>{Campaign.Start(difficulty,Environment.TickCount);selected=-1;mapOffset=-1;Map();});Hint(start.gameObject,"기본 덱 12장으로 시작합니다. 진행 중인 런은 종료됩니다.");
            Button("이어하기",.33f,.36f,.34f,.075f,Continue,Campaign.Run!=null && Campaign.Run.phase!="done");
            Button("해금 콘텐츠",.33f,.24f,.34f,.075f,Shop);Button("뒤로 가기",.33f,.13f,.34f,.075f,Exit);
        }
        void Exit(){returnTitle?.Invoke();Destroy(gameObject);}
        void Continue() {
            switch(Campaign.Run.phase){case "map":Map();break;case "battle":LaunchBattle();break;case "prepare":Prepare();break;case "done":Result();break;default:Reward();break;}
        }
        void Header() {
            var r=Campaign.Run;Text($"HP {r.hp}/{r.maxHP}   ◆ 런 토큰 {r.tokens}   덱 {r.deck.Count}장   {Campaign.Data.difficulties[r.difficulty].name}",.08f,.83f,.84f,.045f,21);
            if(r.augments.Count>0)Text("증강: "+string.Join(" / ",r.augments.Select(id=>Campaign.Data.augments.First(a=>a.id==id).name)),.1f,.78f,.8f,.04f,18);
        }
        public void Map() {
            if(mapScroll!=null && mapScroll.gameObject.activeInHierarchy)mapOffset=mapScroll.horizontalNormalizedPosition;
            Clear("CHAPTER 1");var r=Campaign.Run;
            Text($"HP {r.hp}/{r.maxHP}     ◆ 런 토큰 {r.tokens}",.05f,.815f,.62f,.05f,23);
            var viewport=Rect(root,"Route viewport",.035f,.18f,.66f,.60f,new Color(.025f,.018f,.035f,.82f));viewport.gameObject.AddComponent<RectMask2D>();
            mapScroll=viewport.gameObject.AddComponent<ScrollRect>();mapScroll.horizontal=true;mapScroll.vertical=false;mapScroll.scrollSensitivity=45;mapScroll.viewport=viewport;mapScroll.movementType=ScrollRect.MovementType.Clamped;
            var plot=Rect(viewport,"Route graph",0,0,0,1);plot.pivot=new Vector2(0,.5f);plot.sizeDelta=new Vector2(2600,0);mapScroll.content=plot;
            foreach(var n in r.nodes)foreach(int target in n.next) {
                bool passed=Enumerable.Range(0,Math.Max(0,r.visited.Count-1)).Any(i=>r.visited[i]==n.id && r.visited[i+1]==target);
                bool current=n.id==r.current;var state=Campaign.NodeState(target);
                Color color=passed?new Color(.48f,.57f,.65f):current?(state==SingleNodeState.Available?new Color(.35f,.85f,.76f):new Color(.8f,.3f,.3f)):new Color(.33f,.28f,.29f,.6f);
                bool highlight=current && target==selected;
                if(highlight)color=state==SingleNodeState.Available?new Color(1,.79f,.32f):new Color(1,.4f,.35f);
                var line=Rect(plot,"Path "+n.id+"-"+target,0,0,0,0,color);line.GetComponent<Image>().raycastTarget=false;
                var path=line.gameObject.AddComponent<SinglePathLine>();path.from=Position(n);path.to=Position(r.nodes[target]);path.width=highlight?5:current?4:2;
            }
            foreach(var n in r.nodes) {
                var stage=Campaign.Data.stages.First(s=>s.id==n.stage);var pos=Position(n);int id=n.id;var state=Campaign.NodeState(id);
                string turns=Combat(stage)?"◷ 제한 턴 "+EffectiveTurns(stage):"";
                var button=Button(NodeIcon(stage.kind)+" "+ShortKind(stage.kind)+"\n"+turns+"\n◆ 진입 토큰 "+stage.cost+"\n"+StateName(state),pos.x-.035f,pos.y-.105f,.07f,.21f,()=>{selected=id;Map();},true,plot);
                button.name="Node "+id;button.GetComponent<Image>().color=NodeColor(state);
                var edge=button.GetComponent<Outline>();edge.effectDistance=Vector2.one*(selected==id?3:1);edge.effectColor=selected==id?new Color(1,.8f,.32f):new Color(.48f,.35f,.2f);
            }
            var track=Rect(root,"Route scrollbar",.04f,.145f,.65f,.014f,new Color(.11f,.08f,.09f));
            var handle=Rect(track,"Handle",0,0,.3f,1,new Color(.58f,.4f,.19f));var scrollbar=track.gameObject.AddComponent<Scrollbar>();scrollbar.handleRect=handle;scrollbar.targetGraphic=handle.GetComponent<Image>();scrollbar.direction=Scrollbar.Direction.LeftToRight;mapScroll.horizontalScrollbar=scrollbar;
            MapDetail();
            Button("메인 메뉴",.04f,.055f,.18f,.06f,()=>{Campaign.Save();Home();});Button("현재 덱",.25f,.055f,.18f,.06f,Deck);Button("증강",.46f,.055f,.18f,.06f,Augments);
            Canvas.ForceUpdateCanvases();mapScroll.horizontalNormalizedPosition=mapOffset<0?Mathf.Clamp01((r.nodes[r.current].layer-1)/6f):mapOffset;
        }
        void MapDetail() {
            var panel=Rect(root,"Stage detail",.72f,.145f,.25f,.68f,new Color(.035f,.018f,.03f,.96f));
            if(selected<0 || selected>=Campaign.Run.nodes.Length){Text("스테이지 선택",.05f,.44f,.9f,.1f,24,panel);return;}
            var stage=Campaign.Data.stages.First(s=>s.id==Campaign.Run.nodes[selected].stage);var maps=Resources.Load<BattleMapSettings>("BattleMapSettings");
            if(maps!=null && Combat(stage))Artwork(panel,"Stage map artwork",maps.BackgroundFor(stage.map),.04f,.73f,.92f,.23f);
            Text(stage.name,.04f,.65f,.92f,.065f,25,panel);
            Text(NodeIcon(stage.kind)+" "+Kind(stage.kind),.04f,.59f,.92f,.05f,18,panel);
            string details=Combat(stage)?$"적 HP {stage.enemyHP}\n◷ 제한 턴 {EffectiveTurns(stage)}\n총 마나 예산 {Math.Max(0,stage.manaBudget-Campaign.Data.difficulties[Campaign.Run.difficulty].budgetPenalty)}":"";
            Text(details+"\n◆ 진입 토큰 "+stage.cost+"\n방해 효과: "+Restrictions(stage)+"\n보상: "+RewardName(stage),.06f,.24f,.88f,.34f,19,panel);
            string reason=Campaign.EntryError(selected);Text(reason,.06f,.13f,.88f,.10f,18,panel).color=new Color(1,.64f,.57f);
            Button("진입",.10f,.035f,.80f,.075f,()=>{Campaign.Enter(selected);Prepare();},reason.Length==0,panel);
        }
        Vector2 Position(SingleNode n)=>new Vector2(.045f+n.layer*.10f,n.layer==0 || n.layer==9?.5f:.80f-n.lane*.30f);
        bool Combat(SingleStage stage)=>stage.kind==SingleNodeKind.Battle || stage.kind==SingleNodeKind.Elite || stage.kind==SingleNodeKind.Boss;
        int EffectiveTurns(SingleStage stage)=>Math.Max(1,stage.turnLimit-Campaign.Data.difficulties[Campaign.Run.difficulty].turnPenalty);
        string Restrictions(SingleStage stage)=>string.Join(" / ",stage.restrictions.Concat(new[]{SingleRestriction.FirstCost,SingleRestriction.AttackLimit,SingleRestriction.HalfHealing}.Take(Combat(stage)?Campaign.Data.difficulties[Campaign.Run.difficulty].extraRestrictions:0)).Distinct().Select(RestrictionName).DefaultIfEmpty("없음"));
        static string StateName(SingleNodeState state)=>new[]{"현재 위치","이동 가능","토큰 부족","미도달","완료","이전 분기"}[(int)state];
        static Color NodeColor(SingleNodeState state)=>new[]{new Color(.42f,.29f,.10f),new Color(.08f,.29f,.25f),new Color(.36f,.09f,.11f),new Color(.08f,.065f,.09f),new Color(.18f,.22f,.27f),new Color(.07f,.06f,.065f)}[(int)state];
        static string ShortKind(SingleNodeKind kind)=>new[]{"시작","일반 전투","엘리트","증강","회복","덱 정리","이벤트","BOSS"}[(int)kind];
        void Augments() {
            Clear("증강");var r=Campaign.Run;
            if(r.augments.Count==0)Text("장착 증강 없음",.15f,.42f,.7f,.12f,24);
            for(int i=0;i<r.augments.Count;i++){var aug=Campaign.Data.augments.First(a=>a.id==r.augments[i]);Text(aug.name+"\n"+aug.description,.14f,.65f-i*.17f,.72f,.14f,22);}
            Button("돌아가기",.35f,.08f,.3f,.065f,Continue);
        }
        void Prepare() {
            var s=Campaign.Stage;Clear(s.name);Header();
            bool combat=s.kind==SingleNodeKind.Battle || s.kind==SingleNodeKind.Elite || s.kind==SingleNodeKind.Boss;
            if(combat) {
                var d=Campaign.Data.difficulties[Campaign.Run.difficulty];
                Text($"{s.enemyType} · 적 HP {s.enemyHP} · ◷ 제한 턴 {Math.Max(1,s.turnLimit-d.turnPenalty)}\n매 턴 소환 {s.enemySummonsPerTurn}장"+(s.kind==SingleNodeKind.Battle?"":" · 조건 충족 시 최대 2장")+"\n\n"+string.Join(" / ",s.restrictions.Concat(new[]{SingleRestriction.FirstCost,SingleRestriction.AttackLimit,SingleRestriction.HalfHealing}.Take(d.extraRestrictions)).Distinct().Select(RestrictionName))+"\n\n"+RewardName(s),.15f,.36f,.7f,.35f,24);
                var info=Button("전투 상세",.37f,.27f,.26f,.05f,()=>{});
                Hint(info.gameObject,$"시작 마나 {s.startMana} · 회복 {s.manaRecovery} · 턴 상한 {s.turnSpendLimit}\n총 마나 예산 {Math.Max(0,s.manaBudget-d.budgetPenalty)}\n초기 적: "+string.Join(" / ",s.units.Select(u=>CardDatabase.Get(u.card).CardName))+(Campaign.IsTeachingBattle?"\n첫 전투 손패에 A·J 보장":""));
                Button("전투 시작",.34f,.16f,.32f,.08f,LaunchBattle);
            }else {
                Text(Kind(s.kind)+"\n"+RewardName(s)+$"\n진입 토큰 {s.cost} 차감 완료",.15f,.34f,.7f,.3f,26);
                Button("진행",.34f,.18f,.32f,.08f,()=>{Campaign.ResolveSupport();Continue();});
            }
        }
        void LaunchBattle() {
            if(battle!=null)return;root.gameObject.SetActive(false);var go=new GameObject("UNKNOWN Chapter Battle");battle=go.AddComponent<BattleUIController>();
            battle.ConfigureSingle(Campaign,()=>{battle=null;root.gameObject.SetActive(true);if(Campaign.Run.phase=="battle")Home();else Continue();});
        }
        void Reward() {
            var r=Campaign.Run;Clear(r.phase=="card" || r.phase=="augment" ? (Campaign.Stage.kind==SingleNodeKind.Augment?"증강 선택":"VICTORY") : Kind(Campaign.Stage.kind));
            if(r.phase=="card" || r.phase=="augment") {
                Text("남은 HP "+r.hp+"/"+r.maxHP+"   ◆ 획득 토큰 +"+(Campaign.Stage.kind==SingleNodeKind.Augment?0:r.lastRunAward)+"   런 토큰 "+r.tokens,.1f,.70f,.8f,.05f);
                if(r.phase=="augment" && r.augments.Count==3){Text("교체할 증강을 먼저 선택하세요.",.1f,.60f,.8f,.05f);for(int i=0;i<3;i++){int n=i;Button((replaceSlot==i?"✓ ":"")+Campaign.Data.augments.First(a=>a.id==r.augments[i]).name,.1f+i*.28f,.51f,.26f,.07f,()=>{replaceSlot=n;Reward();});}}
                for(int i=0;i<r.offers.Count;i++) {
                    string id=r.offers[i];string text=r.phase=="card"?CardDatabase.Get(id).CardName+"\n"+CardDatabase.Get(id).Description:Campaign.Data.augments.First(a=>a.id==id).name+"\n"+Campaign.Data.augments.First(a=>a.id==id).description;
                    Action take=()=>{Campaign.TakeReward(id,replaceSlot);replaceSlot=-1;Continue();};
                    if(r.phase=="card")CardPreview(root,id,.12f+i*.29f,.27f,.18f,.39f,take,true);
                    else {var offer=Button(Campaign.Data.augments.First(a=>a.id==id).name,.08f+i*.30f,.28f,.28f,.19f,take,r.augments.Count<3 || replaceSlot>=0);Hint(offer.gameObject,text);}
                }
                Button("다음 단계 / 보상 건너뛰기",.35f,.12f,.3f,.07f,()=>{Campaign.SkipReward();Continue();});
            }else if(r.phase=="purge")Deck();
            else if(r.phase=="event") {
                Text("희생의 거래",.15f,.61f,.7f,.08f,30);Button($"HP {Campaign.Data.eventDamage}를 지불하고 런 토큰 {Campaign.Data.eventTokens} 획득",.2f,.42f,.6f,.1f,()=>{Campaign.Event(true);Continue();});Button($"안전하게 HP {Campaign.Data.eventHealing} 회복",.2f,.26f,.6f,.1f,()=>{Campaign.Event(false);Continue();});
            }
        }
        void Deck() {
            Clear(Campaign.Run.phase=="purge"?"덱 정리 — 카드를 선택하세요":"현재 덱");
            var r=Campaign.Run;
            if(r.phase=="purge")Button(replaceCard?"선택 카드 교체":"선택 카드 제거",.33f,.79f,.34f,.065f,()=>{replaceCard=!replaceCard;Deck();});
            var list=Scroll();Canvas.ForceUpdateCanvases();var grid=list.gameObject.AddComponent<GridLayoutGroup>();grid.cellSize=new Vector2(220,310);grid.spacing=new Vector2(24,24);grid.padding=new RectOffset(20,20,20,20);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=Math.Max(1,Mathf.FloorToInt((list.rect.width-16)/244));
            var fitter=list.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            for(int i=0;i<r.deck.Count;i++){int index=i;CardPreview(list,r.deck[i],0,0,0,0,()=>{if(r.phase=="purge"){Campaign.Purge(index,replaceCard);Continue();}},r.phase=="purge" && r.deck.Count>5);}

            Button("돌아가기 / 건너뛰기",.35f,.06f,.3f,.065f,()=>{if(r.phase=="purge")Campaign.CompleteNode();Continue();});
        }
        RectTransform Scroll() {
            var viewport=Rect(root,"Scroll",.12f,.18f,.76f,.58f,new Color(.05f,.055f,.08f));viewport.gameObject.AddComponent<RectMask2D>();
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.scrollSensitivity=40;scroll.viewport=viewport;
            var content=Rect(viewport,"Content",0,1,1,0);content.pivot=new Vector2(.5f,1);scroll.content=content;return content;
        }
        void ListButton(RectTransform list,int index,string title,Action action,bool enabled=true) {
            list.sizeDelta=new Vector2(0,(index+1)*65);var b=Button(title,0,1,1,0,action,enabled,list);var r=b.GetComponent<RectTransform>();r.pivot=new Vector2(.5f,1);r.sizeDelta=new Vector2(0,58);r.anchoredPosition=new Vector2(0,-index*65);
        }
        void Shop() {
            Clear("해금 콘텐츠",true);var list=Scroll();int index=0;
            for(int i=1;i<4;i++){int n=i;var d=Campaign.Data.difficulties[i];ListButton(list,index++,d.name+" · "+d.cost+" 영구 토큰 · "+Campaign.Data.difficulties[d.requiredClear].name+" 클리어 필요"+(Campaign.Profile.unlocked.Contains(i)?" [해금 완료]":""),()=>{Campaign.UnlockDifficulty(n);Shop();},!Campaign.Profile.unlocked.Contains(i) && Campaign.Profile.permanentTokens>=d.cost && (d.requiredClear<0 || Campaign.Profile.cleared.Contains(d.requiredClear)));}
            foreach(var u in Campaign.Data.unlocks.Where(u=>!Campaign.Data.starters.Any(st=>st.unlock==u.id))){string id=u.id;ListButton(list,index++,u.name+" · "+u.cost+" 영구 토큰"+(Campaign.Profile.content.Contains(id)?" [해금 완료]":""),()=>{Campaign.Unlock(id);Shop();},!Campaign.Profile.content.Contains(id) && Campaign.Profile.permanentTokens>=u.cost && (u.clear<0 || Campaign.Profile.cleared.Contains(u.clear)));}
            Button("돌아가기",.35f,.06f,.3f,.065f,Home);
        }
        void History() {
            Clear("플레이 기록",true);var list=Scroll();int i=0;foreach(var h in Campaign.Profile.history.AsEnumerable().Reverse())ListButton(list,i++,Campaign.Data.difficulties[h.difficulty].name+" · "+h.result+" · 방문 "+h.visited+" · HP "+h.hp+" · 영구 토큰 "+h.reward,()=>{},false);
            Button("돌아가기",.35f,.06f,.3f,.065f,Home);
        }
        void Result() {
            var r=Campaign.Run;Clear(r.failure.Length==0?"CHAPTER CLEAR":r.failure);Text($"{Campaign.Data.difficulties[r.difficulty].name}\n남은 HP {r.hp}/{r.maxHP}\n획득 영구 토큰 {r.permanentAward}\n최초 클리어 보너스 {r.firstClearAward}",.22f,.39f,.56f,.34f,26);
            Button("새 런 / 난이도 선택",.16f,.2f,.3f,.08f,Home);Button("해금 가능한 콘텐츠",.54f,.2f,.3f,.08f,Shop);Button("최종 덱 보기",.35f,.09f,.3f,.07f,Deck);
        }
        RawImage Artwork(Transform parent,string name,Texture2D texture,float x,float y,float w,float h,bool cover=false) {
            if(texture==null)return null;var frame=Rect(parent,name,x,y,w,h);if(cover)frame.gameObject.AddComponent<RectMask2D>();var rect=Rect(frame,"Texture",0,0,1,1);var image=rect.gameObject.AddComponent<RawImage>();image.texture=texture;image.raycastTarget=false;
            var fit=rect.gameObject.AddComponent<AspectRatioFitter>();fit.aspectRatio=(float)texture.width/texture.height;fit.aspectMode=cover?AspectRatioFitter.AspectMode.EnvelopeParent:AspectRatioFitter.AspectMode.FitInParent;return image;
        }
        void CardPreview(Transform parent,string id,float x,float y,float w,float h,Action click,bool enabled) {
            var def=CardDatabase.Get(id);var rect=Rect(parent,"Card "+id,x,y,w,h,Color.clear);rect.GetComponent<Image>().raycastTarget=true;
            var button=rect.gameObject.AddComponent<Button>();button.interactable=enabled;button.onClick.AddListener(()=>{try{click();}catch(Exception ex){error=ex.Message;Continue();}});
            var texture=CardArtSettings.TextureFor(id);if(texture==null){Text(def.CardName,0,0,1,1,22,rect);return;}
            var art=Rect(rect,"Card artwork",0,0,1,1).gameObject.AddComponent<RawImage>();art.texture=texture;art.raycastTarget=false;
            var mana=Text(def.ManaCost.ToString(),0,0,1,1,18,rect);var desc=Text(def.Description,0,0,1,1,10,rect);
            var attack=Text(def.CardType==CardType.Joker?"50%":def.Attack.ToString(),0,0,1,1,18,rect);var health=Text(def.MaxHP.ToString(),0,0,1,1,18,rect);health.gameObject.SetActive(def.CardType!=CardType.Joker);
            foreach(var text in new[]{mana,desc,attack,health}){text.color=new Color(.98f,.92f,.82f);var shadow=text.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.85f);shadow.effectDistance=new Vector2(1,-1);}
            foreach(var text in new[]{mana,attack,health})text.gameObject.AddComponent<CardBadgeCenter>();
            var layout=rect.gameObject.AddComponent<CardArtLayout>();layout.artwork=art;layout.mana=mana.rectTransform;layout.description=desc.rectTransform;layout.attack=attack.rectTransform;layout.health=health.rectTransform;layout.joker=def.CardType==CardType.Joker;layout.RefreshLayout();
            rect.gameObject.AddComponent<CardHover>().Configure(null,null,true,parent.GetComponent<LayoutGroup>()==null);
        }
        static string NodeIcon(SingleNodeKind k)=>new[]{"●","⚔","◆","✦","+","−","?","♛"}[(int)k];
        void Hint(GameObject target,string value) {
            var h=target.AddComponent<SingleChapterHint>();h.show=()=>ShowHint(value);h.hide=HideHint;
        }
        void HideHint(){if(tooltip!=null)tooltip.gameObject.SetActive(false);}
        void ShowHint(string value) {
            if(tooltip==null){tooltip=Rect(root,"Tooltip",0,0,0,0,new Color(.025f,.03f,.05f,.98f));tooltip.GetComponent<Image>().raycastTarget=false;tooltip.pivot=Vector2.zero;tooltip.sizeDelta=new Vector2(420,150);tooltipText=Text("",.04f,.05f,.92f,.9f,18,tooltip);}
            tooltipText.text=value;tooltip.gameObject.SetActive(true);tooltip.SetAsLastSibling();MoveHint();
        }
        void Update(){if(tooltip!=null && tooltip.gameObject.activeSelf)MoveHint();}
        void MoveHint() {
            var mouse=UnityEngine.InputSystem.Mouse.current;if(mouse==null)return;
            Vector2 point;RectTransformUtility.ScreenPointToLocalPointInRectangle(root,mouse.position.ReadValue(),null,out point);
            point-=root.rect.min;tooltip.anchoredPosition=new Vector2(Mathf.Clamp(point.x+18,0,Mathf.Max(0,root.rect.width-tooltip.rect.width)),Mathf.Clamp(point.y+18,0,Mathf.Max(0,root.rect.height-tooltip.rect.height)));
        }
        void OnDestroy(){if(battle!=null)Destroy(battle.gameObject);if(font!=null)Destroy(font);}
        public static string Kind(SingleNodeKind k)=>new[]{"시작","일반 전투","엘리트","증강","회복","덱 정리","이벤트","최종 보스"}[(int)k];
        static string RewardName(SingleStage s)=>s.kind==SingleNodeKind.Battle?"HP 비율 토큰 1~5 + 카드 3택1":s.kind==SingleNodeKind.Elite?"HP 비율 토큰 1~5 + 증강 3택1":s.kind==SingleNodeKind.Boss?"챕터 클리어 / 영구 토큰":s.kind==SingleNodeKind.Heal?"HP 회복":s.kind==SingleNodeKind.Purge?"카드 제거 또는 교체":s.kind==SingleNodeKind.Event?"HP와 토큰 선택":"증강 3택1";
        public static string RestrictionName(SingleRestriction r)=>new[]{"없음","턴 공격 최대 2회","최강 유닛 첫 공격 -2","직접 공격 턴당 1회","첫 소환 출전 효과 봉인","3번 슬롯 포커 보너스 봉인","스페이드 출전 효과 봉인","회복량 절반","전투 종료 회복 금지","소환 시 내 HP -1","첫 추가 드로우 1장 무효","마나 회복 -1","첫 소환 비용 +1","턴 홀짝 계급 소환 제한","매 턴 제약 변경","턴마다 1·5번 슬롯 교환"}[(int)r];
    }
    public sealed class SingleChapterHint : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler {
        public Action show,hide;
        public void OnPointerEnter(PointerEventData e)=>show?.Invoke();
        public void OnPointerExit(PointerEventData e)=>hide?.Invoke();
        void OnDisable()=>hide?.Invoke();
    }
    public sealed class SinglePathLine : MonoBehaviour {
        public Vector2 from,to;public float width=2;
        void OnEnable(){Canvas.willRenderCanvases+=Layout;}
        void OnDisable(){Canvas.willRenderCanvases-=Layout;}
        void LateUpdate()=>Layout();
        void Layout(){var r=(RectTransform)transform;var parent=(RectTransform)transform.parent;var size=parent.rect.size;var delta=Vector2.Scale(to-from,size);r.anchorMin=r.anchorMax=Vector2.zero;r.pivot=new Vector2(0,.5f);r.anchoredPosition=Vector2.Scale(from,size);r.sizeDelta=new Vector2(delta.magnitude,width);r.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);}
    }
}
