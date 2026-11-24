using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
namespace Unknown.Battle
{
    public sealed partial class SingleChapterUI : MonoBehaviour
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
            font=Unknown.Battle.PlatformFont.Create(new[]{"Malgun Gothic","Arial"},18,"Body");
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
            var start=Button("새로운 런",.33f,.47f,.34f,.075f,ChooseStartingDeck);Hint(start.gameObject,"시작 카드 "+Campaign.Data.startingDeckSize+"장을 선택합니다. 시작을 확정하면 진행 중인 런은 종료됩니다.");
            Button("이어하기",.33f,.36f,.34f,.075f,Continue,Campaign.Run!=null && Campaign.Run.phase!="done");
            Button("해금 콘텐츠",.33f,.24f,.34f,.075f,Shop);Button("뒤로 가기",.33f,.13f,.34f,.075f,Exit);
        }
        void Exit(){returnTitle?.Invoke();Destroy(gameObject);}
        void Continue() {
            switch(Campaign.Run.phase){case "map":Map();break;case "battle":LaunchBattle();break;case "prepare":Prepare();break;case "shop":RunShopScreen();break;case "done":Result();break;default:Reward();break;}
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
            var plot=Rect(viewport,"Route graph",0,0,0,1);plot.pivot=new Vector2(0,.5f);plot.sizeDelta=new Vector2(2800,0);mapScroll.content=plot;
            for(int layer=0;layer<10;layer++)Text(layer==0?"START":layer==9?"BOSS":layer.ToString("00"),.045f+layer*.10f-.035f,.945f,.07f,.04f,18,plot);
            foreach(var n in r.nodes)foreach(int target in n.next) {
                bool passed=Enumerable.Range(0,Math.Max(0,r.visited.Count-1)).Any(i=>r.visited[i]==n.id && r.visited[i+1]==target);
                bool current=n.id==r.current,related=n.id==selected || target==selected;
                bool available=Campaign.EntryError(target).Length==0;
                Color color=current?(available?new Color(.35f,.9f,.76f):new Color(.9f,.4f,.35f)):related?new Color(1,.79f,.32f):passed?new Color(.60f,.65f,.72f):new Color(.44f,.43f,.48f,selected>=0?.16f:.28f);
                if(current && target==selected)color=new Color(1,.79f,.32f);
                var line=Rect(plot,"Path "+n.id+"-"+target,0,0,1,1);
                var path=line.gameObject.AddComponent<SinglePathLine>();path.raycastTarget=false;path.color=color;
                path.from=Position(n);path.to=Position(r.nodes[target]);path.width=current?5:related?4:passed?3:2.5f;
                path.detour=r.nodes[target].layer-n.layer>1;path.rail=.035f+(n.id%3)*.012f;
            }
            foreach(var path in plot.GetComponentsInChildren<SinglePathLine>().OrderBy(p=>p.width))path.transform.SetAsLastSibling();
            var nodeMaps=Resources.Load<BattleMapSettings>("BattleMapSettings");
            foreach(var n in r.nodes) {
                var stage=Campaign.Data.stages.First(s=>s.id==n.stage);var pos=Position(n);int id=n.id;var state=Campaign.NodeState(id);
                string reward=stage.IsCombat?Campaign.BaseRewardFor(id)+" 토큰":stage.kind==SingleNodeKind.Start?"없음":stage.kind==SingleNodeKind.Shop?"카드 구매":RewardName(stage);
                var button=Button((stage.kind==SingleNodeKind.Start?"시작":Kind(stage.kind))+"\n진입 토큰: "+Campaign.EntryCostFor(id)+"\n보상: "+reward,pos.x-.035f,pos.y-.105f,.07f,.21f,()=>{selected=id;Map();},true,plot);
                button.name="Node "+id;button.GetComponent<Image>().color=NodeColor(state);
                var edge=button.GetComponent<Outline>();edge.effectDistance=Vector2.one*(selected==id?3:1);edge.effectColor=selected==id?new Color(1,.8f,.32f):new Color(.48f,.35f,.2f);
                // Special nodes use dedicated artwork; other nodes keep their map card back.
                var back=nodeMaps==null?null:nodeMaps.NodeCardBackFor(stage);
                if(back!=null) {
                    button.GetComponentInChildren<Text>().gameObject.SetActive(false);
                    button.GetComponent<Image>().color=Color.clear;edge.enabled=false;
                    var iconArea=Rect(button.transform,"Node icon area",0,.18f,1,.82f);
                    var frame=Rect(iconArea,"Node card frame",0,0,1,1,NodeColor(state));
                    var frameImage=frame.GetComponent<Image>();frameImage.raycastTarget=false;
                    var fit=frame.gameObject.AddComponent<AspectRatioFitter>();fit.aspectRatio=(float)back.width/back.height;fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;
                    var border=frame.gameObject.AddComponent<Outline>();border.effectDistance=Vector2.one*(selected==id?3:2);
                    border.effectColor=selected==id?new Color(1,.8f,.32f):NodeColor(state);
                    Artwork(frame,"Map card back",back,.035f,.025f,.93f,.95f);
                    button.targetGraphic=frameImage;
                    Text(Kind(stage.kind),.02f,0,.96f,.15f,18,button.transform);
                }
            }
            var track=Rect(root,"Route scrollbar",.04f,.145f,.65f,.014f,new Color(.11f,.08f,.09f));
            var handle=Rect(track,"Handle",0,0,.3f,1,new Color(.58f,.4f,.19f));var scrollbar=track.gameObject.AddComponent<Scrollbar>();scrollbar.handleRect=handle;scrollbar.targetGraphic=handle.GetComponent<Image>();scrollbar.direction=Scrollbar.Direction.LeftToRight;mapScroll.horizontalScrollbar=scrollbar;
            MapDetail();
            Button("메인 메뉴",.04f,.055f,.18f,.06f,()=>{Campaign.Save();Home();});Button("현재 덱",.25f,.055f,.18f,.06f,Deck);Button("증강",.46f,.055f,.18f,.06f,Augments);
            Canvas.ForceUpdateCanvases();mapScroll.horizontalNormalizedPosition=mapOffset<0?Mathf.Clamp01((r.nodes[r.current].layer-1)/6f):mapOffset;
            ShowStartGuide();
        }
        // These pages only introduce the route; battle objectives use actual engine events.
        void StartGuide(int page) {
            if(Campaign.Run==null || Campaign.Run.current!=0 || Campaign.Run.phase!="map")return;
            Campaign.Run.startGuide=Mathf.Clamp(page,-1,7);
            if(page<0)Campaign.Run.battleGuide=-1;
            Campaign.Save();Map();
        }
        void BeginLiveGuide() {
            var run=Campaign.Run;if(run.current!=0 || run.phase!="map")return;
            run.startGuide=7;run.battleGuide=1;run.battleGuideTurn=0;Campaign.Save();selected=-1;Map();
        }
        void ShowStartGuide() {
            var run=Campaign.Run;
            if(run.current!=0 || run.phase!="map" || run.startGuide<0 || run.startGuide>=7)return;
            int page=Mathf.Clamp(run.startGuide,0,3);
            // No filled highlight or Outline: the route and its labels stay visible.
            var overlay=Rect(root,"Start tutorial",0,0,1,1,Color.clear);
            var panel=Rect(overlay,"Tutorial panel",.72f,.145f,.25f,.68f,new Color(.035f,.018f,.03f,1f));
            Text("01 · 시작",.05f,.88f,.90f,.07f,28,panel).color=new Color(1,.80f,.40f);
            string[] titles={"싱글플레이 안내","경로 선택","HP와 토큰","덱과 실전 전투"};
            Text(titles[page],.06f,.77f,.88f,.08f,23,panel);
            string body;
            switch(page) {
                case 0:body="경로와 자원을 살펴본 뒤 실제 첫 전투에서 직접 배웁니다.\n\n카드 소환 → 턴 종료 → 적 행동 → 공격 → 승리와 보상\n\n안내는 언제든 스킵할 수 있습니다.\n\n안내 선택에는 토큰이 들지 않으며, 실제 전투 진입과 결과는 기존 런 규칙을 따릅니다.";break;
                case 1:body="현재 노드에서 밝게 연결된 다음 구역을 선택하세요.\n같은 다음 층의 모든 구역으로 이동할 수 있습니다.\n\n노드를 누르면 오른쪽에 정보가 표시됩니다.\n아래 스크롤바로 먼 경로도 볼 수 있습니다.\n\n선택만으로 이동하지 않으며, 진입을 눌러야 토큰을 지불합니다.";break;
                case 2:body=$"현재 HP {run.hp}/{run.maxHP} · 런 토큰 {run.tokens}\n\nHP는 다음 전투로 이어집니다.\n런 토큰은 스테이지 진입에 사용합니다.\n\n전투 등급·층별 기본 보상에서 해당 전투의 HP 손실 비율만큼 토큰이 감소합니다.\nHP가 0이 되거나 다음 경로의 진입 토큰이 부족하면 런이 종료됩니다.\n\n영구 토큰은 챕터 클리어로 얻으며 해금에 사용합니다.";break;
                default:body=$"현재 덱 {run.deck.Count}장\n\n현재 덱 버튼으로 카드와 효과를 확인할 수 있습니다.\n전투 보상으로 카드를 추가하고 덱 정리 노드에서 제거·교체합니다.\n증강은 런 동안 유지되며 최대 3개를 장착합니다.\n\n이제 실전 전투로 배우기를 누르고 다음 전투 노드를 선택해 진입하세요.\n전투에서 소환·턴·공격을 직접 하면 안내가 진행됩니다.";break;
            }
            Text(body,.07f,.20f,.86f,.55f,20,panel);
            if(page==0) {
                Button("안내 시작",.08f,.105f,.84f,.07f,()=>StartGuide(1),true,panel);
                Button("스킵하고 출발",.08f,.025f,.84f,.065f,()=>StartGuide(-1),true,panel);
            } else {
                Text(page+" / 3",.35f,.12f,.30f,.05f,17,panel);
                Button("이전",.06f,.04f,.23f,.065f,()=>StartGuide(page-1),true,panel);
                Button("스킵",.32f,.04f,.23f,.065f,()=>StartGuide(-1),true,panel);
                Button(page==3?"실전으로":"다음",.58f,.04f,.36f,.065f,()=>{if(page==3)BeginLiveGuide();else StartGuide(page+1);},true,panel);
            }
        }
        void MapDetail() {
            var panel=Rect(root,"Stage detail",.72f,.145f,.25f,.68f,new Color(.035f,.018f,.03f,.96f));
            if(selected<0 || selected>=Campaign.Run.nodes.Length){Text(Campaign.Run.battleGuide==1 && Campaign.Run.current==0?"실전 안내 준비 완료\n다음 전투 노드를 선택하세요.": "스테이지 선택",.05f,.44f,.9f,.1f,24,panel);return;}
            var stage=Campaign.Data.stages.First(s=>s.id==Campaign.Run.nodes[selected].stage);
            if(stage.kind==SingleNodeKind.Start) {
                Text("01 · 시작",.06f,.72f,.88f,.10f,28,panel);
                Text("싱글플레이 튜토리얼\n\n경로·토큰·전투·성장 흐름을 안내합니다.\n원하면 스킵하고 바로 출발할 수 있습니다.",.07f,.32f,.86f,.35f,21,panel);
                Button("튜토리얼 다시 보기",.08f,.12f,.84f,.08f,()=>StartGuide(0),Campaign.Run.current==0,panel);
                return;
            }
            var maps=Resources.Load<BattleMapSettings>("BattleMapSettings");
            if(maps!=null && stage.IsCombat)Artwork(panel,"Stage map artwork",maps.BackgroundFor(stage.map),.04f,.73f,.92f,.23f);
            string details="난이도: "+Campaign.Data.difficulties[Campaign.Run.difficulty].name
                +"\n적 HP: "+(stage.IsCombat?stage.enemyHP.ToString():"—")
                +"\n제한 턴: "+(stage.IsCombat?EffectiveTurns(stage,selected).ToString():"—")
                +"\n마나 예산: "+(stage.IsCombat?Campaign.ManaBudgetFor(stage).ToString():"—")
                +"\n진입 토큰: "+Campaign.EntryCostFor(selected)
                +"\n방해효과: "+Restrictions(stage)
                +"\n보상: "+RewardForNode(stage,selected);
            if(stage.kind==SingleNodeKind.Event || stage.kind==SingleNodeKind.Heal || stage.kind==SingleNodeKind.Augment || stage.kind==SingleNodeKind.Shop || stage.kind==SingleNodeKind.Purge)details=RewardName(stage);
            Text(details,.06f,.24f,.88f,stage.IsCombat?.48f:.60f,22,panel);
            string reason=Campaign.EntryError(selected);
            if(reason.Length>0)Text(reason,.06f,.115f,.88f,.065f,18,panel).color=new Color(1,.64f,.57f);
            Button("진입",.10f,.035f,.80f,.075f,()=>{Campaign.Enter(selected);Prepare();},reason.Length==0,panel);
        }
        Vector2 Position(SingleNode n) {
            int count=Campaign.Run.nodes.Count(x=>x.layer==n.layer);
            return new Vector2(.045f+n.layer*.10f,count==1?.5f:count==2?.68f-n.lane*.36f:.82f-n.lane*.32f);
        }
        bool Combat(SingleStage stage)=>stage.kind==SingleNodeKind.Battle || stage.kind==SingleNodeKind.Elite || stage.kind==SingleNodeKind.Boss;
        int EffectiveTurns(SingleStage stage,int node)=>Campaign.TurnLimitFor(stage,node);
        string Restrictions(SingleStage stage)=>string.Join(" / ",stage.restrictions.Concat(new[]{SingleRestriction.FirstCost,SingleRestriction.AttackLimit,SingleRestriction.HalfHealing}.Take(Combat(stage)?Campaign.Data.difficulties[Campaign.Run.difficulty].extraRestrictions:0)).Distinct().Select(RestrictionName).DefaultIfEmpty("없음"));
        static string StateName(SingleNodeState state)=>new[]{"현재 위치","이동 가능","토큰 부족","미도달","완료","이전 분기"}[(int)state];
        static Color NodeColor(SingleNodeState state)=>new[]{new Color(.42f,.29f,.10f),new Color(.08f,.29f,.25f),new Color(.36f,.09f,.11f),new Color(.08f,.065f,.09f),new Color(.18f,.22f,.27f),new Color(.07f,.06f,.065f)}[(int)state];
        static string BattleTypeName(SingleStage s)=>s.IsCombat?new[]{"일반전","속공전","방어 돌파전","마나 제한전","카드 제한전","엘리트전","보스전"}[(int)s.battleType]:Kind(s.kind);
        static string ShortKind(SingleNodeKind kind)=>new[]{"시작","일반 전투","엘리트","증강","회복","덱 정리","이벤트","BOSS","▣ 상점"}[(int)kind];
        void Augments() {
            Clear("증강");var r=Campaign.Run;
            if(r.augments.Count==0)Text("장착 증강 없음",.15f,.42f,.7f,.12f,24);
            for(int i=0;i<r.augments.Count;i++){var aug=Campaign.Data.augments.First(a=>a.id==r.augments[i]);Text(aug.name+"\n"+aug.description,.14f,.65f-i*.17f,.72f,.14f,22);}
            Button("돌아가기",.35f,.08f,.3f,.065f,Continue);
        }
        void Prepare() {
            var s=Campaign.Stage;if(s.kind==SingleNodeKind.Shop){if(Campaign.Run.phase=="prepare")Campaign.ResolveSupport();RunShopScreen();return;}Clear(s.name);Header();
            bool combat=s.kind==SingleNodeKind.Battle || s.kind==SingleNodeKind.Elite || s.kind==SingleNodeKind.Boss;
            if(combat) {
                var d=Campaign.Data.difficulties[Campaign.Run.difficulty];
                Text($"{BattleTypeName(s)} · {s.enemyType} · 적 HP {s.enemyHP} · ◷ 제한 턴 {Campaign.CurrentTurnLimit}\n총 마나 예산 {Campaign.CurrentManaBudget} · 매 턴 소환 {s.enemySummonsPerTurn}장"+(s.kind==SingleNodeKind.Battle?"":" · 조건 충족 시 최대 2장")+"\n\n"+string.Join(" / ",s.restrictions.Concat(new[]{SingleRestriction.FirstCost,SingleRestriction.AttackLimit,SingleRestriction.HalfHealing}.Take(d.extraRestrictions)).Distinct().Select(RestrictionName))+"\n\n"+RewardForNode(s,Campaign.Run.current),.15f,.36f,.7f,.35f,24);
                var info=Button("전투 상세",.37f,.27f,.26f,.05f,()=>{});
                Hint(info.gameObject,$"시작 마나 {s.startMana} · 회복 {s.manaRecovery} · 턴 상한 {s.turnSpendLimit}\n총 마나 예산 {Campaign.CurrentManaBudget}\n초기 적: "+string.Join(" / ",s.units.Select(u=>CardDatabase.Get(u.card).CardName))+(Campaign.IsTeachingBattle?"\n첫 전투 손패에 A·J 보장":""));
                if(Campaign.Run.battleGuide>0 && Campaign.Run.battleGuide<5 && Campaign.IsTeachingBattle)Text("실전 안내 · 직접 소환하고 공격하며 배웁니다. 언제든 스킵할 수 있습니다.",.12f,.73f,.76f,.05f,20);
                Button("전투 시작",.34f,.16f,.32f,.08f,LaunchBattle);
            }else {
                Text(Kind(s.kind)+"\n"+RewardName(s)+$"\n진입 토큰 {Campaign.EntryCostFor(Campaign.Run.current)} 차감 완료",.15f,.34f,.7f,.3f,26);
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
                if(r.battleGuide==5 && Campaign.IsTeachingBattle)Text("실전 안내 완료 · 보상을 선택하거나 건너뛰세요.",.1f,.82f,.8f,.04f,20);
                Text("남은 HP "+r.hp+"/"+r.maxHP+" · 런 토큰 "+r.tokens+(Campaign.Stage.IsCombat && r.lastTokenReward!=null?"\n"+r.lastTokenReward.Summary:""),.1f,.685f,.8f,.12f,21);
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
            var r=Campaign.Run;Clear(r.failure.Length==0?"CHAPTER CLEAR":r.failure);Text($"{Campaign.Data.difficulties[r.difficulty].name}\n남은 HP {r.hp}/{r.maxHP}\n획득 영구 토큰 {r.permanentAward}\n최초 클리어 보너스 {r.firstClearAward}"+(r.lastTokenReward!=null?"\n\n마지막 전투\n"+r.lastTokenReward.Summary:""),.18f,.32f,.64f,.47f,23);
            Button("새 런 / 난이도 선택",.16f,.2f,.3f,.08f,Home);Button("해금 가능한 콘텐츠",.54f,.2f,.3f,.08f,Shop);Button("최종 덱 보기",.35f,.09f,.3f,.07f,Deck);
        }
        RawImage Artwork(Transform parent,string name,Texture2D texture,float x,float y,float w,float h,bool cover=false) {
            if(texture==null)return null;var frame=Rect(parent,name,x,y,w,h);if(cover)frame.gameObject.AddComponent<RectMask2D>();var rect=Rect(frame,"Texture",0,0,1,1);var image=rect.gameObject.AddComponent<RawImage>();image.texture=texture;image.raycastTarget=false;
            var fit=rect.gameObject.AddComponent<AspectRatioFitter>();fit.aspectRatio=(float)texture.width/texture.height;fit.aspectMode=cover?AspectRatioFitter.AspectMode.EnvelopeParent:AspectRatioFitter.AspectMode.FitInParent;return image;
        }
        RectTransform CardPreview(Transform parent,string id,float x,float y,float w,float h,Action click,bool enabled,bool compact=true) {
            var def=CardDatabase.Get(id);var rect=Rect(parent,"Card "+id,x,y,w,h,Color.clear);rect.GetComponent<Image>().raycastTarget=true;
            var button=rect.gameObject.AddComponent<Button>();button.interactable=enabled;button.onClick.AddListener(()=>{try{click();}catch(Exception ex){error=ex.Message;Continue();}});
            var texture=CardArtSettings.TextureFor(id);if(texture==null){Text(def.CardName,0,0,1,1,22,rect);return rect;}
            var art=Rect(rect,"Card artwork",0,0,1,1).gameObject.AddComponent<RawImage>();art.texture=texture;art.raycastTarget=false;
            var mana=Text(def.ManaCost.ToString(),0,0,1,1,18,rect);
            var descriptionArea=compact ? Rect(rect,"Description viewport",0,0,1,1) : null;
            if(descriptionArea!=null)descriptionArea.gameObject.AddComponent<RectMask2D>();
            var desc=Text(compact ? CardDisplayText.For(new CardInstance(-1,def,0)) : def.Description,0,0,1,1,10,descriptionArea!=null ? descriptionArea : rect);
            if(compact)desc.gameObject.AddComponent<CardDescriptionFit>().SetMaximum(11);
            var attack=Text(def.CardType==CardType.Joker?"50%":def.Attack.ToString(),0,0,1,1,18,rect);var health=Text(def.MaxHP.ToString(),0,0,1,1,18,rect);health.gameObject.SetActive(def.CardType!=CardType.Joker);
            foreach(var text in new[]{mana,desc,attack,health}){text.color=new Color(.98f,.92f,.82f);var shadow=text.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.85f);shadow.effectDistance=new Vector2(1,-1);}
            foreach(var text in new[]{mana,attack,health})text.gameObject.AddComponent<CardBadgeCenter>();
            var layout=rect.gameObject.AddComponent<CardArtLayout>();layout.artwork=art;layout.mana=mana.rectTransform;layout.description=descriptionArea!=null ? descriptionArea : desc.rectTransform;layout.attack=attack.rectTransform;layout.health=health.rectTransform;layout.joker=def.CardType==CardType.Joker;layout.RefreshLayout();
            rect.gameObject.AddComponent<CardHover>().Configure(null,null,true,parent.GetComponent<LayoutGroup>()==null);
            return rect;
        }
        static string NodeIcon(SingleNodeKind k)=>new[]{"●","⚔","◆","✦","+","−","?","♛","▣"}[(int)k];
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
        void OnDestroy(){if(battle!=null)Destroy(battle.gameObject);if(font!=null)Unknown.Battle.PlatformFont.Release(font);}
        public static string Kind(SingleNodeKind k)=>new[]{"시작","일반 전투","엘리트","증강","회복","덱 정리","이벤트","최종 보스","상점"}[(int)k];
        string RewardForNode(SingleStage stage,int node)=>(stage.IsCombat?"기본 "+Campaign.BaseRewardFor(node)+" 토큰 · ":"")+RewardName(stage);
        static string RewardName(SingleStage s)=>s.kind==SingleNodeKind.Shop?"런 토큰으로 카드 구매":s.kind==SingleNodeKind.Battle?"카드 3택1":s.kind==SingleNodeKind.Elite?"증강 3택1":s.kind==SingleNodeKind.Boss?"챕터 클리어 / 영구 토큰":s.kind==SingleNodeKind.Heal?"HP 회복":s.kind==SingleNodeKind.Purge?"카드 제거 또는 교체":s.kind==SingleNodeKind.Event?"HP와 토큰 선택":"증강 3택1";
        public static string RestrictionName(SingleRestriction r)=>new[]{"없음","턴 공격 최대 2회","최강 유닛 첫 공격 -2","직접 공격 턴당 1회","첫 소환 출전 효과 봉인","3번 슬롯 포커 보너스 봉인","스페이드 출전 효과 봉인","회복량 절반","전투 종료 회복 금지","소환 시 내 HP -1","첫 추가 드로우 1장 무효","마나 회복 -1","첫 소환 비용 +1","턴 홀짝 계급 소환 제한","매 턴 제약 변경","턴마다 1·5번 슬롯 교환"}[(int)r];
    }
    public sealed class SingleChapterHint : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler {
        public Action show,hide;
        public void OnPointerEnter(PointerEventData e)=>show?.Invoke();
        public void OnPointerExit(PointerEventData e)=>hide?.Invoke();
        void OnDisable()=>hide?.Invoke();
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SinglePathLine : MaskableGraphic {
        public Vector2 from,to;public float width=3,rail=.035f;public bool detour;
        protected override void OnPopulateMesh(VertexHelper mesh) {
            mesh.Clear();var rect=rectTransform.rect;
            Vector2 Point(Vector2 p)=>rect.min+Vector2.Scale(p,rect.size);
            var a=Point(from+new Vector2(.035f,0));var b=Point(to-new Vector2(.035f,0));
            float bend=Mathf.Min(70,(b.x-a.x)*.45f);
            if(detour) {
                float y=rect.yMin+rail*rect.height;
                var c=new Vector2(a.x+bend,y);var d=new Vector2(b.x-bend,y);
                Curve(mesh,a,a+Vector2.right*bend,c+Vector2.up*40,c);
                Segment(mesh,c,d);Curve(mesh,d,d+Vector2.up*40,b-Vector2.right*bend,b);
            } else Curve(mesh,a,a+Vector2.right*bend,b-Vector2.right*bend,b);
            int start=mesh.currentVertCount;
            mesh.AddVert(b,color,Vector2.zero);mesh.AddVert(b+new Vector2(-13,7),color,Vector2.zero);mesh.AddVert(b+new Vector2(-13,-7),color,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);
        }
        void Curve(VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Vector2 d) {
            var previous=a;
            for(int i=1;i<=16;i++) {float t=i/16f,u=1-t;var point=u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;Segment(mesh,previous,point);previous=point;}
        }
        void Segment(VertexHelper mesh,Vector2 a,Vector2 b) {
            var delta=b-a;if(delta.sqrMagnitude<.001f)return;
            var normal=new Vector2(-delta.y,delta.x).normalized*width*.5f;int start=mesh.currentVertCount;
            mesh.AddVert(a-normal,color,Vector2.zero);mesh.AddVert(a+normal,color,Vector2.zero);mesh.AddVert(b+normal,color,Vector2.zero);mesh.AddVert(b-normal,color,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
        }
    }

}
