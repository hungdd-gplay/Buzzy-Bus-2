using System;
using System.Collections.Generic;
using System.IO;
using Buzzy.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Buzzy.Game.EditorTools
{
    /// <summary>
    /// M6: tự chơi hàng loạt bằng view thật (edit-mode, không Play Mode) theo đúng luồng người chơi: MetaState chung, level thứ n = GetPlayable(n),
    /// popup mở khoá tự đóng, popup thắng bấm nhận xu, sang level kế. Ghi report.md + ảnh nhỏ đầu / cuối mỗi level.
    /// -executeMethod Buzzy.Game.EditorTools.BuzzyBatchPlay.Run ; môi trường: BUZZY_OUT, BUZZY_FROM (1), BUZZY_TO (hết), BUZZY_SIZE (162x360), BUZZY_MAXT (600 s mô phỏng / level), BUZZY_DT (0.05).
    /// </summary>
    public static class BuzzyBatchPlay
    {
        const string Res = "Assets/02_Gameplay/Res/";

        static float Env(string k, float d) { var v = Environment.GetEnvironmentVariable(k); return string.IsNullOrEmpty(v) ? d : float.Parse(v, System.Globalization.CultureInfo.InvariantCulture); }

        public static void Run()
        {
            string outDir = Environment.GetEnvironmentVariable("BUZZY_OUT"); if (string.IsNullOrEmpty(outDir)) throw new Exception("Thiếu BUZZY_OUT");
            Directory.CreateDirectory(outDir);
            var size = new Vector2Int(162, 360); var sz = Environment.GetEnvironmentVariable("BUZZY_SIZE");
            if (!string.IsNullOrEmpty(sz)) { var p = sz.ToLower().Split('x'); size = new Vector2Int(int.Parse(p[0]), int.Parse(p[1])); }
            float maxT = Env("BUZZY_MAXT", 600f), dt = Env("BUZZY_DT", 0.05f);
            var cfg = BuzzyStaticBuilder.LoadOrCreateConfig(); var assets = BuzzyMotionCapture.LoadOrCreateAssets();
            var db = AssetDatabase.LoadAssetAtPath<BuzzyLevelDatabase>(Res + "Levels/BuzzyLevels.asset");
            int from = (int)Env("BUZZY_FROM", 1), to = (int)Env("BUZZY_TO", db.PlayableCount);
            to = Mathf.Min(to, db.PlayableCount);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Camera"); var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = cfg.backgroundTop; cfg.ApplyCamera(cam, (float)size.x / size.y);
            var rt = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
            var tex = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            Action<string> shot = name =>
            {
                Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); tex.Apply();
                File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
            };

            // meta chung cả chuỗi (như BuzzyBootstrap): bắt đầu ở level `from`, xu ban đầu
            var mc = cfg.ToMetaConfig(); mc.MaxLevel = Mathf.Min(mc.MaxLevel, db.PlayableCount);
            var meta = new MetaState(mc); meta.Level = from;
            var rows = new List<string>(); var log = new List<string>();
            int won = 0, dead = 0, timeout = 0, noOrder = 0; var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int n = from; n <= to; n++)
            {
                var lv = db.GetPlayable(n); if (lv == null) break;
                var t0 = sw.ElapsedMilliseconds;
                var go = new GameObject("BuzzyGame"); var view = go.AddComponent<BuzzyGameView>();
                view.Meta = meta; view.DisplayLevel = n; view.Log = s => { if (s.Contains("BẾ TẮC") || s.Contains("Băng") || s.Contains("Chìa")) log.Add("Lv" + n + " " + s); };
                int earned = -1; view.LevelCompleted += c => earned = c;
                view.Init(lv, cfg, assets, cam, size);
                var sp = new double[3][]; for (int i = 0; i < 3; i++) sp[i] = new[] { view.Game.SlotX[i], view.Game.SlotY[i] };
                var order = Solver.FindOrder(lv.ToCore(), view.Game.Rules, sp, 300, n);
                if (order == null) { noOrder++; log.Add("Lv" + n + " (index " + lv.index + "): solver không tìm được thứ tự thắng, chơi tham lam"); }
                int oi = 0, unlocks = 0; float nextTap = 0f; bool shotStart = false; string status = "timeout";
                while (view.Time < maxT)
                {
                    view.Tick(dt);
                    if (!shotStart && view.Time >= cfg.inputDelay + 0.3f) { shotStart = true; shot("start_" + n.ToString("000")); }
                    if (view.Popup == BuzzyGameView.PopupKind.Unlock) { unlocks++; view.ClaimUnlockPopup(); }   // như người chơi bấm Claim
                    if (view.Popup == BuzzyGameView.PopupKind.Completed) { view.ClaimWin(false); status = "won"; break; }
                    if (view.Lost || (view.Game.Status == GameStatus.Deadlock && view.Game.InFlight == 0 && view.Time > cfg.inputDelay + 1f)) { status = "deadlock"; break; }
                    if (view.Time >= nextTap && view.Game.InFlight == 0 && !view.Won && view.Popup == BuzzyGameView.PopupKind.None && view.Time >= cfg.inputDelay + 0.2f)
                    {
                        int? c = order != null && oi < order.Count ? order[oi] : view.Game.TappableColor();
                        if (c.HasValue && view.TapColor(c.Value) && order != null) oi++;
                        nextTap = view.Time + 0.3f;
                    }
                }
                shot("end_" + n.ToString("000"));
                if (status == "won") { won++; meta.CompleteLevel(earned); } else if (status == "deadlock") dead++; else timeout++;
                rows.Add("| " + n + " | " + lv.index + " | " + lv.tag + " | " + LevelValidator.MechanicList(lv.ToCore()) + " | " + status + " | " + view.TapCount + " | " + view.Time.ToString("0") + " | " + unlocks + " | " + meta.Coins + " | " + (sw.ElapsedMilliseconds - t0) + " |");
                UnityEngine.Object.DestroyImmediate(go);
                if (status != "won") log.Add("Lv" + n + " (index " + lv.index + "): " + status + " sau " + view.TapCount + " lần chạm, t=" + view.Time.ToString("0"));
            }
            RenderTexture.active = null; cam.targetTexture = null;
            var md = new List<string>();
            md.Add("# Tự chơi hàng loạt (M6) — BuzzyBatchPlay, sinh tự động");
            md.Add("");
            md.Add("- Level " + from + "–" + to + " (số người chơi thấy; level ngoài phạm vi bị bỏ qua) · thắng " + won + " · bế tắc " + dead + " · quá giờ " + timeout + " · solver không tìm được thứ tự " + noOrder);
            md.Add("- Luật: băng đếm mọi pixel (chia " + cfg.frozenHitDivisor + "), Mystery " + (cfg.mysteryBlocks ? "chặn" : "không chặn") + " · dt " + dt + " s · xu cuối " + meta.Coins + " · thời gian thật " + (sw.ElapsedMilliseconds / 1000) + " s");
            md.Add("");
            md.Add("| Lv | Index gốc | Tag | Mechanic | Kết quả | Chạm | Giây mô phỏng | Popup mở khoá | Xu sau | ms thật |");
            md.Add("|---|---|---|---|---|---|---|---|---|---|");
            md.AddRange(rows);
            md.Add(""); md.Add("## Ghi chú"); md.AddRange(log);
            File.WriteAllLines(Path.Combine(outDir, "report.md"), md.ToArray());
            Debug.Log("[BuzzyBatchPlay] level " + from + "-" + to + ": thắng " + won + ", bế tắc " + dead + ", quá giờ " + timeout + ", không thứ tự " + noOrder + " -> " + outDir);
        }
    }
}
