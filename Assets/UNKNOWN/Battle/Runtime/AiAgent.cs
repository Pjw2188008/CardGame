using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
namespace Unknown.Battle
{
    // Policy tuning only: never changes either player's cards, resources or rules.
    public sealed class AiSearchSettings
    {
        public int Candidates {get;}
        public int Continuations {get;}
        public int OwnSteps {get;}
        public int ReplySteps {get;}
        public int NodeBudget {get;}
        public AiSearchSettings(int candidates=12,int continuations=3,int ownSteps=4,int replySteps=5,int nodeBudget=384)
        {
            Candidates=Math.Max(1,Math.Min(16,candidates));Continuations=Math.Max(1,Math.Min(5,continuations));
            OwnSteps=Math.Max(1,Math.Min(6,ownSteps));ReplySteps=Math.Max(0,Math.Min(5,replySteps));
            NodeBudget=Math.Max(16,Math.Min(1024,nodeBudget));
        }
    }
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
        public int ReplyNodes {get;private set;}
        public AiSearchSettings Search {get;}
        public const int MaxCandidates=12,MaxNodes=384,MaxDepth=4;
        public AiAgent(AiDifficulty difficulty,int seed,AiSearchSettings search=null){Difficulty=difficulty;random=new SeededRandom(seed);Search=search??new AiSearchSettings();}
        public AiMove Decide(AiObservation o)
        {
            SimulationNodes=0;ReplyNodes=0;var moves=Generate(o);foreach(var m in moves)Score(o,m,Difficulty);
            LastCandidates=moves.OrderByDescending(m=>m.Score).ToArray();
            if(moves.Count==0)return new AiMove(BattleAction.EndTurn){Reason="행동 없음"};
            var win=moves.FirstOrDefault(m=>m.Score>=100000);if(win!=null)return win;
            if(Difficulty==AiDifficulty.Easy)return Easy(o,moves);
            if(Difficulty==AiDifficulty.Normal || o.View.targets.Length>0 || o.View.exchangePending)return LastCandidates[0];
            return Plan(o);
        }
        AiMove Plan(AiObservation o)
        {
            // Fixed work budget makes normal decisions independent of machine speed.
            // The wall-clock guard is an emergency escape, not a difficulty setting.
            var watch=Stopwatch.StartNew();double before=Value(o.Simulation(0),o.Actor);
            bool Available()=>SimulationNodes<Search.NodeBudget && watch.ElapsedMilliseconds<100;
            var known=new HashSet<int>(o.View.players[o.Actor].hand.Select(c=>c.id));
            var roots=LastCandidates.Where(c=>c.Action!=BattleAction.EndTurn).Take(Search.Candidates-1)
                .Concat(LastCandidates.Where(c=>c.Action==BattleAction.EndTurn)).ToArray();
            AiMove best=LastCandidates[0];double bestScore=double.NegativeInfinity;
            foreach(var root in roots) {
                int branches=root.Action==BattleAction.Joker ? 2 : 1;
                // Never compare a half-evaluated gamble against a full expectation.
                if(!Available() || Search.NodeBudget-SimulationNodes<branches)break;
                double total=0;bool complete=true;
                for(int outcome=0;outcome<branches;outcome++) {
                    if(!Available()){complete=false;break;}
                    var path=new List<AiMove>{root};var g=Replay(o,path,outcome);SimulationNodes++;
                    if(g==null){total-=1000000;continue;}
                    // Plan several affordable actions, including attack ordering and clearing blockers.
                    for(int depth=1;depth<Search.OwnSteps && !g.State.IsOver && g.State.ActivePlayer==o.Actor && root.Action!=BattleAction.EndTurn;depth++) {
                        var view=AiObservation.Capture(g,o.Actor);var candidates=Generate(view);
                        foreach(var c in candidates)Score(view,c,AiDifficulty.Hard);
                        double current=Value(g,o.Actor);BattleEngine next=null;AiMove chosen=null;
                        foreach(var c in candidates.Where(c=>c.Action!=BattleAction.EndTurn && c.Action!=BattleAction.Joker &&
                            (c.Action==BattleAction.Attack || c.Action==BattleAction.Target || known.Contains(c.First))).OrderByDescending(c=>c.Score).Take(Search.Continuations)) {
                            if(!Available())break;
                            path.Add(c);var child=Replay(o,path,outcome);path.RemoveAt(path.Count-1);SimulationNodes++;
                            if(child==null)continue;
                            double value=Value(child,o.Actor)+c.Score*.03;
                            if(value>current){current=value;next=child;chosen=c;}
                        }
                        if(chosen==null)break;path.Add(chosen);g=next;
                    }
                    double position=root.Action==BattleAction.EndTurn ? before : Value(g,o.Actor);
                    double publicBefore=root.Action==BattleAction.EndTurn ? PublicValue(o.Simulation(outcome),o.Actor) : PublicValue(g,o.Actor);
                    // Opponent's public-board best reply: unknown cards are never treated as known threats.
                    if(!g.State.IsOver && Search.ReplySteps>0) {
                        if(g.State.ActivePlayer==o.Actor)g.EndTurn(o.Actor);
                        for(int depth=0;depth<Search.ReplySteps && !g.State.IsOver && Available();depth++) {
                            var view=AiObservation.Capture(g,1-o.Actor);var attacks=Generate(view).Where(c=>c.Action==BattleAction.Attack).ToArray();
                            foreach(var attack in attacks)Score(view,attack,AiDifficulty.Normal);
                            BattleEngine worst=null;double worstValue=PublicValue(g,o.Actor);
                            foreach(var attack in attacks.OrderByDescending(c=>c.Score).Take(Search.Continuations)) {
                                if(!Available())break;
                                var reply=AiObservation.Capture(g,1-o.Actor).Simulation(outcome);SimulationNodes++;ReplyNodes++;
                                if(!attack.Execute(reply,1-o.Actor))continue;
                                // Evaluate only public board/HP here. Recapturing for the opponent masks our private counters.
                                double value=PublicValue(reply,o.Actor);
                                if(value<worstValue){worstValue=value;worst=reply;}
                            }
                            if(worst==null)break;g=worst;
                        }
                        if(g.State.IsOver)position=g.State.Winner==o.Actor ? 1000000 : -1000000;
                        else position+=.9*(PublicValue(g,o.Actor)-publicBefore);
                    }
                    double score=position-before+root.Score*.12;
                    total+=score*(branches==2 ? (outcome==0 ? Chance(o) : 1-Chance(o)) : 1);
                }
                if(complete && total>bestScore){bestScore=total;best=root;}
            }
            best.Reason+=" · 연속 행동/공개 반격 "+SimulationNodes+"노드 (반격 "+ReplyNodes+")";
            return best;
        }
        static BattleEngine Replay(AiObservation o,IEnumerable<AiMove> path,int outcome)
        {
            var g=o.Simulation(outcome);
            foreach(var move in path){if(!move.Execute(g,o.Actor))return null;ResolveTargets(g,o.Actor);if(g.State.IsOver)break;}
            return g;
        }
        static double PublicValue(BattleEngine g,int actor)
        {
            if(g.State.IsOver)return g.State.Winner==actor ? 1000000 : -1000000;
            double Side(int owner) {var p=g.State.Players[owner];return p.HP*2+p.Board.Where(c=>c!=null).Sum(c=>c.Attack*1.4+c.CurrentHP*.85)+(int)p.Poker.kind*3;}
            return Side(actor)-Side(1-actor);
        }
        static double Chance(AiObservation o) => Math.Max(0,Math.Min(1,(50+o.View.players[o.Actor].jokerChanceDelta)/100.0));
        public static List<AiMove> Generate(AiObservation o)
        {
            var s=o.View;int a=o.Actor;var p=s.players[a];var enemy=s.players[1-a];var moves=new List<AiMove>();
            if(s.winner>=0 || s.expired || s.active!=a)return moves;
            if(s.exchangePending){moves.Add(new AiMove(BattleAction.Exchange,-1));foreach(var card in p.hand)moves.Add(new AiMove(BattleAction.Exchange,card.id));return moves;}
            if(s.targets.Length>0){foreach(int t in s.targets)moves.Add(new AiMove(BattleAction.Target,t/p.board.Length,t%p.board.Length));return moves;}
            int empty=Array.FindIndex(p.board,c=>c==null || !c.occupied);
            foreach(var h in p.hand) {
                var d=h.Definition;
                if(d.CardType!=CardType.Joker && d.ManaCost<=p.mana && empty>=0)moves.Add(new AiMove(BattleAction.Summon,h.id,empty));
                if(d.CardType==CardType.Joker && d.ManaCost<=p.mana)foreach(var c in p.board.Where(c=>c?.occupied==true && !c.jokerUsed))moves.Add(new AiMove(BattleAction.Joker,h.id,c.id));
            }
            foreach(var c in p.board.Where(c=>c?.occupied==true && !c.attacked && c.hp>0 && c.summonedTurn<s.turn)) {
                var targets=enemy.board.Where(c=>c?.occupied==true).ToArray();
                if(targets.Length==0)moves.Add(new AiMove(BattleAction.Attack,c.id,-1));else foreach(var t in targets)moves.Add(new AiMove(BattleAction.Attack,c.id,t.id));
            }
            if(p.unknownProgress+p.hand.Count(c=>c.Definition.CardType==CardType.Unknown)<o.Rules.UnknownLimit)foreach(var ace in p.hand.Where(c=>c.Definition.CardType==CardType.Normal && c.Definition.Rank==Rank.Ace))
                foreach(var face in p.hand.Where(c=>c.Definition.CardType==CardType.Normal && c.Definition.Rank>=Rank.Jack && c.Definition.Rank<=Rank.King))moves.Add(new AiMove(BattleAction.Combine,ace.id,face.id));
            if(!p.manualDiscardUsed && p.handCount>=7)foreach(var c in p.hand)moves.Add(new AiMove(BattleAction.Discard,c.id));
            moves.Add(new AiMove(BattleAction.EndTurn));return moves;
        }
        AiMove Easy(AiObservation o,List<AiMove> moves)
        {
            var choices=new List<AiMove>();
            foreach(var m in moves) {
                int weight=1;
                if(m.Action==BattleAction.Target)weight=5;
                if(m.Action==BattleAction.Summon)weight=10-Math.Min(8,o.View.players[o.Actor].hand.First(c=>c.id==m.First).Definition.ManaCost);
                if(m.Action==BattleAction.Attack)weight=8;
                if(m.Action==BattleAction.Combine){if(random.Next(100)>=25)continue;weight=2;}
                if(m.Action==BattleAction.Joker){if(random.Next(100)>=20)continue;weight=2;}
                if(m.Action==BattleAction.Discard){if(m.Score<0 || random.Next(100)>=10)continue;weight=1;}
                for(int i=0;i<weight;i++)choices.Add(m);
            }
            var picked=choices[random.Next(choices.Count)];picked.Reason="초급 가중 무작위 · "+picked.Reason;return picked;
        }
        static double UnitValue(CardViewData c) => c==null || !c.occupied ? 0 : c.attack*1.35+c.hp*.75+(c.Definition.CardType==CardType.Unknown ? 4 : 0);
        static void Score(AiObservation o,AiMove m,AiDifficulty level)
        {
            int a=o.Actor;var s=o.View;var p=s.players[a];var enemy=s.players[1-a];m.Score=0;m.Reason="턴 종료";
            var own=p.board.Where(c=>c?.occupied==true).ToArray();var foes=enemy.board.Where(c=>c?.occupied==true).ToArray();
            if(m.Action==BattleAction.Exchange){m.Score=m.First<0 ? 0 : Math.Min(o.Pending?.Amount ?? 0,Math.Min(p.deckCount,9-p.handCount))*3-UnitValue(p.hand.First(c=>c.id==m.First))*.4;return;}
            if(m.Action==BattleAction.Target) {
                var target=s.players[m.First].board[m.Second];var effect=o.Pending;
                m.Score=m.First==a ? UnitValue(target) : UnitValue(target)*.6;
                if(effect!=null) {if(effect.Kind==EffectKind.Heal)m.Score=Math.Min(effect.Amount,target.maxHP-target.hp)*4;
                    if(effect.Kind==EffectKind.ChainWeaken)m.Score=Math.Min(effect.Amount,target.attack)*3;
                    if(effect.Kind==EffectKind.Damage || effect.Kind==EffectKind.ChainDamage)m.Score+=target.hp<=effect.Amount ? UnitValue(target)+12 : Math.Min(target.hp,effect.Amount);
                    if(effect.Kind==EffectKind.ModifyAttack)m.Score+=Math.Min(Math.Abs(effect.Amount),target.attack)*2;}
                m.Reason="유효한 소환 효과 대상";return;
            }
            if(m.Action==BattleAction.Attack) {
                var c=own.First(x=>x.id==m.First);
                if(m.Second==-1){m.Score=c.attack>=enemy.hp ? 1000000 : c.attack*2+4;m.Reason=c.attack>=enemy.hp ? "직접 공격으로 즉시 승리" : "빈 필드 직접 공격";return;}
                var t=foes.First(x=>x.id==m.Second);bool kill=c.attack>=t.hp,die=t.attack>=c.hp;
                m.Score=Math.Min(c.attack,t.hp)*1.1+(kill ? UnitValue(t)+5 : 0)-(die ? UnitValue(c)+3 : Math.Min(t.attack,c.hp)*.6);
                if(!c.firstAttackHealSpent && !die)m.Score+=Math.Min(c.Definition.EffectList.Where(e=>e.Kind==EffectKind.FirstAttackHeal).Sum(e=>e.Amount),c.maxHP-c.hp+t.attack)*1.5;
                if(kill) {var next=PokerSynergy.Evaluate(foes.Where(x=>x.id!=t.id).Select(x=>x.ToCard()));m.Score+=Math.Max(0,(int)enemy.poker.kind-(int)next.kind)*(level==AiDifficulty.Hard ? 6 : 2);
                    if(foes.Sum(x=>x.attack)>=p.hp)m.Score+=t.attack*2;}
                if(die)m.Score-=own.Length>1 && p.poker.members.Contains(c.id) ? 5 : 0;
                m.Reason=kill ? "위협 제거·교환·상대 시너지 비교" : "피해와 반격 손실 비교";return;
            }
            if(m.Action==BattleAction.EndTurn)return;
            var h=p.hand.First(x=>x.id==m.First);
            var d=h.Definition;
            if(m.Action==BattleAction.Summon) {
                if(d.CardType==CardType.Unknown && s.map!=BattleMap.JokerGame && p.unknownProgress==2){m.Score=1000000;m.Reason="UNKNOWN 필드 3장 배치로 승리";return;}
                var cards=own.Select(c=>c.ToCard()).ToList();cards.Add(h.ToCard());var next=PokerSynergy.Evaluate(cards);
                double synergy=PokerStrength(next)-PokerStrength(p.poker);
                m.Score=d.Attack*1.2+d.MaxHP*.65-d.ManaCost*.9+synergy+2;
                foreach(var e in d.EffectList) {
                    if(e.Kind==EffectKind.DrawCard)m.Score+=Math.Min(e.Amount,8-p.handCount+1)*2;
                    else if(e.Kind==EffectKind.GainMana)m.Score+=Math.Min(e.Amount,10-(p.mana-d.ManaCost))*2.5;
                    else if(e.Kind==EffectKind.Exchange)m.Score+=p.handCount>1 ? Math.Min(e.Amount,Math.Min(p.deckCount,9-p.handCount))*2-1 : 0;
                    else if(e.Kind==EffectKind.FirstAttackHeal)m.Score+=foes.Any(t=>t.attack<d.MaxHP && t.attack>0) ? Math.Min(e.Amount,foes.Max(t=>t.attack))*1.5 : 0;
                    else if(e.Kind==EffectKind.Damage || e.Kind==EffectKind.ChainDamage || e.Kind==EffectKind.RandomDamage)m.Score+=foes.Length==0 ? 0 : Math.Min(e.Amount,foes.Max(x=>x.hp))*1.8;
                    else if(e.Kind==EffectKind.SelfDamage)m.Score-=e.Amount*.6;
                    else m.Score+=Math.Abs(e.Amount)*.7;
                }
                var map=MapDefinition.For(s.map,o.Rules.MapRules);if(d.CardType==CardType.Normal && d.Suit==map.suit)m.Score+=map.summonAttack*1.5+map.summonHP+map.summonDraw*2;
                if(p.hand.Any(x=>x.id!=h.id && x.Definition.ManaCost<=p.mana-d.ManaCost))m.Score+=2;
                if(level==AiDifficulty.Hard && cards.Count<5) {
                    double potential=PokerStrength(next);int futureBudget=p.mana-d.ManaCost+Math.Min(10,p.maxMana+1);
                    var future=p.hand.Where(x=>x.id!=h.id && x.Definition.CardType==CardType.Normal).ToArray();
                    for(int i=0;i<future.Length;i++) {
                        var first=CardDatabase.Get(future[i].definition);if(first.ManaCost>futureBudget)continue;
                        var plan=cards.Concat(new[]{future[i].ToCard()}).ToArray();potential=Math.Max(potential,PokerStrength(PokerSynergy.Evaluate(plan))-first.ManaCost);
                        if(plan.Length>=5)continue;
                        for(int j=i+1;j<future.Length;j++) {var second=CardDatabase.Get(future[j].definition);if(first.ManaCost+second.ManaCost>futureBudget)continue;
                            potential=Math.Max(potential,PokerStrength(PokerSynergy.Evaluate(plan.Concat(new[]{future[j].ToCard()})))-first.ManaCost-second.ManaCost);}
                    }
                    m.Score+=Math.Max(0,potential-PokerStrength(next))*.35;
                }
                if(Material(p,h) && s.map!=BattleMap.JokerGame)m.Score-=p.unknownProgress>=1 ? 12 : 4;
                m.Reason=next.kind!=p.poker.kind ? "시너지 "+PokerSynergy.Name(next.kind)+" · 비용과 카드 효과" : "능력치·비용·효과·맵 효율";return;
            }
            if(m.Action==BattleAction.Combine){m.Score=s.map==BattleMap.JokerGame ? 4 : 19+p.unknownProgress*10;m.Reason="UNKNOWN 전투 가치와 필드 배치 승리 준비";return;}
            if(m.Action==BattleAction.Joker){var t=own.First(c=>c.id==m.Second);double chance=Chance(o);m.Score=chance*UnitValue(t)*.5-(1-chance)*UnitValue(t)+2;
                if(s.map==BattleMap.JokerGame)m.Score+=chance*(p.jokerSuccesses==2 ? 300 : 15+p.jokerSuccesses*12);
                if(t.summonedTurn<s.turn && !t.attacked && foes.Length==0)m.Score-=t.attack*1.5;
                m.Reason="도박 기대 가치·실패 손실·맵 승리 확률";return;}
            if(m.Action==BattleAction.Discard){m.Score=(p.handCount==8 ? 7 : 2)-(d.Attack+d.MaxHP*.5)*.5-(Material(p,h) ? 20 : 0);
                if(d.CardType==CardType.Joker)m.Score-=s.map==BattleMap.JokerGame ? 25 : 10;
                if(own.Any(c=>c.Definition.Rank==d.Rank))m.Score-=8;if(d.ManaCost<=p.maxMana+1 && d.Attack>=6)m.Score-=4;
                m.Reason="손패 공간 확보 · 조합/시너지/다음 턴 핵심 카드 보존";}
        }
        static bool Material(PlayerViewData p,CardViewData h)
        {var d=h.Definition;if(d.CardType!=CardType.Normal)return false;return d.Rank==Rank.Ace || d.Rank>=Rank.Jack && d.Rank<=Rank.King;}
        static double PokerStrength(PokerState poker) => poker.targets.Length*(PokerSynergy.Attack(poker.kind)*1.2+PokerSynergy.HP(poker.kind)*.65);
        static double Value(BattleEngine g,int a)
        {
            if(g.State.IsOver)return g.State.Winner==a ? 1000000 : -1000000;
            double Side(int owner) {var p=g.State.Players[owner];var board=p.Board.Where(c=>c!=null).ToArray();double v=p.HP*2+board.Sum(c=>c.Attack*1.4+c.CurrentHP*.85)+p.Hand.Count*1.4;
                v+=(int)p.Poker.kind*3;if(owner==a)v+=g.Config.Map==BattleMap.JokerGame ? p.JokerSuccesses*35 : p.UnknownProgress*28;
                // Direct damage is legal only after all blockers have been removed.
                if(owner==a && board.Length==0 && g.State.Players[1-a].Board.Where(c=>c!=null).Sum(c=>c.Attack)>=p.HP)v-=80;
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
