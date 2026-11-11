using System.Linq;
namespace Unknown.Battle
{
    public sealed partial class BattleEngine
    {
        bool waitingExchange;
        internal int PendingSourceID=>source?.InstanceID ?? -1;
        internal int PendingIndex=>effectIndex;
        internal int[] PendingKinds=>selectedTargets.Keys.Select(k=>(int)k).ToArray();
        internal int[] PendingIDs=>selectedTargets.Values.ToArray();
        internal int[] PendingDistinct=>chainHits.ToArray();
        public bool NeedsExchange => viewOnly ? viewExchange : waitingExchange && source!=null;
        static bool IsDistinctEffect(CardEffect effect) => effect.Kind==EffectKind.ChainDamage || effect.Kind==EffectKind.ChainWeaken;
        public bool ChooseExchange(int actor,int cardID)
        {
            if(!Guard(actor,true) || !NeedsExchange)return false;
            if(cardID!=-1 && !State.Players[actor].HandCards.Any(c=>c.InstanceID==cardID))return Fail("남은 손패에서 버릴 카드를 선택하세요.");
            CompleteExchange(cardID);return true;
        }
        void CompleteExchange(int cardID)
        {
            var player=State.Players[source.Owner];var effect=source.Definition.EffectList[effectIndex];
            waitingExchange=false;
            if(cardID>=0) {
                var card=player.HandCards.First(c=>c.InstanceID==cardID);
                player.HandCards.Remove(card);player.DiscardCards.Add(card);
                Emit(new BattleEvent(BattleEventKind.Discard,source.Owner,source:source.InstanceID,target:cardID,secondary:3));
                int before=player.HandCards.Count;Draw(source.Owner,effect.Amount);
                MeasureBalanceEffect(effect,card,player.HandCards.Count-before,"resolved");
            } else MeasureBalanceEffect(effect,null,0,"cancelled");
            effectIndex++;ResolveEffects();
        }
        void ApplyAttackRecovery(CardInstance card)
        {
            if(State.IsOver)return;
            foreach(var effect in card.Definition.EffectList.Where(e=>e.Kind==EffectKind.FirstAttackHeal)) {
                if(!IsOnBoard(card)){MeasureAttackRecoverySkipped(card,effect);continue;}
                int before=card.CurrentHP,actual=System.Math.Min(effect.Amount,card.MaxHP-before);card.CurrentHP+=actual;
                Emit(new BattleEvent(BattleEventKind.Heal,card.Owner,source:card.InstanceID,target:card.InstanceID,amount:actual,secondary:1,suit:card.Definition.Suit));
                MeasureAttackRecovery(card,effect,before,actual);
            }
        }
        partial void MeasureAttackRecovery(CardInstance card,CardEffect effect,int before,int actual);
        partial void MeasureAttackRecoverySkipped(CardInstance card,CardEffect effect);
    }
}
