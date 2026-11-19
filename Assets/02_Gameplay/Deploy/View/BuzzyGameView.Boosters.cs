using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Buzzy.Core;

namespace Buzzy.Game
{
    /// <summary>
    /// M3 + góp ý 11 (M7): 4 booster theo clip gốc (visual-check mục H, B1–B9) + nút tốc độ x1/x2.
    /// Luật: BusGameBoosters.cs (design-decisions 4–6); số: BuzzyLayoutConfig khối "Booster".
    /// Móc giữ cho các phiên khác: DevTap trong UiTap, Sfx(...), TapPixel → TapColor(color, x, y), chữ ký UseVipBus / UseSkyGrab / UseMagicWand / UseBusClaw / ToggleSpeed.
    /// </summary>
    public partial class BuzzyGameView
    {
        public const int VipBus = 0, SkyGrab = 1, MagicWand = 2, BusClaw = 3;
        // B9: câu tooltip đáy đúng chữ clip (Magic Wand dùng ngay nên clip không có tooltip)
        static readonly string[] BoosterHintKey = { "bz_tip_vip", "bz_tip_sky", "bz_tip_wand", "bz_tip_claw" };   // M8: câu tooltip theo clip, qua Localize

        /// <summary>Số booster còn lại (kho xu / quảng cáo làm ở M5).</summary>
        public readonly int[] BoosterCount = new int[4];
        int _mode = -1;
        readonly Image[] _boosterRing = new Image[4];
        readonly Text[] _countText = new Text[4];
        readonly GameObject[] _countObj = new GameObject[4];
        Text _hint; Image _speedImg, _speedImgR, _speedIconImg; Text _speedText; bool _fast;
        Transform _hudRoot; RectTransform _tipPanel, _dim;

        // ---- hiệu ứng đang chạy
        class FillFx { public Pass p; public CarV car; public bool lane; public float start; public Vector3 from, off; }
        class SuckFx { public Pass p; public CarV car; public float start; public Vector3 from; }
        class HeliFx { public Transform tr, wing; public float t0; public Vector3 enter, target; public List<SuckFx> suck = new List<SuckFx>(); public bool sucked; public Transform shadow; }
        class ClawFx { public Transform claw, cable, swirl; public CarV cv; public ClawResult res; public int col; public float t0; public Vector3 busFrom; public bool filled, shifted; }
        class WandFx { public float t0; public List<CarV>[] cols; public bool[] grey, shown, numbers; public List<GameObject> greys = new List<GameObject>(); public HashSet<CarV> hidden = new HashSet<CarV>(); }
        readonly Text[] _pillText = new Text[4];   // chữ Lv.N trên pill khoá (làm mới khi đổi ngôn ngữ)
        readonly List<FillFx> _fills = new List<FillFx>();
        readonly List<HeliFx> _heli = new List<HeliFx>();
        readonly List<ClawFx> _claws = new List<ClawFx>();
        readonly List<CarV> _laneCars = new List<CarV>();
        WandFx _wand;
        // camera trượt lên khi chọn xe cho Bus Claw (B8)
        Vector3 _camBase; bool _camBaseSet; float _camShift, _camZoom = 1f, _camShiftTo, _camZoomTo = 1f, _camShiftFrom, _camZoomFrom = 1f, _camT0 = -1f, _camDur;

        /// <summary>Hiệu ứng booster còn đang chạy (fd: thêm vào điều kiện hiện popup thắng / thua nếu cần).</summary>
        public bool BoosterBusy { get { return _fills.Count > 0 || _heli.Count > 0 || _claws.Count > 0 || _wand != null; } }

        bool IsUnlocked(int i) { return _lv.index >= _cfg.boosterUnlock[i]; }
        public bool SpeedUnlocked { get { return _lv.index >= _cfg.speedUnlockLevel; } }
        public bool Fast { get { return _fast; } }

        void InitBoosters()
        {
            EnsureMeta();
            for (int i = 0; i < 4; i++) BoosterCount[i] = IsUnlocked(i) ? Meta.Boosters[i] : 0;
            Game.Boosters = _cfg.ToBoosterRules();
            Game.LaneCarDeparted += OnLaneCarDeparted;
            FitHudLabels();   // M8: nhãn Full / pill / badge co chữ theo vật chứa
            RefreshBoosterUi();
            int pend = Meta.PendingUnlock(); if (pend >= 0 && Meta.Level == _lv.index) ShowUnlock(pend);   // U5: popup mở khoá ở level vừa mở
        }

        // ---------------------------------------------------------------- UI
        void BuildBoosterBar(Transform ct)
        {
            _hudRoot = ct;
            // M8: "Lv.N" theo cấp mở khoá trong config, qua khoá bz_lv
            string[] lvTxt = new string[4]; for (int k = 0; k < 4; k++) lvTxt[k] = BuzzyStrings.Format("bz_lv", _cfg.boosterUnlock[k]);
            for (int i = 0; i < 4; i++)
            {
                var c = Bottom(new Vector2(_cfg.boosterX[i], _cfg.boosterY));   // U13: neo mép dưới, lệch con theo RefDy
                _boosterRing[i] = Img("Booster" + i, _a.boosterCircle, Color.white, ct, c, _cfg.boosterSize).GetComponent<Image>();
                if (IsUnlocked(i))
                {
                    Img("Icon" + i, _a.boosterIcons[i], Color.white, ct, new Vector2(c.x, c.y - RefDy(0.003f)), _cfg.boosterSize * 0.8f);
                    var badge = Img("Count" + i, _a.countBadge, Color.white, ct, new Vector2(c.x + _cfg.boosterSize * 0.34f, c.y + RefDy(0.025f)), _cfg.boosterSize * 0.5f);
                    _countText[i] = Label(BoosterCount[i].ToString(), badge, new Vector2(badge.sizeDelta.x * 0.5f, -badge.sizeDelta.y * 0.5f), (int)(46 * _canvasSize.x / 1080f), Color.white);
                    _countObj[i] = badge.gameObject;
                }
                else
                {
                    Img("Lock" + i, _a.lockIcon, Color.white, ct, new Vector2(c.x, c.y - RefDy(0.008f)), _cfg.boosterSize * 0.52f);
                    var pill = Img("Pill" + i, _a.levelBar, Color.white, ct, new Vector2(c.x, c.y + RefDy(0.034f)), _cfg.boosterSize * 0.55f, 0.5f);
                    _pillText[i] = Label(lvTxt[i], pill, new Vector2(pill.sizeDelta.x * 0.5f, -pill.sizeDelta.y * 0.5f), (int)(34 * _canvasSize.x / 1080f), new Color32(0x4A, 0x45, 0x9A, 255));
                }
            }
        }

