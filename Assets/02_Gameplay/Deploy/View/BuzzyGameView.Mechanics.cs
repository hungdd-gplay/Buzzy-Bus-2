using System.Collections.Generic;
using UnityEngine;
using Buzzy.Core;

namespace Buzzy.Game
{
    /// <summary>M3: hình + chuyển động cho Mystery, Frozen, Walls, Spawner, Key + xe xích (xem design-decisions.md mục 9-12).</summary>
    public partial class BuzzyGameView
    {
        class IceV { public Transform tr; public TextMesh[] texts; public int last = -1; }
        class SpawnV { public TextMesh[] texts; public int last = -1; }
        class Fade { public Transform tr; public float start, dur; public Vector3 from; }
        class Reveal { public Pass p; public float start; }
        class Vanish { public Pass p; public float start; }

        readonly Dictionary<int, IceV> _ice = new Dictionary<int, IceV>();
        readonly Dictionary<int, Transform> _keys = new Dictionary<int, Transform>();
        readonly Dictionary<int, SpawnV> _spawn = new Dictionary<int, SpawnV>();
        readonly List<Fade> _fades = new List<Fade>();
        readonly List<Reveal> _reveals = new List<Reveal>();
        readonly List<Vanish> _vanish = new List<Vanish>();
        Material _hiddenMat;
        int _spawnIndex;

        /// <summary>Xoá pass khỏi bảng tra, chỉ khi ô đó chưa bị pixel sinh mới chiếm.</summary>
        void ForgetPass(Pass p)
        {
            Pass cur; int key = p.d.X * 1000 + p.d.Y;
            if (_pass.TryGetValue(key, out cur) && cur == p) _pass.Remove(key);
        }

        void SubscribeMechanics()
        {
            Game.FrozenBroken += OnFrozenBroken;
            Game.MysteryRevealed += OnMysteryRevealed;
            Game.PixelSpawned += OnPixelSpawned;
            Game.KeyCollected += OnKeyCollected;
            Game.CarUnlocked += OnCarUnlocked;
            Game.CarDisplaced += OnCarDisplaced;
            Game.CarClawed += OnCarClawed;
            Game.CarRecolored += OnCarRecolored;
            Game.PixelsVanished += OnPixelsVanished;
        }

        // ---------------------------------------------------------------- dựng
        Vector3 RectCenter(RectData r, float y = 0f)
        {
            return new Vector3(_cellX0 + (r.X + (r.W - 1) * 0.5f) * CellX, y, _boardZ0 + (r.Y + (r.H - 1) * 0.5f) * CellZ);
        }

        GameObject Cube(string name, Color c, Vector3 center, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(go.GetComponent<Collider>());
            go.name = name; go.transform.SetParent(_root, false);
            go.transform.localPosition = center + new Vector3(0f, size.y * 0.5f, 0f); go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat("Custom/BuzzyEnvironment", c);
            return go;
        }

        void BuildMechanics()
        {
            var b = Game.Board;
            // Mystery: pixel ẩn dùng shader Mystery của BuzzyPassenger (alpha < 0.5)
            _hiddenMat = Mat("Custom/BuzzyPassenger", new Color(0.5f, 0.5f, 0.6f, 0f));
            foreach (var kv in _pass)
            {
                var d = kv.Value.d;
                if (b.IsHidden(d.X, d.Y)) { kv.Value.hidden = true; kv.Value.mr.sharedMaterial = _hiddenMat; }
            }
            // Walls: khối tường dọc theo ô (kể cả y = -1 quanh khay)
            foreach (var w in b.WallList)
                Cube("Wall", _cfg.wallColor, RectCenter(w), new Vector3(w.W * CellX, _cfg.wallHeight * _cellScale, w.H * CellZ));   // M7b: cao theo tỉ lệ ô
            // Spawner: thân máy + số đợt còn lại
            for (int i = 0; i < b.SpawnerList.Count; i++)
            {
                var sp = b.SpawnerList[i];
                var m = Cube("Spawner" + i, _cfg.spawnerColor, RectCenter(sp.Machine), new Vector3(sp.Machine.W * CellX, _cfg.spawnerHeight * _cellScale, sp.Machine.H * CellZ));
                var tx = AddRoofNumber(m.transform, 0);
                var root = m.transform.Find("Seats"); root.localPosition = new Vector3(0f, 0.55f, 0f);
                root.localScale = new Vector3(1f / m.transform.localScale.x, 1f / m.transform.localScale.y, 1f / m.transform.localScale.z) * 2f;
                _spawn[i] = new SpawnV { texts = tx };
            }
            // Frozen: khối băng trong mờ phủ vùng, số hit còn lại ở giữa (V28)
            for (int i = 0; i < b.FrozenAreas.Count; i++)
            {
                var r = b.FrozenAreas[i].Rect;
                var root = new GameObject("Ice" + i); root.transform.SetParent(_root, false);
                root.transform.localPosition = RectCenter(r);
                var mesh = new GameObject("IceMesh"); mesh.transform.SetParent(root.transform, false);
                mesh.AddComponent<MeshFilter>().sharedMesh = _a.iceMesh;
                var mat = Mat("Custom/BuzzyEnvironment", _cfg.iceColor);
                mat.SetFloat("_SrcBlend", 5f); mat.SetFloat("_DstBlend", 10f); mat.SetFloat("_ZWrite", 0f); mat.renderQueue = 3000;
                mesh.AddComponent<MeshRenderer>().sharedMaterial = mat;
                mesh.transform.localScale = new Vector3(r.W * CellX / 1.93f, _cfg.iceYScale * _cellScale, r.H * CellZ / 1.73f);
                var tx = AddRoofNumber(root.transform, 0);
                var nr = root.transform.Find("Seats"); nr.localPosition = new Vector3(0f, 1.5f, 0f); nr.localScale = Vector3.one * (_cfg.iceNumberSize / _cfg.roofNumberSize);
                _ice[i] = new IceV { tr = root.transform, texts = tx };
            }
            // Key: chìa vàng 3x3 nằm trên bảng (V29)
            for (int i = 0; i < b.KeyList.Count; i++)
            {
                var go = new GameObject("Key" + i); go.transform.SetParent(_root, false);
                go.transform.localPosition = RectCenter(b.KeyList[i].Rect, 0.4f * _cellScale);
                go.AddComponent<MeshFilter>().sharedMesh = _a.keyMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = Mat("Custom/BuzzyEnvironment", _cfg.keyColor);
                go.transform.localScale = Vector3.one * _cfg.keyScale * _cellScale; go.transform.localEulerAngles = new Vector3(70f, 0f, 0f);   // M7b: chìa theo tỉ lệ ô
                _keys[i] = go.transform;
            }
        }

