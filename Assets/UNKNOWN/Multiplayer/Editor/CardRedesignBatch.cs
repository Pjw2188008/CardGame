using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Unknown.Battle;
namespace Unknown.Multiplayer.Editor
{
    public static class CardRedesignBatch
    {
        static CardRedesignExperiment experiment;
        static int lastProgress=-1;
        [Serializable]sealed class CheckResult{public bool completed;public string[] passed;public string error;}
        public static void RunChecksBatch()
        {
            try {
                CardRedesignChecks.Run("Library/CardRedesignValidation");
                File.WriteAllText("Library/CardRedesign-Checks.json",JsonUtility.ToJson(new CheckResult{completed=true,passed=CardRedesignChecks.Passed.ToArray()},true));
                EditorApplication.Exit(0);
            } catch(Exception error) {
                Debug.LogException(error);File.WriteAllText("Library/CardRedesign-Checks.json",JsonUtility.ToJson(new CheckResult{completed=false,passed=CardRedesignChecks.Passed.ToArray(),error=error.ToString()},true));EditorApplication.Exit(1);
            }
        }
        public static void RunComparisonBatch()
        {
            try {
                CardRedesignChecks.Run("Library/CardRedesignValidation");
                File.WriteAllText("Library/CardRedesign-Checks.json",JsonUtility.ToJson(new CheckResult{completed=true,passed=CardRedesignChecks.Passed.ToArray()},true));
                if(!EditorApplication.ExecuteMenuItem("Tools/UNKNOWN/Card Redesign Comparison"))throw new InvalidOperationException("Comparison menu missing");
                EditorWindow.GetWindow<CardRedesignComparisonWindow>().Close();
                experiment=new CardRedesignExperiment(7312026,Path.GetFullPath("BalanceResults"));EditorApplication.update+=Tick;
            } catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void Tick()
        {
            try {
                experiment.Tick(50);
                if(experiment.Completed/100!=lastProgress){lastProgress=experiment.Completed/100;Debug.Log("[Redesign] "+experiment.Completed+" / 1000 "+experiment.Progress);}
                if(!experiment.Finished)return;
                File.WriteAllText("Library/CardRedesign-ExperimentLocation.json",JsonUtility.ToJson(new Location{directory=experiment.DirectoryPath},true));
                EditorApplication.update-=Tick;EditorApplication.Exit(0);
            } catch(Exception e){Debug.LogException(e);experiment?.Dispose();EditorApplication.update-=Tick;EditorApplication.Exit(1);}
        }
        [Serializable]sealed class Location{public string directory;}
    }
}
