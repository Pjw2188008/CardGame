#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace Unknown.Battle
{
    public static class DiamondBuffChecks
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
            int changedAmounts = 0;
            foreach (var before in CardDatabase.AllFor(CardBalanceVersion.Revised))
            {
                var after = CardDatabase.Get(before.CardID, CardBalanceVersion.DiamondBuff);
                Check(before.CardName == after.CardName && before.Suit == after.Suit
                    && before.Rank == after.Rank && before.CardType == after.CardType
                    && before.ManaCost == after.ManaCost && before.Attack == after.Attack
                    && before.MaxHP == after.MaxHP && before.EffectList.Count == after.EffectList.Count,
                    "Unchanged identity, cost, body and effect count " + before.CardID);
                for (int i = 0; i < before.EffectList.Count; i++)
                {
                    var a = before.EffectList[i];
                    var b = after.EffectList[i];
                    int expected = a.Amount;
                    if (before.CardID == "Diamond_4") expected = 4;
                    if (before.CardID == "Diamond_7") expected = 3;
                    if (before.CardID == "Diamond_10") expected = 6;
                    if (a.Amount != b.Amount) changedAmounts++;
                    Check(a.Kind == b.Kind && a.Target == b.Target && a.FreshTarget == b.FreshTarget
                        && b.Amount == expected, "Exact effect amount and target rule " + before.CardID + "/" + i);
                }
                if (before.CardID != "Diamond_4" && before.CardID != "Diamond_7" && before.CardID != "Diamond_10")
                    Check(before.Description == after.Description, "Preserved description " + before.CardID);
            }
            Check(changedAmounts == 3, "Exactly three changed amounts");
            Check(CardDatabase.Get("Heart_Q", CardBalanceVersion.DiamondBuff).EffectList[0].Amount == 3
                && CardDatabase.Get("Heart_K", CardBalanceVersion.DiamondBuff).EffectList[1].Amount == 5,
                "DiamondBuff uses original Revised hearts");
            Check(CardDatabase.Get("Diamond_4").Version == CardBalanceVersion.DiamondBuff
                && new BattleConfig().CardVersion == CardBalanceVersion.DiamondBuff,
                "Normal entry points select DiamondBuff");
            Check(CardDatabase.Get("Diamond_4", CardBalanceVersion.Revised).EffectList[0].Amount == 3
                && CardDatabase.Get("Diamond_7", CardBalanceVersion.Revised).EffectList[0].Amount == 2
                && CardDatabase.Get("Diamond_10", CardBalanceVersion.Revised).EffectList[0].Amount == 5,
                "Original Revised damage is preserved");
            CheckTargetRules();
            return passed.ToArray();
        }

        static BattleEngine Fixture()
        {
            return new BattleEngine(421, new BattleConfig {
                StartingHand = 0, StartingMana = 10, EnablePoker = false,
                CardVersion = CardBalanceVersion.DiamondBuff, Map = BattleMap.Normal
            });
        }

        static CardInstance Enemy(BattleEngine engine, int slot, int hp = 10)
        {
            var card = engine.NewCard(CardDatabase.Get("Heart_A", CardBalanceVersion.DiamondBuff), 1);
            card.CurrentHP = hp;
            engine.State.Players[1].Slots[slot] = card;
            return card;
        }

        static CardInstance Summon(BattleEngine engine, string id)
        {
            var card = engine.NewCard(CardDatabase.Get(id, CardBalanceVersion.DiamondBuff), 0);
            engine.State.Players[0].HandCards.Add(card);
            int before = engine.State.Players[0].CurrentMana;
            Check(engine.Summon(0, card.InstanceID, 0), "Summon " + id);
            Check(engine.State.Players[0].CurrentMana == before - card.Definition.ManaCost,
                "Unchanged actual summon cost " + id);
            return card;
        }

        static void CheckTargetRules()
        {
            var game = Fixture();
            var enemy = Enemy(game, 0);
            Summon(game, "Diamond_4");
            Check(enemy.CurrentHP == 6 && !game.NeedsTarget, "Random damage four resolves once");
            game = Fixture();
            Summon(game, "Diamond_4");
            Check(!game.NeedsTarget, "Random damage with no enemy completes");

            game = Fixture();
            Summon(game, "Diamond_7");
            Check(!game.NeedsTarget, "Chain damage with no enemy completes");
            game = Fixture();
            enemy = Enemy(game, 0);
            Summon(game, "Diamond_7");
            Check(game.SelectEffectTarget(0, 1, 0) && enemy.CurrentHP == 7 && !game.NeedsTarget,
                "One enemy takes three once without repetition");
            game = Fixture();
            var first = Enemy(game, 0);
            var second = Enemy(game, 1);
            var third = Enemy(game, 2);
            Summon(game, "Diamond_7");
            Check(game.SelectEffectTarget(0, 1, 0) && game.NeedsTarget,
                "First distinct target takes three and awaits another");
            Check(!game.SelectEffectTarget(0, 1, 0) && first.CurrentHP == 7,
                "Same living target cannot be selected twice");
            Check(game.SelectEffectTarget(0, 1, 1) && first.CurrentHP == 7 && second.CurrentHP == 7
                && third.CurrentHP == 10 && !game.NeedsTarget,
                "Two distinct enemies take three each and the third is untouched");
            game = Fixture();
            Enemy(game, 0, 2);
            Summon(game, "Diamond_7");
            Check(game.SelectEffectTarget(0, 1, 0) && !game.NeedsTarget
                && game.State.Players[1].Board[0] == null, "Single lethal chain target completes");

            game = Fixture();
            enemy = Enemy(game, 0);
            Summon(game, "Diamond_10");
            Check(game.SelectEffectTarget(0, 1, 0) && enemy.CurrentHP == 4,
                "Selected enemy takes six");
            game = Fixture();
            Enemy(game, 0, 5);
            Summon(game, "Diamond_10");
            Check(game.SelectEffectTarget(0, 1, 0) && game.State.Players[1].Board[0] == null
                && game.DrainEvents().Any(e => e.Kind == BattleEventKind.Damage && e.Amount == 5),
                "Overkill records actual remaining HP rather than requested six");
        }
    }
}
#endif