        /// <summary>B9: tooltip đáy (icon + câu trên tấm kem) + màn tối cho VIP, hiện khi đang chọn mục tiêu; Magic Wand không có.</summary>
        void ShowBoosterTip(int i)
        {
            HideBoosterTip();
            if (_hudRoot == null) return;
            if (i == VipBus && _cfg.vipDimAlpha > 0f)
                _dim = Img("VipDim", null, new Color(0f, 0f, 0f, _cfg.vipDimAlpha), _hudRoot, new Vector2(0.5f, 0.5f), 1f, _canvasSize.y / _canvasSize.x);
            _tipPanel = Img("BoosterTip", _a.levelBar, new Color32(0xFF, 0xEF, 0xDA, 255), _hudRoot, Bottom(new Vector2(0.5f, _cfg.tooltipY)), _cfg.tooltipWidth, _cfg.tooltipAspect);   // U13: neo đáy
            float h = _tipPanel.sizeDelta.y;
            var icon = Img("TipIcon", _a.boosterIcons != null && i < _a.boosterIcons.Length ? _a.boosterIcons[i] : null, Color.white, _tipPanel, Vector2.zero, 0f);
            icon.sizeDelta = new Vector2(h * 0.8f, h * 0.8f); icon.anchoredPosition = new Vector2(h * 0.6f, -h * 0.5f);
            _hint = Label(BuzzyStrings.Get(BoosterHintKey[i]), _tipPanel, new Vector2(_tipPanel.sizeDelta.x * 0.5f + h * 0.45f, -h * 0.5f), (int)(_cfg.tooltipFontSize * _canvasSize.x / 1080f), new Color32(0x5A, 0x3E, 0xA6, 255));
            _hint.GetComponent<RectTransform>().sizeDelta = new Vector2(_tipPanel.sizeDelta.x - h * 1.3f, h * 0.32f);   // cao 1 dòng: câu dài phải thu chữ chứ không xuống dòng (r6 f50 dòng 2 tràn mép pill)
            _hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            _hint.resizeTextForBestFit = true; _hint.resizeTextMaxSize = _hint.fontSize; _hint.resizeTextMinSize = 12;   // B9: câu Sky Grab dài → tự thu chữ, không tràn (fd r5 f50)
        }

        void HideBoosterTip()
        {
            if (_tipPanel != null) { Kill(_tipPanel.gameObject); _tipPanel = null; _hint = null; }
            if (_dim != null) { Kill(_dim.gameObject); _dim = null; }
        }

        void BuildSpeedButton(Transform ct)
        {
            if (!SpeedUnlocked) return;
            // pill = hai nửa (trái, phải lật) + viền kem phía sau; cao theo speedPillSize
            float hw = _cfg.speedPillSize.x * 0.5f, asp = _cfg.speedPillSize.y * _cfg.hudRefAspect / hw;   // cao pill = % cũ × khung tham chiếu (theo bề ngang)
            var c0 = Top(_cfg.speedButtonCenter);   // U12: neo mép trên
            Img("SpeedRimL", _a.speedHalfRim, Color.white, ct, new Vector2(c0.x - hw * 0.5f, c0.y), hw * 1.08f, asp * 1.08f);
            var rimR = Img("SpeedRimR", _a.speedHalfRim, Color.white, ct, new Vector2(c0.x + hw * 0.5f, c0.y), hw * 1.08f, asp * 1.08f); rimR.localScale = new Vector3(-1f, 1f, 1f);
            var left = Img("SpeedL", _a.speedHalfGreen, Color.white, ct, new Vector2(c0.x - hw * 0.5f, c0.y), hw, asp);
            var right = Img("SpeedR", _a.speedHalfGreen, Color.white, ct, new Vector2(c0.x + hw * 0.5f, c0.y), hw, asp); right.localScale = new Vector3(-1f, 1f, 1f);
            _speedImg = left.GetComponent<Image>(); _speedImgR = right.GetComponent<Image>();
            _speedIconImg = Img("SpeedIcon", _a.speedIcon, Color.white, ct, new Vector2(c0.x + hw * 0.55f, c0.y), hw * 0.55f).GetComponent<Image>();
            _speedText = Label("x1", ct, new Vector2((c0.x - hw * 0.28f) * _canvasSize.x, -c0.y * _canvasSize.y), (int)(40 * _canvasSize.x / 1080f), Color.white);
            _speedText.GetComponent<RectTransform>().sizeDelta = new Vector2(120f, 60f);
        }

        void RefreshBoosterUi()
        {
            for (int i = 0; i < 4; i++)
            {
                if (_countText[i] != null) _countText[i].text = BoosterCount[i].ToString();
                if (_countObj[i] != null) _countObj[i].SetActive(BoosterCount[i] > 0);
                if (_boosterRing[i] != null) _boosterRing[i].transform.localScale = Vector3.one * (_mode == i ? 1.15f : 1f);
            }
            if (_mode >= 0) ShowBoosterTip(_mode); else HideBoosterTip();
        }

        /// <summary>C23: bật / tắt tốc độ x2 (nhân TimeScale).</summary>
        public void ToggleSpeed()
        {
            if (!SpeedUnlocked) return;
            _fast = !_fast; TimeScale = _fast ? _cfg.speedFast : 1f;
            if (_speedImg != null) { _speedImg.sprite = _fast ? _a.speedHalfPurple : _a.speedHalfGreen; _speedImgR.sprite = _speedImg.sprite; }
            if (_speedText != null) _speedText.text = _fast ? "x2" : "x1";
            if (_speedIconImg != null && _a.pauseIcon != null) _speedIconImg.sprite = _fast ? _a.pauseIcon : _a.speedIcon;   // mẫu: x2 hiện biểu tượng ‖
            Log?.Invoke("t=" + Time.ToString("0.00") + " Tốc độ " + (_fast ? "x2" : "x1"));
        }

        // ---------------------------------------------------------------- dùng booster
        bool CanUse(int i)
        {
            if (Won || Time < _inputOpenAt || !IsUnlocked(i) || BoosterCount[i] <= 0) { if (Vibrate != null) Vibrate(); return false; }
            return true;
        }

        void Spent(int i, string what)
        {
            BoosterCount[i]--; _mode = -1; RefreshBoosterUi(); if (Meta != null) { Meta.Boosters[i] = BoosterCount[i]; MetaChanged?.Invoke(); }
            Log?.Invoke("t=" + Time.ToString("0.00") + " Booster " + what + " (còn " + BoosterCount[i] + ")");
        }

