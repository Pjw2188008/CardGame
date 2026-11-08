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
            public bool systemsFixture, timerProtocol=true;
            public int refills, refillCards, frozenClockSamples, runningClockSamples;
            public double firstTurnElapsed;
            public BattleMap map;
            public bool mapMatches=true, guestCannotSelect=true, mapEventsOnce=true;
            public int grantedJokers, ownJokerSuccesses;
            public List<int> mapEventIDs=new List<int>();
            public List<string> mapResults=new List<string>();
            public int round=1, hostMapDraws, introCount;
            public int suitTurns,suitDamage,suitStatChanges,heroHeals,manaGains;
            public bool lobbyMapHidden=true, resetState=true, introLocks=true;
            public List<string> pokerResults=new List<string>();
            public int manaRejects;
            public bool manaRejectionMatches=true;
            public bool handLimitMatches=true,optionsClockContinues,optionsInputBlocked,optionsRestored,optionsChecked,manualRepeatRejected;
            public int manualDiscards,overflowDiscards;
            public List<string> handEvents=new List<string>();
        }
        Report report;
        string exchange, output;
        RoomController room;
        NetworkBattleController battle;
        internal static int? SeedOverride { get; private set; }
        internal static bool SystemsCheck => Debug.isDebugBuild && Environment.GetCommandLineArgs().Contains("-unknownSmoke") && Environment.GetCommandLineArgs().Contains("-unknownSmokeSystems");
        double firstClockStart=-1;
        int round;
        bool repeatPending;
        int repeatErrorVersion,repeatRevision,repeatHand,repeatGrave;
        bool manaProbePending;
        int manaProbeVersion,manaProbeRevision,manaProbeCard,manaProbeCost,manaProbeMana,manaProbeHand;
        int MatchCount { get { var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-unknownSmokeRounds");return at>=0 && at+1<args.Length && int.TryParse(args[at+1],out int count) ? Math.Max(1,Math.Min(5,count)) : args.Contains("-unknownSmokeRematch") ? 2 : 1; } }
        bool Rematch => MatchCount>1;
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
            runner.report = new Report { role = role, systemsFixture=SystemsCheck }; runner.exchange = exchangePath;
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
            for(round=0;round<MatchCount;round++)
            {
                if(round>0) { report=new Report{role=report.role,systemsFixture=SystemsCheck,round=round+1}; firstClockStart=-1; }
                yield return MatchLoop();
                File.WriteAllText(exchange+"."+report.role+".round"+(round+1)+".json",JsonUtility.ToJson(report,true));
                if(report.status=="failed")yield break;
            }
        }
        IEnumerator MatchLoop()
        {
            report.lobbyMapHidden=!battle.SelectedMap.HasValue;
            report.resetState=battle.Snapshot==null && !battle.CanAct && !battle.BothReady;
            report.guestCannotSelect=typeof(NetworkBattleController).GetMethod("SelectMap")==null;
            report.status = "lobby"; Save();
            double limit = Time.realtimeSinceStartupAsDouble + 900;
            float retry = 0;
            while (!room.HasStarted && Time.realtimeSinceStartupAsDouble < limit)
            {
                report.lobbyMapHidden &= !battle.SelectedMap.HasValue;
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
                if(repeatPending){if(battle.ActionErrorVersion>repeatErrorVersion){report.manualRepeatRejected=s.revision==repeatRevision && s.players[s.recipient].handCount==repeatHand && s.players[s.recipient].discardCount==repeatGrave && battle.ActionError.Contains("이미");repeatPending=false;Save();}else {yield return new WaitForSecondsRealtime(0.1f);continue;}}
                if(manaProbePending) {
                    if(battle.ActionErrorVersion>manaProbeVersion) {
                        yield return new WaitForSecondsRealtime(0.35f);
                        var probeUI=FindFirstObjectByType<BattleUIController>();var current=battle.Snapshot;
                        report.manaRejectionMatches &= current!=null && current.revision==manaProbeRevision && current.players[current.recipient].mana==manaProbeMana
                            && current.players[current.recipient].handCount==manaProbeHand && current.players[current.recipient].hand.Any(c=>c.id==manaProbeCard)
                            && probeUI!=null && probeUI.MatchesManaRejection(manaProbeCard,manaProbeCost,manaProbeMana);
                        report.manaRejects++;manaProbePending=false;Save();
                    } else {yield return new WaitForSecondsRealtime(0.1f);continue;}
                }
                var introUI=FindFirstObjectByType<BattleUIController>();
                if(introUI!=null) { report.introCount=introUI.MapIntroCount; if(introUI.IsMapIntroActive)report.introLocks &= !battle.CanAct && s!=null && !s.clockRunning && Math.Abs(battle.Remaining-40)<0.01; }
                if(s!=null && s.winner<0) {
                    report.timerProtocol &= Math.Abs(s.turnDuration-40)<0.01 && battle.Remaining>=0 && battle.Remaining<=40.1;
                    if(!s.clockRunning) { report.frozenClockSamples++; report.timerProtocol &= s.busy && !battle.CanAct && Math.Abs(battle.Remaining-40)<0.01; }
                    else report.runningClockSamples++;
                }
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
                        report.returnedToLobby=room.Session!=null && !room.HasStarted && battle.Snapshot==null && !battle.BothReady && !battle.SelectedMap.HasValue; Save();
                        yield return new WaitForSecondsRealtime(3);
                        if(round==MatchCount-1) { var leave = room.LeaveRoomAsync(); while (!leave.IsCompleted) yield return null; report.cleaned = room.Session == null; Save(); }
                    }
                    else
                    {
                        double cleanupLimit = Time.realtimeSinceStartupAsDouble + 20;
                        while (room.HasStarted && Time.realtimeSinceStartupAsDouble < cleanupLimit) yield return null;
                        report.returnedToLobby=room.Session!=null && !room.HasStarted && battle.Snapshot==null && !battle.BothReady && !battle.SelectedMap.HasValue; Save();
                        yield return new WaitForSecondsRealtime(5);
                        if(round==MatchCount-1) { var leave=room.LeaveRoomAsync(); while(!leave.IsCompleted) yield return null; report.cleaned = room.Session == null; Save(); }
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
                // First turn deliberately expires, exercising the real forty-second Host clock.
                if (s != null && (s.turn > 1 || Environment.GetCommandLineArgs().Contains("-unknownSmokeNoWait")) && battle.CanAct && ui != null && !ui.IsPresenting)
                {
                    if(Environment.GetCommandLineArgs().Contains("-unknownSmokeHand") && !report.optionsChecked){double before=battle.Remaining;report.optionsInputBlocked=ui.SmokeOptions(true);yield return new WaitForSecondsRealtime(1.1f);report.optionsClockContinues=battle.Remaining<before-0.5 && battle.Snapshot.clockRunning;report.optionsRestored=ui.SmokeOptions(false);report.optionsChecked=true;Save();}
                    Choose(s);
                }
                yield return new WaitForSecondsRealtime(0.12f);
            }
            Fail("Test exceeded 15 minutes");
        }
        void Observe(BattleSnapshot s)
        {
            if(s.turn==1 && s.clockRunning && firstClockStart<0)firstClockStart=battle.GetComponent<Unity.Netcode.NetworkManager>().ServerTime.Time;
            if(s.events.Any(e=>e.kind==BattleEventKind.TimeOut) && firstClockStart>=0 && report.firstTurnElapsed==0)
                report.firstTurnElapsed=battle.GetComponent<Unity.Netcode.NetworkManager>().ServerTime.Time-firstClockStart;
            report.snapshots++; report.revision = s.revision; report.turn = s.turn; report.winner = s.winner;
            report.victoryReason=s.victoryReason;
            report.map=s.map; report.mapMatches &= s.map==battle.SelectedMap;
            report.hostMapDraws=battle.MapDrawCount;
            report.ownJokerSuccesses=s.players[s.recipient].jokerSuccesses;
            report.handLimitMatches &= s.handLimit==8 && s.players.All(p=>p.handCount<=8);
            report.privacy &= s.players[1-s.recipient].hand.Length == 0 && s.players[1-s.recipient].unknownCreated == 0
                && s.players[1-s.recipient].jokerSuccesses==0 && s.players[1-s.recipient].jokerChanceDelta==0
                && !s.events.Any(e=>e.owner!=s.recipient && e.kind==BattleEventKind.CardGranted)
                && (s.mapEvent==null || s.mapEvent.owner==s.recipient || s.mapEvent.kind!=JokerEventKind.None || s.mapEvent.detail=="상대에게 비공개 사건이 적용되었습니다." || s.mapEvent.name=="효과 없음")
                && !s.events.Any(e => e.owner != s.recipient && e.kind == BattleEventKind.Combine)
                && !s.events.Any(e => e.owner != s.recipient && e.kind == BattleEventKind.HandRefill)
                && !s.events.Any(e=>e.owner!=s.recipient && (e.kind==BattleEventKind.Discard || e.kind==BattleEventKind.Draw) && (e.target!=-1 || !string.IsNullOrEmpty(e.definition)))
                && !s.log.Any(l => l.StartsWith("P"+(2-s.recipient)+": UNKNOWN 조합"));
            foreach (var e in s.events)
            {
                if(e.kind==BattleEventKind.Discard){report.handEvents.Add($"{s.revision}:P{e.owner+1}:reason={e.secondary}:hand={s.players[e.owner].handCount}:grave={s.players[e.owner].discardCount}");if(e.secondary==2 && e.owner==s.recipient)report.manualDiscards++;if(e.secondary==1)report.overflowDiscards++;}
                if(e.kind==BattleEventKind.PokerChanged)report.pokerResults.Add($"{s.revision}:P{e.owner+1}:{(PokerKind)e.amount}->{(PokerKind)e.secondary}");
                if(e.kind==BattleEventKind.SuitMapTurn)report.suitTurns++;
                if(e.kind==BattleEventKind.Damage && e.source<0)report.suitDamage+=e.amount;
                if((e.kind==BattleEventKind.AttackChanged || e.kind==BattleEventKind.MaxHPChanged) && e.source<0 && MapDefinition.IsSuit(s.map))report.suitStatChanges++;
                if(e.kind==BattleEventKind.HeroChanged && e.amount>0)report.heroHeals+=e.amount;
                if(e.kind==BattleEventKind.ManaGained)report.manaGains+=e.amount;
                if(e.kind==BattleEventKind.MapEvent || e.kind==BattleEventKind.SuitMapTurn) { report.mapEventsOnce &= !report.mapEventIDs.Contains(e.source); report.mapEventIDs.Add(e.source); }
                if(e.kind==BattleEventKind.CardGranted && e.owner==s.recipient)report.grantedJokers++;
                if(e.kind==BattleEventKind.MapEvent || e.kind==BattleEventKind.SuitMapTurn || e.kind==BattleEventKind.ManaGained || e.kind==BattleEventKind.Damage || e.kind==BattleEventKind.AttackChanged || e.kind==BattleEventKind.MaxHPChanged || e.kind==BattleEventKind.HeroChanged)
                    report.mapResults.Add($"{s.revision}:{e.kind}:{e.owner}:{e.target}:{e.amount}:{e.secondary}");
                if(e.kind==BattleEventKind.HandRefill) { report.refills++; report.refillCards+=e.amount; }
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
                p.hp + "," + p.mana + "," + p.maxMana + "," + p.handCount + "," + p.deckCount + ","+p.discardCount+","+p.manualDiscardUsed+","+p.poker.kind+":"+string.Join(",",p.poker.members)+":"+string.Join(",",p.poker.targets)+":" +
                string.Join(";",p.board.Select(c => c == null || !c.occupied ? "-" : c.id + "," + c.definition + "," + c.attack + "," + c.hp + "," + c.maxHP + "," + c.attacked + "," + c.jokerUsed+","+c.pokerAttack+","+c.pokerHP+","+c.rawAttack+","+c.rawHP+","+c.mapAttack+","+c.mapHP+","+c.jokerAttack+","+c.jokerHP+","+c.abilityAttack+","+c.abilityHP))));
            Save();
        }
        void Choose(BattleSnapshot s)
        {
            int me = s.recipient, enemy = 1-me; var p = s.players[me];
            if (s.targets.Length > 0) { int t = s.targets[0]; battle.Submit(BattleAction.Target,t / 5,t % 5); return; }
            if(Environment.GetCommandLineArgs().Contains("-unknownSmokeHand")) {
                // Natural stockpiling reaches capacity using real turn draws; no cards or decks are injected.
                if(s.turn<=8){battle.Submit(BattleAction.EndTurn);return;}
                if(p.manualDiscardUsed && !report.manualRepeatRejected && !repeatPending && p.hand.Length>0){repeatPending=true;repeatErrorVersion=battle.ActionErrorVersion;repeatRevision=s.revision;repeatHand=p.handCount;repeatGrave=p.discardCount;battle.Submit(BattleAction.Discard,p.hand[0].id);return;}
                if(!p.manualDiscardUsed && report.manualDiscards<2 && p.hand.Length>1){var discard=p.hand.OrderBy(c=>CardDatabase.Get(c.definition).ManaCost).First();if(!FindFirstObjectByType<BattleUIController>().SmokeDiscard(discard.id))Debug.LogError("Smoke discard UI failed");return;}
            }
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
            if(Environment.GetCommandLineArgs().Contains("-unknownSmokeMana") && report.manaRejects<2 && empty>=0) {
                var denied=hand.FirstOrDefault(c=>c.Definition.CardType!=CardType.Joker && c.Definition.ManaCost>p.mana);
                if(denied!=null) {
                    manaProbeVersion=battle.ActionErrorVersion;manaProbeRevision=s.revision;manaProbeCard=denied.InstanceID;manaProbeCost=denied.Definition.ManaCost;manaProbeMana=p.mana;manaProbeHand=p.handCount;manaProbePending=true;
                    if(!FindFirstObjectByType<BattleUIController>().SmokeDrag(denied.InstanceID,CardDropKind.Unit,empty)) {report.manaRejectionMatches=false;manaProbePending=false;report.manaRejects++;}return;
                }
            }
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
