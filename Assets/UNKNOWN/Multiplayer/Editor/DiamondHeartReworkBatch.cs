using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unknown.Battle;

namespace Unknown.Multiplayer.Editor
{
    public static class DiamondHeartReworkBatch
    {
        static readonly CardBalanceVersion[] versions = {
            CardBalanceVersion.Revised, CardBalanceVersion.DiamondReworkOnly,
            CardBalanceVersion.HeartReworkOnly, CardBalanceVersion.DiamondHeartRework
        };
        [Serializable]
        sealed class Cohort
        {
            public string version, directory;
            public BalanceDefinition[] cards;
        }
        [Serializable]
        sealed class Experiment
        {
            public string schemaVersion = "3.0", status, error;
            public int seed = 7312026, gamesPerVersion = 1000;
            public string previousLiveVersion = "DiamondBuff";
            public BalanceDefinition[] previousLiveCards;
            public Cohort[] cohorts;
            public BalanceScheduledMatch[] schedule;
            public string[] passed;
        }
        static BalanceTestRun active;
        static BalanceScheduledMatch[] schedule;
        static readonly List<Cohort> cohorts = new List<Cohort>();
        static readonly List<string> passed = new List<string>();
        static string directory;
        static int phase, progress;
        static bool batch;

        static BalanceScheduledMatch Swap(BalanceScheduledMatch entry)
        {
            return new BalanceScheduledMatch {
                conditionID = entry.conditionID, swapID = "BA", map = entry.map,
                first = entry.second, second = entry.first, combatSeed = entry.combatSeed,
                firstDeckSeed = entry.secondDeckSeed, secondDeckSeed = entry.firstDeckSeed,
                firstPolicySeed = entry.secondPolicySeed, secondPolicySeed = entry.firstPolicySeed
            };
        }
        static BalanceScheduledMatch[] BuildSchedule()
        {
            var result = new List<BalanceScheduledMatch>();
            for (int block = 0; block < 2; block++)
            {
                var main = CardRedesignExperiment.MainSchedule(7312026 + block * 50000003);
                foreach (var entry in main)
                {
                    if (block > 0) entry.conditionID = "block2-" + entry.conditionID;
                    result.Add(entry);
                }
                foreach (var entry in CardRedesignExperiment.StressSchedule(7312026))
                    result.Add(block == 0 ? entry : Swap(entry));
            }
            return result.ToArray();
        }
        static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            passed.Add(name);
        }
        [MenuItem("Tools/UNKNOWN/Run Diamond Heart Rework Comparison")]
        public static void RunInteractive() => Start(false);
        public static void RunBatch() => Start(true);

        static void Start(bool isBatch)
        {
            if (active != null) throw new InvalidOperationException("A rework comparison is already running.");
            batch = isBatch;
            phase = 0;
            cohorts.Clear(); passed.Clear();
            directory = Path.GetFullPath(Path.Combine("diamond_heart_rework_test", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")));
            Directory.CreateDirectory(directory);
            try
            {
                passed.AddRange(DiamondHeartReworkChecks.Run());
                passed.AddRange(DiamondBuffChecks.Run());
                passed.AddRange(HeartNerfChecks.Run());
                CardRedesignChecks.Run(Path.Combine(directory, "revised_regression"));
                passed.AddRange(CardRedesignChecks.Passed);
                schedule = BuildSchedule();
                Check(schedule.Length == 1000 && schedule.GroupBy(e => e.conditionID).All(g =>
                    g.Count() == 2 && g.Any(e => e.swapID == "AB") && g.Any(e => e.swapID == "BA")),
                    "500 conditions each have exactly two swapped legs");
                foreach (BalanceAI style in Enum.GetValues(typeof(BalanceAI)))
                    Check(schedule.Count(e => e.first == style) == schedule.Count(e => e.second == style),
                        "Exactly balanced seats for " + style);
                StartPhase();
                EditorApplication.update += Tick;
                AssemblyReloadEvents.beforeAssemblyReload += Stop;
            }
            catch (Exception error) { Fail(error); }
        }
        static void StartPhase()
        {
            var version = versions[phase];
            active = new BalanceTestRun(new BalanceTestSettings { seed = 7312026, detailedLog = true },
                Path.Combine(directory, version.ToString()), new BattleConfig {
                    CardVersion = version, EnablePoker = false,
                    MapRules = BattleMapSettings.LoadRules(), MapEvents = JokerMapSettings.LoadRules()
                }, schedule);
            cohorts.Add(new Cohort { version = version.ToString(), directory = active.DirectoryPath,
                cards = CardDatabase.AllFor(version).Select(BalanceDefinition.From).ToArray() });
            progress = -1;
            WriteResult("running");
        }
        static void WriteResult(string status, string error = "")
        {
            File.WriteAllText(Path.Combine(directory, "experiment.json"), JsonUtility.ToJson(new Experiment {
                status = status, error = error, cohorts = cohorts.ToArray(), schedule = schedule, passed = passed.ToArray(),
                previousLiveCards = CardDatabase.AllFor(CardBalanceVersion.DiamondBuff).Select(BalanceDefinition.From).ToArray()
            }, true));
            File.WriteAllText("Library/DiamondHeartRework-Location.txt", directory);
        }
        static void Detach()
        {
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
        }
        static void Tick()
        {
            try
            {
                active.Tick(50);
                if (active.Matches.Count / 100 != progress)
                {
                    progress = active.Matches.Count / 100;
                    Debug.Log("[DHRework] " + versions[phase] + " " + active.Matches.Count + "/1000");
                }
                if (!active.Finished) return;
                string log = Path.Combine(active.DirectoryPath, "effect_events.jsonl");
                using (var input = File.OpenRead(log))
                using (var output = File.Create(log + ".gz"))
                using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal)) input.CopyTo(gzip);
                active.Dispose(); active = null; phase++;
                if (phase < versions.Length) { StartPhase(); return; }
                WriteResult("completed"); Detach();
                Debug.Log("[DHRework] Completed " + directory);
                if (batch) EditorApplication.Exit(0);
            }
            catch (Exception error) { Fail(error); }
        }
        [MenuItem("Tools/UNKNOWN/Stop Diamond Heart Rework Comparison")]
        public static void Stop()
        {
            if (active == null) return;
            active.Dispose(); active = null;
            WriteResult("stopped"); Detach();
        }
        static void Fail(Exception error)
        {
            Debug.LogException(error);
            active?.Dispose(); active = null;
            WriteResult("failed", error.ToString()); Detach();
            if (batch) EditorApplication.Exit(1);
        }
    }
}
