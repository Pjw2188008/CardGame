using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Web-only build settings. The Windows target and its build options are untouched.</summary>
public sealed class UnknownWebBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 100;
    // Apply this for the ordinary Build Profiles button too, not only the custom menu.
    // Limit the generated C++ and skip the expensive wasm-opt pass during iteration.
    // These are Web-only settings; Windows retains its own backend/code generation.
    [MenuItem("UNKNOWN/Build/Configure Web build time")]
    public static void ConfigureWebBuildTime()
    {
        PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
        UnityEditor.WebGL.UserBuildSettings.codeOptimization=UnityEditor.WebGL.WasmCodeOptimization.BuildTimes;
    }
    public void OnPreprocessBuild(BuildReport report)
    {
        if(report.summary.platform==BuildTarget.WebGL)ConfigureWebBuildTime();
    }
    [MenuItem("UNKNOWN/Build/Web (Release, uncompressed)")]
    public static void Build()
    {
        try {
            ConfigureWebBuildTime();
            foreach(var role in new[]{"Body","Serif","Title"}) {
                var font=Resources.Load<Font>("WebFonts/"+role);
                if(font==null)throw new BuildFailedException("Missing web font: "+role);
                var importer=(TrueTypeFontImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(font));
                if(!importer.includeFontData || importer.fontTextureCase!=FontTextureCase.Dynamic) {
                    importer.includeFontData=true;importer.fontTextureCase=FontTextureCase.Dynamic;importer.SaveAndReimport();
                }
            }
            PlayerSettings.defaultWebScreenWidth=1280;PlayerSettings.defaultWebScreenHeight=720;
            PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback=false;
            PlayerSettings.WebGL.dataCaching=true;
            PlayerSettings.WebGL.template="APPLICATION:Default";
            PlayerSettings.WebGL.initialMemorySize=128;
            var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
            if(scenes.Length==0)throw new BuildFailedException("No enabled title scene");
            var output=Environment.GetEnvironmentVariable("UNKNOWN_WEB_OUTPUT");
            if(string.IsNullOrEmpty(output))output="Builds/Web/unknown/play";
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName=output,target=BuildTarget.WebGL,options=BuildOptions.None});
            Directory.CreateDirectory("Library/WebGLValidation");
            File.WriteAllText("Library/WebGLValidation/build-result.txt",report.summary.result+" errors="+report.summary.totalErrors+" warnings="+report.summary.totalWarnings+" bytes="+report.summary.totalSize+" output="+Path.GetFullPath(output));
            if(Application.isBatchMode)EditorApplication.Exit(report.summary.result==BuildResult.Succeeded?0:1);
        } catch(Exception e) {
            Debug.LogException(e);Directory.CreateDirectory("Library/WebGLValidation");File.WriteAllText("Library/WebGLValidation/build-failure.txt",e.ToString());
            if(Application.isBatchMode)EditorApplication.Exit(1);else throw;
        }
    }
    public void OnPostprocessBuild(BuildReport report)
    {
        if(report.summary.platform!=BuildTarget.WebGL)return;
        var path=Path.Combine(report.summary.outputPath,"index.html");
        var html=File.ReadAllText(path);
        // Keep Unity's default loader/template; enable the documented Unity 6 IndexedDB autosync.
        const string marker="document.body.appendChild(script);";
        if(!html.Contains(marker)) {
            Debug.LogWarning("[UNKNOWN Web] Custom template: enable config.autoSyncPersistentDataPath and 16:9 canvas sizing in that template.");
            return;
        }
        html=html.Replace(marker,@"config.autoSyncPersistentDataPath = true;
      config.devicePixelRatio = Math.min(window.devicePixelRatio || 1, 2);
      function fitUnknownCanvas() {
        const full = !!document.fullscreenElement;
        const width = Math.min(window.innerWidth, (window.innerHeight - (full ? 0 : 38)) * 16 / 9);
        canvas.style.width = Math.max(1, width) + 'px';
        canvas.style.height = Math.max(1, width * 9 / 16) + 'px';
        const container = document.getElementById('unity-container');
        container.style.position = 'absolute'; container.style.left = '50%'; container.style.top = '50%';
        container.style.transform = 'translate(-50%, -50%)';
      }
      window.addEventListener('resize', fitUnknownCanvas);
      document.addEventListener('fullscreenchange', fitUnknownCanvas);
      fitUnknownCanvas();
      document.body.appendChild(script);");
        File.WriteAllText(path,html);
    }
}
