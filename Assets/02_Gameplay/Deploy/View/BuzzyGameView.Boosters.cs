using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Buzzy.Core;

namespace Buzzy.Game
{
    /// <summary>M3: 4 booster (VIP Bus, Sky Grab, Magic Wand, Bus Claw) + nút tốc độ x1/x2. Luật: design-decisions.md mục 4-7.</summary>
    public partial class BuzzyGameView
    {
        public const int VipBus = 0, SkyGrab = 1, MagicWand = 2, BusClaw = 3;
        static readonly string[] BoosterHint = { "Chọn xe đang đỗ để xóa", "Chọn màu pixel để nhấc", "Chọn pixel để đổi màu xe", "Chọn xe ở hàng chờ để kẹp" };

        /// <summary>Số booster còn lại (kho xu / quảng cáo làm ở M5).</summary>
        public readonly int[] BoosterCount = new int[4];
        int _mode = -1;
        readonly Image[] _boosterRing = new Image[4];
        readonly Text[] _countText = new Text[4];
        readonly GameObject[] _countObj = new GameObject[4];
        Text _hint; Image _speedImg, _speedImgR, _speedIconImg; Text _speedText; bool _fast;

        class HeliFx { public Transform tr, wing; public Vector3[] pts; public float start, dur; }
        readonly List<HeliFx> _heli = new List<HeliFx>();

        bool IsUnlocked(int i) { return _lv.index >= _cfg.boosterUnlock[i]; }
        public bool SpeedUnlocked { get { return _lv.index >= _cfg.speedUnlockLevel; } }
        public bool Fast { get { return _fast; } }

        void InitBoosters()
        {
            EnsureMeta();
            for (int i = 0; i < 4; i++) BoosterCount[i] = IsUnlocked(i) ? Meta.Boosters[i] : 0;
            RefreshBoosterUi();
            int pend = Meta.PendingUnlock(); if (pend >= 0 && Meta.Level == _lv.index) ShowUnlock(pend);   // U5: popup mở khoá ở level vừa mở
        }

        // ---------------------------------------------------------------- UI
        void BuildBoosterBar(Transform ct)
        {
            string[] lvTxt = { "Lv.4", "Lv.8", "Lv.13", "Lv.16" };
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector2(_cfg.boosterX[i], _cfg.boosterY);
                _boosterRing[i] = Img("Booster" + i, _a.boosterCircle, Color.white, ct, c, _cfg.boosterSize).GetComponent<Image>();
                if (IsUnlocked(i))
                {
                    Img("Icon" + i, _a.boosterIcons[i], Color.white, ct, new Vector2(c.x, c.y - 0.003f), _cfg.boosterSize * 0.8f);
                    var badge = Img("Count" + i, _a.countBadge, Color.white, ct, new Vector2(c.x + _cfg.boosterSize * 0.34f, c.y + 0.025f), _cfg.boosterSize * 0.5f);
                    _countText[i] = Label(BoosterCount[i].ToString(), badge, new Vector2(badge.sizeDelta.x * 0.5f, -badge.sizeDelta.y * 0.5f), (int)(46 * _canvasSize.x / 1080f), Color.white);
                    _countObj[i] = badge.gameObject;
                }
                else
                {
                    Img("Lock" + i, _a.lockIcon, Color.white, ct, new Vector2(c.x, c.y - 0.008f), _cfg.boosterSize * 0.52f);
                    var pill = Img("Pill" + i, _a.levelBar, Color.white, ct, new Vector2(c.x, c.y + 0.034f), _cfg.boosterSize * 0.55f, 0.5f);
                    Label(lvTxt[i], pill, new Vector2(pill.sizeDelta.x * 0.5f, -pill.sizeDelta.y * 0.5f), (int)(34 * _canvasSize.x / 1080f), new Color32(0x4A, 0x45, 0x9A, 255));
                }
            }
            _hint = Label("", ct, new Vector2(_canvasSize.x * 0.5f, -_canvasSize.y * 0.66f), (int)(44 * _canvasSize.x / 1080f), Color.white);
            _hint.GetComponent<RectTransform>().sizeDelta = new Vector2(_canvasSize.x, 120f);
        }

