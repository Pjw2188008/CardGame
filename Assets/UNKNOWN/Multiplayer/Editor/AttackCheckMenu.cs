using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace Unknown.Multiplayer.Editor
{
    [InitializeOnLoad]
    public static class AttackCheckMenu
    {
        static AttackCheckMenu() { EditorApplication.playModeStateChanged+=Changed; if(SessionState.GetBool("UNKNOWN.AttackSuiteBatch",false))EditorApplication.update+=PollSuite; }
        [MenuItem("UNKNOWN/Multiplayer/Run Attack UI Checks %&a")]
        public static void Run()
        {
            if(EditorApplication.isPlaying) { new GameObject("UNKNOWN Attack UI Checks").AddComponent<Unknown.Battle.BattleUIController>().RunAttackChecks(); return; }
            SessionState.SetBool("UNKNOWN.RunAttackUI",true); EditorApplication.isPlaying=true;
        }
        static void Changed(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("UNKNOWN.RunAttackUI",false))
            { SessionState.SetBool("UNKNOWN.RunAttackUI",false); new GameObject("UNKNOWN Attack UI Checks").AddComponent<Unknown.Battle.BattleUIController>().RunAttackChecks(); }
        }
        [Serializable] sealed class CheckResult { public bool completed; public string[] failed,errors; }
        public static void RunSuiteBatch()
        {
            SessionState.SetBool("UNKNOWN.AttackSuiteBatch",true); SessionState.SetInt("UNKNOWN.AttackSuitePhase",0); SessionState.SetBool("UNKNOWN.AttackSuiteFailed",false);
            SessionState.SetString("UNKNOWN.AttackSuiteStarted",DateTime.UtcNow.ToString("O"));
            Run(); EditorApplication.update+=PollSuite;
        }
        static void PollSuite()
        {
                int phase=SessionState.GetInt("UNKNOWN.AttackSuitePhase",0);
                var started=DateTime.Parse(SessionState.GetString("UNKNOWN.AttackSuiteStarted",""),null,System.Globalization.DateTimeStyles.RoundtripKind);
                bool failed=SessionState.GetBool("UNKNOWN.AttackSuiteFailed",false);
                string[] paths={"Library/UNKNOWN-AttackUI-Checks.json","Library/UNKNOWN-DragUI-Checks.json","Library/UNKNOWN-PvP-Regressions.json"};
                if((DateTime.UtcNow-started).TotalSeconds>180) { SessionState.SetBool("UNKNOWN.AttackSuiteBatch",false); Debug.LogError("Attack suite timed out"); EditorApplication.Exit(1); return; }
                if(!File.Exists(paths[phase]) || File.GetLastWriteTimeUtc(paths[phase])<=started)return;
                var result=JsonUtility.FromJson<CheckResult>(File.ReadAllText(paths[phase]));
                failed|=!result.completed || (result.failed?.Length??0)>0 || (result.errors?.Length??0)>0;
                phase++;
                SessionState.SetBool("UNKNOWN.AttackSuiteFailed",failed); SessionState.SetInt("UNKNOWN.AttackSuitePhase",phase);
                if(phase==paths.Length) { SessionState.SetBool("UNKNOWN.AttackSuiteBatch",false); EditorApplication.update-=PollSuite; if(Application.isBatchMode)EditorApplication.Exit(failed?1:0); else { EditorApplication.isPlaying=false; Debug.Log("[UNKNOWN] UI suite finished. failed="+failed); } return; }
                var ui=new GameObject("UNKNOWN UI Regression Suite").AddComponent<Unknown.Battle.BattleUIController>();
                if(phase==1)ui.RunDragChecks(); else ui.RunPvPRegressionChecks();
        }
    }
}
