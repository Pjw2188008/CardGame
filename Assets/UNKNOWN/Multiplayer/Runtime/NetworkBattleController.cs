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
        const string Request = "UNKNOWN.Intent.v1", State = "UNKNOWN.State.v1", Ready = "UNKNOWN.Ready.v1", Lobby = "UNKNOWN.Lobby.v1";
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
        public double Remaining => Snapshot == null ? 60 : Snapshot.winner >= 0 ? 0 : Math.Max(0, Snapshot.deadline - Manager.ServerTime.Time);
        public bool CanAct => Snapshot != null && !Snapshot.busy && !Sending && Snapshot.active == LocalPlayer && Snapshot.winner < 0 && Remaining > 0 && string.IsNullOrEmpty(Failure);
        NetworkManager Manager => GetComponent<NetworkManager>();
        RoomController room;
        CustomMessagingManager messages;
        BattleAuthority authority;
        int sequence, lastRevision = -1, previousCount;
        bool busy;
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
                    Unregister(); HostReady = GuestReady = false; authority = null; Sending = false; Changed?.Invoke();
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
            }
            if (nm.IsHost && !room.HasStarted && (previousCount != nm.ConnectedClientsIds.Count || Time.unscaledTime >= nextLobby))
            {
                if (previousCount != nm.ConnectedClientsIds.Count) HostReady = GuestReady = false;
                previousCount = nm.ConnectedClientsIds.Count; nextLobby = Time.unscaledTime + 1; SendLobby();
            }
            if (!nm.IsHost || !room.HasStarted) return;
            if (authority == null)
            {
                authority = new BattleAuthority(NetworkBattleSmokeRunner.SeedOverride ?? Environment.TickCount); deadline = nm.ServerTime.Time + 60;
                busy = true; Publish(authority.Engine.DrainEvents());
            }
            if (!authority.Engine.State.IsOver && nm.ServerTime.Time >= deadline && !authority.Engine.State.TurnExpired)
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
            if (Read(reader, out var value)) { GuestReady = value == "1"; SendLobby(); }
        }
        void SendLobby()
        {
            foreach (var id in Manager.ConnectedClientsIds) if (id != 0) Send(Lobby, id, (HostReady ? "1" : "0") + (GuestReady ? "1" : "0"));
            Changed?.Invoke();
        }
        void OnLobby(ulong sender, FastBufferReader reader)
        {
            if (sender != 0 || Manager.IsHost || !Read(reader, out var text) || text.Length != 2) return;
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
            if (busy || Manager.ServerTime.Time >= deadline || authority.Engine.State.IsOver)
            {
                if (intent != null) authority.RejectSequence(actor, intent.sequence);
                Reply(actor); return;
            }
            int oldTurn = authority.Engine.State.TurnNumber;
            if (!authority.Execute(actor, intent)) { Reply(actor); return; }
            if (oldTurn != authority.Engine.State.TurnNumber) deadline = Manager.ServerTime.Time + 60;
            busy = true; Publish(authority.Engine.DrainEvents());
        }
        public void PresentationFinished(int revision)
        {
            if (!Manager.IsHost || authority == null || revision != authority.Revision || !busy) return;
            var pending = authority.Engine.DrainEvents();
            if (pending.Count > 0) { authority.AdvanceRevision(); Publish(pending); return; }
            if (authority.Engine.State.TurnExpired && !authority.Engine.State.IsOver)
            {
                authority.Engine.FinishExpiredTurn(authority.Engine.State.ActivePlayer); authority.AdvanceRevision();
                deadline = Manager.ServerTime.Time + 60; Publish(authority.Engine.DrainEvents()); return;
            }
            busy = false; authority.AdvanceRevision(); Publish(Array.Empty<BattleEvent>());
        }
        void Reply(int actor)
        {
            var s = BattleSnapshot.For(authority.Engine, actor, authority.Revision, deadline, busy, Array.Empty<BattleEvent>());
            s.error = string.IsNullOrEmpty(authority.Engine.State.LastError) ? "현재 상태에서 이 행동을 할 수 없습니다." : authority.Engine.State.LastError;
            if (actor == 0) Receive(s); else foreach (var id in Manager.ConnectedClientsIds) if (id != 0) Send(State, id, JsonUtility.ToJson(s));
        }
        void Publish(IReadOnlyList<BattleEvent> events)
        {
            foreach (var id in Manager.ConnectedClientsIds)
            {
                int owner = id == 0 ? 0 : 1;
                var s = BattleSnapshot.For(authority.Engine, owner, authority.Revision, deadline, busy, events);
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
            // Equal revisions are request rejection replies; never replay their events.
            if (s.revision == lastRevision) { Changed?.Invoke(); return; }
            lastRevision = s.revision; Snapshot = s; SnapshotReceived?.Invoke(s); Changed?.Invoke();
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
            messages.UnregisterNamedMessageHandler(Ready); messages.UnregisterNamedMessageHandler(Lobby); messages = null;
        }
        void OnDestroy() { Unregister(); }
        public void ResetMatch()
        {
            authority = null; Snapshot = null; busy = false; Sending = false; Failure = "";
            lastRevision = -1; sequence = 0; HostReady = GuestReady = false; Changed?.Invoke();
        }
    }
}
