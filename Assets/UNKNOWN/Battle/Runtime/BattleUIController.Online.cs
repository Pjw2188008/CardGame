using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unknown.Multiplayer;

namespace Unknown.Battle
{
    public sealed partial class BattleUIController
    {
        NetworkBattleController online;
        Action returned;
        readonly Queue<BattleSnapshot> snapshots = new Queue<BattleSnapshot>();
        bool presenting;
        string shownActionError = "";
        int appliedViewRevision = -1, lastViewRefreshFrame = -1;
        public bool IsPresenting => actionBusy || transitioning || presenting;
        int ViewPlayer
        {
            get
            {
#if UNITY_EDITOR
                if (checkRecipient >= 0) return checkRecipient;
#endif
                return online == null ? engine.State.ActivePlayer : online.LocalPlayer;
            }
        }
        public void ConfigureOnline(NetworkBattleController controller, Action onReturn)
        { online = controller; returned = onReturn; }
        void Dispatch(BattleAction action, int first, int second, Func<bool> localCommand, string message)
        {
            if (online == null) RunCommand(localCommand, message);
            else if (!Blocked()) { online.Submit(action, first, second); ClearSelection(); feedback = "Host 응답을 기다리는 중"; }
        }
        void StartOnline()
        {
            revealHands = false; passOverlay.SetActive(false); resultOverlay.SetActive(false);
            online.SnapshotReceived += EnqueueSnapshot;
            if (online.Snapshot != null) EnqueueSnapshot(online.Snapshot);
        }
        void EnqueueSnapshot(BattleSnapshot s)
        {
            snapshots.Enqueue(s);
            if (!presenting) StartCoroutine(PresentSnapshots());
        }
        IEnumerator PresentSnapshots()
        {
            presenting = true;
            while (snapshots.Count > 0)
            {
                var s = snapshots.Dequeue(); actionBusy = true;
                if (engine == null) { engine = BattleEngine.CreateView(s); PrepareHandPlayback(s.events.Select(e => e.ToEvent()).ToArray()); Refresh(); }
                else engine.ApplyView(s);
                // Authoritative progress/result changes now; visual playback never determines victory.
                RefreshMana(); RefreshUnknownProgress();
                // Existing views remain bound during animation, including units destroyed by this action.
                var entries = s.events.Select(e => e.ToEvent()).ToArray();
                ObserveAttackResult(entries);
                PrepareHandPlayback(entries);
                foreach (var e in entries) if (e.Kind == BattleEventKind.TurnStart)
                    yield return animations.Banner(e.Owner == ViewPlayer ? "YOUR TURN" : "OPPONENT TURN", gold, 0.5f);
                yield return animations.Play(entries);
                animations.ClearAction(); ClearSelection(); actionBusy = false;
                feedback = s.active == ViewPlayer ? "카드를 선택하세요" : "상대방의 턴입니다";
                Refresh();
                appliedViewRevision = s.revision; lastViewRefreshFrame = Time.frameCount;
                if (engine.State.IsOver) ShowWinner();
                // Only Host presentation completion gates the next accepted action. Remote animation never changes rules.
                online.PresentationFinished(s.revision);
            }
            presenting = false;
        }
        void UpdateOnline()
        {
            if (online.ActionError != shownActionError)
            { shownActionError = online.ActionError; if (!string.IsNullOrEmpty(shownActionError)) { awaitingAttack=-1; AttackState=AttackDragState.Cancelled; EndAttackDrag(); Toast(shownActionError); } }
            if (!string.IsNullOrEmpty(online.Failure))
            { awaitingAttack=-1; EndAttackDrag(); winnerText.text = online.Failure; resultOverlay.SetActive(true); }
            var remaining = online.Remaining;
            timerText.text = online.Snapshot?.winner >= 0 ? "GAME OVER" : Math.Ceiling(remaining).ToString("00") + "s";
            timerText.color = remaining <= 10 ? new Color(1, 0.3f, 0.3f) : Color.white;
            timerFill.color = timerText.color; timerFill.rectTransform.anchorMax = new Vector2((float)(remaining / 60), 1);
            RefreshControls();
            returnLobby.interactable = online.Snapshot?.winner >= 0 && online.GetComponent<RoomController>().Session != null && string.IsNullOrEmpty(online.Failure);
        }
        async void ReturnToLobby()
        {
            if (online == null) return;
            returnLobby.interactable = false;
            await online.GetComponent<RoomController>().ReturnToLobbyAsync();
        }
        async void ReturnToMenu()
        {
            if (online != null) await online.GetComponent<RoomController>().LeaveRoomAsync();
            returned?.Invoke(); Destroy(gameObject);
        }
        void StopOnline() { if (online != null) online.SnapshotReceived -= EnqueueSnapshot; }
        internal bool? MatchesNetworkView()
        {
            if (engine == null || online?.Snapshot == null || IsPresenting || appliedViewRevision != online.Snapshot.revision || Time.frameCount <= lastViewRefreshFrame) return null;
            for (int owner=0;owner<2;owner++)
                for(int slot=0;slot<engine.Config.FieldSlots;slot++)
                {
                    var c=engine.State.Players[owner].Board[slot];
                    var expected=online.Snapshot.players[owner].board[slot];
                    if ((c!=null)!=(expected?.occupied==true)) return false;
                    if (c!=null && (c.InstanceID!=expected.id || c.Attack!=expected.attack || c.CurrentHP!=expected.hp
                        || !cardViews.TryGetValue(c.InstanceID,out var view) || view.parent!=(owner==ViewPlayer ? ownBoard : enemyBoard))) return false;
                }
            return ownBoard.gameObject.activeInHierarchy && enemyBoard.gameObject.activeInHierarchy
                && ownBoard.Cast<Transform>().Count(t=>t.gameObject.activeSelf)==5 && enemyBoard.Cast<Transform>().Count(t=>t.gameObject.activeSelf)==5
                && ownInfo.text.Contains("플레이어 "+(ViewPlayer+1)) && opponentInfo.text.Contains("플레이어 "+(2-ViewPlayer));
        }
        internal bool MatchesTerminalResult()
        {
            if (online?.Snapshot == null || !engine.State.IsOver || !resultOverlay.activeSelf) return false;
            bool won=online.Snapshot.winner==ViewPlayer;
            string expected=online.Snapshot.victoryReason==VictoryReason.UnknownComplete
                ? won ? "VICTORY — UNKNOWN COMPLETE" : "DEFEAT — OPPONENT COMPLETED UNKNOWN"
                : won ? "VICTORY — OPPONENT HP ZERO" : "DEFEAT — YOUR HP ZERO";
            return winnerText.text==expected && timerText.text=="GAME OVER" && online.Remaining==0
                && !endTurn.interactable && !combineButton.interactable;
        }
    }
}
