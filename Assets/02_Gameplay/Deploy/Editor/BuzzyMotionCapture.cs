using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Buzzy.Core;

namespace Buzzy.Game.EditorTools
{
    /// <summary>
    /// M2: tự chơi một level bằng chuyển động thật (không cần Play Mode) và chụp từng khung ra thư mục.
    /// -executeMethod Buzzy.Game.EditorTools.BuzzyMotionCapture.Run ; môi trường: BUZZY_OUT (thư mục), BUZZY_LEVEL, BUZZY_FPS, BUZZY_MAXT.
    /// </summary>
    public static class BuzzyMotionCapture
    {
        const string Res = "Assets/02_Gameplay/Res/";
        const string AssetsPath = Res + "Config/BuzzyAssets.asset";

        public static BuzzyAssets LoadOrCreateAssets()
        {
            var a = AssetDatabase.LoadAssetAtPath<BuzzyAssets>(AssetsPath);
            if (a == null) { a = ScriptableObject.CreateInstance<BuzzyAssets>(); Directory.CreateDirectory(Path.GetDirectoryName(AssetsPath)); AssetDatabase.CreateAsset(a, AssetsPath); }
            a.busMesh = Mesh("Bus"); a.blockMesh = Mesh("Block_Mesh"); a.barrierMesh = Mesh("Barrier");
            a.busTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Res + "Buzzy/Textures/T_Bus.png");
            a.font = AssetDatabase.LoadAssetAtPath<Font>(Res + "Buzzy/Fonts/LilitaOne-Regular.ttf");
            a.tunnel = Sprite("Tunnel"); a.levelBar = Sprite("lvl_bar"); a.boosterCircle = Sprite("btn_Booster2");
            a.lockIcon = Sprite("icon_Lock"); a.settings = Sprite("btn_Setting");
            a.knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            EditorUtility.SetDirty(a); AssetDatabase.SaveAssets();
            return a;
        }

        [MenuItem("Tools/Buzzy Bus/Build Game Scene")]
        public static void BuildGameScene()
        {
            var cfg = BuzzyStaticBuilder.LoadOrCreateConfig();
            var assets = LoadOrCreateAssets();
            var db = AssetDatabase.LoadAssetAtPath<BuzzyLevelDatabase>(Res + "Levels/BuzzyLevels.asset");
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            var go = new GameObject("BuzzyBootstrap"); var b = go.AddComponent<BuzzyBootstrap>();
            b.database = db; b.config = cfg; b.assets = assets; b.level = 2;
            Directory.CreateDirectory(Res + "Scenes");
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, Res + "Scenes/BuzzyGame.unity");
            Debug.Log("[BuzzyMotionCapture] Đã tạo " + Res + "Scenes/BuzzyGame.unity");
        }

        static Mesh Mesh(string n)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Res + "Buzzy/Meshes/" + n + ".obj")) if (o is Mesh m) return m;
            throw new Exception("Không thấy mesh " + n);
        }

        static Sprite Sprite(string n)
        {
            string path = Res + "Buzzy/Sprites/" + n + ".png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.alphaIsTransparency = true; ti.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static float Env(string k, float d) { var v = Environment.GetEnvironmentVariable(k); return string.IsNullOrEmpty(v) ? d : float.Parse(v, System.Globalization.CultureInfo.InvariantCulture); }

        public static void Run()
        {
            string outDir = Environment.GetEnvironmentVariable("BUZZY_OUT");
            if (string.IsNullOrEmpty(outDir)) throw new Exception("Thiếu BUZZY_OUT");
            int level = (int)Env("BUZZY_LEVEL", 2); float fps = Env("BUZZY_FPS", 8f); float maxT = Env("BUZZY_MAXT", 60f);
            Directory.CreateDirectory(outDir);
            var cfg = BuzzyStaticBuilder.LoadOrCreateConfig();
            var assets = LoadOrCreateAssets();
            var db = AssetDatabase.LoadAssetAtPath<BuzzyLevelDatabase>(Res + "Levels/BuzzyLevels.asset");
            var lv = db.Get(level);
            var size = new Vector2Int(324, 720);

            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            var camGo = new GameObject("Camera"); var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = cfg.backgroundTop;
            cam.transform.position = cfg.cameraPosition; cam.transform.eulerAngles = cfg.cameraEuler;
            cam.fieldOfView = cfg.fieldOfView; cam.nearClipPlane = 0.3f; cam.farClipPlane = 200f; cam.aspect = (float)size.x / size.y;
            var go = new GameObject("BuzzyGame"); var view = go.AddComponent<BuzzyGameView>();
            var log = new List<string>();
            view.Log = s => log.Add(s);
            int vibrations = 0; view.Vibrate = () => { vibrations++; log.Add("RUNG (haptic) lần " + vibrations + " t=" + view.Time.ToString("0.00")); };
            view.Init(lv, cfg, assets, cam, size);

            var rt = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
            var tex = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            bool probed = false; float dt = 1f / 60f, nextShot = 0f, nextTap = 0f, endAt = float.MaxValue; int shot = 0;
            while (view.Time < maxT && view.Time < endAt)
            {
                view.Tick(dt);
                if (!probed && view.Time >= cfg.inputDelay + 0.1f) { probed = true; view.TapColor(3); }   // màu không có xe: phải rung
                if (view.Time >= nextTap && view.Game.InFlight == 0 && !view.Won && view.Time >= cfg.inputDelay + 0.2f)
                {
                    var c = view.Game.TappableColor();
                    if (c.HasValue) view.TapColor(c.Value);
                    nextTap = view.Time + 0.5f;
                    if (!c.HasValue && view.Game.Status == GameStatus.Deadlock) { log.Add("BẾ TẮC t=" + view.Time.ToString("0.00")); break; }
                }
                if (view.Won && endAt == float.MaxValue) endAt = view.Time + 1.2f;
                if (view.Time >= nextShot)
                {
                    cam.Render(); RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); tex.Apply();
                    File.WriteAllBytes(Path.Combine(outDir, "f_" + shot.ToString("0000") + ".png"), tex.EncodeToPNG());
                    shot++; nextShot += 1f / fps;
                }
            }
            RenderTexture.active = null; cam.targetTexture = null;
            File.WriteAllLines(Path.Combine(outDir, "log.txt"), log.ToArray());
            Debug.Log("[BuzzyMotionCapture] " + shot + " khung, thắng=" + view.Won + ", tap=" + view.TapCount + ", t=" + view.Time.ToString("0.00") + " -> " + outDir);
        }
    }
}
