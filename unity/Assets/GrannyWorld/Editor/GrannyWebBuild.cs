using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GrannyWorld.EditorTools
{
    /// <summary>
    /// Builds the Granny World scene for WebGL with settings that work on static hosts such as
    /// GitHub Pages (gzip + decompression fallback, since Pages can't send Content-Encoding headers).
    /// Project-wide settings it touches are restored afterwards.
    /// </summary>
    public static class GrannyWebBuild
    {
        const string Scene = "Assets/GrannyWorld/Scenes/GrannyWorld.unity";

        [MenuItem("Tools/Granny World/Build WebGL")]
        static void BuildFromMenu()
        {
            string path = EditorUtility.SaveFolderPanel("WebGL output folder", "", "docs");
            if (!string.IsNullOrEmpty(path)) Debug.Log(Build(path));
        }

        public static string Build(string outputPath)
        {
            string productName = PlayerSettings.productName;
            var compression = PlayerSettings.WebGL.compressionFormat;
            bool fallback = PlayerSettings.WebGL.decompressionFallback;
            int webQuality = GetWebGLDefaultQuality();
            try
            {
                PlayerSettings.productName = "Sporty Granny's Island";
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = true;
                SetWebGLDefaultQuality(System.Array.IndexOf(QualitySettings.names, "PC"));

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { Scene },
                    locationPathName = outputPath,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None,
                });
                var s = report.summary;
                if (s.result == BuildResult.Succeeded) WriteFullWindowPage(outputPath);
                return $"{s.result}: {s.totalSize / (1024f * 1024f):F1} MB in {s.totalTime.TotalSeconds:F0}s, errors={s.totalErrors}, warnings={s.totalWarnings}";
            }
            finally
            {
                PlayerSettings.productName = productName;
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.decompressionFallback = fallback;
                SetWebGLDefaultQuality(webQuality);
                AssetDatabase.SaveAssets(); // the build saved the temporary values to disk; persist the originals
            }
        }

        /// <summary>Replaces Unity's fixed-size default page with a full-window canvas and a loading screen.</summary>
        static void WriteFullWindowPage(string outputPath)
        {
            string name = System.IO.Path.GetFileName(outputPath.TrimEnd('/', '\\'));
            string html = @"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>Sporty Granny's Island</title>
<link rel=""icon"" type=""image/svg+xml"" href=""favicon.svg"">
<link rel=""icon"" type=""image/png"" sizes=""32x32"" href=""favicon-32.png"">
<link rel=""apple-touch-icon"" href=""apple-touch-icon.png"">
<meta name=""theme-color"" content=""#6fbcf0"">
<style>
  html, body { margin: 0; height: 100%; overflow: hidden; background: #8fc3ea; font-family: system-ui, sans-serif; }
  #game { position: fixed; inset: 0; width: 100%; height: 100%; display: block; outline: none; }
  #loading { position: fixed; inset: 0; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 18px;
             color: #fff; background: linear-gradient(#7db3e4, #c4e6f8); text-shadow: 0 2px 6px rgba(0,0,0,.25); }
  #loading h1 { margin: 0; font-size: clamp(28px, 5vw, 56px); }
  #bar { width: min(420px, 70vw); height: 14px; border-radius: 7px; background: rgba(255,255,255,.4); overflow: hidden; }
  #fill { height: 100%; width: 0; background: #ffd84d; transition: width .2s; }
  #hint { opacity: .9; }
  #error { display: none; max-width: 80vw; padding: 8px 14px; border-radius: 6px; background: #b33; }
</style>
</head>
<body>
<canvas id=""game"" tabindex=""-1""></canvas>
<div id=""loading"">
  <h1>Sporty Granny's Island</h1>
  <div id=""bar""><div id=""fill""></div></div>
  <div id=""hint"">Loading… then click the game to play (keyboard &amp; mouse or gamepad)</div>
  <div id=""error""></div>
</div>
<script src=""Build/{NAME}.loader.js""></script>
<script>
  const canvas = document.getElementById('game');
  createUnityInstance(canvas, {
    dataUrl: 'Build/{NAME}.data.unityweb',
    frameworkUrl: 'Build/{NAME}.framework.js.unityweb',
    codeUrl: 'Build/{NAME}.wasm.unityweb',
    streamingAssetsUrl: 'StreamingAssets',
    companyName: 'Granny',
    productName: ""Sporty Granny's Island"",
    productVersion: '1.0',
    devicePixelRatio: Math.min(window.devicePixelRatio || 1, 1.5),
  }, p => { document.getElementById('fill').style.width = (p * 100) + '%'; })
  .then(() => { document.getElementById('loading').remove(); canvas.focus(); })
  .catch(err => { const e = document.getElementById('error'); e.textContent = err; e.style.display = 'block'; });
</script>
</body>
</html>
".Replace("{NAME}", name);
            System.IO.File.WriteAllText(System.IO.Path.Combine(outputPath, "index.html"), html);
            System.IO.File.WriteAllText(System.IO.Path.Combine(outputPath, ".nojekyll"), "");

            // Granny favicons live in <project>/WebGL; Unity's TemplateData (logo, default icon) is unused.
            string icons = System.IO.Path.Combine(Application.dataPath, "..", "WebGL");
            if (System.IO.Directory.Exists(icons))
                foreach (var file in System.IO.Directory.GetFiles(icons))
                    System.IO.File.Copy(file, System.IO.Path.Combine(outputPath, System.IO.Path.GetFileName(file)), true);
            string templateData = System.IO.Path.Combine(outputPath, "TemplateData");
            if (System.IO.Directory.Exists(templateData)) System.IO.Directory.Delete(templateData, true);
        }

        // QualitySettings exposes no API for the per-platform default level, so edit it via serialization.
        static SerializedProperty WebGLQualityEntry(out SerializedObject so)
        {
            so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var map = so.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; i < map.arraySize; i++)
            {
                var entry = map.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue == "WebGL") return entry.FindPropertyRelative("second");
            }
            return null;
        }

        static int GetWebGLDefaultQuality() => WebGLQualityEntry(out _)?.intValue ?? 0;

        static void SetWebGLDefaultQuality(int level)
        {
            if (level < 0) return;
            var prop = WebGLQualityEntry(out var so);
            if (prop == null) return;
            prop.intValue = level;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