        /// <summary>B1/B2 VIP Bus: xe đỗ ở ô <paramref name="slot"/> được đổ đầy: pixel màu xe bật lên, tụ trên nóc, rơi vào xe (2,0 s), xe rời như thường.</summary>
        public bool UseVipBus(int slot)
        {
            if (!CanUse(VipBus)) return false;
            var r = Game.VipBusFill(slot);
            if (!r.Matched) { if (Vibrate != null) Vibrate(); return false; }
            Spent(VipBus, "VIP Bus ô " + slot + " -> " + r.Pixels.Count + " pixel"); Sfx(_a.sfxVip);
            StartFill(r.Pixels, ViewOf(r.Car), false);
            return true;
        }

        /// <summary>B7/B8 Bus Claw: kẹp xe (cột, vị trí) khỏi cột chờ, mang xuống làn trên ô bến, đổ đầy ngay, xe rời. col &lt; 0 = xe đầu tiên tìm thấy (kịch bản tự chơi).</summary>
        public bool UseBusClaw(int col, int pos)
        {
            if (!CanUse(BusClaw)) return false;
            if (col < 0 || pos < 0) { col = -1; for (int c = 0; c < 3 && col < 0; c++) if (_queue[c].Count > 0) { col = c; pos = 0; } if (col < 0) { if (Vibrate != null) Vibrate(); return false; } }
            if (pos >= _queue[col].Count) { if (Vibrate != null) Vibrate(); return false; }
            var cv = _queue[col][pos];
            var r = Game.Claw(col, pos);
            if (!r.Ok || r.Car != cv.car) { if (Vibrate != null) Vibrate(); Log?.Invoke("CẢNH BÁO: Bus Claw không khớp hàng chờ"); return false; }
            Spent(BusClaw, "Bus Claw cột " + col + " vị trí " + pos + " (xe #" + r.Car.Id + ") -> " + r.Pixels.Count + " pixel"); Sfx(_a.sfxClawStart);
            _queue[col].RemoveAt(pos); cv.move = null; cv.col = -1;
            _laneCars.Add(cv);
            var fx = new ClawFx { cv = cv, res = r, col = col, t0 = Time, busFrom = cv.tr.localPosition };
            // kẹp + cáp tím
            var claw = new GameObject("Claw"); claw.transform.SetParent(_root, false);
            // đầu kẹp: khối vàng 1,4 đv (× clawMeshScale ≈ bề ngang xe) luôn có; móc Claw.obj (0,46 × 1,18 đv) treo dưới nếu có mesh (B8, 3c: mesh móc một mình chỉ là chấm)
            var head = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(head.GetComponent<Collider>()); head.name = "Head"; head.transform.SetParent(claw.transform, false);
            head.transform.localScale = new Vector3(1.4f, 0.8f, 1.4f); head.GetComponent<MeshRenderer>().sharedMaterial = Mat("Custom/BuzzyEnvironment", new Color32(0xFF, 0xC8, 0x20, 255));
            if (_a.clawMesh != null)
            {
                var hook = new GameObject("Hook"); hook.transform.SetParent(claw.transform, false); hook.transform.localPosition = new Vector3(0f, -0.3f, 0f); hook.transform.localScale = Vector3.one * 1.6f;
                hook.AddComponent<MeshFilter>().sharedMesh = _a.clawMesh; hook.AddComponent<MeshRenderer>().sharedMaterial = Mat("Custom/BuzzyEnvironment", new Color32(0xFF, 0xC8, 0x20, 255));
            }
            claw.transform.localScale = Vector3.one * _cfg.clawMeshScale;
            var cable = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(cable.GetComponent<Collider>()); cable.name = "Cable"; cable.transform.SetParent(_root, false);
            cable.GetComponent<MeshRenderer>().sharedMaterial = Mat("Custom/BuzzyEnvironment", new Color32(0x7A, 0x3F, 0xE0, 255));
            fx.claw = claw.transform; fx.cable = cable.transform;
            PlaceClaw(fx, fx.busFrom + Vector3.up * _cfg.clawCableHeight);
            _claws.Add(fx);
            return true;
        }

        /// <summary>B3/B4 Sky Grab: chạm điểm bất kỳ (toạ độ lưới, có thể lẻ): trực thăng tới, hút mọi pixel trong bán kính, xe cùng màu nhận, phần còn lại biến mất.</summary>
        public bool UseSkyGrabAt(double gx, double gy)
        {
            if (!CanUse(SkyGrab)) return false;
            var r = Game.SkyGrab(gx, gy);
            if (!r.Matched) { if (Vibrate != null) Vibrate(); CancelMode(); return false; }
            Spent(SkyGrab, "Sky Grab tại (" + gx.ToString("0.0") + "," + gy.ToString("0.0") + ") -> " + r.Count + " pixel (" + r.Vanished.Count + " biến mất)"); Sfx(_a.sfxHeli);
            var target = new Vector3(_cellX0 + (float)gx * CellX, 0f, _boardZ0 + (float)gy * CellZ);   // M7b bước 2: ô có thể thu (CellScale)
            var fx = new HeliFx { t0 = Time, target = target + Vector3.up * _cfg.skyHeight, enter = target + new Vector3(_cfg.skyEnterFrom.x, _cfg.skyHeight, _cfg.skyEnterFrom.y) };
            var go = new GameObject("Heli"); go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = _a.heliMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mat("Custom/BuzzyEnvironment", new Color32(0xFF, 0xC8, 0x20, 255));
            go.transform.localScale = Vector3.one * _cfg.skyScale;
            var wing = new GameObject("Wing"); wing.transform.SetParent(go.transform, false);
            wing.AddComponent<MeshFilter>().sharedMesh = _a.heliWingMesh;
            wing.AddComponent<MeshRenderer>().sharedMaterial = Mat("Custom/BuzzyEnvironment", new Color32(0xDD, 0xDD, 0xEE, 255));
            wing.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            go.transform.localPosition = fx.enter; go.SetActive(false);
            fx.tr = go.transform; fx.wing = wing.transform;
            if (_cfg.skyShadowSize.x > 0f)
            {
                // bóng mờ trên mặt bảng ngay dưới trực thăng (con của heli, bù lại skyHeight và skyScale) để thấy nó lơ lửng chứ không nằm trên bảng
                fx.shadow = MakeShadow(go.transform);
                if (fx.shadow != null) { float k = 1f / Mathf.Max(0.01f, _cfg.skyScale); fx.shadow.localPosition = new Vector3(0f, (-_cfg.skyHeight + 0.03f) * k, 0f); fx.shadow.localRotation = Quaternion.Euler(90f, 0f, 0f); fx.shadow.localScale = new Vector3(_cfg.skyShadowSize.x * k, _cfg.skyShadowSize.y * k, 1f); }
            }
            // pixel bị hút: về xe (số giảm lúc tới bụng trực thăng) hoặc biến mất
            foreach (var tr in r.ToCars) { var cv = ViewOf(tr.Car); foreach (var d in tr.Pixels) AddSuck(fx, d, cv); }
            foreach (var d in r.Vanished) AddSuck(fx, d, null);
            _heli.Add(fx);
            return true;
        }

