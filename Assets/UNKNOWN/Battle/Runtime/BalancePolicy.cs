#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
namespace Unknown.Battle
{
    public enum BalanceAI { Random, Aggressive, Control, Effect }
    public sealed class BalancePolicy
    {
        readonly SeededRandom random;
        public int RandomCalls=>random.Calls;
        public long RandomFingerprint=>random.Fingerprint;
        readonly BalanceAI style;
        public BalancePolicy(BalanceAI style,int seed){this.style=style;random=new SeededRandom(seed);}
        public static List<AiMove> LegalMoves(AiObservation observation)
        {
            var moves=AiAgent.Generate(observation);
            if(observation.View.exchangePending || observation.View.targets.Length>0 || moves.Count==0)return moves;
            var player=observation.View.players[observation.Actor];
            // Existing policy prunes equivalent summon positions and early discards.
            // The balance tester expands these choices without changing legality.
            var summons=moves.Where(m=>m.Action==BattleAction.Summon).ToArray();
            foreach(var move in summons)
                for(int slot=0;slot<player.board.Length;slot++)
                    if(slot!=move.Second && player.board[slot]?.occupied!=true)moves.Add(new AiMove(BattleAction.Summon,move.First,slot));
            if(!player.manualDiscardUsed && player.handCount<7)
                foreach(var card in player.hand)moves.Add(new AiMove(BattleAction.Discard,card.id));
            return moves;
        }
        public AiMove Choose(AiObservation observation,List<AiMove> moves)
        {
            if(moves.Count==0)throw new InvalidOperationException("No legal action for active player.");
            if(style==BalanceAI.Random)return moves[random.Next(moves.Count)];
            double best=double.NegativeInfinity;AiMove selected=null;int ties=0;
            foreach(var move in moves) {
                double score=Score(observation,move);
                if(score>best+1e-8){best=score;selected=move;ties=1;}
                else if(Math.Abs(score-best)<1e-8 && random.Next(++ties)==0)selected=move;
            }
            return selected;
        }
        static double Value(CardViewData card) => card==null || !card.occupied ? 0 : card.attack*1.5+card.hp;
        double Score(AiObservation observation,AiMove move)
        {
            var view=observation.View;int actor=observation.Actor;var own=view.players[actor];var enemy=view.players[1-actor];
            var allies=own.board.Where(c=>c?.occupied==true).ToArray();var foes=enemy.board.Where(c=>c?.occupied==true).ToArray();
            bool control=style==BalanceAI.Control,effects=style==BalanceAI.Effect;
            if(move.Action==BattleAction.EndTurn)return -1;
            if(move.Action==BattleAction.Exchange) {
                if(move.First<0)return 0;
                var material=own.hand.First(c=>c.id==move.First);
                return Math.Min(observation.Pending?.Amount ?? 0,Math.Min(own.deckCount,9-own.handCount))*3-Value(material)*.5;
            }
            if(move.Action==BattleAction.Target) {
                var target=view.players[move.First].board[move.Second];var effect=observation.Pending;
                if(effect==null)return Value(target);
                if(effect.Kind==EffectKind.ChainWeaken)return Math.Min(effect.Amount,target.attack)*3;
                if(effect.Kind==EffectKind.Heal)return Math.Min(effect.Amount,target.maxHP-target.hp)*(control ? 5 : 3);
                if(effect.Kind==EffectKind.ModifyAttack)return effect.Amount>0 ? effect.Amount*(target.attacked ? 1 : 3) : Math.Min(-effect.Amount,target.attack)*3;
                if(effect.Kind==EffectKind.IncreaseMaxHP)return effect.Amount*(target.hp<4 ? 4 : 2);
                return Math.Min(target.hp,effect.Amount)*2+(target.hp<=effect.Amount ? Value(target)+6 : 0);
            }
            if(move.Action==BattleAction.Attack) {
                var source=allies.First(c=>c.id==move.First);
                if(move.Second==-1)return source.attack>=enemy.hp ? 100000 : source.attack*(control ? 2 : 4)+5;
                var target=foes.First(c=>c.id==move.Second);
                double gain=Math.Min(target.hp,source.attack)*(control ? 1.5 : 2.5)+(target.hp<=source.attack ? Value(target)+7 : 0);
                double loss=target.attack>=source.hp ? Value(source)+4 : Math.Min(source.hp,target.attack)*.8;
                double recovery=!source.firstAttackHealSpent && source.hp>target.attack ? Math.Min(source.Definition.EffectList.Where(e=>e.Kind==EffectKind.FirstAttackHeal).Sum(e=>e.Amount),source.maxHP-source.hp+target.attack) : 0;
                return gain-loss*(control ? 1.3 : .8)+(control && target.hp<=source.attack ? target.attack : 0)+recovery*(control ? 2 : 1);
            }
            var hand=own.hand.First(c=>c.id==move.First);var definition=hand.Definition;
            if(move.Action==BattleAction.Discard)return own.handCount>=7 ? 1-Value(hand)*.4 : -10;
            if(move.Action==BattleAction.Combine)return own.unknownProgress>=1 ? 30 : effects ? 17 : 12;
            if(move.Action==BattleAction.Joker) {
                var target=allies.First(c=>c.id==move.Second);double chance=(50+own.jokerChanceDelta)/100.0;
                return chance*Value(target)*.5-(1-chance)*Value(target)+(view.map==BattleMap.JokerGame ? 15+own.jokerSuccesses*20 : effects ? 6 : 0);
            }
            double score=definition.Attack*(control ? 1 : 1.8)+definition.MaxHP*(control ? 1.3 : .6)-definition.ManaCost+2;
            if(definition.CardType==CardType.Unknown && view.map!=BattleMap.JokerGame && own.unknownProgress==2)return 100000;
            double ability=0;
            foreach(var effect in definition.EffectList) {
                var targets=effect.Target==EffectTarget.AllEnemies || effect.Target==EffectTarget.Enemy ? foes : allies.Concat(new[]{hand}).ToArray();
                double count=effect.Target==EffectTarget.AllEnemies || effect.Target==EffectTarget.AllAllies ? targets.Length : Math.Min(1,targets.Length);
                switch(effect.Kind) {
                    case EffectKind.Damage:case EffectKind.RandomDamage:case EffectKind.ChainDamage:
                        ability+=foes.Length==0 ? 0 : Math.Min(effect.Amount,foes.Max(c=>c.hp))*(effect.Kind==EffectKind.ChainDamage ? Math.Min(2,foes.Length) : count)*2;break;
                    case EffectKind.SelfDamage:ability-=effect.Amount;break;
                    case EffectKind.Heal:ability+=targets.Length==0 ? 0 : Math.Min(effect.Amount,targets.Max(c=>c.maxHP-c.hp))*count*(control ? 3 : 1);break;
                    case EffectKind.ModifyAttack:ability+=Math.Abs(effect.Amount)*count*(effect.Amount<0 ? 2 : 1.5);break;
                    case EffectKind.IncreaseMaxHP:ability+=effect.Amount*count*(control ? 2 : 1);break;
                    case EffectKind.DrawCard:ability+=Math.Min(effect.Amount,Math.Min(own.deckCount,8-own.handCount+1))*(control ? 4 : 2);break;
                    case EffectKind.DiscardCard:ability+=Math.Min(effect.Amount,enemy.handCount)*2;break;
                    case EffectKind.GainMana:ability+=Math.Min(effect.Amount,10-(own.mana-definition.ManaCost))*2;break;
                    case EffectKind.Exchange:ability+=own.handCount>1 ? Math.Min(effect.Amount,Math.Min(own.deckCount,9-own.handCount))*2-1 : 0;break;
                    case EffectKind.FirstAttackHeal:ability+=foes.Any(t=>t.attack<definition.MaxHP && t.attack>0) ? Math.Min(effect.Amount,foes.Max(t=>t.attack))*(control ? 3 : 1.5) : 0;break;
                    case EffectKind.ChainWeaken:ability+=Math.Min(2,foes.Length)*effect.Amount*2;break;
                }
            }
            var map=MapDefinition.For(view.map,observation.Rules.MapRules);
            if(definition.CardType==CardType.Normal && definition.Suit==map.suit)score+=map.summonAttack*1.5+map.summonHP+map.summonDraw*2;
            return score+ability*(effects ? 3 : 1);
        }
    }
}
#endif
