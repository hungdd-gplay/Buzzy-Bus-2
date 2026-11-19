using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Buzzy.Core;

namespace Buzzy.Game
{
    /// <summary>
    /// M5: màn thắng (V31, V32), popup thắng (U2–U4), thua (design-decisions 3), mở khoá booster (U5), mua booster (mục 7), cài đặt, shop (mục 8).
    /// Dựng bằng uGUI trong code trên cùng canvas HUD; nút = vùng chạm theo % khung (cùng cách với booster bar) để bộ chụp batchmode điều khiển được.
    /// </summary>
    public partial class BuzzyGameView
    {
        public enum PopupKind { None, WinSequence, Completed, Lose, Unlock, Settings, Shop, BuyBooster, Language }

        /// <summary>Tiến trình người chơi; flow gán trước Init. Null → tạo mặc định theo level (test / bộ chụp).</summary>
        public MetaState Meta;
        public PopupKind Popup { get; private set; }
        public bool Lost { get; private set; }
        public float LostAt { get; private set; }
        /// <summary>Xu nhận được khi bấm nút ở popup thắng (flow cộng vào Meta).</summary>
        public int CoinsEarned { get; private set; }
        public event Action<int> LevelCompleted;
        public event Action RetryRequested;
        public event Action MetaChanged;
        /// <summary>Xem quảng cáo thưởng: gọi callback khi xem xong; trả false nếu không có quảng cáo (khi đó bỏ qua).</summary>
        public Func<Action, bool> ShowRewardAd;

        // M8: khoá Localize (Resources/Localization-Buzzy-<Lang>.txt); tên / câu popup theo clip gốc
        static readonly string[] BoosterNameKey = { "bz_booster_vip", "bz_booster_sky", "bz_booster_wand", "bz_booster_claw" };
        static readonly string[] BoosterDescKey = { "bz_desc_vip", "bz_desc_sky", "bz_desc_wand", "bz_desc_claw" };
        static string S(string key) { return BuzzyStrings.Get(key); }
        Text _badgeLabel;   // chữ Lv.N trên badge (làm mới khi đổi ngôn ngữ)

        class PBtn { public Vector2 c, size; public Action act; public string name; public HudAnchor a; }
        class Spark { public RectTransform rt; public Image im; public Vector2 o, dir; public float at, size; }

        RectTransform _popupRoot; float _popupAt; readonly List<PBtn> _btns = new List<PBtn>();
        readonly List<Spark> _sparks = new List<Spark>(); float _nextBurst; Image _overlayImg;
        RectTransform _sliderMark, _sliderRt; Text _adText; int _buyIdx; System.Random _fxRnd = new System.Random(7);

        void EnsureMeta()
        {
            if (Meta != null) return;
            Meta = new MetaState(_cfg.ToMetaConfig()); Meta.Level = _lv.index;
            for (int i = 0; i < 4; i++) if (Meta.IsBoosterUnlocked(i)) Meta.ClaimUnlock(i);   // không có flow: nhận sẵn quà mở khoá (= boosterStartCount cũ)
        }

        void Kill(GameObject go) { if (go == null) return; if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); }

        // ---------------------------------------------------------------- dựng
        RectTransform Root()
        {
            if (_popupRoot != null) return _popupRoot;
            var go = new GameObject("Popup", typeof(RectTransform)); go.transform.SetParent(_canvas.transform, false);
            _popupRoot = go.GetComponent<RectTransform>(); _popupRoot.anchorMin = Vector2.zero; _popupRoot.anchorMax = Vector2.one; _popupRoot.offsetMin = _popupRoot.offsetMax = Vector2.zero;
            return _popupRoot;
        }
        RectTransform PImg(string n, Sprite s, Color c, Vector2 pct, float w, float? asp = null) { return Img(n, s, c, Root(), new Vector2(pct.x, AnchorY(_pAnchor, pct.y)), w, asp); }

        // ---------------------------------------------------------------- M7b HUD 9:16 (U12–U14): neo mép + safe area
        /// <summary>Cách neo một nhóm UI: Center = giữ tâm màn (popup, loading); Top / Bottom = cách mép trên / dưới (HUD).</summary>
        public enum HudAnchor { Center, Top, Bottom }
        HudAnchor _pAnchor = HudAnchor.Center;   // neo đang dùng cho PImg / PText / nút popup
        /// <summary>Lề an toàn trên (px canvas; canvas = px màn hình vì referenceResolution = kích thước khung).</summary>
        float SafeTopPx { get { float extra = _cfg.hudSafeExtra.x * _canvasSize.x; if (!_cfg.hudUseSafeArea) return extra; var r = Screen.safeArea; float top = Screen.height - (r.y + r.height); return Mathf.Max(extra, top * _canvasSize.y / Mathf.Max(1f, Screen.height)); } }
        float SafeBottomPx { get { float extra = _cfg.hudSafeExtra.y * _canvasSize.x; if (!_cfg.hudUseSafeArea) return extra; var r = Screen.safeArea; return Mathf.Max(extra, r.y * _canvasSize.y / Mathf.Max(1f, Screen.height)); } }
        /// <summary>% y cũ (đo trên khung tham chiếu hudRefAspect) → % chiều cao khung thật, neo mép trên: khoảng cách mép tính theo bề ngang.</summary>
        float TopY(float yRef) { return (SafeTopPx + yRef * _cfg.hudRefAspect * _canvasSize.x) / _canvasSize.y; }
        float BottomY(float yRef) { return 1f - (SafeBottomPx + (1f - yRef) * _cfg.hudRefAspect * _canvasSize.x) / _canvasSize.y; }
        float CenterY(float yRef) { return 0.5f + (yRef - 0.5f) * _cfg.hudRefAspect * _canvasSize.x / _canvasSize.y; }
        /// <summary>Khoảng lệch dọc % cũ → % chiều cao thật (giữ cỡ theo bề ngang).</summary>
        float RefDy(float d) { return d * _cfg.hudRefAspect * _canvasSize.x / _canvasSize.y; }
        /// <summary>Chiều cao px → % chiều cao KHUNG THAM CHIẾU (đơn vị của mọi toạ độ popup trước khi qua CenterY); WRef: % bề ngang → cùng đơn vị.</summary>
        float RefH(float px) { return px / (_cfg.hudRefAspect * _canvasSize.x); }
        float WRef(float wPct) { return wPct / _cfg.hudRefAspect; }
        float AnchorY(HudAnchor a, float yRef) { return a == HudAnchor.Top ? TopY(yRef) : a == HudAnchor.Bottom ? BottomY(yRef) : CenterY(yRef); }
        Vector2 Top(Vector2 p) { return new Vector2(p.x, TopY(p.y)); }
        Vector2 Bottom(Vector2 p) { return new Vector2(p.x, BottomY(p.y)); }
        /// <summary>Bộ chụp / tool: toạ độ chạm thật (% khung) của một điểm HUD neo đáy / đỉnh ghi bằng % cũ (vd devPanelX[i], devPanelY).</summary>
        public Vector2 HudBottom(float x, float yRef) { return new Vector2(x, BottomY(yRef)); }
        public Vector2 HudTop(float x, float yRef) { return new Vector2(x, TopY(yRef)); }