        /// <summary>Chữ ký cũ (kịch bản tự chơi / MotionCapture): color ≥ 0 → tâm cụm lớn nhất của màu đó; -1 → pixel lấy được của xe đỗ còn chỗ gần tâm ảnh nhất.</summary>
        public bool UseSkyGrab(int color)
        {
            double gx = _lv.width * 0.5, gy = _lv.height * 0.5; bool found = false;
            var b = Game.Board; double bestD = double.MaxValue;
            for (int x = 0; x < b.W; x++) for (int y = 0; y < b.H; y++)
            {
                int c = b.ColorAt(x, y); if (c == Board.Empty || !b.Takeable(x, y)) continue;
                if (color >= 0 ? c != color : !HasParkedCar(c)) continue;
                double dx = x - b.W * 0.5, dy = y - b.H * 0.5, d2 = dx * dx + dy * dy;
                if (d2 < bestD) { bestD = d2; gx = x; gy = y; found = true; }
            }
            if (!found && color >= 0) { if (Vibrate != null) Vibrate(); return false; }
            return UseSkyGrabAt(gx, gy);
        }

        bool HasParkedCar(int color) { foreach (var c in Game.Slots) if (c != null && !c.Locked && c.Color == color && c.Free > 0) return true; return false; }

        void AddSuck(HeliFx fx, PixelData d, CarV cv)
        {
            Pass p; if (!_pass.TryGetValue(d.X * 1000 + d.Y, out p)) return;
            p.flying = true;
            if (p.hidden) { p.hidden = false; p.mr.sharedMaterial = PassMat(d.Color); }
            fx.suck.Add(new SuckFx { p = p, car = cv, from = p.tr.localPosition });
        }

        /// <summary>B5/B6 Magic Wand: "Shuffle the buses!": dùng ngay khi bấm nút, không cần chạm ảnh (tham số color giữ cho chữ ký cũ, bỏ qua).</summary>
        public bool UseMagicWand(int color)
        {
            if (!CanUse(MagicWand)) return false;
            if (_wand != null) { if (Vibrate != null) Vibrate(); return false; }
            var r = Game.Shuffle();
            if (!r.Ok) { if (Vibrate != null) Vibrate(); Log?.Invoke("t=" + Time.ToString("0.00") + " Magic Wand: không có xe để xáo"); return false; }
            string s = ""; for (int i = 0; i < 3; i++) if (r.NewSlots[i] != null) s += " ô" + i + ": xe#" + r.OldSlots[i].Id + "→xe#" + r.NewSlots[i].Id + " màu " + r.NewSlots[i].Color;
            Spent(MagicWand, "Magic Wand xáo xe:" + s); Sfx(_a.sfxShuffle);
            var fx = new WandFx { t0 = Time, cols = new List<CarV>[3], grey = new bool[3], shown = new bool[3], numbers = new bool[3] };
            for (int c = 0; c < 3; c++) { fx.cols[c] = new List<CarV>(); if (_slotCar[c] != null) fx.cols[c].Add(_slotCar[c]); fx.cols[c].AddRange(_queue[c]); }
            _wand = fx;
            return true;
        }

        // ---------------------------------------------------------------- đổ đầy (B2): bật lên → tụ trên nóc → rơi vào xe
        void StartFill(List<PixelData> pixels, CarV cv, bool lane)
        {
            if (cv == null) return;
            int n = pixels.Count;
            for (int i = 0; i < n; i++)
            {
                Pass p; if (!_pass.TryGetValue(pixels[i].X * 1000 + pixels[i].Y, out p)) continue;
                p.flying = true;
                if (p.hidden) { p.hidden = false; p.mr.sharedMaterial = PassMat(pixels[i].Color); }
                float a = (float)_rnd.NextDouble() * Mathf.PI * 2f, rr = _cfg.fillGatherRadius * Mathf.Sqrt((float)_rnd.NextDouble());
                _fills.Add(new FillFx { p = p, car = cv, lane = lane, start = Time + (n <= 1 ? 0f : _cfg.fillSpread * i / (n - 1)), from = p.tr.localPosition, off = new Vector3(Mathf.Cos(a) * rr, _cfg.fillGatherHeight + (float)_rnd.NextDouble() * 0.6f, Mathf.Sin(a) * rr * 0.6f) });
            }
        }

        void TickFills()
        {
            for (int i = _fills.Count - 1; i >= 0; i--)
            {
                var f = _fills[i]; float t = Time - f.start; if (t < 0f || f.p.tr == null) { if (f.p.tr == null) _fills.RemoveAt(i); continue; }
                var carPos = f.car != null && f.car.tr != null ? f.car.tr.localPosition : f.from;
                Vector3 pos; float sc = 1f;
                if (t < _cfg.fillPopDuration) { float u = t / _cfg.fillPopDuration; u = 1f - (1f - u) * (1f - u); pos = f.from + Vector3.up * (_cfg.fillPopHeight * u); }
                else if (t < _cfg.fillPopDuration + _cfg.fillGatherDuration)
                {
                    float u = (t - _cfg.fillPopDuration) / _cfg.fillGatherDuration; u = u * u * (3f - 2f * u);
                    var a = f.from + Vector3.up * _cfg.fillPopHeight; var b = carPos + f.off;
                    pos = Vector3.Lerp(a, b, u) + Vector3.up * (1.2f * 4f * u * (1f - u));
                    f.p.tr.localRotation = Quaternion.Euler(0f, 360f * u, 0f);
                }
                else
                {
                    float u = (t - _cfg.fillPopDuration - _cfg.fillGatherDuration) / Mathf.Max(0.01f, _cfg.fillDropDuration);
                    if (u >= 1f)
                    {
                        _fills.RemoveAt(i); Kill(f.p.tr.gameObject); ForgetPass(f.p);
                        if (f.lane) Game.ArriveLane(f.car.car, f.p.d); else Game.Arrive(f.car.car, f.p.d);
                        continue;
                    }
                    pos = Vector3.Lerp(carPos + f.off, carPos + Vector3.up * 1.3f, u * u); sc = Mathf.Lerp(1f, 0.3f, u);
                }
                f.p.tr.localPosition = pos; f.p.tr.localScale = BlockScale * sc;
            }
        }

