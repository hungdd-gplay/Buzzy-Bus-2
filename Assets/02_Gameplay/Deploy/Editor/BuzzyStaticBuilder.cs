using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Buzzy.Game.EditorTools
{
    /// <summary>
    /// M1: dựng khung hình tĩnh của một level (mặc định Lv.2) từ BuzzyLevels.asset + BuzzyLayoutConfig và chụp ảnh.
    /// -executeMethod Buzzy.Game.EditorTools.BuzzyStaticBuilder.BuildAndCapture ; biến môi trường: BUZZY_OUT (đường dẫn png), BUZZY_LEVEL.
    /// </summary>
    public static class BuzzyStaticBuilder
    {
        const string Res = "Assets/02_Gameplay/Res/";
        const string MeshDir = Res + "Buzzy/Meshes/";
        const string ConfigPath = Res + "Config/BuzzyLayoutConfig.asset";
        const string DbPath = Res + "Levels/BuzzyLevels.asset";
        const string ScenePath = Res + "Scenes/BuzzyStatic.unity";

        static Dictionary<int, Material> _passengerMats = new Dictionary<int, Material>();

        [MenuItem("Tools/Buzzy Bus/Build Static Scene (Lv.2)")]
        public static void BuildMenu() { Build(2, null); }

        public static void BuildAndCapture()
        {
            string level = Environment.GetEnvironmentVariable("BUZZY_LEVEL");
            string outPath = Environment.GetEnvironmentVariable("BUZZY_OUT");
            Build(string.IsNullOrEmpty(level) ? 2 : int.Parse(level), outPath);
        }

        public static BuzzyLayoutConfig LoadOrCreateConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<BuzzyLayoutConfig>(ConfigPath);
            if (cfg != null) return cfg;
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            cfg = ScriptableObject.CreateInstance<BuzzyLayoutConfig>();
            AssetDatabase.CreateAsset(cfg, ConfigPath);
            AssetDatabase.SaveAssets();
            return cfg;
        }

        static Mesh LoadMesh(string name)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(MeshDir + name + ".obj"))
                if (o is Mesh m) return m;
            throw new Exception("Không thấy mesh " + name);
        }

        static Material Mat(string shader, Color c)
        {
            var m = new Material(Shader.Find(shader));
            m.color = c;
            return m;
        }

        static GameObject Make(string name, Mesh mesh, Material mat, Vector3 pos, Vector3 euler, Vector3 scale, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localEulerAngles = euler; go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Quad(string name, Color c, float cx, float z0, float z1, float halfW, float y, Transform parent)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = name; q.transform.SetParent(parent, false);
            q.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            q.transform.localPosition = new Vector3(cx, y, (z0 + z1) * 0.5f);
            q.transform.localScale = new Vector3(halfW * 2f, z1 - z0, 1f);
            q.GetComponent<MeshRenderer>().sharedMaterial = Mat("Custom/BuzzyEnvironment", c);
            return q;
        }

        public static void Build(int levelIndex, string outPath)
        {
            var cfg = LoadOrCreateConfig();
            var db = AssetDatabase.LoadAssetAtPath<BuzzyLevelDatabase>(DbPath);
            if (db == null) throw new Exception("Chưa có " + DbPath + " (chạy Tools/Buzzy Bus/Import Levels)");
            var lv = db.Get(levelIndex);
            if (lv == null) throw new Exception("Không thấy level " + levelIndex);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("BuzzyStatic").transform;
            _passengerMats.Clear();

            var busMesh = LoadMesh("Bus");
            var blockMesh = LoadMesh("Block_Mesh");
            var busTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Res + "Buzzy/Textures/T_Bus.png");

            // nền + đường + vạch đứt
            AddGradientBackground(cam0: null, cfg: cfg, root: root);
            Quad("Road", cfg.roadColor, 0f, cfg.roadNearZ, cfg.roadFarZ, 40f, 0f, root);
            for (float x = -14f; x <= 14f; x += cfg.dashSpacing) Quad("Dash", cfg.dashColor, x, cfg.dashZ - cfg.dashHalfWidth, cfg.dashZ + cfg.dashHalfWidth, cfg.dashHalfLength, 0.03f, root);

            // xe: đầu cột vào ô bến, xe thứ hai ở hàng chờ
            for (int i = 0; i < lv.queues.Count; i++)
            {
                var cars = lv.queues[i].cars;
                for (int k = 0; k < cars.Count && k < 2; k++)
                {
                    var car = cars[k];
                    var mat = Mat("Custom/BuzzyVehicle", BuzzyPalette.Get(car.color));
                    mat.SetTexture("_BaseTex", busTex);
                    bool inSlot = k == 0;
                    Vector3 pos = inSlot ? new Vector3(cfg.slotX[i], 0f, cfg.slotZ) : new Vector3(cfg.queueX[i], 0f, cfg.queueZ + (k - 1) * 4.5f);
                    float yaw = inSlot ? cfg.slotYaw : 0f;
                    float s = inSlot ? cfg.busScale : cfg.queueBusScale * cfg.busScale; // queueBusScale: tỉ lệ so với xe ở bến (V11)
                    var carGo = Make("Car" + car.id, busMesh, mat, pos, new Vector3(0, yaw, 0), Vector3.one * s, root);
                    AddRoofNumber(carGo.transform, car.seats, cfg.roofNumberSize);
                }
            }

            // ảnh pixel
            float cx = cfg.boardCenterX - (lv.width * 0.5f) * cfg.cellPitchX, z0 = cfg.BoardZ0(lv.height);
            foreach (var p in lv.pixels)
            {
                Material m;
                if (!_passengerMats.TryGetValue(p.color, out m)) { m = Mat("Custom/BuzzyPassenger", BuzzyPalette.Get(p.color)); _passengerMats[p.color] = m; }
                Make("P", blockMesh, m, new Vector3(cx + p.x * cfg.cellPitchX, 0f, z0 + p.y * cfg.cellPitch), new Vector3(0, 180f, 0), new Vector3(cfg.blockScale * cfg.blockWidthScale, cfg.blockScale * cfg.blockHeightScale, cfg.blockScale), root);
            }

            // camera
            var camGo = new GameObject("Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = cfg.backgroundTop;
            cfg.ApplyCamera(cam, (float)cfg.captureSize.x / cfg.captureSize.y);
            if (_bg != null)
            {
                _bg.SetParent(cam.transform, false);
                float dist = 150f; var bs = cfg.BackdropSize(cam, dist);
                _bg.localPosition = new Vector3(0, 0, dist); _bg.localRotation = Quaternion.identity; _bg.localScale = new Vector3(bs.x, bs.y, 1f);
            }

            BuildUi(cam, cfg, levelIndex, root);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            if (!string.IsNullOrEmpty(outPath)) Capture(cam, cfg.captureSize, outPath);
        }


        // nền lavender chuyển sắc dọc (V3): quad phủ khung nhìn, gắn vào camera ở xa nhất
        static void AddGradientBackground(Camera cam0, BuzzyLayoutConfig cfg, Transform root)
        {
            var tex = new Texture2D(1, 64, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 64; i++) tex.SetPixel(0, 63 - i, Color.Lerp(cfg.backgroundTop, cfg.backgroundBottom, i / 63f));
            tex.Apply();
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = "BackgroundGradient";
            var mat = new Material(Shader.Find("Unlit/Texture")); mat.mainTexture = tex;
            q.GetComponent<MeshRenderer>().sharedMaterial = mat;
            q.transform.SetParent(root, false);
            q.tag = "Untagged";
            _bg = q.transform;
        }
        static Transform _bg;

        static Font _font;
        static Font GameFont()
        {
            if (_font == null) _font = AssetDatabase.LoadAssetAtPath<Font>(Res + "Buzzy/Fonts/LilitaOne-Regular.ttf");
            return _font;
        }

        static void AddRoofNumber(Transform car, int seats, float size)
        {
            var go = new GameObject("Seats");
            go.transform.SetParent(car, false);
            go.transform.localPosition = new Vector3(0f, 1.56f, 0.05f);   // nóc xe (mesh Bus cao 1.52)
            go.transform.localEulerAngles = new Vector3(90f, 0f, 0f);       // nằm trên nóc
            var tm = go.AddComponent<TextMesh>();
            tm.text = seats.ToString(); tm.font = GameFont(); tm.fontSize = 64; tm.characterSize = size * 0.1f;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = Color.white;
            var mainMat = new Material(GameFont().material); mainMat.renderQueue = 3010;
            go.GetComponent<MeshRenderer>().sharedMaterial = mainMat;
            var outMat = new Material(GameFont().material); outMat.renderQueue = 2990;
            // viền tối: 4 bản sao lệch nhẹ nằm dưới chữ trắng (ref: chữ trắng viền đen đậm)
            Vector2[] offs = { new Vector2(0.04f, 0f), new Vector2(-0.04f, 0f), new Vector2(0f, 0.04f), new Vector2(0f, -0.04f) };
            foreach (var o in offs)
            {
                var sh = new GameObject("Outline");
                sh.transform.SetParent(go.transform, false);
                sh.transform.localPosition = new Vector3(o.x * 0.6f, o.y * 0.6f, -0.02f);
                var t2 = sh.AddComponent<TextMesh>();
                t2.text = tm.text; t2.font = tm.font; t2.fontSize = tm.fontSize; t2.characterSize = tm.characterSize;
                t2.anchor = tm.anchor; t2.alignment = tm.alignment; t2.color = new Color(0.1f, 0.1f, 0.15f, 1f);
                sh.GetComponent<MeshRenderer>().sharedMaterial = outMat;
            }
        }

        static Sprite LoadSprite(string name)
        {
            string path = Res + "Buzzy/Sprites/" + name + ".png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.alphaIsTransparency = true;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static RectTransform Img(string name, Sprite sprite, Color color, Transform parent, Vector2 pctCenter, float widthPct, Vector2 canvas, float? aspectOverride = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var im = go.GetComponent<Image>(); im.sprite = sprite; im.color = color; im.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0.5f, 0.5f);
            float w = widthPct * canvas.x;
            float aspect = aspectOverride ?? (sprite != null ? sprite.rect.height / sprite.rect.width : 1f);
            rt.sizeDelta = new Vector2(w, w * aspect);
            rt.anchoredPosition = new Vector2(pctCenter.x * canvas.x, -pctCenter.y * canvas.y);
            return rt;
        }

        static void Sliced(string name, Sprite sprite, Color color, Transform parent, Vector2 pctCenter, float wPct, float hPct, Vector2 canvas)
        {
            var rt = Img(name, sprite, color, parent, pctCenter, wPct, canvas, hPct * canvas.y / (wPct * canvas.x));
            rt.GetComponent<Image>().type = Image.Type.Sliced;
            rt.SetSiblingIndex(0);
        }

        static void Label(string text, Transform parent, Vector2 anchored, int size, Color c)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>(); t.text = text; t.font = GameFont(); t.fontSize = size; t.color = c; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.sizeDelta = new Vector2(260, 90); rt.anchoredPosition = anchored;
        }

        // UI dựng theo % khung game (visual-check V3-V5, V24-V27)
        static void BuildUi(Camera cam, BuzzyLayoutConfig cfg, int level, Transform root)
        {
            var cv = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            cv.transform.SetParent(root, false);
            var canvas = cv.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1f;
            var sc = cv.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(cfg.captureSize.x, cfg.captureSize.y);
            var size = new Vector2(cfg.captureSize.x, cfg.captureSize.y);
            var ct = cv.transform;

            // khay trên cùng (sprite Tunnel), chỉ phần dưới nhìn thấy
            Img("Tray", LoadSprite("Tunnel"), Color.white, ct, new Vector2(0.5f, cfg.trayVisibleHeight * 0.5f - 0.11f + cfg.trayShift), 1.06f, size);
            // thanh đáy
            Img("BottomBar", null, cfg.bottomBarColor, ct, new Vector2(0.5f, (1f + cfg.bottomBarTop) * 0.5f), 1f, size, (1f - cfg.bottomBarTop) * size.y / size.x);
            // badge Lv
            var badge = Img("LvBadge", LoadSprite("lvl_bar"), Color.white, ct, cfg.badgeCenter, cfg.badgeWidth, size);
            Label("Lv." + level, badge, new Vector2(badge.sizeDelta.x * 0.5f, -badge.sizeDelta.y * 0.38f), 52, new Color32(0x4A, 0x45, 0x9A, 255));
            // 4 booster khóa
            string[] lv = { "Lv.4", "Lv.8", "Lv.13", "Lv.16" };
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector2(cfg.boosterX[i], cfg.boosterY);
                Img("Booster" + i, LoadSprite("btn_Booster2"), Color.white, ct, c, cfg.boosterSize, size);
                Img("Lock" + i, LoadSprite("icon_Lock"), Color.white, ct, new Vector2(c.x, c.y - 0.008f), cfg.boosterSize * 0.52f, size);
                var pill = Img("Pill" + i, LoadSprite("lvl_bar"), Color.white, ct, new Vector2(c.x, c.y + 0.034f), cfg.boosterSize * 0.55f, size, 0.5f);
                Label(lv[i], pill, new Vector2(pill.sizeDelta.x * 0.5f, -pill.sizeDelta.y * 0.5f), 34, new Color32(0x4A, 0x45, 0x9A, 255));
            }
            // bong bóng "Full" cạnh xe ở ô 1 (V16)
            var full = Img("FullBubble", AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"), new Color(0.72f, 0.75f, 0.95f, 1f), ct, cfg.fullBubbleCenter, cfg.fullBubbleWidth, size, 1f);
            Label("Full", full, new Vector2(full.sizeDelta.x * 0.5f, -full.sizeDelta.y * 0.45f), 40, new Color32(0x4A, 0x45, 0x9A, 255));
            // đuôi bong bóng: cuống + chấm nhỏ phía dưới (V16)
            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            // gờ bo tròn quanh bến (V4): bậc dưới + hai khối bên hàng chờ
            var bg = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            Img("Settings", LoadSprite("btn_Setting"), Color.white, ct, cfg.settingsCenter, cfg.settingsSize, size);
            Canvas.ForceUpdateCanvases();
        }

        static void Capture(Camera cam, Vector2Int size, string path)
        {
            var rt = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log("[BuzzyStaticBuilder] Đã chụp " + path);
        }
    }
}
