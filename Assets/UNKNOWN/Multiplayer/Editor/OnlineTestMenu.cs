using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unknown.Multiplayer.Editor
{
    [InitializeOnLoad]
    public static class OnlineTestMenu
    {
        static OnlineTestMenu() { EditorApplication.playModeStateChanged += Changed; }
        [MenuItem("UNKNOWN/Multiplayer/Preview Drag UI %&v")]
        static void PreviewDrag()
        {
            if(EditorApplication.isPlaying) { new GameObject("UNKNOWN Drag Preview").AddComponent<Unknown.Battle.BattleUIController>().PreviewDragUI(); return; }
            SessionState.SetBool("UNKNOWN.PreviewDrag",true); EditorApplication.isPlaying=true;
        }
        [MenuItem("UNKNOWN/Multiplayer/Run Drag UI Checks %&d")]
        static void RunDragUI()
        {
            if(EditorApplication.isPlaying) { new GameObject("UNKNOWN Drag UI Checks").AddComponent<Unknown.Battle.BattleUIController>().RunDragChecks(); return; }
            SessionState.SetBool("UNKNOWN.RunDragUI",true); EditorApplication.isPlaying=true;
        }
        [MenuItem("UNKNOWN/Multiplayer/Run PvP UI Regression Checks %&u")]
        static void RunUI()
        {
            if (EditorApplication.isPlaying) { new GameObject("UNKNOWN PvP UI Checks").AddComponent<Unknown.Battle.BattleUIController>().RunPvPRegressionChecks(); return; }
            SessionState.SetBool("UNKNOWN.RunPvPUI",true); EditorApplication.isPlaying=true;
        }
        [MenuItem("UNKNOWN/Multiplayer/Run Relay Host Smoke Check %&r")]
        static void Run()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            // A stale exchange code must never connect a new guest to an old test room.
            string path = Path.GetFullPath("Library/UNKNOWN-RelaySmoke.code");
            File.WriteAllText(path, "");
            SessionState.SetBool("UNKNOWN.RunRelaySmoke", true);
            EditorApplication.isPlaying = true;
        }
        static void Changed(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("UNKNOWN.PreviewDrag",false))
            { SessionState.SetBool("UNKNOWN.PreviewDrag",false); new GameObject("UNKNOWN Drag Preview").AddComponent<Unknown.Battle.BattleUIController>().PreviewDragUI(); }
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("UNKNOWN.RunDragUI",false))
            { SessionState.SetBool("UNKNOWN.RunDragUI",false); new GameObject("UNKNOWN Drag UI Checks").AddComponent<Unknown.Battle.BattleUIController>().RunDragChecks(); }
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("UNKNOWN.RunPvPUI",false))
            {
                SessionState.SetBool("UNKNOWN.RunPvPUI",false);
                new GameObject("UNKNOWN PvP UI Checks").AddComponent<Unknown.Battle.BattleUIController>().RunPvPRegressionChecks();
            }
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("UNKNOWN.RunRelaySmoke", false)) return;
            SessionState.SetBool("UNKNOWN.RunRelaySmoke", false);
            NetworkBattleSmokeRunner.Launch("host", Path.GetFullPath("Library/UNKNOWN-RelaySmoke.code"));
        }
    }
}
