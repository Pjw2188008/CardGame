using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace Unknown.Multiplayer.Editor
{
    /// <summary>Run both offline entry paths then hand off to the existing multiplayer UI regression suite.</summary>
    [InitializeOnLoad]
    public static class OfflineModesCheckMenu
    {
        const string Active="UNKNOWN.OfflineModesBatch";
        static OfflineModesCheckMenu(){EditorApplication.playModeStateChanged+=Changed;if(SessionState.GetBool(Active,false))EditorApplication.update+=Poll;}
        [MenuItem("UNKNOWN/AI/Run AI Tutorial and Regression Suite")]
        public static void RunBatch()
        {
            if(!EditorApplication.isPlaying)UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            SessionState.SetBool(Active,true);SessionState.SetInt("UNKNOWN.OfflinePhase",0);SessionState.SetString("UNKNOWN.OfflineStarted",DateTime.UtcNow.ToString("O"));
            EditorApplication.update+=Poll;if(EditorApplication.isPlaying)Launch();else EditorApplication.isPlaying=true;
        }
        static void Changed(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active,false))Launch();}
        static void Launch(){var ui=UnityEngine.Object.FindFirstObjectByType<RoomTestUI>();if(ui==null)throw new InvalidOperationException("SampleScene RoomTestUI required");if(SessionState.GetInt("UNKNOWN.OfflinePhase",0)==0)ui.RunAiChecksFromTitle();else ui.RunTutorialChecksFromTitle();}
        [Serializable]sealed class Result{public bool completed;public string[] failed,errors;}
        static void Poll()
        {
            int phase=SessionState.GetInt("UNKNOWN.OfflinePhase",0);var start=DateTime.Parse(SessionState.GetString("UNKNOWN.OfflineStarted",DateTime.UtcNow.ToString("O")),null,System.Globalization.DateTimeStyles.RoundtripKind);
            string path=phase==0 ? "Library/UNKNOWN-AI-Checks.json" : "Library/UNKNOWN-Tutorial-Checks.json";
            if((DateTime.UtcNow-start).TotalSeconds>400){SessionState.SetBool(Active,false);EditorApplication.Exit(1);return;}
            if(!File.Exists(path) || File.GetLastWriteTimeUtc(path)<=start)return;
            var r=JsonUtility.FromJson<Result>(File.ReadAllText(path));if(!r.completed || r.failed.Length>0 || r.errors.Length>0){SessionState.SetBool(Active,false);EditorApplication.Exit(1);return;}
            if(phase==0){SessionState.SetInt("UNKNOWN.OfflinePhase",1);SessionState.SetString("UNKNOWN.OfflineStarted",DateTime.UtcNow.ToString("O"));Launch();return;}
            SessionState.SetBool(Active,false);EditorApplication.update-=Poll;AttackCheckMenu.RunSuiteBatch();
        }
    }
}
