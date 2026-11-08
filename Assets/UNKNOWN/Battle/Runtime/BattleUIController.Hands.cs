using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    public sealed partial class BattleUIController
    {
        [SerializeField, Range(0.2f, 1.5f)] float drawMoveDuration = 0.5f;
        [SerializeField, Range(0.1f, 0.4f)] float drawFlipDuration = 0.18f;
        [SerializeField, Range(0.1f, 0.5f)] float handReflowDuration = 0.25f;
        [SerializeField, Range(0.05f, 0.3f)] float drawInterval = 0.12f;
        readonly Dictionary<int, RectTransform> handViews = new Dictionary<int, RectTransform>();
        readonly List<RectTransform> backViews = new List<RectTransform>();
        readonly HashSet<int> pendingDraws = new HashSet<int>();
        int pendingEnemyDraws, handViewer = -1;
        readonly RectTransform[] deckMarkers = new RectTransform[2];
        void BuildDeckMarkers()
        {
            for (int row = 0; row < 2; row++)
            {
                float y = row == 0 ? 0.12f : 0.88f;
                var rect = Box(battlefield, "Deck " + row, new Vector2(0.94f,y), new Vector2(0.94f,y), new Color(0.13f,0.17f,0.25f));
                rect.sizeDelta = new Vector2(62,84); Label(rect,"◇",Vector2.zero,Vector2.one,23,new Color(0.62f,0.52f,0.79f));
                deckMarkers[row] = rect;
            }
        }
        void ResetHandViews()
        {
            if(dragLayer!=null)foreach(Transform view in dragLayer)if(view.name=="Rejected card return") {view.gameObject.SetActive(false);Destroy(view.gameObject);}
            foreach (var view in handViews.Values) if (view != null) { view.gameObject.SetActive(false); Destroy(view.gameObject); }
            foreach (var view in backViews) if (view != null) { view.gameObject.SetActive(false); Destroy(view.gameObject); }
            handViews.Clear(); backViews.Clear(); pendingDraws.Clear(); pendingEnemyDraws = 0; handViewer = ViewPlayer;
        }
        void PrepareHandPlayback(IReadOnlyList<BattleEvent> entries)
        {
            if (handViewer != ViewPlayer) ResetHandViews();
            pendingDraws.Clear(); pendingEnemyDraws = 0;
            foreach (var e in entries.Where(e => e.Kind == BattleEventKind.Draw || e.Kind==BattleEventKind.CardGranted))
                if (e.Owner == ViewPlayer) pendingDraws.Add(e.TargetID); else pendingEnemyDraws++;
        }
        void RefreshHands(PlayerState me, PlayerState enemy)
        {
            if (handViewer != ViewPlayer) ResetHandViews();
            Canvas.ForceUpdateCanvases();
            var cards = me.Hand.Where(c => c != null && !pendingDraws.Contains(c.InstanceID)).ToArray();
            foreach (var id in handViews.Keys.Where(id => !cards.Any(c => c.InstanceID == id)).ToArray())
            { var view=handViews[id]; view.gameObject.SetActive(false); Destroy(view.gameObject); handViews.Remove(id); }
            float available = Mathf.Max(200, ((RectTransform)handContent.parent).rect.width);
            int count = cards.Length;
            float gap = Mathf.Lerp(24,8,Mathf.InverseLerp(1,10,count));
            float width = Mathf.Min(180,(available-Mathf.Max(0,count-1)*gap)/Mathf.Max(1,count));
            float total = count*width+Mathf.Max(0,count-1)*gap;
            handContent.sizeDelta = new Vector2(available,width*1.22f);
            for (int i=0;i<count;i++)
            {
                var card=cards[i]; bool created=!handViews.TryGetValue(card.InstanceID,out var rect);
                if(created)
                {
                    rect=Box(handContent,card.Definition.CardName,Vector2.one*0.5f,Vector2.one*0.5f,paper);
                    DrawCard(rect,card,false,false,()=>SelectHand(card),false);
                    rect.gameObject.AddComponent<HandCardMotion>(); handViews[card.InstanceID]=rect;
                }
                bool selected=handID==card.InstanceID || aceID==card.InstanceID || faceID==card.InstanceID;
                rect.GetComponent<Outline>().effectColor=combinationMode && (aceID==card.InstanceID || faceID==card.InstanceID) ? combinationPurple : selected ? gold : SuitInk(card.Definition.Suit);
                var group=rect.GetComponent<CanvasGroup>(); if(group==null)group=rect.gameObject.AddComponent<CanvasGroup>();
                if(dragged==null || dragged.InstanceID!=card.InstanceID)group.alpha=1;
                UpdateCardStats(rect,card,false);
                var motion=rect.GetComponent<HandCardMotion>(); motion.Held=dragged!=null && dragged.InstanceID==card.InstanceID;
                motion.Layout(new Vector2(-total/2+width/2+i*(width+gap),handID==card.InstanceID ? 10 : 0),new Vector2(width,width*1.22f),handReflowDuration,created);
                cardViews[card.InstanceID]=rect;
            }
            int enemyCount=Mathf.Max(0,enemy.Hand.Count-pendingEnemyDraws);
            while(backViews.Count>enemyCount) { var view=backViews[backViews.Count-1]; backViews.RemoveAt(backViews.Count-1); view.gameObject.SetActive(false); Destroy(view.gameObject); }
            float backWidth=46, backGap=Mathf.Lerp(18,8,Mathf.InverseLerp(1,10,enemyCount));
            for(int i=0;i<enemyCount;i++)
            {
                bool created=i>=backViews.Count;
                if(created)
                {
                    var rect=Box(opponentHand,"Hidden opponent card",Vector2.one*0.5f,Vector2.one*0.5f,new Color(0.13f,0.17f,0.25f));
                    Label(rect,"◇",Vector2.zero,Vector2.one,23,new Color(0.62f,0.52f,0.79f)); rect.gameObject.AddComponent<HandCardMotion>(); backViews.Add(rect);
                }
                backViews[i].GetComponent<HandCardMotion>().Layout(new Vector2((i-(enemyCount-1)/2f)*(backWidth+backGap),0),new Vector2(backWidth,64),handReflowDuration,created);
                if(i<enemy.Hand.Count && enemy.Hand[i]!=null)cardViews[enemy.Hand[i].InstanceID]=backViews[i];
            }
            RefreshOpponentReveal();
        }
        Color SuitInk(Suit suit) => suit==Suit.Diamond ? new Color(0.72f,0.12f,0.15f) : suit==Suit.Heart ? new Color(0.8f,0.22f,0.46f) : suit==Suit.Club ? new Color(0.12f,0.44f,0.3f) : new Color(0.29f,0.17f,0.4f);
        IEnumerator HandStep(float seconds, System.Action<float> step)
        {
            float elapsed=0;
            while(elapsed<seconds) { if(!IsPaused) { elapsed+=Time.unscaledDeltaTime; step(Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/seconds))); } yield return null; }
            step(1);
        }
        IEnumerator PresentDraw(BattleEvent entry)
        {
            bool own=entry.Owner==ViewPlayer;
            if(own)pendingDraws.Remove(entry.TargetID); else pendingEnemyDraws=Mathf.Max(0,pendingEnemyDraws-1);
            RefreshHands(engine.State.Players[ViewPlayer],engine.State.Players[1-ViewPlayer]);
            RectTransform rect=own ? handViews.TryGetValue(entry.TargetID,out var card) ? card : null : backViews.LastOrDefault();
            // Burned cards never enter the hand and use the existing discard presentation.
            if(rect==null)yield break;
            var motion=rect.GetComponent<HandCardMotion>(); motion.Held=true;
            Vector2 destination=motion.Destination;
            Vector2 origin=(Vector2)rect.parent.InverseTransformPoint(deckMarkers[own ? 0 : 1].position);
            rect.anchoredPosition=origin; rect.localScale=Vector3.one*0.55f;
            RectTransform cover=null;
            if(own) { cover=Box(rect,"Drawing card back",Vector2.zero,Vector2.one,new Color(0.13f,0.17f,0.25f)); Label(cover,"◇",Vector2.zero,Vector2.one,23,new Color(0.62f,0.52f,0.79f)); }
            var group=rect.GetComponent<CanvasGroup>(); if(group==null)group=rect.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts=false;
            yield return HandStep(drawMoveDuration,t=> { rect.anchoredPosition=Vector2.Lerp(origin,destination,t)+Vector2.up*Mathf.Sin(t*Mathf.PI)*35; rect.localScale=Vector3.one*Mathf.Lerp(0.55f,1,t); });
            if(own)yield return HandStep(drawFlipDuration,t=> { rect.localScale=new Vector3(Mathf.Abs(1-2*t),1,1); if(t>=0.5f && cover!=null)cover.gameObject.SetActive(false); });
            if(cover!=null)Destroy(cover.gameObject);
            rect.localScale=Vector3.one; rect.anchoredPosition=destination; motion.Held=false; group.blocksRaycasts=true;
            yield return HandStep(drawInterval,_=>{});
        }
        IEnumerator InitialHandPresentation(IReadOnlyList<BattleEvent> entries)
        {
            transitioning=true;
            clock.Begin(Mathf.Max(1,turnDuration),Time.realtimeSinceStartupAsDouble); ShowRefillFeedback(entries);
            yield return animations.Play(entries);
            animations.ClearAction(); transitioning=false; Refresh();
            clock.Begin(Mathf.Max(1,turnDuration),Time.realtimeSinceStartupAsDouble);
        }
        void ShowRefillFeedback(IReadOnlyList<BattleEvent> entries)
        {
            var refill=entries.FirstOrDefault(e=>e.Kind==BattleEventKind.HandRefill && e.Owner==ViewPlayer);
            if(refill!=null)Toast("손패 소진 — 카드 "+refill.Amount+"장 보충");
        }
    }
}
