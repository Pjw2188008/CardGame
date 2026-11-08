using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
namespace Unknown.Battle
{
    public sealed class AiMove
    {
        public BattleAction Action;
        public int First,Second;
        public double Score;
        public string Reason;
        public AiMove(BattleAction action,int first=0,int second=0){Action=action;First=first;Second=second;}
        public bool Execute(BattleEngine engine,int actor) => BattleCommands.Execute(engine,actor,Action,First,Second);
    }
    public sealed class AiAgent
    {
        readonly IRandomSource random;
        public AiDifficulty Difficulty {get;}
        public IReadOnlyList<AiMove> LastCandidates {get;private set;}=new AiMove[0];
        public int SimulationNodes {get;private set;}
        public const int MaxCandidates=12,MaxNodes=64,MaxDepth=2;
        public AiAgent(AiDifficulty difficulty,int seed){Difficulty=difficulty;random=new SeededRandom(seed);}
        public AiMove Decide(AiObservation o)
        {
            SimulationNodes=0;var moves=Generate(o);foreach(var m in moves)Score(o,m,Difficulty);
            LastCandidates=moves.OrderByDescending(m=>m.Score).ToArray();
            if(moves.Count==0)return new AiMove(BattleAction.EndTurn){Reason="행동 없음"};
            var win=moves.FirstOrDefault(m=>m.Score>=100000);if(win!=null)return win;
            if(Difficulty==AiDifficulty.Easy)return Easy(o,moves);
            if(Difficulty==AiDifficulty.Normal || o.View.targets.Length>0)return LastCandidates[0];
            var watch=Stopwatch.StartNew();double before=Value(o.Simulation(0),o.Actor);AiMove best=LastCandidates[0];double bestScore=double.NegativeInfinity;
            foreach(var m in LastCandidates.Take(MaxCandidates)) {
                if(watch.ElapsedMilliseconds>=20 || SimulationNodes>=MaxNodes)break;
                if(m.Action==BattleAction.EndTurn){if(bestScore<0){best=m;bestScore=0;}continue;}
                double total=0;int branches=m.Action==BattleAction.Joker ? 2 : 1;
                for(int outcome=0;outcome<branches;outcome++) {
                    var g=o.Simulation(outcome);SimulationNodes++;if(!m.Execute(g,o.Actor)){total-=10000;continue;}ResolveTargets(g,o.Actor);
                    double immediate=Value(g,o.Actor)-before,next=0;
                    if(!g.State.IsOver && !g.NeedsTarget && watch.ElapsedMilliseconds<20) {
                        var nextView=AiObservation.Capture(g,o.Actor);var candidates=Generate(nextView);foreach(var c in candidates)Score(nextView,c,AiDifficulty.Normal);
                        foreach(var n in candidates.Where(c=>c.Action!=BattleAction.EndTurn).OrderByDescending(c=>c.Score).Take(3)) {
                            if(SimulationNodes>=MaxNodes || watch.ElapsedMilliseconds>=20)break;
                            // Reconstruct from the same filtered initial observation, then replay the two actions.
                            var child=o.Simulation(outcome);SimulationNodes++;m.Execute(child,o.Actor);ResolveTargets(child,o.Actor);
                            if(n.Execute(child,o.Actor)){ResolveTargets(child,o.Actor);next=Math.Max(next,Value(child,o.Actor)-Value(g,o.Actor));}
                        }
                    }
                    double score=immediate+next*.70+m.Score*.18;
                    double probability=branches==2 ? (outcome==0 ? Chance(o) : 1-Chance(o)) : 1;
                    total+=score*probability;
                }
                if(total>bestScore){bestScore=total;best=m;}
            }
            best.Reason+=" · 제한 탐색 "+SimulationNodes+"노드";return best;
        }
        static double Chance(AiObservation o) => Math.Max(0,Math.Min(1,(50+o.View.players[o.Actor].jokerChanceDelta)/100.0));
        public static List<AiMove> Generate(AiObservation o)
        {
            var s=o.View;int a=o.Actor;var p=s.players[a];var enemy=s.players[1-a];var moves=new List<AiMove>();
            if(s.winner>=0 || s.expired || s.active!=a)return moves;
            if(s.targets.Length>0){foreach(int t in s.targets)moves.Add(new AiMove(BattleAction.Target,t/p.board.Length,t%p.board.Length));return moves;}
            int empty=Array.FindIndex(p.board,c=>c==null || !c.occupied);
            foreach(var h in p.hand) {
                var d=CardDatabase.Get(h.definition);
                if(d.CardType!=CardType.Joker && d.ManaCost<=p.mana && empty>=0)moves.Add(new AiMove(BattleAction.Summon,h.id,empty));
                if(d.CardType==CardType.Joker && d.ManaCost<=p.mana)foreach(var c in p.board.Where(c=>c?.occupied==true && !c.jokerUsed))moves.Add(new AiMove(BattleAction.Joker,h.id,c.id));
            }
            foreach(var c in p.board.Where(c=>c?.occupied==true && !c.attacked && c.hp>0 && c.summonedTurn<s.turn)) {
                var targets=enemy.board.Where(c=>c?.occupied==true).ToArray();
                if(targets.Length==0)moves.Add(new AiMove(BattleAction.Attack,c.id,-1));else foreach(var t in targets)moves.Add(new AiMove(BattleAction.Attack,c.id,t.id));
            }
            if(p.unknownCreated<o.Rules.UnknownLimit)foreach(var ace in p.hand.Where(c=>CardDatabase.Get(c.definition).CardType==CardType.Normal && CardDatabase.Get(c.definition).Rank==Rank.Ace))
                foreach(var face in p.hand.Where(c=>CardDatabase.Get(c.definition).CardType==CardType.Normal && CardDatabase.Get(c.definition).Rank>=Rank.Jack && CardDatabase.Get(c.definition).Rank<=Rank.King))moves.Add(new AiMove(BattleAction.Combine,ace.id,face.id));
            if(!p.manualDiscardUsed && p.handCount>=7)foreach(var c in p.hand)moves.Add(new AiMove(BattleAction.Discard,c.id));
            moves.Add(new AiMove(BattleAction.EndTurn));return moves;
        }
        AiMove Easy(AiObservation o,List<AiMove> moves)
        {
            var choices=new List<AiMove>();
            foreach(var m in moves) {
                int weight=1;
                if(m.Action==BattleAction.Target)weight=5;
                if(m.Action==BattleAction.Summon)weight=10-Math.Min(8,CardDatabase.Get(o.View.players[o.Actor].hand.First(c=>c.id==m.First).definition).ManaCost);
                if(m.Action==BattleAction.Attack)weight=8;
                if(m.Action==BattleAction.Combine){if(random.Next(100)>=25)continue;weight=2;}
                if(m.Action==BattleAction.Joker){if(random.Next(100)>=20)continue;weight=2;}
                if(m.Action==BattleAction.Discard){if(m.Score<0 || random.Next(100)>=10)continue;weight=1;}
                for(int i=0;i<weight;i++)choices.Add(m);
            }
            var picked=choices[random.Next(choices.Count)];picked.Reason="초급 가중 무작위 · "+picked.Reason;return picked;
        }
        static double UnitValue(CardViewData c) => c==null || !c.occupied ? 0 : c.attack*1.35+c.hp*.75+(CardDatabase.Get(c.definition).CardType==CardType.Unknown ? 4 : 0);
        static void Score(AiObservation o,AiMove m,AiDifficulty level)
        {
            int a=o.Actor;var s=o.View;var p=s.players[a];var enemy=s.players[1-a];m.Score=0;m.Reason="턴 종료";
            var own=p.board.Where(c=>c?.occupied==true).ToArray();var foes=enemy.board.Where(c=>c?.occupied==true).ToArray();
            if(m.Action==BattleAction.Target) {
                var target=s.players[m.First].board[m.Second];var effect=o.Pending;
                m.Score=m.First==a ? UnitValue(target) : UnitValue(target)*.6;
                if(effect!=null) {if(effect.Kind==EffectKind.Heal)m.Score=Math.Min(effect.Amount,target.maxHP-target.hp)*4;
                    if(effect.Kind==EffectKind.Damage || effect.Kind==EffectKind.ChainDamage)m.Score+=target.hp<=effect.Amount ? UnitValue(target)+12 : Math.Min(target.hp,effect.Amount);
                    if(effect.Kind==EffectKind.ModifyAttack)m.Score+=Math.Min(Math.Abs(effect.Amount),target.attack)*2;}
                m.Reason="유효한 소환 효과 대상";return;
            }
            if(m.Action==BattleAction.Attack) {
                var c=own.First(x=>x.id==m.First);
                if(m.Second==-1){m.Score=c.attack>=enemy.hp ? 1000000 : c.attack*2+4;m.Reason=c.attack>=enemy.hp ? "직접 공격으로 즉시 승리" : "빈 필드 직접 공격";return;}
                var t=foes.First(x=>x.id==m.Second);bool kill=c.attack>=t.hp,die=t.attack>=c.hp;
                m.Score=Math.Min(c.attack,t.hp)*1.1+(kill ? UnitValue(t)+5 : 0)-(die ? UnitValue(c)+3 : Math.Min(t.attack,c.hp)*.6);
                if(kill) {var next=PokerSynergy.Evaluate(foes.Where(x=>x.id!=t.id).Select(x=>x.ToCard()));m.Score+=Math.Max(0,(int)enemy.poker.kind-(int)next.kind)*(level==AiDifficulty.Hard ? 6 : 2);
                    if(foes.Sum(x=>x.attack)>=p.hp)m.Score+=t.attack*2;}
                if(die)m.Score-=own.Length>1 && p.poker.members.Contains(c.id) ? 5 : 0;
                m.Reason=kill ? "위협 제거·교환·상대 시너지 비교" : "피해와 반격 손실 비교";return;
            }
            if(m.Action==BattleAction.EndTurn)return;
            var h=p.hand.First(x=>x.id==m.First);
            var d=CardDatabase.Get(h.definition);
            if(m.Action==BattleAction.Summon) {
                var cards=own.Select(c=>c.ToCard()).ToList();cards.Add(h.ToCard());var next=PokerSynergy.Evaluate(cards);
                double synergy=PokerStrength(next)-PokerStrength(p.poker);
                m.Score=d.Attack*1.2+d.MaxHP*.65-d.ManaCost*.9+synergy+2;
                foreach(var e in d.EffectList) {
                    if(e.Kind==EffectKind.DrawCard)m.Score+=Math.Min(e.Amount,8-p.handCount+1)*2;
                    else if(e.Kind==EffectKind.Damage || e.Kind==EffectKind.ChainDamage || e.Kind==EffectKind.RandomDamage)m.Score+=foes.Length==0 ? 0 : Math.Min(e.Amount,foes.Max(x=>x.hp))*1.8;
                    else if(e.Kind==EffectKind.SelfDamage)m.Score-=e.Amount*.6;
                    else m.Score+=Math.Abs(e.Amount)*.7;
                }
                var map=MapDefinition.For(s.map,o.Rules.MapRules);if(d.CardType==CardType.Normal && d.Suit==map.suit)m.Score+=map.summonAttack*1.5+map.summonHP+map.summonDraw*2;
                if(p.hand.Any(x=>x.id!=h.id && CardDatabase.Get(x.definition).ManaCost<=p.mana-d.ManaCost))m.Score+=2;
                if(level==AiDifficulty.Hard && cards.Count<5) {
                    double potential=PokerStrength(next);int futureBudget=p.mana-d.ManaCost+Math.Min(10,p.maxMana+1);
                    var future=p.hand.Where(x=>x.id!=h.id && CardDatabase.Get(x.definition).CardType==CardType.Normal).ToArray();
                    for(int i=0;i<future.Length;i++) {
                        var first=CardDatabase.Get(future[i].definition);if(first.ManaCost>futureBudget)continue;
                        var plan=cards.Concat(new[]{future[i].ToCard()}).ToArray();potential=Math.Max(potential,PokerStrength(PokerSynergy.Evaluate(plan))-first.ManaCost);
                        if(plan.Length>=5)continue;
                        for(int j=i+1;j<future.Length;j++) {var second=CardDatabase.Get(future[j].definition);if(first.ManaCost+second.ManaCost>futureBudget)continue;
                            potential=Math.Max(potential,PokerStrength(PokerSynergy.Evaluate(plan.Concat(new[]{future[j].ToCard()})))-first.ManaCost-second.ManaCost);}
                    }
                    m.Score+=Math.Max(0,potential-PokerStrength(next))*.35;
                }
                if(Material(p,h) && s.map!=BattleMap.JokerGame)m.Score-=p.unknownCreated>=1 ? 12 : 4;
                m.Reason=next.kind!=p.poker.kind ? "시너지 "+PokerSynergy.Name(next.kind)+" · 비용과 카드 효과" : "능력치·비용·효과·맵 효율";return;
            }
            if(m.Action==BattleAction.Combine){m.Score=s.map!=BattleMap.JokerGame && p.unknownCreated==2 ? 1000000 : s.map==BattleMap.JokerGame ? 4 : 19+p.unknownCreated*10;m.Reason=m.Score>=100000 ? "UNKNOWN 세 번째 생성으로 즉시 승리" : "UNKNOWN 전투 가치와 생성 승리 진행";return;}
            if(m.Action==BattleAction.Joker){var t=own.First(c=>c.id==m.Second);double chance=Chance(o);m.Score=chance*UnitValue(t)*.5-(1-chance)*UnitValue(t)+2;
                if(s.map==BattleMap.JokerGame)m.Score+=chance*(p.jokerSuccesses==2 ? 300 : 15+p.jokerSuccesses*12);
                if(t.summonedTurn<s.turn && !t.attacked && foes.Length==0)m.Score-=t.attack*1.5;
                m.Reason="도박 기대 가치·실패 손실·맵 승리 확률";return;}
            if(m.Action==BattleAction.Discard){m.Score=(p.handCount==8 ? 7 : 2)-(d.Attack+d.MaxHP*.5)*.5-(Material(p,h) ? 20 : 0);
                if(d.CardType==CardType.Joker)m.Score-=s.map==BattleMap.JokerGame ? 25 : 10;
                if(own.Any(c=>CardDatabase.Get(c.definition).Rank==d.Rank))m.Score-=8;if(d.ManaCost<=p.maxMana+1 && d.Attack>=6)m.Score-=4;
                m.Reason="손패 공간 확보 · 조합/시너지/다음 턴 핵심 카드 보존";}
        }
        static bool Material(PlayerViewData p,CardViewData h)
        {var d=CardDatabase.Get(h.definition);if(d.CardType!=CardType.Normal)return false;return d.Rank==Rank.Ace || d.Rank>=Rank.Jack && d.Rank<=Rank.King;}
        static double PokerStrength(PokerState poker) => poker.targets.Length*(PokerSynergy.Attack(poker.kind)*1.2+PokerSynergy.HP(poker.kind)*.65);
        static double Value(BattleEngine g,int a)
        {
            if(g.State.IsOver)return g.State.Winner==a ? 1000000 : -1000000;
            double Side(int owner) {var p=g.State.Players[owner];var board=p.Board.Where(c=>c!=null).ToArray();double v=p.HP*2+board.Sum(c=>c.Attack*1.4+c.CurrentHP*.85)+p.Hand.Count*1.4;
                v+=(int)p.Poker.kind*3;if(owner==a)v+=g.Config.Map==BattleMap.JokerGame ? p.JokerSuccesses*35 : p.UnknownCreated*28;
                if(owner==a && g.State.Players[1-a].Board.Where(c=>c!=null).Sum(c=>c.Attack)>=p.HP)v-=80;
                return v;}
            return Side(a)-Side(1-a);
        }
        internal static void ResolveTargets(BattleEngine g,int actor)
        {
            for(int n=0;n<20 && g.NeedsTarget && !g.State.IsOver;n++) {
                var o=AiObservation.Capture(g,actor);var targets=Generate(o);foreach(var t in targets)Score(o,t,AiDifficulty.Normal);
                var best=targets.OrderByDescending(t=>t.Score).FirstOrDefault();if(best==null || !best.Execute(g,actor))break;
            }
        }
    }
}