        // ---------------------------------------------------------------- Sky Grab (B4)
        void TickHeli(float dt)
        {
            for (int i = _heli.Count - 1; i >= 0; i--)
            {
                var h = _heli[i]; float t = Time - h.t0 - _cfg.skyEnterDelay;
                if (t < 0f) continue;
                if (!h.tr.gameObject.activeSelf) h.tr.gameObject.SetActive(true);
                h.wing.localEulerAngles += new Vector3(0f, _cfg.skyRotorSpeed * dt, 0f);
                float tEnter = _cfg.skyEnterDuration, tHover = tEnter + _cfg.skyHover, tExit1 = tHover + _cfg.skyExitDuration, tExit2 = tExit1 + _cfg.skyExitDuration;
                var exit1 = new Vector3(h.target.x, _cfg.skyHeight, _cfg.slotZ + 2f);
                var exit2 = new Vector3(h.target.x, _cfg.skyHeight + 2f, _cfg.queueZ + 16f);
                Vector3 pos;
                if (t < tEnter) { float u = t / tEnter; u = u * u * (3f - 2f * u); pos = Vector3.Lerp(h.enter, h.target, u); }
                else if (t < tHover)
                {
                    pos = h.target + Vector3.up * (0.15f * Mathf.Sin((t - tEnter) * 6f));
                    if (!h.sucked)
                    {
                        h.sucked = true; int n = h.suck.Count;
                        for (int k = 0; k < n; k++) h.suck[k].start = Time + (n <= 1 ? 0f : _cfg.skySuckSpread * k / (n - 1));
                    }
                }
                else if (t < tExit1) { float u = (t - tHover) / _cfg.skyExitDuration; u = u * u; pos = Vector3.Lerp(h.target, exit1, u); }
                else if (t < tExit2) { float u = (t - tExit1) / _cfg.skyExitDuration; pos = Vector3.Lerp(exit1, exit2, u); }
                else { Kill(h.tr.gameObject); FinishSuck(h); _heli.RemoveAt(i); continue; }
                var dir = pos - h.tr.localPosition; dir.y = 0f;
                if (dir.sqrMagnitude > 1e-6f) h.tr.localRotation = Quaternion.Slerp(h.tr.localRotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-8f * dt));
                h.tr.localPosition = pos;
                // pixel bay lên bụng trực thăng; tới nơi: biến mất, xe cùng màu nhận (số giảm trong lúc lơ lửng)
                for (int k = h.suck.Count - 1; k >= 0; k--)
                {
                    var s = h.suck[k]; if (!h.sucked || Time < s.start || s.p.tr == null) { if (s.p.tr == null) h.suck.RemoveAt(k); continue; }
                    float u = (Time - s.start) / Mathf.Max(0.01f, _cfg.skySuckDuration);
                    if (u >= 1f) { h.suck.RemoveAt(k); Kill(s.p.tr.gameObject); ForgetPass(s.p); if (s.car != null) Game.Arrive(s.car.car, s.p.d); continue; }
                    float e = u * u;
                    s.p.tr.localPosition = Vector3.Lerp(s.from, pos - Vector3.up * 0.6f, e) + Vector3.up * (0.3f * Mathf.Sin(u * Mathf.PI));
                    s.p.tr.localScale = BlockScale * Mathf.Lerp(1f, 0.25f, e);
                    s.p.tr.localRotation = Quaternion.Euler(0f, 540f * u, 0f);
                }
            }
        }

        void FinishSuck(HeliFx h)
        {
            foreach (var s in h.suck) { if (s.p.tr != null) Kill(s.p.tr.gameObject); ForgetPass(s.p); if (s.car != null) Game.Arrive(s.car.car, s.p.d); }
            h.suck.Clear();
        }

        // ---------------------------------------------------------------- Magic Wand (B6)
        void TickWand()
        {
            var w = _wand; if (w == null) return;
            bool all = true;
            for (int c = 0; c < 3; c++)
            {
                float t = Time - w.t0 - c * _cfg.wandStagger;
                if (t < 0f) { all = false; continue; }
                if (t < _cfg.wandSpin)
                {
                    all = false;
                    if (w.cols[c].Count > 0 && _slotCar[c] == w.cols[c][0] && !w.hidden.Contains(w.cols[c][0])) w.cols[c][0].tr.localRotation = SlotRot * Quaternion.Euler(0f, 360f * (t / _cfg.wandSpin), 0f);
                    continue;
                }
                if (!w.grey[c])
                {
                    w.grey[c] = true;
                    foreach (var cv in w.cols[c]) if (!w.hidden.Contains(cv)) { HideCar(w, cv); w.greys.Add(MakeGreyBox(cv, c)); }
                }
                if (t < _cfg.wandSpin + _cfg.wandGrey) { all = false; continue; }
                if (!w.shown[c])
                {
                    w.shown[c] = true;
                    // xếp xe theo trạng thái Core cho cột c: ô đỗ + cột chờ
                    for (int k = w.greys.Count - 1; k >= 0; k--) if (w.greys[k] != null && w.greys[k].name == "Grey" + c) { Kill(w.greys[k]); w.greys.RemoveAt(k); }
                    _queue[c].Clear();
                    var parked = Game.Slots[c];
                    if (parked != null) { var cv = ViewOf(parked); if (cv != null) { if (!w.hidden.Contains(cv)) HideCar(w, cv); PlaceCar(cv, c, -1); ShowCar(w, cv, false); _slotCar[c] = cv; } }
                    else _slotCar[c] = null;
                    int q = 0; foreach (var car in Game.Queues[c]) { var cv = ViewOf(car); if (cv != null) { if (!w.hidden.Contains(cv)) HideCar(w, cv); PlaceCar(cv, c, q); ShowCar(w, cv, false); _queue[c].Add(cv); } q++; }
                }
                if (t < _cfg.wandSpin + _cfg.wandGrey + _cfg.wandNumberDelay) { all = false; continue; }
                if (!w.numbers[c])
                {
                    w.numbers[c] = true;
                    if (_slotCar[c] != null) ShowCar(w, _slotCar[c], true);
                    foreach (var cv in _queue[c]) ShowCar(w, cv, true);
                }
            }
            if (all) { foreach (var g in w.greys) if (g != null) Kill(g); foreach (var cv in new List<CarV>(w.hidden)) ShowCar(w, cv, true); _wand = null; }
        }

