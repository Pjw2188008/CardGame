using System;
using System.Collections.Generic;
using System.Linq;
namespace Unknown.Battle
{
    public enum PokerKind { None,OnePair,TwoPair,Triple,Straight,Flush,FullHouse,FourKind,StraightFlush,RoyalFlush }
    [Serializable] public sealed class PokerState
    {
        public PokerKind kind;
        public int[] members=new int[0],targets=new int[0];
        public bool straightRewardUsed;
        public PokerState Copy() => new PokerState{kind=kind,members=(int[])members.Clone(),targets=(int[])targets.Clone(),straightRewardUsed=straightRewardUsed};
    }
    public static class PokerSynergy
    {
        public static int RankValue(Rank r) => r==Rank.Ace ? 14 : r==Rank.Jack ? 11 : r==Rank.Queen ? 12 : r==Rank.King ? 13 : (int)r;
        public static int Attack(PokerKind k) => new[]{0,2,2,3,3,2,4,5,6,8}[(int)k];
        public static int HP(PokerKind k) => new[]{0,0,1,2,0,4,4,3,6,8}[(int)k];
        public static string Name(PokerKind k) => new[]{"NONE","ONE PAIR","TWO PAIR","TRIPLE","STRAIGHT","FLUSH","FULL HOUSE","FOUR OF A KIND","STRAIGHT FLUSH","ROYAL FLUSH"}[(int)k];
        public static string Condition(PokerKind k) => new[]{"족보 없음","같은 계급 2장","서로 다른 페어 2쌍","같은 계급 3장","연속 계급 5장 (A2345 / TJQKA)","같은 문양 5장","트리플 + 페어","같은 계급 4장","같은 문양의 연속 계급 5장","같은 문양 10·J·Q·K·A"}[(int)k];
        public static bool All(PokerKind k) => k!=PokerKind.None && k!=PokerKind.OnePair && k!=PokerKind.Triple && k!=PokerKind.FourKind;
        public static string Effect(PokerKind k) => k==PokerKind.None ? "활성 시너지 없음" : (All(k) ? "아군 전체" : "구성 카드")+" ATK +"+Attack(k)+(HP(k)>0 ? " / MAX HP +"+HP(k) : "")+(k==PokerKind.Straight ? "\n경기당 최초 1회 카드 2장 드로우" : "");
        public static PokerState Evaluate(IEnumerable<CardInstance> board)
        {
            var alive=board.Where(c=>c!=null && c.CurrentHP>0).ToArray();
            var cards=alive.Where(c=>c.Definition.CardType==CardType.Normal).OrderBy(c=>c.InstanceID).ToArray();
            var groups=cards.GroupBy(c=>RankValue(c.Definition.Rank)).OrderByDescending(g=>g.Count()).ThenByDescending(g=>g.Key).ToArray();
            var ranks=cards.Select(c=>RankValue(c.Definition.Rank)).Distinct().OrderBy(r=>r).ToArray();
            bool straight=cards.Length==5 && ranks.Length==5 && (ranks[4]-ranks[0]==4 || ranks.SequenceEqual(new[]{2,3,4,5,14}));
            bool flush=cards.Length==5 && cards.All(c=>c.Definition.Suit==cards[0].Definition.Suit);
            PokerKind kind=PokerKind.None; CardInstance[] members=new CardInstance[0];
            if(flush && straight)kind=ranks.SequenceEqual(new[]{10,11,12,13,14}) ? PokerKind.RoyalFlush : PokerKind.StraightFlush;
            else if(groups.Any(g=>g.Count()>=4)) {kind=PokerKind.FourKind;members=groups.First(g=>g.Count()>=4).Take(4).ToArray();}
            else if(groups.Length==2 && groups[0].Count()==3 && groups[1].Count()==2)kind=PokerKind.FullHouse;
            else if(flush)kind=PokerKind.Flush;
            else if(straight)kind=PokerKind.Straight;
            else if(groups.Any(g=>g.Count()==3)) {kind=PokerKind.Triple;members=groups.First(g=>g.Count()==3).ToArray();}
            else if(groups.Count(g=>g.Count()==2)>=2) {kind=PokerKind.TwoPair;members=groups.Where(g=>g.Count()==2).Take(2).SelectMany(g=>g).ToArray();}
            else if(groups.Any(g=>g.Count()==2)) {kind=PokerKind.OnePair;members=groups.First(g=>g.Count()==2).ToArray();}
            if(kind!=PokerKind.None && members.Length==0)members=cards;
            return new PokerState{kind=kind,members=members.Select(c=>c.InstanceID).OrderBy(i=>i).ToArray(),targets=(All(kind) ? alive : members).Select(c=>c.InstanceID).OrderBy(i=>i).ToArray()};
        }
    }
    public sealed partial class BattleEngine
    {
        bool resolvingPoker;
        public void ReconcilePoker()
        {
            if(State.IsOver)return;
            if(viewOnly || resolvingPoker || !rules.EnablePoker) { if(!viewOnly && !resolvingPoker && !rules.EnablePoker)RemoveDeadRaw(); return; }
            resolvingPoker=true;
            try {
                var before=State.Players.Select(p=>p.Poker.Copy()).ToArray();
                var initial=State.Players.SelectMany(p=>p.Slots).Where(c=>c!=null).ToDictionary(c=>c.InstanceID,c=>new[]{c.Attack,c.MaxHP});
                for(int pass=0;pass<=rules.FieldSlots*2;pass++) {
                    RemoveDeadRaw();
                    foreach(var p in State.Players) {
                        bool reward=p.Poker.straightRewardUsed; var next=PokerSynergy.Evaluate(p.Slots);next.straightRewardUsed=reward;p.Poker=next;
                        foreach(var c in p.Slots.Where(c=>c!=null)) {
                            int max=c.MaxHP; c.PokerAttack=next.targets.Contains(c.InstanceID) ? PokerSynergy.Attack(next.kind) : 0;
                            c.PokerHP=next.targets.Contains(c.InstanceID) ? PokerSynergy.HP(next.kind) : 0;
                            c.CurrentHP+=c.MaxHP-max;
                        }
                    }
                    if(!State.Players.SelectMany(p=>p.Slots).Any(c=>c!=null && c.CurrentHP<=0))break;
                }
                for(int owner=0;owner<2;owner++) {
                    var p=State.Players[owner];
                    if(before[owner].kind!=p.Poker.kind) {
                        Emit(new BattleEvent(BattleEventKind.PokerChanged,owner,amount:(int)before[owner].kind,secondary:(int)p.Poker.kind));
                        Log($"P{owner+1}: POKER {PokerSynergy.Name(before[owner].kind)} → {PokerSynergy.Name(p.Poker.kind)}");
                    }
                    foreach(var c in p.Slots.Where(c=>c!=null))if(initial.TryGetValue(c.InstanceID,out var stats)) {
                        if(stats[0]!=c.Attack)Emit(new BattleEvent(BattleEventKind.AttackChanged,owner,target:c.InstanceID,amount:c.Attack-stats[0]));
                        if(stats[1]!=c.MaxHP)Emit(new BattleEvent(BattleEventKind.MaxHPChanged,owner,target:c.InstanceID,amount:c.MaxHP-stats[1]));
                    }
                    if(p.Poker.kind==PokerKind.Straight && !p.Poker.straightRewardUsed && !State.IsOver) {p.Poker.straightRewardUsed=true;Draw(owner,2);}
                }
            } finally {resolvingPoker=false;}
        }
        void RemoveDeadRaw()
        {
            foreach(var p in State.Players)for(int i=0;i<rules.FieldSlots;i++)if(p.Slots[i]!=null && p.Slots[i].CurrentHP<=0) {
                var dead=p.Slots[i];Emit(new BattleEvent(BattleEventKind.Destroy,dead.Owner,target:dead.InstanceID,slot:i));p.Slots[i]=null;p.DiscardCards.Add(dead);
                int max=dead.MaxHP;dead.PokerAttack=dead.PokerHP=0;dead.CurrentHP+=dead.MaxHP-max;Log($"{dead.Definition.CardName} 파괴");
            }
        }
    }
}
