#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Unknown.Battle
{
    // Editor-only end-to-end checks: real rule commands, UI rebuilds, clocks, FX coroutines and locks.
    // Controlled fixtures are never used by NewMatch or included in a player build.
    public sealed partial class BattleUIController
    {
        [Serializable] sealed class PlayCheckReport
        {
            public string unityVersion;
            public string completedUtc;
            public bool completed;
            public List<string> passed = new List<string>();
            public List<string> failed = new List<string>();
            public List<string> runtimeErrors = new List<string>();
        }
        sealed class CheckDice : IRandomSource
        {
            readonly int result;
            public CheckDice(int value) { result = value; }
            public int Next(int max) => result % max;
        }
        bool integrationChecksRunning;
        PlayCheckReport playReport;

        public void RunIntegrationChecks()
        {
            if (integrationChecksRunning) return;
            StopAllCoroutines(); animations.ResetAll();
            integrationChecksRunning = true; StartCoroutine(IntegrationChecks());
        }
        void Verify(bool condition, string name)
        {
            if (condition) playReport.passed.Add(name); else playReport.failed.Add(name);
        }
        void CheckFixture(int dice = 0)
        {
            animations.ResetAll(); actionBusy = transitioning = paused = false;
            passOverlay.SetActive(false); resultOverlay.SetActive(false); ClearSelection();
            engine = new BattleEngine(10, new BattleConfig { EnablePoker=false,StartingHand = 0, StartingMana = 10, ManaCap = 10, ShuffleDecks = false }, new CheckDice(dice));
            foreach (var player in engine.State.Players) player.HandCards.Clear();
            ResetHandViews();
            engine.DrainEvents(); clock.Begin(40, Time.realtimeSinceStartupAsDouble);
            feedback = "PLAY MODE INTEGRATION CHECK";
        }
        CardInstance CheckHand(string id, int owner = 0)
        { var card = engine.NewCard(CardDatabase.Get(id), owner); engine.State.Players[owner].HandCards.Add(card); return card; }
        CardInstance CheckUnit(string id, int owner, int slot = 0)
        {
            var card = engine.NewCard(CardDatabase.Get(id), owner); card.SummonedTurn = 0;
            engine.State.Players[owner].Slots[slot] = card; return card;
        }
        IEnumerator CheckIdle()
        {
            double until = Time.realtimeSinceStartupAsDouble + 8;
            while ((actionBusy || transitioning) && Time.realtimeSinceStartupAsDouble < until) yield return null;
            Verify(!actionBusy && !transitioning, "Action/transition completed without a stuck input lock");
        }
        IEnumerator IntegrationChecks()
        {
            playReport = new PlayCheckReport { unityVersion = Application.unityVersion };
            bool originalReveal = revealHands; float originalDuration = turnDuration;
            Application.LogCallback errorListener = (message, stack, type) =>
            { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) playReport.runtimeErrors.Add(message + "\n" + stack); };
            Application.logMessageReceived += errorListener;
            try
            {
                revealHands = false;
                CheckFixture(); var enemy = CheckUnit("Heart_K", 1); var first = CheckHand("Diamond_2"); var second = CheckHand("Club_2"); Refresh();
                RunCommand(() => engine.Summon(0, first.InstanceID, 0), "summon");
                RunCommand(() => engine.Summon(0, second.InstanceID, 1), "duplicate should be blocked");
                Verify(engine.State.SummonsThisTurn == 1, "Duplicate click blocked during summon animation");
                yield return CheckIdle();
                RunCommand(() => engine.SelectEffectTarget(0, 1, 0), "diamond hit"); yield return CheckIdle();
                RunCommand(() => engine.Summon(0, second.InstanceID, 1), "second summon"); yield return CheckIdle();
                RunCommand(() => engine.SelectEffectTarget(0, 0, 0), "club buff"); yield return CheckIdle();
                Verify(engine.State.Players[0].CurrentMana == 8 && engine.State.Players[0].Board.Count(c => c != null) == 2 && enemy.CurrentHP == 11,
                    "Multiple summons, diamond damage, club buff and exact mana UI refresh");

                CheckFixture(); var attacker = CheckUnit("Diamond_2", 0); CheckUnit("Diamond_2", 1); Refresh();
                RunCommand(() => engine.Attack(0, attacker.InstanceID, 0), "combat"); yield return CheckIdle();
                Verify(engine.State.Players.All(p => p.Board.All(c => c == null)), "Attack lunge, two damage values and two destruction animations");

                CheckFixture(); var ally = CheckUnit("Heart_K", 0); ally.CurrentHP = 5; var healing = CheckHand("Heart_6"); Refresh();
                RunCommand(() => engine.Summon(0, healing.InstanceID, 1), "heal"); yield return CheckIdle();
                Verify(ally.CurrentHP == 7, "Heart all-allies healing and actual green floating value");

                CheckFixture(); enemy = CheckUnit("Heart_K", 1); var leech = CheckHand("Heart_7"); Refresh();
                RunCommand(() => engine.Summon(0, leech.InstanceID, 0), "heart summon"); yield return CheckIdle();
                leech.CurrentHP = 2; Refresh(); // Wounded committed unit fixture, before its selected-target command.
                RunCommand(() => engine.SelectEffectTarget(0, 1, 0), "heart return light"); yield return CheckIdle();
                Verify(leech.CurrentHP == 5 && enemy.CurrentHP == 9, "Heart damage-to-self healing return-light sequence");

                CheckFixture(); enemy = CheckUnit("Heart_2", 1); CheckHand("Club_2", 1); CheckHand("Club_3", 1); var spade = CheckHand("Spade_Q"); Refresh();
                RunCommand(() => engine.Summon(0, spade.InstanceID, 0), "discard/debuff"); yield return CheckIdle();
                Verify(enemy.Attack == 0 && engine.State.Players[1].Hand.Count == 0, "Spade sequential discards and clamped attack reduction");

                CheckFixture(); ally = CheckUnit("Heart_K", 0); var club = CheckHand("Club_A"); Refresh();
                RunCommand(() => engine.Summon(0, club.InstanceID, 1), "draw/max hp"); yield return CheckIdle();
                Verify(engine.State.Players[0].Hand.Count == 2 && ally.MaxHP == 14, "Deck-to-hand draw and all-allies max HP effects");

                CheckFixture(); var chainA = CheckUnit("Heart_K", 1); var chainB = CheckUnit("Heart_K", 1, 1); var chain = CheckHand("Diamond_7"); Refresh();
                RunCommand(() => engine.Summon(0, chain.InstanceID, 0), "chain summon"); yield return CheckIdle();
                RunCommand(() => engine.SelectEffectTarget(0, 1, 0), "chain 1"); yield return CheckIdle();
                RunCommand(() => engine.SelectEffectTarget(0, 1, 1), "chain 2"); yield return CheckIdle();
                Verify(chainA.CurrentHP == 10 && chainB.CurrentHP == 10, "Distinct chain targets across two animation batches");

                CheckFixture(); var ace = CheckHand("Heart_A"); var queen = CheckHand("Spade_Q"); var joker = CheckHand("Joker_1"); Refresh();
                RunCommand(() => engine.Combine(0, ace.InstanceID, queen.InstanceID), "combine"); yield return CheckIdle();
                var unknown = engine.State.Players[0].Hand.First(c => c.Definition.CardType == CardType.Unknown);
                Verify(engine.State.Players[0].CurrentMana == 10, "UNKNOWN merge animation costs zero mana");
                RunCommand(() => engine.Summon(0, unknown.InstanceID, 0), "unknown summon"); yield return CheckIdle();
                RunCommand(() => engine.Gamble(0, joker.InstanceID, unknown.InstanceID), "joker success"); yield return CheckIdle();
                Verify(unknown.Attack == 18 && unknown.MaxHP == 27 && engine.State.Players[0].CurrentMana == 0,
                    "UNKNOWN summon, Joker SUCCESS, stat text and exact 8+2 mana cost");

                CheckFixture(1); var doomed = CheckUnit("Unknown_K", 0); joker = CheckHand("Joker_1"); Refresh();
                RunCommand(() => engine.Gamble(0, joker.InstanceID, doomed.InstanceID), "joker fail"); yield return CheckIdle();
                Verify(engine.State.Players[0].Board[0] == null && engine.State.Players[0].CurrentMana == 8, "Joker FAILED followed by destruction");

                CheckFixture(); attacker = CheckUnit("Unknown_K", 0); engine.State.Players[1].HP = 2; Refresh();
                RunCommand(() => engine.Attack(0, attacker.InstanceID), "direct lethal"); yield return CheckIdle();
                Verify(engine.State.Winner == 0 && resultOverlay.activeSelf && !clock.Running, "Direct attack, winner overlay, stopped timer");

                CheckFixture(); enemy = CheckUnit("Heart_K", 1); var timedSummon = CheckHand("Diamond_2"); Refresh();
                clock.Begin(0.08, Time.realtimeSinceStartupAsDouble);
                RunCommand(() => engine.Summon(0, timedSummon.InstanceID, 0), "timeout during summon"); yield return CheckIdle();
                Verify(engine.State.TurnNumber == 2 && enemy.CurrentHP == 11 && engine.State.Players[0].CurrentMana == 9 && passOverlay.activeSelf,
                    "Timeout during animation auto-resolves pending target and transitions exactly once");

                CheckFixture(); Refresh(); EndTurn(); EndTurn(); yield return CheckIdle();
                Verify(engine.State.TurnNumber == 2 && passOverlay.activeSelf, "Double end-turn click does not skip a player");

                CheckFixture(); Refresh(); clock.Begin(60, Time.realtimeSinceStartupAsDouble); paused = true;
                yield return new WaitForSecondsRealtime(0.15f);
                Verify(Math.Abs(clock.Remaining - 60) < 0.05, "Pause holds the real-time timer");
                paused = false; yield return new WaitForSecondsRealtime(0.15f);
                Verify(clock.Remaining < 60, "Timer resumes after pause");
                playReport.completed = true;
            }
            finally
            {
                Application.logMessageReceived -= errorListener;
                revealHands = originalReveal; turnDuration = originalDuration; paused = false;
                playReport.completedUtc = DateTime.UtcNow.ToString("O");
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/UNKNOWN-Battle-PlayMode.json"));
                File.WriteAllText(path, JsonUtility.ToJson(playReport, true));
                Debug.Log($"[UNKNOWN Play Checks] completed={playReport.completed}, passed={playReport.passed.Count}, failed={playReport.failed.Count}, runtimeErrors={playReport.runtimeErrors.Count}. {path}");
                integrationChecksRunning = false;
                NewMatch();
            }
        }
    }
}
#endif
