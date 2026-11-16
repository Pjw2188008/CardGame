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
        html=FitWebPage(html);
        File.WriteAllText(path,html);
    }
    // Share one viewport policy across title, room menus and every game canvas.
    internal static string FitWebPage(string html)
    {
        if(html.Contains("id=\"unknown-web-viewport\""))return html;
        const string style=@"<meta name=""viewport"" content=""width=device-width, initial-scale=1.0, viewport-fit=cover"">
    <style id=""unknown-web-viewport"">
      html, body { width: 100%; height: 100%; margin: 0; padding: 0; overflow: hidden; background: #060a11; }
      #unity-container.unity-desktop, #unity-container.unity-mobile { position: fixed; display: flex; flex-direction: column; transform: none; }
      #unity-canvas { display: block; flex: none; max-width: 100%; }
      #unity-footer { display: flex; align-items: center; width: 100%; height: 38px; flex: 0 0 38px; overflow: hidden; white-space: nowrap; }
      #unity-logo-title-footer, #unity-fullscreen-button { float: none; flex: none; }
      #unity-build-title { float: none; min-width: 0; flex: 1; text-align: right; overflow: hidden; text-overflow: ellipsis; }
      #unity-fullscreen-button { order: 1; }
      #unity-container:fullscreen { left: 0 !important; top: 0 !important; width: 100% !important; height: 100% !important; align-items: center; justify-content: center; background: #060a11; }
      #unity-container:fullscreen #unity-footer { display: none; }
    </style>
";
        html=html.Replace("</head>",style+"</head>");
        // Reuse this viewport tag when Unity's mobile template configures it.
        html=html.Replace("var meta = document.createElement('meta');","var meta = document.querySelector('meta[name=viewport]') || document.createElement('meta');");
        html=html.Replace("width=device-width, height=device-height, initial-scale=1.0, user-scalable=no, shrink-to-fit=yes","width=device-width, initial-scale=1.0, viewport-fit=cover");
        html=html.Replace("unityInstance.SetFullscreen(1);",@"const container = document.getElementById('unity-container');
                  if (container.requestFullscreen) container.requestFullscreen().catch(() => unityInstance.SetFullscreen(1));
                  else unityInstance.SetFullscreen(1);");
        return html.Replace("document.body.appendChild(script);",@"config.autoSyncPersistentDataPath = true;
      config.devicePixelRatio = Math.min(window.devicePixelRatio || 1, 2);
      config.matchWebGLToCanvasSize = true;
      function fitUnknownCanvas() {
        const container = document.getElementById('unity-container');
        if (!container || !canvas) return;
        const full = !!document.fullscreenElement;
        const viewport = !full && window.visualViewport;
        const width = Math.max(1, viewport ? viewport.width : window.innerWidth);
        const height = Math.max(1, viewport ? viewport.height : window.innerHeight);
        const footer = document.getElementById('unity-footer');
        const footerHeight = full || container.classList.contains('unity-mobile') || height < 160 ? 0 : 38;
        if (footer) footer.style.display = footerHeight ? 'flex' : 'none';
        const canvasWidth = Math.min(width, Math.max(1, height - footerHeight) * 16 / 9);
        const canvasHeight = canvasWidth * 9 / 16;
        canvas.style.width = canvasWidth + 'px';
        canvas.style.height = canvasHeight + 'px';
        container.style.width = canvasWidth + 'px';
        container.style.height = (canvasHeight + footerHeight) + 'px';
        container.style.left = ((viewport ? viewport.offsetLeft : 0) + (width - canvasWidth) / 2) + 'px';
        container.style.top = ((viewport ? viewport.offsetTop : 0) + (height - canvasHeight - footerHeight) / 2) + 'px';
      }
      window.addEventListener('resize', fitUnknownCanvas);
      document.addEventListener('fullscreenchange', fitUnknownCanvas);
      if (window.visualViewport) {
        window.visualViewport.addEventListener('resize', fitUnknownCanvas);
        window.visualViewport.addEventListener('scroll', fitUnknownCanvas);
      }
      fitUnknownCanvas();
      document.body.appendChild(script);");
    }

}
