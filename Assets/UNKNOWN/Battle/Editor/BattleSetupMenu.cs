using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unknown.Battle.Editor
{
    public static class BattleSetupMenu
    {
        const string ScenePath = "Assets/Scenes/UNKNOWN_Battle_Test.unity";
        [MenuItem("UNKNOWN/Battle/Run Play Mode Integration Checks %&t")]
        public static void CheckPlayMode()
        {
            var controller = Object.FindFirstObjectByType<BattleUIController>();
            if (!EditorApplication.isPlaying || controller == null)
            { Debug.LogWarning("Open UNKNOWN_Battle_Test and enter Play mode first."); return; }
            controller.RunIntegrationChecks();
        }
        [MenuItem("UNKNOWN/Battle/Open Local Battle Test")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Stop Play mode before opening the battle scene."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene;
            if (System.IO.File.Exists(ScenePath)) scene = EditorSceneManager.OpenScene(ScenePath);
            else scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!Object.FindObjectsByType<BattleUIController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any())
                Undo.AddComponent<BattleUIController>(new GameObject("UNKNOWN Battle Prototype"));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeObject = Object.FindFirstObjectByType<BattleUIController>();
            Debug.Log("[UNKNOWN] Local battle scene ready. Play creates the Canvas and card controls. Existing multiplayer scenes are preserved.");
        }
    }
}
