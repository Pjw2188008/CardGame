using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    // Hand discard controls and the battle menu share the main controller's state.
    public sealed partial class BattleUIController
    {
        bool discardMode;
        int discardID=-1,discardTurn=-1;
        Button discardButton;
        Text handCountLabel;
        RectTransform[] graveMarkers=new RectTransform[2];
        RectTransform battleMenu;
        bool MenuOpen => battleMenu!=null && battleMenu.gameObject.activeSelf;
        void BuildHandTools()
        {
            discardButton=ActionButton(battlefield,"카드 버리기",new Vector2(0.90f,0.055f),new Vector2(0.99f,0.092f),ToggleDiscard);
            handCountLabel=Label(battlefield,"",new Vector2(0.90f,0.012f),new Vector2(0.99f,0.05f),18,Color.white);
            for(int row=0;row<2;row++) {float y=row==0 ? 0.20f : 0.82f;graveMarkers[row]=Box(battlefield,"Discard pile "+row,new Vector2(0.94f,y),new Vector2(0.94f,y),panel);graveMarkers[row].sizeDelta=new Vector2(100,40);Label(graveMarkers[row],"",Vector2.zero,Vector2.one,14,Color.white);}
        }
        void RefreshHandTools()
        {
            if(discardButton==null || engine==null)return;
            if(discardMode && (engine.State.TurnNumber!=discardTurn || engine.State.ActivePlayer!=ViewPlayer || engine.State.IsOver || engine.NeedsTarget)) {discardMode=false;discardID=-1;}
            var p=engine.State.Players[ViewPlayer];handCountLabel.text=$"HAND {p.Hand.Count}/{engine.Config.HandLimit}";handCountLabel.color=p.Hand.Count>=engine.Config.HandLimit ? gold : Color.white;
            discardButton.GetComponentInChildren<Text>().text=discardMode ? "버리기 확정" : p.ManualDiscardUsed ? "버리기 사용됨" : "카드 버리기";
            for(int row=0;row<2;row++)graveMarkers[row].GetComponentInChildren<Text>().text="묘지 "+engine.State.Players[row==0 ? ViewPlayer : 1-ViewPlayer].DiscardCount;
            if(discardMode && handViews.TryGetValue(discardID,out var selected))selected.GetComponent<Outline>().effectColor=new Color(1,0.45f,0.35f);
        }
        void ToggleDiscard()
        {
            if(Blocked()) {Toast(engine.State.ActivePlayer!=ViewPlayer ? "현재 자신의 턴이 아닙니다." : "현재 행동을 처리할 수 없습니다.");return;}
            var p=engine.State.Players[ViewPlayer];
            if(p.ManualDiscardUsed){Toast("이번 턴에는 이미 카드를 버렸습니다.");return;}
            if(p.Hand.Count==0){Toast("손패에 카드가 없습니다.");return;}
            if(engine.NeedsTarget){Toast(engine.TargetPrompt);return;}
            if(!discardMode){ClearSelection();discardMode=true;discardTurn=engine.State.TurnNumber;Toast("버릴 카드 1장을 선택한 뒤 다시 확정하세요.");Refresh();return;}
            if(discardID<0){Toast("버릴 카드를 선택해주세요.");return;}
            int actor=ViewPlayer,id=discardID;ClearSelection();Dispatch(BattleAction.Discard,id,0,()=>engine.DiscardHand(actor,id),"손패 카드 1장 버림");
        }
        void ToggleBattleMenu()
        {
            CancelInteractionSelection();battleMenu.gameObject.SetActive(!MenuOpen);battleMenu.SetAsLastSibling();
        }
        void HandleBattleEscape()
        {
            if(GamePreferences.IsOpen || GamePreferences.ConsumedEscapeFrame==Time.frameCount)return;
            if(discardMode || combinationMode || AttackDragging || dragged!=null || handID>=0 || attackerID>=0)CancelInteractionSelection();
            else ToggleBattleMenu();
        }
        IEnumerator PresentDiscard(BattleEvent e)
        {
            if(e.Secondary==1)yield break; // Overflow draws have already travelled from deck to grave.
            bool own=e.Owner==ViewPlayer;RectTransform original=null;
            if(own)handViews.TryGetValue(e.TargetID,out original);else original=backViews.LastOrDefault();
            RectTransform ghost;
            if(original!=null) {ghost=Instantiate(original.gameObject,dragLayer).GetComponent<RectTransform>();foreach(var behaviour in ghost.GetComponentsInChildren<MonoBehaviour>())if(!(behaviour is Graphic))behaviour.enabled=false;}
            else {ghost=Box(dragLayer,"Anonymous discard",Vector2.one*0.5f,Vector2.one*0.5f,new Color(0.13f,0.17f,0.25f));ghost.sizeDelta=new Vector2(70,100);Label(ghost,"◇",Vector2.zero,Vector2.one,24,Color.white);}
            foreach(var g in ghost.GetComponentsInChildren<Graphic>())g.raycastTarget=false;
            ghost.anchorMin=ghost.anchorMax=Vector2.one*0.5f;ghost.position=original!=null ? original.position : opponentHand.position;
            var from=ghost.anchoredPosition;var to=(Vector2)dragLayer.InverseTransformPoint(graveMarkers[own ? 0 : 1].position);var group=ghost.GetComponent<CanvasGroup>();if(group==null)group=ghost.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;
            if(original!=null)original.gameObject.SetActive(false);
            yield return HandStep(0.35f,t=>{ghost.anchoredPosition=Vector2.Lerp(from,to,t);ghost.localScale=Vector3.one*Mathf.Lerp(1,0.3f,t);group.alpha=1-t*0.6f;});Destroy(ghost.gameObject);
        }
        IEnumerator PresentOverflow(BattleEvent e)
        {
            bool own=e.Owner==ViewPlayer;var ghost=Box(dragLayer,"Overflow draw",Vector2.one*0.5f,Vector2.one*0.5f,new Color(0.13f,0.17f,0.25f));ghost.sizeDelta=new Vector2(72,100);Label(ghost,"◇",Vector2.zero,Vector2.one,25,Color.white);
            foreach(var g in ghost.GetComponentsInChildren<Graphic>())g.raycastTarget=false;
            var from=(Vector2)dragLayer.InverseTransformPoint(deckMarkers[own ? 0 : 1].position);var to=(Vector2)dragLayer.InverseTransformPoint(graveMarkers[own ? 0 : 1].position);var group=ghost.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;
            yield return HandStep(0.5f,t=>{ghost.anchoredPosition=Vector2.Lerp(from,to,t)+Vector2.left*Mathf.Sin(t*Mathf.PI)*70;ghost.localScale=Vector3.one*Mathf.Lerp(1,0.4f,t);group.alpha=1-t*0.7f;});Destroy(ghost.gameObject);
            Toast((own ? "손패 초과: 카드 " : "상대 손패 초과: 카드 ")+Mathf.Max(1,overflowCounts[e.Owner])+"장 폐기");
        }
        internal bool SmokeDiscard(int id)
        {
            if(!Debug.isDebugBuild || !System.Environment.GetCommandLineArgs().Contains("-unknownSmoke"))return false;
            var card=engine.State.Players[ViewPlayer].Hand.FirstOrDefault(c=>c!=null && c.InstanceID==id);if(card==null)return false;
            ToggleDiscard();if(!discardMode)return false;SelectHand(card);if(discardID!=id)return false;ToggleDiscard();return !discardMode;
        }
        internal bool SmokeOptions(bool open)
        {
            if(!Debug.isDebugBuild || !System.Environment.GetCommandLineArgs().Contains("-unknownSmoke"))return false;
            if(open){if(!MenuOpen)ToggleBattleMenu();GamePreferences.Instance.Open();return Blocked();}
            GamePreferences.Instance.Close();if(MenuOpen)ToggleBattleMenu();return !Blocked();
        }
    }
}
