using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace Unknown.Multiplayer.Editor
{
    [InitializeOnLoad]
    public static class TitleCheckMenu
    {
        static TitleCheckMenu(){EditorApplication.playModeStateChanged+=Changed;if(SessionState.GetBool("UNKNOWN.TitleBatch",false))EditorApplication.update+=Poll;}
        [MenuItem("UNKNOWN/Maps/Connect Title Background")]
        public static void Connect()
        {
            const string path="Assets/UNKNOWN/Multiplayer/Resources/RoomVisualSettings.asset";
            var settings=AssetDatabase.LoadAssetAtPath<RoomVisualSettings>(path);
            if(settings==null){settings=ScriptableObject.CreateInstance<RoomVisualSettings>();AssetDatabase.CreateAsset(settings,path);}
            settings.titleBackground=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Title/Title.png") ?? throw new InvalidOperationException("Assets/Title/Title.png is missing");
            settings.menuBackground=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Title/back.png") ?? throw new InvalidOperationException("Assets/Title/back.png is missing");
            EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
        }
        public static void RunBatch()
        {
            Connect();UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            SessionState.SetBool("UNKNOWN.TitleBatch",true);SessionState.SetString("UNKNOWN.TitleStarted",DateTime.UtcNow.ToString("O"));
            EditorApplication.update+=Poll;EditorApplication.isPlaying=true;
        }
        static void Changed(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("UNKNOWN.TitleBatch",false))UnityEngine.Object.FindFirstObjectByType<RoomTestUI>().RunTitleChecks();}
        [Serializable] sealed class Report{public bool completed;public string[] failed,errors;}
        static void Poll()
        {
            var start=DateTime.Parse(SessionState.GetString("UNKNOWN.TitleStarted",DateTime.UtcNow.ToString("O")),null,System.Globalization.DateTimeStyles.RoundtripKind);
            if((DateTime.UtcNow-start).TotalSeconds>150){SessionState.SetBool("UNKNOWN.TitleBatch",false);EditorApplication.Exit(1);return;}
            const string path="Library/UNKNOWN-Title-Checks.json";if(!File.Exists(path) || File.GetLastWriteTimeUtc(path)<=start)return;
            var report=JsonUtility.FromJson<Report>(File.ReadAllText(path));SessionState.SetBool("UNKNOWN.TitleBatch",false);EditorApplication.update-=Poll;
            EditorApplication.Exit(report.completed && report.failed.Length==0 && report.errors.Length==0 ? 0 : 1);
        }
    }
}
