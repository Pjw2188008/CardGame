using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace Unknown.Multiplayer.Editor
{
    [InitializeOnLoad]public static class CardRedesignUiBatch
    {
        static CardRedesignUiBatch(){EditorApplication.playModeStateChanged+=Changed;if(SessionState.GetBool("Redesign.UI",false))EditorApplication.update+=Poll;}
        public static void RunBatch()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            SessionState.SetBool("Redesign.UI",true);SessionState.SetString("Redesign.UI.Start",DateTime.UtcNow.ToString("O"));EditorApplication.update+=Poll;EditorApplication.isPlaying=true;
        }
        static void Changed(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("Redesign.UI",false))new GameObject("Redesign UI Checks").AddComponent<Unknown.Battle.BattleUIController>().RunRedesignUIChecks();}
        [Serializable]sealed class Result{public bool completed;public string[] failed,errors;}
        static void Poll()
        {
            var start=DateTime.Parse(SessionState.GetString("Redesign.UI.Start",DateTime.UtcNow.ToString("O")),null,System.Globalization.DateTimeStyles.RoundtripKind);
            if((DateTime.UtcNow-start).TotalSeconds>180){SessionState.SetBool("Redesign.UI",false);EditorApplication.Exit(1);return;}
            const string path="Library/CardRedesign-UI.json";if(!File.Exists(path) || File.GetLastWriteTimeUtc(path)<=start)return;
            var result=JsonUtility.FromJson<Result>(File.ReadAllText(path));SessionState.SetBool("Redesign.UI",false);EditorApplication.update-=Poll;EditorApplication.Exit(result.completed && result.failed.Length==0 && result.errors.Length==0 ? 0 : 1);
        }
    }
}
