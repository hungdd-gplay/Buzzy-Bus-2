using System;
using UnityEngine;
using UnityEngine.UI;
using Buzzy.Core;

namespace Buzzy.Game
{
    /// <summary>
    /// M7 (góp ý 03): cảm giác chơi: âm thanh (A*), phản hồi chạm / xe (J*), bảng thử nghiệm Editor (T*). Số ở BuzzyLayoutConfig mục "Cảm giác chơi".
    /// Ánh xạ âm thanh gốc → sự kiện là [thiết kế] (design-decisions mục 17), đổi clip ở BuzzyAssets.
    /// </summary>
    public partial class BuzzyGameView
    {
        /// <summary>Nguồn phát âm (BuzzyBootstrap gắn); null = im lặng (bộ chụp edit-mode).</summary>
        public BuzzyAudio Audio;
        /// <summary>T1: bảng thử nghiệm (<, Lv, >, R) — BuzzyBootstrap bật trong Editor / bản development; chọn level, chơi lại.</summary>
        public bool DevPanel;
        public int DevMaxLevel = 300;
        public Action<int> DevLevelRequested;
        public Action DevRestartRequested;

        int _popIdx; float _lastPopAt = -10f;
        class Skid { public MeshRenderer mr; public Transform tr; public float at; public Vector3 start, dir; }
        Texture2D _skidTex;
        readonly System.Collections.Generic.List<Skid> _skids = new System.Collections.Generic.List<Skid>();
        int _shakeColor = -1; float _shakeAt = -10f; int _shakeLogs; float _shakeLogT = -1f;

        void Sfx(AudioClip clip, float vol = 1f, float pitch = 1f)
        {
            if (Audio == null || clip == null) return;
            Audio.Play(clip, vol * _cfg.sfxVolume, pitch);
        }

        void FeelLog(string s) { Log?.Invoke("t=" + Time.ToString("0.00") + " FEEL " + s); }

        /// <summary>Áp cài đặt Sound / Music (popup cài đặt) vào nguồn phát.</summary>
        public void ApplyAudio()
        {
            if (Audio == null) return;
            EnsureMeta(); Audio.SoundOn = Meta.Sound; Audio.MusicOn = Meta.Music;
        }

        // ---------------------------------------------------------------- móc từ luồng chơi
        /// <summary>A4 + A5 + J5: chạm đúng: tiếng cụm, rung tuỳ chọn; xe vừa đủ chỗ → bubble + lắc.</summary>
        void FeelTap(TapResult r)
        {
            Sfx(_a.sfxTap, _cfg.tapVolume); FeelLog("chạm cụm: tap_passenger, nhấc " + r.Pixels.Count + " pixel lên " + _cfg.pickLift);
            if (_cfg.hapticOnTap && Vibrate != null) Vibrate();
            var cv = ViewOf(r.Car);
            if (cv != null && cv.car.IsFull && cv.fullAt == -1f) { cv.fullAt = Time; Sfx(_a.sfxBubble, 0.8f); FeelLog("xe #" + cv.car.Id + " đầy: bubble + lắc yaw " + _cfg.fullWiggle + "°"); }
        }

        /// <summary>A4 + J6: chạm sai: tiếng choose-wrong, cụm màu đó rung ngang (rung máy ở TapColor, C21).</summary>
        void FeelWrong(int color)
        {
            Sfx(_a.sfxWrong, _cfg.wrongVolume); _shakeColor = color; _shakeAt = Time; _shakeLogs = 0;
            FeelLog("chạm sai màu " + color + ": choose-wrong + cụm rung ngang " + _cfg.wrongShake);
        }

