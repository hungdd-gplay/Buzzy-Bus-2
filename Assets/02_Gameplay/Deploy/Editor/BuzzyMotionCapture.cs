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
            a.iceMesh = Mesh("Ice"); a.keyMesh = Mesh("Key"); a.lockMesh = Mesh("Lock"); a.heliMesh = Mesh("Helicopter"); a.heliWingMesh = Mesh("Helicopter_Wing"); a.clawMesh = Mesh("Claw");
            a.boosterIcons = new[] { Sprite("booster_1"), Sprite("booster_2"), Sprite("booster_3"), Sprite("booster_4") };
            a.countBadge = Sprite("btn_Count"); a.speedIcon = Sprite("icon_sppedup"); a.speedGreen = Sprite("btn_Booster"); a.speedPurple = Sprite("btn_Booster2");
            a.pauseIcon = Sprite("icon_Pause_0"); a.levelBarHard = Sprite("lv_hardbar"); a.levelBarSuper = Sprite("lv_superhardbar");
            a.speedHalfGreen = Sprite("btn_x2_1"); a.speedHalfPurple = Sprite("btn_x2_2"); a.speedHalfRim = Sprite("btn_x2_base2");
            a.busTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Res + "Buzzy/Textures/T_Bus.png");
            a.font = AssetDatabase.LoadAssetAtPath<Font>(Res + "Buzzy/Fonts/LilitaOne-Regular.ttf");
            a.tunnel = Sprite("Tunnel"); a.levelBar = Sprite("lvl_bar"); a.boosterCircle = Sprite("btn_Booster2");
            a.lockIcon = Sprite("icon_Lock"); a.settings = Sprite("btn_Setting");
            a.knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            // M5
            a.ribbonTop = Sprite("ribbon_U"); a.ribbonSide = Sprite("ribbon_L"); a.trumpetLeft = Sprite("trumpet_L"); a.trumpetRight = Sprite("trumpet_R"); a.spark = Sprite("spark"); a.logoPlate = Sprite("white_frame");
            a.framePopup = Sprite("frame_popup"); a.frameOuter = Sprite("tab_menu"); a.titlePlate = Sprite("title_btn"); a.titleYellow = Sprite("title_Yellow2"); a.titlePurple = Sprite("title_Purple"); a.titleRed = Sprite("tite_Red");
            a.buttonGreen = Sprite("btn_Green"); a.buttonOrange = Sprite("btn_Orange"); a.closeButton = Sprite("btn_X"); a.whiteFrame = Sprite("white_frame");
            a.currencyBar = Sprite("currency_bar"); a.coinIcon = Sprite("icon_coin"); a.coinPile = Sprite("icon_gold5"); a.coinSmall = Sprite("icon_gold2"); a.adIcon = Sprite("icon_Ads");
            a.heartInfinity = Sprite("icon_HeartInfinity"); a.rewardSlider = Sprite("reward_slider"); a.featureTrack = Sprite("ui_bst_unlock");
            a.soundIcon = Sprite("icon_Sound"); a.musicIcon = Sprite("icon_Music"); a.hapticIcon = Sprite("icon_Haptic"); a.toggleOn = Sprite("slider_on"); a.toggleOff = Sprite("slider_off");
            a.loseIllustration = Sprite("illus_lose"); a.busLoading = Sprite("icon_BusLoading");
            a.featureIcons = new[] { Sprite("ic_MysteryPixel"), Sprite("ic_MysteryCanvas"), Sprite("icon_Lock") };
            a.loadingBackground = AssetDatabase.LoadAssetAtPath<Texture2D>(Res + "Buzzy/Textures/BG_Home_Halloween.png");   // U1: nền đêm Halloween như mẫu
            a.colorTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Res + "Buzzy/Textures/Color.png");   // góp ý 14
            a.fullSignMesh = Mesh("Sign_Full");
            a.shadowTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Res + "Buzzy/Textures/Shadow.png");   // V14: bóng xe
            // M7: âm thanh (tên file đã đổi từ APK, design-decisions 17)
            a.sfxTap = Clip("tap_passenger"); a.sfxPop = Clip("pop"); a.sfxWrong = Clip("choose_wrong"); a.sfxWin = Clip("win"); a.sfxLose = Clip("on_lose");
            a.sfxButton = Clip("button_click"); a.sfxPositive = Clip("positive_click"); a.sfxBubble = Clip("bubble"); a.sfxSwipe = Clip("menu_swipe"); a.sfxReward = Clip("reward_receive");
            a.sfxIntroduce = Clip("introduce_booster"); a.sfxChangeFalling = Clip("change_falling"); a.sfxBusLeave = Clip("bus_leave");
            a.sfxIceInit = Clip("ice_init"); a.sfxIceBreak = Clip("ice_break"); a.sfxIceBreak2 = Clip("ice_break_2"); a.sfxVip = Clip("vip_bus"); a.sfxHeli = Clip("helicopter");
            a.sfxClawStart = Clip("claw_start"); a.sfxClawEnd = Clip("claw_end"); a.sfxShuffle = Clip("shuffle_car");
            a.music = new[] { Clip("bg_1"), Clip("bg_2") };
            EditorUtility.SetDirty(a); AssetDatabase.SaveAssets();
            return a;
        }

        [MenuItem("Tools/Buzzy Bus/Refresh Assets")]
        public static void RefreshAssets() { LoadOrCreateAssets(); Debug.Log("[BuzzyMotionCapture] Đã gán lại BuzzyAssets (mesh, sprite, âm thanh)"); }

        static AudioClip Clip(string n)
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(Res + "Buzzy/Audio/" + n + ".wav");
            if (c == null) Debug.LogWarning("Không thấy âm thanh " + n);
            return c;
        }

        [MenuItem("Tools/Buzzy Bus/Build Game Scene")]
        public static void BuildGameScene()
        {
            var cfg = BuzzyStaticBuilder.LoadOrCreateConfig();
            var assets = LoadOrCreateAssets();
            var db = AssetDatabase.LoadAssetAtPath<BuzzyLevelDatabase>(Res + "Levels/BuzzyLevels.asset");
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            var go = new GameObject("BuzzyBootstrap"); var b = go.AddComponent<BuzzyBootstrap>();
            b.database = db; b.config = cfg; b.assets = assets; b.level = 0;
            Directory.CreateDirectory(Res + "Scenes");
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, Res + "Scenes/BuzzyGame.unity");
            Debug.Log("[BuzzyMotionCapture] Đã tạo " + Res + "Scenes/BuzzyGame.unity");
        }

        static Mesh Mesh(string n)
        {
            // góp ý 14: OBJ gốc có 2 sub-mesh (X_0 = hình thật, X_1 = quad 2 mặt); LoadAllAssets trả quad trước với Barrier / Sign_Full → lấy sub-mesh nhiều đỉnh nhất
            Mesh best = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Res + "Buzzy/Meshes/" + n + ".obj")) if (o is Mesh m && (best == null || m.vertexCount > best.vertexCount)) best = m;
            if (best == null) throw new Exception("Không thấy mesh " + n);
            return best;
        }

        static Sprite Sprite(string n)
        {
            string path = Res + "Buzzy/Sprites/" + n + ".png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.alphaIsTransparency = true; ti.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Góp ý 14 (bài học): asset BuzzyLayoutConfig lưu từ M5 giữ giá trị cũ của trường đã có → đổi mặc định trong code KHÔNG tới asset.
        /// Chép mặc định code vào asset cho các trường BUZZY_FIELDS="a,b,c" (không có → mọi trường lệch), in danh sách đã đổi. Menu: Tools/Buzzy Bus/Sync Config Defaults.</summary>
        [MenuItem("Tools/Buzzy Bus/Sync Config Defaults")]
        public static void SyncConfigDefaults()
        {
            var cfg = BuzzyStaticBuilder.LoadOrCreateConfig(); var def = ScriptableObject.CreateInstance<BuzzyLayoutConfig>();
            var only = Environment.GetEnvironmentVariable("BUZZY_FIELDS"); var set = string.IsNullOrEmpty(only) ? null : new HashSet<string>(only.Split(','));
            bool dry = Environment.GetEnvironmentVariable("BUZZY_DRY") == "1";   // chỉ liệt kê trường lệch, không ghi (bước xem trước khi sync theo AGENTS)
            int n = 0;
            foreach (var f in typeof(BuzzyLayoutConfig).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (set != null && !set.Contains(f.Name)) continue;
                var a = f.GetValue(cfg); var b = f.GetValue(def);
                if (a == null ? b == null : a.Equals(b)) continue;
                if (f.FieldType.IsArray) continue;   // mảng: không tự đồng bộ (so tay)
                Debug.Log("[SyncConfig] " + f.Name + ": " + a + " -> " + b); if (!dry) f.SetValue(cfg, b); n++;
            }
            if (dry) { Debug.Log("[SyncConfig] (chỉ xem) " + n + " trường lệch, KHÔNG ghi"); UnityEngine.Object.DestroyImmediate(def); return; }
            EditorUtility.SetDirty(cfg); AssetDatabase.SaveAssets();
            Debug.Log("[SyncConfig] đã đổi " + n + " trường");
            UnityEngine.Object.DestroyImmediate(def);
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
            var size = new Vector2Int(324, 720); var szv = Environment.GetEnvironmentVariable("BUZZY_SIZE");
            if (!string.IsNullOrEmpty(szv)) { var pp = szv.ToLower().Split('x'); size = new Vector2Int(int.Parse(pp[0]), int.Parse(pp[1])); }   // M7: chụp nét để soi

            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            var camGo = new GameObject("Camera"); var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = cfg.backgroundTop;
            cfg.ApplyCamera(cam, (float)size.x / size.y);
            var go = new GameObject("BuzzyGame"); var view = go.AddComponent<BuzzyGameView>();
            var log = new List<string>();
            view.Log = s => log.Add(s);
            int vibrations = 0; view.Vibrate = () => { vibrations++; log.Add("RUNG (haptic) lần " + vibrations + " t=" + view.Time.ToString("0.00")); };
            // M7: BUZZY_DEV=1 → hiện bảng thử nghiệm T1 (<, Lv, >, R) để chụp; BUZZY_DEVTAP=t → bấm nút ">" lúc t (log DEV)
            if (Environment.GetEnvironmentVariable("BUZZY_DEV") == "1") { view.DevPanel = true; view.DevMaxLevel = db.PlayableCount; view.DevLevelRequested = n => log.Add("DEV yêu cầu level " + n + " t=" + view.Time.ToString("0.00")); view.DevRestartRequested = () => log.Add("DEV yêu cầu chơi lại t=" + view.Time.ToString("0.00")); }
            view.Init(lv, cfg, assets, cam, size);

            var rt = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
            var tex = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            bool probed = false; float dt = 1f / 60f, nextShot = 0f, nextTap = 0f, endAt = float.MaxValue, claimAt = -1f; int shot = 0;
            view.LevelCompleted += c => log.Add("LEVEL XONG: +" + c + " xu (flow sẽ sang level kế)");
            // thứ tự chạm: tìm bằng Solver.FindOrder (level có mechanic thì greedy thuần có thể bế tắc)
            var sp = new double[3][]; for (int i = 0; i < 3; i++) sp[i] = new[] { view.Game.SlotX[i], view.Game.SlotY[i] };
            var order = Solver.FindOrder(lv.ToCore(), view.Game.Rules, sp, 400, 1);
            log.Add(order == null ? "Solver: KHÔNG tìm được thứ tự thắng (level cần booster)" : "Solver: " + order.Count + " lần chạm");
            int oi = 0;
            // kịch bản booster: BUZZY_SCRIPT="t:vip:slot;t:sky:color;t:wand:color;t:claw:col:pos;t:speed;t:mode:i;t:pick:p1:p2"
            // góp ý 11: "mode:i" bấm nút booster i như người chơi (tooltip B9, màn tối VIP, camera trượt Claw B8; Wand dùng ngay), "pick:p1:p2" chọn đích cho booster đang chọn (VIP ô; Claw cột:vị trí; Sky toạ độ lưới)
            var script = new List<string[]>();
            var sv = Environment.GetEnvironmentVariable("BUZZY_SCRIPT");
            if (!string.IsNullOrEmpty(sv)) foreach (var part in sv.Split(';')) if (part.Trim().Length > 0) script.Add(part.Trim().Split(':'));
            script.Sort((a, b) => float.Parse(a[0], System.Globalization.CultureInfo.InvariantCulture).CompareTo(float.Parse(b[0], System.Globalization.CultureInfo.InvariantCulture)));
            int si = 0;
            while (view.Time < maxT && view.Time < endAt)
            {
                view.Tick(dt);
                if (!probed && view.Time >= cfg.inputDelay + 0.1f) { probed = true; view.TapColor((int)Env("BUZZY_PROBE", 20f)); }   // chạm sai thử: màu không có xe đỗ (mặc định 20 = không level nào dùng; BUZZY_PROBE=<màu có pixel> để thấy cụm rung): phải rung
                float devTap = Env("BUZZY_DEVTAP", -1f);
                if (devTap >= 0f && view.Time >= devTap && view.DevPanel) { var hp = view.HudBottom(cfg.devPanelX[2], cfg.devPanelY); bool hit = view.DebugUiTap(hp.x, hp.y); /* M7b: bảng thử neo đáy theo HUD 1f */ log.Add("DEV bấm '>' tại (" + cfg.devPanelX[2] + ", " + cfg.devPanelY + ") -> " + hit); Environment.SetEnvironmentVariable("BUZZY_DEVTAP", "-1"); }
                while (si < script.Count && view.Time >= float.Parse(script[si][0], System.Globalization.CultureInfo.InvariantCulture))
                {
                    var a = script[si++]; string name = a[1]; int p1 = a.Length > 2 ? int.Parse(a[2]) : 0, p2 = a.Length > 3 ? int.Parse(a[3]) : 0;
                    // tham số -1 = tự chọn: sky = màu xe đang đỗ còn chỗ; wand = màu pixel không có xe đỗ; claw = cột đầu có xe
                    if (name == "sky" && p1 < 0) foreach (var car in view.Game.Slots) if (car != null && car.Free > 0 && view.Game.Board.CountColor(car.Color) > 0) { p1 = car.Color; break; }
                    if (name == "wand" && p1 < 0)
                        for (int cc = 0; cc < 21 && p1 < 0; cc++)
                        {
                            bool parked = false; foreach (var car in view.Game.Slots) if (car != null && car.Color == cc) parked = true;
                            if (!parked && view.Game.Board.CountColor(cc) > 0) p1 = cc;
                        }
                    if (name == "claw" && p1 < 0) for (int qq = 0; qq < 3 && p1 < 0; qq++) if (view.Game.Queues[qq].Count > 0) p1 = qq;
                    bool ok = name == "vip" ? view.UseVipBus(p1) : name == "sky" ? view.UseSkyGrab(p1) : name == "wand" ? view.UseMagicWand(p1) : name == "claw" ? view.UseBusClaw(p1, p2)
                        : name == "mode" ? view.DebugBoosterMode(p1) : name == "pick" ? view.DebugBoosterPick(p1, p2) : name == "tap" ? view.TapColor(p1) : false;   // tap:màu (góp ý 13: chạm khi xe đang xuống)
                    if (name == "speed") view.ToggleSpeed();
                    if (ok) order = null;   // booster đổi trạng thái: bỏ thứ tự đã tìm, chơi tham lam tiếp
                    log.Add("KỊCH BẢN t=" + view.Time.ToString("0.00") + " " + string.Join(":", a) + " -> " + (name == "speed" ? "x" + view.TimeScale : ok.ToString()));
                }
                if (view.Time >= nextTap && view.Game.InFlight == 0 && !view.Won && view.Time >= cfg.inputDelay + 0.2f)
                {
                    int? c = null;
                    if (order != null && oi < order.Count) { c = order[oi]; }
                    else if (order == null) c = view.Game.TappableColor();
                    bool tapped = false;
                    if (c.HasValue) tapped = view.TapColor(c.Value);
                    if (tapped && order != null) oi++;
                    nextTap = view.Time + 0.5f;
                    if (!c.HasValue && view.Game.Status == GameStatus.Deadlock) { log.Add("BẾ TẮC t=" + view.Time.ToString("0.00")); break; }
                    if (c.HasValue && !tapped && view.Game.Status == GameStatus.Deadlock && view.Game.InFlight == 0) { log.Add("BẾ TẮC (chạm không khớp) t=" + view.Time.ToString("0.00")); break; }
                }
                if (view.Won && endAt == float.MaxValue) endAt = view.Time + Env("BUZZY_AFTERWIN", 1.2f);
                // M5: BUZZY_CLAIM=coin|ad → bấm nút ở popup thắng sau BUZZY_CLAIMAT giây kể từ khi popup hiện
                if (view.Popup == BuzzyGameView.PopupKind.Completed && claimAt < 0f) claimAt = view.Time + Env("BUZZY_CLAIMAT", 1.0f);
                if (claimAt > 0f && view.Time >= claimAt && view.Popup == BuzzyGameView.PopupKind.Completed) { var cl = Environment.GetEnvironmentVariable("BUZZY_CLAIM"); if (!string.IsNullOrEmpty(cl)) view.ClaimWin(cl == "ad"); claimAt = float.MaxValue; }
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
