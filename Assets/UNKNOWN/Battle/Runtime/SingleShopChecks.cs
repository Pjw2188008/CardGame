#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    [InitializeOnLoad] public static class SingleShopChecks
    {
        static SingleShopChecks(){EditorApplication.playModeStateChanged+=s=>{
            if(s==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("UNKNOWN.ShopChecks",false)){
                SessionState.SetBool("UNKNOWN.ShopChecks",false);new GameObject("Shop checks").AddComponent<SingleShopCheckRunner>();
            }
        };}
        public static void Run(){SessionState.SetBool("UNKNOWN.ShopChecks",true);EditorApplication.isPlaying=true;}
    }
    public sealed class SingleShopCheckRunner:MonoBehaviour
    {
        [Serializable] sealed class Report {public bool completed;public int checks;public List<string> passed=new List<string>(),errors=new List<string>();}
        readonly Report report=new Report();const string Dir="Library/SingleShop";
        SingleChapterUI ui;string previousSave;GameObject[] roots;
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);report.checks++;report.passed.Add(text);}
        void Error(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception)report.errors.Add(message);}
        Button Button(string name)=>ui.GetComponentsInChildren<Button>().First(b=>b.name==name);
        void Click(string name){var b=Button(name);Check(b.interactable,"Button enabled: "+name);b.onClick.Invoke();}
        IEnumerator Start(){
            Application.logMessageReceived+=Error;previousSave=SingleChapterUI.CheckSavePath;
            roots=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Where(g=>g!=gameObject && g.activeSelf).ToArray();foreach(var root in roots)root.SetActive(false);
            Directory.CreateDirectory(Dir);var checks=RunChecks();
            while(true){bool move=false;object next=null;try{move=checks.MoveNext();if(move)next=checks.Current;}catch(Exception ex){report.errors.Add(ex.ToString());break;}if(!move)break;yield return next;}
            Application.logMessageReceived-=Error;report.completed=report.errors.Count==0;
            File.WriteAllText(Dir+"/checks.json",JsonUtility.ToJson(report,true));SingleChapterUI.CheckSavePath=previousSave;
            if(ui!=null)Destroy(ui.gameObject);foreach(var root in roots)if(root!=null)root.SetActive(true);
            EditorApplication.isPlaying=false;
        }
        IEnumerator RunChecks(){
            SingleChapterUI.CheckSavePath=Dir+"/progress-"+Guid.NewGuid().ToString("N")+".json";
            ui=SingleChapterUI.Open(()=>{});yield return null;yield return null;
            var c=ui.Campaign;var data=c.Data;
            ButtonTextLayout.VerifyVisible(ui.gameObject,Check,"single home");
            ui.SendMessage("Shop");yield return null;yield return null;
            ButtonTextLayout.VerifyVisible(ui.gameObject,Check,"unlock list");
            ui.SendMessage("ChooseStartingDeck");yield return null;yield return null;
            for(int pick=0;pick<8;pick++){
                var card=ui.GetComponentsInChildren<CardArtLayout>().First(l=>!l.name.StartsWith("✓ "));
                card.GetComponent<Button>().onClick.Invoke();yield return null;
            }
            yield return null;ButtonTextLayout.VerifyVisible(ui.gameObject,Check,"eight selected starting cards");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+"/buttons-starting-deck.png"));yield return new WaitForEndOfFrame();yield return null;
            Click("콘셉트 자동 선택");yield return null;yield return null;
            ButtonTextLayout.VerifyVisible(ui.gameObject,Check,"automatic deck concepts");
            ui.Home();yield return null;

            var shopCounts=new HashSet<int>();
            for(int seed=0;seed<24;seed++){
                c.Start(0,seed);var nodes=c.Run.nodes;SingleGraph.Validate(data,nodes);
                int count=nodes.Count(n=>data.stages.First(s=>s.id==n.stage).kind==SingleNodeKind.Shop);shopCounts.Add(count);
                Check(nodes.Length==25 && count>=1 && count<=2,"25 nodes and one/two shops: seed "+seed);
                Check(nodes.Where(n=>n.layer<9).All(n=>n.next.SequenceEqual(nodes.Where(t=>t.layer==n.layer+1).Select(t=>t.id))),"Original complete next-layer edges: seed "+seed);
            }
            Check(shopCounts.Count==2,"Both shop count settings appear across seeds");
            foreach(var d in CardDatabase.All.Where(d=>d.CardType!=CardType.Unknown)){
                int price=d.CardType==CardType.Joker?8:d.Rank==Rank.Ace?7:d.Rank>=Rank.Jack && d.Rank<=Rank.King?5:(int)d.Rank<=6?2:3;
                Check(data.shop.Price(d)==price,"Configured price: "+d.CardID);
            }
            var draft=c.CardPool().Take(data.startingDeckSize).ToArray();c.StartSelected(0,draft,42);
            Check(c.Run.deck.SequenceEqual(draft),"Starting-card selection is not overwritten");
            // A legacy save has the original 25 template IDs and no shop-state field.
            var unused=data.stages.Where(s=>!s.IsCombat && s.kind!=SingleNodeKind.Start && s.kind!=SingleNodeKind.Shop && !c.Run.nodes.Any(n=>n.stage==s.id)).ToList();
            foreach(var n in c.Run.nodes.Where(n=>data.stages.First(s=>s.id==n.stage).kind==SingleNodeKind.Shop)){n.stage=unused[0].id;unused.RemoveAt(0);}
            c.Run.shops=null;c.Save();string raw=File.ReadAllText(c.SavePath);raw=Regex.Replace(raw,"\\s*\"shops\":\\s*null,?","");File.WriteAllText(c.SavePath,raw);
            var legacy=new SingleCampaign(data,c.SavePath);Check(legacy.Run.deck.SequenceEqual(draft) && legacy.Run.nodes.Length==25,"Legacy no-shop save remains loadable");
            c.StartSelected(0,draft,42);c.Run.tokens=100;c.Save();
            var shopNode=c.Run.nodes.First(n=>data.stages.First(s=>s.id==n.stage).kind==SingleNodeKind.Shop);
            while(c.Run.nodes[c.Run.current].layer<shopNode.layer){
                int layer=c.Run.nodes[c.Run.current].layer+1;
                int next=layer==shopNode.layer?shopNode.id:c.Run.nodes.First(n=>n.layer==layer).id;
                c.Enter(next);if(next!=shopNode.id)c.CompleteNode();
            }
            Check(c.Run.phase=="shop" && c.Run.battle==null,"Node enters shop without combat");
            var stock=c.CurrentShop;Check(stock.cards.Length==4 && stock.cards.Distinct().Count()==4 && stock.cards.All(c.CardPool().Contains),"Four distinct unlocked sale cards");
            var savedCards=stock.cards.ToArray();var prices=stock.prices.ToArray();
            var reloaded=new SingleCampaign(data,c.SavePath);Check(reloaded.CurrentShop.cards.SequenceEqual(savedCards) && reloaded.CurrentShop.prices.SequenceEqual(prices),"Entering stock and prices are persisted");
            ui.Home();Click("이어하기");yield return null;yield return null;
            Check(ui.GetComponentsInChildren<CardArtLayout>().Length==4 && ui.GetComponentsInChildren<CardArtLayout>().All(l=>l.artwork.texture!=null),"Existing shop UI displays four card previews");
            Check(ui.GetComponentsInChildren<Button>().Count(b=>b.name=="구매" && b.interactable)==4,"Four purchase buttons available");
            ButtonTextLayout.VerifyVisible(ui.gameObject,Check,"card shop");
            var visuals=Resources.Load<Unknown.Multiplayer.RoomVisualSettings>("RoomVisualSettings");
            var purchase=Button("구매");var purchaseImage=purchase.targetGraphic as Image;
            Check(purchase is SingleButtonSkin && purchaseImage.sprite==visuals.singleButtonNormal && purchaseImage.type==Image.Type.Sliced,"Single button uses the supplied normal artwork with preserved corners");
            var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
            purchase.OnPointerEnter(pointer);purchase.OnPointerDown(pointer);
            Check(purchaseImage.overrideSprite==visuals.singleButtonPressed,"Native pointer press switches to the supplied pressed artwork");
            Canvas.ForceUpdateCanvases();yield return null;yield return null;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+"/button-pressed.png"));yield return new WaitForEndOfFrame();yield return null;
            purchase.OnPointerUp(pointer);
            Check(purchaseImage.overrideSprite==visuals.singleButtonNormal,"Pointer release restores normal artwork");
            purchase.interactable=false;purchase.OnPointerDown(pointer);
            Check(purchaseImage.overrideSprite==visuals.singleButtonNormal,"Disabled button cannot display an active pressed state");
            purchase.interactable=true;purchase.OnPointerExit(pointer);
            Check(ui.GetComponentsInChildren<CardArtLayout>().All(l=>l.GetComponent<SingleButtonSkin>()==null),"Card previews keep their existing artwork and click targets");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+"/shop.png"));yield return new WaitForEndOfFrame();yield return null;yield return null;
            int tokens=c.Run.tokens,deck=c.Run.deck.Count,duplicates=c.Run.deck.Count(id=>id==savedCards[0]);
            Click("구매");yield return null;
            Check(c.Run.tokens==tokens-prices[0] && c.Run.deck.Count==deck+1 && c.Run.deck.Count(id=>id==savedCards[0])==duplicates+1 && stock.purchased[0],"Purchase deducts price and adds exactly one existing card ID");
            Check(ui.GetComponentsInChildren<Button>().Any(b=>b.name=="구매 완료" && !b.interactable),"Purchased product is visibly disabled");
            Check(ui.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("런 토큰 "+c.Run.tokens) && t.text.Contains("덱 "+c.Run.deck.Count+"장")),"UI token and deck totals update immediately");
            tokens=c.Run.tokens;deck=c.Run.deck.Count;
            Check(!c.BuyShopCard(0) && c.Run.tokens==tokens && c.Run.deck.Count==deck,"Double purchase rejected without token/card changes");
            reloaded=new SingleCampaign(data,c.SavePath);
            Check(reloaded.CurrentShop.purchased[0] && !reloaded.BuyShopCard(0) && reloaded.Run.deck.Count==deck && reloaded.Run.tokens==tokens,"Save/reload cannot restore purchased stock");
            // Exercise a real filesystem failure in the isolated test save only.
            bool failed=false;
            using(var locked=new FileStream(c.SavePath,FileMode.Open,FileAccess.Read,FileShare.None)){
                try{c.BuyShopCard(1);}catch(IOException){failed=true;}
            }
            Check(failed && c.Run.tokens==tokens && c.Run.deck.Count==deck && !stock.purchased[1],"Failed save rolls back tokens, deck and product state");
            Check(c.BuyShopCard(1) && stock.purchased[1],"Retry after failed save grants only one card");
            c.Run.tokens=0;c.Save();ui.Home();Click("이어하기");yield return null;
            Check(ui.GetComponentsInChildren<Button>().Count(b=>b.name=="토큰 부족" && !b.interactable)==2 && !c.BuyShopCard(2),"Insufficient tokens disable purchase and reject direct call");
            c.Run.tokens=100;c.Save();ui.Home();Click("이어하기");yield return null;
            var deckAfter=c.Run.deck.ToArray();Click("상점 나가기");yield return null;
            Check(c.Run.phase=="map" && c.Run.deck.SequenceEqual(deckAfter) && c.Run.shops[0].purchased[0],"Leaving shop returns to normal route with deck and stock retained");
            int nextNode=c.Run.nodes[c.Run.current].next[0];c.Enter(nextNode);
            reloaded=new SingleCampaign(data,c.SavePath);
            Check(reloaded.Run.deck.SequenceEqual(deckAfter) && reloaded.Run.shops[0].purchased[0] && !reloaded.BuyShopCard(0),"Next node and resume preserve purchases and reject out-of-shop buying");
            foreach(SingleDeckConcept theme in Enum.GetValues(typeof(SingleDeckConcept)))Check(SingleStartingDeck.Build(c.CardPool(),data.startingDeckSize,theme).Count==8,"Existing automatic starting deck: "+theme);
            // Reuse existing screens with isolated campaign data; do not claim rewards or change a live save.
            string phase=c.Run.phase;var offers=c.Run.offers.ToArray();
            c.Run.phase="card";c.Run.offers=c.CardPool().Take(3).ToList();ui.SendMessage("Continue");yield return null;yield return null;
            ButtonTextLayout.VerifyVisible(ui.gameObject,Check,"card reward");
            c.Run.phase="augment";c.Run.offers=data.augments.Take(3).Select(a=>a.id).ToList();ui.SendMessage("Continue");yield return null;yield return null;
            ButtonTextLayout.VerifyVisible(ui.gameObject,Check,"augment choices");
            c.Run.phase="event";ui.SendMessage("Continue");yield return null;yield return null;
            ButtonTextLayout.VerifyVisible(ui.gameObject,Check,"event choices");
            c.Run.phase=phase;c.Run.offers=offers.ToList();
            c.Start(0,42);ui.SendMessage("Map");yield return null;yield return null;
            ButtonTextLayout.VerifyVisible(ui.gameObject,Check,"start guide invitation");
            Click("안내 시작");yield return null;yield return null;
            ButtonTextLayout.VerifyVisible(ui.gameObject,Check,"guide navigation");
            Click("스킵");yield return null;c.Enter(c.Run.nodes[0].next[0]);ui.SendMessage("Continue");yield return null;yield return null;
            ButtonTextLayout.VerifyVisible(ui.gameObject,Check,"stage preparation");
            var preferences=GamePreferences.Instance;preferences.Open();yield return null;yield return null;
            ButtonTextLayout.VerifyVisible(preferences.gameObject,Check,"settings");preferences.Close();
            ui.gameObject.SetActive(false);
            var battleCheckObject=new GameObject("Existing battle map text checks");var battleCheck=battleCheckObject.AddComponent<BattleUIController>();
            yield return null;yield return null;
            var mapChecks=battleCheck.VerifyMapTextPresentation(Check,Dir,c);while(mapChecks.MoveNext())yield return mapChecks.Current;
            Destroy(battleCheckObject);ui.gameObject.SetActive(true);

        }
    }
}
#endif