        /// <summary>A3 + J2 + J4: mỗi khách lên xe: pop cao độ tăng dần, xe nhún, số trên nóc bật to.</summary>
        void FeelArrive(CarV cv)
        {
            if (cv == null) return;
            if (Time - _lastPopAt > _cfg.popResetGap) _popIdx = 0;
            float pitch = _cfg.popPitchBase + _cfg.popPitchStep * Mathf.Min(_popIdx, _cfg.popPitchSteps);
            if (Time - _lastPopAt >= _cfg.popMinInterval)
            {
                Sfx(_a.sfxPop, _cfg.popVolume, pitch); _lastPopAt = Time;
                if (_popIdx < 2 || _popIdx == _cfg.popPitchSteps) FeelLog("pop khách " + (_popIdx + 1) + " cao độ " + pitch.ToString("0.00"));
            }
            _popIdx++;
            // góp ý 07: không nhún lại khi đang nhún / cách lần trước < carBounceMinInterval (tránh rung); số bật cũng vậy
            if (cv.bounceAt < 0f && Time - cv.lastBounceAt >= _cfg.carBounceMinInterval)
            {
                cv.bounceAt = Time; cv.lastBounceAt = Time; cv.bounceAmp = _cfg.carBounce;
                if (_cfg.boardRockPitch != 0f) cv.pitches.Add(new Pitch { at = Time, amp = _cfg.boardRockPitch, dur = _cfg.carBounceDuration });   // J2 đợt 2: lắc chúi mũi thấy được ở 20 fps (gốc _boardRockPitchAngle 10)
            }
            if (cv.numPopAt < 0f) cv.numPopAt = Time;
        }

        /// <summary>J3: xe đỗ xong (vào bến) nhún mạnh hơn.</summary>
        void FeelParked(CarV cv)
        {
            cv.bounceAt = Time; cv.bounceAmp = _cfg.parkBounce;
            for (int i = 0; i < 3; i++) if (_slotCar[i] == cv) Game.MarkParked(i);   // [thiết kế] tapWhileArriving: từ đây xe chạm được dù cờ tắt
        }

        /// <summary>A6: xe hàng chờ bắt đầu xuống ô trống.</summary>
        void FeelCarEnter(CarV cv) { Sfx(_a.sfxChangeFalling, 0.6f); FeelLog("xe #" + cv.car.Id + " xuống bến: change_falling"); }

        /// <summary>A6: xe rời bến.</summary>
        void FeelDepart(CarV cv) { FeelLog("xe #" + cv.car.Id + " rời bến: lùi vòng rồi phóng (bus_leave lúc phóng)"); }