        void BuildSpeedButton(Transform ct)
        {
            if (!SpeedUnlocked) return;
            // pill = hai nửa (trái, phải lật) + viền kem phía sau; cao theo speedPillSize
            float hw = _cfg.speedPillSize.x * 0.5f, asp = _cfg.speedPillSize.y * _canvasSize.y / (hw * _canvasSize.x);
            var c0 = _cfg.speedButtonCenter;
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
            if (_hint != null) _hint.text = _mode >= 0 ? BoosterHint[_mode] : "";
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

        public bool UseVipBus(int slot)
        {
            if (!CanUse(VipBus)) return false;
            if (!Game.VipBus(slot)) { if (Vibrate != null) Vibrate(); return false; }
            Spent(VipBus, "VIP Bus ô " + slot); return true;
        }

        public bool UseBusClaw(int col, int pos)
        {
            if (!CanUse(BusClaw)) return false;
            if (!Game.Claw(col, pos)) { if (Vibrate != null) Vibrate(); return false; }
            Spent(BusClaw, "Bus Claw cột " + col + " vị trí " + pos); return true;
        }

        public bool UseSkyGrab(int color)
        {
            if (!CanUse(SkyGrab)) return false;
            var r = Game.SkyGrab(color);
            if (!r.Matched) { if (Vibrate != null) Vibrate(); return false; }
            Spent(SkyGrab, "Sky Grab màu " + color + " -> " + r.Pixels.Count + " pixel");
            SpawnHeli(r);
            StartFlights(r, _cfg.heliDuration * 0.35f);
            return true;
        }

        public bool UseMagicWand(int color)
        {
            if (!CanUse(MagicWand)) return false;
            int slot = -1;
            for (int i = 0; i < 3; i++)
            {
                var c = Game.Slots[i];
                if (c != null && !c.Locked && c.Color != color && c.Reserved == c.Boarded && (slot < 0 || c.Free > Game.Slots[slot].Free)) slot = i;
            }
            if (slot < 0 || !Game.MagicWand(slot, color)) { if (Vibrate != null) Vibrate(); return false; }
            Spent(MagicWand, "Magic Wand ô " + slot + " -> màu " + color); return true;
        }

        void SpawnHeli(TapResult r)
        {
            var go = new GameObject("Heli"); go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = _a.heliMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mat("Custom/BuzzyEnvironment", new Color32(0xFF, 0xC8, 0x20, 255));
            go.transform.localScale = Vector3.one * _cfg.heliScale;
            var wing = new GameObject("Wing"); wing.transform.SetParent(go.transform, false);
            wing.AddComponent<MeshFilter>().sharedMesh = _a.heliWingMesh;
            wing.AddComponent<MeshRenderer>().sharedMaterial = Mat("Custom/BuzzyEnvironment", new Color32(0xDD, 0xDD, 0xEE, 255));
            wing.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            Vector3 centroid = Vector3.zero; foreach (var d in r.Pixels) centroid += PixelPos(d); centroid /= Mathf.Max(1, r.Pixels.Count);
            float h = _cfg.heliHeight;
            var pts = new[] { new Vector3(-16f, h, centroid.z), centroid + Vector3.up * h, SlotPos(r.Slot) + Vector3.up * h * 0.8f, new Vector3(16f, h, SlotPos(r.Slot).z + 4f) };
            go.transform.localPosition = pts[0];
            _heli.Add(new HeliFx { tr = go.transform, wing = wing.transform, pts = pts, start = Time, dur = _cfg.heliDuration });
        }

        void TickBoosters(float dt)
        {
            for (int i = _heli.Count - 1; i >= 0; i--)
            {
                var h = _heli[i]; float u = Mathf.Clamp01((Time - h.start) / h.dur);
                float f = u * (h.pts.Length - 1); int seg = Mathf.Min(h.pts.Length - 2, (int)f);
                var pos = Vector3.Lerp(h.pts[seg], h.pts[seg + 1], f - seg);
                var dir = pos - h.tr.localPosition;
                h.tr.localPosition = pos;
                if (dir.sqrMagnitude > 1e-6f) h.tr.localRotation = Quaternion.Slerp(h.tr.localRotation, Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z)), 1f - Mathf.Exp(-8f * dt));
                h.wing.localEulerAngles += new Vector3(0f, 1500f * dt, 0f);
                if (u >= 1f) { Kill(h.tr.gameObject); _heli.RemoveAt(i); }
            }
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
            if (Time < _inputOpenAt) return true;
            if (Hit(_cfg.settingsCenter, _cfg.settingsSize, nx, ny)) { OpenSettings(); return true; }
            if (SpeedUnlocked && Hit(_cfg.speedButtonCenter, _cfg.speedButtonSize, nx, ny)) { ToggleSpeed(); return true; }
            for (int i = 0; i < 4; i++)
            {
                if (!Hit(new Vector2(_cfg.boosterX[i], _cfg.boosterY), _cfg.boosterSize, nx, ny)) continue;
                if (!IsUnlocked(i)) { if (Vibrate != null) Vibrate(); Log?.Invoke("Booster " + i + " chưa mở"); return true; }
                if (BoosterCount[i] <= 0) { ShowBuyBooster(i); return true; }
                _mode = _mode == i ? -1 : i; RefreshBoosterUi();
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
            if (_mode == SkyGrab) { if (pass != null) UseSkyGrab(pass.d.Color); else CancelMode(); return; }
            if (_mode == MagicWand) { if (pass != null) UseMagicWand(pass.d.Color); else CancelMode(); return; }
            if (pass != null) TapPixel(pass);
        }

        void CancelMode() { _mode = -1; RefreshBoosterUi(); }

        /// <summary>Chạm một pixel: băng / ẩn không chạm được (rung), còn lại chạm cả cụm màu.</summary>
        void TapPixel(Pass p)
        {
            if (!Game.Board.Takeable(p.d.X, p.d.Y)) { if (Vibrate != null) Vibrate(); Log?.Invoke("Chạm pixel bị băng / ẩn (" + p.d.X + "," + p.d.Y + ") -> rung"); return; }
            TapColor(p.d.Color);
        }
    }
}
