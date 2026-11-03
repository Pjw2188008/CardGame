using System;
using System.Text.RegularExpressions;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
namespace Unknown.Multiplayer
{
    public sealed partial class RoomController
    {
        public string ConnectionStage { get; private set; } = "Idle";
        public string LastConnectionFailure { get; private set; } = "";
        public string AuthenticationProfile => UnityServices.State == ServicesInitializationState.Initialized ? AuthenticationService.Instance.Profile : "uninitialized";
        public string AuthenticationPlayerId => UnityServices.State == ServicesInitializationState.Initialized ? AuthenticationService.Instance.PlayerId : "";
        public int ProcessId => System.Diagnostics.Process.GetCurrentProcess().Id;
        public string NetworkState() => networkManager == null ? "manager=null" : $"manager={networkManager.GetInstanceID()} transport={networkManager.NetworkConfig.NetworkTransport?.GetType().Name} listening={networkManager.IsListening} host={networkManager.IsHost} client={networkManager.IsClient} connected={networkManager.IsConnectedClient} localClient={networkManager.LocalClientId} connectedPlayers={ConnectedPlayers}";
        void Trace(string step,string detail,bool failure=false)
        {
            if (!Debug.isDebugBuild && !Application.isEditor) return;
            string text=$"[UNKNOWN Connection] pid={ProcessId} step={step} {detail}";
            if(failure)Debug.LogWarning(text); else Debug.Log(text);
        }
        static string SafeMessage(string value)
        {
            if(string.IsNullOrEmpty(value))return "(none)";
            value=Regex.Replace(value,@"(?i)(bearer\s+)[^\s""']+","$1[redacted]");
            value=Regex.Replace(value,@"(?i)((?:access[_-]?token|session[_-]?token|secret[_-]?key|authorization)\s*[""']?\s*[:=]\s*[""']?)[^\s,""'}]+","$1[redacted]");
            return value.Replace('\n',' ').Replace('\r',' ');
        }
        static string DescribeFailure(Exception error)
        {
            string result="";
            for(var e=error;e!=null;e=e.InnerException)
                result += $" type={e.GetType().Name} code="+(e is SessionException se ? se.Error+"("+(int)se.Error+")" : e is RequestFailedException rf ? rf.ErrorCode.ToString() : "n/a")+$" message={SafeMessage(e.Message)}";
            return result;
        }
    }
}
