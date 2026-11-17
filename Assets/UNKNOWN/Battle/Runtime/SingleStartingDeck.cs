using System;
using System.Collections.Generic;
using System.Linq;

namespace Unknown.Battle
{
    public enum SingleDeckConcept { Attack, Survival, Weakening, Resource, Poker, Unknown }

    // Uses the live definitions and the campaign's existing unlocked pool; never grants unlocks.
    public static class SingleStartingDeck
    {
        public static List<string> Build(IEnumerable<string> available,int count,SingleDeckConcept concept)
        {
            var pool=available.Distinct().Select(id=>CardDatabase.Get(id)).ToList();
            if(count<1 || pool.Count<count)throw new InvalidOperationException("해금된 카드가 시작 덱 수량보다 적습니다.");
            var selected=new List<CardDefinition>();
            void Add(CardDefinition card){if(card!=null && selected.Count<count && !selected.Contains(card))selected.Add(card);}
            if(concept==SingleDeckConcept.Poker){
                // Same-rank groups support the actual pair/triple/four-of-a-kind rules.
                foreach(var group in pool.Where(c=>c.CardType==CardType.Normal).GroupBy(c=>c.Rank)
                    .OrderByDescending(g=>g.Count()).ThenBy(g=>g.Average(c=>c.ManaCost)).ThenBy(g=>(int)g.Key))
                    foreach(var card in group.OrderBy(c=>c.ManaCost).ThenBy(c=>c.CardID,StringComparer.Ordinal))Add(card);
            }else if(concept==SingleDeckConcept.Unknown){
                // The engine combines any normal Ace + J/Q/K, regardless of suit.
                var aces=pool.Where(c=>c.CardType==CardType.Normal && c.Rank==Rank.Ace).OrderBy(c=>c.ManaCost).ThenBy(c=>c.CardID,StringComparer.Ordinal).ToArray();
                var faces=pool.Where(c=>c.CardType==CardType.Normal && c.Rank>=Rank.Jack && c.Rank<=Rank.King).OrderBy(c=>c.ManaCost).ThenBy(c=>c.CardID,StringComparer.Ordinal).ToArray();
                int pairs=Math.Min(2,Math.Min(count/2,Math.Min(aces.Length,faces.Length)));
                for(int i=0;i<pairs;i++){Add(aces[i]);Add(faces[i]);}
            }
            // Keep at least two inexpensive options for opening turns where available.
            foreach(var card in pool.Where(c=>c.CardType==CardType.Normal && c.ManaCost<=2)
                .OrderByDescending(c=>Score(c,concept)).ThenBy(c=>c.CardID,StringComparer.Ordinal).Take(2))Add(card);
            foreach(var card in pool.OrderByDescending(c=>Score(c,concept)).ThenBy(c=>c.ManaCost).ThenBy(c=>c.CardID,StringComparer.Ordinal))Add(card);
            return selected.Select(c=>c.CardID).ToList();
        }
        static double Score(CardDefinition card,SingleDeckConcept concept)
        {
            double score=-card.ManaCost*.8+card.Attack*.15+card.MaxHP*.08;
            foreach(var e in card.EffectList){
                bool match=false;
                switch(concept){
                    case SingleDeckConcept.Attack:
                        match=new[]{EffectKind.Damage,EffectKind.ChainDamage,EffectKind.RandomDamage,EffectKind.StrikeBonus,EffectKind.IgnoreProtection,EffectKind.KillHero,EffectKind.KillGrow,EffectKind.WeakStrike,EffectKind.FullStrike,EffectKind.WoundedDamage,EffectKind.WeakDamage,EffectKind.DamageGrow,EffectKind.DamageHero,EffectKind.DamageTeamGrow,EffectKind.WeakBonusDamage,EffectKind.DiamondBonusDamage,EffectKind.WeakenThenDamage,EffectKind.PriorWeakDamage}.Contains(e.Kind) || (e.Kind==EffectKind.ModifyAttack || e.Kind==EffectKind.TemporaryAttack) && e.Amount>0;break;
                    case SingleDeckConcept.Survival:
                        match=new[]{EffectKind.Heal,EffectKind.IncreaseMaxHP,EffectKind.FirstAttackHeal,EffectKind.TurnHealSelf,EffectKind.Protection,EffectKind.HealProtect,EffectKind.HealFullAttack}.Contains(e.Kind);break;
                    case SingleDeckConcept.Weakening:
                        match=new[]{EffectKind.CardWeaken,EffectKind.HealthWeaken,EffectKind.AttackWeaken,EffectKind.ChainWeaken,EffectKind.WeakenThenDamage,EffectKind.PriorWeakDamage,EffectKind.WeakDamage,EffectKind.WeakStrike,EffectKind.WeakBonusDamage}.Contains(e.Kind) || e.Kind==EffectKind.ModifyAttack && e.Amount<0;break;
                    case SingleDeckConcept.Resource:case SingleDeckConcept.Unknown:
                        match=new[]{EffectKind.DrawCard,EffectKind.GainMana,EffectKind.Exchange,EffectKind.TacticalExchange,EffectKind.Discount}.Contains(e.Kind);break;
                }
                if(match)score+=8+Math.Min(4,Math.Abs(e.Amount))*.5;
            }
            return score;
        }
    }
}
