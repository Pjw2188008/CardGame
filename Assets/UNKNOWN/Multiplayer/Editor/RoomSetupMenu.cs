using System;
using System.Linq;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unknown.Multiplayer.Editor
{
    public static class RoomSetupMenu
    {
        [MenuItem("UNKNOWN/Maps/Connect Supplied Backgrounds")]
        public static void ConnectMapBackgrounds()
        {
            var settings=AssetDatabase.LoadAssetAtPath<Unknown.Battle.BattleMapSettings>("Assets/UNKNOWN/Battle/Resources/BattleMapSettings.asset");
            if(settings==null)throw new InvalidOperationException("BattleMapSettings asset is missing.");
            Texture2D Load(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Map/"+name+".png")
                ?? throw new InvalidOperationException("Map image is missing: "+name);
            settings.normalBackground=Load("O_MAP");settings.jokerBackground=Load("J_MAP");
            settings.diamondBackground=Load("D_MAP");settings.spadeBackground=Load("S_MAP");
            settings.heartBackground=Load("H_MAP");settings.clubBackground=Load("C_MAP");
            EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
        }
        public static void RunMapBackgroundChecksBatch(){ConnectMapBackgrounds();AttackCheckMenu.RunSuiteBatch();}

        [MenuItem("UNKNOWN/Multiplayer/Build Windows Test Player %&b")]
        public static void BuildWindowsTestPlayer()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureUISettings();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("Add the room scene to Build Profiles first.");
            Directory.CreateDirectory("Builds/UNKNOWN");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes, locationPathName = "Builds/UNKNOWN/UNKNOWN.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            Debug.Log($"[UNKNOWN] Windows build: {report.summary.result}, errors={report.summary.totalErrors}, warnings={report.summary.totalWarnings}");
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Windows test build failed. See Console.");
        }

        [MenuItem("UNKNOWN/Multiplayer/Install Room Test UI in Current Scene %&i")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            var managers = UnityEngine.Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (managers.Length != 1)
            {
                EditorUtility.DisplayDialog("UNKNOWN", "기존 NetworkManager가 정확히 하나 있어야 합니다. 중복 생성하지 않습니다.", "확인");
                return;
            }
            var manager = managers[0];
            if (!manager.gameObject.activeInHierarchy || !manager.enabled || !(manager.NetworkConfig.NetworkTransport is UnityTransport))
            {
                EditorUtility.DisplayDialog("UNKNOWN", "기존 NetworkManager를 활성화하고 Network Transport에 UnityTransport를 연결하세요.", "확인");
                return;
            }
            EnsureUISettings();
            Undo.SetCurrentGroupName("Install UNKNOWN room test");
            int group = Undo.GetCurrentGroup();
            if (manager.GetComponent<RoomController>() == null) Undo.AddComponent<RoomController>(manager.gameObject);
            if (manager.GetComponent<NetworkBattleController>() == null) Undo.AddComponent<NetworkBattleController>(manager.gameObject);
            if (manager.GetComponent<RoomTestUI>() == null) Undo.AddComponent<RoomTestUI>(manager.gameObject);
            // Preserve existing Widgets, but stop a second session/authentication flow from running beside this UI.
            var widgets = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(c => c != null && c.GetType().Namespace == "Unity.Multiplayer.Widgets" && c.gameObject.scene == manager.gameObject.scene)
                .Select(c => c.gameObject).Distinct().ToArray();
            foreach (var widget in widgets)
            {
                if (!widget.activeSelf || widget == manager.gameObject) continue;
                Undo.RecordObject(widget, "Disable existing multiplayer widget");
                widget.SetActive(false);
                EditorUtility.SetDirty(widget);
            }
            Undo.RecordObject(manager, "Run multiplayer in background");
            manager.RunInBackground = true;
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = manager.gameObject;
            Debug.Log("[UNKNOWN] RoomController + RoomTestUI installed on the existing NetworkManager. Existing Widgets preserved and disabled. Save the scene, then Play.");
        }

        static void EnsureUISettings()
        {
            const string path = "Assets/UNKNOWN/Multiplayer/Resources/UnknownRoomPanel.asset";
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (settings != null) return;
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UNKNOWN/Multiplayer/Resources/UnknownRoomTheme.tss");
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
        }
    }
}
