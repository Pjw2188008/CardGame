#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace Unknown.Battle
{
    public static class DiamondHeartReworkChecks
    {
        static readonly List<string> passed = new List<string>();
        static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            passed.Add(name);
        }
        static string Effects(CardDefinition card) => string.Join("|", card.EffectList.Select(
            e => e.Kind + ":" + e.Amount + ":" + e.Target + ":" + e.FreshTarget));

        public static string[] Run()
        {
            passed.Clear();
            string[] ranks = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A" };
            string[] diamond = {
                "Damage:2:Enemy:False", "SelfDamage:1:Self:False|Damage:3:Enemy:False",
                "RandomDamage:4:Enemy:False", "Damage:3:Enemy:False",
                "SelfDamage:1:Self:False|Damage:5:Enemy:False", "ChainDamage:3:Enemy:False",
                "RandomDamage:5:Enemy:False", "Damage:5:Enemy:False|ModifyAttack:1:Self:False",
                "Damage:7:Enemy:False", "SelfDamage:2:Self:False|Damage:8:Enemy:False",
                "ChainDamage:5:Enemy:False", "Damage:9:Enemy:False|ModifyAttack:2:Self:False",
                "ModifyAttack:2:AllAllies:False|Damage:4:Enemy:False"
            };
            string[] heart = {
                "Heal:2:Ally:False", "IncreaseMaxHP:1:Self:False", "FirstAttackHeal:2:Self:False",
                "IncreaseMaxHP:2:Ally:False", "Heal:1:AllAllies:False", "FirstAttackHeal:3:Self:False",
                "IncreaseMaxHP:3:Ally:False", "Heal:2:AllAllies:False", "FirstAttackHeal:4:Self:False",
                "IncreaseMaxHP:3:Ally:False|Heal:2:Ally:False", "IncreaseMaxHP:2:AllAllies:False",
                "Heal:3:AllAllies:False|FirstAttackHeal:2:Self:False",
                "IncreaseMaxHP:2:AllAllies:False|Heal:2:AllAllies:False"
            };
            var versions = new[] { CardBalanceVersion.DiamondHeartRework, CardBalanceVersion.DiamondReworkOnly, CardBalanceVersion.HeartReworkOnly };
            foreach (var version in versions)
            {
                foreach (var before in CardDatabase.AllFor(CardBalanceVersion.Revised))
                {
                    var after = CardDatabase.Get(before.CardID, version);
                    Check(before.ManaCost == after.ManaCost && before.Attack == after.Attack
                        && before.MaxHP == after.MaxHP && before.CardType == after.CardType
                        && before.Suit == after.Suit && before.Rank == after.Rank,
                        "Preserved body/cost/identity " + version + "/" + before.CardID);
                    bool changedSuit = before.Suit == Suit.Diamond && version != CardBalanceVersion.HeartReworkOnly
                        || before.Suit == Suit.Heart && version != CardBalanceVersion.DiamondReworkOnly;
                    if (!changedSuit)
                        Check(Effects(before) == Effects(after) && before.Description == after.Description,
                            "Untouched effect/text " + version + "/" + before.CardID);
                }
                for (int i = 0; i < ranks.Length; i++)
                {
                    if (version != CardBalanceVersion.HeartReworkOnly)
                        Check(Effects(CardDatabase.Get("Diamond_" + ranks[i], version)) == diamond[i],
                            "Independent diamond specification " + version + "/" + ranks[i]);
                    if (version != CardBalanceVersion.DiamondReworkOnly)
                        Check(Effects(CardDatabase.Get("Heart_" + ranks[i], version)) == heart[i],
                            "Independent heart specification " + version + "/" + ranks[i]);
                }
            }
            Check(CardDatabase.Get("Heart_7").EffectList.Count == 1 && CardDatabase.Get("Heart_7").EffectList[0].Amount == 3
                && new BattleConfig().CardVersion == CardBalanceVersion.DiamondHeartRework, "Actual default is the full rework");
            CheckEffects();
            return passed.ToArray();
        }

        static BattleEngine Fixture()
        {
            return new BattleEngine(421, new BattleConfig {
                StartingHand = 0, StartingMana = 10, EnablePoker = false,
                CardVersion = CardBalanceVersion.DiamondHeartRework, Map = BattleMap.Normal
            });
        }
        static CardInstance Unit(BattleEngine game, string id, int owner = 0, int slot = 0)
        {
            var card = game.NewCard(CardDatabase.Get(id, game.Config.CardVersion), owner);
            game.State.Players[owner].Slots[slot] = card;
            return card;
        }
        static CardInstance Summon(BattleEngine game, string id, int slot = 0)
        {
            var card = game.NewCard(CardDatabase.Get(id, game.Config.CardVersion), 0);
            game.State.Players[0].HandCards.Add(card);
            int mana = game.State.Players[0].CurrentMana;
            Check(game.Summon(0, card.InstanceID, slot)
                && game.State.Players[0].CurrentMana == mana - card.Definition.ManaCost, "Summon/pay once " + id);
            return card;
        }
        static void CheckEffects()
        {
            var game = Fixture();
            var ally = Unit(game, "Heart_A");
            ally.CurrentHP = 4;
            var jack = Summon(game, "Heart_J", 1);
            Check(game.SelectEffectTarget(0, 0, 0) && !game.NeedsTarget
                && ally.MaxHP == 13 && ally.CurrentHP == 9 && jack.CurrentHP == jack.MaxHP,
                "Heart J increases maximum/current HP then heals the same selected ally");
            game = Fixture();
            ally = Unit(game, "Heart_A"); ally.CurrentHP = 4;
            var queen = Summon(game, "Heart_Q", 1);
            Check(ally.MaxHP == 12 && ally.CurrentHP == 6 && queen.CurrentHP == 12
                && !game.DrainEvents().Any(e => e.Kind == BattleEventKind.Heal), "Heart Q grants HP without healing");
            game = Fixture(); ally = Unit(game, "Heart_A"); ally.CurrentHP = 4;
            Summon(game, "Heart_A", 1);
            Check(ally.MaxHP == 12 && ally.CurrentHP == 8, "Heart A increases HP before recovery two");
            foreach (var id in new[] { "Heart_4", "Heart_7", "Heart_10", "Heart_K" })
            {
                int heal = id == "Heart_7" ? 3 : id == "Heart_10" ? 4 : 2;
                game = Fixture(); ally = Unit(game, id); ally.CurrentHP = 3;
                Check(game.Attack(0, ally.InstanceID) && ally.CurrentHP == 3 + heal && ally.FirstAttackHealSpent,
                    "First active surviving attack recovers exact amount " + id);
                ally.HasAttacked = false;
                Check(game.Attack(0, ally.InstanceID) && ally.CurrentHP == 3 + heal, "No second recovery " + id);
                game = Fixture(); ally = Unit(game, id); ally.CurrentHP = 1;
                Unit(game, "Spade_2", 1);
                Check(game.Attack(0, ally.InstanceID, 0) && game.State.Players[0].Board[0] == null,
                    "Counter death prevents recovery " + id);
                game = Fixture(); ally = Unit(game, id);
                Check(game.Attack(0, ally.InstanceID) && ally.CurrentHP == ally.MaxHP && ally.FirstAttackHealSpent,
                    "Full health consumes once " + id);
                game = Fixture(); ally = Unit(game, id); var attacker = Unit(game, "Spade_2", 1);
                game.State.ActivePlayer = 1;
                Check(game.Attack(1, attacker.InstanceID, 0) && !ally.FirstAttackHealSpent,
                    "Defending never consumes first active recovery " + id);
            }
            foreach (string id in new[] { "Heart_7", "Heart_10" })
            {
                game = Fixture(); ally = Unit(game, "Heart_A"); ally.CurrentHP = 1;
                Summon(game, id, 1);
                Check(ally.CurrentHP == 1 && !game.NeedsTarget, "No removed summon heal " + id);
            }
            foreach (string id in new[] { "Diamond_7", "Diamond_Q" })
            {
                int damage = id == "Diamond_7" ? 3 : 5;
                game = Fixture(); ally = Unit(game, "Heart_A", 1); Summon(game, id);
                Check(game.SelectEffectTarget(0, 1, 0) && ally.CurrentHP == 10 - damage && !game.NeedsTarget,
                    "One enemy receives chain damage only once " + id);
                game = Fixture(); ally = Unit(game, "Heart_A", 1); var second = Unit(game, "Heart_A", 1, 1);
                Summon(game, id);
                Check(game.SelectEffectTarget(0, 1, 0) && !game.SelectEffectTarget(0, 1, 0)
                    && game.SelectEffectTarget(0, 1, 1) && ally.CurrentHP == 10 - damage && second.CurrentHP == 10 - damage,
                    "Two distinct chain targets " + id);
                game = Fixture(); Summon(game, id);
                Check(!game.NeedsTarget, "Empty enemy board resolves " + id);
            }
        }
    }
}
#endif