        void HideWorldTexts()
        {
            foreach (var t in _root.GetComponentsInChildren<TextMesh>(true)) t.gameObject.SetActive(false);
        }

        void AddLockOverlay(CarV cv)
        {
            var go = new GameObject("Lock"); go.transform.SetParent(cv.tr, false);
            go.transform.localPosition = new Vector3(0f, 1.7f, 0.05f); go.transform.localEulerAngles = new Vector3(90f, 0f, 0f); go.transform.localScale = Vector3.one * _cfg.lockScale;
            go.AddComponent<MeshFilter>().sharedMesh = _a.lockMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mat("Custom/BuzzyEnvironment", _cfg.keyColor);
            cv.lockObj = go;
            foreach (var t in cv.texts) t.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- sự kiện
        void OnFrozenBroken(int i)
        {
            Log?.Invoke("t=" + Time.ToString("0.00") + " Băng " + i + " vỡ");
            Sfx(_a.sfxIceBreak);   // M7
            IceV v; if (!_ice.TryGetValue(i, out v)) return;
            _ice.Remove(i);
            _fades.Add(new Fade { tr = v.tr, start = Time, dur = 0.3f, from = v.tr.localScale });
        }

        void OnMysteryRevealed(PixelData d)
        {
            Pass p; if (!_pass.TryGetValue(d.X * 1000 + d.Y, out p)) return;
            p.hidden = false; p.mr.sharedMaterial = PassMat(d.Color);
            _reveals.Add(new Reveal { p = p, start = Time });
        }

        // Tạo pass muộn tới đầu khung kế (sau StartFlights): pixel sinh ra có thể trùng ô với pixel đang bị lấy trong cùng lần chạm.
        readonly List<PixelData> _spawnQueue = new List<PixelData>();
        void OnPixelSpawned(PixelData d) { _spawnQueue.Add(d); }

        void OnKeyCollected(int i, int color)
        {
            Log?.Invoke("t=" + Time.ToString("0.00") + " Nhặt chìa " + i + " (màu " + color + ")");
            Transform t; if (!_keys.TryGetValue(i, out t)) return;
            _keys.Remove(i);
            _fades.Add(new Fade { tr = t, start = Time, dur = 0.5f, from = t.localScale });
        }

        void OnCarUnlocked(Car car)
        {
            Log?.Invoke("t=" + Time.ToString("0.00") + " Xe #" + car.Id + " hết xích");
            var cv = ViewOf(car); if (cv == null || cv.lockObj == null) return;
            _fades.Add(new Fade { tr = cv.lockObj.transform, start = Time, dur = 0.35f, from = cv.lockObj.transform.localScale });
            foreach (var t in cv.texts) t.gameObject.SetActive(true);
            cv.lockObj = null;
        }

        CarV _displaced;
        void OnCarDisplaced(Car old, int queue, int pos)
        {
            _displaced = ViewOf(old);
            if (_displaced != null) for (int i = 0; i < 3; i++) if (_slotCar[i] == _displaced) _slotCar[i] = null;
        }

        void OnCarClawed(Car car, int queue, int pos, int dest)
        {
            var q = _queue[queue]; if (pos >= q.Count) return;
            var cv = q[pos];
            if (_displaced != null)
            {
                q[pos] = _displaced;   // xe bị đẩy về đúng chỗ xe vừa kẹp
                _displaced.col = queue; _displaced.tr.localScale = Vector3.one * (_cfg.queueBusScale * _cfg.busScale);
                var dl = _displaced.tr.localPosition;
                StartMove(_displaced, new[] { dl, dl + Vector3.up * _cfg.clawLift, QueuePos(queue, pos) + Vector3.up * _cfg.clawLift, QueuePos(queue, pos) }, _cfg.clawDuration, Quaternion.identity, true, 0f);
                _displaced = null;
            }
            else q.RemoveAt(pos);
            if (cv.car != car) Log?.Invoke("CẢNH BÁO: xe kẹp không khớp hàng chờ");
            cv.col = dest; cv.slotPos = SlotPos(dest); _slotCar[dest] = cv;
            cv.tr.localScale = Vector3.one * _cfg.busScale;
            var from = cv.tr.localPosition; var lift = Vector3.up * _cfg.clawLift;
            StartMove(cv, new[] { from, from + lift, SlotPos(dest) + lift, SlotPos(dest) }, _cfg.clawDuration, SlotRot, true, 0f);
            if (q.Count > 0 && q[Mathf.Min(pos, q.Count - 1)].move == null)
                for (int k = pos; k < q.Count; k++)
                    StartMove(q[k], new[] { q[k].tr.localPosition, QueuePos(queue, k) }, _cfg.queueToSlotDuration, Quaternion.identity, true, 0.3f);
            Log?.Invoke("t=" + Time.ToString("0.00") + " Bus Claw: xe #" + car.Id + " từ cột " + queue + " vào ô " + dest);
        }

        void OnCarRecolored(Car car, int oldColor)
        {
            var cv = ViewOf(car); if (cv == null) return;
            cv.mr.sharedMaterial.color = BuzzyPalette.Get(car.Color);
            foreach (var t in cv.texts) t.text = Mathf.Max(0, car.Remaining).ToString();
            Log?.Invoke("t=" + Time.ToString("0.00") + " Magic Wand: xe #" + car.Id + " " + oldColor + " -> " + car.Color);
        }

        void OnPixelsVanished(List<PixelData> list)
        {
            foreach (var d in list)
            {
                Pass p; if (!_pass.TryGetValue(d.X * 1000 + d.Y, out p)) continue;
                p.flying = true; _vanish.Add(new Vanish { p = p, start = Time });
            }
        }

        // ---------------------------------------------------------------- mỗi khung
        void TickMechanics(float dt)
        {
            var b = Game.Board;
            foreach (var d in _spawnQueue) CreatePass(d, Time + 0.03f * ((_spawnIndex++) % 8));
            _spawnQueue.Clear();
            foreach (var kv in _ice)
            {
                int left = b.FrozenLeft[kv.Key];
                if (left != kv.Value.last) { kv.Value.last = left; foreach (var t in kv.Value.texts) t.text = left.ToString(); }
            }
            foreach (var kv in _spawn)
            {
                var sp = b.SpawnerList[kv.Key]; int left = sp.Waves.Length - b.SpawnWave[kv.Key];
                if (left != kv.Value.last) { kv.Value.last = left; foreach (var t in kv.Value.texts) t.text = left.ToString(); }
            }
            foreach (var kv in _keys) kv.Value.localPosition = new Vector3(kv.Value.localPosition.x, 0.4f + 0.15f * Mathf.Sin(Time * 3f + kv.Key), kv.Value.localPosition.z);
            for (int i = _reveals.Count - 1; i >= 0; i--)
            {
                var r = _reveals[i]; float u = Mathf.Clamp01((Time - r.start) / _cfg.revealPopDuration);
                if (r.p.tr != null && !r.p.flying) r.p.tr.localScale = BlockScale * (1f + 0.3f * Mathf.Sin(Mathf.PI * u));
                if (u >= 1f) _reveals.RemoveAt(i);
            }
            for (int i = _fades.Count - 1; i >= 0; i--)
            {
                var f = _fades[i]; float u = Mathf.Clamp01((Time - f.start) / f.dur);
                if (f.tr != null) f.tr.localScale = f.from * (1f - u * u);
                if (u >= 1f) { if (f.tr != null) Kill(f.tr.gameObject); _fades.RemoveAt(i); }
            }
            for (int i = _vanish.Count - 1; i >= 0; i--)
            {
                var v = _vanish[i]; float u = Mathf.Clamp01((Time - v.start) / _cfg.vanishDuration);
                if (v.p.tr != null) v.p.tr.localScale = BlockScale * (1f - u);
                if (u >= 1f) { if (v.p.tr != null) Kill(v.p.tr.gameObject); ForgetPass(v.p); _vanish.RemoveAt(i); }
            }
            TickBoosters(dt);
        }
    }
}
