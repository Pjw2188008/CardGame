using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace Unknown.Multiplayer.Editor
{
    [InitializeOnLoad]
    public static class AiCheckMenu
    {
        static AiCheckMenu(){EditorApplication.playModeStateChanged+=Changed;if(SessionState.GetBool("UNKNOWN.AiBatch",false))EditorApplication.update+=Poll;}
        [MenuItem("UNKNOWN/AI/Run AI UI Checks")]
        public static void Run(){SessionState.SetBool("UNKNOWN.AiPending",true);if(EditorApplication.isPlaying)Launch();else EditorApplication.isPlaying=true;}
        static void Changed(PlayModeStateChange s){if(s==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("UNKNOWN.AiPending",false))Launch();}
        static void Launch(){SessionState.SetBool("UNKNOWN.AiPending",false);var room=UnityEngine.Object.FindFirstObjectByType<RoomTestUI>();if(room==null)throw new InvalidOperationException("Open SampleScene first");room.RunAiChecksFromTitle();}
        public static void RunBatch(){UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");SessionState.SetBool("UNKNOWN.AiBatch",true);SessionState.SetString("UNKNOWN.AiStarted",DateTime.UtcNow.ToString("O"));Run();EditorApplication.update+=Poll;}
        [Serializable]sealed class Result{public bool completed;public string[] failed,errors;}
        static void Poll(){var start=DateTime.Parse(SessionState.GetString("UNKNOWN.AiStarted",DateTime.UtcNow.ToString("O")),null,System.Globalization.DateTimeStyles.RoundtripKind);const string path="Library/UNKNOWN-AI-Checks.json";
            if((DateTime.UtcNow-start).TotalSeconds>240){SessionState.SetBool("UNKNOWN.AiBatch",false);EditorApplication.Exit(1);return;}
            if(!File.Exists(path) || File.GetLastWriteTimeUtc(path)<=start)return;var r=JsonUtility.FromJson<Result>(File.ReadAllText(path));SessionState.SetBool("UNKNOWN.AiBatch",false);EditorApplication.update-=Poll;EditorApplication.Exit(r.completed && r.failed.Length==0 && r.errors.Length==0 ? 0 : 1);}
    }
}
