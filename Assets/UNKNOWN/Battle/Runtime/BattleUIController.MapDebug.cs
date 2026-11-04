#if UNITY_EDITOR
using System.Linq;
using UnityEngine;
namespace Unknown.Battle
{
    public sealed partial class BattleUIController
    {
        public bool CanLocalMapDebug => online==null && engine!=null && !IsPresenting && !engine.State.IsOver;
        public void DebugMapNewGame(BattleMap map) { if(online!=null)return; localMap=map; NewMatch(); }
        public bool DebugMapEvent(JokerEventKind kind)
        {
            if(!CanLocalMapDebug || engine.Config.Map!=BattleMap.JokerGame)return false;
            engine.ResetMapEventForDebug(); presentedMapEvents.Remove(engine.State.TurnNumber);
            bool ok=engine.ResolveMapEvent(ViewPlayer,JokerMapSettings.LoadRules().First(e=>e.kind==kind));
            var entries=engine.DrainEvents(); PrepareHandPlayback(entries); StartCoroutine(ActionPresentation(entries)); return ok;
        }
        public bool DebugGiveJoker()
        {
            if(!CanLocalMapDebug)return false;
            engine.GiveMapJoker(ViewPlayer); var entries=engine.DrainEvents(); PrepareHandPlayback(entries); StartCoroutine(ActionPresentation(entries)); return true;
        }
        public void DebugGambleOutcome(int outcome) { if(CanLocalMapDebug)engine.DebugNextGamble=outcome<0 ? (bool?)null : outcome==1; }
        public string DebugMapStatus => online!=null ? "온라인 대전에서는 디버그를 사용할 수 없습니다." : engine==null ? "로컬 전투를 실행하세요." : "맵: "+engine.Config.Map+" · JOKER 성공 "+engine.State.Players[ViewPlayer].JokerSuccesses+"/3";
        public void DebugMapVictory()
        {
            if(!CanLocalMapDebug)return;
            if(engine.Config.Map==BattleMap.JokerGame)
            {
                int actor=ViewPlayer;
                for(int i=0;i<engine.Config.FieldSlots && !engine.State.IsOver;i++)
                {
                    var p=engine.State.Players[actor]; var c=engine.NewCard(CardDatabase.Get("Heart_K"),actor); p.Slots[i]=c;
                    var j=engine.NewCard(CardDatabase.Get("Joker_1"),actor); p.HandCards.Add(j); p.CurrentMana=10; engine.DebugNextGamble=true; engine.Gamble(actor,j.InstanceID,c.InstanceID);
                }
            }
            else for(int i=0;i<3 && !engine.State.IsOver;i++)
            { var a=engine.NewCard(CardDatabase.Get("Heart_A"),ViewPlayer); var f=engine.NewCard(CardDatabase.Get("Club_K"),ViewPlayer); engine.State.Players[ViewPlayer].HandCards.Add(a); engine.State.Players[ViewPlayer].HandCards.Add(f); engine.Combine(ViewPlayer,a.InstanceID,f.InstanceID); }
            var entries=engine.DrainEvents(); PrepareHandPlayback(entries); StartCoroutine(ActionPresentation(entries));
        }
    }
}
#endif
