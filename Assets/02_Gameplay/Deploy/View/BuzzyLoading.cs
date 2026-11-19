using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Buzzy.Game
{
    /// <summary>
    /// U1: màn loading: nền đêm (BG_Loading của APK), logo, thanh Loading có xe chạy, "Version x". Hết loadingDuration → scene BuzzyGame.
    /// Dựng bằng code (Build) để bộ chụp batchmode dùng lại được.
    /// </summary>
    public class BuzzyLoading : MonoBehaviour
    {
        public BuzzyLayoutConfig config;
        public BuzzyAssets assets;
        public string gameScene = "BuzzyGame";

        RectTransform _fill, _bus; float _t, _barW;
        Vector2 _size;

        void Start()
        {
            _size = new Vector2(Screen.width, Screen.height);
            var cam = new GameObject("Camera").AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            Build(transform, cam, _size, config, assets, 0f, out _fill, out _bus, out _barW);
        }

        void Update()
        {
            _t += Time.deltaTime; float p = Mathf.Clamp01(_t / config.loadingDuration);
            SetProgress(_fill, _bus, _barW, p);
            if (_t >= config.loadingDuration) { enabled = false; SceneManager.LoadScene(gameScene); }
        }

        public static void SetProgress(RectTransform fill, RectTransform bus, float barW, float p)
        {
            if (fill != null) fill.sizeDelta = new Vector2(Mathf.Max(1f, barW * p), fill.sizeDelta.y);
            if (bus != null && fill != null) bus.anchoredPosition = new Vector2(fill.anchoredPosition.x + barW * p, bus.anchoredPosition.y);
        }

        /// <summary>Dựng canvas loading; trả về thanh đầy + xe để cập nhật tiến trình.</summary>
        public static Canvas Build(Transform parent, Camera cam, Vector2 size, BuzzyLayoutConfig cfg, BuzzyAssets a, float progress, out RectTransform fill, out RectTransform bus, out float barW)
        {
            var cv = new GameObject("LoadingCanvas", typeof(Canvas), typeof(CanvasScaler)); cv.transform.SetParent(parent, false);
            var canvas = cv.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1f;
            var sc = cv.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = size;
            var ct = cv.transform;
            // M7b (U14): % y cũ đo trên khung tham chiếu hudRefAspect → giữ tâm theo bề ngang; "Version" neo mép dưới + safe area
            float R = cfg.hudRefAspect, safeBottom = cfg.hudSafeExtra.y * size.x;
            if (cfg.hudUseSafeArea) safeBottom = Mathf.Max(safeBottom, Screen.safeArea.y * size.y / Mathf.Max(1f, Screen.height));
            System.Func<float, float> C = y => 0.5f + (y - 0.5f) * R * size.x / size.y;
            System.Func<float, float> B = y => 1f - (safeBottom + (1f - y) * R * size.x) / size.y;
            // nền: texture vuông 2340, cắt theo khung (phủ hết chiều cao)
            if (a.loadingBackground != null)
            {
                var tex = a.loadingBackground;
                var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                float h = size.y, w = h * tex.width / tex.height; if (w < size.x) { w = size.x; h = w * tex.height / tex.width; }
                var bg = Img("BG", sp, Color.white, ct, size, new Vector2(0.5f, 0.5f), w / size.x, h / w);
            }
            // logo (chưa có art logo: chữ trên tấm nền, chủ ý chờ art)
            Img("LogoPlate", a.logoPlate, cfg.logoPlateColor, ct, size, new Vector2(0.5f, C(cfg.loadingLogoY)), cfg.loadingLogoWidth, cfg.logoPlateAspect);
            Txt("BUZZY", ct, a.font, size, new Vector2(0.5f, C(cfg.loadingLogoY - cfg.logoLineGap)), cfg.loadingLogoWidth, 0.08f, Px(cfg.logoFontPx, size), cfg.logoTopColor, cfg.textOutlineColor);
            Txt("BUS", ct, a.font, size, new Vector2(0.5f, C(cfg.loadingLogoY + cfg.logoLineGap)), cfg.loadingLogoWidth, 0.08f, Px(cfg.logoFontPx, size), cfg.logoBottomColor, cfg.textOutlineColor);
            // thanh loading: nền tím, phần đầy xanh lá, xe chạy theo
            Txt("Loading...", ct, a.font, size, new Vector2(0.5f, C(cfg.loadingBarY - 0.045f)), 0.6f, 0.05f, Px(40, size), Color.white, cfg.textOutlineColor);
            barW = cfg.loadingBarWidth * size.x;
            var track = Img("Track", a.featureTrack, new Color32(0x5B, 0x3F, 0x9E, 255), ct, size, new Vector2(0.5f, C(cfg.loadingBarY)), cfg.loadingBarWidth, cfg.loadingBarAspect);
            fill = Img("Fill", a.featureTrack, new Color32(0x7C, 0xD6, 0x2E, 255), ct, size, new Vector2(0.5f - cfg.loadingBarWidth * 0.5f, C(cfg.loadingBarY)), cfg.loadingBarWidth * 0.96f, cfg.loadingBarAspect * 0.7f);
            fill.pivot = new Vector2(0f, 0.5f); fill.anchoredPosition = new Vector2((0.5f - cfg.loadingBarWidth * 0.48f) * size.x, -C(cfg.loadingBarY) * size.y);
            barW = cfg.loadingBarWidth * 0.96f * size.x;
            bus = Img("Bus", a.busLoading, Color.white, ct, size, new Vector2(0.5f - cfg.loadingBarWidth * 0.5f, C(cfg.loadingBarY - 0.01f)), 0.17f, null);
            SetProgress(fill, bus, barW, progress);
            Txt("Version " + Application.version, ct, a.font, size, new Vector2(0.5f, B(0.965f)), 0.6f, 0.04f, Px(30, size), Color.white, cfg.textOutlineColor);
            Canvas.ForceUpdateCanvases();
            return canvas;
        }

        static int Px(float f, Vector2 size) { return Mathf.RoundToInt(f * size.x / 1080f); }

        static RectTransform Img(string name, Sprite sprite, Color color, Transform parent, Vector2 size, Vector2 pct, float widthPct, float? aspect)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var im = go.GetComponent<Image>(); im.sprite = sprite; im.color = color; im.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0.5f, 0.5f);
            float w = widthPct * size.x; float asp = aspect ?? (sprite != null ? sprite.rect.height / sprite.rect.width : 1f);
            rt.sizeDelta = new Vector2(w, w * asp); rt.anchoredPosition = new Vector2(pct.x * size.x, -pct.y * size.y);
            return rt;
        }

        static Text Txt(string s, Transform parent, Font font, Vector2 size, Vector2 pct, float wPct, float hPct, int px, Color c, Color outline)
        {
            var go = new GameObject("T", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>(); t.text = s; t.font = font; t.fontSize = px; t.color = c; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(wPct * size.x, hPct * size.y); rt.anchoredPosition = new Vector2(pct.x * size.x, -pct.y * size.y);
            var o = go.AddComponent<Outline>(); o.effectColor = outline; o.effectDistance = new Vector2(px * 0.05f, -px * 0.05f);
            return t;
        }
    }
}
