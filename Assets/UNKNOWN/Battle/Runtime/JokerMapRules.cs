using System;
using System.Collections.Generic;
using System.Linq;

namespace Unknown.Battle
{
    public enum BattleMap { Normal, JokerGame }
    public enum JokerEventKind { None=-1, Strengthen, Curse, Reveal, Mana, Draw, Exchange, Discard, HalveHP, Gift, Retry, JokerCurse, ForcedGamble, LastHand, Multiply }
    [Serializable] public sealed class JokerEventDefinition
    {
        public JokerEventKind kind;
        public string name;
        public int weight, amount;
        public JokerEventDefinition Copy() => (JokerEventDefinition)MemberwiseClone();
        public static JokerEventDefinition[] Defaults()
        {
            string[] names={"운명의 강화","불운의 저주","비밀의 공개","행운의 마나","운명의 드로우","혼돈의 교환","강제 폐기","운명의 반토막","조커의 선물","운명의 재도전","조커의 저주","강제 도박","마지막 패","혼돈의 증식"};
            int[] weights={13,10,10,10,10,8,6,3,8,5,5,4,4,4}, amounts={3,2,1,2,2,1,2,50,1,20,20,1,2,50};
            return names.Select((name,i)=>new JokerEventDefinition{kind=(JokerEventKind)i,name=name,weight=weights[i],amount=amounts[i]}).ToArray();
        }
    }
    public sealed class MapEventResult
    {
        public int ID, Owner;
        public JokerEventKind Kind;
        public string Name, Detail;
        public bool Private;
    }
    public sealed partial class BattleEngine
    {
#if UNITY_EDITOR
        internal bool? DebugNextGamble;
        internal void ResetMapEventForDebug() { MapEvent=null; }
#endif
        public MapEventResult MapEvent { get; private set; }
        int revealViewer=-1, revealID=-1, revealTurn=-1;
        CardInstance viewRevealed;
        int viewRevealedIndex=-1;
        public CardInstance RevealedFor(int viewer)
        {
            if(viewOnly)return viewer==revealViewer ? viewRevealed : null;
            return viewer==revealViewer && revealTurn==State.TurnNumber && viewer==State.ActivePlayer
                ? State.Players[1-viewer].HandCards.FirstOrDefault(c=>c.InstanceID==revealID) : null;
        }
        public int RevealedIndexFor(int viewer)
        {
            var card=RevealedFor(viewer); if(card==null)return -1;
            return viewOnly ? viewRevealedIndex : State.Players[1-viewer].HandCards.IndexOf(card);
        }
        public int JokerChance(int owner) => Math.Max(0,Math.Min(100,50+State.Players[owner].JokerChanceDelta));
        bool CanMapEvent(int owner,JokerEventKind kind)
        {
            var p=State.Players[owner];
            switch(kind)
            {
                case JokerEventKind.Strengthen: case JokerEventKind.Curse: case JokerEventKind.Multiply: return p.Slots.Any(c=>c!=null && c.CurrentHP>0);
                case JokerEventKind.Reveal: return State.Players[1-owner].Hand.Count>0;
                case JokerEventKind.Mana: return p.CurrentMana<10;
                case JokerEventKind.Draw: return p.DeckCount>0;
                case JokerEventKind.Exchange: case JokerEventKind.Discard: return p.Hand.Count>0;
                case JokerEventKind.Gift: return p.Hand.Count<rules.HandLimit;
                case JokerEventKind.LastHand: return p.Hand.Count>=2;
                case JokerEventKind.ForcedGamble: return p.Hand.Any(c=>c.Definition.CardType==CardType.Joker) && p.Slots.Any(c=>c!=null && c.CurrentHP>0 && !c.JokerUsed);
                case JokerEventKind.HalveHP: case JokerEventKind.Retry: case JokerEventKind.JokerCurse: return true;
                default: return false;
            }
        }
        void StartMapTurn(int owner)
        {
            revealViewer=revealID=revealTurn=-1; viewRevealed=null;
            foreach(var p in State.Players)p.JokerChanceDelta=0;
            if(rules.Map!=BattleMap.JokerGame) { MapEvent=null; return; }
            var valid=rules.MapEvents.Where(e=>e.weight>0 && CanMapEvent(owner,e.kind)).ToArray();
            if(valid.Length==0) { ResolveMapEvent(owner,null); return; }
            int roll=random.Next(valid.Sum(e=>e.weight));
            foreach(var e in valid) { roll-=e.weight; if(roll<0) { ResolveMapEvent(owner,e); return; } }
        }
        // Internal so editor/console fixtures can cover every branch; never exposed as a network intent.
        internal bool ResolveMapEvent(int owner,JokerEventDefinition e)
        {
            if(viewOnly || rules.Map!=BattleMap.JokerGame || State.IsOver || owner!=State.ActivePlayer || (MapEvent!=null && MapEvent.ID==State.TurnNumber))return false;
            if(e!=null && !CanMapEvent(owner,e.kind))return false;
            var p=State.Players[owner]; var kind=e?.kind ?? JokerEventKind.None;
            MapEvent=new MapEventResult{ID=State.TurnNumber,Owner=owner,Kind=kind,Name=e?.name ?? "효과 없음",Detail="적용 가능한 사건이 없습니다."};
            Emit(new BattleEvent(BattleEventKind.MapEvent,owner,source:MapEvent.ID));
            int amount=Math.Max(0,e?.amount ?? 0);
            var units=p.Slots.Where(c=>c!=null && c.CurrentHP>0).ToArray();
            CardInstance unit=null;
            if(kind==JokerEventKind.Strengthen || kind==JokerEventKind.Curse || kind==JokerEventKind.Multiply)unit=units[random.Next(units.Length)];
            switch(kind)
            {
                case JokerEventKind.Strengthen: case JokerEventKind.Curse:
                    bool attack=random.Next(2)==0; int delta=kind==JokerEventKind.Strengthen ? amount : -amount;
                    ChangeMapStats(unit,attack ? Math.Max(0,unit.Attack+delta) : unit.Attack,attack ? unit.MaxHP : Math.Max(1,unit.MaxHP+delta));
                    MapEvent.Detail=unit.Definition.CardName+" · "+(attack ? "공격력 " : "최대 체력 ")+(delta>=0 ? "+" : "")+delta; break;
                case JokerEventKind.Multiply:
                    bool increase=random.Next(2)==0; double factor=increase ? 1+amount/100.0 : Math.Max(0,1-amount/100.0);
                    ChangeMapStats(unit,Math.Max(0,(int)(increase ? Math.Ceiling(unit.Attack*factor) : Math.Floor(unit.Attack*factor))),Math.Max(1,(int)(increase ? Math.Ceiling(unit.MaxHP*factor) : Math.Floor(unit.MaxHP*factor))));
                    MapEvent.Detail=unit.Definition.CardName+" · 공격력·최대 체력 "+(increase ? "+" : "-")+amount+"%"; break;
                case JokerEventKind.Reveal:
                    var other=State.Players[1-owner].HandCards; var shown=other[random.Next(other.Count)];
                    revealViewer=owner; revealID=shown.InstanceID; revealTurn=State.TurnNumber;
                    MapEvent.Private=true; MapEvent.Detail="이번 턴 공개: "+shown.Definition.CardName; break;
                case JokerEventKind.Mana:
                    int gained=Math.Min(amount,10-p.CurrentMana); p.CurrentMana+=gained; MapEvent.Detail="현재 마나 +"+gained+" (이번 턴)"; break;
                case JokerEventKind.Draw:
                    int draws=Math.Min(amount,p.DeckCount); Draw(owner,draws); MapEvent.Private=true; MapEvent.Detail="카드 "+draws+"장 추가 드로우"; break;
                case JokerEventKind.Exchange:
                    var returned=p.HandCards[random.Next(p.Hand.Count)]; p.HandCards.Remove(returned); p.DeckCards.Add(returned);
                    Emit(new BattleEvent(BattleEventKind.HandReturn,owner,target:returned.InstanceID));
                    for(int i=p.DeckCards.Count-1;i>0;i--) { int j=random.Next(i+1); var c=p.DeckCards[i]; p.DeckCards[i]=p.DeckCards[j]; p.DeckCards[j]=c; }
                    Draw(owner,1); MapEvent.Private=true; MapEvent.Detail="손패 1장 교환"; break;
                case JokerEventKind.Discard:
                    int discarded=DiscardMapCards(owner,amount); MapEvent.Private=true; MapEvent.Detail="손패 "+discarded+"장 폐기"; break;
                case JokerEventKind.HalveHP:
                    int before=p.HP; p.HP=Math.Max(1,(int)Math.Ceiling(p.HP*Math.Max(0,1-amount/100.0)));
                    Emit(new BattleEvent(BattleEventKind.HeroChanged,owner,amount:p.HP-before)); MapEvent.Detail="HP "+before+" → "+p.HP; break;
                case JokerEventKind.Gift:
                    GiveMapJoker(owner); MapEvent.Private=true; MapEvent.Detail="새로운 JOKER 1장 획득"; break;
                case JokerEventKind.LastHand:
                    DiscardMapCards(owner,2); GiveMapJoker(owner); MapEvent.Private=true; MapEvent.Detail="손패 2장 폐기 · 새로운 JOKER 1장 획득"; break;
                case JokerEventKind.Retry: case JokerEventKind.JokerCurse:
                    p.JokerChanceDelta=kind==JokerEventKind.Retry ? amount : -amount;
                    MapEvent.Private=true; MapEvent.Detail="이번 턴 다음 도박 성공률 "+JokerChance(owner)+"%"; break;
                case JokerEventKind.ForcedGamble:
                    var targets=units.Where(c=>!c.JokerUsed).ToArray(); var target=targets[random.Next(targets.Length)];
                    var joker=p.HandCards.First(c=>c.Definition.CardType==CardType.Joker);
                    GambleResolved(owner,joker,target,false); MapEvent.Detail="아군 유닛에 무료 JOKER 도박 적용"; break;
            }
            return true;
        }
        int DiscardMapCards(int owner,int count)
        {
            var p=State.Players[owner]; int actual=Math.Min(count,p.Hand.Count);
            for(int i=0;i<actual;i++) { var c=p.HandCards[random.Next(p.Hand.Count)]; p.HandCards.Remove(c); p.DiscardCards.Add(c); Emit(new BattleEvent(BattleEventKind.Discard,owner,target:c.InstanceID)); }
            return actual;
        }
        internal void GiveMapJoker(int owner)
        {
            var p=State.Players[owner]; if(p.Hand.Count>=rules.HandLimit || viewOnly || State.IsOver)return;
            var c=NewCard(CardDatabase.Get("Joker_1"),owner); p.HandCards.Add(c);
            Emit(new BattleEvent(BattleEventKind.CardGranted,owner,target:c.InstanceID,definition:c.Definition));
        }
        void ChangeMapStats(CardInstance c,int attack,int max)
        {
            int da=attack-c.Attack,dh=max-c.MaxHP; c.Attack=attack; c.MaxHP=max;
            c.CurrentHP=dh>0 ? c.CurrentHP+dh : Math.Min(c.CurrentHP,max);
            if(da!=0)Emit(new BattleEvent(BattleEventKind.AttackChanged,c.Owner,target:c.InstanceID,amount:da));
            if(dh!=0)Emit(new BattleEvent(BattleEventKind.MaxHPChanged,c.Owner,target:c.InstanceID,amount:dh));
        }
    }
}
