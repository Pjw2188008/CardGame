using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace Unknown.Multiplayer
{
    // Opt-in Development Build diagnostic. Uses the same public entry points as the room buttons.
    public sealed class RoomConnectionCheck : MonoBehaviour
    {
        [Serializable] sealed class Result
        {
            public string role, profile, playerId, projectId, environment, code, stage, error;
            public int pid, sessionPlayers, connectedPlayers;
            public bool twoPlayers, uiTwoPlayers, connected, cleaned;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if(!Debug.isDebugBuild)return;
            var args=Environment.GetCommandLineArgs(); int at=Array.IndexOf(args,"-unknownRoomCheck");
            if(at<0 || at+2>=args.Length)return;
            new GameObject("UNKNOWN Explicit Room Connection Check").AddComponent<RoomConnectionCheck>().StartCoroutine(Run(args[at+1],args[at+2]));
        }
        static IEnumerator Run(string role,string exchange)
        {
            var room=FindFirstObjectByType<RoomController>();
            var result=new Result{role=role,projectId=Application.cloudProjectId};
            string output=exchange+"."+role+".json";
            string joinCode=null;
            if(role!="host")
            {
                double deadline=Time.realtimeSinceStartupAsDouble+90;
                while(string.IsNullOrEmpty(joinCode=ReadCode(exchange)) && Time.realtimeSinceStartupAsDouble<deadline)yield return new WaitForSecondsRealtime(0.1f);
                if(string.IsNullOrEmpty(joinCode)) { result.error="No host code"; File.WriteAllText(output,JsonUtility.ToJson(result,true)); Application.Quit();yield break; }
            }
            var task=role=="host" ? room.CreateRoomAsync() : room.JoinRoomAsync(joinCode);
            while(!task.IsCompleted)yield return null;
            if(role=="host" && room.Session!=null)File.WriteAllText(exchange,room.Session.Code);
            double until=Time.realtimeSinceStartupAsDouble+(room.Session==null ? 0 : 90);
            do
            {
                result.pid=room.ProcessId; result.profile=room.AuthenticationProfile; result.playerId=room.AuthenticationPlayerId;
                result.environment=room.ServicesEnvironment;
                result.code=room.Session?.Code; result.stage=room.ConnectionStage; result.error=room.LastConnectionFailure;
                result.sessionPlayers=room.Session?.PlayerCount??0; result.connectedPlayers=room.ConnectedPlayers;
                result.connected=room.Session!=null && room.ConnectedPlayers>0;
                result.twoPlayers|=result.sessionPlayers==2 && result.connectedPlayers==2;
                result.uiTwoPlayers|=FindFirstObjectByType<RoomTestUI>()?.DisplaysTwoPlayers==true;
                File.WriteAllText(output,JsonUtility.ToJson(result,true));
                if((result.twoPlayers && result.uiTwoPlayers) || File.Exists(exchange+".stop"))break;
                yield return null;
            }while(Time.realtimeSinceStartupAsDouble<until);
            if(result.twoPlayers)yield return new WaitForSecondsRealtime(8);
            var cleanup=room.LeaveRoomAsync(); while(!cleanup.IsCompleted)yield return null;
            result.cleaned=room.Session==null; File.WriteAllText(output,JsonUtility.ToJson(result,true)); Application.Quit();
        }
        internal static string ReadCode(string path)
        {
            // The other process may still be writing the exchange file. Retry without affecting the room API.
            try { using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite); using var reader=new StreamReader(stream); string code=reader.ReadToEnd().Trim(); return System.Text.RegularExpressions.Regex.IsMatch(code,@"^[A-Z0-9]{6}$") ? code : null; }
            catch(IOException) { return null; }
        }
    }
}
