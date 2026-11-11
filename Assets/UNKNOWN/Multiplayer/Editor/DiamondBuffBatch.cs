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
    public static class DiamondBuffBatch
    {
        static BalanceTestRun active;
        static BalanceScheduledMatch[] schedule;
        static readonly List<string> passed = new List<string>();
        static string directory;
        static string revisedDirectory;
        static int phase;
        static int progress = -1;

        [Serializable]
        sealed class Experiment
        {
            public string schemaVersion = "2.0";
            public string status;
            public int seed = 7312026;
            public int matchesPerVersion = 1000;
            public string revisedDirectory;
            public string diamondBuffDirectory;
            public string[] passed;
            public string error;
            public BalanceDefinition[] revisedCards;
            public BalanceDefinition[] diamondBuffCards;
            public BalanceScheduledMatch[] schedule;
        }

        static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            passed.Add(name);
        }

        static BalanceScheduledMatch[] BuildSchedule()
        {
            var all = new List<BalanceScheduledMatch>();
            for (int block = 0; block < 2; block++)
            {
                int seed = 7312026 + block * 50000003;
                var entries = CardRedesignExperiment.MainSchedule(seed)
                    .Concat(CardRedesignExperiment.StressSchedule(seed));
                foreach (var entry in entries)
                {
                    if (block > 0) entry.conditionID = "block2-" + entry.conditionID;
                    all.Add(entry);
                }
            }
            return all.ToArray();
        }

        [MenuItem("Tools/UNKNOWN/Run Diamond Buff Comparison")]
        public static void RunInteractive() { Start(false); }
        static bool batch;
        public static void RunBatch() { Start(true); }

        static void Start(bool isBatch)
        {
            if (active != null) throw new InvalidOperationException("A diamond comparison is already active.");
            batch = isBatch;
            phase = 0;
            passed.Clear();
            directory = Path.GetFullPath(Path.Combine("diamond_buff_test",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")));
            Directory.CreateDirectory(directory);
            try
            {
                passed.AddRange(DiamondBuffChecks.Run());
                passed.AddRange(HeartNerfChecks.Run());
                CardRedesignChecks.Run(Path.Combine(directory, "revised_regression"));
                passed.AddRange(CardRedesignChecks.Passed);
                schedule = BuildSchedule();
                Check(schedule.Length == 1000, "Exactly 1000 paired games per version");
                StartPhase();
                EditorApplication.update += Tick;
            }
            catch (Exception error) { Fail(error); }
        }

        static void StartPhase()
        {
            var version = phase == 0 ? CardBalanceVersion.Revised : CardBalanceVersion.DiamondBuff;
            active = new BalanceTestRun(new BalanceTestSettings { seed = 7312026, detailedLog = true },
                Path.Combine(directory, version.ToString()), new BattleConfig {
                    EnablePoker = false, CardVersion = version,
                    MapRules = BattleMapSettings.LoadRules(), MapEvents = JokerMapSettings.LoadRules()
                }, schedule);
            progress = -1;
        }

        static void CompressLog(string runDirectory)
        {
            string log = Path.Combine(runDirectory, "effect_events.jsonl");
            using (var input = File.OpenRead(log))
            using (var output = File.Create(log + ".gz"))
            using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal)) input.CopyTo(gzip);
        }

        static void WriteResult(string status, string error = "")
        {
            var result = new Experiment {
                status = status, revisedDirectory = revisedDirectory,
                diamondBuffDirectory = phase == 1 ? active?.DirectoryPath : null,
                passed = passed.ToArray(), error = error, schedule = schedule,
                revisedCards = CardDatabase.AllFor(CardBalanceVersion.Revised).Select(BalanceDefinition.From).ToArray(),
                diamondBuffCards = CardDatabase.AllFor(CardBalanceVersion.DiamondBuff).Select(BalanceDefinition.From).ToArray()
            };
            File.WriteAllText(Path.Combine(directory, "experiment.json"), JsonUtility.ToJson(result, true));
            File.WriteAllText("Library/DiamondBuff-Location.txt", directory);
        }

        static void Tick()
        {
            try
            {
                active.Tick(50);
                if (active.Matches.Count / 100 != progress)
                {
                    progress = active.Matches.Count / 100;
                    Debug.Log("[DiamondBuff] " + active.Rules.CardVersion + " " + active.Matches.Count + "/1000");
                }
                if (!active.Finished) return;
                CompressLog(active.DirectoryPath);
                if (phase == 0)
                {
                    revisedDirectory = active.DirectoryPath;
                    active.Dispose();
                    phase = 1;
                    StartPhase();
                    return;
                }
                WriteResult("completed");
                active.Dispose();
                active = null;
                EditorApplication.update -= Tick;
                Debug.Log("[DiamondBuff] Completed " + directory);
                if (batch) EditorApplication.Exit(0);
            }
            catch (Exception error) { Fail(error); }
        }

        static void Fail(Exception error)
        {
            Debug.LogException(error);
            active?.Dispose();
            WriteResult("failed", error.ToString());
            active = null;
            EditorApplication.update -= Tick;
            if (batch) EditorApplication.Exit(1);
        }
    }
}