        /// <summary>C13 (góp ý 05): 2 vệt bánh tối trên mặt đường từ điểm xe phóng đi, mờ dần.</summary>
        void SpawnSkid(Vector3 carCenter, Vector3 dir, float at)
        {
            if (_cfg.skidLength <= 0f) return;
            if (_skidTex == null)
            {   // gradient alpha dọc vệt: đậm ở chỗ xuất phát, mờ dần về cuối
                _skidTex = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int i = 0; i < 32; i++) _skidTex.SetPixel(0, i, new Color(1f, 1f, 1f, 1f - i / 31f));
                _skidTex.Apply();
            }
            var start = carCenter - dir * _cfg.skidRearOffset;   // bánh sau (ngược hướng phóng)
            for (int side = -1; side <= 1; side += 2)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); UnityEngine.Object.Destroy(q.GetComponent<Collider>()); q.name = "Skid"; q.transform.SetParent(_root, false);
                var s0 = start + Vector3.Cross(Vector3.up, dir) * (side * _cfg.skidHalfTrack);
                q.transform.localRotation = Quaternion.LookRotation(Vector3.down, dir);
                q.transform.localPosition = new Vector3(s0.x, 0.03f, s0.z); q.transform.localScale = new Vector3(_cfg.skidWidth, 0.001f, 1f);
                var m = new Material(Shader.Find("Sprites/Default")); m.mainTexture = _skidTex; m.color = _cfg.skidColor; var mr = q.GetComponent<MeshRenderer>(); mr.sharedMaterial = m;
                _skids.Add(new Skid { mr = mr, tr = q.transform, at = at, start = s0, dir = dir });
            }
            FeelLog("vệt bánh từ x=" + start.x.ToString("0.0") + " lúc t=" + at.ToString("0.00"));
        }

        // ---------------------------------------------------------------- mỗi khung
        void TickFeel(float dt)
        {
            for (int i = _skids.Count - 1; i >= 0; i--)
            {
                var sk = _skids[i]; if (sk.mr == null) { _skids.RemoveAt(i); continue; }
                float g = Mathf.Clamp01((Time - sk.at) / Mathf.Max(0.05f, _cfg.skidGrow));       // mọc dần theo xe phóng
                float len = Mathf.Max(0.001f, _cfg.skidLength * g); var c0 = sk.start + sk.dir * (len * 0.5f);
                sk.tr.localPosition = new Vector3(c0.x, 0.03f, c0.z); sk.tr.localScale = new Vector3(_cfg.skidWidth, len, 1f);
                float u = (Time - sk.at - _cfg.skidGrow) / Mathf.Max(0.1f, _cfg.skidFade);
                if (u >= 1f) { Kill(sk.mr.gameObject); _skids.RemoveAt(i); continue; }
                var c = _cfg.skidColor; if (u > 0f) c.a *= 1f - u * u; if (Time < sk.at) c.a = 0f; sk.mr.sharedMaterial.color = c;
            }
            // V14 (góp ý 10): bóng xe: phẳng trên đường, xoay theo yaw xe, lệch phải-dưới cố định (hướng sáng), cỡ theo cỡ xe
            foreach (var cv in _cars)
            {
                if (cv.shadow == null || cv.tr == null) continue;
                float yaw = cv.tr.eulerAngles.y; var p = cv.tr.position;
                cv.shadow.position = new Vector3(p.x + _cfg.carShadowOffset.x, 0.012f, p.z + _cfg.carShadowOffset.y);
                cv.shadow.rotation = Quaternion.Euler(90f, yaw, 0f);
                float s = cv.tr.lossyScale.x / Mathf.Max(0.01f, cv.baseScale > 0f ? cv.baseScale : 1f);
                cv.shadow.localScale = new Vector3(_cfg.carShadowSize.x / Mathf.Max(0.01f, cv.tr.lossyScale.x), _cfg.carShadowSize.y / Mathf.Max(0.01f, cv.tr.lossyScale.z), 1f) * (cv.baseScale > 0f ? cv.baseScale : 1f);
            }
            // góp ý 06: chúi / ngửa mũi xe (gỡ góc khung trước, áp góc khung này)
            foreach (var cv in _cars)
            {
                if (cv.tr == null || cv.pitches.Count == 0) continue;
                float p = 0f;
                for (int k = cv.pitches.Count - 1; k >= 0; k--)
                {
                    var pc = cv.pitches[k]; float u = (Time - pc.at) / Mathf.Max(0.05f, pc.dur);
                    if (u >= 1f) { cv.pitches.RemoveAt(k); continue; }
                    if (u > 0f) p += pc.amp * Mathf.Sin(u * Mathf.PI) * (1f - 0.5f * u);
                }
                var q = Quaternion.Euler(p, 0f, 0f);
                cv.tr.localRotation = cv.tr.localRotation * Quaternion.Inverse(cv.lastPitch) * q; cv.lastPitch = q;
            }
            foreach (var cv in _cars)
            {
                if (cv.tr == null) continue;
                // J2 / J3: nhún (ép y, nở x-z) rồi về cỡ gốc
                if (cv.bounceAt >= 0f)
                {
                    float s = cv.baseScale > 0f ? cv.baseScale : cv.tr.localScale.x;
                    float u = (Time - cv.bounceAt) / Mathf.Max(0.01f, _cfg.carBounceDuration);
                    // J2 đợt 3 (3c đo: ép cỡ + chúi 5° chỉ đổi ≤ 2 px / 648): thêm nhấc cả xe lên theo Y (carBounceLift, ≥ 5 % cao xe), gỡ độ nhấc cũ trước khi áp mới
                    float lift = 0f;
                    if (u >= 1f) { cv.bounceAt = -1f; cv.tr.localScale = Vector3.one * s; }
                    else { float k = Mathf.Sin(u * Mathf.PI) * (1f - u) * cv.bounceAmp; cv.tr.localScale = new Vector3(s * (1f + k * 0.5f), s * (1f - k), s * (1f + k * 0.5f)); lift = Mathf.Sin(u * Mathf.PI) * (1f - u) * (cv.bounceAmp == _cfg.parkBounce ? 0f : _cfg.carBounceLift); }
                    cv.tr.localPosition += Vector3.up * (lift - cv.lastLift); cv.lastLift = lift;
                }
                // J4: số trên nóc
                if (cv.numPopAt >= 0f && cv.texts != null && cv.texts.Length > 0)
                {
                    var root = cv.texts[cv.texts.Length - 1].transform;
                    float u = (Time - cv.numPopAt) / Mathf.Max(0.01f, _cfg.numberPopDuration);
                    if (u >= 1f) { cv.numPopAt = -1f; root.localScale = Vector3.one; }
                    else root.localScale = Vector3.one * (1f + _cfg.numberPop * (1f - u));
                }
                // J5: xe đầy lắc yaw quanh hướng đỗ, chỉ khi đứng yên ở bến
                if (cv.fullAt >= 0f)
                {
                    if (cv.leaving || cv.move != null) { cv.fullAt = -2f; continue; }
                    float u = (Time - cv.fullAt) / Mathf.Max(0.01f, _cfg.fullWiggleDuration);
                    if (u >= 1f) { cv.fullAt = -2f; cv.tr.localRotation = SlotRot; }
                    else cv.tr.localRotation = SlotRot * Quaternion.Euler(0f, _cfg.fullWiggle * Mathf.Sin(u * Mathf.PI * 2f * _cfg.fullWiggleCycles) * (1f - u), 0f);
                }
            }
        }

        /// <summary>J6: độ lệch ngang của pixel màu đang rung vì chạm sai (0 khi không rung).</summary>
        float WrongShakeOffset(int color)
        {
            if (color != _shakeColor) return 0f;
            float w = (Time - _shakeAt) / Mathf.Max(0.01f, _cfg.wrongShakeDuration);
            if (w >= 1f) return 0f;   // khung cuối: về gốc; Tick tắt _shakeColor sau vòng pixel
            float o = _cfg.wrongShake * Mathf.Sin(w * Mathf.PI * 2f * _cfg.wrongShakeCycles) * (1f - w);
            // lưu ý khi soi ảnh: rung 10 Hz, chụp 10 / 20 fps có thể rơi đúng điểm qua 0 → phải chụp 60 fps (r3_tap60)
            if (_shakeLogs < 4 && Time != _shakeLogT) { _shakeLogs++; _shakeLogT = Time; FeelLog("rung cụm màu " + color + " lệch x=" + o.ToString("0.000")); }
            return o;
        }

        /// <summary>J1: độ nhấc của pixel đã được chọn nhưng chưa tới lượt đi (0 → pickLift trong pickLiftDuration).</summary>
        float PickLift(float tapAt)
        {
            float l = _cfg.pickLiftDuration > 0f ? Mathf.Clamp01((Time - tapAt) / _cfg.pickLiftDuration) : 1f;
            return _cfg.pickLift * (1f - (1f - l) * (1f - l));
        }

        // ---------------------------------------------------------------- T1: bảng thử nghiệm
        void BuildDevPanel(Transform ct)
        {
            if (!DevPanel) return;
            string[] names = { "<", "Lv " + (DisplayLevel > 0 ? DisplayLevel : _lv.index), ">", "R" };
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector2(_cfg.devPanelX[i], _cfg.devPanelY);
                bool lbl = i == 1;
                var b = Img("Dev" + i, _a.whiteFrame, lbl ? new Color(0.15f, 0.15f, 0.25f, 0.7f) : new Color(0.95f, 0.55f, 0.2f, 0.9f), ct, c, lbl ? _cfg.devButtonSize * 1.7f : _cfg.devButtonSize, lbl ? 0.55f : 1f);
                Label(names[i], b, new Vector2(b.sizeDelta.x * 0.5f, -b.sizeDelta.y * 0.5f), (int)((lbl ? 34 : 50) * _canvasSize.x / 1080f), Color.white);
            }
        }

        /// <summary>Bộ chụp edit-mode: chạm UI theo toạ độ % khung (0..1, y từ trên) như người chơi.</summary>
        public bool DebugUiTap(float nx, float ny) { return UiTap(nx, ny); }

        bool DevTap(float nx, float ny)
        {
            if (!DevPanel) return false;
            int cur = DisplayLevel > 0 ? DisplayLevel : _lv.index;
            for (int i = 0; i < 4; i++)
            {
                if (i == 1 || !Hit(new Vector2(_cfg.devPanelX[i], _cfg.devPanelY), _cfg.devButtonSize, nx, ny)) continue;
                Sfx(_a.sfxButton);
                if (i == 3) { FeelLog("DEV: chơi lại"); DevRestartRequested?.Invoke(); }
                else
                {
                    int n = Mathf.Clamp(cur + (i == 0 ? -1 : 1), 1, Mathf.Max(1, DevMaxLevel));
                    if (n != cur) { FeelLog("DEV: sang level " + n); DevLevelRequested?.Invoke(n); }
                }
                return true;
            }
            return false;
        }
    }
}