        int Px(float f) { return Mathf.RoundToInt(f * _canvasSize.x / 1080f); }
        /// <summary>Tỉ lệ cao / rộng của khung hình (đổi % bề ngang sang % chiều cao).</summary>
        float WH { get { return _canvasSize.x / _canvasSize.y; } }

        Text PText(string s, Vector2 pct, float wPct, float hPct, int px, Color c, bool outline = false)
        {
            var go = new GameObject("T", typeof(RectTransform), typeof(Text)); go.transform.SetParent(Root(), false);
            var t = go.GetComponent<Text>(); t.text = s; t.font = _a.font; t.fontSize = px; t.color = c; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(wPct * _canvasSize.x, hPct * _cfg.hudRefAspect * _canvasSize.x); rt.anchoredPosition = new Vector2(pct.x * _canvasSize.x, -AnchorY(_pAnchor, pct.y) * _canvasSize.y);   // hPct = % cao khung tham chiếu (theo bề ngang)
            if (outline) { var o = go.AddComponent<Outline>(); o.effectColor = _cfg.textOutlineColor; o.effectDistance = new Vector2(px * 0.05f, -px * 0.05f); }
            return t;
        }

        RectTransform Btn(string n, Sprite s, Vector2 c, float w, string label, Sprite icon, Action act, int px = 48)
        {
            var r = PImg(n, s, Color.white, c, w, _cfg.popupButtonAspect);
            float hPct = r.sizeDelta.y / _canvasSize.y;   // % chiều cao THẬT: vùng chạm
            float tx = c.x;
            if (icon != null) { PImg(n + "Icon", icon, Color.white, new Vector2(c.x - w * 0.28f, c.y), w * 0.3f); tx = c.x + w * 0.1f; }
            // M8 (3c đo DE/RU): nhãn nút co chữ (best fit) trong ô hẹp hơn nút, 1 dòng, không đè huy hiệu AD / viền nút
            var lbl = PText(label, new Vector2(tx, c.y), icon != null ? w * 0.6f : w * 0.88f, RefH(r.sizeDelta.y) * 0.55f, Px(px), Color.white, true);
            lbl.horizontalOverflow = HorizontalWrapMode.Wrap; lbl.verticalOverflow = VerticalWrapMode.Truncate;
            lbl.resizeTextForBestFit = true; lbl.resizeTextMaxSize = Px(px); lbl.resizeTextMinSize = Px(16);
            _btns.Add(new PBtn { c = c, size = new Vector2(w, hPct), act = act, name = n, a = _pAnchor });
            return r;
        }

        readonly List<GameObject> _hiddenWorldTexts = new List<GameObject>();
        /// <summary>U15 (M7b): chữ thế giới (số nóc xe, "?"…) là TextMesh với shader chữ ZTest Always nên vẽ ĐÈ lên màn tối popup → ẩn tạm những chữ đang hiện khi mở popup có màn tối, hiện lại đúng các chữ đó khi đóng (chữ bị khoá / win ẩn hẳn không đụng).</summary>
        void HideWorldTextsTemp()
        {
            if (_hiddenWorldTexts.Count > 0 || _root == null) return;
            foreach (var t in _root.GetComponentsInChildren<TextMesh>(false)) { t.gameObject.SetActive(false); _hiddenWorldTexts.Add(t.gameObject); }
        }
        void RestoreWorldTexts() { foreach (var g in _hiddenWorldTexts) if (g != null) g.SetActive(true); _hiddenWorldTexts.Clear(); }

        /// <summary>M8 (U16): chữ mô tả nhiều ngôn ngữ: ô rộng popupDescWidth × bảng kem, tối đa popupDescLines dòng, best fit co chữ, NEO MÉP DƯỚI bảng kem (r2 RU: neo theo ảnh minh hoạ bị rơi xuống vùng tím vì sprite có lề trong suốt). yBottomRef = mép dưới ô (đơn vị khung tham chiếu).</summary>
        Text PDesc(string s, float yBottomRef, int px)
        {
            float hRef = RefH(px * 1.25f * Mathf.Max(1, _cfg.popupDescLines));
            var t = PText(s, new Vector2(0.5f, yBottomRef - hRef * 0.5f), _cfg.popupInnerWidth * _cfg.popupDescWidth, hRef, px, _cfg.popupTextColor, false);
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true; t.resizeTextMaxSize = px; t.resizeTextMinSize = Px(16);
            return t;
        }

