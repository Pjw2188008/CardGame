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
        public int rewardsUsed;
        public int flushHealTurn = -1;
        public PokerState Copy() => new PokerState{kind=kind,members=(int[])members.Clone(),targets=(int[])targets.Clone(),rewardsUsed=rewardsUsed,flushHealTurn=flushHealTurn};
    }
    public static class PokerSynergy
    {
        public static int RankValue(Rank r) => r==Rank.Ace ? 14 : r==Rank.Jack ? 11 : r==Rank.Queen ? 12 : r==Rank.King ? 13 : (int)r;
        public static int Attack(PokerKind k) => new[]{0,1,1,2,4,0,4,4,4,5}[(int)k];
        public static int HP(PokerKind k) => new[]{0,0,2,0,0,6,4,0,4,5}[(int)k];
        public static string Name(PokerKind k) => new[]{"NONE","ONE PAIR","TWO PAIR","THREE OF A KIND","STRAIGHT","FLUSH","FULL HOUSE","FOUR OF A KIND","STRAIGHT FLUSH","ROYAL FLUSH"}[(int)k];
        public static string Condition(PokerKind k) => new[]{"족보 없음","같은 계급 2장","서로 다른 페어 2쌍","같은 계급 3장","연속 계급 5장 (A2345 / TJQKA)","같은 문양 5장","트리플 + 페어","같은 계급 4장","같은 문양의 연속 계급 5장","같은 문양 10·J·Q·K·A"}[(int)k];
        public static bool All(PokerKind k) => k!=PokerKind.None;
        public static string Effect(PokerKind k)
        {
            if(k==PokerKind.None)return "활성 시너지 없음";
            string stats="아군 전체 "+(Attack(k)>0 ? "ATK +"+Attack(k) : "")+(HP(k)>0 ? (Attack(k)>0 ? " / " : "")+"MAX HP +"+HP(k) : "");
            string extra=k==PokerKind.Straight ? "유닛별 첫 공격 피해 +2" : k==PokerKind.Flush ? "내 턴 시작: 아군 각각 HP 2 회복" :
                k==PokerKind.FullHouse ? "최초 완성: 드로우 2 · 내 HP 4 회복" : k==PokerKind.FourKind ? "최초 완성: 적 필드 전체 피해 2" :
                k==PokerKind.StraightFlush ? "최초 완성: 적 필드 전체 피해 3" : k==PokerKind.RoyalFlush ? "최초 완성: 적 전체 피해 5 · 적 HP 피해 5" : "";
            return stats+(extra.Length>0 ? "\n"+extra : "");
        }
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
            if(State.IsOver || viewOnly || resolvingPoker)return;
            if(!rules.EnablePoker){RemoveDeadRaw();return;}
            resolvingPoker=true;
            try {
                // A reward is marked before resolving it. Re-evaluate deaths before any next reward.
                // Only eight one-time rewards exist across both owners, so this cannot recur forever.
                while(!State.IsOver) {
                    for(int pass=0;pass<=rules.FieldSlots*2;pass++) {
                        RemoveDeadRaw();
                        for(int playerIndex=0;playerIndex<State.Players.Count;playerIndex++) {
                            var p=State.Players[playerIndex];
                            var old=p.Poker;var next=PokerSynergy.Evaluate(p.Slots);
                            next.rewardsUsed=old.rewardsUsed;next.flushHealTurn=old.flushHealTurn;p.Poker=next;
                            foreach(var c in p.Slots.Where(c=>c!=null)) {
                                int attack=c.Attack,max=c.MaxHP;
                                c.PokerAttack=next.kind!=PokerKind.None ? PokerSynergy.Attack(next.kind) : 0;
                                c.PokerHP=next.kind!=PokerKind.None ? PokerSynergy.HP(next.kind) : 0;
                                c.CurrentHP=Math.Min(c.MaxHP,c.CurrentHP+c.MaxHP-max);
                                if(next.kind!=PokerKind.Straight)c.PokerStraightAttackSpent=false;
                                if(attack!=c.Attack)Emit(new BattleEvent(BattleEventKind.AttackChanged,playerIndex,target:c.InstanceID,amount:c.Attack-attack));
                                if(max!=c.MaxHP)Emit(new BattleEvent(BattleEventKind.MaxHPChanged,playerIndex,target:c.InstanceID,amount:c.MaxHP-max));
                            }
                            if(old.kind!=next.kind) {
                                Emit(new BattleEvent(BattleEventKind.PokerChanged,playerIndex,amount:(int)old.kind,secondary:(int)next.kind));
                                Log($"P{playerIndex+1}: POKER {PokerSynergy.Name(old.kind)} → {PokerSynergy.Name(next.kind)}");
                            }
                        }
                        if(!State.Players.SelectMany(p=>p.Slots).Any(c=>c!=null && c.CurrentHP<=0))break;
                    }
                    int owner=Enumerable.Range(0,State.Players.Count).Where(i=>State.Players[i].Poker.kind>=PokerKind.FullHouse && (State.Players[i].Poker.rewardsUsed & (1<<(int)State.Players[i].Poker.kind))==0).DefaultIfEmpty(-1).First();
                    if(owner<0)break;
                    var poker=State.Players[owner].Poker;var kind=poker.kind;
                    poker.rewardsUsed|=1<<(int)kind;
                    ResolvePokerReward(owner,kind);
                }
            } finally {resolvingPoker=false;}
        }
        void ResolvePokerReward(int owner,PokerKind kind)
        {
            var player=State.Players[owner];
            if(kind==PokerKind.FullHouse) {
                Draw(owner,2);int before=player.HP;player.HP=Math.Min(rules.StartingHP,player.HP+4);
                Emit(new BattleEvent(BattleEventKind.HeroChanged,owner,amount:player.HP-before));
            } else {
                int damage=kind==PokerKind.FourKind ? 2 : kind==PokerKind.StraightFlush ? 3 : 5;
                foreach(var unit in State.Players[1-owner].Slots.Where(c=>c!=null && c.CurrentHP>0).ToArray())
                    DamageUnit(unit,damage,-1,Suit.None);
                if(kind==PokerKind.RoyalFlush) {
                    var enemy=State.Players[1-owner];int actual=Math.Min(enemy.HP,5);enemy.HP=Math.Max(0,enemy.HP-5);
                    Emit(new BattleEvent(BattleEventKind.HeroChanged,1-owner,amount:-actual));
                    // Settle field deaths even when the direct hit ends the match.
                    RemoveDeadRaw();
                    if(enemy.HP<=0)FinishGame(owner,VictoryReason.OpponentHPZero);
                }
            }
        }
        int PokerAttackDamage(CardInstance unit)
        {
            if(rules.EnablePoker && State.Players[unit.Owner].Poker.kind==PokerKind.Straight && !unit.PokerStraightAttackSpent) {
                unit.PokerStraightAttackSpent=true;return unit.Attack+2;
            }
            return unit.Attack;
        }
        void StartPokerTurn(int owner)
        {
            if(State.IsOver || viewOnly || !rules.EnablePoker)return;
            var p=State.Players[owner];
            if(p.Poker.kind!=PokerKind.Flush || p.Poker.flushHealTurn==State.TurnNumber)return;
            p.Poker.flushHealTurn=State.TurnNumber;
            foreach(var c in p.Slots.Where(c=>c!=null && c.CurrentHP>0)) {
                int before=c.CurrentHP;c.CurrentHP=Math.Min(c.MaxHP,c.CurrentHP+2);
                Emit(new BattleEvent(BattleEventKind.Heal,owner,target:c.InstanceID,amount:c.CurrentHP-before));
            }
        }
        void RemoveDeadRaw()
        {
            foreach(var p in State.Players)for(int i=0;i<rules.FieldSlots;i++)if(p.Slots[i]!=null && p.Slots[i].CurrentHP<=0) {
                var dead=p.Slots[i];Emit(new BattleEvent(BattleEventKind.Destroy,dead.Owner,target:dead.InstanceID,slot:i));p.Slots[i]=null;p.DiscardCards.Add(dead);
                int max=dead.MaxHP;dead.PokerAttack=dead.PokerHP=0;dead.PokerStraightAttackSpent=false;dead.CurrentHP+=dead.MaxHP-max;Log($"{dead.Definition.CardName} 파괴");
            }
        }
    }
}
