using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    public sealed partial class BattleUIController
    {
        readonly Text[] pokerLabels=new Text[2];
        RectTransform pokerList;
        readonly Text[] pokerRows=new Text[10];
        readonly Color statUp=new Color(0.05f,0.52f,0.22f),statDown=new Color(0.82f,0.13f,0.16f);
        void BuildPokerUI()
        {
            for(int row=0;row<2;row++) {
                float bottom=row==0 ? 0.37f : 0.70f;
                var badge=Box(battlefield,"Poker Badge "+row,new Vector2(0.80f,bottom),new Vector2(0.985f,bottom+0.105f),new Color(0.05f,0.065f,0.09f,0.85f));
                badge.gameObject.AddComponent<Button>().onClick.AddListener(()=>pokerList.gameObject.SetActive(!pokerList.gameObject.activeSelf));
                pokerLabels[row]=Label(badge,"",Vector2.zero,Vector2.one,15,new Color(0.84f,0.76f,1));
            }
            pokerList=Box(interactionRoot,"Poker reference",new Vector2(0.01f,0.31f),new Vector2(0.21f,0.84f),new Color(0.025f,0.035f,0.055f,1));
            Label(pokerList,"POKER SYNERGY",new Vector2(0.05f,0.91f),new Vector2(0.95f,0.99f),24,gold);
            ActionButton(pokerList,"닫기",new Vector2(0.85f,0.93f),new Vector2(0.98f,0.99f),()=>pokerList.gameObject.SetActive(false));
            for(int i=1;i<=9;i++) {
                float top=0.90f-(i-1)*0.096f; var k=(PokerKind)i;
                pokerRows[i]=Label(pokerList,PokerSynergy.Name(k)+" · "+PokerSynergy.Condition(k)+"\n"+PokerSynergy.Effect(k),new Vector2(0.03f,top-0.09f),new Vector2(0.97f,top),13,Color.white);
            }
            pokerList.gameObject.SetActive(false);
        }
        void RefreshPokerUI()
        {
            if(engine==null || ViewPlayer<0)return;
            if(pokerLabels[0]==null)BuildPokerUI();
            for(int row=0;row<2;row++) {var p=engine.State.Players[row==0 ? ViewPlayer : 1-ViewPlayer].Poker;
                pokerLabels[row].text=(row==0 ? "MY SYNERGY" : "ENEMY SYNERGY")+"\n"+PokerSynergy.Name(p.kind)+"\n"+PokerSynergy.Effect(p.kind);
            }
            for(int i=1;i<=9;i++)pokerRows[i].color=engine.State.Players[ViewPlayer].Poker.kind==(PokerKind)i ? gold : engine.State.Players[1-ViewPlayer].Poker.kind==(PokerKind)i ? new Color(1,0.6f,0.7f) : Color.white;
            foreach(var p in engine.State.Players)foreach(var card in p.Board.Where(c=>c!=null))if(cardViews.TryGetValue(card.InstanceID,out var rect))UpdateCardStats(rect,card,true);
        }
        IEnumerator PresentPoker(BattleEvent e)
        {
            RefreshPokerUI(); var old=(PokerKind)e.Amount;var next=(PokerKind)e.Secondary;
            if(next!=PokerKind.None)StartCoroutine(PulsePoker(e.Owner));
            string title=next==PokerKind.None ? "POKER SYNERGY LOST" : old==PokerKind.None ? "POKER SYNERGY ACTIVATED" : "POKER SYNERGY CHANGED";
            string name=old!=PokerKind.None && next!=PokerKind.None ? PokerSynergy.Name(old)+" → "+PokerSynergy.Name(next) : PokerSynergy.Name(next==PokerKind.None ? old : next);
            yield return animations.PokerBanner((e.Owner==ViewPlayer ? "MY " : "ENEMY ")+title+"\n"+name+(next==PokerKind.None ? "" : "\n"+PokerSynergy.Effect(next)));
        }
        IEnumerator PulsePoker(int owner)
        {
            var rings=engine.State.Players[owner].Poker.members.Where(id=>cardViews.ContainsKey(id)).Select(id=>cardViews[id].Find("Poker border")?.GetComponent<AttackGlowGraphic>()).Where(g=>g!=null).ToArray();
            yield return HandStep(0.45f,t=>{foreach(var ring in rings)if(ring!=null)ring.color=new Color(0.66f,0.4f,0.95f,0.35f+0.3f*Mathf.Sin(t*Mathf.PI));});
        }
        string StatBreakdown(CardInstance c)
        {
            string text=$"ATK: {c.Definition.Attack} → {c.Attack}    HP: {c.CurrentHP}/{c.MaxHP}\n기본 ATK {c.Definition.Attack} / MAX HP {c.Definition.MaxHP}";
            void Line(string name,int a,int h) {if(a!=0 || h!=0)text+="\n"+name+": ATK "+a.ToString("+0;-0;0")+" / HP "+h.ToString("+0;-0;0");}
            Line("포커 시너지",c.PokerAttack,c.PokerHP);Line("맵 효과",c.MapAttack,c.MapHP);Line("JOKER 효과",c.JokerAttack,c.JokerHP);Line("카드 특수 능력",c.AbilityAttack,c.AbilityHP);Line("기타 강화·약화",c.OtherAttack,c.OtherHP);
            if(c.CurrentHP<c.MaxHP)text+="\n받은 피해: "+(c.MaxHP-c.CurrentHP);
            return text;
        }
        void UpdateCardStats(RectTransform rect,CardInstance card,bool onBoard)
        {
            var atk=rect.Find("Stat ATK")?.GetComponent<Text>();var hp=rect.Find("Stat HP")?.GetComponent<Text>(); if(atk==null || hp==null)return;
            var ink=card.Definition.CardType==CardType.Unknown ? new Color(0.85f,0.73f,1) : SuitInk(card.Definition.Suit);
            atk.text=card.Definition.CardType==CardType.Joker ? "50%" : card.Attack.ToString();
            atk.color=card.Attack>card.Definition.Attack ? statUp : card.Attack<card.Definition.Attack ? statDown : ink;
            Color current=card.CurrentHP<card.MaxHP ? statDown : card.MaxHP>card.Definition.MaxHP ? statUp : card.MaxHP<card.Definition.MaxHP ? statDown : ink;
            Color maximum=card.MaxHP>card.Definition.MaxHP ? statUp : card.MaxHP<card.Definition.MaxHP ? statDown : ink;
            hp.text=card.Definition.CardType==CardType.Joker ? "" : "<color=#"+ColorUtility.ToHtmlStringRGB(current)+">"+card.CurrentHP+"</color><size=12>/<color=#"+ColorUtility.ToHtmlStringRGB(maximum)+">"+card.MaxHP+"</color></size>";
            if(!onBoard)return;
            bool member=engine.State.Players[card.Owner].Poker.members.Contains(card.InstanceID);
            var icon=rect.Find("Poker Icon");
            if(icon==null) { var marker=Label(rect,"✦",new Vector2(0.74f,0.66f),new Vector2(0.96f,0.83f),18,new Color(0.66f,0.4f,0.95f));marker.name="Poker Icon";icon=marker.transform;
                var innerGO=new GameObject("Poker border",typeof(RectTransform),typeof(CanvasRenderer),typeof(AttackGlowGraphic));innerGO.transform.SetParent(rect,false);
                var inner=innerGO.GetComponent<RectTransform>();inner.anchorMin=new Vector2(0.035f,0.035f);inner.anchorMax=new Vector2(0.965f,0.965f);inner.offsetMin=inner.offsetMax=Vector2.zero;
                var ring=innerGO.GetComponent<AttackGlowGraphic>();ring.raycastTarget=false;ring.color=new Color(0.66f,0.4f,0.95f,0.35f);
            }
            icon.gameObject.SetActive(member);rect.Find("Poker border").gameObject.SetActive(member);
            rect.GetComponent<CardHover>()?.Configure(data=>ShowTooltip(card,rect,data),HideTooltip,true,false);
        }
        int lastDropID=-1;Vector2 lastDropPoint;
        void RememberDrop() {if(dragged!=null){lastDropID=dragged.InstanceID;lastDropPoint=dragGhost!=null ? dragGhost.anchoredPosition : Vector2.zero;}}
        void ReturnRejectedCard() {if(lastDropID>=0 && handViews.TryGetValue(lastDropID,out var rect))StartCoroutine(ReturnRejected(rect));lastDropID=-1;}
        IEnumerator ReturnRejected(RectTransform rect)
        {
            var ghost=Instantiate(rect.gameObject,dragLayer).GetComponent<RectTransform>();
            ghost.name="Rejected card return";
            foreach(var c in ghost.GetComponentsInChildren<CardDrag>())c.enabled=false;foreach(var c in ghost.GetComponentsInChildren<CardHover>())c.enabled=false;foreach(var c in ghost.GetComponentsInChildren<HandCardMotion>())c.enabled=false;
            foreach(var g in ghost.GetComponentsInChildren<Graphic>())g.raycastTarget=false;
            var group=ghost.GetComponent<CanvasGroup>();if(group==null)group=ghost.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;
            ghost.anchorMin=ghost.anchorMax=Vector2.one*0.5f;ghost.sizeDelta=rect.rect.size;Vector2 from=lastDropPoint,to=dragLayer.InverseTransformPoint(rect.position);
            yield return HandStep(0.22f,t=>{if(ghost==null)return;ghost.anchoredPosition=Vector2.Lerp(from,to,t);group.alpha=1-t*0.4f;});if(ghost!=null)Destroy(ghost.gameObject);
        }
    }
}