        void PlaceCar(CarV cv, int col, int queuePos)
        {
            cv.move = null; cv.col = col;
            if (queuePos < 0) { cv.slotPos = SlotPos(col); cv.tr.localPosition = cv.slotPos; cv.tr.localRotation = SlotRot; cv.baseScale = _cfg.busScale; }
            else { cv.tr.localPosition = QueuePos(col, queuePos); cv.tr.localRotation = Quaternion.identity; cv.baseScale = _cfg.queueBusScale * _cfg.busScale; }
            cv.tr.localScale = Vector3.one * cv.baseScale;
            foreach (var tx in cv.texts) tx.text = Mathf.Max(0, cv.car.Remaining).ToString();
        }

        void HideCar(WandFx w, CarV cv)
        {
            w.hidden.Add(cv);
            foreach (var r in cv.tr.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }

        void ShowCar(WandFx w, CarV cv, bool withNumbers)
        {
            foreach (var r in cv.tr.GetComponentsInChildren<Renderer>(true))
            {
                bool isText = r.GetComponent<TextMesh>() != null;
                if (isText && !withNumbers) continue;
                r.enabled = true;
            }
            if (withNumbers) w.hidden.Remove(cv);
        }

        /// <summary>Khối xám có dấu "?" đúng chỗ xe (B6).</summary>
        GameObject MakeGreyBox(CarV cv, int col)
        {
            var go = new GameObject("Grey" + col); go.transform.SetParent(_root, false);
            go.transform.localPosition = cv.tr.localPosition; go.transform.localRotation = cv.tr.localRotation;
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(cube.GetComponent<Collider>()); cube.transform.SetParent(go.transform, false);
            float s = cv.baseScale > 0f ? cv.baseScale : _cfg.busScale;
            cube.transform.localPosition = new Vector3(0f, 0.75f * s, 0f); cube.transform.localScale = new Vector3(1.6f, 1.5f, 2.6f) * s;
            cube.GetComponent<MeshRenderer>().sharedMaterial = Mat("Custom/BuzzyEnvironment", _cfg.wandGreyColor);
            var tgo = new GameObject("Q"); tgo.transform.SetParent(go.transform, false);
            tgo.transform.localPosition = new Vector3(0f, 1.56f * s, 0f); tgo.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            var tm = tgo.AddComponent<TextMesh>(); tm.text = "?"; tm.font = _a.font; tm.fontSize = 64; tm.characterSize = _cfg.roofNumberSize * 0.24f * s; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = new Color(0.25f, 0.25f, 0.3f, 1f);   // B6: "?" to rõ (fd f88–f90 nhỏ quá)
            var m = new Material(_a.font.material); m.renderQueue = 3010; tgo.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        // ---------------------------------------------------------------- Bus Claw (B8)
        void PlaceClaw(ClawFx fx, Vector3 clawPos)
        {
            fx.claw.localPosition = clawPos;
            float top = clawPos.y + _cfg.clawCableHeight; float len = Mathf.Max(0.05f, top - clawPos.y);
            fx.cable.localPosition = new Vector3(clawPos.x, clawPos.y + len * 0.5f + 0.4f, clawPos.z);
            fx.cable.localScale = new Vector3(0.12f, len, 0.12f);
        }

        void TickClaws(float dt)
        {
            for (int i = _claws.Count - 1; i >= 0; i--)
            {
                var f = _claws[i]; float t = Time - f.t0;
                var cv = f.cv; float busTop = 1.7f * cv.tr.localScale.y;
                float tDesc = _cfg.clawDescend, tGrab = tDesc + _cfg.clawGrab, tLift = tGrab + _cfg.clawLiftDuration, tCarry = tLift + _cfg.clawCarry, tRel = tCarry + _cfg.clawRelease;
                var lane = new Vector3(_cfg.clawLanePos.x, 0f, _cfg.clawLanePos.y);
                if (t < tDesc)
                {
                    float u = t / tDesc; u = u * u * (3f - 2f * u);
                    PlaceClaw(f, f.busFrom + Vector3.up * Mathf.Lerp(_cfg.clawCableHeight, busTop, u));
                }
                else if (t < tGrab)
                {
                    float u = (t - tDesc) / _cfg.clawGrab;
                    PlaceClaw(f, f.busFrom + Vector3.up * busTop + Vector3.right * (0.06f * Mathf.Sin(u * 40f)));
                    if (u > 0.6f && !f.shifted)
                    {
                        f.shifted = true;
                        // xe sau trong cột dồn lên 1 bậc; camera về
                        for (int k = 0; k < _queue[f.col].Count; k++) StartMove(_queue[f.col][k], new[] { _queue[f.col][k].tr.localPosition, QueuePos(f.col, k) }, _cfg.queueToSlotDuration, Quaternion.identity, true, _cfg.clawLiftDuration);
                        CamTo(0f, 1f, _cfg.clawCamBack);
                    }
                }
                else if (t < tLift)
                {
                    float u = (t - tGrab) / _cfg.clawLiftDuration; u = 1f - (1f - u) * (1f - u);
                    var p = f.busFrom + Vector3.up * (_cfg.clawLiftHeight * u);
                    cv.tr.localPosition = p; cv.tr.localScale = Vector3.one * Mathf.Lerp(_cfg.queueBusScale * _cfg.busScale, _cfg.busScale, u); cv.baseScale = cv.tr.localScale.x;
                    PlaceClaw(f, p + Vector3.up * busTop);
                }
                else if (t < tCarry)
                {
                    float u = (t - tLift) / _cfg.clawCarry;
                    var a = f.busFrom + Vector3.up * _cfg.clawLiftHeight; var b = lane + Vector3.up * _cfg.clawLiftHeight;
                    float h = Mathf.Clamp01(u / 0.65f); h = h * h * (3f - 2f * h);
                    var p = Vector3.Lerp(a, b, h);
                    float dsc = Mathf.Clamp01((u - 0.55f) / 0.45f); dsc = dsc * dsc * (3f - 2f * dsc);
                    p.y = Mathf.Lerp(_cfg.clawLiftHeight, 0f, dsc);
                    cv.tr.localPosition = p; cv.tr.localRotation = Quaternion.Slerp(cv.tr.localRotation, Quaternion.identity, 1f - Mathf.Exp(-6f * dt));
                    PlaceClaw(f, p + Vector3.up * busTop);
                }
                else if (t < tRel)
                {
                    float u = (t - tCarry) / _cfg.clawRelease; u = u * u;
                    cv.tr.localPosition = lane; cv.slotPos = lane;
                    PlaceClaw(f, lane + Vector3.up * Mathf.Lerp(busTop, _cfg.clawCableHeight, u));
                }
                else
                {
                    if (f.claw != null) { Kill(f.claw.gameObject); Kill(f.cable.gameObject); f.claw = null; f.cable = null; }
                    float ts = t - tRel;
                    if (f.swirl == null && !f.filled) f.swirl = MakeSwirl(cv);   // xoáy tím trên nóc trước (B8, 3c: 0,3–0,7 s rồi pixel mới bay)
                    if (!f.filled && ts >= _cfg.swirlDuration)
                    {
                        f.filled = true;   // đổ đầy sau xoáy (B7); không còn pixel → Core đã cho xe rời lúc kẹp
                        if (f.res.Pixels.Count > 0) StartFill(f.res.Pixels, cv, true);
                    }
                    if (f.swirl != null) { f.swirl.localEulerAngles += new Vector3(0f, 720f * dt, 0f); f.swirl.localScale = Vector3.one * (0.9f + 0.25f * Mathf.Sin(ts * 9f)); f.swirl.localPosition = cv.tr.localPosition + Vector3.up * (busTop + 0.1f); }
                    bool stillFilling = false; foreach (var ff in _fills) if (ff.car == cv) { stillFilling = true; break; }
                    if (ts > _cfg.swirlDuration + 0.2f && f.swirl != null) { Kill(f.swirl.gameObject); f.swirl = null; }   // xoáy tắt ngay sau khi pixel bắt đầu bay
                    if (f.filled && f.swirl == null && !stillFilling) _claws.RemoveAt(i);
                }
            }
        }

        Transform MakeSwirl(CarV cv)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(go.GetComponent<Collider>()); go.name = "Swirl"; go.transform.SetParent(_root, false);
            go.transform.localScale = new Vector3(1.4f, 0.1f, 1.4f);   // đĩa tím cỡ nóc xe; 3 chấm sáng trên vành để thấy quay (r5 f144–f148 đĩa mỏng 0,9 không nhận ra)
            var m = Mat("Custom/BuzzyEnvironment", new Color32(0x9A, 0x4F, 0xF0, 255)); go.GetComponent<MeshRenderer>().sharedMaterial = m;
            var lm = Mat("Custom/BuzzyEnvironment", new Color32(0xE0, 0xC8, 0xFF, 255));
            for (int k = 0; k < 3; k++) { var d = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(d.GetComponent<Collider>()); d.transform.SetParent(go.transform, false); float a = k * 2.094f; d.transform.localPosition = new Vector3(0.42f * Mathf.Cos(a), 1.5f, 0.42f * Mathf.Sin(a)); d.transform.localScale = new Vector3(0.22f, 3f, 0.22f); d.GetComponent<MeshRenderer>().sharedMaterial = lm; }
            go.transform.localPosition = cv.tr.localPosition + Vector3.up * 1.8f;
            return go.transform;
        }

        /// <summary>Xe ở làn (Claw) đủ người: phóng sang phải ra khỏi màn (clip 1034,0–1034,5 s).</summary>
        void OnLaneCarDeparted(Car car)
        {
            var cv = ViewOf(car); if (cv == null) return;
            _laneCars.Remove(cv); cv.leaving = true;
            FeelDepart(cv);
            Action gone = () => { _cars.Remove(cv); Kill(cv.tr.gameObject); };
            var from = cv.tr.localPosition; var exit = new Vector3(_cfg.leaveEdgeX + 6f, 0f, from.z);
            // B10: làn trên chỉ 1 xe một lúc (hàng đợi LaneDepart trong BuzzyGameView.cs); xe đứng chờ tại chỗ tới khi làn trống
            LaneDepart(cv, () => { StartMove(cv, new[] { from, exit }, Vector3.Distance(from, exit) / Mathf.Max(1f, _cfg.leaveExitSpeed), Quaternion.identity, false, 0.25f, gone); cv.move.ease = 2; });
        }

        // ---------------------------------------------------------------- camera (B8)
        void CamTo(float shift, float zoom, float dur)
        {
            if (_cam == null) return;
            if (!_camBaseSet) { _camBase = _cam.transform.position; _camBaseSet = true; }
            _camShiftFrom = _camShift; _camZoomFrom = _camZoom; _camShiftTo = shift; _camZoomTo = zoom; _camT0 = Time; _camDur = Mathf.Max(0.01f, dur);
        }

        void TickCam()
        {
            if (_camT0 < 0f || _cam == null) return;
            float u = Mathf.Clamp01((Time - _camT0) / _camDur); float e = u * u * (3f - 2f * u);
            _camShift = Mathf.Lerp(_camShiftFrom, _camShiftTo, e); _camZoom = Mathf.Lerp(_camZoomFrom, _camZoomTo, e);
            _cam.transform.position = _camBase + Vector3.forward * _camShift;
            if (_cam.orthographic && _baseOrtho > 0f) _cam.orthographicSize = _baseOrtho * _camZoom;
            if (u >= 1f) { _camT0 = -1f; if (_camShiftTo == 0f && _camZoomTo == 1f && _cam.orthographic && _baseOrtho > 0f) _cam.orthographicSize = _baseOrtho; }
        }

        /// <summary>B8: trượt camera lên (và phóng nhỏ nếu cột dài) để thấy toàn bộ 3 cột chờ; đáy = ô bến.</summary>
        void CamShowQueues()
        {
            if (_cam == null || !_cam.orthographic) return;
            int maxLen = 0; for (int c = 0; c < 3; c++) maxLen = Mathf.Max(maxLen, _queue[c].Count);
            if (maxLen == 0) return;
            float needTop = QueuePos(0, maxLen - 1).z + _cfg.clawCamMargin, needBottom = _cfg.slotZ - _cfg.clawCamMargin;
            var plane = new Plane(Vector3.up, Vector3.zero); float d;
            var rt = _cam.ViewportPointToRay(new Vector3(0.5f, 1f, 0f)); var rb = _cam.ViewportPointToRay(new Vector3(0.5f, 0.12f, 0f));
            if (!plane.Raycast(rt, out d)) return; float visTop = rt.GetPoint(d).z;
            if (!plane.Raycast(rb, out d)) return; float visBottom = rb.GetPoint(d).z;
            float vis = visTop - visBottom, need = needTop - needBottom;
            float zoom = Mathf.Max(1f, need / Mathf.Max(1f, vis));
            float center = (visTop + visBottom) * 0.5f, newTop = center + vis * 0.5f * zoom;
            float shift = Mathf.Max(0f, needTop - newTop);
            CamTo(shift, zoom, _cfg.clawCamUp);
        }

        void TickBoosters(float dt)
        {
            TickCam();
            TickFills();
            TickHeli(dt);
            TickWand();
            TickClaws(dt);
        }

        // ---------------------------------------------------------------- nhập (chuột / chạm)
        void HandleInput()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!UnityEngine.Input.GetMouseButtonDown(0) || _cam == null) return;
            var mp = UnityEngine.Input.mousePosition;
            float nx = mp.x / Screen.width, ny = 1f - mp.y / Screen.height;
            if (UiTap(nx, ny)) return;
            WorldTap(_cam.ScreenPointToRay(mp));
#endif
        }

        bool Hit(Vector2 centerPct, float sizePct, float nx, float ny)
        {
            float rx = sizePct * 0.5f, ry = sizePct * 0.5f * _canvasSize.x / _canvasSize.y;
            return Mathf.Abs(nx - centerPct.x) <= rx && Mathf.Abs(ny - centerPct.y) <= ry;
        }

        bool UiTap(float nx, float ny)
        {
            if (Popup != PopupKind.None) return PopupTap(nx, ny);
            if (DevTap(nx, ny)) return true;   // T1 (M7)
            if (Time < _inputOpenAt) return true;
            if (Hit(Bottom(_cfg.settingsCenter), _cfg.settingsSize, nx, ny)) { OpenSettings(); return true; }
            if (SpeedUnlocked && Hit(Top(_cfg.speedButtonCenter), _cfg.speedButtonSize, nx, ny)) { ToggleSpeed(); return true; }
            for (int i = 0; i < 4; i++)
            {
                if (!Hit(Bottom(new Vector2(_cfg.boosterX[i], _cfg.boosterY)), _cfg.boosterSize, nx, ny)) continue;
                if (!IsUnlocked(i)) { if (Vibrate != null) Vibrate(); Log?.Invoke("Booster " + i + " chưa mở"); return true; }
                if (BoosterCount[i] <= 0) { ShowBuyBooster(i); return true; }
                if (i == MagicWand) { CancelMode(); UseMagicWand(-1); return true; }   // B5: dùng ngay, không chọn mục tiêu
                if (_mode == i) { CancelMode(); return true; }
                _mode = i; RefreshBoosterUi();
                if (i == BusClaw) CamShowQueues();   // B8: camera trượt lên cho thấy cả cột chờ
                return true;
            }
            return false;
        }

        void WorldTap(Ray ray)
        {
            var hits = Physics.RaycastAll(ray, 500f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            CarV car = null; Pass pass = null;
            foreach (var h in hits)
            {
                foreach (var c in _cars) if (c.tr == h.transform && !c.leaving) { car = c; break; }
                if (car != null) break;
                foreach (var kv in _pass) if (kv.Value.tr == h.transform) { pass = kv.Value; break; }
                if (pass != null) break;
            }
            if (_mode == VipBus) { int s = car == null ? -1 : System.Array.IndexOf(_slotCar, car); if (s >= 0) UseVipBus(s); else CancelMode(); return; }
            if (_mode == BusClaw)
            {
                if (car != null) for (int c = 0; c < 3; c++) { int k = _queue[c].IndexOf(car); if (k >= 0) { UseBusClaw(c, k); return; } }
                CancelMode(); return;
            }
            if (_mode == SkyGrab)
            {
                // B3: chạm điểm bất kỳ trên mặt ảnh (kể cả ô rỗng): giao tia với mặt đất → toạ độ lưới lẻ
                var plane = new Plane(Vector3.up, _root.position); float d;
                if (plane.Raycast(ray, out d))
                {
                    var w = _root.InverseTransformPoint(ray.GetPoint(d));
                    double gx = (w.x - _cellX0) / CellX, gy = (w.z - _boardZ0) / CellZ;   // M7b bước 2
                    if (gx >= -1 && gy >= -1 && gx <= _lv.width + 1 && gy <= _lv.height + 1) { UseSkyGrabAt(gx, gy); return; }
                }
                CancelMode(); return;
            }
            if (_mode == MagicWand) { CancelMode(); return; }
            if (pass != null) TapPixel(pass);
        }

        /// <summary>Kịch bản chụp (B8/B9): bấm nút booster i như người chơi (tooltip đáy, màn tối VIP, camera trượt Claw). Trả về false nếu nút chưa mở / hết số.</summary>
        public bool DebugBoosterMode(int i)
        {
            if (i < 0 || i > 3) return false;
            var p = Bottom(new Vector2(_cfg.boosterX[i], _cfg.boosterY)); UiTap(p.x, p.y);
            return _mode == i || i == MagicWand;
        }

        /// <summary>Kịch bản chụp: chạm đích cho booster đang chọn (sau DebugBoosterMode). VIP: p1 = ô; Claw: p1 = cột, p2 = vị trí (âm = xe đầu cột 0); Sky: (p1, p2) = toạ độ lưới.</summary>
        public bool DebugBoosterPick(int p1, int p2)
        {
            if (_mode == VipBus) return UseVipBus(p1);
            if (_mode == BusClaw) return UseBusClaw(p1 < 0 ? 0 : p1, p2 < 0 ? 0 : p2);
            if (_mode == SkyGrab) return UseSkyGrabAt(p1, p2);
            return false;
        }

        void CancelMode()
        {
            bool wasClaw = _mode == BusClaw;
            _mode = -1; RefreshBoosterUi();
            if (wasClaw) CamTo(0f, 1f, _cfg.clawCamBack);
        }

        /// <summary>Chạm một pixel: băng / ẩn không chạm được (rung), còn lại chạm cả cụm màu.</summary>
        void TapPixel(Pass p)
        {
            if (!Game.Board.Takeable(p.d.X, p.d.Y)) { if (Vibrate != null) Vibrate(); Log?.Invoke("Chạm pixel bị băng / ẩn (" + p.d.X + "," + p.d.Y + ") -> rung"); return; }
            TapColor(p.d.Color, p.d.X, p.d.Y);
        }
    }
}
