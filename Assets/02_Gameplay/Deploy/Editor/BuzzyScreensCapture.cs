using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Buzzy.Game.EditorTools
{
    /// <summary>
    /// M5: chụp từng màn / popup (U1–U6, V31) không cần Play Mode: loading, màn thắng, popup thắng, thua, mở khoá, mua booster, cài đặt, shop.
    /// -executeMethod Buzzy.Game.EditorTools.BuzzyScreensCapture.Run ; môi trường: BUZZY_OUT (thư mục), BUZZY_LEVEL (mặc định 4), BUZZY_SIZE (324x720).
    /// Cũng dựng scene BuzzyLoading.unity + BuzzyGame.unity và ghi Build Settings (BuildScenes).
    /// </summary>
    public static class BuzzyScreensCapture
    {
        const string Res = "Assets/02_Gameplay/Res/";

        public static void Run()
        {
            string outDir = Environment.GetEnvironmentVariable("BUZZY_OUT"); if (string.IsNullOrEmpty(outDir)) throw new Exception("Thiếu BUZZY_OUT");
            Directory.CreateDirectory(outDir);
            int level = 4; int.TryParse(Environment.GetEnvironmentVariable("BUZZY_LEVEL"), out level); if (level <= 0) level = 4;
            var size = new Vector2Int(324, 720); var sz = Environment.GetEnvironmentVariable("BUZZY_SIZE");
            if (!string.IsNullOrEmpty(sz)) { var p = sz.ToLower().Split('x'); size = new Vector2Int(int.Parse(p[0]), int.Parse(p[1])); }
            var cfg = BuzzyStaticBuilder.LoadOrCreateConfig(); var assets = BuzzyMotionCapture.LoadOrCreateAssets();
            var db = AssetDatabase.LoadAssetAtPath<BuzzyLevelDatabase>(Res + "Levels/BuzzyLevels.asset");

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Camera"); var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = cfg.backgroundTop; cfg.ApplyCamera(cam, (float)size.x / size.y);
            var rt = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
            var tex = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            var log = new List<string>();
            Action<string> shot = name =>
            {
                Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); tex.Apply();
                File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG()); log.Add("chụp " + name);
            };

            // 1. loading (U1)
            var loadRoot = new GameObject("Loading"); RectTransform fill, bus; float barW;
            BuzzyLoading.Build(loadRoot.transform, cam, size, cfg, assets, 0.55f, out fill, out bus, out barW);
            cam.backgroundColor = Color.black; shot("loading"); UnityEngine.Object.DestroyImmediate(loadRoot); cam.backgroundColor = cfg.backgroundTop;

            // 2. level + popup
            var go = new GameObject("BuzzyGame"); var view = go.AddComponent<BuzzyGameView>();
            view.Log = s => log.Add(s);
            var meta = new Buzzy.Core.MetaState(cfg.ToMetaConfig()); meta.Level = level; meta.Coins = 640;
            for (int i = 0; i < 4; i++) if (meta.IsBoosterUnlocked(i) && cfg.boosterUnlock[i] < level) meta.ClaimUnlock(i);   // booster mở ở chính level này → còn popup Claim
            view.Meta = meta;
            view.Init(db.Get(level), cfg, assets, cam, size);
            float dt = 1f / 60f; Action<float> tick = sec => { int n = Mathf.CeilToInt(sec / dt); for (int i = 0; i < n; i++) view.Tick(dt); };
            tick(cfg.inputDelay + 0.6f);
            if (view.Popup == BuzzyGameView.PopupKind.Unlock) { shot("unlock"); view.ClosePopup(); }
            else { view.ShowUnlock(0); tick(0.1f); shot("unlock"); view.ClosePopup(); }
            shot("level");
            view.OpenSettings(); tick(0.1f); shot("settings"); view.ClosePopup();
            view.OpenShop(); tick(0.1f); shot("shop"); view.ClosePopup();
            view.ShowBuyBooster(0); tick(0.1f); shot("buy_booster"); view.ClosePopup();
            view.ShowLose(); tick(0.1f); shot("lose"); view.ClosePopup();
            view.DebugForceWin(); tick(0.9f); shot("win_sequence_1"); tick(0.8f); shot("win_sequence_2");
            tick(cfg.winSequenceDuration); tick(0.05f); shot("completed_1"); tick(0.7f); shot("completed_2");
            int earned = -1; view.LevelCompleted += c => earned = c;
            view.ClaimWin(true); log.Add("ClaimWin(ad) -> " + earned + " xu");
            // luồng level kế (cùng bước như BuzzyBootstrap.OnLevelCompleted): cộng xu, level + 1, dựng view mới
            meta.CompleteLevel(earned); UnityEngine.Object.DestroyImmediate(go);
            go = new GameObject("BuzzyGame"); view = go.AddComponent<BuzzyGameView>(); view.Log = s => log.Add(s); view.Meta = meta;
            view.Init(db.Get(meta.Level), cfg, assets, cam, size);
            tick(cfg.inputDelay + 0.6f); if (view.Popup != BuzzyGameView.PopupKind.None) view.ClosePopup();
            shot("next_level"); log.Add("level kế = " + meta.Level + ", xu = " + meta.Coins);
            view.DebugForceWin(); tick(cfg.winSequenceDuration + 0.2f); shot("next_completed");

            RenderTexture.active = null; cam.targetTexture = null;
            File.WriteAllLines(Path.Combine(outDir, "log.txt"), log.ToArray());
            Debug.Log("[BuzzyScreensCapture] " + log.Count + " dòng -> " + outDir);
        }

        /// <summary>Dựng scene BuzzyLoading + BuzzyGame và ghi Build Settings (loading trước).</summary>
        public static void BuildScenes()
        {
            var cfg = BuzzyStaticBuilder.LoadOrCreateConfig(); var assets = BuzzyMotionCapture.LoadOrCreateAssets();
            var db = AssetDatabase.LoadAssetAtPath<BuzzyLevelDatabase>(Res + "Levels/BuzzyLevels.asset");
            Directory.CreateDirectory(Res + "Scenes");
            var s1 = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var l = new GameObject("BuzzyLoading").AddComponent<BuzzyLoading>(); l.config = cfg; l.assets = assets;
            EditorSceneManager.SaveScene(s1, Res + "Scenes/BuzzyLoading.unity");
            var s2 = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var b = new GameObject("BuzzyBootstrap").AddComponent<BuzzyBootstrap>(); b.database = db; b.config = cfg; b.assets = assets; b.level = 0;
            EditorSceneManager.SaveScene(s2, Res + "Scenes/BuzzyGame.unity");
            var list = new List<EditorBuildSettingsScene>();
            list.Add(new EditorBuildSettingsScene(Res + "Scenes/BuzzyLoading.unity", true));
            list.Add(new EditorBuildSettingsScene(Res + "Scenes/BuzzyGame.unity", true));
            foreach (var s in EditorBuildSettings.scenes) if (!s.path.Contains("02_Gameplay")) list.Add(new EditorBuildSettingsScene(s.path, false));   // scene base giữ nhưng tắt
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[BuzzyScreensCapture] Đã dựng BuzzyLoading.unity + BuzzyGame.unity, Build Settings: " + list.Count + " scene");
        }
    }
}
