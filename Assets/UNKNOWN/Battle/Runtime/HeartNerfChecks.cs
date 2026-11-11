#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace Unknown.Battle
{
    public static class HeartNerfChecks
    {
        static readonly List<string> passed = new List<string>();
        static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            passed.Add(name);
        }
        public static string[] Run()
        {
            passed.Clear();
            CheckCatalog();
            CheckEffects();
            return passed.ToArray();
        }
        static void CheckCatalog()
        {
            foreach (var before in CardDatabase.AllFor(CardBalanceVersion.Revised))
            {
                var after = CardDatabase.Get(before.CardID, CardBalanceVersion.HeartNerf);
                Check(before.CardName == after.CardName && before.CardType == after.CardType
                    && before.Suit == after.Suit && before.Rank == after.Rank
                    && before.ManaCost == after.ManaCost && before.Attack == after.Attack
                    && before.MaxHP == after.MaxHP && before.EffectList.Count == after.EffectList.Count,
                    "Unchanged identity, mana, body and effect count " + before.CardID);
                for (int i = 0; i < before.EffectList.Count; i++)
                {
                    var a = before.EffectList[i];
                    var b = after.EffectList[i];
                    int expected = a.Amount;
                    if (before.CardID == "Heart_9" || before.CardID == "Heart_Q") expected = 2;
                    if (before.CardID == "Heart_K") expected = 3;
                    Check(a.Kind == b.Kind && a.Target == b.Target && a.FreshTarget == b.FreshTarget
                        && b.Amount == expected, "Exact effect and amount " + before.CardID + "/" + i);
                }
                if (before.CardID != "Heart_9" && before.CardID != "Heart_Q" && before.CardID != "Heart_K")
                    Check(before.Description == after.Description, "Unchanged description " + before.CardID);
            }
            Check(CardDatabase.Get("Heart_9", CardBalanceVersion.HeartNerf).Version == CardBalanceVersion.HeartNerf
                && CardDatabase.Get("Heart_Q", CardBalanceVersion.HeartNerf).EffectList[0].Amount == 2,
                "HeartNerf remains available independently of the default version");
            Check(CardDatabase.Get("Heart_9", CardBalanceVersion.Revised).EffectList[0].Amount == 3
                && CardDatabase.Get("Heart_Q", CardBalanceVersion.Revised).EffectList[0].Amount == 3
                && CardDatabase.Get("Heart_K", CardBalanceVersion.Revised).EffectList[1].Amount == 5,
                "Original Revised remains available");
        }

        static BattleEngine Fixture()
        {
            return new BattleEngine(421, new BattleConfig {
                StartingHand = 0, StartingMana = 10, EnablePoker = false,
                CardVersion = CardBalanceVersion.HeartNerf, Map = BattleMap.Normal
            });
        }

        static CardInstance Unit(BattleEngine engine, string id, int owner = 0, int slot = 0)
        {
            var card = engine.NewCard(CardDatabase.Get(id, CardBalanceVersion.HeartNerf), owner);
            engine.State.Players[owner].Slots[slot] = card;
            return card;
        }

        static void CheckEffects()
        {
            foreach (string id in new[] { "Heart_9", "Heart_Q", "Heart_K" })
            {
                var engine = Fixture();
                var ally = Unit(engine, "Heart_A");
                ally.CurrentHP = 4;
                var card = engine.NewCard(CardDatabase.Get(id, CardBalanceVersion.HeartNerf), 0);
                engine.State.Players[0].HandCards.Add(card);
                int oldMax = ally.MaxHP;
                Check(engine.Summon(0, card.InstanceID, 1), "Summon " + id);
                Check(ally.CurrentHP == (id == "Heart_Q" ? 8 : id == "Heart_9" ? 6 : 7),
                    "Exact wounded ally current HP " + id);
                Check(ally.MaxHP == oldMax + (id == "Heart_Q" ? 2 : 0), "Exact ally maximum HP " + id);
                if (id == "Heart_Q") Check(card.MaxHP == 12 && card.CurrentHP == 12,
                    "Queen includes summoned body and adds maximum/current HP together");
            }
            var game = Fixture();
            var king = Unit(game, "Heart_K");
            king.CurrentHP = 4;
            Check(game.Attack(0, king.InstanceID) && king.CurrentHP == 7 && king.FirstAttackHealSpent,
                "King first surviving active attack heals three");
            king.HasAttacked = false;
            Check(game.Attack(0, king.InstanceID) && king.CurrentHP == 7,
                "King second active attack never heals again");
            game = Fixture();
            king = Unit(game, "Heart_K");
            king.CurrentHP = 1;
            Unit(game, "Spade_2", 1);
            Check(game.Attack(0, king.InstanceID, 0) && game.State.Players[0].Board[0] == null,
                "King counter death prevents recovery");
            game = Fixture();
            king = Unit(game, "Heart_K");
            Check(game.Attack(0, king.InstanceID) && king.CurrentHP == king.MaxHP && king.FirstAttackHealSpent,
                "Full-health king consumes its first-attack recovery");
        }

    }
}
#endif
