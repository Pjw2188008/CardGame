using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Unknown.Battle;

namespace Unknown.Multiplayer
{
    /// <summary>Explicit development-only two-process Relay check. Never runs in a normal launch.</summary>
    public sealed class NetworkBattleSmokeRunner : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public string role, status = "starting", error = "", publicState = "";
            public int revision, turn, winner = -1, snapshots, summons, attacks, combines, jokers, timeouts, viewSamples;
            public bool privacy = true, finished, cleaned, hostFieldSeen, guestFieldSeen, viewMatches = true, returnedToLobby, resultMatches;
            public int hostAttacks, guestAttacks, directAttacks, unitAttacks;
            public VictoryReason victoryReason;
            public List<string> errors = new List<string>();
            public List<string> attackResults = new List<string>();
        }
        Report report;
        string exchange, output;
        RoomController room;
        NetworkBattleController battle;
        internal static int? SeedOverride { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void FromCommandLine()
        {
            if (!Debug.isDebugBuild) return;
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-unknownSmoke");
            if (index >= 0 && index + 2 < args.Length) Launch(args[index + 1], args[index + 2]);
        }
        public static void Launch(string role, string exchangePath)
        {
            if (Debug.isDebugBuild)
            {
                var args=Environment.GetCommandLineArgs(); int at=Array.IndexOf(args,"-unknownSmokeSeed");
                if (args.Contains("-unknownSmoke") && at>=0 && at+1<args.Length && int.TryParse(args[at+1],out int seed)) SeedOverride=seed;
            }
            var go = new GameObject("UNKNOWN Explicit Relay Smoke Check");
            var runner = go.AddComponent<NetworkBattleSmokeRunner>();
            runner.report = new Report { role = role }; runner.exchange = exchangePath;
            runner.output = exchangePath + "." + role + ".json";
            runner.StartCoroutine(runner.Run());
        }
        void Save() { File.WriteAllText(output, JsonUtility.ToJson(report, true)); }
        void LogError(string text, string stack, LogType type)
        { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) { report.errors.Add(text); Save(); } }
        IEnumerator Run()
        {
            Application.logMessageReceived += LogError;
            room = FindFirstObjectByType<RoomController>(); battle = room.GetComponent<NetworkBattleController>();
            battle.SnapshotReceived += Observe;
            Save();
            if (report.role == "host")
            {
                var task = room.CreateRoomAsync(); while (!task.IsCompleted) yield return null;
                if (room.Session == null) { Fail(room.Error); yield break; }
                File.WriteAllText(exchange, room.Session.Code);
            }
            else
            {
                double wait = Time.realtimeSinceStartupAsDouble + 120;
                string code;
                while (string.IsNullOrEmpty(code=RoomConnectionCheck.ReadCode(exchange)) && Time.realtimeSinceStartupAsDouble < wait) yield return new WaitForSecondsRealtime(0.1f);
                if (string.IsNullOrEmpty(code)) { Fail("Host code not available"); yield break; }
                var task = room.JoinRoomAsync(code); while (!task.IsCompleted) yield return null;
                if (room.Session == null) { Fail(room.Error); yield break; }
            }
            report.status = "lobby"; Save();
            double limit = Time.realtimeSinceStartupAsDouble + 900;
            float retry = 0;
            while (!room.HasStarted && Time.realtimeSinceStartupAsDouble < limit)
            {
                if (room.ConnectedPlayers == 2 && Time.unscaledTime >= retry)
                {
                    retry = Time.unscaledTime + 1;
                    if (!battle.LocalReady) battle.ToggleReady();
                    if (room.CanStart) { var task = room.StartGameAsync(); while (!task.IsCompleted) yield return null; }
                }
                yield return null;
            }
            report.status = "battle"; Save();
            while (Time.realtimeSinceStartupAsDouble < limit)
            {
                if (!string.IsNullOrEmpty(battle.Failure)) { Fail(battle.Failure); yield break; }
                var s = battle.Snapshot;
                if (s != null && s.winner >= 0)
                {
                    report.finished = true; report.status = "victory"; Save();
                    // Allow the other peer to receive and finish the same terminal snapshot before leaving.
                    yield return new WaitForSecondsRealtime(6);
                    var resultUI=FindFirstObjectByType<BattleUIController>();
                    report.resultMatches=resultUI!=null && resultUI.MatchesTerminalResult();
                    var finalView=resultUI?.MatchesNetworkView();
                    if(finalView.HasValue) { report.viewMatches &= finalView.Value; report.viewSamples++; } Save();
                    if (report.role == "guest")
                    {
                        var back = room.ReturnToLobbyAsync(); while (!back.IsCompleted) yield return null;
                        double lobbyLimit=Time.realtimeSinceStartupAsDouble+15;
                        while(room.HasStarted && Time.realtimeSinceStartupAsDouble<lobbyLimit) yield return null;
                        report.returnedToLobby=room.Session!=null && !room.HasStarted && battle.Snapshot==null && !battle.BothReady; Save();
                        yield return new WaitForSecondsRealtime(3);
                        var leave = room.LeaveRoomAsync(); while (!leave.IsCompleted) yield return null;
                        report.cleaned = room.Session == null; Save();
                    }
                    else
                    {
                        double cleanupLimit = Time.realtimeSinceStartupAsDouble + 20;
                        while (room.HasStarted && Time.realtimeSinceStartupAsDouble < cleanupLimit) yield return null;
                        report.returnedToLobby=room.Session!=null && !room.HasStarted && battle.Snapshot==null && !battle.BothReady; Save();
                        yield return new WaitForSecondsRealtime(5);
                        var leave=room.LeaveRoomAsync(); while(!leave.IsCompleted) yield return null;
                        report.cleaned = room.Session == null; Save();
                    }
                    yield break;
                }
                var ui = FindFirstObjectByType<BattleUIController>();
                if (s!=null && ui!=null && !ui.IsPresenting)
                {
                    var matches=ui.MatchesNetworkView();
                    if(matches.HasValue) { report.viewMatches &= matches.Value; report.viewSamples++; }
                    report.hostFieldSeen |= s.players[0].board.Any(c=>c!=null && c.occupied);
                    report.guestFieldSeen |= s.players[1].board.Any(c=>c!=null && c.occupied);
                    Save();
                }
                // First turn deliberately expires, exercising the real 60 second Host clock.
                if (s != null && (s.turn > 1 || Environment.GetCommandLineArgs().Contains("-unknownSmokeNoWait")) && battle.CanAct && ui != null && !ui.IsPresenting)
                    Choose(s);
                yield return new WaitForSecondsRealtime(0.12f);
            }
            Fail("Test exceeded 15 minutes");
        }
        void Observe(BattleSnapshot s)
        {
            report.snapshots++; report.revision = s.revision; report.turn = s.turn; report.winner = s.winner;
            report.victoryReason=s.victoryReason;
            report.privacy &= s.players[1-s.recipient].hand.Length == 0 && s.players[1-s.recipient].unknownCreated == 0
                && !s.events.Any(e => e.owner != s.recipient && e.kind == BattleEventKind.Combine)
                && !s.log.Any(l => l.StartsWith("P"+(2-s.recipient)+": UNKNOWN 조합"));
            foreach (var e in s.events)
            {
                if (e.kind == BattleEventKind.Summon) report.summons++;
                if (e.kind == BattleEventKind.Attack)
                {
                    report.attacks++;
                    if(e.owner==0)report.hostAttacks++; else report.guestAttacks++;
                    if(e.target<0)report.directAttacks++; else report.unitAttacks++;
                    report.attackResults.Add($"{s.revision}:{e.owner}:{e.source}:{e.target}:{e.amount}:{e.secondary}:{s.players[0].hp}:{s.players[1].hp}");
                }
                if (e.kind == BattleEventKind.Combine) report.combines++;
                if (e.kind == BattleEventKind.JokerSuccess || e.kind == BattleEventKind.JokerFailure) report.jokers++;
                if (e.kind == BattleEventKind.TimeOut) report.timeouts++;
            }
            report.publicState = s.turn + ":" + s.active + ":" + s.winner + ":" + string.Join("|", s.players.Select(p =>
                p.hp + "," + p.mana + "," + p.maxMana + "," + p.handCount + "," + p.deckCount + ":" +
                string.Join(";",p.board.Select(c => c == null || !c.occupied ? "-" : c.id + "," + c.definition + "," + c.attack + "," + c.hp + "," + c.maxHP + "," + c.attacked + "," + c.jokerUsed))));
            Save();
        }
        void Choose(BattleSnapshot s)
        {
            int me = s.recipient, enemy = 1-me; var p = s.players[me];
            if (s.targets.Length > 0) { int t = s.targets[0]; battle.Submit(BattleAction.Target,t / 5,t % 5); return; }
            var hand = p.hand.Select(c => c.ToCard()).ToArray();
            var ace = hand.FirstOrDefault(c => c.Definition.Rank == Rank.Ace && c.Definition.CardType == CardType.Normal);
            var face = hand.FirstOrDefault(c => c.Definition.Rank >= Rank.Jack && c.Definition.Rank <= Rank.King && c.Definition.CardType == CardType.Normal);
            if (ace != null && face != null && p.unknownCreated < 3)
            { if(!FindFirstObjectByType<BattleUIController>().SmokeCombine(ace.InstanceID,face.InstanceID)) Debug.LogError("Smoke combination drag failed"); return; }
            var joker = hand.FirstOrDefault(c => c.Definition.CardType == CardType.Joker);
            var gamble = p.board.FirstOrDefault(c => c != null && c.occupied && !c.jokerUsed);
            if (joker != null && gamble != null && p.mana >= 2)
            { if(!FindFirstObjectByType<BattleUIController>().SmokeDrag(joker.InstanceID,CardDropKind.Unit,Array.IndexOf(p.board,gamble))) Debug.LogError("Smoke Joker drag failed"); return; }
            var attack = p.board.FirstOrDefault(c => c != null && c.occupied && !c.attacked && c.summonedTurn < s.turn && c.attack > 0);
            if (attack != null)
            {
                int slot = Array.FindIndex(s.players[enemy].board,c => c != null && c.occupied);
                int target=slot<0 ? -1 : s.players[enemy].board[slot].id;
                if(!FindFirstObjectByType<BattleUIController>().SmokeAttack(attack.id,target))Debug.LogError("Smoke attack drag failed"); return;
            }
            int empty = Array.FindIndex(p.board,c => c == null || !c.occupied);
            var summon = hand.Where(c => c.Definition.CardType != CardType.Joker && c.Definition.ManaCost <= p.mana).OrderByDescending(c => c.Definition.ManaCost).FirstOrDefault();
            if (empty >= 0 && summon != null) { battle.Submit(BattleAction.Summon,summon.InstanceID,empty); return; }
            battle.Submit(BattleAction.EndTurn);
        }
        void Fail(string error) { report.error = error; report.status = "failed"; Save(); }
        void OnDestroy()
        {
            Application.logMessageReceived -= LogError;
            if (battle != null) battle.SnapshotReceived -= Observe;
        }
    }
}