        /// <summary>M8 (U16): nhãn HUD dựng bằng Label() (ô 260×90 mặc định) → thu ô theo vật chứa + best fit để chữ dài (RU "Полный", DE…) không tràn bong bóng Full / pill khoá / badge.</summary>
        void FitHudLabels()
        {
            FitLabel(_badgeLabel, 0.85f, 0.7f);
            for (int i = 0; i < 4; i++) FitLabel(_pillText[i], 0.9f, 0.8f);
            for (int i = 0; i < 3; i++) FitLabel(_fullLabels[i], 0.8f, 0.5f);
        }
        void FitLabel(Text t, float wFrac, float hFrac)
        {
            if (t == null) return; var p = t.transform.parent as RectTransform; if (p == null) return;
            var rt = t.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(p.sizeDelta.x * wFrac, p.sizeDelta.y * hFrac);
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true; t.resizeTextMaxSize = t.fontSize; t.resizeTextMinSize = Px(12);
        }

        void Overlay() { HideWorldTextsTemp(); _overlayImg = PImg("Overlay", null, new Color(_cfg.overlayColor.r, _cfg.overlayColor.g, _cfg.overlayColor.b, _cfg.overlayAlpha), new Vector2(0.5f, 0.5f), 1f, 1f / WH).GetComponent<Image>(); }

        /// <summary>
        /// Khung popup theo mẫu U2 / U5: khung tím ngoài (tab_menu) + tấm tiêu đề màu + bảng kem bên trong (frame_popup).
        /// Trả về y đỉnh / đáy khung ngoài (% chiều cao); innerFrac = chiều cao bảng kem theo tỉ lệ khung ngoài.
        /// </summary>
        void Frame(Sprite titleSprite, string title, string sub, float innerFrac, out float top, out float bottom)
        {
            var fr = PImg("Frame", _a.frameOuter, Color.white, new Vector2(0.5f, _cfg.popupCenterY), _cfg.popupWidth, _cfg.popupAspect);
            // U14 (M7b): top / bottom / h tính theo % cao KHUNG THAM CHIẾU (RefH) để mọi hàng bên trong đi qua CenterY đúng một lần (lần 1 ở 9:16 dùng % H thật → giãn 1,25×, nút rơi ngoài khung)
            float h = RefH(fr.sizeDelta.y); top = _cfg.popupCenterY - h * 0.5f; bottom = _cfg.popupCenterY + h * 0.5f;
            float iy = top + h * (_cfg.popupInnerTop + innerFrac * 0.5f);
            PImg("Inner", _a.framePopup, Color.white, new Vector2(0.5f, iy), _cfg.popupInnerWidth, innerFrac * h * _cfg.hudRefAspect / _cfg.popupInnerWidth);
            var tp = PImg("Title", titleSprite, Color.white, new Vector2(0.5f, top + _cfg.popupTitleDrop), _cfg.popupTitleWidth, _cfg.popupTitleAspect);
            float th = RefH(tp.sizeDelta.y);
            PText(title, new Vector2(0.5f, top + _cfg.popupTitleDrop - (sub != null ? th * 0.16f : 0f)), _cfg.popupTitleWidth, th * 0.5f, Px(sub != null ? 52 : 56), Color.white, true);
            if (sub != null) PText(sub, new Vector2(0.5f, top + _cfg.popupTitleDrop + th * 0.22f), _cfg.popupTitleWidth, th * 0.35f, Px(34), Color.white, true);
        }

        void CloseButton(float top, Action act)
        {
            float x = 0.5f + _cfg.popupWidth * 0.5f - _cfg.closeButtonSize * 0.45f, y = top + WRef(_cfg.closeButtonSize) * 0.35f;
            var r = PImg("Close", _a.closeButton, Color.white, new Vector2(x, y), _cfg.closeButtonSize);
            _btns.Add(new PBtn { c = new Vector2(x, y), size = new Vector2(_cfg.closeButtonSize, r.sizeDelta.y / _canvasSize.y), act = act, name = "Close", a = _pAnchor });
        }

        void CurrencyBar()
        {
            _pAnchor = HudAnchor.Top;   // U4 / U12: thanh xu + tim neo mép trên
            var bar = PImg("Coins", _a.currencyBar, Color.white, _cfg.currencyBarCenter, _cfg.currencyBarWidth);
            float h = bar.sizeDelta.y / _canvasSize.y;   // % H thật (vùng chạm); chữ dùng RefH
            PImg("CoinIcon", _a.coinIcon, Color.white, new Vector2(_cfg.currencyBarCenter.x - _cfg.currencyBarWidth * 0.45f, _cfg.currencyBarCenter.y), _cfg.currencyBarWidth * 0.36f);
            PText(Meta.Coins.ToString(), new Vector2(_cfg.currencyBarCenter.x + _cfg.currencyBarWidth * 0.1f, _cfg.currencyBarCenter.y), _cfg.currencyBarWidth, RefH(bar.sizeDelta.y), Px(40), Color.white, true);
            _btns.Add(new PBtn { c = _cfg.currencyBarCenter, size = new Vector2(_cfg.currencyBarWidth, h), act = OpenShop, name = "Shop", a = _pAnchor });
            // tim: bản đầu không giới hạn (U4: biểu tượng "Full")
            PImg("Heart", _a.heartInfinity, Color.white, _cfg.heartCenter, _cfg.heartWidth);
            var pill = PImg("HeartPill", _a.currencyBar, Color.white, new Vector2(_cfg.heartCenter.x + _cfg.heartWidth * 0.75f, _cfg.heartCenter.y), _cfg.heartWidth * 1.1f);
            var ft = PText(S("bz_full"), new Vector2(_cfg.heartCenter.x + _cfg.heartWidth * 0.85f, _cfg.heartCenter.y), _cfg.heartWidth * 0.95f, RefH(pill.sizeDelta.y) * 0.6f, Px(34), Color.white, true);
            ft.horizontalOverflow = HorizontalWrapMode.Wrap; ft.verticalOverflow = VerticalWrapMode.Truncate; ft.resizeTextForBestFit = true; ft.resizeTextMaxSize = Px(34); ft.resizeTextMinSize = Px(14);   // M8: RU "Полный" tràn pill
            _pAnchor = HudAnchor.Center;
        }

