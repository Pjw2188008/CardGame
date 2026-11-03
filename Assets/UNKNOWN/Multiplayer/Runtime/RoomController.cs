using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unknown.Multiplayer
{
    /// <summary>One private MPS session and its NGO connection, using the scene's existing manager.</summary>
    [DisallowMultipleComponent]
    public sealed class RoomController : MonoBehaviour
    {
        const string StartMessage = "UNKNOWN.Start.v1";
        const string ReturnRequest = "UNKNOWN.ReturnRequest.v2", ReturnMessage = "UNKNOWN.ReturnLobby.v2";
        const string SessionType = "unknown-1vs1";
        [SerializeField] NetworkManager networkManager;
        [Tooltip("Leave empty to use the built-in connection test screen. Otherwise add this scene to Build Profiles.")]
        [SerializeField] string gameplayScene;
        [Tooltip("Optional Authentication profile for Editor + standalone tests on the same PC. Use letters/numbers only.")]
        [SerializeField] string authenticationProfile;

        public event Action Changed;
        public event Action GameStarted;
        public ISession Session { get; private set; }
        public bool Busy { get; private set; }
        public bool HasStarted { get; private set; }
        public string Status { get; private set; } = "방을 만들거나 초대 코드를 입력하세요.";
        public string Error { get; private set; } = "";
        public int ConnectedPlayers => networkManager != null && networkManager.IsListening
            ? (networkManager.IsHost ? networkManager.ConnectedClientsIds.Count : (networkManager.IsConnectedClient ? Session?.PlayerCount ?? 0 : 0)) : 0;
        public bool CanStart => !Busy && !HasStarted && Session != null && Session.IsHost
            && networkManager.IsHost && Session.PlayerCount == 2 && ConnectedPlayers == 2 && connectionLoss == null
            && GetComponent<NetworkBattleController>().BothReady;
        public string GameplayScene { get => gameplayScene; set => gameplayScene = value; }

        readonly HashSet<ulong> pendingApprovals = new HashSet<ulong>();
        readonly HashSet<ulong> establishedClients = new HashSet<ulong>();
        CancellationTokenSource lifetime;
        bool callbacksInstalled;
        bool previousApproval;
        bool closing;
        bool destroyed;
        string connectionLoss;
        float refreshAt;
        bool refreshing;
        Task initialization;

        void Awake()
        {
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(Application.exitCancellationToken);
            if (networkManager == null) networkManager = GetComponent<NetworkManager>();
            if (GetComponent<NetworkBattleController>() == null) gameObject.AddComponent<NetworkBattleController>();
        }

        void Update()
        {
            // SDK/NGO callbacks can run during shutdown. Defer teardown outside their call stacks.
            if (connectionLoss != null && !Busy && !closing)
            {
                string reason = connectionLoss;
                connectionLoss = null;
                _ = CloseAfterLossAsync(reason);
            }
            if (Session != null && !Busy && !closing && !refreshing && Time.unscaledTime >= refreshAt)
            {
                refreshAt = Time.unscaledTime + 5f;
                _ = RefreshAsync();
            }
        }

        public Task CreateRoomAsync() => EnterAsync(null);
        public Task JoinRoomAsync(string code) => EnterAsync(code ?? "");

        async Task EnterAsync(string code)
        {
            if (Busy || closing || Session != null) return;
            if (code != null)
            {
                code = code.Trim().ToUpperInvariant();
                if (code.Length == 0 || code.Length > 16 || !IsCode(code))
                {
                    SetError("방 코드는 영문과 숫자로 입력하세요.");
                    return;
                }
            }
            Busy = true;
            Error = "";
            Status = code == null ? "로그인 및 Relay 방 생성 중…" : "로그인 및 방 연결 중…";
            Notify();
            try
            {
                ValidateManager();
                initialization ??= InitializeAsync();
                try { await initialization; }
                catch { initialization = null; throw; }
                lifetime.Token.ThrowIfCancellationRequested();
                InstallCallbacks();
                pendingApprovals.Clear();
                establishedClients.Clear();
                HasStarted = false;
                connectionLoss = null;
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(45));
                ISession session;
                if (code == null)
                {
                    var options = new SessionOptions { Name = "UNKNOWN 1vs1", Type = SessionType, MaxPlayers = 2, IsPrivate = true }
                        .WithRelayNetwork();
                    session = await MultiplayerService.Instance.CreateSessionAsync(options, timeout.Token);
                }
                else
                {
                    session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code,
                        new JoinSessionOptions { Type = SessionType }, timeout.Token);
                }
                Session = session;
                Subscribe(session);
                lifetime.Token.ThrowIfCancellationRequested();
                if (!networkManager.IsConnectedClient) throw new InvalidOperationException("Relay 연결을 완료하지 못했습니다.");
                if (session.MaxPlayers != 2) throw new InvalidOperationException("UNKNOWN 1vs1 방 코드가 아닙니다.");
                networkManager.CustomMessagingManager.RegisterNamedMessageHandler(StartMessage, OnStartMessage);
                networkManager.CustomMessagingManager.RegisterNamedMessageHandler(ReturnRequest, OnReturnRequest);
                networkManager.CustomMessagingManager.RegisterNamedMessageHandler(ReturnMessage, OnReturnMessage);
                refreshAt = Time.unscaledTime + 5f;
                Status = "대기실에 연결되었습니다. 두 명이 모이면 방장이 시작할 수 있습니다.";
                Debug.Log($"[UNKNOWN] Relay connected. Host={session.IsHost}, members={session.PlayerCount}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                await CleanupAsync();
                if (!destroyed) SetError(Explain(e));
            }
            finally
            {
                Busy = false;
                Notify();
            }
        }

        static bool IsCode(string code)
        {
            foreach (char c in code) if (!(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9')) return false;
            return true;
        }

        async Task InitializeAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            var auth = AuthenticationService.Instance;
            // Separate cached identities when testing two processes on the same machine.
            string profile = authenticationProfile;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-unknownProfile") profile = args[i + 1];
            if (!auth.IsSignedIn)
            {
                if (!string.IsNullOrWhiteSpace(profile)) auth.SwitchProfile(profile);
                await auth.SignInAnonymouslyAsync();
            }
        }

        void ValidateManager()
        {
            var managers = FindObjectsByType<NetworkManager>(FindObjectsSortMode.None);
            if (managers.Length != 1 || networkManager == null || NetworkManager.Singleton != networkManager)
                throw new InvalidOperationException("씬에 활성 NetworkManager가 정확히 하나 있어야 합니다. 기존 NetworkManager에 RoomController를 연결하세요.");
            if (!(networkManager.NetworkConfig.NetworkTransport is UnityTransport))
                throw new InvalidOperationException("기존 NetworkManager의 Network Transport에 UnityTransport를 지정하세요.");
            if (networkManager.IsListening || networkManager.ShutdownInProgress)
                throw new InvalidOperationException("이전 네트워크 연결이 종료된 후 다시 시도하세요.");
            if (!callbacksInstalled && networkManager.ConnectionApprovalCallback != null)
                throw new InvalidOperationException("기존 ConnectionApprovalCallback이 있습니다. RoomController의 2인 승인 규칙과 통합해야 합니다.");
        }

        void InstallCallbacks()
        {
            if (callbacksInstalled) return;
            previousApproval = networkManager.NetworkConfig.ConnectionApproval;
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = Approve;
            networkManager.OnClientConnectedCallback += OnConnected;
            networkManager.OnClientDisconnectCallback += OnDisconnected;
            networkManager.OnTransportFailure += OnTransportFailure;
            networkManager.OnClientStopped += OnNetworkStopped;
            networkManager.OnServerStopped += OnNetworkStopped;
            callbacksInstalled = true;
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            // Include pending approvals so simultaneous requests cannot claim the last slot twice.
            bool allowed = !closing && !HasStarted && connectionLoss == null
                && networkManager.ConnectedClientsIds.Count + pendingApprovals.Count < 2;
            response.Approved = allowed;
            response.CreatePlayerObject = allowed && networkManager.NetworkConfig.PlayerPrefab != null;
            response.Pending = false;
            response.Reason = allowed ? "" : "방이 가득 찼거나 이미 시작되었습니다.";
            if (allowed) pendingApprovals.Add(request.ClientNetworkId);
        }

        void OnConnected(ulong id) { pendingApprovals.Remove(id); establishedClients.Add(id); Notify(); }
        void OnDisconnected(ulong id)
        {
            pendingApprovals.Remove(id);
            bool wasConnected = establishedClients.Remove(id);
            if (Session == null || closing) return;
            // This connection prototype ends the room on a peer loss; no implicit host migration.
            if ((networkManager.IsHost && wasConnected && HasStarted) || (!networkManager.IsHost && id == networkManager.LocalClientId))
                connectionLoss = "플레이어 연결이 해제되어 방을 종료했습니다. 새 방을 만들어 다시 연결하세요.";
            else if (networkManager.IsHost && wasConnected) Status = "참가자의 연결이 종료되었습니다. 새 참가자를 기다립니다.";
            Notify();
        }
        void OnTransportFailure() { if (!closing) connectionLoss = "Relay 네트워크 연결에 실패했습니다. 인터넷 연결을 확인하세요."; }
        void OnNetworkStopped(bool wasHost) { if (Session != null && !closing) connectionLoss = "네트워크 연결이 종료되었습니다."; }
        void OnSessionEnded() { if (!closing) connectionLoss = "방이 종료되었거나 방에서 나갔습니다."; }
        void OnHostChanged(string host) { if (!closing) connectionLoss = "방장이 나가 방을 종료합니다. 새 방으로 다시 연결하세요."; }
        void OnStateChanged(SessionState state)
        {
            if (state == SessionState.Deleted) OnSessionEnded();
            else if (state == SessionState.Disconnected && !closing)
                connectionLoss = "세션 서비스 연결이 해제되었습니다. 다시 접속하세요.";
            Notify();
        }

        void Subscribe(ISession session)
        {
            session.Changed += Notify;
            session.RemovedFromSession += OnSessionEnded;
            session.Deleted += OnSessionEnded;
            session.SessionHostChanged += OnHostChanged;
            session.StateChanged += OnStateChanged;
        }
        void Unsubscribe(ISession session)
        {
            session.Changed -= Notify;
            session.RemovedFromSession -= OnSessionEnded;
            session.Deleted -= OnSessionEnded;
            session.SessionHostChanged -= OnHostChanged;
            session.StateChanged -= OnStateChanged;
        }

        public async Task StartGameAsync()
        {
            if (!CanStart) return;
            Busy = true;
            Error = "";
            Notify();
            var session = Session;
            try
            {
                if (!string.IsNullOrWhiteSpace(gameplayScene) &&
                    (!networkManager.NetworkConfig.EnableSceneManagement || !Application.CanStreamedLevelBeLoaded(gameplayScene)))
                    throw new InvalidOperationException("게임 씬을 Build Profiles에 추가하고 Enable Scene Management를 켜세요.");
                var host = session.AsHost();
                host.IsLocked = true;
                await host.SavePropertiesAsync();
                if (Session != session || connectionLoss != null || ConnectedPlayers != 2 || session.PlayerCount != 2)
                    throw new InvalidOperationException("두 플레이어의 연결이 모두 완료되어야 시작할 수 있습니다.");
                if (!string.IsNullOrWhiteSpace(gameplayScene))
                {
                    var result = networkManager.SceneManager.LoadScene(gameplayScene, LoadSceneMode.Single);
                    if (result != SceneEventProgressStatus.Started)
                        throw new InvalidOperationException($"게임 씬 로드 실패: {result}");
                }
                using var writer = new FastBufferWriter(1, Unity.Collections.Allocator.Temp);
                writer.WriteValueSafe((byte)1);
                networkManager.CustomMessagingManager.SendNamedMessageToAll(StartMessage, writer, NetworkDelivery.ReliableSequenced);
                MarkStarted();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Session == session && session.IsHost && connectionLoss == null)
                {
                    try { session.AsHost().IsLocked = false; await session.AsHost().SavePropertiesAsync(); }
                    catch (Exception unlockError) { Debug.LogException(unlockError); connectionLoss = "방 상태 복구에 실패해 방을 종료합니다."; }
                }
                SetError(Explain(e));
            }
            finally { Busy = false; Notify(); }
        }

        void OnStartMessage(ulong sender, FastBufferReader reader)
        {
            if (Session == null || sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out byte version);
            if (version == 1) MarkStarted();
        }
        void MarkStarted()
        {
            if (HasStarted) return;
            HasStarted = true;
            Status = "두 플레이어의 연결과 게임 시작 신호를 확인했습니다.";
            Notify();
            GameStarted?.Invoke();
        }
        public async Task ReturnToLobbyAsync()
        {
            var battle = GetComponent<NetworkBattleController>();
            if (Busy || closing || Session == null || !HasStarted || battle.Snapshot == null || battle.Snapshot.winner < 0) return;
            if (!networkManager.IsHost)
            {
                using var request = new FastBufferWriter(1, Unity.Collections.Allocator.Temp);
                request.WriteValueSafe((byte)1);
                networkManager.CustomMessagingManager.SendNamedMessage(ReturnRequest, NetworkManager.ServerClientId, request, NetworkDelivery.ReliableSequenced);
                return;
            }
            Busy = true; Notify();
            try
            {
                Session.AsHost().IsLocked = false;
                await Session.AsHost().SavePropertiesAsync();
                using var writer = new FastBufferWriter(1, Unity.Collections.Allocator.Temp);
                writer.WriteValueSafe((byte)1);
                networkManager.CustomMessagingManager.SendNamedMessageToAll(ReturnMessage, writer, NetworkDelivery.ReliableSequenced);
                ResetLobby();
            }
            catch (Exception e) { SetError(Explain(e)); }
            finally { Busy = false; Notify(); }
        }
        void OnReturnRequest(ulong sender, FastBufferReader reader)
        {
            if (networkManager.IsHost && sender != NetworkManager.ServerClientId && establishedClients.Contains(sender)) _ = ReturnToLobbyAsync();
        }
        void OnReturnMessage(ulong sender, FastBufferReader reader)
        { if (sender == NetworkManager.ServerClientId && Session != null && HasStarted) ResetLobby(); }
        void ResetLobby()
        {
            HasStarted = false; GetComponent<NetworkBattleController>().ResetMatch();
            Status = "대기실로 돌아왔습니다. 다시 준비해 주세요."; Notify();
        }

        public async Task LeaveRoomAsync()
        {
            if (Busy || closing) return;
            Busy = true;
            Notify();
            try { await CleanupAsync(); Status = "방에서 나왔습니다."; Error = ""; }
            finally { Busy = false; Notify(); }
        }
        async Task CloseAfterLossAsync(string reason)
        {
            Busy = true;
            Notify();
            try { await CleanupAsync(); SetError(reason); }
            finally { Busy = false; Notify(); }
        }

        async Task CleanupAsync()
        {
            closing = true;
            var session = Session;
            Session = null;
            HasStarted = false;
            connectionLoss = null;
            pendingApprovals.Clear();
            establishedClients.Clear();
            if (networkManager != null && networkManager.CustomMessagingManager != null)
            {
                networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(StartMessage);
                networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(ReturnRequest);
                networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(ReturnMessage);
            }
            try
            {
                if (session != null)
                {
                    Unsubscribe(session);
                    // Delete rather than migrate a host's private two-player room.
                    try
                    {
                        Task leave = session.IsHost ? session.AsHost().DeleteAsync() : session.LeaveAsync();
                        await AwaitCleanupAsync(leave);
                    }
                    catch (Exception e) { Debug.LogWarning($"[UNKNOWN] Session cleanup: {e.Message}"); }
                }
            }
            finally
            {
                if (networkManager != null && (networkManager.IsListening || networkManager.IsClient)) networkManager.Shutdown();
                closing = false;
            }
        }

        static async Task AwaitCleanupAsync(Task cleanup)
        {
            if (await Task.WhenAny(cleanup, Task.Delay(8000)) == cleanup) { await cleanup; return; }
            // Observe late failures instead of allowing unobserved task exceptions.
            _ = ObserveCleanupAsync(cleanup);
            Debug.LogWarning("[UNKNOWN] Session cleanup timed out; local transport will shut down.");
        }
        static async Task ObserveCleanupAsync(Task task)
        {
            try { await task; } catch (Exception e) { Debug.LogWarning($"[UNKNOWN] Late cleanup: {e.Message}"); }
        }
        async Task RefreshAsync()
        {
            refreshing = true;
            var session = Session;
            try { await session.RefreshAsync(); }
            catch (SessionException e) when (e.Error == SessionError.SessionNotFound || e.Error == SessionError.SessionDeleted)
            { if (Session == session) OnSessionEnded(); }
            catch (Exception e) { Debug.LogWarning($"[UNKNOWN] Session refresh: {e.Message}"); }
            finally { refreshing = false; Notify(); }
        }

        public void CopyCode()
        {
            if (Session == null) return;
            GUIUtility.systemCopyBuffer = Session.Code;
            Status = "방 코드를 복사했습니다.";
            Notify();
        }
        void SetError(string message) { Error = message; Status = "다시 시도할 수 있습니다."; Notify(); }
        void Notify() { if (!destroyed) Changed?.Invoke(); }

        static string Explain(Exception e)
        {
            if (e is OperationCanceledException) return "연결 시간이 초과되었거나 작업이 취소되었습니다. 다시 시도하세요.";
            if (e is SessionException se)
            {
                switch (se.Error)
                {
                    case SessionError.SessionNotFound:
                    case SessionError.SessionDeleted: return "유효하지 않거나 종료된 방 코드입니다.";
                    case SessionError.InvalidParameter: return "방 코드 또는 요청 형식을 확인하세요.";
                    case SessionError.RateLimitExceeded: return "요청이 너무 많습니다. 잠시 후 다시 시도하세요.";
                    case SessionError.NetworkManagerStartFailed:
                    case SessionError.NetworkSetupFailed: return "Relay 연결에 실패했습니다. 인터넷과 방 상태를 확인하세요.";
                }
                // MPS 2.4.1 maps lobby full/locked to Unknown without preserving the inner exception.
                string detail = se.Message.ToLowerInvariant();
                if (detail.Contains("full")) return "이미 2명이 접속한 방입니다.";
                if (detail.Contains("locked")) return "이미 게임이 시작되었거나 잠긴 방입니다.";
                if (detail.Contains("not found") || detail.Contains("invalid join code")) return "유효하지 않은 방 코드입니다.";
                return $"방 연결 실패 ({se.Error}). 방 코드와 Unity Services 설정을 확인하세요.\n{se.Message}";
            }
            if (e is InvalidOperationException || e is ArgumentException) return e.Message;
            return $"로그인 또는 네트워크 요청에 실패했습니다. 인터넷과 Unity Services 설정을 확인하세요.\n{e.Message}";
        }

        void OnDestroy()
        {
            destroyed = true;
            lifetime?.Cancel();
            lifetime?.Dispose();
            if (Session != null) Unsubscribe(Session);
            // MPS already handles Application.quitting; no async-void quit handler is necessary.
            if (callbacksInstalled && networkManager != null)
            {
                networkManager.OnClientConnectedCallback -= OnConnected;
                networkManager.OnClientDisconnectCallback -= OnDisconnected;
                networkManager.OnTransportFailure -= OnTransportFailure;
                networkManager.OnClientStopped -= OnNetworkStopped;
                networkManager.OnServerStopped -= OnNetworkStopped;
                if (networkManager.ConnectionApprovalCallback == Approve)
                {
                    networkManager.ConnectionApprovalCallback = null;
                    networkManager.NetworkConfig.ConnectionApproval = previousApproval;
                }
            }
        }
    }
}
