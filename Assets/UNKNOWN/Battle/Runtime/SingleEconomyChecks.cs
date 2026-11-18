#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    [InitializeOnLoad] public static class SingleEconomyChecks
    {
        static SingleEconomyChecks(){EditorApplication.playModeStateChanged+=s=>{
            if(s==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("UNKNOWN.EconomyChecks",false)){
                SessionState.SetBool("UNKNOWN.EconomyChecks",false);new GameObject("Economy checks").AddComponent<SingleEconomyCheckRunner>();
            }
        };}
        public static void Run(){SessionState.SetBool("UNKNOWN.EconomyChecks",true);EditorApplication.isPlaying=true;}
    }
    public sealed class SingleEconomyCheckRunner:MonoBehaviour
    {
        [Serializable] sealed class Report {public bool completed;public int checks;public List<string> passed=new List<string>(),errors=new List<string>();}
        readonly Report report=new Report();const string Dir="Library/SingleEconomy";
        SingleChapterUI ui;string previousSave;GameObject[] roots;
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);report.checks++;report.passed.Add(text);}
        void Error(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception)report.errors.Add(message);}
        IEnumerator Start(){
            Application.logMessageReceived+=Error;previousSave=SingleChapterUI.CheckSavePath;
            roots=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Where(g=>g!=gameObject && g.activeSelf).ToArray();foreach(var root in roots)root.SetActive(false);
            Directory.CreateDirectory(Dir);var checks=RunChecks();
            while(true){bool move=false;object next=null;try{move=checks.MoveNext();if(move)next=checks.Current;}catch(Exception ex){report.errors.Add(ex.ToString());break;}if(!move)break;yield return next;}
            Application.logMessageReceived-=Error;report.completed=report.errors.Count==0;
            File.WriteAllText(Dir+"/checks.json",JsonUtility.ToJson(report,true));SingleChapterUI.CheckSavePath=previousSave;
            if(ui!=null)Destroy(ui.gameObject);foreach(var root in roots)if(root!=null)root.SetActive(true);EditorApplication.isPlaying=false;
        }
        BattleEngine Won(SingleCampaign c,int startHP,int endHP){
            c.Run.phase="battle";c.Run.hp=startHP;c.Run.settled=false;
            var g=BattleEngine.CreateSingle(new SingleBattleOptions{stage=c.Stage,difficulty=c.Data.difficulties[0],augmentData=c.Data.augments,deck=c.Run.deck.ToArray(),augments=c.Run.augments.ToArray(),layer=c.Run.nodes[c.Run.current].layer,hp=startHP,maxHP=c.Run.maxHP,seed=123});
            g.State.Players[0].HP=endHP;g.SingleFinish(0);return g;
        }
        IEnumerator RunChecks(){
            SingleChapterUI.CheckSavePath=Dir+"/progress-"+Guid.NewGuid().ToString("N")+".json";
            ui=SingleChapterUI.Open(()=>{});yield return null;yield return null;var c=ui.Campaign;var data=c.Data;
            foreach(var test in new[]{new[]{30,30,30,10},new[]{30,21,30,7},new[]{30,15,30,5},new[]{20,25,30,10},new[]{20,11,30,7},new[]{30,-5,30,0},new[]{3,2,30,9}}){
                var reward=SingleTokenReward.Calculate(10,test[0],test[1],test[2]);Check(reward.finalTokens==test[3] && reward.deducted==10-test[3],"HP-loss floor: "+string.Join(",",test));
            }
            Check(SingleTokenReward.Calculate(10,30,21,30).LossRatio==.3f,"Loss uses battle HP delta / max HP");
            for(int seed=0;seed<32;seed++){
                c.Start(0,seed);SingleGraph.Validate(data,c.Run.nodes);
                Check(c.Run.nodes.Length==25 && c.Run.nodes.Where(n=>n.layer<9).All(n=>n.next.SequenceEqual(c.Run.nodes.Where(t=>t.layer==n.layer+1).Select(t=>t.id))),"Existing 25-node, complete next-layer graph: "+seed);
            }
            var permanent=data.permanentRewards.ToArray();int eventTokens=data.eventTokens;
            foreach(var node in c.Run.nodes){var stage=data.stages.First(s=>s.id==node.stage);
                Check(c.EntryCostFor(node.id)==(stage.IsCombat?data.economy.entryCosts[node.layer]:stage.cost),"Layer entry / original support cost: "+node.id);
                Check(c.BaseRewardFor(node.id)==(stage.IsCombat?(stage.kind==SingleNodeKind.Boss?data.economy.bossRewards:stage.kind==SingleNodeKind.Elite?data.economy.eliteRewards:data.economy.normalRewards)[node.layer]:0),"Grade/layer base reward; no noncombat reward: "+node.id);
            }
            c.Start(0,42);var first=c.Run.nodes.First(n=>n.layer==1);int before=c.Run.tokens;
            c.Enter(first.id);Check(c.Run.tokens==before-c.EntryCostFor(first.id),"Entry deducts displayed cost once");
            bool rejected=false;try{c.Enter(first.id);}catch(InvalidOperationException){rejected=true;}
            Check(rejected && c.Run.tokens==before-c.EntryCostFor(first.id),"Repeated entry cannot charge twice");
            var engine=Won(c,30,21);before=c.Run.tokens;int expected=SingleTokenReward.Calculate(c.BaseRewardFor(first.id),30,21,30).finalTokens;
            c.BattleResult(engine);Check(c.Run.tokens==before+expected && c.Run.lastRunAward==expected && c.Run.lastTokenReward.startHP==30,"Actual battle settlement uses start/end HP");
            c.BattleResult(engine);Check(c.Run.tokens==before+expected,"Repeated battle-result callback cannot grant twice");
            var restored=new SingleCampaign(data,c.SavePath);Check(restored.Run.tokens==c.Run.tokens && restored.Run.lastTokenReward.finalTokens==expected,"Reward breakdown and balance survive resume");
            ui.SendMessage("Continue");yield return null;yield return null;
            Check(ui.GetComponentsInChildren<Text>().Any(t=>t.text.Contains(c.Run.lastTokenReward.Summary)),"Existing victory UI shows base/loss/deduction/final reward");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+"/reward.png"));yield return new WaitForEndOfFrame();yield return null;yield return null;
            // Resume a battle checkpoint: run.hp remains its starting HP, not checkpoint HP.
            engine=Won(c,20,25);c.Run.phase="battle";c.Run.settled=false;c.Run.hp=20;c.Run.battle=engine.SaveSingle();c.Save();
            restored=new SingleCampaign(data,c.SavePath);before=restored.Run.tokens;restored.BattleResult(engine);
            Check(restored.Run.tokens==before+restored.BaseRewardFor(restored.Run.current) && restored.Run.lastTokenReward.lostHP==0,"Healing above battle start never increases base reward; saved battle baseline retained");
            // Exercise each battle grade, including unchanged boss permanent-token settlement.
            foreach(var kind in new[]{SingleNodeKind.Battle,SingleNodeKind.Elite,SingleNodeKind.Boss}){
                c.Start(0,42);c.Run.current=c.Run.nodes.First(n=>data.stages.First(s=>s.id==n.stage).kind==kind).id;c.Run.tokens=100;
                engine=Won(c,30,15);expected=c.BaseRewardFor(c.Run.current)/2;c.BattleResult(engine);
                Check(c.Run.lastRunAward==expected && c.Run.lastTokenReward.deducted==c.Run.lastTokenReward.baseTokens-expected,"Grade reward and 50% HP loss: "+kind);
                if(kind==SingleNodeKind.Boss)Check(c.Run.permanentAward==permanent[SingleCampaign.Tier(15,30)]+data.firstClearBonus,"Existing permanent-token clear reward unchanged");
            }
            c.Start(0,42);var shopNode=c.Run.nodes.First(n=>data.stages.First(s=>s.id==n.stage).kind==SingleNodeKind.Shop);c.Run.tokens=100;
            while(c.Run.nodes[c.Run.current].layer<shopNode.layer){int layer=c.Run.nodes[c.Run.current].layer+1;int next=layer==shopNode.layer?shopNode.id:c.Run.nodes.First(n=>n.layer==layer).id;c.Enter(next);if(next!=shopNode.id)c.CompleteNode();}
            var stock=c.CurrentShop;before=c.Run.tokens;int size=c.Run.deck.Count,price=stock.prices[0];Check(c.BuyShopCard(0) && c.Run.tokens==before-price && c.Run.deck.Count==size+1,"Shop still deducts original price and adds one card");
            restored=new SingleCampaign(data,c.SavePath);Check(restored.Run.tokens==c.Run.tokens && restored.CurrentShop.purchased[0] && !restored.BuyShopCard(0),"Purchased stock and token balance persist without duplicate purchase");
            c.LeaveShop();Check(c.Run.lastTokenReward==null && c.Run.lastRunAward==0,"Noncombat shop applies no HP-loss penalty or stale battle award");
            var nextNode=c.Run.nodes[c.Run.current].next[0];c.Run.tokens=c.EntryCostFor(nextNode)-1;Check(c.EntryError(nextNode).Length>0,"Insufficient balance blocks next combat using effective layer cost");
            c.Run.tokens=100;c.Save();typeof(SingleChapterUI).GetField("selected",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(ui,nextNode);ui.SendMessage("Map");yield return null;yield return null;
            Check(ui.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("진입 토큰: "+c.EntryCostFor(nextNode)) && t.text.Contains("기본 "+c.BaseRewardFor(nextNode)+" 토큰")),"Existing node detail shows actual entry cost and base reward");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+"/map.png"));yield return new WaitForEndOfFrame();yield return null;yield return null;
            Check(data.permanentRewards.SequenceEqual(permanent) && data.eventTokens==eventTokens,"Permanent rewards and noncombat event tokens unchanged");
        }
    }
}
#endif