        public void ClosePopup()
        {
            if (_popupRoot != null) Kill(_popupRoot.gameObject);
            RestoreWorldTexts();   // U15
            _popupRoot = null; _btns.Clear(); _sparks.Clear(); _overlayImg = null; _sliderMark = null; _adText = null;
            Popup = PopupKind.None;
        }

        bool PopupTap(float nx, float ny)
        {
            if (Popup == PopupKind.None) return false;
            foreach (var b in _btns)
                if (Mathf.Abs(nx - b.c.x) <= b.size.x * 0.5f && Mathf.Abs(ny - AnchorY(b.a, b.c.y)) <= b.size.y * 0.5f) { Log?.Invoke("t=" + Time.ToString("0.00") + " nút " + b.name); Sfx(_a.sfxButton); b.act(); return true; }
            return true;   // popup mở: nuốt mọi chạm khác
        }

        void Changed() { MetaChanged?.Invoke(); }

        // ---------------------------------------------------------------- màn thắng (V31, V32)
        void ShowWin()
        {
            Log?.Invoke("t=" + Time.ToString("0.00") + " THẮNG: hết pixel");
            Sfx(_a.sfxWin);   // A6 (M7)
            HideWorldTexts(); EnsureMeta();
            ClosePopup(); Popup = PopupKind.WinSequence; _popupAt = Time; _nextBurst = Time + 0.2f;
            Overlay(); _overlayImg.color = new Color(_cfg.overlayColor.r, _cfg.overlayColor.g, _cfg.overlayColor.b, 0f);
            float rw = _cfg.ribbonTopWidth; var ru = PImg("RibbonU", _a.ribbonTop, Color.white, new Vector2(0.5f, 0f), rw);
            ru.anchoredPosition = new Vector2(0.5f * _canvasSize.x, -ru.sizeDelta.y * _cfg.ribbonTopVisible);
            _pAnchor = HudAnchor.Top;   // U12: ruy băng hai bên bám mép trên
            PopIn(PImg("RibbonL", _a.ribbonSide, Color.white, new Vector2(_cfg.ribbonSideX, _cfg.ribbonSideY), _cfg.ribbonSideWidth), 0.05f);
            var rr = PImg("RibbonR", _a.ribbonSide, Color.white, new Vector2(1f - _cfg.ribbonSideX, _cfg.ribbonSideY), _cfg.ribbonSideWidth); rr.localScale = new Vector3(-1, 1, 1); PopIn(rr, 0.05f, true);
            _pAnchor = HudAnchor.Center;
            // logo: chưa có art logo "BUZZY BUS" (sprite trong APK là bản reskin) → chữ trên tấm nền, chủ ý chờ art
            var plate = PImg("LogoPlate", _a.logoPlate, _cfg.logoPlateColor, new Vector2(0.5f, _cfg.winLogoY), _cfg.winLogoWidth, _cfg.logoPlateAspect); PopIn(plate, 0.15f);
            var t1 = PText("BUZZY", new Vector2(0.5f, _cfg.winLogoY - _cfg.logoLineGap), _cfg.winLogoWidth, 0.08f, Px(_cfg.logoFontPx), _cfg.logoTopColor, true); PopIn(t1.rectTransform, 0.2f);
            var t2 = PText("BUS", new Vector2(0.5f, _cfg.winLogoY + _cfg.logoLineGap), _cfg.winLogoWidth, 0.08f, Px(_cfg.logoFontPx), _cfg.logoBottomColor, true); PopIn(t2.rectTransform, 0.25f);
            // 4 loa tím hai góc dưới (V31): neo mép dưới (U13)
            _pAnchor = HudAnchor.Bottom;
            PopIn(PImg("TrumpetL1", _a.trumpetLeft, Color.white, _cfg.trumpetLeft, _cfg.trumpetWidth), 0.3f);
            PopIn(PImg("TrumpetL2", _a.trumpetLeft, Color.white, _cfg.trumpetLeft + _cfg.trumpetOffset, _cfg.trumpetWidth), 0.38f);
            PopIn(PImg("TrumpetR1", _a.trumpetRight, Color.white, new Vector2(1f - _cfg.trumpetLeft.x, _cfg.trumpetLeft.y), _cfg.trumpetWidth), 0.3f);
            PopIn(PImg("TrumpetR2", _a.trumpetRight, Color.white, new Vector2(1f - _cfg.trumpetLeft.x - _cfg.trumpetOffset.x, _cfg.trumpetLeft.y + _cfg.trumpetOffset.y), _cfg.trumpetWidth), 0.38f);
            _pAnchor = HudAnchor.Center;
        }

        void PopIn(RectTransform rt, float delay, bool mirror = false)
        {
            var s = mirror ? new Vector3(-1, 1, 1) : Vector3.one;
            rt.localScale = Vector3.zero;
            _pops.Add(new Pop { tr = rt, at = Time + delay, dur = 0.45f, scale = s });
        }

