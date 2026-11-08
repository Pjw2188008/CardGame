using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Unknown.Battle;

namespace Unknown.Multiplayer
{
    [DisallowMultipleComponent, RequireComponent(typeof(RoomController))]
    public sealed class NetworkBattleController : MonoBehaviour
    {
        // v8 adds authoritative hand limits, discard counts and once-per-turn manual discard.
        const string Request = "UNKNOWN.Intent.v8", State = "UNKNOWN.State.v8", Ready = "UNKNOWN.Ready.v8", Lobby = "UNKNOWN.Lobby.v8", Presented = "UNKNOWN.Presented.v8";
        public BattleMap? SelectedMap { get; private set; }
        RandomMapSelection mapSelection;
        public int MapDrawCount => mapSelection?.DrawCount ?? 0;
        [SerializeField, Min(1)] float turnDuration = 40;
        public double TurnDuration => Math.Max(1, turnDuration);
        public event Action Changed;
        public event Action<BattleSnapshot> SnapshotReceived;
        public BattleSnapshot Snapshot { get; private set; }
        public bool HostReady { get; private set; }
        public bool GuestReady { get; private set; }
        public bool BothReady => HostReady && GuestReady;
        public bool LocalReady => Manager.IsHost ? HostReady : GuestReady;
        public int LocalPlayer => Snapshot?.recipient ?? -1;
        public string Failure { get; private set; } = "";
        public bool Sending { get; private set; }
        public string ActionError { get; private set; } = "";
        public int ActionErrorVersion { get; private set; }
        public double Remaining => Snapshot == null ? TurnDuration : Snapshot.winner >= 0 ? 0 : !Snapshot.clockRunning ? Snapshot.turnDuration : Math.Min(Snapshot.turnDuration, Math.Max(0, Snapshot.deadline - Manager.ServerTime.Time));
        public bool CanAct => Snapshot != null && Snapshot.clockRunning && !Snapshot.busy && !Sending && Snapshot.active == LocalPlayer && Snapshot.winner < 0 && Remaining > 0 && string.IsNullOrEmpty(Failure);
        NetworkManager Manager => GetComponent<NetworkManager>();
        RoomController room;
        CustomMessagingManager messages;
        BattleAuthority authority;
        int sequence, lastRevision = -1, previousCount;
        bool busy;
        bool clockRunning, waitingForTurnPresentation, hostPresented, guestPresented;
        double deadline;
        float nextLobby;
        void Awake() { room = GetComponent<RoomController>(); }
        void Update()
        {
            var nm = Manager;
            if (!nm.IsListening)
            {
                if (messages != null)
                {
                    if (Snapshot != null && Snapshot.winner < 0) Failure = "상대방과 연결이 종료되었습니다.";
                    Unregister(); HostReady = GuestReady = false; authority = null; Sending = false; SelectedMap=null; mapSelection?.ResetMatch(); Changed?.Invoke();
                }
                return;
            }
            if (messages == null)
            {
                messages = nm.CustomMessagingManager; Snapshot = null; Failure = ""; sequence = 0; lastRevision = -1;
                messages.RegisterNamedMessageHandler(Request, OnIntent);
                messages.RegisterNamedMessageHandler(State, OnState);
                messages.RegisterNamedMessageHandler(Ready, OnReady);
                messages.RegisterNamedMessageHandler(Lobby, OnLobby);
                messages.RegisterNamedMessageHandler(Presented, OnPresented);
            }
            if (nm.IsHost && !room.HasStarted && (previousCount != nm.ConnectedClientsIds.Count || Time.unscaledTime >= nextLobby))
            {
                if (previousCount != nm.ConnectedClientsIds.Count) HostReady = GuestReady = false;
                previousCount = nm.ConnectedClientsIds.Count; nextLobby = Time.unscaledTime + 1; SendLobby();
            }
            if (!nm.IsHost || !room.HasStarted) return;
            if (authority == null)
            {
                var mapRules=BattleMapSettings.LoadRules();
                if(mapSelection==null)mapSelection=new RandomMapSelection(new SeededRandom(NetworkBattleSmokeRunner.SeedOverride ?? Environment.TickCount),mapRules);
                SelectedMap=mapSelection.BeginMatch();
                Debug.Log($"[UNKNOWN Map] match={MapDrawCount} map={SelectedMap} hostDraws={MapDrawCount}");
                authority = new BattleAuthority(NetworkBattleSmokeRunner.SeedOverride ?? Environment.TickCount,
                    new BattleConfig { Map=SelectedMap.Value,MapRules=mapRules, StartingHand=NetworkBattleSmokeRunner.SystemsCheck ? 0 : 5, MapEvents=SelectedMap==BattleMap.JokerGame ? JokerMapSettings.LoadRules() : JokerEventDefinition.Defaults() }); PrepareTurnClock();
                busy = true; Publish(authority.Engine.DrainEvents());
            }
            if (clockRunning && !authority.Engine.State.IsOver && nm.ServerTime.Time >= deadline && !authority.Engine.State.TurnExpired)
            {
                authority.Engine.ExpireTurn(authority.Engine.State.ActivePlayer);
                // Keep an accepted action's events in order; it will publish expiry after its presentation.
                if (!busy) { busy = true; authority.AdvanceRevision(); Publish(authority.Engine.DrainEvents()); }
            }
        }
        public void ToggleReady()
        {
            if (messages == null || room.HasStarted) return;
            if (Manager.IsHost) { HostReady = !HostReady; SendLobby(); }
            else Send(Ready, 0, LocalReady ? "0" : "1");
        }
        void OnReady(ulong sender, FastBufferReader reader)
        {
            if (!Manager.IsHost || sender == 0 || !Manager.ConnectedClientsIds.Contains(sender) || room.HasStarted) return;
            if (Read(reader, out var value) && (value=="0" || value=="1")) { GuestReady = value=="1"; SendLobby(); }
        }
        void SendLobby()
        {
            foreach (var id in Manager.ConnectedClientsIds) if (id != 0) Send(Lobby, id, (HostReady ? "1" : "0") + (GuestReady ? "1" : "0"));
            Changed?.Invoke();
        }
        void OnLobby(ulong sender, FastBufferReader reader)
        {
            if (sender != 0 || Manager.IsHost || !Read(reader, out var text) || text.Length != 2 || text.Any(c=>c!='0' && c!='1')) return;
            HostReady = text[0] == '1'; GuestReady = text[1] == '1'; Changed?.Invoke();
        }
        public void Submit(BattleAction action, int first = -1, int second = -1)
        {
            if (!CanAct) return;
            var intent = new BattleIntent { action = action, first = first, second = second, turn = Snapshot.turn, revision = Snapshot.revision, sequence = ++sequence };
            Sending = true;
            ActionError = "";
            if (Manager.IsHost) Execute(0, intent); else Send(Request, 0, JsonUtility.ToJson(intent));
        }
        void OnIntent(ulong sender, FastBufferReader reader)
        {
            if (!Manager.IsHost || authority == null || sender == 0 || !Manager.ConnectedClientsIds.Contains(sender) || !Read(reader, out var json)) return;
            try { Execute(1, JsonUtility.FromJson<BattleIntent>(json)); }
            catch (Exception e) { Debug.LogWarning("[UNKNOWN] Invalid intent: " + e.Message); }
        }
        void Execute(int actor, BattleIntent intent)
        {
            // Consume valid sequence numbers even while busy, then reject without mutating rules.
            if (busy || !clockRunning || Manager.ServerTime.Time >= deadline || authority.Engine.State.IsOver)
            {
                if (intent != null) authority.RejectSequence(actor, intent.sequence);
                Reply(actor); return;
            }
            int oldTurn = authority.Engine.State.TurnNumber;
            if (!authority.Execute(actor, intent)) { Reply(actor); return; }
            if (oldTurn != authority.Engine.State.TurnNumber) PrepareTurnClock();
            busy = true; Publish(authority.Engine.DrainEvents());
        }
        public void PresentationFinished(int revision)
        {
            if (!Manager.IsHost)
            {
                if (Snapshot!=null && revision==Snapshot.revision && Snapshot.busy && !Snapshot.clockRunning)
                    Send(Presented,0,revision.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return;
            }
            if (!Manager.IsHost || authority == null || revision != authority.Revision || !busy) return;
            FinishPresentation(0,revision);
        }
        void PrepareTurnClock()
        {
            clockRunning=false; deadline=0; waitingForTurnPresentation=true; hostPresented=guestPresented=false;
        }
        void OnPresented(ulong sender,FastBufferReader reader)
        {
            if(!Manager.IsHost || sender==0 || !Manager.ConnectedClientsIds.Contains(sender) || !Read(reader,out var text) || !int.TryParse(text,out int revision))return;
            FinishPresentation(1,revision);
        }
        void FinishPresentation(int actor,int revision)
        {
            if(authority==null || !busy || revision!=authority.Revision)return;
            if(waitingForTurnPresentation)
            {
                if(actor==0)hostPresented=true; else guestPresented=true;
                if(!hostPresented || !guestPresented)return;
                waitingForTurnPresentation=false; clockRunning=!authority.Engine.State.IsOver; deadline=clockRunning ? Manager.ServerTime.Time+TurnDuration : 0;
            }
            else if(actor!=0)return;
            var pending = authority.Engine.DrainEvents();
            if (pending.Count > 0) { authority.AdvanceRevision(); Publish(pending); return; }
            if (authority.Engine.State.TurnExpired && !authority.Engine.State.IsOver)
            {
                authority.Engine.FinishExpiredTurn(authority.Engine.State.ActivePlayer); authority.AdvanceRevision();
                PrepareTurnClock(); Publish(authority.Engine.DrainEvents()); return;
            }
            busy = false; authority.AdvanceRevision(); Publish(Array.Empty<BattleEvent>());
        }
        void Reply(int actor)
        {
            var s = BattleSnapshot.For(authority.Engine, actor, authority.Revision, deadline, busy, Array.Empty<BattleEvent>(), TurnDuration, clockRunning);
            s.error = string.IsNullOrEmpty(authority.Engine.State.LastError) ? "현재 상태에서 이 행동을 할 수 없습니다." : authority.Engine.State.LastError;
            if (actor == 0) Receive(s); else foreach (var id in Manager.ConnectedClientsIds) if (id != 0) Send(State, id, JsonUtility.ToJson(s));
        }
        void Publish(IReadOnlyList<BattleEvent> events)
        {
            foreach (var id in Manager.ConnectedClientsIds)
            {
                int owner = id == 0 ? 0 : 1;
                var s = BattleSnapshot.For(authority.Engine, owner, authority.Revision, deadline, busy, events, TurnDuration, clockRunning);
                if (owner == 0) Receive(s); else Send(State, id, JsonUtility.ToJson(s));
            }
        }
        void OnState(ulong sender, FastBufferReader reader)
        {
            if (sender != 0 || Manager.IsHost || !Read(reader, out var json)) return;
            try { Receive(JsonUtility.FromJson<BattleSnapshot>(json)); }
            catch (Exception e) { Debug.LogWarning("[UNKNOWN] Invalid snapshot: " + e.Message); }
        }
        void Receive(BattleSnapshot s)
        {
            if (s == null || s.revision < lastRevision) return;
            if (s.recipient != (Manager.LocalClientId == NetworkManager.ServerClientId ? 0 : 1))
                throw new InvalidOperationException("Snapshot recipient does not match the local connection.");
            Sending = false;
            ActionError = s.error ?? "";
            if(!string.IsNullOrEmpty(ActionError))ActionErrorVersion++;
            // Equal revisions are request rejection replies; never replay their events.
            if (s.revision == lastRevision) { Changed?.Invoke(); return; }
            lastRevision = s.revision; Snapshot = s; SelectedMap=s.map; SnapshotReceived?.Invoke(s); Changed?.Invoke();
        }
        void Send(string name, ulong id, string text)
        {
            using var writer = new FastBufferWriter(System.Text.Encoding.UTF8.GetByteCount(text) * 2 + 16, Allocator.Temp, 131072);
            writer.WriteValueSafe(text);
            messages.SendNamedMessage(name, id, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }
        static bool Read(FastBufferReader reader, out string text)
        {
            text = null;
            if (reader.Length > 131072) return false;
            try { reader.ReadValueSafe(out text); return text != null && text.Length <= 60000; }
            catch { return false; }
        }
        void Unregister()
        {
            if (messages == null) return;
            messages.UnregisterNamedMessageHandler(Request); messages.UnregisterNamedMessageHandler(State);
            messages.UnregisterNamedMessageHandler(Ready); messages.UnregisterNamedMessageHandler(Lobby);
            messages.UnregisterNamedMessageHandler(Presented); messages = null;
        }
        void OnDestroy() { Unregister(); }
        public void ResetMatch()
        {
            authority = null; Snapshot = null; busy = false; Sending = false; Failure = "";
            SelectedMap=null; mapSelection?.ResetMatch(); deadline=0;
            clockRunning=waitingForTurnPresentation=hostPresented=guestPresented=false;
            lastRevision = -1; sequence = 0; HostReady = GuestReady = false; Changed?.Invoke();
        }
    }
}
