using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace Unknown.Multiplayer.Editor
{
    [InitializeOnLoad]
    public static class TutorialCheckMenu
    {
        static TutorialCheckMenu(){EditorApplication.playModeStateChanged+=Changed;if(SessionState.GetBool("UNKNOWN.TutorialBatch",false))EditorApplication.update+=Poll;}
        [MenuItem("UNKNOWN/Tutorial/Run Tutorial Checks")]
        public static void Run(){SessionState.SetBool("UNKNOWN.TutorialChecksPending",true);if(EditorApplication.isPlaying)Launch();else EditorApplication.isPlaying=true;}
        static void Changed(PlayModeStateChange s){if(s==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("UNKNOWN.TutorialChecksPending",false))Launch();}
        static void Launch(){SessionState.SetBool("UNKNOWN.TutorialChecksPending",false);var room=UnityEngine.Object.FindFirstObjectByType<RoomTestUI>();if(room==null)throw new InvalidOperationException("Open SampleScene before running tutorial checks");room.RunTutorialChecksFromTitle();}
        public static void RunBatch(){UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");SessionState.SetBool("UNKNOWN.TutorialBatch",true);SessionState.SetString("UNKNOWN.TutorialBatchStart",DateTime.UtcNow.ToString("O"));Run();EditorApplication.update+=Poll;}
        [Serializable]sealed class Result{public bool completed;public string[] failed,errors;}
        static void Poll(){var start=DateTime.Parse(SessionState.GetString("UNKNOWN.TutorialBatchStart",DateTime.UtcNow.ToString("O")),null,System.Globalization.DateTimeStyles.RoundtripKind);const string path="Library/UNKNOWN-Tutorial-Checks.json";
            if((DateTime.UtcNow-start).TotalSeconds>240){SessionState.SetBool("UNKNOWN.TutorialBatch",false);EditorApplication.Exit(1);return;}
            if(!File.Exists(path) || File.GetLastWriteTimeUtc(path)<=start)return;var r=JsonUtility.FromJson<Result>(File.ReadAllText(path));SessionState.SetBool("UNKNOWN.TutorialBatch",false);EditorApplication.update-=Poll;EditorApplication.Exit(r.completed && r.failed.Length==0 && r.errors.Length==0 ? 0 : 1);
        }
    }
}