        void Burst()
        {
            var o = new Vector2(0.2f + 0.6f * (float)_fxRnd.NextDouble(), 0.12f + 0.3f * (float)_fxRnd.NextDouble());
            var tint = Color.HSVToRGB((float)_fxRnd.NextDouble(), 0.6f, 1f);
            for (int i = 0; i < _cfg.fireworkSparks; i++)
            {
                float a = (i + (float)_fxRnd.NextDouble() * 0.5f) / _cfg.fireworkSparks * Mathf.PI * 2f;
                var rt = PImg("Spark", _a.spark, tint, o, _cfg.sparkWidth);
                _sparks.Add(new Spark { rt = rt, im = rt.GetComponent<Image>(), o = o, dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a) / _cfg.hudRefAspect), at = Time, size = _cfg.sparkWidth });   // y theo đơn vị khung tham chiếu (CenterY đổi ra thật)
            }
        }

        void TickPopups(float dt)
        {
            if (Popup == PopupKind.WinSequence)
            {
                float u = Time - _popupAt;
                if (_overlayImg != null) _overlayImg.color = new Color(_cfg.overlayColor.r, _cfg.overlayColor.g, _cfg.overlayColor.b, _cfg.overlayAlpha * Mathf.Clamp01(u / 0.4f));
                if (Time >= _nextBurst && u < _cfg.winSequenceDuration - _cfg.fireworkDuration) { Burst(); _nextBurst = Time + _cfg.fireworkDuration * 0.45f; }
                for (int i = _sparks.Count - 1; i >= 0; i--)
                {
                    var s = _sparks[i]; float v = (Time - s.at) / _cfg.fireworkDuration;
                    if (v >= 1f || s.rt == null) { if (s.rt != null) Kill(s.rt.gameObject); _sparks.RemoveAt(i); continue; }
                    float e = 1f - (1f - v) * (1f - v);
                    var p = s.o + s.dir * (_cfg.fireworkRadius * e);
                    s.rt.anchoredPosition = new Vector2(p.x * _canvasSize.x, -CenterY(p.y) * _canvasSize.y);
                    s.rt.localScale = Vector3.one * (1f - 0.6f * v);
                    var c = s.im.color; c.a = 1f - v; s.im.color = c;
                }
                if (u >= _cfg.winSequenceDuration) ShowCompleted();
            }
            else if (Popup == PopupKind.Completed && _sliderMark != null)
            {
                int k = Meta.SliderIndex(Time - _popupAt); int n = Meta.Cfg.AdMultipliers.Length;
                float left = 0.5f - _cfg.sliderWidth * 0.5f, cw = _cfg.sliderWidth / n;
                _sliderMark.anchoredPosition = new Vector2((left + (k + 0.5f) * cw) * _canvasSize.x, _sliderRt.anchoredPosition.y);
                if (_adText != null) _adText.text = Meta.AdReward(Time - _popupAt).ToString();
            }
            if (Lost && Popup == PopupKind.None && Time >= LostAt + _cfg.loseDelay) ShowLose();
        }

        // ---------------------------------------------------------------- popup thắng (U2–U4)
        public void ShowCompleted()
        {
            EnsureMeta(); ClosePopup(); Popup = PopupKind.Completed; _popupAt = Time; Sfx(_a.sfxSwipe);
            Overlay(); CurrencyBar();
            float top, bottom; Frame(_a.titleYellow, S("bz_completed"), BuzzyStrings.Format("bz_level_n", _lv.index), _cfg.popupInnerHeightTall, out top, out bottom);
            float h = bottom - top;
            // New Feature %
            int fi = Meta.NextFeature(_lv.index);
            if (fi >= 0)
            {
                float y = top + h * _cfg.featureRowY;
                PText(S("bz_new_feature"), new Vector2(0.5f, y - h * 0.06f), _cfg.popupWidth, 0.04f, Px(34), _cfg.popupTextColor, false);
                float tx = 0.5f - _cfg.featureBarWidth * 0.08f;
                var track = PImg("FeatureTrack", _a.featureTrack, Color.white, new Vector2(tx, y), _cfg.featureBarWidth);
                float pr = (float)Meta.FeatureProgress(_lv.index), th = track.sizeDelta.y;
                var fill = PImg("FeatureFill", _a.featureTrack, _cfg.featureFillColor, new Vector2(tx, y), _cfg.featureBarWidth, 1f);
                fill.pivot = new Vector2(0f, 0.5f); fill.sizeDelta = new Vector2(Mathf.Max(th * 0.6f, _cfg.featureBarWidth * 0.95f * pr * _canvasSize.x), th * 0.72f);
                fill.anchoredPosition = new Vector2((tx - _cfg.featureBarWidth * 0.475f) * _canvasSize.x, -CenterY(y) * _canvasSize.y);
                PText(Mathf.RoundToInt(pr * 100f) + "%", new Vector2(0.5f - _cfg.featureBarWidth * 0.08f, y), _cfg.featureBarWidth, RefH(th), Px(30), Color.white, true);
                var ico = fi < _a.featureIcons.Length ? _a.featureIcons[fi] : null;
                PImg("FeatureIcon", ico, Color.white, new Vector2(0.5f + _cfg.featureBarWidth * 0.5f, y), _cfg.featureBarWidth * 0.2f);
            }
            // Your Reward + đống xu + thanh nhân
            float ry = top + h * _cfg.rewardRowY;
            PText(S("bz_your_reward"), new Vector2(0.5f, ry), _cfg.popupWidth, 0.04f, Px(34), _cfg.popupTextColor, false);
            PImg("Coins", _a.coinPile, Color.white, new Vector2(0.5f, ry + h * 0.11f), _cfg.coinPileWidth);
            float sy = top + h * _cfg.sliderRowY;
            _sliderRt = PImg("Slider", _a.rewardSlider, Color.white, new Vector2(0.5f, sy), _cfg.sliderWidth);
            float sh = _sliderRt.sizeDelta.y; int n = Meta.Cfg.AdMultipliers.Length; float cw = _cfg.sliderWidth / n, left = 0.5f - _cfg.sliderWidth * 0.5f;
            _sliderMark = PImg("SliderMark", _a.whiteFrame, _cfg.sliderMarkColor, new Vector2(left + 0.5f * cw, sy), cw * 0.9f, sh * 0.8f / (cw * 0.9f * _canvasSize.x));
            for (int i = 0; i < n; i++) PText("x" + Meta.Cfg.AdMultipliers[i], new Vector2(left + (i + 0.5f) * cw, sy), cw, RefH(sh), Px(30), Color.white, true);
            // nút xu / AD (U3)
            float by = bottom - h * _cfg.buttonRowUp;
            Btn("Coin", _a.buttonGreen, new Vector2(0.5f - _cfg.popupButtonGap, by), _cfg.popupButtonWidth, Meta.CoinReward.ToString(), _a.coinIcon, () => ClaimWin(false));
            var ad = Btn("Ad", _a.buttonOrange, new Vector2(0.5f + _cfg.popupButtonGap, by), _cfg.popupButtonWidth, Meta.AdReward(0).ToString(), _a.adIcon, () => ClaimWin(true));
            _adText = _popupRoot.GetChild(_popupRoot.childCount - 1).GetComponent<Text>();
        }

        /// <summary>Nhận thưởng (ad = xem quảng cáo nhân thưởng) rồi báo flow sang level kế.</summary>
        public void ClaimWin(bool ad)
        {
            if (Popup != PopupKind.Completed) return;
            int coins = ad ? Meta.AdReward(Time - _popupAt) : Meta.CoinReward;
            if (ad && ShowRewardAd != null) { if (!ShowRewardAd(() => FinishLevel(coins))) Log?.Invoke("không có quảng cáo"); }
            else FinishLevel(coins);
        }

        void FinishLevel(int coins)
        {
            CoinsEarned = coins; SyncBoosters(); ClosePopup(); Sfx(_a.sfxReward);   // A6 (M7)
            Log?.Invoke("t=" + Time.ToString("0.00") + " nhận " + coins + " xu, sang level kế");
            LevelCompleted?.Invoke(coins);
        }

        void SyncBoosters() { if (Meta == null) return; for (int i = 0; i < 4; i++) if (IsUnlocked(i)) Meta.Boosters[i] = BoosterCount[i]; }

        // ---------------------------------------------------------------- thua (design-decisions mục 3)
        void CheckLose()
        {
            if (Won || Lost || Popup != PopupKind.None) return;
            if (Game.Status != GameStatus.Deadlock || _flights.Count != 0 || _walks.Count != 0 || _vanish.Count != 0 || BoosterBusy) return;
            foreach (var c in _cars) if (c.move != null || c.leaving) return;
            Lost = true; LostAt = Time; Log?.Invoke("t=" + Time.ToString("0.00") + " BẾ TẮC: hết chỗ");
        }

        public void ShowLose()
        {
            EnsureMeta(); ClosePopup(); Popup = PopupKind.Lose; _popupAt = Time; Lost = true; Sfx(_a.sfxLose);   // A6 (M7)
            Overlay(); CurrencyBar();
            float top, bottom; Frame(_a.titleRed, S("bz_out_of_space"), null, _cfg.popupInnerHeight, out top, out bottom); float h = bottom - top;
            CloseButton(top, Retry);
            PImg("Illus", _a.loseIllustration, Color.white, new Vector2(0.5f, top + h * 0.40f), _cfg.loseIllusWidth);
            PDesc(S("bz_no_bus"), top + h * (_cfg.popupInnerTop + _cfg.popupInnerHeight) - WRef(_cfg.popupDescGap), Px(28));   // M8: sát mép dưới bảng kem, 2 dòng best fit
            float by = bottom - h * _cfg.buttonRowUp;
            Btn("Revive", _a.buttonGreen, new Vector2(0.5f - _cfg.popupButtonGap, by), _cfg.popupButtonWidth, S("bz_revive"), _a.adIcon, Revive, 40);
            Btn("Retry", _a.buttonOrange, new Vector2(0.5f + _cfg.popupButtonGap, by), _cfg.popupButtonWidth, S("bz_retry"), null, Retry, 40);
        }

        /// <summary>Revive: xem quảng cáo → hiệu ứng VIP Bus xoá 1 xe đang đỗ rồi chơi tiếp.</summary>
        public void Revive()
        {
            Action go = () =>
            {
                int slot = -1; for (int i = 0; i < 3; i++) if (Game.Slots[i] != null) { slot = i; break; }
                ClosePopup(); Lost = false;
                if (slot >= 0 && Game.VipBus(slot)) Log?.Invoke("t=" + Time.ToString("0.00") + " Revive: xoá xe ô " + slot);
            };
            if (ShowRewardAd != null) { if (!ShowRewardAd(go)) Log?.Invoke("không có quảng cáo"); } else go();
        }

        public void Retry() { ClosePopup(); Log?.Invoke("t=" + Time.ToString("0.00") + " chơi lại level"); RetryRequested?.Invoke(); }

        // ---------------------------------------------------------------- mở khoá booster (U5) + mua booster (mục 7)
        public void ShowUnlock(int i)
        {
            EnsureMeta(); ClosePopup(); Popup = PopupKind.Unlock; _popupAt = Time; _buyIdx = i; Sfx(_a.sfxIntroduce);   // A6 (M7)
            Overlay();
            float top, bottom; Frame(_a.titlePurple, S(BoosterNameKey[i]), null, _cfg.popupInnerHeight, out top, out bottom); float h = bottom - top;
            CloseButton(top, ClosePopup);
            PImg("Icon", _a.boosterIcons[i], Color.white, new Vector2(0.5f, top + h * 0.38f), _cfg.popupWidth * 0.4f);
            PDesc(S(BoosterDescKey[i]), top + h * (_cfg.popupInnerTop + _cfg.popupInnerHeight) - WRef(_cfg.popupDescGap), Px(30));
            Btn("Claim", _a.buttonGreen, new Vector2(0.5f, bottom - h * _cfg.buttonRowUp), _cfg.popupButtonWideWidth, S("bz_claim"), null, ClaimUnlockPopup, 40);
        }

        /// <summary>Nút Claim của popup mở khoá (bộ tự chơi M6 gọi thẳng). Đóng bằng X thì quà còn treo, popup hiện lại ở level kế.</summary>
        public void ClaimUnlockPopup()
        {
            if (Popup != PopupKind.Unlock) return; int i = _buyIdx;
            Meta.ClaimUnlock(i); BoosterCount[i] = Meta.Boosters[i]; RefreshBoosterUi(); Changed(); ClosePopup();
        }

        public void ShowBuyBooster(int i)
        {
            EnsureMeta(); ClosePopup(); Popup = PopupKind.BuyBooster; _popupAt = Time; _buyIdx = i; Sfx(_a.sfxSwipe);
            Overlay(); CurrencyBar();
            float top, bottom; Frame(_a.titlePurple, S(BoosterNameKey[i]), null, _cfg.popupInnerHeight, out top, out bottom); float h = bottom - top;
            CloseButton(top, ClosePopup);
            PImg("Icon", _a.boosterIcons[i], Color.white, new Vector2(0.5f, top + h * 0.38f), _cfg.popupWidth * 0.4f);
            PDesc(BuzzyStrings.Format("bz_buy_qty", Meta.Cfg.BoosterBuyQty) + "  " + S(BoosterDescKey[i]), top + h * (_cfg.popupInnerTop + _cfg.popupInnerHeight) - WRef(_cfg.popupDescGap), Px(28));
            float by = bottom - h * _cfg.buttonRowUp;
            Btn("Buy", _a.buttonGreen, new Vector2(0.5f - _cfg.popupButtonGap, by), _cfg.popupButtonWidth, Meta.Cfg.BoosterPrice[i].ToString(), _a.coinIcon, () =>
            {
                if (!Meta.BuyBooster(i)) { if (Vibrate != null) Vibrate(); Log?.Invoke("không đủ xu"); return; }
                BoosterCount[i] = Meta.Boosters[i]; RefreshBoosterUi(); Changed(); ClosePopup();
            }, 40);
            Btn("AdBooster", _a.buttonOrange, new Vector2(0.5f + _cfg.popupButtonGap, by), _cfg.popupButtonWidth, "+" + Meta.Cfg.BoosterAdQty, _a.adIcon, () =>
            {
                Action go = () => { Meta.AdBooster(i); BoosterCount[i] = Meta.Boosters[i]; RefreshBoosterUi(); Changed(); ClosePopup(); };
                if (ShowRewardAd != null) { if (!ShowRewardAd(go)) Log?.Invoke("không có quảng cáo"); } else go();
            }, 40);
        }

        // ---------------------------------------------------------------- cài đặt (mục 8, 13)
        public void OpenSettings()
        {
            if (Popup != PopupKind.Settings) Sfx(_a.sfxSwipe);
            EnsureMeta(); ClosePopup(); Popup = PopupKind.Settings; _popupAt = Time;
            Overlay();
            float top, bottom; Frame(_a.titlePurple, S("bz_settings"), null, _cfg.popupInnerHeight, out top, out bottom); float h = bottom - top;
            CloseButton(top, () => { Changed(); ClosePopup(); });
            string[] names = { "Sound", "Music", "Haptic" }; Sprite[] icons = { _a.soundIcon, _a.musicIcon, _a.hapticIcon };   // names: tên nút trong log / bộ chụp (không dịch)
            string[] shown = { S("bz_sound"), S("bz_music"), S("bz_haptic") };
            bool[] on = { Meta.Sound, Meta.Music, Meta.Haptic };
            for (int i = 0; i < 3; i++)
            {
                float y = top + h * (_cfg.settingsFirstRowY + i * _cfg.settingsRowStep);
                PImg("Ico" + i, icons[i], Color.white, new Vector2(0.5f - _cfg.popupWidth * 0.32f, y), _cfg.popupWidth * 0.11f);
                PText(shown[i], new Vector2(0.5f - _cfg.popupWidth * 0.08f, y), _cfg.popupWidth * 0.3f, 0.05f, Px(38), _cfg.popupTextColor, false);
                int k = i; var tc = new Vector2(0.5f + _cfg.popupWidth * 0.28f, y);
                var tg = PImg("Toggle" + i, on[i] ? _a.toggleOn : _a.toggleOff, Color.white, tc, _cfg.toggleWidth);
                _btns.Add(new PBtn { c = tc, size = new Vector2(_cfg.toggleWidth, tg.sizeDelta.y / _canvasSize.y), name = "Toggle" + names[i], act = () =>
                {
                    if (k == 0) Meta.Sound = !Meta.Sound; else if (k == 1) Meta.Music = !Meta.Music; else Meta.Haptic = !Meta.Haptic;
                    ApplyAudio();   // M7
                    OpenSettings();
                } });
            }
            // M8 (U17): hàng Language → popup chọn 15 ngôn ngữ
            float ly = top + h * _cfg.settingsLanguageRowY;
            PText(S("bz_language"), new Vector2(0.5f - _cfg.popupWidth * 0.2f, ly), _cfg.popupWidth * 0.36f, 0.05f, Px(38), _cfg.popupTextColor, false);
            Btn("Language", _a.buttonGreen, new Vector2(0.5f + _cfg.popupWidth * 0.2f, ly), _cfg.settingsLanguageButtonWidth, BuzzyStrings.NativeNames[BuzzyStrings.CurrentIndex], null, OpenLanguage, 30);
        }

        // ---------------------------------------------------------------- chọn ngôn ngữ (M8, U17): 15 ngôn ngữ của base, lưới languageCols × hàng
        public void OpenLanguage()
        {
            EnsureMeta(); ClosePopup(); Popup = PopupKind.Language; _popupAt = Time; Sfx(_a.sfxSwipe);
            Overlay();
            float top, bottom; Frame(_a.titlePurple, S("bz_language"), null, _cfg.popupInnerHeightTall, out top, out bottom); float h = bottom - top;
            CloseButton(top, OpenSettings);
            int cols = Mathf.Max(1, _cfg.languageCols), cur = BuzzyStrings.CurrentIndex;
            for (int i = 0; i < BuzzyStrings.Languages.Length; i++)
            {
                int r = i / cols, c = i % cols; int k = i;
                float cx = 0.5f + (cols == 1 ? 0f : (c / (float)(cols - 1) - 0.5f) * 2f * _cfg.languageColSpread * _cfg.popupWidth);
                float y = top + h * (_cfg.languageFirstRowY + r * _cfg.languageRowStep);
                Btn("Lang" + BuzzyStrings.Languages[i], i == cur ? _a.buttonGreen : _a.buttonOrange, new Vector2(cx, y), _cfg.languageButtonWidth, BuzzyStrings.NativeNames[i], null, () => SelectLanguage(BuzzyStrings.Languages[k]), _cfg.languageFontPx);
            }
        }

        /// <summary>Đổi ngôn ngữ (bộ chụp gọi thẳng được): nạp file, làm mới chữ HUD, mở lại Settings.</summary>
        public void SelectLanguage(string lang)
        {
            BuzzyStrings.Apply(lang);
            Log?.Invoke("t=" + Time.ToString("0.00") + " ngôn ngữ: " + BuzzyStrings.Current);
            RelocalizeHud();
            Changed();
            OpenSettings();
        }

        /// <summary>Làm mới chữ HUD đã dựng sẵn sau khi đổi ngôn ngữ (badge Lv, pill khoá booster, chữ Full, tooltip đang mở).</summary>
        public void RelocalizeHud()
        {
            if (_badgeLabel != null) _badgeLabel.text = BuzzyStrings.Format("bz_lv", DisplayLevel > 0 ? DisplayLevel : _lv.index);
            for (int i = 0; i < 4; i++) if (_pillText[i] != null) _pillText[i].text = BuzzyStrings.Format("bz_lv", _cfg.boosterUnlock[i]);
            for (int i = 0; i < 3; i++) if (_fullLabels[i] != null) _fullLabels[i].text = S("bz_full");
            if (_mode >= 0) ShowBoosterTip(_mode);
        }
        readonly Text[] _fullLabels = new Text[3];

        // ---------------------------------------------------------------- shop (mục 8) [thiết kế]: IAP nối ở M7
        public void OpenShop()
        {
            EnsureMeta(); ClosePopup(); Popup = PopupKind.Shop; _popupAt = Time; Sfx(_a.sfxSwipe);
            Overlay(); CurrencyBar();
            float top, bottom; Frame(_a.titleYellow, S("bz_shop"), null, _cfg.popupInnerHeight, out top, out bottom); float h = bottom - top;
            CloseButton(top, () => { Changed(); ClosePopup(); });
            for (int i = 0; i < _cfg.shopPackCoins.Length; i++)
            {
                float y = top + h * (_cfg.shopFirstRowY + i * _cfg.shopRowStep); int k = i;
                PImg("Pack" + i, i == 0 ? _a.coinSmall : _a.coinPile, Color.white, new Vector2(0.5f - _cfg.popupWidth * 0.32f, y), _cfg.popupWidth * 0.14f);
                PText("+" + _cfg.shopPackCoins[i], new Vector2(0.5f - _cfg.popupWidth * 0.06f, y), _cfg.popupWidth * 0.3f, 0.05f, Px(36), _cfg.popupTextColor, false);
                Btn("Pack" + i, _a.buttonGreen, new Vector2(0.5f + _cfg.popupWidth * 0.27f, y), _cfg.popupButtonWidth * 0.8f, _cfg.shopPackPrice[i], null, () => Log?.Invoke("IAP gói " + k + " (nối SDK ở M7)"), 30);
            }
            float ay = top + h * (_cfg.shopFirstRowY + _cfg.shopPackCoins.Length * _cfg.shopRowStep);
            PText(BuzzyStrings.Format("bz_free", _cfg.shopAdCoins), new Vector2(0.5f - _cfg.popupWidth * 0.1f, ay), _cfg.popupWidth * 0.4f, 0.05f, Px(34), _cfg.popupTextColor, false);
            Btn("AdCoins", _a.buttonOrange, new Vector2(0.5f + _cfg.popupWidth * 0.27f, ay), _cfg.popupButtonWidth * 0.8f, S("bz_ad"), _a.adIcon, () =>
            {
                Action go = () => { Meta.Coins += _cfg.shopAdCoins; Changed(); OpenShop(); };
                if (ShowRewardAd != null) { if (!ShowRewardAd(go)) Log?.Invoke("không có quảng cáo"); } else go();
            }, 30);
        }

        /// <summary>Bộ chụp: ép thắng ngay để xem màn thắng + popup.</summary>
        public void DebugForceWin() { if (!Won) { Won = true; WonAt = Time; ShowWin(); } }
    }
}
