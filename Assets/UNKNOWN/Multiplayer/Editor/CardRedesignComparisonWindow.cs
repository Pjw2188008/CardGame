using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Unknown.Battle;
namespace Unknown.Multiplayer.Editor
{
    public sealed class CardRedesignComparisonWindow : EditorWindow
    {
        int seed=7312026;
        CardRedesignExperiment experiment;
        [MenuItem("Tools/UNKNOWN/Card Redesign Comparison")]
        public static void Open(){GetWindow<CardRedesignComparisonWindow>("Card Redesign Comparison");}
        void OnEnable(){EditorApplication.update+=UpdateRun;AssemblyReloadEvents.beforeAssemblyReload+=Stop;}
        void OnDisable(){EditorApplication.update-=UpdateRun;AssemblyReloadEvents.beforeAssemblyReload-=Stop;Stop();}
        void Stop(){experiment?.Dispose();}
        void UpdateRun()
        {
            if(experiment==null || experiment.Finished)return;
            try {experiment.Tick();Repaint();}catch(Exception e){Debug.LogException(e);Stop();}
        }
        void OnGUI()
        {
            bool running=experiment!=null && !experiment.Finished;
            using(new EditorGUI.DisabledScope(running)) {
                seed=EditorGUILayout.IntField("Seed",seed);
                EditorGUILayout.LabelField("Main", "Baseline 480 / Revised 480, 240 paired conditions");
                EditorGUILayout.LabelField("Stress", "Baseline 20 / Revised 20, Random cases");
                if(GUILayout.Button("Start 1000-Match Comparison"))try {experiment=new CardRedesignExperiment(seed,Path.GetFullPath("BalanceResults"));}catch(Exception e){Debug.LogException(e);}
            }
            using(new EditorGUI.DisabledScope(!running))if(GUILayout.Button("Stop Test"))Stop();
            if(experiment==null)return;
            EditorGUILayout.LabelField(experiment.Status+" · "+experiment.Completed+" / 1000 · "+experiment.Progress);
            EditorGUI.ProgressBar(GUILayoutUtility.GetRect(10,20),experiment.Completed/1000f,"Comparison progress");
            if(GUILayout.Button("Open Results Folder"))EditorUtility.RevealInFinder(experiment.DirectoryPath);
            EditorGUILayout.HelpBox("같은 초기 덱·AI 정체성을 대응시킵니다. 전투 경로가 바뀌면 RNG 소비도 달라집니다. 카드 개편 수치는 1차 시험안입니다.",MessageType.Info);
        }
    }
}
